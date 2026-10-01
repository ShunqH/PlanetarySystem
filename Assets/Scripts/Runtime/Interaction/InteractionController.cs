using System;
using PlanetSystem.Core;
using PlanetSystem.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PlanetSystem.Interaction
{
    /// <summary>
    /// Interaction states, cycled with Tab. Add new states at the end; <see cref="InteractionController"/>
    /// cycles through all enum values in order.
    /// </summary>
    public enum InteractionState
    {
        /// <summary>Fixed / tracking camera presets (keys 1-6); bodies are selected by clicking.</summary>
        Edit = 0,
        /// <summary>Drone-style camera: drag to look, WASD + QE to move, scroll to dolly.</summary>
        FreeFly = 1,
    }

    /// <summary>
    /// Global keyboard shortcuts, the interaction state machine and pointer handling (click to select,
    /// drag to look in free-fly). Shortcuts are ignored while a text field has keyboard focus.
    ///
    /// Any state:  F1-F4 select bodies 1-4 · F5 start/stop · F6 slower · F7 faster · Esc edit state · Tab next state
    /// Any state:  1-6 camera presets (in free-fly they reset the camera to that view and flying continues)
    /// </summary>
    public sealed class InteractionController : MonoBehaviour
    {
        /// <summary>Pointer travel (pixels) that turns a click into a drag.</summary>
        private const float DragThreshold = 5f;

        private SimulationController _sim;
        private CameraController _camera;
        private SceneViewManager _scene;

        private bool _pressActive;
        private bool _pressOverUI;
        private bool _dragging;
        private Vector2 _pressPosition;

        public InteractionState State { get; private set; } = InteractionState.Edit;

        /// <summary>Raised after the interaction state changes.</summary>
        public event Action<InteractionState> StateChanged;

        public CameraController Camera => _camera;

        public void Initialize(SimulationController sim, CameraController camera, SceneViewManager scene)
        {
            _sim = sim;
            _camera = camera;
            _scene = scene;
        }

        public void SetState(InteractionState state)
        {
            if (State == state) return;
            State = state;
            if (state == InteractionState.FreeFly)
            {
                // Drop UI focus so no widget reacts to the movement keys.
                EventSystem.current?.SetSelectedGameObject(null);
                _camera.EnterFreeFly();
            }
            else
            {
                _camera.ExitFreeFly(); // keep the current pose; presets are applied on 1-6
            }
            StateChanged?.Invoke(state);
        }

        public void CycleState()
        {
            int count = Enum.GetValues(typeof(InteractionState)).Length;
            SetState((InteractionState)(((int)State + 1) % count));
        }

        private void Update()
        {
            // A modal dialog owns the keyboard (Enter / Esc) and blocks the scene.
            bool typing = IsTyping() || UI.ModalDialog.OwnsKeyboard;
            _camera.KeyboardEnabled = !typing;
            _camera.ScrollEnabled = !PointerOverUI();

            if (!typing) HandleKeyboard();
            HandlePointer();
        }

        // ------------------------------------------------------------------
        // Keyboard
        // ------------------------------------------------------------------

        private void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // --- Global shortcuts ---
            if (kb.f1Key.wasPressedThisFrame) SelectByIndex(0);
            if (kb.f2Key.wasPressedThisFrame) SelectByIndex(1);
            if (kb.f3Key.wasPressedThisFrame) SelectByIndex(2);
            if (kb.f4Key.wasPressedThisFrame) SelectByIndex(3);
            if (kb.f5Key.wasPressedThisFrame)
            {
                if (_sim.IsRunning) _sim.StopIntegration();
                else _sim.StartIntegration();
            }
            if (kb.f6Key.wasPressedThisFrame) _sim.SetSpeed(_sim.SpeedIndex - 1);
            if (kb.f7Key.wasPressedThisFrame) _sim.SetSpeed(_sim.SpeedIndex + 1);
            if (kb.escapeKey.wasPressedThisFrame) SetState(InteractionState.Edit);
            if (kb.tabKey.wasPressedThisFrame) CycleState();

            // --- Camera presets (edit: held / tracked; free-fly: glide there, then keep flying) ---
            {
                if (Pressed(kb.digit1Key, kb.numpad1Key)) _camera.SetView(CameraView.Oblique);
                if (Pressed(kb.digit2Key, kb.numpad2Key)) _camera.SetView(CameraView.Top);
                if (Pressed(kb.digit3Key, kb.numpad3Key)) _camera.SetView(CameraView.AlongX);
                if (Pressed(kb.digit4Key, kb.numpad4Key)) _camera.SetView(CameraView.AlongY);
                if (Pressed(kb.digit5Key, kb.numpad5Key)) _camera.SetView(CameraView.TrackPerpendicular);
                if (Pressed(kb.digit6Key, kb.numpad6Key)) _camera.SetView(CameraView.TrackPericenter);
            }
        }

        private static bool Pressed(UnityEngine.InputSystem.Controls.KeyControl a, UnityEngine.InputSystem.Controls.KeyControl b) =>
            a.wasPressedThisFrame || b.wasPressedThisFrame;

        private void SelectByIndex(int index)
        {
            if (index < _sim.Bodies.Count) _sim.Select(_sim.Bodies[index].Id);
        }

        private static bool IsTyping()
        {
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return false;
            var field = es.currentSelectedGameObject.GetComponent<InputField>();
            return field != null && field.isFocused;
        }

        private static bool PointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------------
        // Pointer: click selects (any state), drag looks around (free-fly)
        // ------------------------------------------------------------------

        private void HandlePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var button = mouse.leftButton;
            Vector2 pos = mouse.position.ReadValue();

            if (button.wasPressedThisFrame)
            {
                _pressActive = true;
                _pressOverUI = PointerOverUI();
                _dragging = false;
                _pressPosition = pos;
            }

            if (_pressActive && button.isPressed && !_pressOverUI)
            {
                if (!_dragging && (pos - _pressPosition).sqrMagnitude > DragThreshold * DragThreshold) _dragging = true;
                if (_dragging && State == InteractionState.FreeFly) _camera.Look(mouse.delta.ReadValue());
            }

            if (_pressActive && button.wasReleasedThisFrame)
            {
                if (!_pressOverUI && !_dragging) _sim.Select(_scene.PickBody(pos));
                _pressActive = false;
                _dragging = false;
            }
        }
    }
}
