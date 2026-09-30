namespace PlanetSystem.Physics.Integrators
{
    /// <summary>
    /// Second-order symplectic leapfrog in drift-kick-drift form (the same scheme as Rebound's LEAPFROG).
    /// One force evaluation per step. Energy errors stay bounded (no secular drift) but orbital phases
    /// slowly accumulate an error proportional to dt^2. The cheapest choice.
    /// </summary>
    public sealed class LeapfrogIntegrator : FixedStepIntegrator
    {
        public LeapfrogIntegrator(double eta) : base(eta) { }

        public override string Name => "Leapfrog";
        protected override int EvaluationsPerStep => 1;

        protected override void Step(ParticleArrays p, double dt)
        {
            Drift(p, 0.5 * dt);
            Gravity.Compute(p);
            Kick(p, dt);
            Drift(p, 0.5 * dt);
        }
    }
}
