using System.Collections.Generic;
using PlanetSystem.Physics;

namespace PlanetSystem.Data
{
    /// <summary>
    /// Preset systems shipped with the app. Bodies are defined by orbital elements and applied in order,
    /// exactly as if added through the Add-body form (elements relative to the given primary, then a shift
    /// to the barycenter after every body).
    /// </summary>
    public static class BuiltInScenarios
    {
        public const string SolarSystemName = "Solar System";
        public const string KozaiLidovName = "Kozai–Lidov";
        public const string PolarPlanetsName = "Polar Planets";

        public static IReadOnlyList<Scenario> All => new[] { SolarSystem(), KozaiLidov(), PolarPlanets() };

        public static Scenario ByName(string name)
        {
            foreach (var s in All) if (Scenario.NamesEqual(s.Name, name)) return s;
            return null;
        }

        // ------------------------------------------------------------------
        // Solar System
        // ------------------------------------------------------------------

        /// <summary>
        /// Sun, the eight planets (all massive) and a massless Halley-like comet.
        /// Planet orbits: JPL approximate heliocentric ecliptic elements at J2000 (Standish, valid 1800-2050),
        /// applied relative to the barycenter of the bodies added so far (massive bodies always orbit the
        /// barycenter), which differs from heliocentric placement by at most ~0.005 AU (the Sun's offset
        /// once Jupiter is added). Masses and mean radii are the real values; the reference plane is the
        /// J2000 ecliptic. The massless comet orbits the Sun.
        /// </summary>
        public static Scenario SolarSystem()
        {
            var s = new Scenario
            {
                Name = SolarSystemName,
                IsBuiltIn = true,
                Speed = 1.0,
                Description = "The Sun and the eight planets at J2000 with real masses and radii, plus a massless Halley-like comet on a retrograde, highly eccentric orbit.",
            };
            s.Bodies.Add(Star("Sun", 1.0, 1.0, 1.00, 0.85, 0.35));

            // name, mass [Msun], mean radius [km], a [AU], e, I, L (mean longitude), long. of perihelion, Omega [deg]
            Planet(s, "Mercury", 1.6601e-7, 2439.7, 0.38709927, 0.20563593, 7.00497902, 252.25032350, 77.45779628, 48.33076593, 0.62, 0.60, 0.58);
            Planet(s, "Venus", 2.4478e-6, 6051.8, 0.72333566, 0.00677672, 3.39467605, 181.97909950, 131.60246718, 76.67984255, 0.93, 0.83, 0.60);
            Planet(s, "Earth", 3.0035e-6, 6371.0, 1.00000261, 0.01671123, 0.0, 100.46457166, 102.93768193, 0.0, 0.35, 0.60, 1.00);
            Planet(s, "Mars", 3.2272e-7, 3389.5, 1.52371034, 0.09339410, 1.84969142, -4.55343205, -23.94362959, 49.55953891, 0.95, 0.45, 0.30);
            Planet(s, "Jupiter", 9.5479e-4, 69911.0, 5.20288700, 0.04838624, 1.30439695, 34.39644051, 14.72847983, 100.47390909, 0.85, 0.70, 0.52);
            Planet(s, "Saturn", 2.8589e-4, 58232.0, 9.53667594, 0.05386179, 2.48599187, 49.95424423, 92.59887831, 113.66242448, 0.92, 0.82, 0.55);
            Planet(s, "Uranus", 4.3662e-5, 25362.0, 19.18916464, 0.04725744, 0.77263783, 313.23810451, 170.95427630, 74.01692503, 0.55, 0.88, 0.92);
            Planet(s, "Neptune", 5.1514e-5, 24622.0, 30.06992276, 0.00859048, 1.77004347, -55.12002969, 44.96476227, 131.78422574, 0.35, 0.50, 0.98);

            // Halley-like comet (a, e, i, Omega, omega of 1P/Halley); placed inbound, about 6 yr before perihelion.
            var comet = new OrbitalElements
            {
                SemiMajorAxis = 17.834,
                Eccentricity = 0.96714,
                Inclination = Constants.DegToRad(162.26),
                LongitudeOfAscendingNode = Constants.DegToRad(58.42),
                ArgumentOfPericenter = Constants.DegToRad(111.33),
            };
            comet.MeanAnomaly = Constants.DegToRad(330.0);
            s.Bodies.Add(new ScenarioBody
            {
                Name = "Comet", Kind = BodyKind.TestParticle, Radius = 5.5 * Constants.KmInAu,
                Color = new[] { 0.85, 0.95, 1.00 }, ReferenceIndex = 0, UsesElements = true, Elements = comet,
            });
            return s;
        }

        private static void Planet(Scenario s, string name, double mass, double radiusKm, double a, double e, double incDeg,
            double meanLongitudeDeg, double perihelionLongitudeDeg, double nodeDeg, double r, double g, double b)
        {
            var el = new OrbitalElements
            {
                SemiMajorAxis = a,
                Eccentricity = e,
                Inclination = Constants.DegToRad(incDeg),
                LongitudeOfAscendingNode = Constants.DegToRad(nodeDeg),
                ArgumentOfPericenter = Constants.WrapTwoPi(Constants.DegToRad(perihelionLongitudeDeg - nodeDeg)),
            };
            el.MeanAnomaly = Constants.WrapTwoPi(Constants.DegToRad(meanLongitudeDeg - perihelionLongitudeDeg));
            s.Bodies.Add(new ScenarioBody
            {
                Name = name, Kind = BodyKind.Massive, Mass = mass, Radius = radiusKm * Constants.KmInAu,
                Color = new[] { r, g, b }, ReferenceIndex = -1, UsesElements = true, Elements = el,
            });
        }

