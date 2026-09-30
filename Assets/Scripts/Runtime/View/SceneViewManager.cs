using System.Collections.Generic;
using PlanetSystem.Core;
using PlanetSystem.Physics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PlanetSystem.View
{
    /// <summary>
    /// Keeps the 3D scene in sync with the <see cref="SimulationController"/>: one sphere, one osculating
    /// ellipse and one label per body, a reference grid, auto-framing camera, click selection and the
    /// "ghost" preview shown while a new body is being configured.
    /// </summary>
    public sealed class SceneViewManager : MonoBehaviour
    {
        private SimulationController _controller;
        private SimulationSettings _settings;
        private Camera _camera;
        private CameraFramer _framer;
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

            _framer = camera.gameObject.AddComponent<CameraFramer>();
            _framer.Initialize(camera, settings);

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
        // Per-frame sync (positions, ellipses, labels, camera)
        // ------------------------------------------------------------------

        private void LateUpdate()
        {
            if (_controller == null) return;
            double maxExtentAu = 0.5;
            float width = Mathf.Max(0.005f, _framer.Distance * _settings.OrbitLineWidthFraction);

            foreach (var rec in _controller.Bodies)
            {
                var bv = _bodyViews[rec.Id];
                bv.ApplyPosition(rec.Body, _settings);
                bv.FaceCamera(_camera);

                var orbit = _orbitViews[rec.Id];
                if (_controller.TryGetDrawnOrbit(rec, out var el, out var refPos, out _))
                {
                    orbit.SetVisible(true);
                    orbit.SetWidth(rec.Id == _controller.SelectedId ? width * 2f : width);
                    double extent = orbit.Rebuild(el, refPos, _settings);
                    if (extent > maxExtentAu) maxExtentAu = extent;
                    double r = rec.Body.Position.Length;
                    if (r > maxExtentAu) maxExtentAu = r;
                }
                else
                {
                    // Unbound (or the lone central body): no ellipse, and it does not drive the camera framing.
                    orbit.SetVisible(false);
                }

                _labels[rec.Id].Follow(_camera, bv.transform.position, bv.DisplayRadiusUnits > 0 ? 12f : 8f);
            }

            if (_preview != null)
            {
                if (_controller.TryComputeState(_preview, -1, out var p, out var v, out _))
                {
                    _previewBody.gameObject.SetActive(true);
                    _previewBody.transform.position = Units.ToUnity(p, _settings.UnitsPerAu);
                    if (_controller.TryGetDrawnOrbit(_preview.Kind, _preview.Mass, p, v, _preview.ReferenceId, -1,
                            out var pel, out var pcenter, out _))
                    {
                        _previewOrbit.SetVisible(true);
                        _previewOrbit.SetWidth(width);
                        double extent = _previewOrbit.Rebuild(pel, pcenter, _settings);
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

            _grid.EnsureExtent(maxExtentAu);
            _framer.FitRadius((float)maxExtentAu * _settings.UnitsPerAu);
        }

        // ------------------------------------------------------------------
        // Click selection
        // ------------------------------------------------------------------

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            // Bodies move every frame; make sure colliders match the rendered positions before raycasting.
            UnityEngine.Physics.SyncTransforms();
            var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (UnityEngine.Physics.Raycast(ray, out var hit, 10000f))
            {
                var bv = hit.collider.GetComponentInParent<BodyView>();
                if (bv != null && bv.BodyId >= 0)
                {
                    _controller.Select(bv.BodyId);
                    return;
                }
            }
            _controller.Select(-1);
        }

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
