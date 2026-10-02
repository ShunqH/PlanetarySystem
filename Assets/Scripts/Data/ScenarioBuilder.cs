using System;
using System.Collections.Generic;
using PlanetSystem.Physics;

namespace PlanetSystem.Data
{
    /// <summary>
    /// Turns a <see cref="Scenario"/> into physical bodies. Element-defined bodies follow the Add-body rules
    /// (and Rebound's default): the first body sits at the origin; every later one is placed relative to its
    /// primary with mu = G (M + m), and the system is shifted to its barycenter after each body. Massive
    /// bodies always use the barycenter of the massive bodies added so far as primary; test particles may
    /// instead name an earlier massive body. State-defined bodies are copied as is.
    /// </summary>
    public static class ScenarioBuilder
    {
        public static SimulationState Build(Scenario scenario, int firstId = 1)
        {
            var state = new SimulationState();
            int id = firstId;
            for (int index = 0; index < scenario.Bodies.Count; index++)
            {
                var sb = scenario.Bodies[index];
                var body = new Body(id++, sb.Name, sb.Kind, sb.Mass, sb.Radius);
                if (sb.UsesElements)
                {
                    int reference = sb.Kind == BodyKind.Massive ? -1 : sb.ReferenceIndex;
                    if (TryGetPrimary(state, reference, index, out var rp, out var rv, out var rm))
                    {
                        double mu = Constants.G * (rm + body.Mass);
                        OrbitConversion.RelativeStateFromElements(sb.Elements, mu, out var r, out var v);
                        body.Position = rp + r;
                        body.Velocity = rv + v;
                    }
                }
                else
                {
                    body.Position = sb.Position;
                    body.Velocity = sb.Velocity;
                }
                state.Bodies.Add(body);
                if (sb.UsesElements) state.MoveToCenterOfMass();
            }
            state.MoveToCenterOfMass();
            return state;
        }

        private static bool TryGetPrimary(SimulationState state, int referenceIndex, int selfIndex,
            out Vec3d position, out Vec3d velocity, out double mass)
        {
            if (referenceIndex >= 0 && referenceIndex < selfIndex)
            {
                var b = state.Bodies[referenceIndex];
                if (b.Mass > 0.0)
                {
                    position = b.Position;
                    velocity = b.Velocity;
                    mass = b.Mass;
                    return true;
                }
            }
            return state.TryGetCenterOfMass(b => b.IsMassive, out position, out velocity, out mass);
        }

        /// <summary>Validates a scenario before loading; returns null when fine, otherwise a message.</summary>
        public static string Validate(Scenario s)
        {
            if (s.Bodies.Count == 0) return "The case has no bodies.";
            for (int i = 0; i < s.Bodies.Count; i++)
            {
                var b = s.Bodies[i];
                if (b.Kind == BodyKind.Massive && !(b.Mass > 0.0)) return $"{b.Name}: massive bodies need a positive mass.";
                if (!(b.Radius > 0.0)) return $"{b.Name}: radius must be positive.";
                if (b.UsesElements && i > 0)
                {
                    if (!(b.Elements.SemiMajorAxis > 0.0)) return $"{b.Name}: semi-major axis must be positive.";
                    if (b.Elements.Eccentricity < 0.0 || b.Elements.Eccentricity >= 1.0) return $"{b.Name}: eccentricity must be in [0, 1).";
                }
                if (!b.UsesElements && !(IsFinite(b.Position) && IsFinite(b.Velocity))) return $"{b.Name}: invalid position or velocity.";
            }
            return null;
        }

        private static bool IsFinite(Vec3d v) =>
            !(double.IsNaN(v.X) || double.IsNaN(v.Y) || double.IsNaN(v.Z) ||
              double.IsInfinity(v.X) || double.IsInfinity(v.Y) || double.IsInfinity(v.Z));
    }
}
