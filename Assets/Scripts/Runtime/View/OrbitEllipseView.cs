using PlanetSystem.Core;
using PlanetSystem.Physics;
using UnityEngine;

namespace PlanetSystem.View
{
    /// <summary>
    /// Draws the osculating orbit of a body as a closed ellipse around its reference primary.
    /// Points are sampled in true anomaly, so pericenter regions get denser sampling automatically.
    /// </summary>
    public sealed class OrbitEllipseView : MonoBehaviour
    {
        private LineRenderer _line;
        private Material _material;
        private Vec3d[] _buffer;
        private Vector3[] _points;

        /// <summary>World position of the focus the ellipse is drawn around (used for line width).</summary>
        public Vector3 Center { get; set; }

        public static OrbitEllipseView Create(Transform parent, string name, int segments)
        {
            var go = new GameObject($"Orbit_{name}");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<OrbitEllipseView>();
            view._line = go.AddComponent<LineRenderer>();
            view._line.useWorldSpace = true;
            view._line.loop = true;
            view._line.positionCount = segments;
            view._line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view._line.receiveShadows = false;
            view._line.numCornerVertices = 2;
            view._material = ViewMaterials.Instance(ViewMaterials.Unlit, Color.white);
            view._line.sharedMaterial = view._material;
            view._buffer = new Vec3d[segments];
            view._points = new Vector3[segments];
            return view;
        }

        public void SetColor(Color color)
        {
            _material.SetColor("_BaseColor", color);
            _material.color = color;
        }

        public void SetWidth(float width)
        {
            _line.startWidth = _line.endWidth = width;
        }

        public void SetVisible(bool visible)
        {
            _line.enabled = visible;
        }

        /// <summary>
        /// Recomputes the ellipse. Returns the largest distance of any sampled point from the world origin,
        /// in AU, which the camera framer uses to fit the whole system on screen.
        /// </summary>
        public double Rebuild(in OrbitalElements elements, Vec3d referencePosition, SimulationSettings settings)
        {
            OrbitConversion.SampleOrbit(elements, _buffer);
            double maxR = 0.0;
            for (int i = 0; i < _buffer.Length; i++)
            {
                var p = referencePosition + _buffer[i];
                double r = p.Length;
                if (r > maxR) maxR = r;
                _points[i] = Units.ToUnity(p, settings.UnitsPerAu);
            }
            _line.SetPositions(_points);
            return maxR;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
