using System;
using System.Collections.Generic;

namespace PlanetSystem.Physics
{
    /// <summary>
    /// Complete physical state of the system: all bodies and the simulation clock.
    /// </summary>
    public sealed class SimulationState
    {
        public readonly List<Body> Bodies = new List<Body>();

        /// <summary>Simulation time in years.</summary>
        public double Time;

        /// <summary>
        /// Center of mass (position, velocity, total mass) of the bodies accepted by the filter.
        /// Returns false when the filtered set has zero total mass.
        /// </summary>
        public bool TryGetCenterOfMass(Func<Body, bool> include, out Vec3d position, out Vec3d velocity, out double totalMass)
        {
            Vec3d p = Vec3d.Zero, v = Vec3d.Zero;
            double m = 0.0;
            foreach (var b in Bodies)
            {
                if (include != null && !include(b)) continue;
                if (b.Mass <= 0.0) continue;
                p += b.Position * b.Mass;
                v += b.Velocity * b.Mass;
                m += b.Mass;
            }
            if (m <= 0.0)
            {
                position = Vec3d.Zero;
                velocity = Vec3d.Zero;
                totalMass = 0.0;
                return false;
            }
            position = p / m;
            velocity = v / m;
            totalMass = m;
            return true;
        }

        /// <summary>
        /// Shifts every body so that the barycenter of all massive bodies is at the origin with zero velocity.
        /// Mirrors Rebound's move_to_com(). No-op when there is no mass in the system.
        /// </summary>
        public void MoveToCenterOfMass()
        {
            if (!TryGetCenterOfMass(b => b.IsMassive, out var cp, out var cv, out _)) return;
            foreach (var b in Bodies)
            {
                b.Position -= cp;
                b.Velocity -= cv;
            }
        }
    }
}
