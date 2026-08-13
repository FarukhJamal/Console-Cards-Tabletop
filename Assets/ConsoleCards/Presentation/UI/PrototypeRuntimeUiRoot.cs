using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PrototypeRuntimeUiRoot : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule inputModule;
        [SerializeField] private GameObject sessionEntryLayer;
        [SerializeField] private GameObject activeSessionHudLayer;
        [SerializeField] private GameObject popupLayer;
        [SerializeField] private PrototypeSessionEntryView sessionEntryView;
        [SerializeField] private PrototypeActiveSessionToolbarView activeSessionToolbarView;
        [SerializeField] private PrototypeStatusMessageView statusMessageView;
        [SerializeField] private Transform componentToolboxMount;
        [SerializeField] private PrototypeComponentToolboxView componentToolboxPrefab;

        private PrototypeComponentToolboxView componentToolboxView;

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

            if (sessionEntryLayer == null
                || activeSessionHudLayer == null
                || popupLayer == null
                || sessionEntryView == null
                || activeSessionToolbarView == null
                || statusMessageView == null
                || componentToolboxMount == null
                || componentToolboxPrefab == null)
            {
                throw new InvalidOperationException(
                    "PrototypeRuntimeUiRoot requires its authored layers and view references.");
            }

            sessionEntryView.ValidateReferences();
            activeSessionToolbarView.ValidateReferences();
            statusMessageView.ValidateReferences();
            componentToolboxPrefab.ValidateReferences();
        }

        public void ShowSessionEntry(
            Action selectEmptyTable,
            IReadOnlyList<PrototypeSessionTemplateOption> templateOptions,
            string errorMessage)
        {
            ValidateReferences();
            activeSessionToolbarView.Unbind();
            componentToolboxView?.Unbind();
            activeSessionHudLayer.SetActive(false);
            popupLayer.SetActive(false);
            sessionEntryLayer.SetActive(true);
            sessionEntryView.Bind(selectEmptyTable, templateOptions, errorMessage);
            ClearSelectedUiObject();
        }

        public void ShowActiveSession(
            string sessionTitle,
            Action resetSession,
            Action returnToSessionEntry,
            string statusMessage,
            PrototypeComponentToolboxBindings componentToolboxBindings)
        {
            ValidateReferences();
            EnsureComponentToolboxView();
            sessionEntryView.Unbind();
            sessionEntryLayer.SetActive(false);
            popupLayer.SetActive(false);
            activeSessionHudLayer.SetActive(true);
            activeSessionToolbarView.Bind(sessionTitle, resetSession, returnToSessionEntry);
            componentToolboxView.Bind(componentToolboxBindings);
            statusMessageView.SetMessage(statusMessage);
            ClearSelectedUiObject();
        }

        public void SetSessionEntryError(string errorMessage)
        {
            sessionEntryView.SetError(errorMessage);
        }

        public void SetStatusMessage(string statusMessage)
        {
            if (!activeSessionHudLayer.activeSelf)
            {
                return;
            }

            statusMessageView.SetMessage(statusMessage);
        }

        public void ShowPlacementHint(string placementSubject)
        {
            EnsureComponentToolboxView();
            componentToolboxView.ShowPlacementHint(placementSubject);
        }

        public void ClearPlacementHint()
        {
            componentToolboxView?.ClearPlacementHint();
        }

        public void ClearActiveSessionTransientUi()
        {
            componentToolboxView?.CloseToolbox();
            componentToolboxView?.ClearPlacementHint();
        }

        public void ReleaseBindings()
        {
            sessionEntryView?.Unbind();
            activeSessionToolbarView?.Unbind();
            componentToolboxView?.Unbind();
        }

        private void EnsureComponentToolboxView()
        {
            if (componentToolboxView != null)
            {
                return;
            }

            componentToolboxView = Instantiate(componentToolboxPrefab, componentToolboxMount, false);
            componentToolboxView.name = componentToolboxPrefab.name;
            componentToolboxView.ValidateReferences();
        }

        private void ClearSelectedUiObject()
        {
            if (eventSystem.currentSelectedGameObject != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }

        private void OnDestroy()
        {
            ReleaseBindings();
        }
    }
}
