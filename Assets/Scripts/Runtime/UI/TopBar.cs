using PlanetSystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>Title bar with global actions. Time controls will be added here once integration exists.</summary>
    public sealed class TopBar : MonoBehaviour
    {
        public static TopBar Create(Transform canvas, SimulationController controller, BodyEditorPanel editor)
        {
            var root = UIFactory.CreateRect(canvas, "TopBar");
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, 40f);
            root.anchoredPosition = Vector2.zero;
            root.gameObject.AddComponent<Image>().color = UIFactory.PanelColor;
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 6, 6);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var bar = root.gameObject.AddComponent<TopBar>();
            UIFactory.CreateLabel(root, "PlanetSystem", 18, FontStyle.Bold, width: 150f);
            UIFactory.CreateLabel(root, "Interactive N-body sandbox  ·  click a body to select it", 12, color: UIFactory.MutedColor);
            UIFactory.CreateButton(root, "Load example", () => { editor.Close(); controller.LoadCircumbinaryExample(); },
                UIFactory.NeutralButtonColor, width: 120f, fontSize: 13);
            UIFactory.CreateButton(root, "Clear all", () => { editor.Close(); controller.Clear(); },
                UIFactory.DangerColor, width: 90f, fontSize: 13);
            return bar;
        }
    }
}
