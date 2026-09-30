namespace PlanetSystem.Physics.Integrators
{
    /// <summary>Outcome of one <see cref="IIntegrator.Advance"/> call.</summary>
    public struct IntegrationResult
    {
        /// <summary>Simulated time actually advanced [yr]. Can be less than requested (budget or collision).</summary>
        public double Advanced;
        /// <summary>Accepted integration steps.</summary>
        public int Steps;
        /// <summary>Force evaluations used.</summary>
        public int Evaluations;
        /// <summary>True when the force-evaluation budget stopped the integrator before the requested time.</summary>
        public bool BudgetLimited;
        /// <summary>True when two bodies overlapped; the integrator stops at that point.</summary>
        public bool Collision;
    }

    /// <summary>
    /// Common interface of all time integrators. An integrator owns whatever internal state it needs
    /// (step size, cached derivatives) and advances a <see cref="ParticleArrays"/> in place.
    /// </summary>
    public interface IIntegrator
    {
        string Name { get; }

        /// <summary>Current (fixed-step) or most recent (adaptive) step size [yr].</summary>
        double StepSize { get; }

        /// <summary>
        /// Prepares the integrator for the given particles. Must be called before the first Advance and
        /// whenever the particles are modified from outside. <paramref name="frameDuration"/> is the
        /// nominal simulated time per rendered frame [yr]; fixed-step integrators choose a step that
        /// divides it exactly so every frame takes the same number of equal steps.
        /// </summary>
        void Reset(ParticleArrays p, double frameDuration);

        /// <summary>
        /// Advances the particles by <paramref name="duration"/> years, using at most
        /// <paramref name="maxForceEvaluations"/> force evaluations.
        /// </summary>
        IntegrationResult Advance(ParticleArrays p, double duration, int maxForceEvaluations);
    }
}
