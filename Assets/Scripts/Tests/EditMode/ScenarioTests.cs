using System;
using System.Collections.Generic;
using NUnit.Framework;
using PlanetSystem.Data;
using PlanetSystem.Physics;

namespace PlanetSystem.Physics.Tests
{
    public class ScenarioTests
    {
        [Test]
        public void Json_RoundTripsDoublesExactly()
        {
            var r = new Random(7);
            for (int k = 0; k < 2000; k++)
            {
                double d = (r.NextDouble() - 0.5) * Math.Pow(10, r.Next(-300, 300));
                Assert.AreEqual(d, (double)MiniJson.Parse(MiniJson.FormatDouble(d)), 0.0);
            }
            Assert.AreEqual(0.1, (double)MiniJson.Parse("0.1"), 0.0);
        }

        [Test]
        public void Json_ParsesNestedStructuresAndEscapes()
        {
            var o = (Dictionary<string, object>)MiniJson.Parse("{ \"a\": [1, -2.5e3, true, null], \"s\": \"x\\\"y\\n\\u00e9\" }");
            var a = (List<object>)o["a"];
            Assert.AreEqual(1.0, a[0]);
            Assert.AreEqual(-2500.0, a[1]);
            Assert.AreEqual(true, a[2]);
            Assert.IsNull(a[3]);
            Assert.AreEqual("x\"y\né", o["s"]);
            Assert.Throws<FormatException>(() => MiniJson.Parse("{ \"a\": 1,, }"));
        }

        [Test]
        public void SavedCase_RoundTripsThroughJsonBitForBit()
        {
            var state = ScenarioBuilder.Build(BuiltInScenarios.SolarSystem());
            var saved = new Scenario { Name = "My \"case\"", SavedAt = "2026-09-30T12:00:00", IntegratorIndex = 2, Speed = 5 };
            foreach (var b in state.Bodies)
                saved.Bodies.Add(new ScenarioBody
                {
                    Name = b.Name, Kind = b.Kind, Mass = b.Mass, Radius = b.Radius, ReferenceIndex = 0,
                    Position = b.Position, Velocity = b.Velocity, Color = new[] { 0.1, 0.2, 0.3 },
                });

            var back = ScenarioJson.Deserialize(ScenarioJson.Serialize(new[] { saved }))[0];
            Assert.AreEqual(saved.Name, back.Name);
            Assert.AreEqual(2, back.IntegratorIndex);
            Assert.AreEqual(5.0, back.Speed);
            Assert.AreEqual(saved.Bodies.Count, back.Bodies.Count);
            for (int i = 0; i < saved.Bodies.Count; i++)
            {
                var a = saved.Bodies[i];
                var b = back.Bodies[i];
                Assert.AreEqual(a.Name, b.Name);
                Assert.AreEqual(a.Kind, b.Kind);
                Assert.AreEqual(a.Mass, b.Mass, 0.0);
                Assert.AreEqual(a.Radius, b.Radius, 0.0);
                Assert.IsTrue(a.Position.Equals(b.Position) && a.Velocity.Equals(b.Velocity), $"{a.Name} state must be bit-identical");
                Assert.IsFalse(b.UsesElements);
            }

            // Rebuilding a state-defined case must reproduce the state exactly.
            var rebuilt = ScenarioBuilder.Build(back);
            for (int i = 0; i < state.Bodies.Count; i++)
                Assert.IsTrue(state.Bodies[i].Position.Equals(rebuilt.Bodies[i].Position));
        }

        [Test]
        public void ElementCase_InJsonUsesDegrees()
        {
            string json = "{\"cases\":[{\"name\":\"hand\",\"bodies\":[" +
                          "{\"name\":\"S\",\"kind\":\"massive\",\"mass\":1,\"radius\":0.005}," +
                          "{\"name\":\"p\",\"kind\":\"test\",\"radius\":0.0001,\"reference\":0,\"elements\":{\"a\":2,\"e\":0.1,\"inc\":30}}]}]}";
            var s = ScenarioJson.Deserialize(json)[0];
            Assert.AreEqual(Constants.DegToRad(30), s.Bodies[1].Elements.Inclination, 1e-15);
            var st = ScenarioBuilder.Build(s);
            OrbitConversion.TryElementsFromRelativeState(st.Bodies[1].Position - st.Bodies[0].Position,
                st.Bodies[1].Velocity - st.Bodies[0].Velocity, Constants.G, out var el);
            Assert.AreEqual(2.0, el.SemiMajorAxis, 1e-12);
        }

