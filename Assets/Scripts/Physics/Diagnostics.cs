using System;
using System.Collections.Generic;

namespace PlanetSystem.Physics
{
    /// <summary>Conserved quantities used to monitor integration accuracy.</summary>
    public static class Diagnostics
    {
        /// <summary>
        /// Total energy (kinetic + potential) of the massive bodies, in Msun AU^2 / yr^2.
        /// Test particles are massless and do not contribute. Uses the same softening as the force.
        /// </summary>
        public static double MassiveEnergy(IReadOnlyList<Body> bodies, double softening2)
        {
            double kinetic = 0.0, potential = 0.0;
            for (int i = 0; i < bodies.Count; i++)
            {
                var a = bodies[i];
                if (!a.IsMassive) continue;
                kinetic += 0.5 * a.Mass * a.Velocity.LengthSquared;
                for (int j = i + 1; j < bodies.Count; j++)
                {
                    var b = bodies[j];
                    if (!b.IsMassive) continue;
                    double r = Math.Sqrt((a.Position - b.Position).LengthSquared + softening2);
                    if (r > 0.0) potential -= Constants.G * a.Mass * b.Mass / r;
                }
            }
            return kinetic + potential;
        }
    }
}
