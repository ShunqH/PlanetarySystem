using System;

namespace PlanetSystem.Physics
{
    /// <summary>
    /// Estimates the shortest dynamical timescale in the system, used to choose integration step sizes.
    ///
    /// Naively treating every pair as an isolated two-body orbit fails for hierarchical systems: a
    /// circumbinary planet has no meaningful Keplerian orbit around one star of the binary, and that
    /// fictitious orbit can have a pericenter of almost zero. Instead, for every body we take the minimum of
    /// <list type="number">
    /// <item>sqrt(r^3 / mu) for every (massive, other) pair at the current separation, which resolves
    /// close approaches and satellites;</item>
    /// <item>the pericenter timescale sqrt(rp^3 / mu) of the body's orbit around its <em>primary</em>:
    /// the dominant attractor if it supplies at least <see cref="DominanceFraction"/> of the pull, otherwise
    /// the barycenter of all other massive bodies (the same rule used to draw orbits). For an unbound
    /// primary orbit the crossing time r / v is used instead.</item>
    /// </list>
    /// Pericenters are never taken below the sum of physical radii (the bodies would collide first).
    /// </summary>
    public static class Timescales
    {
        /// <summary>Fraction of the summed pull a single body must supply to count as the primary.</summary>
        public const double DominanceFraction = 0.9;

        public static double ShortestTimescale(ParticleArrays p)
        {
            int n = p.Count, nm = p.MassiveCount;
            if (nm == 0 || n < 2) return double.PositiveInfinity;

            // Barycenter of all massive bodies (individual bodies are subtracted below).
            double mt = 0.0, cx = 0.0, cy = 0.0, cz = 0.0, cvx = 0.0, cvy = 0.0, cvz = 0.0;
            for (int i = 0; i < nm; i++)
            {
                double m = p.M[i];
                mt += m;
                cx += m * p.X[i]; cy += m * p.Y[i]; cz += m * p.Z[i];
                cvx += m * p.VX[i]; cvy += m * p.VY[i]; cvz += m * p.VZ[i];
            }

            double best = double.PositiveInfinity;
            for (int j = 0; j < n; j++)
            {
                // (1) Instantaneous pair timescales, and find the dominant attractor.
                int dominant = -1;
                double strongest = 0.0, sum = 0.0;
                for (int i = 0; i < nm; i++)
                {
                    if (i == j) continue;
                    double dx = p.X[j] - p.X[i], dy = p.Y[j] - p.Y[i], dz = p.Z[j] - p.Z[i];
                    double r = Math.Max(Math.Sqrt(dx * dx + dy * dy + dz * dz), MinSeparation(p, i, j));
                    double mu = Constants.G * (p.M[i] + p.M[j]);
                    if (mu > 0.0) best = Math.Min(best, Math.Sqrt(r * r * r / mu));
                    double pull = p.M[i] / (r * r);
                    sum += pull;
                    if (pull > strongest) { strongest = pull; dominant = i; }
                }
                if (dominant < 0) continue;

                // (2) Pericenter timescale of the orbit around the primary.
                double px, py, pz, pvx, pvy, pvz, pm, rMin;
                if (strongest >= DominanceFraction * sum)
                {
                    px = p.X[dominant]; py = p.Y[dominant]; pz = p.Z[dominant];
                    pvx = p.VX[dominant]; pvy = p.VY[dominant]; pvz = p.VZ[dominant];
                    pm = p.M[dominant];
                    rMin = MinSeparation(p, dominant, j);
                }
                else
                {
                    double mj = j < nm ? p.M[j] : 0.0;
                    pm = mt - mj;
                    if (pm <= 0.0) continue;
                    px = (cx - mj * p.X[j]) / pm; py = (cy - mj * p.Y[j]) / pm; pz = (cz - mj * p.Z[j]) / pm;
                    pvx = (cvx - mj * p.VX[j]) / pm; pvy = (cvy - mj * p.VY[j]) / pm; pvz = (cvz - mj * p.VZ[j]) / pm;
                    rMin = Math.Max(p.R[j], 1e-12);
                }
                double mOrbit = Constants.G * (pm + (j < nm ? p.M[j] : 0.0));
                best = Math.Min(best, OrbitTimescale(
                    p.X[j] - px, p.Y[j] - py, p.Z[j] - pz,
                    p.VX[j] - pvx, p.VY[j] - pvy, p.VZ[j] - pvz, mOrbit, rMin));
            }
            return best;
        }

        private static double MinSeparation(ParticleArrays p, int i, int j) => Math.Max(p.R[i] + p.R[j], 1e-12);

        /// <summary>Pericenter timescale of a bound relative orbit, or the crossing time r / v when unbound.</summary>
        private static double OrbitTimescale(double dx, double dy, double dz, double dvx, double dvy, double dvz, double mu, double rMin)
        {
            if (mu <= 0.0) return double.PositiveInfinity;
            double r = Math.Max(Math.Sqrt(dx * dx + dy * dy + dz * dz), rMin);
            double v2 = dvx * dvx + dvy * dvy + dvz * dvz;
            double energy = 0.5 * v2 - mu / r;
            if (energy < 0.0)
            {
                double a = -mu / (2.0 * energy);
                double hx = dy * dvz - dz * dvy, hy = dz * dvx - dx * dvz, hz = dx * dvy - dy * dvx;
                double e2 = Math.Max(0.0, 1.0 - (hx * hx + hy * hy + hz * hz) / (mu * a));
                double rp = Math.Max(a * (1.0 - Math.Sqrt(e2)), rMin);
                return Math.Sqrt(rp * rp * rp / mu);
            }
            return v2 > 0.0 ? r / Math.Sqrt(v2) : double.PositiveInfinity;
        }
    }
}
