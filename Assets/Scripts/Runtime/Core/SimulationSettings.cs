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

        [Header("Time integration")]
        [Tooltip("Target rendering frame rate. VSync is disabled so this is honoured exactly.")]
        public int TargetFrameRate = 60;
        [Tooltip("Default time acceleration in simulated years per real second.")]
        public float DefaultSpeed = 1f;
        [Tooltip("Index into the integrator list (0 = Leapfrog, 1 = Yoshida 4, 2 = Dormand-Prince).")]
        public int DefaultIntegrator = 0;
        [Tooltip("CPU time allowed for integration per frame, in milliseconds. If exceeded, the simulation runs slower than requested.")]
        public float MaxIntegrationMsPerFrame = 8f;
        [Tooltip("Leapfrog step as a fraction of the shortest dynamical timescale.")]
        public float LeapfrogStepFraction = 0.03f;
        [Tooltip("Yoshida step as a fraction of the shortest dynamical timescale.")]
        public float YoshidaStepFraction = 0.06f;
        [Tooltip("Relative per-step error tolerance of the adaptive Dormand-Prince integrator.")]
        public float AdaptiveTolerance = 1e-10f;
        [Tooltip("Plummer softening length in AU (0 = pure Newtonian).")]
        public float SofteningAu = 0f;

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
        [Tooltip("How fast the camera moves to a newly chosen preset view (1/s). Higher is snappier.")]
        public float ViewTransitionRate = 6f;
        [Tooltip("Views 5/6: elevation of the line of sight above the primary's orbital plane, in degrees.")]
        public float TrackingElevationDeg = 45f;

        [Header("Free-fly camera")]
        [Tooltip("Look rotation in degrees per pixel of pointer drag.")]
        public float FlyLookSensitivity = 0.15f;
        [Tooltip("WASD/QE speed in system radii per second (the system radius is measured when entering free-fly).")]
        public float FlySpeedFraction = 0.5f;
        [Tooltip("Speed multiplier while Shift is held (movement and scroll).")]
        public float FlyBoostMultiplier = 4f;
        [Tooltip("Distance moved per scroll-wheel notch, in system radii.")]
        public float FlyDollyFraction = 0.08f;

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
