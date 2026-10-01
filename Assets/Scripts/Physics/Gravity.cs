using System;
using System.Threading.Tasks;

namespace PlanetSystem.Physics
{
    /// <summary>
    /// Direct-summation Newtonian gravity.
    /// Massive-massive pairs are computed once and applied symmetrically: O(Nm^2 / 2).
    /// Test particles only feel the massive bodies: O(Nt * Nm). Test particles never affect anything.
    ///
    /// Large systems are evaluated in parallel: each body's acceleration is summed independently over all
    /// massive bodies (no shared writes, so the result does not depend on the thread count). Small systems
    /// stay serial because starting parallel work costs more than the arithmetic it would save.
    /// </summary>
    public static class Gravity
    {
        /// <summary>
        /// Minimum number of pair interactions per evaluation before the parallel path is used.
        /// Measured on Apple Silicon with Mono: below this, thread hand-off overhead dominates.
        /// </summary>
        public static int ParallelThreshold = 4000;

        /// <summary>Set false to force the serial path (used by tests and benchmarks).</summary>
        public static bool AllowParallel = true;

        private static readonly int WorkerCount = Math.Max(1, Environment.ProcessorCount);

        /// <summary>Fills p.AX/AY/AZ with accelerations [AU/yr^2] at the current positions.</summary>
        public static void Compute(ParticleArrays p)
        {
            long nm = p.MassiveCount, n = p.Count;
            long pairs = nm * (nm - 1) / 2 + (n - nm) * nm;
            if (AllowParallel && WorkerCount > 1 && pairs >= ParallelThreshold) ComputeParallel(p);
            else ComputeSerial(p);
            p.ForceEvaluations++;
        }

        private static void ComputeSerial(ParticleArrays p)
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
            for (int k = nm; k < n; k++) SumOverMassive(p, k, detect);
        }

        /// <summary>
        /// Acceleration of body k from every massive body except itself, written to p.AX/AY/AZ[k].
        /// Touches only index k for writing, so it is safe to call concurrently for different k.
        /// </summary>
        private static void SumOverMassive(ParticleArrays p, int k, bool detect)
        {
            int nm = p.MassiveCount;
            double[] x = p.X, y = p.Y, z = p.Z, m = p.M, rad = p.R;
            double eps2 = p.Softening2;
            double xk = x[k], yk = y[k], zk = z[k], rk = rad[k];
            double sax = 0.0, say = 0.0, saz = 0.0;
            for (int i = 0; i < nm; i++)
            {
                if (i == k) continue;
                double dx = x[i] - xk, dy = y[i] - yk, dz = z[i] - zk;
                double r2 = dx * dx + dy * dy + dz * dz;
                if (detect && p.CollisionA < 0)
                {
                    double rs = rad[i] + rk;
                    if (r2 < rs * rs) p.ReportCollision(i, k);
                }
                r2 += eps2;
                if (r2 <= 0.0) continue;
                double f = Constants.G * m[i] / (r2 * Math.Sqrt(r2));
                sax += f * dx; say += f * dy; saz += f * dz;
            }
            p.AX[k] = sax; p.AY[k] = say; p.AZ[k] = saz;
        }

        private static void ComputeParallel(ParticleArrays p)
        {
            var kernel = p.ParallelKernel ??= new Kernel();
            kernel.Run(p, WorkerCount);
        }

        /// <summary>Reusable parallel job (cached per ParticleArrays so no delegate is allocated per call).</summary>
        internal sealed class Kernel
        {
            private ParticleArrays _p;
            private int _chunk;
            private bool _detect;
            private readonly Action<int> _body;

            public Kernel()
            {
                _body = RunChunk;
            }

            public void Run(ParticleArrays p, int workers)
            {
                _p = p;
                _detect = p.DetectCollisions;
                int chunks = Math.Min(p.Count, workers * 2);
                _chunk = (p.Count + chunks - 1) / chunks;
                Parallel.For(0, chunks, _body);
                _p = null;
            }

            private void RunChunk(int c)
            {
                int start = c * _chunk;
                int end = Math.Min(_p.Count, start + _chunk);
                for (int k = start; k < end; k++) SumOverMassive(_p, k, _detect);
            }
        }
    }
}
