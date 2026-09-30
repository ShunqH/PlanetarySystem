using System;

namespace PlanetSystem.Physics
{
    /// <summary>
    /// Direct-summation Newtonian gravity.
    /// Massive-massive pairs are computed once and applied symmetrically: O(Nm^2 / 2).
    /// Test particles only feel the massive bodies: O(Nt * Nm). Test particles never affect anything.
    /// </summary>
    public static class Gravity
    {
        /// <summary>Fills p.AX/AY/AZ with accelerations [AU/yr^2] at the current positions.</summary>
        public static void Compute(ParticleArrays p)
        {
            int n = p.Count, nm = p.MassiveCount;
            double[] x = p.X, y = p.Y, z = p.Z, ax = p.AX, ay = p.AY, az = p.AZ, m = p.M, rad = p.R;
            double eps2 = p.Softening2;
            bool detect = p.DetectCollisions;
            const double G = Constants.G;

            Array.Clear(ax, 0, n);
            Array.Clear(ay, 0, n);
            Array.Clear(az, 0, n);

            // Massive <-> massive
            for (int i = 0; i < nm; i++)
            {
                double xi = x[i], yi = y[i], zi = z[i];
                double gmi = G * m[i];
                for (int j = i + 1; j < nm; j++)
                {
                    double dx = x[j] - xi, dy = y[j] - yi, dz = z[j] - zi;
                    double r2 = dx * dx + dy * dy + dz * dz;
                    if (detect && p.CollisionA < 0)
                    {
                        double rs = rad[i] + rad[j];
                        if (r2 < rs * rs) { p.CollisionA = i; p.CollisionB = j; }
                    }
                    r2 += eps2;
                    if (r2 <= 0.0) continue;
                    double inv3 = 1.0 / (r2 * Math.Sqrt(r2));
                    double fi = G * m[j] * inv3; // pull on i toward j
                    double fj = gmi * inv3;      // pull on j toward i
                    ax[i] += fi * dx; ay[i] += fi * dy; az[i] += fi * dz;
                    ax[j] -= fj * dx; ay[j] -= fj * dy; az[j] -= fj * dz;
                }
            }

            // Massive -> test particles
            for (int k = nm; k < n; k++)
            {
                double xk = x[k], yk = y[k], zk = z[k];
                double sax = 0.0, say = 0.0, saz = 0.0;
                for (int i = 0; i < nm; i++)
                {
                    double dx = x[i] - xk, dy = y[i] - yk, dz = z[i] - zk;
                    double r2 = dx * dx + dy * dy + dz * dz;
                    if (detect && p.CollisionA < 0)
                    {
                        double rs = rad[i] + rad[k];
                        if (r2 < rs * rs) { p.CollisionA = i; p.CollisionB = k; }
                    }
                    r2 += eps2;
                    if (r2 <= 0.0) continue;
                    double f = G * m[i] / (r2 * Math.Sqrt(r2));
                    sax += f * dx; say += f * dy; saz += f * dz;
                }
                ax[k] = sax; ay[k] = say; az[k] = saz;
            }

            p.ForceEvaluations++;
        }
    }
}
