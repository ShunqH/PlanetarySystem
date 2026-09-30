using UnityEngine;
using UnityEngine.UI;

namespace PlanetSystem.View
{
    /// <summary>Screen-space name tag that follows a body.</summary>
    public sealed class BodyLabel : MonoBehaviour
    {
        private Text _text;
        private RectTransform _rect;

        public static BodyLabel Create(Transform canvas, Font font)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var label = go.AddComponent<BodyLabel>();
            label._rect = go.GetComponent<RectTransform>();
            label._rect.anchorMin = label._rect.anchorMax = Vector2.zero;
            label._rect.pivot = new Vector2(0f, 0.5f);
            label._rect.sizeDelta = new Vector2(200f, 20f);
            label._text = go.AddComponent<Text>();
            label._text.font = font;
            label._text.fontSize = 13;
            label._text.alignment = TextAnchor.MiddleLeft;
            label._text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        public void Set(string text, Color color)
        {
            _text.text = text;
            _text.color = color;
        }

        /// <summary>Places the label next to a world position; hides it when behind the camera.</summary>
        public void Follow(Camera cam, Vector3 worldPos, float screenOffset)
        {
            var sp = cam.WorldToScreenPoint(worldPos);
            bool visible = sp.z > 0f;
            _text.enabled = visible;
            if (!visible) return;
            _rect.anchoredPosition = new Vector2(sp.x + screenOffset, sp.y + screenOffset * 0.6f);
        }
    }
}
