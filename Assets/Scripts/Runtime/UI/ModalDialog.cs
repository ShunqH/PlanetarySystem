using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>Result of validating the text of a prompt dialog.</summary>
    public struct PromptCheck
    {
        public bool Ok;
        /// <summary>Hint or error shown under the text field.</summary>
        public string Message;
        /// <summary>Label of the confirm button (e.g. "Save" or "Overwrite").</summary>
        public string ConfirmLabel;
    }

    /// <summary>
    /// Centered modal dialog over a dimmed, click-blocking backdrop: messages, confirmations and a text
    /// prompt. Enter confirms, Esc cancels. Requests made while a dialog is open are queued.
    /// Global hotkeys are suspended while a dialog is open (see <see cref="IsOpen"/>).
    /// </summary>
    public sealed class ModalDialog : MonoBehaviour
    {
        private static ModalDialog _instance;

        /// <summary>True while any dialog is visible.</summary>
        public static bool IsOpen => _instance != null && _instance._root.activeSelf;

        /// <summary>
        /// True while a dialog is open or was closed during this frame, so the Enter/Esc that closed it is
        /// not also handled as a global shortcut.
        /// </summary>
        public static bool OwnsKeyboard => IsOpen || (_instance != null && _instance._closedFrame == Time.frameCount);

        private int _closedFrame = -1;

        private GameObject _root;
        private Text _title;
        private Text _message;
        private InputField _input;
        private Text _hint;
        private Button _confirm;
        private Image _confirmImage;
        private Button _cancel;

        private Action _onConfirm;
        private Action _onCancel;
        private Func<string, PromptCheck> _validate;
        private readonly Queue<Action> _queue = new Queue<Action>();

        public static ModalDialog Create(Transform overlayCanvas)
        {
            var root = UIFactory.CreateRect(overlayCanvas, "ModalDialog");
            UIFactory.Stretch(root);
            root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f); // backdrop blocks clicks

            var panel = UIFactory.CreatePanel(root, "Panel", 480f, padding: 18, spacing: 10f);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;

            var dialog = root.gameObject.AddComponent<ModalDialog>();
            dialog._root = root.gameObject;
            dialog._title = UIFactory.CreateLabel(panel, "", 17, FontStyle.Bold);
            dialog._message = UIFactory.CreateLabel(panel, "", 13, wrap: true);
            dialog._input = UIFactory.CreateInputField(panel, "", null, height: 30f);
            dialog._input.onValueChanged.AddListener(_ => dialog.Revalidate());
            dialog._hint = UIFactory.CreateLabel(panel, "", 12, color: UIFactory.MutedColor, wrap: true);
            var buttons = UIFactory.CreateRow(panel, 32f, 10f);
            UIFactory.CreateSpacer(buttons, 1f).flexibleWidth = 1f;
            dialog._cancel = UIFactory.CreateButton(buttons, "Cancel", dialog.Cancel, UIFactory.NeutralButtonColor, height: 32f, width: 110f);
            dialog._confirm = UIFactory.CreateButton(buttons, "OK", dialog.Confirm, height: 32f, width: 130f);
            dialog._confirmImage = dialog._confirm.GetComponent<Image>();

            root.gameObject.SetActive(false);
            _instance = dialog;
            return dialog;
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Information or warning with a single button.</summary>
        public static void ShowMessage(string title, string message, string buttonLabel = "OK", Action onClose = null)
        {
            Enqueue(d => d.Open(title, message, null, null, buttonLabel, null, false, onClose, onClose));
        }

        /// <summary>Yes/no question. <paramref name="danger"/> colors the confirm button red.</summary>
        public static void ShowConfirm(string title, string message, string confirmLabel, Action onConfirm, bool danger = false)
        {
            Enqueue(d => d.Open(title, message, null, null, confirmLabel, "Cancel", danger, onConfirm, null));
        }

        /// <summary>Text prompt. <paramref name="validate"/> runs on every keystroke and controls the confirm button.</summary>
        public static void ShowPrompt(string title, string message, string initialText, Func<string, PromptCheck> validate,
            Action<string> onConfirm)
        {
            Enqueue(d =>
            {
                d.Open(title, message, initialText, validate, "OK", "Cancel", false, null, null);
                d._onConfirm = () => onConfirm(d._input.text.Trim());
            });
        }

        // ------------------------------------------------------------------

        private static void Enqueue(Action<ModalDialog> show)
        {
            if (_instance == null) return;
            var d = _instance;
            if (IsOpen) d._queue.Enqueue(() => show(d));
            else show(d);
        }

        private void Open(string title, string message, string input, Func<string, PromptCheck> validate,
            string confirmLabel, string cancelLabel, bool danger, Action onConfirm, Action onCancel)
        {
            _title.text = title;
            _message.text = message ?? "";
            _message.gameObject.SetActive(!string.IsNullOrEmpty(message));
            _validate = validate;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            bool prompt = input != null;
            _input.gameObject.SetActive(prompt);
            _hint.gameObject.SetActive(prompt);
            UIFactory.SetButtonText(_confirm, confirmLabel);
            _confirmImage.color = danger ? UIFactory.DangerColor : UIFactory.ButtonColor;
            _cancel.gameObject.SetActive(cancelLabel != null);
            if (cancelLabel != null) UIFactory.SetButtonText(_cancel, cancelLabel);

            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            if (prompt)
            {
                _input.text = input;
                Revalidate();
                EventSystem.current?.SetSelectedGameObject(_input.gameObject);
                _input.ActivateInputField();
                _input.selectionAnchorPosition = 0;
                _input.selectionFocusPosition = input.Length;
            }
            else
            {
                _confirm.interactable = true;
                EventSystem.current?.SetSelectedGameObject(null);
            }
        }

        private void Revalidate()
        {
            if (_validate == null) return;
            var check = _validate(_input.text.Trim());
            _confirm.interactable = check.Ok;
            _hint.text = check.Message ?? "";
            _hint.color = check.Ok ? UIFactory.MutedColor : UIFactory.ErrorColor;
            if (!string.IsNullOrEmpty(check.ConfirmLabel)) UIFactory.SetButtonText(_confirm, check.ConfirmLabel);
        }

        private void Confirm()
        {
            if (!_confirm.interactable) return;
            var action = _onConfirm;
            Close();
            action?.Invoke();
        }

        private void Cancel()
        {
            var action = _onCancel;
            Close();
            action?.Invoke();
        }

        private void Close()
        {
            _closedFrame = Time.frameCount;
            _root.SetActive(false);
            _onConfirm = _onCancel = null;
            _validate = null;
            EventSystem.current?.SetSelectedGameObject(null);
            if (_queue.Count > 0) _queue.Dequeue()();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm();
            else if (kb.escapeKey.wasPressedThisFrame)
            {
                if (_cancel.gameObject.activeSelf) Cancel();
                else Confirm(); // single-button message: Esc just closes it
            }
        }
    }
}
