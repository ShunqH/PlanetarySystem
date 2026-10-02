using System.Collections.Generic;
using PlanetSystem.Core;
using UnityEngine;

namespace PlanetSystem.View
{
    /// <summary>
    /// A square grid in the reference plane (physics xy = Unity xz) with brighter axes through the origin.
    /// Built as line meshes so it costs two draw calls regardless of size.
    /// </summary>
    public sealed class ReferenceGrid : MonoBehaviour
    {
        private MeshFilter _gridFilter, _axisFilter;
        private float _currentExtentAu = -1f;
        private float _currentSpacingAu = -1f;
        private SimulationSettings _settings;

        public static ReferenceGrid Create(Transform parent, SimulationSettings settings)
        {
            var go = new GameObject("ReferenceGrid");
            go.transform.SetParent(parent, false);
            var grid = go.AddComponent<ReferenceGrid>();
            grid._settings = settings;
            grid._gridFilter = CreateLayer(go.transform, "GridLines", settings.GridColor);
            grid._axisFilter = CreateLayer(go.transform, "AxisLines", settings.GridAxisColor);
            return grid;
        }

        private static MeshFilter CreateLayer(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ViewMaterials.Instance(ViewMaterials.Unlit, color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mf.sharedMesh = new Mesh { name = name };
            return mf;
        }

        /// <summary>Rebuilds the grid so it covers at least the given half-extent (AU). Cheap when unchanged.</summary>
        public void EnsureExtent(double halfExtentAu)
        {
            float spacing = Mathf.Max(0.01f, _settings.GridSpacingAu);
            // Choose a spacing (power-of-two multiple of the base) that keeps between about 2 and 20 lines
            // per side: coarser for large systems, finer for small focus frames.
            while (halfExtentAu / spacing > 20) spacing *= 2f;
            while (halfExtentAu / spacing < 2 && spacing > 1e-4f) spacing *= 0.5f;
            float extent = Mathf.Max(spacing * 2f, Mathf.Ceil((float)halfExtentAu / spacing) * spacing);
            if (Mathf.Approximately(extent, _currentExtentAu) && Mathf.Approximately(spacing, _currentSpacingAu)) return;
            _currentExtentAu = extent;
            _currentSpacingAu = spacing;

            float s = _settings.UnitsPerAu;
            var gridVerts = new List<Vector3>();
            var axisVerts = new List<Vector3>();
            int lines = Mathf.RoundToInt(extent / spacing);
            for (int i = -lines; i <= lines; i++)
            {
                float c = i * spacing * s;
                var target = i == 0 ? axisVerts : gridVerts;
                target.Add(new Vector3(c, 0f, -extent * s));
                target.Add(new Vector3(c, 0f, extent * s));
                target.Add(new Vector3(-extent * s, 0f, c));
                target.Add(new Vector3(extent * s, 0f, c));
            }
            Fill(_gridFilter.sharedMesh, gridVerts);
            Fill(_axisFilter.sharedMesh, axisVerts);
        }

        private static void Fill(Mesh mesh, List<Vector3> verts)
        {
            mesh.Clear();
            mesh.SetVertices(verts);
            var indices = new int[verts.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }
    }
}
