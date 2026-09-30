using System;

namespace PlanetSystem.Physics
{
    /// <summary>
    /// Kepler's equation and anomaly conversions for elliptic orbits (0 &lt;= e &lt; 1).
    /// </summary>
    public static class Kepler
    {
        /// <summary>Solves Kepler's equation M = E - e sin E for the eccentric anomaly E (Newton iteration).</summary>
        public static double EccentricFromMean(double meanAnomaly, double e)
        {
            double M = Constants.WrapTwoPi(meanAnomaly);
            double E = e < 0.8 ? M : Math.PI;
            for (int i = 0; i < 60; i++)
            {
                double f = E - e * Math.Sin(E) - M;
                double fp = 1.0 - e * Math.Cos(E);
                double dE = f / fp;
                E -= dE;
                if (Math.Abs(dE) < 1e-15) break;
            }
            return E;
        }

        public static double MeanFromEccentric(double E, double e) => Constants.WrapTwoPi(E - e * Math.Sin(E));

        public static double TrueFromEccentric(double E, double e)
        {
            double f = 2.0 * Math.Atan2(Math.Sqrt(1.0 + e) * Math.Sin(E * 0.5), Math.Sqrt(1.0 - e) * Math.Cos(E * 0.5));
            return Constants.WrapTwoPi(f);
        }

        public static double EccentricFromTrue(double f, double e)
        {
            double E = 2.0 * Math.Atan2(Math.Sqrt(1.0 - e) * Math.Sin(f * 0.5), Math.Sqrt(1.0 + e) * Math.Cos(f * 0.5));
            return Constants.WrapTwoPi(E);
        }

        public static double TrueFromMean(double M, double e) => TrueFromEccentric(EccentricFromMean(M, e), e);

        public static double MeanFromTrue(double f, double e) => MeanFromEccentric(EccentricFromTrue(f, e), e);

        /// <summary>
        /// Safe arccos with quadrant disambiguation, following Rebound's acos2.
        /// Returns acos(num/denom) in [0, pi], negated when the disambiguator is negative.
        /// </summary>
        public static double Acos2(double num, double denom, double disambiguator)
        {
            if (denom <= 0.0) return 0.0;
            double cosine = num / denom;
            double val;
            if (cosine > -1.0 && cosine < 1.0)
            {
                val = Math.Acos(cosine);
                if (disambiguator < 0.0) val = -val;
            }
            else
            {
                val = cosine <= -1.0 ? Math.PI : 0.0;
            }
            return val;
        }
    }
}
