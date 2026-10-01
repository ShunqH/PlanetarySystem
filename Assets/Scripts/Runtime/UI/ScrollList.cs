using UnityEngine;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>
    /// Vertical list that grows with its rows up to a maximum height, then scrolls (mouse wheel or
    /// two-finger trackpad scroll). Call <see cref="Refresh"/> after adding or removing rows.
    /// </summary>
    public sealed class ScrollList : MonoBehaviour
    {
        public RectTransform Content { get; private set; }
        private LayoutElement _layout;
        private float _maxHeight;

        public static ScrollList Create(Transform parent, float maxHeight, float spacing = 3f)
        {
            var root = UIFactory.CreateRect(parent, "ScrollList");
            var list = root.gameObject.AddComponent<ScrollList>();
            list._maxHeight = maxHeight;
            list._layout = UIFactory.SetLayout(root.gameObject, preferredHeight: 0f, flexibleWidth: 1f);

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            var viewport = UIFactory.CreateRect(root, "Viewport");
            UIFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = Color.white;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = UIFactory.CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            list.Content = content;
            return list;
        }

        /// <summary>Resizes the visible area to the content, capped at the maximum height.</summary>
        public void Refresh()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            float h = LayoutUtility.GetPreferredHeight(Content);
            _layout.preferredHeight = Mathf.Min(h, _maxHeight);
        }
    }
}
