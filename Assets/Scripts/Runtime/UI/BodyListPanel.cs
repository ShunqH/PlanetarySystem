using System.Collections.Generic;
using PlanetSystem.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>Left-hand list of all bodies with an "Add" button. Clicking a row selects the body.</summary>
    public sealed class BodyListPanel : MonoBehaviour
    {
        private SimulationController _controller;
        private BodyEditorPanel _editor;
        private RectTransform _root;
        private ScrollList _list;
        private Button _addButton;
        private Text _limits;
        private readonly List<GameObject> _rows = new List<GameObject>();

        public static BodyListPanel Create(Transform canvas, SimulationController controller, BodyEditorPanel editor)
        {
            var root = UIFactory.CreatePanel(canvas, "BodyListPanel", 260f);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(12f, -56f);

            var panel = root.gameObject.AddComponent<BodyListPanel>();
            panel._controller = controller;
            panel._editor = editor;
            panel._root = root;
            panel.Build();
            controller.BodiesChanged += panel.Rebuild;
            controller.SelectionChanged += _ => panel.Rebuild();
            controller.RunStateChanged += panel.Rebuild;
            panel.Rebuild();
            return panel;
        }

        private void Build()
        {
            UIFactory.CreateLabel(_root, "Bodies", 17, FontStyle.Bold);
            _limits = UIFactory.CreateLabel(_root, "", 12, color: UIFactory.MutedColor);
            _addButton = UIFactory.CreateButton(_root, "+ Add star", () => _editor.OpenAdd());
            // Scrolls once the list is taller than the space between the toolbar and the bottom HUD.
            _list = ScrollList.Create(_root, 560f);
        }

        private void Rebuild()
        {
            // Deactivate first: Destroy is deferred, and inactive rows are ignored by the layout refresh below.
            foreach (var row in _rows) { row.SetActive(false); Destroy(row); }
            _rows.Clear();

            _limits.text = $"{_controller.MassiveCount} massive   ·   {_controller.TestParticleCount} test particles";
            UIFactory.SetButtonText(_addButton, _controller.Bodies.Count == 0 ? "+ Add star" : "+ Add body");
            _addButton.interactable = !_controller.IsRunning;

            foreach (var rec in _controller.Bodies)
            {
                int id = rec.Id;
                var row = UIFactory.CreateRow(_list.Content, 28f, 8f);
                var bg = row.gameObject.AddComponent<Image>();
                bg.color = id == _controller.SelectedId ? UIFactory.HighlightColor : new Color(1f, 1f, 1f, 0.04f);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.onClick.AddListener(() => _controller.Select(id));

                var hl = row.GetComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset(8, 8, 0, 0);

                var swatch = UIFactory.CreateRect(row, "Swatch");
                swatch.gameObject.AddComponent<Image>().color = rec.Color;
                UIFactory.SetLayout(swatch.gameObject, preferredWidth: 14f, preferredHeight: 14f);

                UIFactory.CreateLabel(row, rec.Name, 13);
                string tag = rec.Body.IsMassive ? $"{FormatMass(rec.Body.Mass)}" : "test";
                UIFactory.CreateLabel(row, tag, 11, color: UIFactory.MutedColor, anchor: TextAnchor.MiddleRight, width: 90f);
                _rows.Add(row.gameObject);
            }
            _list.Refresh();
        }

        private static string FormatMass(double msun)
        {
            if (msun >= 0.01) return $"{msun:0.###} M☉";
            double mj = msun / Physics.Constants.JupiterMassInSolar;
            if (mj >= 0.05) return $"{mj:0.##} M♃";
            return $"{msun / Physics.Constants.EarthMassInSolar:0.##} M⊕";
        }
    }
}
