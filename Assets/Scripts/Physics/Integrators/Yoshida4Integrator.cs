using System;

namespace PlanetSystem.Physics.Integrators
{
    /// <summary>
    /// Fourth-order symplectic integrator built by composing three leapfrog steps with Yoshida's (1990)
    /// weights w1 = 1 / (2 - 2^(1/3)), w0 = 1 - 2 w1 (a.k.a. Forest-Ruth). Three force evaluations per step,
    /// but the error scales as dt^4, so it allows much larger steps for the same accuracy.
    /// </summary>
    public sealed class Yoshida4Integrator : FixedStepIntegrator
    {
        private static readonly double W1 = 1.0 / (2.0 - Math.Pow(2.0, 1.0 / 3.0));
        private static readonly double W0 = 1.0 - 2.0 * W1;

        // Drift coefficients c1..c4 and kick coefficients d1..d3.
        private static readonly double C1 = 0.5 * W1;
        private static readonly double C2 = 0.5 * (W0 + W1);

        public Yoshida4Integrator(double eta) : base(eta) { }

        public override string Name => "Yoshida 4";
        protected override int EvaluationsPerStep => 3;

        protected override void Step(ParticleArrays p, double dt)
        {
            Drift(p, C1 * dt);
            Gravity.Compute(p);
            Kick(p, W1 * dt);
            Drift(p, C2 * dt);
            Gravity.Compute(p);
            Kick(p, W0 * dt);
            Drift(p, C2 * dt);
            Gravity.Compute(p);
            Kick(p, W1 * dt);
            Drift(p, C1 * dt);
        }
    }
}
