using System;
using System.Collections.Generic;
using PlanetSystem.Physics;

namespace PlanetSystem.Data
{
    /// <summary>
    /// One body of a <see cref="Scenario"/>. A body is defined either by orbital elements relative to a
    /// primary (built-in presets: applied exactly like the Add-body form, including the barycenter shift
    /// after each body), or by an absolute Cartesian state (saved cases: restored bit for bit).
    /// </summary>
    public sealed class ScenarioBody
    {
        public string Name = "Body";
        public BodyKind Kind = BodyKind.Massive;
        /// <summary>Mass [Msun]; ignored for test particles.</summary>
        public double Mass;
        /// <summary>Physical radius [AU].</summary>
        public double Radius;
        /// <summary>Linear RGB in [0, 1].</summary>
        public double[] Color = { 1.0, 1.0, 1.0 };
        /// <summary>Index of the primary in the body list, or -1 for the barycenter of the other massive bodies.</summary>
        public int ReferenceIndex = -1;

        /// <summary>True: use <see cref="Elements"/>. False: use <see cref="Position"/> and <see cref="Velocity"/>.</summary>
        public bool UsesElements;
        public OrbitalElements Elements;
        public Vec3d Position;
        public Vec3d Velocity;

        public ScenarioBody Clone() => (ScenarioBody)MemberwiseClone();
    }

    /// <summary>A complete set of initial conditions that can be loaded into the simulation.</summary>
    public sealed class Scenario
    {
        public string Name = "Case";
        public string Description = "";
        /// <summary>ISO-8601 local time of saving; empty for built-in presets.</summary>
        public string SavedAt = "";
        /// <summary>Built-in presets ship with the app and cannot be deleted or overwritten.</summary>
        public bool IsBuiltIn;
        /// <summary>Integrator to select when loading, or -1 to keep the current choice.</summary>
        public int IntegratorIndex = -1;
        /// <summary>Speed (yr per real second) to select when loading, or 0 to keep the current speed.</summary>
        public double Speed;
        public readonly List<ScenarioBody> Bodies = new List<ScenarioBody>();

        public int MassiveCount
        {
            get { int n = 0; foreach (var b in Bodies) if (b.Kind == BodyKind.Massive) n++; return n; }
        }

        public string Summary
        {
            get
            {
                int massive = MassiveCount;
                string s = $"{massive} massive, {Bodies.Count - massive} test particle{(Bodies.Count - massive == 1 ? "" : "s")}";
                if (!string.IsNullOrEmpty(SavedAt)) s += $"  ·  saved {SavedAt.Replace('T', ' ')}";
                return s;
            }
        }

        public static bool NamesEqual(string a, string b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
