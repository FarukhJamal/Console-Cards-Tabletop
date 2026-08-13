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
        [SerializeField] private Transform tabletopPopupMount;
        [SerializeField] private PrototypeTabletopPopupView tabletopPopupPrefab;

        private PrototypeComponentToolboxView componentToolboxView;
        private PrototypeTabletopPopupView tabletopPopupView;

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
                || componentToolboxPrefab == null
                || tabletopPopupMount == null
                || tabletopPopupPrefab == null)
            {
                throw new InvalidOperationException(
                    "PrototypeRuntimeUiRoot requires its authored layers and view references.");
            }

            sessionEntryView.ValidateReferences();
            activeSessionToolbarView.ValidateReferences();
            statusMessageView.ValidateReferences();
            componentToolboxPrefab.ValidateReferences();
            tabletopPopupPrefab.ValidateReferences();
        }

        public void ShowSessionEntry(
            Action selectEmptyTable,
            IReadOnlyList<PrototypeSessionTemplateOption> templateOptions,
            string errorMessage)
        {
            ValidateReferences();
            activeSessionToolbarView.Unbind();
            componentToolboxView?.Unbind();
            CloseTabletopPopup();
            activeSessionHudLayer.SetActive(false);
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
            CloseTabletopPopup();
            activeSessionHudLayer.SetActive(true);
            activeSessionToolbarView.Bind(sessionTitle, resetSession, returnToSessionEntry);
            componentToolboxView.Bind(componentToolboxBindings, CloseTabletopPopup);
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

        public void ShowContextMenu(
            Vector2 screenPosition,
            string title,
            string body,
            IReadOnlyList<PrototypePopupActionOption> actions,
            Action dismiss,
            Action<Vector2> secondaryDismiss)
        {
            EnsureTabletopPopupView();
            componentToolboxView?.CloseToolbox();
            popupLayer.SetActive(true);
            tabletopPopupView.ShowContextMenu(
                screenPosition,
                title,
                body,
                actions,
                dismiss,
                secondaryDismiss);
        }

        public void ShowDrawCountPopup(
            Vector2 screenPosition,
            int selectedCount,
            int availableCount,
            Action decrement,
            Action increment,
            Action confirm,
            Action cancel,
            Action dismiss,
            Action<Vector2> secondaryDismiss)
        {
            EnsureTabletopPopupView();
            componentToolboxView?.CloseToolbox();
            popupLayer.SetActive(true);
            tabletopPopupView.ShowDrawCount(
                screenPosition,
                selectedCount,
                availableCount,
                decrement,
                increment,
                confirm,
                cancel,
                dismiss,
                secondaryDismiss);
        }

        public void SetDrawCountPopupValue(int selectedCount, int availableCount)
        {
            tabletopPopupView?.SetDrawCount(selectedCount, availableCount);
        }

        public void ShowMergeDestinationPopup(
            Vector2 screenPosition,
            IReadOnlyList<PrototypePopupActionOption> destinations,
            Action back,
            Action dismiss,
            Action<Vector2> secondaryDismiss)
        {
            EnsureTabletopPopupView();
            componentToolboxView?.CloseToolbox();
            popupLayer.SetActive(true);
            tabletopPopupView.ShowMergeDestinations(
                screenPosition,
                destinations,
                back,
                dismiss,
                secondaryDismiss);
        }

        public void CloseTabletopPopup()
        {
            tabletopPopupView?.Close();
            if (popupLayer != null)
            {
                popupLayer.SetActive(false);
            }
        }

        public void ClearActiveSessionTransientUi()
        {
            componentToolboxView?.CloseToolbox();
            componentToolboxView?.ClearPlacementHint();
            CloseTabletopPopup();
        }

        public void ReleaseBindings()
        {
            sessionEntryView?.Unbind();
            activeSessionToolbarView?.Unbind();
            componentToolboxView?.Unbind();
            CloseTabletopPopup();
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

        private void EnsureTabletopPopupView()
        {
            if (tabletopPopupView != null)
            {
                return;
            }

            tabletopPopupView = Instantiate(tabletopPopupPrefab, tabletopPopupMount, false);
            tabletopPopupView.name = tabletopPopupPrefab.name;
            tabletopPopupView.ValidateReferences();
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
