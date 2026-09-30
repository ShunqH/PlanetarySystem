using UnityEngine;

namespace PlanetSystem.View
{
    /// <summary>
    /// Shared access to the URP materials shipped in Resources/Materials.
    /// Materials live in Resources so they (and their shaders) are guaranteed to be included in builds.
    /// </summary>
    public static class ViewMaterials
    {
        private static Material _lit, _unlit;

        /// <summary>URP Lit material used for planets (receives directional light).</summary>
        public static Material Lit => _lit != null ? _lit : (_lit = Load("Materials/BodyLit", "Universal Render Pipeline/Lit"));

        /// <summary>URP Unlit material used for stars, orbit lines and the grid.</summary>
        public static Material Unlit => _unlit != null ? _unlit : (_unlit = Load("Materials/Unlit", "Universal Render Pipeline/Unlit"));

        /// <summary>Creates a private instance of a material so its color can be changed independently.</summary>
        public static Material Instance(Material source, Color color)
        {
            var m = new Material(source);
            m.SetColor("_BaseColor", color);
            m.color = color;
            return m;
        }

        private static Material Load(string path, string fallbackShader)
        {
            var m = Resources.Load<Material>(path);
            if (m != null) return m;
            Debug.LogWarning($"Material '{path}' not found in Resources; falling back to Shader.Find(\"{fallbackShader}\").");
            var shader = Shader.Find(fallbackShader);
            return new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        }
    }
}
