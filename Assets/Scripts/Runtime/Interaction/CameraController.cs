using System;
using PlanetSystem.Core;
using PlanetSystem.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlanetSystem.Interaction
{
    /// <summary>Camera presets of the edit state. Custom means "stay where the free-fly camera left off".</summary>
    public enum CameraView
    {
        Custom = 0,
        /// <summary>1: elevated oblique view of the reference plane (the startup view).</summary>
        Oblique = 1,
        /// <summary>2: looking down the reference-plane normal (+z), x to the right, y up.</summary>
        Top = 2,
        /// <summary>3: looking along -x from the +x axis, y to the right, z up.</summary>
        AlongX = 3,
        /// <summary>4: looking along +y from the -y axis, x to the right, z up.</summary>
        AlongY = 4,
        /// <summary>5: tracks the primary's orbit; line of sight in its plane perpendicular to the eccentricity vector.</summary>
        TrackPerpendicular = 5,
        /// <summary>6: tracks the primary's orbit; line of sight along its eccentricity vector.</summary>
        TrackPericenter = 6,
    }

    /// <summary>
    /// Drives the main camera. In the edit state it smoothly moves to and holds one of the preset views,
    /// re-framing the system every frame (views 5 and 6 also follow the primary's orbital plane and
    /// apsidal line). In the free-fly state it behaves like a drone: look with the mouse, WASD to move in
    /// the facing direction, Q/E down/up, scroll to dolly. Driven explicitly by <see cref="View.SceneViewManager"/>
    /// via <see cref="Tick"/> so the camera moves before labels and line widths are computed each frame.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        private Camera _camera;
        private SimulationSettings _settings;
        private SimulationController _sim;

        // Edit-state pose, expressed as orbit-around-target so view transitions follow an arc.
        private Vector3 _target;
        private Quaternion _rotation = Quaternion.identity;
        private float _distance = 30f;
        private float _fitRadius = 5f;

        /// <summary>The "up" axis of the current view; the free-fly camera inherits it as its horizon.</summary>
        private Vector3 _viewUp = Vector3.up;

        // Free-fly state
        private Vector3 _flyUp = Vector3.up;
        private float _flyScale = 5f;
        private Vector2 _pendingLook;

        public CameraView View { get; private set; } = CameraView.Oblique;
        public bool IsFreeFly { get; private set; }

        /// <summary>Set false while the user types in a text field so WASD/QE do not move the camera.</summary>
        public bool KeyboardEnabled { get; set; } = true;

        /// <summary>Set false while the pointer is over UI so scrolling a list does not dolly the camera.</summary>
        public bool ScrollEnabled { get; set; } = true;

        public Camera Camera => _camera;

        public void Initialize(Camera camera, SimulationSettings settings, SimulationController sim)
        {
            _camera = camera;
            _settings = settings;
            _sim = sim;
            ComputeView(CameraView.Oblique, out _target, out _rotation, out _viewUp);
            _distance = FitDistance(_fitRadius);
            ApplyOrbitPose();
        }

        // ------------------------------------------------------------------
        // Public controls
        // ------------------------------------------------------------------

        /// <summary>Radius (Unity units) around the origin that the edit-state views keep in frame.</summary>
        public void SetFitRadius(float radiusUnits) => _fitRadius = Mathf.Max(0.1f, radiusUnits);

        /// <summary>Switches to a preset view (edit state only). The camera moves there smoothly.</summary>
        public void SetView(CameraView view)
        {
            if (IsFreeFly || view == CameraView.Custom) return;
            if (View == CameraView.Custom)
            {
                // Coming from a free-fly pose: express it as an orbit pose without any jump.
                var t = _camera.transform;
                _rotation = t.rotation;
                _distance = Mathf.Max(0.1f, t.position.magnitude);
                _target = t.position + t.forward * _distance;
            }
            View = view;
        }

        public void EnterFreeFly()
        {
            if (IsFreeFly) return;
            IsFreeFly = true;
            _flyUp = _viewUp;
            _flyScale = _fitRadius;
            _pendingLook = Vector2.zero;
            // Level the horizon relative to the inherited up axis (a no-op once a view transition has settled).
            var t = _camera.transform;
            t.rotation = Quaternion.LookRotation(t.forward, _flyUp);
        }

        /// <summary>Leaves free-fly and keeps the current pose until a preset view is chosen.</summary>
        public void ExitFreeFly()
        {
            if (!IsFreeFly) return;
            IsFreeFly = false;
            _viewUp = _flyUp;
            View = CameraView.Custom;
        }

        /// <summary>Accumulates a pointer drag (pixels) to be applied as look rotation in free-fly.</summary>
        public void Look(Vector2 pixelDelta)
        {
            if (IsFreeFly) _pendingLook += pixelDelta;
        }

        public string ViewName
        {
            get
            {
                switch (View)
                {
                    case CameraView.Oblique: return "1  Oblique";
                    case CameraView.Top: return "2  Top (down z)";
                    case CameraView.AlongX: return "3  Along x";
                    case CameraView.AlongY: return "4  Along y";
                    case CameraView.TrackPerpendicular: return $"5  Tracking {PrimaryName()}, ⊥ e";
                    case CameraView.TrackPericenter: return $"6  Tracking {PrimaryName()}, along e";
                    default: return "Custom (press 1-6 for a preset)";
                }
            }
        }

        // ------------------------------------------------------------------
        // Per-frame update (called by SceneViewManager)
        // ------------------------------------------------------------------

        public void Tick(float dt)
        {
            if (IsFreeFly) FlyUpdate(dt);
            else if (View != CameraView.Custom) FollowView(dt);
            UpdateClipPlanes();
        }

        private void FollowView(float dt)
        {
            ComputeView(View, out var target, out var rotation, out var up);
            _viewUp = up;
            float a = 1f - Mathf.Exp(-_settings.ViewTransitionRate * dt);
            _target = Vector3.Lerp(_target, target, a);
            _rotation = Quaternion.Slerp(_rotation, rotation, a);
            _distance = Mathf.Lerp(_distance, FitDistance(_fitRadius), a);
            ApplyOrbitPose();
        }

        private void ApplyOrbitPose()
        {
            var t = _camera.transform;
            t.rotation = _rotation;
            t.position = _target - (_rotation * Vector3.forward) * _distance;
        }

        private void FlyUpdate(float dt)
        {
            var t = _camera.transform;

            // Look: yaw around the inherited up axis, pitch around the camera's right axis, no roll.
            if (_pendingLook != Vector2.zero)
            {
                float sens = _settings.FlyLookSensitivity;
                Vector3 fwd = Quaternion.AngleAxis(_pendingLook.x * sens, _flyUp) * t.forward;
                float pitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(fwd, _flyUp), -1f, 1f)) * Mathf.Rad2Deg;
                float newPitch = Mathf.Clamp(pitch + _pendingLook.y * sens, -89f, 89f);
                Vector3 right = Vector3.Cross(_flyUp, fwd).normalized;
                if (right.sqrMagnitude > 1e-8f) fwd = Quaternion.AngleAxis(-(newPitch - pitch), right) * fwd;
                t.rotation = Quaternion.LookRotation(fwd, _flyUp);
                _pendingLook = Vector2.zero;
            }

            var kb = Keyboard.current;
            bool boost = kb != null && KeyboardEnabled && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            float boostFactor = boost ? _settings.FlyBoostMultiplier : 1f;

            // Translate in the facing frame. Speed is fixed per free-fly session (set by the system size on
            // entry), so dollying closer never changes movement or look sensitivity.
            Vector3 move = Vector3.zero;
            if (kb != null && KeyboardEnabled)
            {
                if (kb.wKey.isPressed) move += t.forward;
                if (kb.sKey.isPressed) move -= t.forward;
                if (kb.dKey.isPressed) move += t.right;
                if (kb.aKey.isPressed) move -= t.right;
                if (kb.eKey.isPressed) move += _flyUp;
                if (kb.qKey.isPressed) move -= _flyUp;
            }
            if (move.sqrMagnitude > 0f)
                t.position += move.normalized * (_settings.FlySpeedFraction * _flyScale * boostFactor * dt);

            // Scroll = a burst of forward/backward motion along the view direction.
            var mouse = Mouse.current;
            if (mouse != null && ScrollEnabled)
            {
                float notches = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(notches) > 0f)
                    t.position += t.forward * (notches * _settings.FlyDollyFraction * _flyScale * boostFactor);
            }
        }

        private void UpdateClipPlanes()
        {
            if (IsFreeFly || View == CameraView.Custom)
            {
                float far = Mathf.Max(1000f, 60f * _fitRadius + 4f * _camera.transform.position.magnitude);
                _camera.nearClipPlane = Mathf.Max(0.002f, far * 1e-6f);
                _camera.farClipPlane = far;
            }
            else
            {
                _camera.nearClipPlane = Mathf.Max(0.01f, _distance * 0.002f);
                _camera.farClipPlane = _distance * 20f + 100f;
            }
        }

        // ------------------------------------------------------------------
        // View definitions
        // ------------------------------------------------------------------

        private float FitDistance(float radiusUnits)
        {
            float halfFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFov) * aspect);
            float limiting = Mathf.Min(halfFov, halfFovH);
            float d = radiusUnits * _settings.CameraPadding / Mathf.Sin(limiting);
            return Mathf.Max(_settings.CameraMinDistance, d);
        }

        /// <summary>Target point, camera rotation and up axis of a preset view (Unity frame).</summary>
        private void ComputeView(CameraView view, out Vector3 target, out Quaternion rotation, out Vector3 up)
        {
            target = Vector3.zero; // the system is kept on its barycenter
            Vector3 forward;
            switch (view)
            {
                case CameraView.Top:
                    forward = Vector3.down;
                    up = Vector3.forward; // physics +y
                    break;
                case CameraView.AlongX:
                    forward = Vector3.left; // camera on physics +x looking back at the origin
                    up = Vector3.up;
                    break;
                case CameraView.AlongY:
                    forward = Vector3.forward; // camera on physics -y looking along +y
                    up = Vector3.up;
                    break;
                case CameraView.TrackPerpendicular:
                case CameraView.TrackPericenter:
                    ComputeTrackingView(view, out target, out forward, out up);
                    break;
                default:
                {
                    float elev = _settings.CameraElevationDeg * Mathf.Deg2Rad;
                    forward = new Vector3(0f, -Mathf.Sin(elev), Mathf.Cos(elev));
                    up = Vector3.up;
                    break;
                }
            }
            rotation = Quaternion.LookRotation(forward, up);
        }

        /// <summary>
        /// Views 5/6: frame built from the primary's osculating orbit (first massive body, drawn orbit, i.e.
        /// barycentric for a binary). h = orbit normal, P = eccentricity-vector direction, Q = h x P.
        /// The line of sight lies along Q (view 5) or P (view 6), tilted down by TrackingElevationDeg from
        /// the orbital plane, with h pointing up on screen. Near-circular orbits fall back to the node line,
        /// and to the x axis when the orbit is also coplanar with the reference plane.
        /// </summary>
        private void ComputeTrackingView(CameraView view, out Vector3 target, out Vector3 forward, out Vector3 up)
        {
            var h = new Vec3d(0.0, 0.0, 1.0);
            var p = new Vec3d(1.0, 0.0, 0.0);
            var center = Vec3d.Zero;

            var primary = PrimaryRecord();
            if (primary != null && _sim.TryGetDrawnOrbit(primary, out var el, out center, out _))
            {
                double ci = Math.Cos(el.Inclination), si = Math.Sin(el.Inclination);
                double cO = Math.Cos(el.LongitudeOfAscendingNode), sO = Math.Sin(el.LongitudeOfAscendingNode);
                h = new Vec3d(si * sO, -si * cO, ci);
                if (el.Eccentricity >= 1e-3)
                {
                    double co = Math.Cos(el.ArgumentOfPericenter), so = Math.Sin(el.ArgumentOfPericenter);
                    p = new Vec3d(cO * co - sO * so * ci, sO * co + cO * so * ci, so * si);
                }
                else if (el.Inclination > 1e-6)
                {
                    p = new Vec3d(cO, sO, 0.0);
                }
            }

            var u = view == CameraView.TrackPericenter ? p : Vec3d.Cross(h, p);
            double elev = _settings.TrackingElevationDeg * Math.PI / 180.0;
            var sight = u * Math.Cos(elev) - h * Math.Sin(elev); // looking along u and down onto the plane

            target = Units.ToUnity(center, _settings.UnitsPerAu);
            forward = Units.ToUnity(sight, 1f).normalized;
            up = Units.ToUnity(h, 1f).normalized;
        }

        private BodyRecord PrimaryRecord()
        {
            foreach (var r in _sim.Bodies) if (r.Body.IsMassive) return r;
            return null;
        }

        private string PrimaryName()
        {
            var r = PrimaryRecord();
            return r != null ? r.Name : "primary";
        }
    }
}