        [Test]
        public void MalformedCase_IsSkippedWithWarning()
        {
            var warnings = new List<string>();
            var list = ScenarioJson.Deserialize("{\"cases\":[{\"name\":\"bad\"},{\"name\":\"ok\",\"bodies\":[]}]}", warnings);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("ok", list[0].Name);
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void BuiltIns_AreValidAndMatchSpecification()
        {
            foreach (var s in BuiltInScenarios.All)
            {
                Assert.IsNull(ScenarioBuilder.Validate(s), s.Name);
                Assert.IsTrue(s.IsBuiltIn);
            }

            var solar = BuiltInScenarios.SolarSystem();
            Assert.AreEqual(9, solar.MassiveCount, "Sun + 8 planets are massive");
            Assert.AreEqual(10, solar.Bodies.Count, "plus one massless comet");

            var kl = BuiltInScenarios.KozaiLidov();
            Assert.AreEqual(2, kl.MassiveCount);
            Assert.AreEqual(3, kl.Bodies.Count);
            var kls = ScenarioBuilder.Build(kl);
            // Binary: circular, a = 1 AU, symmetric about the barycenter.
            Assert.AreEqual(1.0, (kls.Bodies[1].Position - kls.Bodies[0].Position).Length, 1e-12);
            Assert.AreEqual(0.0, (kls.Bodies[0].Position + kls.Bodies[1].Position).Length, 1e-12);
            // Particle: circular at 0.1 AU around Star A, 60 deg to the binary plane.
            var rp = kls.Bodies[2].Position - kls.Bodies[0].Position;
            var vp = kls.Bodies[2].Velocity - kls.Bodies[0].Velocity;
            OrbitConversion.TryElementsFromRelativeState(rp, vp, Constants.G * 0.5, out var kel);
            Assert.AreEqual(0.1, kel.SemiMajorAxis, 1e-12);
            Assert.AreEqual(Constants.DegToRad(60.0), kel.Inclination, 1e-12);

            var polar = ScenarioBuilder.Build(BuiltInScenarios.PolarPlanets());
            var a = polar.Bodies[0];
            var b = polar.Bodies[1];
            OrbitConversion.TryElementsFromRelativeState(b.Position - a.Position, b.Velocity - a.Velocity, Constants.G, out var bin);
            Assert.AreEqual(1.0, bin.SemiMajorAxis, 1e-12);
            Assert.AreEqual(0.8, bin.Eccentricity, 1e-12);
            // Planet orbit normals are parallel to the binary eccentricity vector (+x): polar alignment.
            for (int k = 2; k < 4; k++)
            {
                var h = Vec3d.Cross(polar.Bodies[k].Position, polar.Bodies[k].Velocity);
                Assert.AreEqual(1.0, h.X / h.Length, 1e-6);
            }
        }

        [Test]
        public void Builder_MassiveBodiesAlwaysOrbitTheBarycenter()
        {
            var s = new Scenario();
            s.Bodies.Add(new ScenarioBody { Name = "A", Mass = 1.0, Radius = 0.005, UsesElements = true });
            s.Bodies.Add(new ScenarioBody { Name = "B", Mass = 1.0, Radius = 0.005, UsesElements = true, Elements = OrbitalElements.Circular(1.0) });
            // C names B as its primary, but massive bodies must ignore that and orbit the A+B barycenter.
            var withRef = new ScenarioBody { Name = "C", Mass = 0.1, Radius = 0.005, ReferenceIndex = 1, UsesElements = true, Elements = OrbitalElements.Circular(5.0) };
            s.Bodies.Add(withRef);
            var a = ScenarioBuilder.Build(s);
            withRef.ReferenceIndex = -1;
            var b = ScenarioBuilder.Build(s);
            for (int i = 0; i < 3; i++) Assert.IsTrue(a.Bodies[i].Position.Equals(b.Bodies[i].Position), s.Bodies[i].Name);
        }

        [Test]
        public void ParallelGravity_MatchesSerial()
        {
            var r = new Random(5);
            var s = new SimulationState();
            for (int i = 0; i < 300; i++)
                s.Bodies.Add(new Body(i, "b", i < 40 ? BodyKind.Massive : BodyKind.TestParticle, 1e-3 * (1 + r.NextDouble()), 1e-9)
                {
                    Position = new Vec3d(r.NextDouble() * 10 - 5, r.NextDouble() * 10 - 5, r.NextDouble() - 0.5),
                });
            var p = new ParticleArrays();
            p.Load(s);
            int saved = Gravity.ParallelThreshold;
            try
            {
                Gravity.ParallelThreshold = int.MaxValue;
                Gravity.Compute(p);
                var serial = (double[])p.AX.Clone();
                Gravity.ParallelThreshold = 0;
                Gravity.Compute(p);
                for (int i = 0; i < p.Count; i++)
                    Assert.AreEqual(serial[i], p.AX[i], 1e-12 * Math.Max(1.0, Math.Abs(serial[i])));
            }
            finally
            {
                Gravity.ParallelThreshold = saved;
            }
        }
    }
}
