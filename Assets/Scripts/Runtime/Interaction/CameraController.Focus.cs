using PlanetSystem.Core;
using PlanetSystem.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlanetSystem.Interaction
{
    /// <summary>
    /// Focus state: the camera travels with one massive body without rotating, so orbits around that body
    /// (for example a test particle around one star of a binary) stay still on screen however fast the body
    /// itself moves. The body position is applied exactly every frame; only target switches, preset
    /// orientations (1-6) and zoom are smoothed. Scroll zooms; the frame size otherwise follows the orbits
    /// of the test particles that use the body as primary.
    /// </summary>
    public sealed partial class CameraController
    {
        private CameraView _focusView = CameraView.Custom;
        /// <summary>Offset from the focused body to the look-at point; decays to zero after a target switch.</summary>
        private Vector3 _focusOffset;
        private float _focusZoom = 1f;

        public bool IsFocus { get; private set; }
        public int FocusTargetId { get; private set; } = -1;

        /// <summary>World position of the focused body this frame.</summary>
        public Vector3 FocusCenter { get; private set; }

        /// <summary>Radius around the focused body kept in frame, in AU (including the scroll zoom).</summary>
        public float FocusRadiusAu { get; private set; } = 1f;

        public string FocusTargetName => _sim.Find(FocusTargetId)?.Name ?? "—";

        /// <summary>Starts following a massive body, keeping the current view direction.</summary>
        public void EnterFocus(int bodyId)
        {
            var rec = _sim.Find(bodyId);
            if (rec == null || !rec.Body.IsMassive) return;
            IsFocus = true;
            FocusTargetId = bodyId;
            _focusView = CameraView.Custom;
            _focusZoom = 1f;

            // Express the current pose relative to the body so nothing jumps; the offset then decays.
            var t = _camera.transform;
            var body = BodyPosition(rec);
            _rotation = t.rotation;
            _distance = Mathf.Max(0.05f, Vector3.Distance(t.position, body));
            _focusOffset = t.position + t.forward * _distance - body;
        }

        /// <summary>Leaves the focus state and keeps the current pose until a preset view is chosen.</summary>
        public void ExitFocus()
        {
            if (!IsFocus) return;
            IsFocus = false;
            FocusTargetId = -1;
            View = CameraView.Custom;
        }

        /// <summary>Glides to another massive body and follows it from then on.</summary>
        public void SetFocusTarget(int bodyId)
        {
            if (!IsFocus || bodyId == FocusTargetId) return;
            var rec = _sim.Find(bodyId);
            if (rec == null || !rec.Body.IsMassive) return;
            var lookAt = _target;
            FocusTargetId = bodyId;
            _focusOffset = lookAt - BodyPosition(rec);
            _focusZoom = 1f;
        }

        private void FocusUpdate(float dt)
        {
            var rec = _sim.Find(FocusTargetId);
            if (rec == null || !rec.Body.IsMassive)
            {
                // The body was removed or a new case was loaded: fall back to the first massive body.
                rec = PrimaryRecord();
                if (rec == null) return;
                SetTargetSilently(rec);
            }

            float a = 1f - Mathf.Exp(-_settings.ViewTransitionRate * dt);
            var body = BodyPosition(rec);
            FocusCenter = body;
            _focusOffset = Vector3.Lerp(_focusOffset, Vector3.zero, a);
            _target = body + _focusOffset; // exact tracking: the body never lags behind

            if (_focusView != CameraView.Custom)
            {
                ComputeView(_focusView, out _, out var rotation, out var up);
                _rotation = Quaternion.Slerp(_rotation, rotation, a);
                _viewUp = up;
            }

            var mouse = Mouse.current;
            if (mouse != null && ScrollEnabled)
            {
                float notches = mouse.scroll.ReadValue().y;
                if (notches != 0f) _focusZoom = Mathf.Clamp(_focusZoom * Mathf.Pow(0.85f, notches), 0.01f, 100f);
            }

            FocusRadiusAu = (float)NaturalRadiusAu(rec) * _focusZoom;
            float wanted = FitDistance(FocusRadiusAu * _settings.UnitsPerAu, 0.02f);
            _distance = Mathf.Lerp(_distance, wanted, a);
            ApplyOrbitPose();
        }

        /// <summary>
        /// Frame size around the focused body: twice the largest semi-major axis of the test particles
        /// orbiting it; otherwise twice its own barycentric semi-major axis (the scale of its neighbours);
        /// otherwise the whole system.
        /// </summary>
        private double NaturalRadiusAu(BodyRecord rec)
        {
            double r = 0.0;
            foreach (var other in _sim.Bodies)
            {
                if (other.Body.IsMassive || other.ReferenceId != rec.Id) continue;
                if (_sim.TryGetDrawnOrbit(other, out var el, out _, out _)) r = System.Math.Max(r, 2.0 * el.SemiMajorAxis);
            }
            if (r <= 0.0 && _sim.TryGetDrawnOrbit(rec, out var own, out _, out _)) r = 2.0 * own.SemiMajorAxis;
            if (r <= 0.0) r = _fitRadius / _settings.UnitsPerAu;
            return r;
        }

        private void SetTargetSilently(BodyRecord rec)
        {
            FocusTargetId = rec.Id;
            _focusOffset = _target - BodyPosition(rec);
            _focusZoom = 1f;
        }

        private Vector3 BodyPosition(BodyRecord rec) => Units.ToUnity(rec.Body.Position, _settings.UnitsPerAu);
    }
}
