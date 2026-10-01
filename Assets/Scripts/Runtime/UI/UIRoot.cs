using PlanetSystem.Core;
using PlanetSystem.Interaction;
using PlanetSystem.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>Creates the event system and canvases, then assembles all panels.</summary>
    public static class UIRoot
    {
        /// <summary>Canvas for world-anchored labels; drawn underneath the control panels.</summary>
        public static Canvas CreateLabelCanvas()
        {
            var go = new GameObject("LabelCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            return canvas;
        }

        public static void Build(SimulationController controller, SceneViewManager scene, InteractionController interaction,
            ScenarioLibrary library)
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                // InputSystemUIInputModule falls back to the package's default actions when none are assigned.
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            // Keyboard UI navigation would steal WASD / arrows / Enter from the camera (e.g. nudging a
            // selected slider while flying). Text fields still receive typing.
            EventSystem.current.sendNavigationEvents = false;

            var canvasGo = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            // Overlay canvas for pop-ups and dialogs, drawn above all panels.
            var overlayGo = new GameObject("OverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var overlay = overlayGo.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 100;
            var overlayScaler = overlayGo.GetComponent<CanvasScaler>();
            overlayScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            overlayScaler.referenceResolution = scaler.referenceResolution;
            overlayScaler.matchWidthOrHeight = scaler.matchWidthOrHeight;

            var editor = BodyEditorPanel.Create(canvasGo.transform, controller, scene);
            BodyListPanel.Create(canvasGo.transform, controller, editor);
            var menu = ScenarioMenu.Create(overlayGo.transform, controller, library, editor);
            TopBar.Create(canvasGo.transform, controller, editor, library, menu);
            ModalDialog.Create(overlayGo.transform);

            controller.BodyCountWarning += message => ModalDialog.ShowMessage("Performance warning", message);
            if (library.LoadProblem != null) ModalDialog.ShowMessage("Saved cases", library.LoadProblem);
            StatusBar.Create(canvasGo.transform, controller);
            ModeHud.Create(canvasGo.transform, interaction);

            controller.SelectionChanged += id =>
            {
                if (id >= 0) editor.OpenEdit(id);
                else if (editor.IsEditing) editor.Close();
            };
        }
    }
}
