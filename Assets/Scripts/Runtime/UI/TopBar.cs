using System.Collections.Generic;
using PlanetSystem.Core;
using PlanetSystem.Physics.Integrators;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>
    /// Top toolbar: integrator choice, time acceleration, start/stop, reset, simulation clock and
    /// scenario actions. The integrator can only be changed while stopped.
    /// </summary>
    public sealed class TopBar : MonoBehaviour
    {
        private static readonly Color StartColor = new Color(0.20f, 0.55f, 0.33f, 1f);

        private SimulationController _controller;
        private BodyEditorPanel _editor;
        private Dropdown _integrator;
        private Dropdown _speed;
        private Button _run;
        private Image _runImage;
        private Text _clock;
        private float _nextRefresh;

        public static TopBar Create(Transform canvas, SimulationController controller, BodyEditorPanel editor)
        {
            var root = UIFactory.CreateRect(canvas, "TopBar");
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, 44f);
            root.anchoredPosition = Vector2.zero;
            root.gameObject.AddComponent<Image>().color = UIFactory.PanelColor;
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 7, 7);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var bar = root.gameObject.AddComponent<TopBar>();
            bar._controller = controller;
            bar._editor = editor;
            bar.Build(root);
            controller.RunStateChanged += bar.RefreshRunState;
            bar.RefreshRunState();
            return bar;
        }

        private void Build(RectTransform root)
        {
            UIFactory.CreateLabel(root, "PlanetSystem", 18, FontStyle.Bold, width: 132f);

            UIFactory.CreateLabel(root, "Integrator", 12, color: UIFactory.MutedColor, width: 62f);
            _integrator = UIFactory.CreateDropdown(root, IntegratorCatalog.Labels, i => _controller.SetIntegrator(i), width: 250f);
            _integrator.SetValueWithoutNotify(_controller.IntegratorIndex);
            _integrator.RefreshShownValue();

            UIFactory.CreateLabel(root, "Speed", 12, color: UIFactory.MutedColor, width: 38f);
            var speedLabels = new List<string>();
            foreach (var s in SimulationController.SpeedOptions) speedLabels.Add($"{s:0.##} yr / s");
            _speed = UIFactory.CreateDropdown(root, speedLabels, i => _controller.SetSpeed(i), width: 112f, visibleItems: 16);
            _speed.SetValueWithoutNotify(_controller.SpeedIndex);
            _speed.RefreshShownValue();

            _run = UIFactory.CreateButton(root, "Start", ToggleRun, StartColor, width: 96f, fontSize: 14);
            _runImage = _run.GetComponent<Image>();
            UIFactory.CreateButton(root, "Reset", () => { _editor.CloseAdd(); _controller.ResetToInitialConditions(); },
                UIFactory.NeutralButtonColor, width: 66f, fontSize: 13);

            _clock = UIFactory.CreateLabel(root, "t = 0 yr", 15, FontStyle.Bold, TextAnchor.MiddleCenter);

            UIFactory.CreateButton(root, "Load example", () => { _editor.Close(); _controller.LoadCircumbinaryExample(); },
                UIFactory.NeutralButtonColor, width: 112f, fontSize: 13);
            UIFactory.CreateButton(root, "Clear all", () => { _editor.Close(); _controller.Clear(); },
                UIFactory.DangerColor, width: 84f, fontSize: 13);
        }

        private void ToggleRun()
        {
            if (_controller.IsRunning) _controller.StopIntegration();
            else _controller.StartIntegration();
        }

        private void RefreshRunState()
        {
            bool running = _controller.IsRunning;
            UIFactory.SetButtonText(_run, running ? "Stop" : "Start");
            _runImage.color = running ? UIFactory.DangerColor : StartColor;
            _integrator.interactable = !running;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            _clock.text = $"t = {FormatYears(_controller.State.Time)}";
        }

        public static string FormatYears(double t)
        {
            if (t < 1e4) return $"{t:0.000} yr";
            if (t < 1e6) return $"{t:0} yr";
            return $"{t:0.000e0} yr";
        }
    }
}