        // ------------------------------------------------------------------
        // Kozai-Lidov
        // ------------------------------------------------------------------

        /// <summary>
        /// Circumstellar Kozai-Lidov setup: an equal-mass circular binary (0.5 + 0.5 Msun, a = 1 AU, elements
        /// relative to the barycenter) and a massless particle on a circular orbit at 0.1 AU around Star A,
        /// inclined 60 deg to the binary plane. Above 39.2 deg of mutual inclination the particle trades
        /// inclination for eccentricity; at quadrupole order e_max = sqrt(1 - 5/3 cos^2 60 deg) = 0.76 with
        /// the inclination falling to about 39 deg. The binary is circular, so the octupole term vanishes.
        /// Matches the user's saved case "KZ" (names, colors, radii, integrator and speed included).
        /// </summary>
        public static Scenario KozaiLidov()
        {
            var s = new Scenario
            {
                Name = KozaiLidovName,
                IsBuiltIn = true,
                IntegratorIndex = 2, // Dormand-Prince: resolves the close pericenter passages at high e
                Speed = 10.0,
                Description = "A test particle at 0.1 AU around Star A, inclined 60° to a circular equal-mass binary (a = 1 AU). Watch e grow to ~0.76 while the inclination drops to ~39°, then cycle back.",
            };
            s.Bodies.Add(Star("Star A", 0.5, 1.0, 1.00, 0.85, 0.35));
            s.Bodies.Add(new ScenarioBody
            {
                Name = "Star B", Kind = BodyKind.Massive, Mass = 0.5, Radius = Constants.SolarRadiusInAu,
                Color = new[] { 1.00, 0.55, 0.25 }, ReferenceIndex = -1, UsesElements = true,
                Elements = new OrbitalElements { SemiMajorAxis = 1.0 },
            });
            s.Bodies.Add(new ScenarioBody
            {
                Name = "Planet a", Kind = BodyKind.TestParticle, Radius = 0.5 * Constants.SolarRadiusInAu,
                Color = new[] { 0.95, 0.35, 0.30 }, ReferenceIndex = 0, UsesElements = true,
                Elements = new OrbitalElements { SemiMajorAxis = 0.1, Inclination = Constants.DegToRad(60.0) },
            });
            return s;
        }

        // ------------------------------------------------------------------
        // Polar Planets
        // ------------------------------------------------------------------

        /// <summary>
        /// Equal-mass eccentric binary (0.5 + 0.5 Msun, a = 1 AU, e = 0.8) with a Jupiter-mass planet at 5 AU
        /// and a massless planet at 10 AU, both on circular polar orbits (inc = 90 deg, Omega = 90 deg): with
        /// the binary's eccentricity vector along +x, both orbit normals point along +x as well.
        /// </summary>
        public static Scenario PolarPlanets()
        {
            double polar = Constants.DegToRad(90.0);
            var s = new Scenario
            {
                Name = PolarPlanetsName,
                IsBuiltIn = true,
                Speed = 1.0,
                Description = "Equal-mass binary (a = 1 AU, e = 0.8) with a Jupiter-mass planet at 5 AU and a massless planet at 10 AU, both on polar orbits aligned with the binary's eccentricity vector.",
            };
            s.Bodies.Add(Star("Star A", 0.5, 0.5, 1.00, 0.85, 0.35));
            s.Bodies.Add(new ScenarioBody
            {
                Name = "Star B", Kind = BodyKind.Massive, Mass = 0.5, Radius = 0.5 * Constants.SolarRadiusInAu,
                Color = new[] { 1.00, 0.55, 0.25 }, UsesElements = true,
                Elements = new OrbitalElements { SemiMajorAxis = 1.0, Eccentricity = 0.8 },
            });
            s.Bodies.Add(new ScenarioBody
            {
                Name = "Planet b", Kind = BodyKind.Massive, Mass = Constants.JupiterMassInSolar, Radius = Constants.JupiterRadiusInAu,
                Color = new[] { 0.55, 0.75, 1.00 }, UsesElements = true,
                Elements = new OrbitalElements { SemiMajorAxis = 5.0, Inclination = polar, LongitudeOfAscendingNode = polar },
            });
            s.Bodies.Add(new ScenarioBody
            {
                Name = "Planet c", Kind = BodyKind.TestParticle, Radius = Constants.EarthRadiusInAu,
                Color = new[] { 0.85, 0.55, 1.00 }, UsesElements = true,
                Elements = new OrbitalElements { SemiMajorAxis = 10.0, Inclination = polar, LongitudeOfAscendingNode = polar },
            });
            return s;
        }

        private static ScenarioBody Star(string name, double mass, double radiusSolar, double r, double g, double b) => new ScenarioBody
        {
            Name = name, Kind = BodyKind.Massive, Mass = mass, Radius = radiusSolar * Constants.SolarRadiusInAu,
            Color = new[] { r, g, b }, UsesElements = true, Elements = OrbitalElements.Circular(1.0),
        };
    }
}
