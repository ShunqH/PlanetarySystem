using PlanetSystem.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>Bottom-left overlay showing the interaction state, the camera view and the relevant keys.</summary>
    public sealed class ModeHud : MonoBehaviour
    {
        private InteractionController _interaction;
        private Text _mode;
        private Text _keys;
        private float _nextRefresh;

        private const string GlobalKeys = "F1-F4 select · F5 start/stop · F6/F7 slower/faster · F focus mode · Tab next mode · Esc edit mode";
        private const string FocusKeys = "click / F1-F4 a massive body to focus on it · click a test particle for details · 1-6 view direction · scroll zoom";
        private const string EditKeys = "1 oblique · 2 top · 3 along x · 4 along y · 5/6 track primary (⊥ e / along e) · click to select";
        private const string FlyKeys = "1-6 reset to a preset view · drag to look · W/S forward/back · A/D left/right · Q/E down/up · scroll dolly · Shift faster";

        public static ModeHud Create(Transform canvas, InteractionController interaction)
        {
            var root = UIFactory.CreateRect(canvas, "ModeHud");
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(14f, 34f);
            root.sizeDelta = new Vector2(900f, 44f);
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 1f;
            // Grow upward from the bottom-left anchor to fit the text.
            root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var hud = root.gameObject.AddComponent<ModeHud>();
            hud._interaction = interaction;
            hud._mode = UIFactory.CreateLabel(root, "", 14, FontStyle.Bold);
            hud._keys = UIFactory.CreateLabel(root, "", 11, color: UIFactory.MutedColor, width: 900f, wrap: true);
            foreach (var t in new[] { hud._mode, hud._keys })
            {
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.85f);
                o.effectDistance = new Vector2(1f, -1f);
            }
            interaction.StateChanged += _ => hud.Refresh();
            hud.Refresh();
            return hud;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void Refresh()
        {
            if (_interaction.State == InteractionState.Focus)
            {
                _mode.text = $"FOCUS  ·  {_interaction.Camera.FocusTargetName}  ·  view {_interaction.Camera.ViewName}";
                _mode.color = new Color(1f, 0.85f, 0.45f, 1f);
                _keys.text = FocusKeys + "\n" + GlobalKeys;
            }
            else if (_interaction.State == InteractionState.FreeFly)
            {
                _mode.text = "FREE FLY";
                _mode.color = new Color(0.55f, 0.85f, 1f, 1f);
                _keys.text = FlyKeys + "\n" + GlobalKeys;
            }
            else
            {
                _mode.text = $"EDIT  ·  view {_interaction.Camera.ViewName}";
                _mode.color = UIFactory.TextColor;
                _keys.text = EditKeys + "\n" + GlobalKeys;
            }
        }
    }
}
