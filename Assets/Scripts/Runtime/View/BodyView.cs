using PlanetSystem.Core;
using PlanetSystem.Physics;
using UnityEngine;

namespace PlanetSystem.View
{
    /// <summary>
    /// Renders one body: a sphere plus a selection ring, both children of an unscaled root placed at the body.
    /// Stars use an unlit self-luminous material, planets a lit one.
    ///
    /// Size: the base radius is exaggerated logarithmically from the physical radius (so planets are
    /// visible at AU scales while staying ordered by size), and the sphere is never drawn smaller than a
    /// few pixels on screen, so every body stays visible however far out the camera is.
    /// </summary>
    public sealed class BodyView : MonoBehaviour
    {
        private const int RingSegments = 64;

        public int BodyId { get; private set; }

        private Transform _sphere;
        private MeshRenderer _renderer;
        private Material _material;
        private bool _isStar;
        private LineRenderer _selectionRing;
        private float _baseRadius;
        private float _ringRadius = -1f;

        /// <summary>Radius actually drawn this frame (Unity units), including the on-screen minimum.</summary>
        public float RenderedRadius { get; private set; }

        public static BodyView Create(Transform parent, int bodyId, string name)
        {
            var root = new GameObject($"Body_{name}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<BodyView>();
            view.BodyId = bodyId;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Sphere";
            // Picking is done in screen space (SceneViewManager.PickBody), so no physics collider is needed.
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(root.transform, false);
            view._sphere = sphere.transform;
            view._renderer = sphere.GetComponent<MeshRenderer>();
            view._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view._renderer.receiveShadows = false;

            var ringGo = new GameObject("SelectionRing");
            ringGo.transform.SetParent(root.transform, false);
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = RingSegments;
            ring.material = ViewMaterials.Instance(ViewMaterials.Unlit, Color.white);
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.enabled = false;
            view._selectionRing = ring;
            return view;
        }

        /// <summary>Updates appearance (material, color, base radius) from the body. Called on structural changes.</summary>
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
            _baseRadius = DisplayRadius(body.Radius, settings);
            if (RenderedRadius <= 0f) SetRenderedRadius(_baseRadius);
        }

        /// <summary>Logarithmically exaggerated radius in Unity units, before the on-screen minimum.</summary>
        public static float DisplayRadius(double radiusAu, SimulationSettings settings)
        {
            double inEarthRadii = radiusAu / Constants.EarthRadiusInAu;
            return settings.MinDisplayRadius + settings.RadiusLogScale * (float)System.Math.Log10(1.0 + inEarthRadii);
        }

        /// <summary>Moves the body to its current position.</summary>
        public void ApplyPosition(Body body, SimulationSettings settings)
        {
            transform.position = Units.ToUnity(body.Position, settings.UnitsPerAu);
        }

        /// <summary>
        /// Applies the on-screen minimum size for the current camera and orients the selection ring.
        /// Call after the camera has moved for this frame.
        /// </summary>
        public void UpdateForCamera(Camera cam, SimulationSettings settings)
        {
            float distance = Vector3.Distance(cam.transform.position, transform.position);
            float unitsPerPixel = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            float minPixels = _isStar ? settings.MinStarPixelRadius : settings.MinBodyPixelRadius;
            SetRenderedRadius(Mathf.Max(_baseRadius, minPixels * unitsPerPixel));

            if (_selectionRing.enabled)
            {
                _selectionRing.transform.rotation = cam.transform.rotation;
                float width = Mathf.Max(0.12f * RenderedRadius, 1.5f * unitsPerPixel);
                _selectionRing.startWidth = _selectionRing.endWidth = width;
            }
        }

        private void SetRenderedRadius(float r)
        {
            RenderedRadius = r;
            _sphere.localScale = Vector3.one * (2f * r);
            float ringRadius = 1.6f * r;
            if (Mathf.Abs(ringRadius - _ringRadius) <= 1e-3f * ringRadius) return;
            _ringRadius = ringRadius;
            for (int i = 0; i < RingSegments; i++)
            {
                float t = Mathf.PI * 2f * i / RingSegments;
                _selectionRing.SetPosition(i, new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * ringRadius);
            }
        }

        public void SetSelected(bool selected)
        {
            _selectionRing.enabled = selected;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_selectionRing != null && _selectionRing.sharedMaterial != null) Destroy(_selectionRing.sharedMaterial);
        }
    }
}
