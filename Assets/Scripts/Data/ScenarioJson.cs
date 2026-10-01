using System;
using System.Collections.Generic;
using PlanetSystem.Physics;

namespace PlanetSystem.Data
{
    /// <summary>
    /// JSON format of the saved-case library:
    /// <code>
    /// { "format": "PlanetSystem cases", "version": 1, "cases": [ {
    ///     "name": "...", "description": "...", "savedAt": "2026-09-30T15:04:05",
    ///     "integrator": 0, "speed": 1,
    ///     "bodies": [ { "name": "Star A", "kind": "massive", "mass": 1, "radius": 0.00465,
    ///                   "color": [1, 0.85, 0.35], "reference": -1,
    ///                   "position": [x, y, z], "velocity": [vx, vy, vz] }, ... ] } ] }
    /// </code>
    /// Units: AU, yr, Msun. Instead of position/velocity a body may give
    /// "elements": { "a", "e", "inc", "Omega", "omega", "f" } with angles in degrees (convenient for
    /// hand-written files).
    /// </summary>
    public static class ScenarioJson
    {
        public const int Version = 1;

        public static string Serialize(IEnumerable<Scenario> scenarios)
        {
            var cases = new List<object>();
            foreach (var s in scenarios) cases.Add(ToJson(s));
            var root = new Dictionary<string, object>
            {
                ["format"] = "PlanetSystem cases",
                ["version"] = Version,
                ["cases"] = cases,
            };
            return MiniJson.Serialize(root);
        }

        /// <summary>Parses a library file. Malformed individual cases are skipped and reported in <paramref name="warnings"/>.</summary>
        public static List<Scenario> Deserialize(string json, List<string> warnings = null)
        {
            var result = new List<Scenario>();
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) throw new FormatException("Root must be an object.");
            if (!(Get(root, "cases") is List<object> cases)) return result;
            foreach (var item in cases)
            {
                try
                {
                    result.Add(FromJson((Dictionary<string, object>)item));
                }
                catch (Exception e)
                {
                    warnings?.Add($"Skipped a malformed case: {e.Message}");
                }
            }
            return result;
        }

        // ------------------------------------------------------------------

        private static Dictionary<string, object> ToJson(Scenario s)
        {
            var bodies = new List<object>();
            foreach (var b in s.Bodies)
            {
                var o = new Dictionary<string, object>
                {
                    ["name"] = b.Name,
                    ["kind"] = b.Kind == BodyKind.Massive ? "massive" : "test",
                    ["mass"] = b.Mass,
                    ["radius"] = b.Radius,
                    ["color"] = new List<object> { b.Color[0], b.Color[1], b.Color[2] },
                    ["reference"] = b.ReferenceIndex,
                };
                if (b.UsesElements)
                {
                    var el = b.Elements;
                    o["elements"] = new Dictionary<string, object>
                    {
                        ["a"] = el.SemiMajorAxis,
                        ["e"] = el.Eccentricity,
                        ["inc"] = Constants.RadToDeg(el.Inclination),
                        ["Omega"] = Constants.RadToDeg(el.LongitudeOfAscendingNode),
                        ["omega"] = Constants.RadToDeg(el.ArgumentOfPericenter),
                        ["f"] = Constants.RadToDeg(el.TrueAnomaly),
                    };
                }
                else
                {
                    o["position"] = Vec(b.Position);
                    o["velocity"] = Vec(b.Velocity);
                }
                bodies.Add(o);
            }
            return new Dictionary<string, object>
            {
                ["name"] = s.Name,
                ["description"] = s.Description ?? "",
                ["savedAt"] = s.SavedAt ?? "",
                ["integrator"] = s.IntegratorIndex,
                ["speed"] = s.Speed,
                ["bodies"] = bodies,
            };
        }

        private static Scenario FromJson(Dictionary<string, object> o)
        {
            var s = new Scenario
            {
                Name = Str(o, "name", "Case"),
                Description = Str(o, "description", ""),
                SavedAt = Str(o, "savedAt", ""),
                IntegratorIndex = (int)Num(o, "integrator", -1),
                Speed = Num(o, "speed", 0),
            };
            if (!(Get(o, "bodies") is List<object> bodies)) throw new FormatException($"Case '{s.Name}' has no bodies.");
            foreach (var item in bodies)
            {
                var bo = (Dictionary<string, object>)item;
                var b = new ScenarioBody
                {
                    Name = Str(bo, "name", "Body"),
                    Kind = Str(bo, "kind", "massive") == "test" ? BodyKind.TestParticle : BodyKind.Massive,
                    Mass = Num(bo, "mass", 0),
                    Radius = Num(bo, "radius", Constants.EarthRadiusInAu),
                    ReferenceIndex = (int)Num(bo, "reference", -1),
                };
                if (Get(bo, "color") is List<object> c && c.Count >= 3)
                    b.Color = new[] { ToDouble(c[0]), ToDouble(c[1]), ToDouble(c[2]) };
                if (Get(bo, "elements") is Dictionary<string, object> el)
                {
                    b.UsesElements = true;
                    b.Elements = new OrbitalElements
                    {
                        SemiMajorAxis = Num(el, "a", 1),
                        Eccentricity = Num(el, "e", 0),
                        Inclination = Constants.DegToRad(Num(el, "inc", 0)),
                        LongitudeOfAscendingNode = Constants.DegToRad(Num(el, "Omega", 0)),
                        ArgumentOfPericenter = Constants.DegToRad(Num(el, "omega", 0)),
                        TrueAnomaly = Constants.DegToRad(Num(el, "f", 0)),
                    };
                }
                else
                {
                    b.Position = ToVec(Get(bo, "position"));
                    b.Velocity = ToVec(Get(bo, "velocity"));
                }
                s.Bodies.Add(b);
            }
            return s;
        }

        private static List<object> Vec(Vec3d v) => new List<object> { v.X, v.Y, v.Z };

        private static Vec3d ToVec(object o)
        {
            if (o is List<object> l && l.Count >= 3) return new Vec3d(ToDouble(l[0]), ToDouble(l[1]), ToDouble(l[2]));
            return Vec3d.Zero;
        }

        private static object Get(Dictionary<string, object> o, string key) => o.TryGetValue(key, out var v) ? v : null;
        private static string Str(Dictionary<string, object> o, string key, string fallback) => Get(o, key) as string ?? fallback;
        private static double Num(Dictionary<string, object> o, string key, double fallback) => Get(o, key) is double d ? d : fallback;
        private static double ToDouble(object o) => o is double d ? d : 0.0;
    }
}
