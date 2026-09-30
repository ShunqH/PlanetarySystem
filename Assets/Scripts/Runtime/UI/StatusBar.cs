using System;
using PlanetSystem.Core;
using PlanetSystem.Physics;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>
    /// Bottom strip with integration diagnostics (step size, steps per frame, energy error, achieved speed)
    /// on the left and the latest status or error message on the right.
    /// </summary>
    public sealed class StatusBar : MonoBehaviour
    {
        private SimulationController _controller;
        private Text _diagnostics;
        private Text _message;
        private float _nextRefresh;

        public static StatusBar Create(Transform canvas, SimulationController controller)
        {
            var root = UIFactory.CreateRect(canvas, "StatusBar");
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(0f, 28f);
            root.anchoredPosition = Vector2.zero;
            root.gameObject.AddComponent<Image>().color = UIFactory.PanelColor;
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 4, 4);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var bar = root.gameObject.AddComponent<StatusBar>();
            bar._controller = controller;
            bar._diagnostics = UIFactory.CreateLabel(root, "", 12, color: UIFactory.MutedColor, width: 640f);
            bar._message = UIFactory.CreateLabel(root, "", 12, anchor: TextAnchor.MiddleRight);
            return bar;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;

            var c = _controller;
            string dt = double.IsNaN(c.StepSize) ? "—" : FormatStep(c.StepSize);
            string dE = double.IsNaN(c.RelativeEnergyError) ? "—" : c.RelativeEnergyError.ToString("0.0e0");
            string text = $"dt = {dt}    |ΔE/E₀| = {dE}";
            if (c.IsRunning)
                text += $"    steps/frame = {c.LastResult.Steps}    speed = {c.EffectiveSpeed:0.###} yr/s";
            _diagnostics.text = text;

            _message.text = c.StatusMessage;
            _message.color = c.StatusIsError ? UIFactory.ErrorColor : UIFactory.MutedColor;
        }

        private static string FormatStep(double years)
        {
            double days = years * Constants.DaysPerYear;
            if (days < 1.0) return $"{days * 24.0:0.##} h";
            if (days < 365.0) return $"{days:0.###} d";
            return $"{years:0.###} yr";
        }
    }
}
