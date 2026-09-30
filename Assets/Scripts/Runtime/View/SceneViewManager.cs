using System.Collections.Generic;
using PlanetSystem.Core;
using PlanetSystem.Physics;
using PlanetSystem.Interaction;
using UnityEngine;

namespace PlanetSystem.View
{
    /// <summary>
    /// Keeps the 3D scene in sync with the <see cref="SimulationController"/>: one sphere, one osculating
    /// ellipse and one label per body, a reference grid, the camera, body picking and the "ghost" preview
    /// shown while a new body is being configured.
    /// </summary>
    public sealed class SceneViewManager : MonoBehaviour
    {
        private SimulationController _controller;
        private SimulationSettings _settings;
        private Camera _camera;
        private CameraController _cameraController;
        private ReferenceGrid _grid;
        private Transform _bodiesRoot;
        private Transform _labelCanvas;
        private Font _font;

        private readonly Dictionary<int, BodyView> _bodyViews = new Dictionary<int, BodyView>();
        private readonly Dictionary<int, OrbitEllipseView> _orbitViews = new Dictionary<int, OrbitEllipseView>();
        private readonly Dictionary<int, BodyLabel> _labels = new Dictionary<int, BodyLabel>();

        private BodyDefinition _preview;
        private BodyView _previewBody;
        private OrbitEllipseView _previewOrbit;
        private static readonly Color PreviewColor = new Color(0.75f, 0.78f, 0.85f, 1f);

        public CameraController CameraController => _cameraController;

        public void Initialize(SimulationController controller, SimulationSettings settings, Camera camera, Transform labelCanvas, Font font)
        {
            _controller = controller;
            _settings = settings;
            _camera = camera;
            _labelCanvas = labelCanvas;
            _font = font;

            _bodiesRoot = new GameObject("Bodies").transform;
            _bodiesRoot.SetParent(transform, false);

            _grid = ReferenceGrid.Create(transform, settings);
            _grid.EnsureExtent(2.0);

            _cameraController = camera.gameObject.AddComponent<CameraController>();
            _cameraController.Initialize(camera, settings, controller);

            _previewBody = BodyView.Create(_bodiesRoot, -1, "Preview");
            _previewBody.gameObject.SetActive(false);
            _previewOrbit = OrbitEllipseView.Create(_bodiesRoot, "Preview", settings.OrbitSegments);
            _previewOrbit.SetColor(PreviewColor);
            _previewOrbit.SetVisible(false);

            controller.BodiesChanged += SyncViews;
            controller.SelectionChanged += OnSelectionChanged;
            SyncViews();
        }

        private void OnDestroy()
        {
            if (_controller == null) return;
            _controller.BodiesChanged -= SyncViews;
            _controller.SelectionChanged -= OnSelectionChanged;
        }

        // ------------------------------------------------------------------
        // Structural sync
        // ------------------------------------------------------------------

        private void SyncViews()
        {
            var alive = new HashSet<int>();
            foreach (var rec in _controller.Bodies)
            {
                alive.Add(rec.Id);
                if (!_bodyViews.TryGetValue(rec.Id, out var bv))
                {
                    bv = BodyView.Create(_bodiesRoot, rec.Id, rec.Name);
                    _bodyViews[rec.Id] = bv;
                    _orbitViews[rec.Id] = OrbitEllipseView.Create(_bodiesRoot, rec.Name, _settings.OrbitSegments);
                    _labels[rec.Id] = BodyLabel.Create(_labelCanvas, _font);
                }
                bv.ApplyAppearance(rec.Body, rec.Color, _settings);
                bv.SetSelected(rec.Id == _controller.SelectedId);
                _orbitViews[rec.Id].SetColor(rec.Color);
                _labels[rec.Id].Set(rec.Name, rec.Color);
            }

            var dead = new List<int>();
            foreach (var id in _bodyViews.Keys) if (!alive.Contains(id)) dead.Add(id);
            foreach (var id in dead)
            {
                Destroy(_bodyViews[id].gameObject);
                Destroy(_orbitViews[id].gameObject);
                Destroy(_labels[id].gameObject);
                _bodyViews.Remove(id);
                _orbitViews.Remove(id);
                _labels.Remove(id);
            }
        }

        private void OnSelectionChanged(int id)
        {
            foreach (var kv in _bodyViews) kv.Value.SetSelected(kv.Key == id);
        }

        // ------------------------------------------------------------------
        // Per-frame sync (positions, ellipses, camera, then labels and line widths)
        // ------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_controller == null) return;
            double maxExtentAu = 0.5;

