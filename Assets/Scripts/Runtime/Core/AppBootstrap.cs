using PlanetSystem.Interaction;
using PlanetSystem.UI;
using PlanetSystem.View;
using UnityEngine;

namespace PlanetSystem.Core
{
    /// <summary>
    /// The only component placed in the scene by hand. Builds the whole application at startup:
    /// settings, controller, 3D views and UI. Keeping everything procedural avoids prefab/scene merge pain.
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        [Tooltip("Optional explicit settings asset. Falls back to Resources/Settings/SimulationSettings.")]
        public SimulationSettings SettingsOverride;

        [Tooltip("Populate the scene with a small circumbinary example on start.")]
        public bool LoadExampleOnStart = true;

        private void Awake()
        {
            var settings = SettingsOverride != null ? SettingsOverride : SimulationSettings.LoadOrDefault();

            // A fixed frame rate keeps the simulated time per frame (speed / fps) constant, which lets the
            // fixed-step integrators use one constant step size. VSync would override targetFrameRate.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = settings.TargetFrameRate;

            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.012f, 0.02f, 1f);
            camera.fieldOfView = 50f;

            if (FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            var controller = gameObject.AddComponent<SimulationController>();
            controller.Initialize(settings);

            var labelCanvas = UIRoot.CreateLabelCanvas();

            var scene = new GameObject("SceneView").AddComponent<SceneViewManager>();
            scene.Initialize(controller, settings, camera, labelCanvas.transform, UIFactory.Font);

            var interaction = gameObject.AddComponent<InteractionController>();
            interaction.Initialize(controller, scene.CameraController, scene);

            UIRoot.Build(controller, scene, interaction);

            if (LoadExampleOnStart) controller.LoadCircumbinaryExample();
        }
    }
}
