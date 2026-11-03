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
    public sealed class TableUiRoot : MonoBehaviour
    {
        [Header("Authored UI infrastructure")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule inputModule;
        [SerializeField] private RuntimeUiManager uiManager;

        public IRuntimeUiService UiService => uiManager;

        // QA Q9 (2026-11-03): in a window narrower than the screens were drawn for (4:3, 16:10) the menus were cut
        // off on the right. Below the reference shape the scaler fits the width instead, so everything stays on.
        private float authoredMatch = -1f;
        private int fittedWidth;
        private int fittedHeight;

        private void Update()
        {
            if (canvasScaler == null) return;
            if (Screen.width == fittedWidth && Screen.height == fittedHeight) return;
            fittedWidth = Screen.width;
            fittedHeight = Screen.height;
            if (authoredMatch < 0f) authoredMatch = canvasScaler.matchWidthOrHeight;
            Vector2 reference = canvasScaler.referenceResolution;
            float referenceAspect = reference.y > 0f ? reference.x / reference.y : 16f / 9f;
            float aspect = fittedHeight > 0 ? (float)fittedWidth / fittedHeight : referenceAspect;
            canvasScaler.matchWidthOrHeight = aspect < referenceAspect - 0.01f ? 0f : authoredMatch;
        }

        public void ValidateReferences()
        {
            if (canvas == null
                || canvasScaler == null
                || graphicRaycaster == null
                || eventSystem == null
                || inputModule == null)
            {
                throw new InvalidOperationException(
                    "TableUiRoot requires its authored Canvas, scaler, raycaster, EventSystem, and Input System UI module.");
            }

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay
                || canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                throw new InvalidOperationException(
                    "TableUiRoot requires a Screen Space Overlay Canvas using Scale With Screen Size.");
            }

            if (uiManager == null)
            {
                throw new InvalidOperationException(
                    "TableUiRoot requires its scene-authored RuntimeUiManager component.");
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
