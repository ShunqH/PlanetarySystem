using System;

namespace PlanetSystem.Physics
{
    /// <summary>
    /// Classical Keplerian elements of a body relative to a primary, using Rebound conventions:
    /// a (semi-major axis), e, inc, Omega (longitude of ascending node), omega (argument of pericenter),
    /// f (true anomaly). Angles in radians. The reference plane is the xy plane, z is "up".
    /// </summary>
    public struct OrbitalElements
    {
        /// <summary>Semi-major axis a [AU]. Must be positive (elliptic orbits only).</summary>
        public double SemiMajorAxis;
        /// <summary>Eccentricity e in [0, 1).</summary>
        public double Eccentricity;
        /// <summary>Inclination i [rad] in [0, pi].</summary>
        public double Inclination;
        /// <summary>Longitude of the ascending node Omega [rad].</summary>
        public double LongitudeOfAscendingNode;
        /// <summary>Argument of pericenter omega [rad].</summary>
        public double ArgumentOfPericenter;
        /// <summary>True anomaly f [rad].</summary>
        public double TrueAnomaly;

        public double MeanAnomaly
        {
            get => Kepler.MeanFromTrue(TrueAnomaly, Eccentricity);
            set => TrueAnomaly = Kepler.TrueFromMean(value, Eccentricity);
        }

        public double Pericenter => SemiMajorAxis * (1.0 - Eccentricity);
        public double Apocenter => SemiMajorAxis * (1.0 + Eccentricity);

        /// <summary>Orbital period for the given gravitational parameter mu = G (M + m).</summary>
        public double Period(double mu) => Constants.TwoPi * Math.Sqrt(SemiMajorAxis * SemiMajorAxis * SemiMajorAxis / mu);

        public static OrbitalElements Circular(double a) => new OrbitalElements { SemiMajorAxis = a };

        public override string ToString() =>
            $"a={SemiMajorAxis:G5} e={Eccentricity:G4} i={Constants.RadToDeg(Inclination):F2}° " +
            $"Ω={Constants.RadToDeg(LongitudeOfAscendingNode):F2}° ω={Constants.RadToDeg(ArgumentOfPericenter):F2}° f={Constants.RadToDeg(TrueAnomaly):F2}°";
    }
}
