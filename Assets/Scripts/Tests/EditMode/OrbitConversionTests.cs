using System;
using NUnit.Framework;

namespace PlanetSystem.Physics.Tests
{
    public class OrbitConversionTests
    {
        private const double Tol = 1e-9;

        [Test]
        public void CircularOrbit_HasKeplerianSpeedAndUnitPeriod()
        {
            var el = OrbitalElements.Circular(1.0);
            double mu = Constants.G; // 1 Msun primary, massless body
            OrbitConversion.RelativeStateFromElements(el, mu, out var r, out var v);

            Assert.AreEqual(1.0, r.Length, Tol);
            Assert.AreEqual(Constants.TwoPi, v.Length, 1e-9, "1 AU circular orbit moves at 2 pi AU/yr");
            Assert.AreEqual(0.0, Vec3d.Dot(r, v), Tol, "velocity perpendicular to radius");
            Assert.AreEqual(1.0, el.Period(mu), Tol);
        }

        [Test]
        public void ElementsRoundTrip_GenericInclinedOrbit()
        {
            var el = new OrbitalElements
            {
                SemiMajorAxis = 2.3,
                Eccentricity = 0.41,
                Inclination = Constants.DegToRad(63.0),
                LongitudeOfAscendingNode = Constants.DegToRad(210.0),
                ArgumentOfPericenter = Constants.DegToRad(75.0),
                TrueAnomaly = Constants.DegToRad(300.0),
            };
            double mu = Constants.G * 1.7;
            OrbitConversion.RelativeStateFromElements(el, mu, out var r, out var v);
            Assert.IsTrue(OrbitConversion.TryElementsFromRelativeState(r, v, mu, out var back));

            Assert.AreEqual(el.SemiMajorAxis, back.SemiMajorAxis, 1e-9);
            Assert.AreEqual(el.Eccentricity, back.Eccentricity, 1e-9);
            Assert.AreEqual(el.Inclination, back.Inclination, 1e-9);
            Assert.AreEqual(el.LongitudeOfAscendingNode, back.LongitudeOfAscendingNode, 1e-9);
            Assert.AreEqual(el.ArgumentOfPericenter, back.ArgumentOfPericenter, 1e-9);
            Assert.AreEqual(el.TrueAnomaly, back.TrueAnomaly, 1e-9);
        }

        [Test]
        public void ElementsRoundTrip_PlanarEccentricOrbit()
        {
            var el = new OrbitalElements
            {
                SemiMajorAxis = 0.5,
                Eccentricity = 0.2,
                ArgumentOfPericenter = Constants.DegToRad(40.0),
                TrueAnomaly = Constants.DegToRad(120.0),
            };
            OrbitConversion.RelativeStateFromElements(el, Constants.G, out var r, out var v);
            Assert.IsTrue(OrbitConversion.TryElementsFromRelativeState(r, v, Constants.G, out var back));

            Assert.AreEqual(0.0, back.Inclination, 1e-9);
            Assert.AreEqual(0.0, back.LongitudeOfAscendingNode, 1e-9);
            // In the planar case omega is measured from the x axis (pomega) since Omega = 0.
            Assert.AreEqual(el.ArgumentOfPericenter, back.ArgumentOfPericenter, 1e-9);
            Assert.AreEqual(el.TrueAnomaly, back.TrueAnomaly, 1e-9);
        }

        [Test]
        public void ElementsRoundTrip_RetrogradeOrbit()
        {
            var el = new OrbitalElements
            {
                SemiMajorAxis = 1.2,
                Eccentricity = 0.1,
                Inclination = Constants.DegToRad(150.0),
                LongitudeOfAscendingNode = Constants.DegToRad(20.0),
                ArgumentOfPericenter = Constants.DegToRad(250.0),
                TrueAnomaly = Constants.DegToRad(10.0),
            };
            OrbitConversion.RelativeStateFromElements(el, Constants.G, out var r, out var v);
            Assert.IsTrue(OrbitConversion.TryElementsFromRelativeState(r, v, Constants.G, out var back));
            Assert.AreEqual(el.Inclination, back.Inclination, 1e-9);
            Assert.AreEqual(el.LongitudeOfAscendingNode, back.LongitudeOfAscendingNode, 1e-9);
            Assert.AreEqual(el.ArgumentOfPericenter, back.ArgumentOfPericenter, 1e-9);
            Assert.AreEqual(el.TrueAnomaly, back.TrueAnomaly, 1e-9);
        }

        [Test]
        public void KeplerEquation_RoundTrips()
        {
            foreach (double e in new[] { 0.0, 0.3, 0.9, 0.99 })
            {
                for (double M = 0.0; M < Constants.TwoPi; M += 0.37)
                {
                    double f = Kepler.TrueFromMean(M, e);
                    double back = Kepler.MeanFromTrue(f, e);
                    Assert.AreEqual(M, back, 1e-10, $"e={e} M={M}");
                }
            }
        }

        [Test]
        public void MeanAnomalySetter_UpdatesTrueAnomaly()
        {
            var el = new OrbitalElements { SemiMajorAxis = 1.0, Eccentricity = 0.5 };
            el.MeanAnomaly = Math.PI; // apocenter
            Assert.AreEqual(Math.PI, el.TrueAnomaly, 1e-9);
        }

        [Test]
        public void UnboundOrbit_IsRejected()
        {
            var r = new Vec3d(1.0, 0.0, 0.0);
            var v = new Vec3d(0.0, 3.0 * Constants.TwoPi, 0.0); // far above escape speed
            Assert.IsFalse(OrbitConversion.TryElementsFromRelativeState(r, v, Constants.G, out _));
        }

        [Test]
        public void MoveToCenterOfMass_ZeroesBarycenter()
        {
            var state = new SimulationState();
            var a = new Body(1, "A", BodyKind.Massive, 1.0, 0.01) { Position = new Vec3d(0, 0, 0) };
            var b = new Body(2, "B", BodyKind.Massive, 0.5, 0.01) { Position = new Vec3d(0.3, 0, 0), Velocity = new Vec3d(0, 1, 0) };
            var t = new Body(3, "T", BodyKind.TestParticle, 0.0, 0.01) { Position = new Vec3d(2, 0, 0) };
            state.Bodies.AddRange(new[] { a, b, t });
            state.MoveToCenterOfMass();

            Assert.IsTrue(state.TryGetCenterOfMass(x => x.IsMassive, out var cp, out var cv, out var m));
            Assert.AreEqual(1.5, m, Tol);
            Assert.AreEqual(0.0, cp.Length, Tol);
            Assert.AreEqual(0.0, cv.Length, Tol);
            Assert.AreEqual(2.0, (t.Position - a.Position).X, Tol, "relative geometry preserved");
        }
    }
}