            // Pass 1: positions and ellipses; measure the extent of everything that is bound.
            foreach (var rec in _controller.Bodies)
            {
                _bodyViews[rec.Id].ApplyPosition(rec.Body, _settings);
                var orbit = _orbitViews[rec.Id];
                if (_controller.TryGetDrawnOrbit(rec, out var el, out var center, out _))
                {
                    orbit.SetVisible(true);
                    double extent = orbit.Rebuild(el, center, _settings);
                    orbit.Center = Units.ToUnity(center, _settings.UnitsPerAu);
                    if (extent > maxExtentAu) maxExtentAu = extent;
                    double r = rec.Body.Position.Length;
                    if (r > maxExtentAu) maxExtentAu = r;
                }
                else
                {
                    // Unbound (or the lone central body): no ellipse, and it does not drive the camera framing.
                    orbit.SetVisible(false);
                }
            }
            UpdatePreview(ref maxExtentAu);
            _grid.EnsureExtent(maxExtentAu);

            // Pass 2: move the camera for this frame.
            _cameraController.SetFitRadius((float)maxExtentAu * _settings.UnitsPerAu);
            _cameraController.Tick(Time.unscaledDeltaTime);

            // Pass 3: everything that depends on the final camera pose.
            var camPos = _camera.transform.position;
            foreach (var rec in _controller.Bodies)
            {
                var bv = _bodyViews[rec.Id];
                bv.FaceCamera(_camera);
                var orbit = _orbitViews[rec.Id];
                float w = LineWidth(camPos, orbit.Center);
                orbit.SetWidth(rec.Id == _controller.SelectedId ? w * 2f : w);
                _labels[rec.Id].Follow(_camera, bv.transform.position, 12f);
            }
            _previewOrbit.SetWidth(LineWidth(camPos, _previewOrbit.Center));
        }

        /// <summary>World-space line width that looks roughly constant on screen at the orbit's distance.</summary>
        private float LineWidth(Vector3 cameraPosition, Vector3 orbitCenter)
        {
            return Mathf.Max(0.002f, Vector3.Distance(cameraPosition, orbitCenter) * _settings.OrbitLineWidthFraction);
        }

        private void UpdatePreview(ref double maxExtentAu)
        {
            if (_preview == null) return;
            if (_controller.TryComputeState(_preview, -1, out var p, out var v, out _))
            {
                _previewBody.gameObject.SetActive(true);
                _previewBody.transform.position = Units.ToUnity(p, _settings.UnitsPerAu);
                if (_controller.TryGetDrawnOrbit(_preview.Kind, _preview.Mass, p, v, _preview.ReferenceId, -1,
                        out var pel, out var pcenter, out _))
                {
                    _previewOrbit.SetVisible(true);
                    double extent = _previewOrbit.Rebuild(pel, pcenter, _settings);
                    _previewOrbit.Center = Units.ToUnity(pcenter, _settings.UnitsPerAu);
                    if (extent > maxExtentAu) maxExtentAu = extent;
                }
                else
                {
                    _previewOrbit.SetVisible(false);
                }
            }
            else
            {
                // No primary yet: the body will sit at the origin.
                _previewBody.gameObject.SetActive(true);
                _previewBody.transform.position = Vector3.zero;
                _previewOrbit.SetVisible(false);
            }
        }

        // ------------------------------------------------------------------
        // Picking
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns the id of the body under a screen position, or -1. Picks in screen space: the nearest
        /// body whose rendered disc, enlarged to at least <see cref="PickRadiusPixels"/>, contains the point,
        /// so even tiny planets are easy to click.
        /// </summary>
        public int PickBody(Vector2 screenPosition)
        {
            int best = -1;
            float bestScore = float.PositiveInfinity;
            foreach (var kv in _bodyViews)
            {
                var bv = kv.Value;
                var center = _camera.WorldToScreenPoint(bv.transform.position);
                if (center.z <= 0f) continue;
                var edge = _camera.WorldToScreenPoint(bv.transform.position + _camera.transform.right * bv.DisplayRadiusUnits);
                float radiusPx = Mathf.Max(PickRadiusPixels, Vector2.Distance(center, edge));
                float d = Vector2.Distance(screenPosition, center);
                if (d > radiusPx) continue;
                float score = d / radiusPx + center.z * 1e-6f; // prefer the closest-to-center, then nearer bodies
                if (score < bestScore) { bestScore = score; best = kv.Key; }
            }
            return best;
        }

        private const float PickRadiusPixels = 14f;

        // ------------------------------------------------------------------
        // Preview ghost for the add form
        // ------------------------------------------------------------------

        /// <summary>Shows a translucent ghost of a body that has not been added yet.</summary>
        public void ShowPreview(BodyDefinition def)
        {
            _preview = def;
            var ghost = new Body(-1, "Preview", def.Kind, def.Mass, def.Radius);
            _previewBody.ApplyAppearance(ghost, PreviewColor, _settings);
            _previewOrbit.SetColor(PreviewColor);
        }

        public void HidePreview()
        {
            _preview = null;
            _previewBody.gameObject.SetActive(false);
            _previewOrbit.SetVisible(false);
        }
    }
}
