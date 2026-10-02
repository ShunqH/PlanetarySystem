using UnityEngine;

namespace PlanetSystem.UI
{
    /// <summary>Remembers how many rows a dropdown's open list may show before it scrolls (see UIFactory.FitListHeight).</summary>
    public sealed class DropdownListSize : MonoBehaviour
    {
        public int MaxVisibleItems = 8;
    }
}
