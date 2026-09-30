using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>
    /// "Label | slider | text box" row for a numeric value. The slider can be linear or logarithmic;
    /// the text box accepts any value inside the range. Changes from either control raise <see cref="ValueChanged"/>.
    /// </summary>
    public sealed class NumberField : MonoBehaviour
    {
        private Text _label;
        private Slider _slider;
        private InputField _input;
        private double _min, _max, _value;
        private bool _log;
        private string _format;
        private bool _suppress;

        public event Action<double> ValueChanged;

        public double Value => _value;

        public static NumberField Create(Transform parent, string label, double min, double max, double value,
            bool logarithmic = false, string format = "0.###", float labelWidth = 120f, float inputWidth = 70f)
        {
            var row = UIFactory.CreateRow(parent);
            var nf = row.gameObject.AddComponent<NumberField>();
            nf._label = UIFactory.CreateLabel(row, label, 13, color: UIFactory.MutedColor, width: labelWidth);
            nf._slider = UIFactory.CreateSlider(row, 0f, nf.OnSlider);
            nf._input = UIFactory.CreateInputField(row, "", nf.OnInputEnd, InputField.ContentType.DecimalNumber, width: inputWidth);
            nf._format = format;
            nf.SetRange(min, max, logarithmic);
            nf.SetValue(value);
            return nf;
        }

        public void SetLabel(string label) => _label.text = label;

        public void SetRange(double min, double max, bool logarithmic)
        {
            _log = logarithmic;
            _min = logarithmic ? Math.Max(min, 1e-12) : min;
            _max = Math.Max(max, _min * (logarithmic ? 1.0001 : 1.0) + (logarithmic ? 0.0 : 1e-12));
            SetValue(_value);
        }

        /// <summary>Sets the value without raising <see cref="ValueChanged"/>.</summary>
        public void SetValue(double value)
        {
            _value = Clamp(value);
            _suppress = true;
            _slider.SetValueWithoutNotify(ToSlider(_value));
            _input.SetTextWithoutNotify(_value.ToString(_format, CultureInfo.InvariantCulture));
            _suppress = false;
        }

        public void SetInteractable(bool interactable)
        {
            _slider.interactable = interactable;
            _input.interactable = interactable;
        }

        private double Clamp(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) v = _min;
            return Math.Min(_max, Math.Max(_min, v));
        }

        private float ToSlider(double v)
        {
            if (_log) return (float)((Math.Log10(v) - Math.Log10(_min)) / (Math.Log10(_max) - Math.Log10(_min)));
            return (float)((v - _min) / (_max - _min));
        }

        private double FromSlider(float t)
        {
            if (_log) return Math.Pow(10.0, Math.Log10(_min) + t * (Math.Log10(_max) - Math.Log10(_min)));
            return _min + t * (_max - _min);
        }

        private void OnSlider(float t)
        {
            if (_suppress) return;
            _value = Clamp(FromSlider(t));
            _suppress = true;
            _input.SetTextWithoutNotify(_value.ToString(_format, CultureInfo.InvariantCulture));
            _suppress = false;
            ValueChanged?.Invoke(_value);
        }

        private void OnInputEnd(string text)
        {
            if (_suppress) return;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                SetValue(_value);
                return;
            }
            SetValue(v);
            ValueChanged?.Invoke(_value);
        }
    }
}
