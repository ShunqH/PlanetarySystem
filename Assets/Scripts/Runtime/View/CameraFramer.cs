using PlanetSystem.Core;
using UnityEngine;

namespace PlanetSystem.View
{
    /// <summary>
    /// Keeps a fixed, elevated viewpoint looking at the origin and backs off far enough to fit the whole system.
    /// Interactive camera controls come in a later phase; this only auto-frames.
    /// </summary>
    public sealed class CameraFramer : MonoBehaviour
    {
        private Camera _camera;
        private SimulationSettings _settings;
        private float _targetDistance;
        private float _distance;

        public float Distance => _distance;

        public void Initialize(Camera camera, SimulationSettings settings)
        {
            _camera = camera;
            _settings = settings;
            _distance = _targetDistance = settings.CameraMinDistance * 3f;
            Apply();
        }

        /// <summary>Requests a framing that fits a sphere of the given radius (Unity units) around the origin.</summary>
        public void FitRadius(float radiusUnits)
        {
            float halfFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            // Account for the aspect ratio: the horizontal FOV may be the limiting one on tall windows.
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFov) * aspect);
            float limiting = Mathf.Min(halfFov, halfFovH);
            float d = radiusUnits * _settings.CameraPadding / Mathf.Sin(limiting);
            _targetDistance = Mathf.Max(_settings.CameraMinDistance, d);
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            Apply();
        }

        private void Apply()
        {
            float elev = _settings.CameraElevationDeg * Mathf.Deg2Rad;
            var dir = new Vector3(0f, Mathf.Sin(elev), -Mathf.Cos(elev));
            _camera.transform.position = dir * _distance;
            _camera.transform.LookAt(Vector3.zero, Vector3.up);
            _camera.nearClipPlane = Mathf.Max(0.01f, _distance * 0.002f);
            _camera.farClipPlane = _distance * 20f + 100f;
        }
    }
}
