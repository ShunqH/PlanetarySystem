using System;

namespace PlanetSystem.Physics.Integrators
{
    /// <summary>
    /// Base class for fixed-step (symplectic) integrators.
    /// The step is dt = frameDuration / n with n = ceil(frameDuration / (eta * tau_min)), so the step
    /// never exceeds eta times the shortest dynamical timescale and stays constant while the speed is
    /// unchanged. A constant step is what makes symplectic integrators conserve energy over long times.
    /// Time that does not fill a whole step is carried over to the next call.
    /// </summary>
    public abstract class FixedStepIntegrator : IIntegrator
    {
        /// <summary>Step size as a fraction of the shortest dynamical timescale.</summary>
        protected readonly double Eta;

        private double _dt;
        private double _frameDuration;
        private double _carry;

        protected FixedStepIntegrator(double eta)
        {
            Eta = eta;
        }

        public abstract string Name { get; }

        /// <summary>Force evaluations performed by one call to <see cref="Step"/>.</summary>
        protected abstract int EvaluationsPerStep { get; }

        /// <summary>Advances the particles by exactly one step of size dt.</summary>
        protected abstract void Step(ParticleArrays p, double dt);

        public double StepSize => _dt;

        public void Reset(ParticleArrays p, double frameDuration)
        {
            _frameDuration = Math.Max(frameDuration, 1e-12);
            _carry = 0.0;
            ChooseStep(Timescales.ShortestTimescale(p));
        }

        private void ChooseStep(double tau)
        {
            double target = Eta * tau;
            if (double.IsNaN(target) || double.IsInfinity(target) || target <= 0.0) target = _frameDuration;
            double n = Math.Max(1.0, Math.Ceiling(_frameDuration / target));
            _dt = _frameDuration / n;
        }

        public IntegrationResult Advance(ParticleArrays p, double duration, int maxForceEvaluations)
        {
            var result = new IntegrationResult();

            // Safety net: if an orbit has become much tighter (e.g. eccentricity pumped by Kozai-Lidov
            // cycles), shrink the step. This is rare and only ever decreases dt.
            double target = Eta * Timescales.ShortestTimescale(p);
            if (target < 0.5 * _dt) ChooseStep(target / Eta);

            _carry += duration;
            long wanted = (long)Math.Floor(_carry / _dt + 1e-9);
            long allowed = Math.Max(1, maxForceEvaluations / EvaluationsPerStep);
            if (wanted > allowed)
            {
                wanted = allowed;
                result.BudgetLimited = true;
            }

            p.DetectCollisions = true;
            p.ClearCollision();
            long before = p.ForceEvaluations;
            int steps = 0;
            for (; steps < wanted; steps++)
            {
                Step(p, _dt);
                p.Time += _dt;
                if (p.HasCollision) { steps++; result.Collision = true; break; }
            }

            _carry -= steps * _dt;
            // When the CPU budget limits us, drop the backlog instead of accumulating an ever-growing lag.
            if (result.BudgetLimited || result.Collision) _carry = Math.Min(Math.Max(_carry, 0.0), _dt);

            result.Steps = steps;
            result.Advanced = steps * _dt;
            result.Evaluations = (int)(p.ForceEvaluations - before);
            return result;
        }

        // ------------------------------------------------------------------
        // Building blocks shared by the symplectic schemes
        // ------------------------------------------------------------------

        /// <summary>x += v * h for all particles.</summary>
        protected static void Drift(ParticleArrays p, double h)
        {
            int n = p.Count;
            double[] x = p.X, y = p.Y, z = p.Z, vx = p.VX, vy = p.VY, vz = p.VZ;
            for (int i = 0; i < n; i++)
            {
                x[i] += vx[i] * h;
                y[i] += vy[i] * h;
                z[i] += vz[i] * h;
            }
        }

        /// <summary>v += a * h for all particles, using the accelerations stored in p.</summary>
        protected static void Kick(ParticleArrays p, double h)
        {
            int n = p.Count;
            double[] vx = p.VX, vy = p.VY, vz = p.VZ, ax = p.AX, ay = p.AY, az = p.AZ;
            for (int i = 0; i < n; i++)
            {
                vx[i] += ax[i] * h;
                vy[i] += ay[i] * h;
                vz[i] += az[i] * h;
            }
        }
    }
}
