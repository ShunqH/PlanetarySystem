using System;
using NUnit.Framework;
using PlanetSystem.Physics.Integrators;

namespace PlanetSystem.Physics.Tests
{
    public class IntegratorTests
    {
        private const double Frame = 1.0 / 60.0; // one rendered frame at 1 yr/s and 60 fps
        private const int Budget = 1_000_000;

        /// <summary>Star of 1 Msun plus a body on the given orbit, moved to the barycenter frame.</summary>
        private static SimulationState TwoBody(OrbitalElements el, BodyKind kind, double mass)
        {
            var state = new SimulationState();
            var star = new Body(1, "Star", BodyKind.Massive, 1.0, Constants.SolarRadiusInAu);
            var body = new Body(2, "Body", kind, mass, Constants.EarthRadiusInAu);
            OrbitConversion.RelativeStateFromElements(el, Constants.G * (1.0 + body.Mass), out var r, out var v);
            body.Position = r;
            body.Velocity = v;
            state.Bodies.Add(star);
            state.Bodies.Add(body);
            state.MoveToCenterOfMass();
            return state;
        }

        private static ParticleArrays Run(SimulationState state, int integrator, double years)
        {
            var p = new ParticleArrays();
            p.Load(state);
            var integ = IntegratorCatalog.Create(integrator, IntegratorOptions.Default);
            integ.Reset(p, Frame);
            int frames = (int)Math.Round(years / Frame);
            for (int f = 0; f < frames; f++)
            {
                var res = integ.Advance(p, Frame, Budget);
                Assert.IsFalse(res.BudgetLimited, "budget should not be hit in tests");
                Assert.IsFalse(res.Collision, "no collision expected");
            }
            p.Store(state);
            return p;
        }

        private static double RelativeEnergyError(SimulationState before, SimulationState after)
        {
            double e0 = Diagnostics.MassiveEnergy(before.Bodies, 0.0);
            double e1 = Diagnostics.MassiveEnergy(after.Bodies, 0.0);
            return Math.Abs((e1 - e0) / e0);
        }

        private static SimulationState Clone(SimulationState s)
        {
            var c = new SimulationState { Time = s.Time };
            foreach (var b in s.Bodies)
                c.Bodies.Add(new Body(b.Id, b.Name, b.Kind, b.Mass, b.Radius) { Position = b.Position, Velocity = b.Velocity });
            return c;
        }

        [Test]
        public void AllIntegrators_ConserveEnergy_JupiterMassPlanet_20yr()
        {
            var el = new OrbitalElements { SemiMajorAxis = 1.0, Eccentricity = 0.3, Inclination = 0.4 };
            double[] tolerance = { 1e-3, 1e-6, 1e-7 };
            for (int k = 0; k < IntegratorCatalog.Count; k++)
            {
                var s0 = TwoBody(el, BodyKind.Massive, Constants.JupiterMassInSolar);
                var s = Clone(s0);
                Run(s, k, 20.0);
                double dE = RelativeEnergyError(s0, s);
                Assert.Less(dE, tolerance[k], $"{IntegratorCatalog.Labels[k]}: |dE/E| = {dE:E2}");
            }
        }

        [Test]
        public void AllIntegrators_ReturnToStart_AfterOnePeriod()
        {
            // Massless planet at 1 AU around 1 Msun: period exactly 1 yr.
            var el = new OrbitalElements { SemiMajorAxis = 1.0, Eccentricity = 0.5, ArgumentOfPericenter = 1.0 };
            double[] tolerance = { 5e-2, 1e-4, 1e-5 };
            for (int k = 0; k < IntegratorCatalog.Count; k++)
            {
                var s = TwoBody(el, BodyKind.TestParticle, 0.0);
                var start = s.Bodies[1].Position;
                Run(s, k, 1.0);
                double miss = (s.Bodies[1].Position - start).Length;
                Assert.Less(miss, tolerance[k], $"{IntegratorCatalog.Labels[k]}: miss = {miss:E2} AU");
                Assert.AreEqual(1.0, s.Time, 1e-9);
            }
        }

        [Test]
        public void Adaptive_HandlesVeryEccentricOrbit()
        {
            var el = new OrbitalElements { SemiMajorAxis = 1.0, Eccentricity = 0.97 };
            var s = TwoBody(el, BodyKind.TestParticle, 0.0);
            var start = s.Bodies[1].Position;
            Run(s, 2, 3.0);
            Assert.Less((s.Bodies[1].Position - start).Length, 1e-5);
        }

        [Test]
        public void TestParticle_DoesNotMoveStar()
        {
            var s = TwoBody(OrbitalElements.Circular(1.0), BodyKind.TestParticle, 0.0);
            Run(s, 1, 2.0);
            Assert.AreEqual(0.0, s.Bodies[0].Position.Length, 1e-15);
            Assert.AreEqual(0.0, s.Bodies[0].Velocity.Length, 1e-15);
        }

        [Test]
        public void FixedStep_DividesFrameExactly()
        {
            var s = TwoBody(OrbitalElements.Circular(0.05), BodyKind.TestParticle, 0.0);
            var p = new ParticleArrays();
            p.Load(s);
            var integ = IntegratorCatalog.Create(0, IntegratorOptions.Default);
            integ.Reset(p, Frame);
            double n = Frame / integ.StepSize;
            Assert.AreEqual(Math.Round(n), n, 1e-9, "frame must be an integer number of steps");
            Assert.Greater(n, 1.0);
            var res = integ.Advance(p, Frame, Budget);
            Assert.AreEqual((int)Math.Round(n), res.Steps);
            Assert.AreEqual(Frame, res.Advanced, 1e-15);
        }

        [Test]
        public void BudgetLimit_IsReported()
        {
            var s = TwoBody(OrbitalElements.Circular(0.05), BodyKind.TestParticle, 0.0);
            var p = new ParticleArrays();
            p.Load(s);
            var integ = IntegratorCatalog.Create(0, IntegratorOptions.Default);
            integ.Reset(p, Frame);
            var res = integ.Advance(p, Frame, 2);
            Assert.IsTrue(res.BudgetLimited);
            Assert.Less(res.Advanced, Frame);
        }

        [Test]
        public void Collision_IsDetected_ForRadialInfall()
        {
            var state = new SimulationState();
            state.Bodies.Add(new Body(1, "Star", BodyKind.Massive, 1.0, Constants.SolarRadiusInAu));
            state.Bodies.Add(new Body(2, "Rock", BodyKind.TestParticle, 0.0, 1e-6) { Position = new Vec3d(0.5, 0, 0) });
            for (int k = 0; k < IntegratorCatalog.Count; k++)
            {
                var s = Clone(state);
                var p = new ParticleArrays();
                p.Load(s);
                var integ = IntegratorCatalog.Create(k, IntegratorOptions.Default);
                integ.Reset(p, Frame);
                bool hit = false;
                for (int f = 0; f < 60 && !hit; f++) hit = integ.Advance(p, Frame, Budget).Collision;
                Assert.IsTrue(hit, $"{IntegratorCatalog.Labels[k]} should report the collision");
                Assert.IsTrue(p.AllFinite());
            }
        }
    }
}
