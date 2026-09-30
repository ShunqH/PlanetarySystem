using PlanetSystem.Physics;
using UnityEngine;

namespace PlanetSystem.Core
{
    /// <summary>
    /// Mapping between the physics frame and Unity's world frame.
    /// Physics uses a z-up frame (reference plane = xy, like Rebound); Unity is y-up.
    /// We map physics (x, y, z) to Unity (x, z, y) so the reference plane is horizontal on screen.
    /// </summary>
    public static class Units
    {
        public static Vector3 ToUnity(Vec3d p, float unitsPerAu)
        {
            return new Vector3((float)(p.X * unitsPerAu), (float)(p.Z * unitsPerAu), (float)(p.Y * unitsPerAu));
        }

        public static Vec3d ToPhysics(Vector3 p, float unitsPerAu)
        {
            double s = 1.0 / unitsPerAu;
            return new Vec3d(p.x * s, p.z * s, p.y * s);
        }

        /// <summary>Mass unit choices offered in the UI, with their value in solar masses.</summary>
        public static readonly (string Label, double InSolar)[] MassUnits =
        {
            ("M☉", 1.0),
            ("M♃ (Jupiter)", Constants.JupiterMassInSolar),
            ("M⊕ (Earth)", Constants.EarthMassInSolar),
        };

        /// <summary>Radius unit choices offered in the UI, with their value in AU.</summary>
        public static readonly (string Label, double InAu)[] RadiusUnits =
        {
            ("R☉", Constants.SolarRadiusInAu),
            ("R♃ (Jupiter)", Constants.JupiterRadiusInAu),
            ("R⊕ (Earth)", Constants.EarthRadiusInAu),
            ("km", Constants.KmInAu),
        };
    }
}
