using UnityEngine;

namespace PlanetSystem.Core
{
    /// <summary>
    /// All tunable parameters of the demo, editable in the Inspector without touching code.
    /// The asset is loaded from Resources/Settings/SimulationSettings; a default instance is created if missing.
    /// </summary>
    [CreateAssetMenu(fileName = "SimulationSettings", menuName = "PlanetSystem/Simulation Settings")]
    public sealed class SimulationSettings : ScriptableObject
    {
        [Header("Body limits")]
        [Tooltip("Maximum number of massive (gravitating) bodies.")]
        public int MaxMassiveBodies = 4;
        [Tooltip("Maximum number of massless test particles.")]
        public int MaxTestParticles = 10;

        [Header("Rendering scale")]
        [Tooltip("Unity world units per astronomical unit.")]
        public float UnitsPerAu = 10f;
        [Tooltip("Smallest rendered body radius in Unity units (exaggerated scale mode).")]
        public float MinDisplayRadius = 0.05f;
        [Tooltip("Extra rendered radius per decade of physical radius, measured in Earth radii.")]
        public float RadiusLogScale = 0.07f;
        [Tooltip("Bodies at or above this mass (Msun) are rendered as self-luminous stars.")]
        public float StarMassThreshold = 0.05f;

        [Header("Orbit lines")]
        [Tooltip("Number of segments used to draw each osculating ellipse.")]
        public int OrbitSegments = 256;
        [Tooltip("Line width as a fraction of the camera distance.")]
        public float OrbitLineWidthFraction = 0.0015f;

        [Header("Reference grid")]
        [Tooltip("Grid spacing in AU.")]
        public float GridSpacingAu = 1f;
        [Tooltip("Grid line color. The unlit line shader is opaque, so use dark RGB values for dim lines.")]
        public Color GridColor = new Color(0.16f, 0.18f, 0.23f, 1f);
        public Color GridAxisColor = new Color(0.34f, 0.37f, 0.45f, 1f);

        [Header("Camera")]
        [Tooltip("Elevation of the fixed camera above the reference plane, in degrees.")]
        public float CameraElevationDeg = 35f;
        [Tooltip("Extra room around the system when auto-framing (1 = tight fit).")]
        public float CameraPadding = 1.3f;
        [Tooltip("Minimum camera distance in Unity units.")]
        public float CameraMinDistance = 4f;

        public static SimulationSettings LoadOrDefault()
        {
            var s = Resources.Load<SimulationSettings>("Settings/SimulationSettings");
            if (s == null)
            {
                Debug.LogWarning("SimulationSettings asset not found in Resources; using defaults.");
                s = CreateInstance<SimulationSettings>();
            }
            return s;
        }
    }
}
