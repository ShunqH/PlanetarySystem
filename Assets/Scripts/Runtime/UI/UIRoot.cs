using PlanetSystem.Core;
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

        public static void Build(SimulationController controller, SceneViewManager scene)
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                // InputSystemUIInputModule falls back to the package's default actions when none are assigned.
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var canvasGo = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            var editor = BodyEditorPanel.Create(canvasGo.transform, controller, scene);
            BodyListPanel.Create(canvasGo.transform, controller, editor);
            TopBar.Create(canvasGo.transform, controller, editor);
            StatusBar.Create(canvasGo.transform, controller);

            controller.SelectionChanged += id =>
            {
                if (id >= 0) editor.OpenEdit(id);
                else if (editor.IsEditing) editor.Close();
            };
        }
    }
}
