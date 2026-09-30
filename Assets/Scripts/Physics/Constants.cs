using System;

namespace PlanetSystem.Physics
{
    /// <summary>
    /// Physical constants and unit conversions.
    /// Internal unit system: length in AU, time in years, mass in solar masses.
    /// In these units G = 4 pi^2, so a 1 AU circular orbit around 1 Msun has a period of exactly 1 yr.
    /// </summary>
    public static class Constants
    {
        public const double TwoPi = 2.0 * Math.PI;

        /// <summary>Gravitational constant in AU^3 / (Msun yr^2).</summary>
        public const double G = 4.0 * Math.PI * Math.PI;

        // --- Length ---
        public const double AuInKm = 149597870.7;
        public const double KmInAu = 1.0 / AuInKm;
        public const double SolarRadiusInAu = 695700.0 / AuInKm;
        public const double JupiterRadiusInAu = 71492.0 / AuInKm;
        public const double EarthRadiusInAu = 6371.0 / AuInKm;

        // --- Mass (in solar masses) ---
        public const double JupiterMassInSolar = 9.5458e-4;
        public const double EarthMassInSolar = 3.0035e-6;

        // --- Time ---
        public const double DaysPerYear = 365.25;

        public static double DegToRad(double deg) => deg * Math.PI / 180.0;
        public static double RadToDeg(double rad) => rad * 180.0 / Math.PI;

        /// <summary>Wraps an angle into [0, 2 pi).</summary>
        public static double WrapTwoPi(double angle)
        {
            angle %= TwoPi;
            if (angle < 0.0) angle += TwoPi;
            return angle;
        }
    }
}
