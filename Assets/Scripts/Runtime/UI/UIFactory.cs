using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>
    /// Builds uGUI widgets from code so the whole interface lives in scripts (no prefabs to maintain).
    /// All widgets are sized for use inside Vertical/HorizontalLayoutGroups.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.11f, 0.94f);
        public static readonly Color FieldColor = new Color(0.16f, 0.18f, 0.23f, 1f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.42f, 0.72f, 1f);
        public static readonly Color DangerColor = new Color(0.70f, 0.25f, 0.25f, 1f);
        public static readonly Color NeutralButtonColor = new Color(0.28f, 0.30f, 0.36f, 1f);
        public static readonly Color TextColor = new Color(0.93f, 0.93f, 0.95f, 1f);
        public static readonly Color MutedColor = new Color(0.65f, 0.68f, 0.75f, 1f);
        public static readonly Color HighlightColor = new Color(0.30f, 0.40f, 0.60f, 0.8f);
        public static readonly Color ErrorColor = new Color(1f, 0.55f, 0.45f, 1f);

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        // ------------------------------------------------------------------
        // Containers
        // ------------------------------------------------------------------

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        /// <summary>Background image with a vertical layout that grows to fit its content.</summary>
        public static RectTransform CreatePanel(Transform parent, string name, float width, int padding = 12, float spacing = 6f)
        {
            var rt = CreateRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = PanelColor;
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rt.sizeDelta = new Vector2(width, 0f);
            return rt;
        }

        /// <summary>Horizontal row for label/field pairs.</summary>
        public static RectTransform CreateRow(Transform parent, float height = 26f, float spacing = 6f)
        {
            var rt = CreateRect(parent, "Row");
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            SetLayout(rt.gameObject, preferredHeight: height, flexibleWidth: 1f);
            return rt;
        }

        public static RectTransform CreateGroup(Transform parent, string name, float spacing = 6f)
        {
            var rt = CreateRect(parent, name);
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            SetLayout(rt.gameObject, flexibleWidth: 1f);
            return rt;
        }

        public static void CreateSpacer(Transform parent, float height)
        {
            var rt = CreateRect(parent, "Spacer");
            SetLayout(rt.gameObject, preferredHeight: height);
        }

        public static LayoutElement SetLayout(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f,
            float flexibleWidth = 0f, float minWidth = -1f, float minHeight = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            le.flexibleWidth = flexibleWidth;
            le.minWidth = minWidth;
            le.minHeight = minHeight;
            return le;
        }

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        // ------------------------------------------------------------------
        // Text
        // ------------------------------------------------------------------

        /// <summary>Raw text component without layout sizing (for use inside custom widgets).</summary>
        public static Text CreateText(Transform parent, string name, string text, int size, TextAnchor anchor, Color color)
        {
            var rt = CreateRect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.text = text;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Text sized for a layout group.</summary>
        public static Text CreateLabel(Transform parent, string text, int size = 14, FontStyle style = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null, float width = -1f, bool wrap = false)
        {
            var t = CreateText(parent, "Label", text, size, anchor, color ?? TextColor);
            t.fontStyle = style;
            if (wrap)
            {
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                SetLayout(t.gameObject, preferredWidth: width, flexibleWidth: width < 0 ? 1f : 0f);
            }
            else
            {
                SetLayout(t.gameObject, preferredWidth: width, preferredHeight: size + 8f, flexibleWidth: width < 0 ? 1f : 0f);
            }
            return t;
        }

        // ------------------------------------------------------------------
        // Buttons
        // ------------------------------------------------------------------

        public static Button CreateButton(Transform parent, string text, UnityAction onClick, Color? background = null,
            float height = 28f, float width = -1f, int fontSize = 14)
        {
            var rt = CreateRect(parent, "Button_" + text);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = background ?? ButtonColor;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);

            var label = CreateText(rt, "Text", text, fontSize, TextAnchor.MiddleCenter, TextColor);
            Stretch(label.rectTransform, 6f, 0f, 6f, 0f);
            SetLayout(rt.gameObject, preferredWidth: width, preferredHeight: height, flexibleWidth: width < 0 ? 1f : 0f);
            return btn;
        }

        public static void SetButtonText(Button button, string text)
        {
            var t = button.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        /// <summary>Small square color swatch button.</summary>
        public static Button CreateSwatch(Transform parent, Color color, UnityAction onClick, float size = 22f)
        {
            var rt = CreateRect(parent, "Swatch");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            SetLayout(rt.gameObject, preferredWidth: size, preferredHeight: size);
            return btn;
        }

        // ------------------------------------------------------------------
        // Input field
        // ------------------------------------------------------------------

        public static InputField CreateInputField(Transform parent, string initial, UnityAction<string> onEndEdit,
            InputField.ContentType contentType = InputField.ContentType.Standard, float height = 26f, float width = -1f)
        {
            var rt = CreateRect(parent, "InputField");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = FieldColor;
            var field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = img;

            var text = CreateText(rt, "Text", "", 13, TextAnchor.MiddleLeft, TextColor);
            text.supportRichText = false;
            Stretch(text.rectTransform, 6f, 2f, 6f, 2f);
            field.textComponent = text;
            field.contentType = contentType;
            field.text = initial;
            field.caretColor = TextColor;
            field.selectionColor = new Color(0.3f, 0.5f, 0.9f, 0.5f);
            if (onEndEdit != null) field.onEndEdit.AddListener(onEndEdit);
            SetLayout(rt.gameObject, preferredWidth: width, preferredHeight: height, flexibleWidth: width < 0 ? 1f : 0f);
            return field;
        }

        // ------------------------------------------------------------------
        // Slider
        // ------------------------------------------------------------------

        public static Slider CreateSlider(Transform parent, float value, UnityAction<float> onChanged, float height = 26f)
        {
            var rt = CreateRect(parent, "Slider");
            var slider = rt.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var bg = CreateRect(rt, "Background");
            bg.gameObject.AddComponent<Image>().color = FieldColor;
            bg.anchorMin = new Vector2(0f, 0.5f);
            bg.anchorMax = new Vector2(1f, 0.5f);
            bg.sizeDelta = new Vector2(0f, 6f);

            var fillArea = CreateRect(rt, "Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(6f, -3f);
            fillArea.offsetMax = new Vector2(-6f, 3f);
            var fill = CreateRect(fillArea, "Fill");
            fill.gameObject.AddComponent<Image>().color = ButtonColor;
            Stretch(fill);

            var handleArea = CreateRect(rt, "Handle Slide Area");
            Stretch(handleArea, 7f, 0f, 7f, 0f);
            var handle = CreateRect(handleArea, "Handle");
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = TextColor;
            handle.sizeDelta = new Vector2(14f, 14f);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            SetLayout(rt.gameObject, preferredHeight: height, flexibleWidth: 1f);
            return slider;
        }

        // ------------------------------------------------------------------
        // Dropdown
        // ------------------------------------------------------------------

        public static Dropdown CreateDropdown(Transform parent, IList<string> options, UnityAction<int> onChanged,
            float height = 26f, float width = -1f, int visibleItems = 8)
        {
            const float itemHeight = 24f;
            var rt = CreateRect(parent, "Dropdown");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = FieldColor;
            var dd = rt.gameObject.AddComponent<Dropdown>();
            dd.targetGraphic = img;

            var caption = CreateText(rt, "Label", "", 13, TextAnchor.MiddleLeft, TextColor);
            Stretch(caption.rectTransform, 8f, 2f, 22f, 2f);
            var arrow = CreateText(rt, "Arrow", "▾", 14, TextAnchor.MiddleCenter, MutedColor);
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(20f, 0f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;

            // Template (hidden until the dropdown opens).
            var template = CreateRect(rt, "Template");
            template.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, 2f);
            template.sizeDelta = new Vector2(0f, itemHeight * Mathf.Min(visibleItems, Mathf.Max(1, options.Count)) + 4f);

            var viewport = CreateRect(template, "Viewport");
            viewport.gameObject.AddComponent<Image>().color = Color.white;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewport, 2f, 2f, 2f, 2f);

            var content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, itemHeight);

            var item = CreateRect(content, "Item");
            item.anchorMin = new Vector2(0f, 0.5f);
            item.anchorMax = new Vector2(1f, 0.5f);
            item.sizeDelta = new Vector2(0f, itemHeight);
            var toggle = item.gameObject.AddComponent<Toggle>();

            var itemBg = CreateRect(item, "Item Background");
            var itemBgImg = itemBg.gameObject.AddComponent<Image>();
            itemBgImg.color = new Color(0.12f, 0.13f, 0.17f, 1f);
            Stretch(itemBg);

            var itemCheck = CreateRect(item, "Item Checkmark");
            var itemCheckImg = itemCheck.gameObject.AddComponent<Image>();
            itemCheckImg.color = ButtonColor;
            itemCheck.anchorMin = new Vector2(0f, 0.5f);
            itemCheck.anchorMax = new Vector2(0f, 0.5f);
            itemCheck.sizeDelta = new Vector2(8f, 8f);
            itemCheck.anchoredPosition = new Vector2(10f, 0f);

            var itemLabel = CreateText(item, "Item Label", "", 13, TextAnchor.MiddleLeft, TextColor);
            Stretch(itemLabel.rectTransform, 20f, 1f, 6f, 1f);

            toggle.targetGraphic = itemBgImg;
            toggle.graphic = itemCheckImg;
            toggle.isOn = true;
            var tc = toggle.colors;
            tc.highlightedColor = HighlightColor;
            tc.selectedColor = HighlightColor;
            toggle.colors = tc;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            dd.template = template;
            dd.captionText = caption;
            dd.itemText = itemLabel;
            template.gameObject.SetActive(false);

            dd.options.Clear();
            foreach (var o in options) dd.options.Add(new Dropdown.OptionData(o));
            dd.RefreshShownValue();
            if (onChanged != null) dd.onValueChanged.AddListener(onChanged);
            SetLayout(rt.gameObject, preferredWidth: width, preferredHeight: height, flexibleWidth: width < 0 ? 1f : 0f);
            return dd;
        }

        public static void SetOptions(Dropdown dd, IList<string> options, int selected)
        {
            dd.options.Clear();
            foreach (var o in options) dd.options.Add(new Dropdown.OptionData(o));
            dd.SetValueWithoutNotify(Mathf.Clamp(selected, 0, Mathf.Max(0, options.Count - 1)));
            dd.RefreshShownValue();
        }
    }
}
