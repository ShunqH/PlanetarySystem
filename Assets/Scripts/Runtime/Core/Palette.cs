using UnityEngine;

namespace PlanetSystem.Core
{
    /// <summary>Color choices for bodies; the UI shows these as swatches.</summary>
    public static class Palette
    {
        public static readonly Color[] BodyColors =
        {
            new Color(1.00f, 0.85f, 0.35f), // warm yellow (sun-like)
            new Color(1.00f, 0.55f, 0.25f), // orange (K dwarf)
            new Color(0.95f, 0.35f, 0.30f), // red (M dwarf)
            new Color(0.55f, 0.75f, 1.00f), // pale blue
            new Color(0.35f, 0.90f, 0.75f), // teal
            new Color(0.60f, 0.95f, 0.40f), // green
            new Color(0.85f, 0.55f, 1.00f), // violet
            new Color(0.90f, 0.90f, 0.90f), // white/grey
        };

        public static Color Pick(int index) => BodyColors[((index % BodyColors.Length) + BodyColors.Length) % BodyColors.Length];
    }
}
