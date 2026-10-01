using System.Collections.Generic;
using PlanetSystem.Core;
using PlanetSystem.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>
    /// Drop-down panel opened from the toolbar's "Examples" button: built-in presets followed by saved
    /// cases. Select a row, then Load it, or Delete it (saved cases only). Clicking outside closes it.
    /// </summary>
    public sealed class ScenarioMenu : MonoBehaviour
    {
        private SimulationController _controller;
        private ScenarioLibrary _library;
        private BodyEditorPanel _editor;

        private GameObject _root;
        private ScrollList _list;
        private Text _info;
        private Button _load;
        private Button _delete;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private Scenario _selected;

        public bool IsOpen => _root.activeSelf;

        public static ScenarioMenu Create(Transform overlayCanvas, SimulationController controller, ScenarioLibrary library,
            BodyEditorPanel editor)
        {
            var container = UIFactory.CreateRect(overlayCanvas, "ScenarioMenu");
            UIFactory.Stretch(container);

            // Full-screen transparent blocker behind the panel (a sibling, so clicks inside the panel do not
            // bubble up to it): a click anywhere outside the panel closes the menu.
            var blocker = UIFactory.CreateRect(container, "Blocker");
            UIFactory.Stretch(blocker);
            blocker.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            var close = blocker.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;

            var panel = UIFactory.CreatePanel(container, "Panel", 400f, padding: 12, spacing: 6f);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-12f, -50f);

            var menu = container.gameObject.AddComponent<ScenarioMenu>();
            menu._controller = controller;
            menu._library = library;
            menu._editor = editor;
            menu._root = container.gameObject;
            close.onClick.AddListener(menu.Close);

            UIFactory.CreateLabel(panel, "Examples", 16, FontStyle.Bold);
            menu._list = ScrollList.Create(panel, 330f);
            menu._info = UIFactory.CreateLabel(panel, "", 12, color: UIFactory.MutedColor, wrap: true);
            var buttons = UIFactory.CreateRow(panel, 30f, 8f);
            menu._load = UIFactory.CreateButton(buttons, "Load", menu.LoadSelected);
            menu._delete = UIFactory.CreateButton(buttons, "Delete", menu.DeleteSelected, UIFactory.DangerColor, width: 90f);
            UIFactory.CreateButton(buttons, "Close", menu.Close, UIFactory.NeutralButtonColor, width: 80f);

            library.Changed += () => { if (menu.IsOpen) menu.Rebuild(); };
            controller.RunStateChanged += () => { if (controller.IsRunning) menu.Close(); };
            container.gameObject.SetActive(false);
            return menu;
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_controller.IsRunning) return;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            Rebuild();
        }

        public void Close() => _root.SetActive(false);

        private void Rebuild()
        {
            // Deactivate first: Destroy is deferred, and inactive rows are ignored by the layout refresh below.
            foreach (var r in _rows) { r.SetActive(false); Destroy(r); }
            _rows.Clear();

            // Keep the selection if it still exists.
            if (_selected != null && !_selected.IsBuiltIn && _library.FindSaved(_selected.Name) == null) _selected = null;

            AddHeader("Presets");
            foreach (var s in _library.BuiltIns) AddRow(s);
            AddHeader(_library.Saved.Count == 0 ? "Saved cases (none yet: use Save)" : "Saved cases");
            foreach (var s in _library.Saved) AddRow(s);

            _list.Refresh();
            RefreshSelection();
        }

        private void AddHeader(string text)
        {
            var label = UIFactory.CreateLabel(_list.Content, text, 11, FontStyle.Bold, color: UIFactory.MutedColor);
            _rows.Add(label.gameObject);
        }

        private void AddRow(Scenario s)
        {
            var row = UIFactory.CreateRow(_list.Content, 26f, 8f);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 8, 0, 0);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = IsSelected(s) ? UIFactory.HighlightColor : new Color(1f, 1f, 1f, 0.04f);
            var btn = row.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => { _selected = s; RefreshSelection(); });
            UIFactory.CreateLabel(row, s.Name, 13);
            UIFactory.CreateLabel(row, s.IsBuiltIn ? "preset" : "saved", 11, color: UIFactory.MutedColor,
                anchor: TextAnchor.MiddleRight, width: 50f);
            _rows.Add(row.gameObject);
        }

        private bool IsSelected(Scenario s) =>
            _selected != null && s.IsBuiltIn == _selected.IsBuiltIn && Scenario.NamesEqual(s.Name, _selected.Name);

        private void RefreshSelection()
        {
            foreach (var row in _rows)
            {
                var btn = row.GetComponent<Button>();
                if (btn == null) continue;
                var name = row.GetComponentInChildren<Text>().text;
                bool selected = _selected != null && Scenario.NamesEqual(name, _selected.Name);
                ((Image)btn.targetGraphic).color = selected ? UIFactory.HighlightColor : new Color(1f, 1f, 1f, 0.04f);
            }

            _load.interactable = _selected != null;
            _delete.interactable = _selected != null && !_selected.IsBuiltIn;
            if (_selected == null)
            {
                _info.text = "Select a case to load it. Built-in presets cannot be deleted.";
                return;
            }
            string text = _selected.Summary;
            if (!string.IsNullOrEmpty(_selected.Description)) text = _selected.Description + "\n" + text;
            _info.text = text;
        }

        private void LoadSelected()
        {
            if (_selected == null) return;
            var scenario = _selected;
            if (_controller.HasUnsavedChanges)
            {
                ModalDialog.ShowConfirm("Load case",
                    $"Load \"{scenario.Name}\"? The current system has unsaved changes and will be replaced.",
                    "Load", () => DoLoad(scenario), danger: true);
            }
            else
            {
                DoLoad(scenario);
            }
        }

        private void DoLoad(Scenario scenario)
        {
            _editor.Close();
            string error = _controller.LoadScenario(scenario);
            if (error != null)
            {
                ModalDialog.ShowMessage("Could not load case", error);
                return;
            }
            Close();
        }

        private void DeleteSelected()
        {
            if (_selected == null || _selected.IsBuiltIn) return;
            string name = _selected.Name;
            ModalDialog.ShowConfirm("Delete case", $"Delete the saved case \"{name}\"? This cannot be undone.", "Delete", () =>
            {
                string error = _library.Delete(name);
                if (error != null) ModalDialog.ShowMessage("Could not delete case", error);
                else _selected = null;
                if (IsOpen) Rebuild();
            }, danger: true);
        }

        private void Update()
        {
            if (ModalDialog.OwnsKeyboard) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        }
    }
}
