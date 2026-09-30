using PlanetSystem.Core;
using PlanetSystem.Physics;
using UnityEngine;

namespace PlanetSystem.View
{
    /// <summary>
    /// Renders one body as a sphere. Stars use an unlit self-luminous material, planets a lit one.
    /// The rendered radius is exaggerated logarithmically so small planets remain visible at AU scales.
    /// </summary>
    public sealed class BodyView : MonoBehaviour
    {
        public int BodyId { get; private set; }

        private MeshRenderer _renderer;
        private Material _material;
        private bool _isStar;
        private LineRenderer _selectionRing;
        private float _displayRadius;

        public static BodyView Create(Transform parent, int bodyId, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Body_{name}";
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BodyView>();
            view.BodyId = bodyId;
            view._renderer = go.GetComponent<MeshRenderer>();
            view._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view._renderer.receiveShadows = false;
            view.CreateSelectionRing();
            return view;
        }

        private void CreateSelectionRing()
        {
            var ringGo = new GameObject("SelectionRing");
            ringGo.transform.SetParent(transform, false);
            _selectionRing = ringGo.AddComponent<LineRenderer>();
            _selectionRing.useWorldSpace = false;
            _selectionRing.loop = true;
            _selectionRing.positionCount = 64;
            _selectionRing.material = ViewMaterials.Instance(ViewMaterials.Unlit, Color.white);
            _selectionRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _selectionRing.receiveShadows = false;
            for (int i = 0; i < 64; i++)
            {
                float t = Mathf.PI * 2f * i / 64f;
                // Local space of the sphere is unit diameter; the ring sits at 1.6x the sphere radius.
                _selectionRing.SetPosition(i, new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * 0.8f);
            }
            _selectionRing.enabled = false;
        }

        /// <summary>Updates appearance (material, color, radius) from the record. Called on structural changes.</summary>
        public void ApplyAppearance(Body body, Color color, SimulationSettings settings)
        {
            gameObject.name = $"Body_{body.Name}";
            bool isStar = body.IsMassive && body.Mass >= settings.StarMassThreshold;
            if (_material == null || isStar != _isStar)
            {
                if (_material != null) Destroy(_material);
                _isStar = isStar;
                _material = ViewMaterials.Instance(isStar ? ViewMaterials.Unlit : ViewMaterials.Lit, color);
                _renderer.sharedMaterial = _material;
            }
            _material.SetColor("_BaseColor", color);
            _material.color = color;

            _displayRadius = DisplayRadius(body.Radius, settings);
            transform.localScale = Vector3.one * (_displayRadius * 2f);
            _selectionRing.startWidth = _selectionRing.endWidth = 0.08f / Mathf.Max(_displayRadius * 2f, 1e-4f) * 0.5f;
        }

        /// <summary>Exaggerated rendered radius in Unity units.</summary>
        public static float DisplayRadius(double radiusAu, SimulationSettings settings)
        {
            double inEarthRadii = radiusAu / Constants.EarthRadiusInAu;
            return settings.MinDisplayRadius + settings.RadiusLogScale * (float)System.Math.Log10(1.0 + inEarthRadii);
        }

        public float DisplayRadiusUnits => _displayRadius;

        /// <summary>Moves the sphere to the body's current position.</summary>
        public void ApplyPosition(Body body, SimulationSettings settings)
        {
            transform.position = Units.ToUnity(body.Position, settings.UnitsPerAu);
        }

        public void SetSelected(bool selected)
        {
            _selectionRing.enabled = selected;
        }

        /// <summary>Billboard the selection ring toward the camera.</summary>
        public void FaceCamera(Camera cam)
        {
            if (_selectionRing.enabled && cam != null)
                _selectionRing.transform.rotation = cam.transform.rotation;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_selectionRing != null && _selectionRing.sharedMaterial != null) Destroy(_selectionRing.sharedMaterial);
        }
    }
}
