using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Scene-authored generic runtime UI shell. It validates infrastructure and exposes the UI service.
    /// </summary>
    public sealed class PrototypeRuntimeUiRoot : MonoBehaviour
    {
        [Header("Authored UI infrastructure")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule inputModule;
        [SerializeField] private RuntimeUiManager uiManager;

        public IRuntimeUiService UiService => uiManager;

        public void ValidateReferences()
        {
            if (canvas == null
                || canvasScaler == null
                || graphicRaycaster == null
                || eventSystem == null
                || inputModule == null)
            {
                throw new InvalidOperationException(
                    "PrototypeRuntimeUiRoot requires its authored Canvas, scaler, raycaster, EventSystem, and Input System UI module.");
            }

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay
                || canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                throw new InvalidOperationException(
                    "PrototypeRuntimeUiRoot requires a Screen Space Overlay Canvas using Scale With Screen Size.");
            }

            if (uiManager == null)
            {
                throw new InvalidOperationException(
                    "PrototypeRuntimeUiRoot requires its scene-authored RuntimeUiManager component.");
            }

            uiManager.ValidateReferences();
        }

        public void ClearSelectedUiObject()
        {
            if (eventSystem.currentSelectedGameObject != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }
    }
}
