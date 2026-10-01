using System;

namespace PlanetSystem.Physics
{
    /// <summary>
    /// Flat structure-of-arrays copy of the bodies used by the integrators (cache friendly, no allocations
    /// in the hot loop). Massive bodies always occupy indices [0, MassiveCount); test particles follow.
    /// Load() copies from a <see cref="SimulationState"/>; Store() writes positions, velocities and time back.
    /// </summary>
    public sealed class ParticleArrays
    {
        public int Count;
        public int MassiveCount;

        public double[] X = Array.Empty<double>(), Y = Array.Empty<double>(), Z = Array.Empty<double>();
        public double[] VX = Array.Empty<double>(), VY = Array.Empty<double>(), VZ = Array.Empty<double>();
        public double[] AX = Array.Empty<double>(), AY = Array.Empty<double>(), AZ = Array.Empty<double>();
        public double[] M = Array.Empty<double>();
        public double[] R = Array.Empty<double>();
        public Body[] Source = Array.Empty<Body>();

        /// <summary>Simulation time in years.</summary>
        public double Time;

        /// <summary>Plummer softening length squared [AU^2]. Zero means pure Newtonian gravity.</summary>
        public double Softening2;

        /// <summary>When true, the force evaluation records the first pair whose physical radii overlap.</summary>
        public bool DetectCollisions = true;

        /// <summary>Indices of the first colliding pair found, or -1.</summary>
        public int CollisionA = -1, CollisionB = -1;

        /// <summary>Total number of force evaluations performed since the last Load().</summary>
        public long ForceEvaluations;

        public bool HasCollision => CollisionA >= 0;

        /// <summary>Cached parallel gravity job (see <see cref="Gravity"/>).</summary>
        internal Gravity.Kernel ParallelKernel;

        private readonly object _collisionLock = new object();

        /// <summary>Records a colliding pair if none has been recorded yet. Thread-safe.</summary>
        public void ReportCollision(int a, int b)
        {
            lock (_collisionLock)
            {
                if (CollisionA >= 0) return;
                CollisionA = Math.Min(a, b);
                CollisionB = Math.Max(a, b);
            }
        }

        public void ClearCollision()
        {
            CollisionA = -1;
            CollisionB = -1;
        }

        public void Load(SimulationState state)
        {
            int n = state.Bodies.Count;
            EnsureCapacity(n);
            Count = n;
            int k = 0;
            // Massive bodies first, then test particles, preserving their relative order.
            foreach (var b in state.Bodies) if (b.IsMassive) Put(k++, b);
            MassiveCount = k;
            foreach (var b in state.Bodies) if (!b.IsMassive) Put(k++, b);
            Time = state.Time;
            ForceEvaluations = 0;
            ClearCollision();
        }

        public void Store(SimulationState state)
        {
            for (int i = 0; i < Count; i++)
            {
                var b = Source[i];
                b.Position = new Vec3d(X[i], Y[i], Z[i]);
                b.Velocity = new Vec3d(VX[i], VY[i], VZ[i]);
            }
            state.Time = Time;
        }

        /// <summary>False if any position or velocity became NaN or infinite.</summary>
        public bool AllFinite()
        {
            for (int i = 0; i < Count; i++)
            {
                if (!IsFinite(X[i]) || !IsFinite(Y[i]) || !IsFinite(Z[i]) ||
                    !IsFinite(VX[i]) || !IsFinite(VY[i]) || !IsFinite(VZ[i])) return false;
            }
            return true;
        }

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        private void Put(int i, Body b)
        {
            Source[i] = b;
            X[i] = b.Position.X; Y[i] = b.Position.Y; Z[i] = b.Position.Z;
            VX[i] = b.Velocity.X; VY[i] = b.Velocity.Y; VZ[i] = b.Velocity.Z;
            AX[i] = AY[i] = AZ[i] = 0.0;
            M[i] = b.IsMassive ? b.Mass : 0.0;
            R[i] = b.Radius;
        }

        private void EnsureCapacity(int n)
        {
            if (X.Length >= n) return;
            X = new double[n]; Y = new double[n]; Z = new double[n];
            VX = new double[n]; VY = new double[n]; VZ = new double[n];
            AX = new double[n]; AY = new double[n]; AZ = new double[n];
            M = new double[n]; R = new double[n];
            Source = new Body[n];
        }
    }
}
