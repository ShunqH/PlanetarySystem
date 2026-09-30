namespace PlanetSystem.Physics.Integrators
{
    /// <summary>Accuracy knobs for the integrators. See <see cref="IntegratorCatalog"/>.</summary>
    public struct IntegratorOptions
    {
        /// <summary>Leapfrog step as a fraction of the shortest dynamical timescale.</summary>
        public double LeapfrogEta;
        /// <summary>Yoshida step as a fraction of the shortest dynamical timescale.</summary>
        public double YoshidaEta;
        /// <summary>Relative per-step error tolerance of the adaptive integrator.</summary>
        public double AdaptiveTolerance;

        public static IntegratorOptions Default => new IntegratorOptions
        {
            LeapfrogEta = 0.03,
            YoshidaEta = 0.06,
            AdaptiveTolerance = 1e-10,
        };
    }

    /// <summary>The integrators offered in the UI, ordered from cheapest to most expensive.</summary>
    public static class IntegratorCatalog
    {
        public static readonly string[] Labels =
        {
            "Leapfrog  (2nd order, fast)",
            "Yoshida  (4th order, symplectic)",
            "Dormand-Prince  (5th order, adaptive)",
        };

        public static readonly string[] Descriptions =
        {
            "Leapfrog: 1 force evaluation per step, symplectic. Cheapest; energy error stays bounded, phases drift slowly.",
            "Yoshida 4: 3 force evaluations per step, symplectic, 4th order. Much more accurate than leapfrog at similar cost.",
            "Dormand-Prince 5(4): adaptive step, 6 evaluations per step. Handles close encounters and e -> 1; not symplectic.",
        };

        public static int Count => Labels.Length;

        public static IIntegrator Create(int index, IntegratorOptions options)
        {
            switch (index)
            {
                case 1: return new Yoshida4Integrator(options.YoshidaEta);
                case 2: return new DormandPrince5Integrator(options.AdaptiveTolerance);
                default: return new LeapfrogIntegrator(options.LeapfrogEta);
            }
        }
    }
}
