using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Prototype feature facade. It binds Platform and current Game-specific Views without adding
    /// feature knowledge to the generic root or UI service.
    /// </summary>
    public sealed class PrototypeRuntimeUiController : MonoBehaviour
    {
        [SerializeField] private PrototypeRuntimeUiRoot root;
        [SerializeField] private GameObject screenLayer;
        [SerializeField] private GameObject hudLayer;
        [SerializeField] private GameObject popupLayer;
        [SerializeField] private GameObject modalLayer;
        [SerializeField] private PrototypeGameTemplatesPanelView gameTemplatesPanelView;
        [SerializeField] private PrototypeActiveSessionToolbarView activeSessionToolbarView;
        [SerializeField] private PrototypeStatusMessageView statusMessageView;

        private IRuntimeUiService uiService;
        private PrototypeComponentToolboxView componentToolboxView;
        private PrototypeTabletopPopupView tabletopPopupView;
        private PrototypeQuantityPopupView quantityPopupView;
        private PrototypeCardInspectView cardInspectPopupView;
        private PrototypeActionAbilityPurchasePopupView actionAbilityPurchasePopupView;
        private PrototypeTrapFloorHudView trapFloorHudView;
        private PrototypeInteractionGuide interactionGuideView;

        public IRuntimeUiService UiService
        {
            get
            {
                EnsureService();
                return uiService;
            }
        }

        public void ValidateReferences()
        {
            if (root == null
                || screenLayer == null
                || hudLayer == null
                || popupLayer == null
                || modalLayer == null
                || gameTemplatesPanelView == null
                || activeSessionToolbarView == null
                || statusMessageView == null)
            {
                throw new InvalidOperationException(
                    "PrototypeRuntimeUiController requires its authored root, layers, and persistent View references.");
            }

            root.ValidateReferences();
            EnsureService();
            gameTemplatesPanelView.Initialize(uiService);
            gameTemplatesPanelView.ValidateReferences();
            activeSessionToolbarView.ValidateReferences();
            statusMessageView.ValidateReferences();
        }

        public void ShowGameTemplatesPanel(
            Action clearTable,
            IReadOnlyList<PrototypeGameTemplateOption> templateOptions,
            string errorMessage)
        {
            ValidateReferences();
            CloseTabletopPopup();
            if (!gameTemplatesPanelView.IsAcquired)
            {
                gameTemplatesPanelView.Acquire();
            }

            gameTemplatesPanelView.Bind(clearTable, templateOptions, errorMessage);
            gameTemplatesPanelView.Show();
            screenLayer.SetActive(true);
            root.ClearSelectedUiObject();
        }

        public void HideGameTemplatesPanel()
        {
            gameTemplatesPanelView?.Release();
            screenLayer?.SetActive(false);
            root?.ClearSelectedUiObject();
        }

        public void ShowActiveSession(
            string sessionTitle,
            Action undo,
            Action redo,
            Action resetSession,
            Action openGameTemplates,
            string statusMessage,
            PrototypeComponentToolboxBindings componentToolboxBindings)
        {
            ValidateReferences();
            EnsureComponentToolboxView();
            EnsureInteractionGuideView();
            HideGameTemplatesPanel();
            CloseTabletopPopup();
            hudLayer.SetActive(true);
            activeSessionToolbarView.Bind(sessionTitle, undo, redo, resetSession, openGameTemplates);
            componentToolboxView.Bind(componentToolboxBindings, CloseTabletopPopup);
            componentToolboxView.Show();
            interactionGuideView.Bind();
            interactionGuideView.Show();
            HideTrapFloorStatus();
            statusMessageView.SetMessage(statusMessage);
            root.ClearSelectedUiObject();
        }

        public void SetUndoState(bool enabled, string label) =>
            activeSessionToolbarView?.SetUndoState(enabled, label);

        public void SetRedoState(bool enabled, string label) =>
            activeSessionToolbarView?.SetRedoState(enabled, label);

        public void SetGameTemplatesError(string errorMessage) =>
            gameTemplatesPanelView.SetError(errorMessage);

        public void SetStatusMessage(string statusMessage)
        {
            if (hudLayer.activeSelf)
            {
                statusMessageView.SetMessage(statusMessage);
            }
        }

        public void ShowPlacementHint(string placementSubject, float rotationDegrees)
        {
            EnsureComponentToolboxView();
            componentToolboxView.ShowPlacementHint(placementSubject, rotationDegrees);
        }

        public void ClearPlacementHint() => componentToolboxView?.ClearPlacementHint();

        public void ShowContextMenu(
            Vector2 screenPosition,
            string title,
            string body,
            IReadOnlyList<PrototypePopupActionOption> actions,
            Action dismiss,
            Action<Vector2> secondaryDismiss)
        {
            EnsureTabletopPopupView();
            ReleaseModalViews();
            componentToolboxView?.CloseToolbox();
            popupLayer.SetActive(true);
            tabletopPopupView.ShowContextMenu(screenPosition, title, body, actions, dismiss, secondaryDismiss);
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
            ReleaseModalViews();
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

        public void SetDrawCountPopupValue(int selectedCount, int availableCount) =>
            tabletopPopupView?.SetDrawCount(selectedCount, availableCount);

        public void ShowQuantityPopup(
            string title,
            string description,
            string confirmText,
            int quantity,
            int minimum,
            int maximum,
            Action decrement,
            Action increment,
            Action confirm,
            Action dismiss)
        {
            ReleaseView(tabletopPopupView);
            popupLayer.SetActive(false);
            ReleaseView(cardInspectPopupView);
            EnsureQuantityPopupView();
            componentToolboxView?.CloseToolbox();
            modalLayer.SetActive(true);
            quantityPopupView.Bind(
                title,
                description,
                confirmText,
                quantity,
                minimum,
                maximum,
                decrement,
                increment,
                confirm,
                dismiss);
            quantityPopupView.Show();
        }

        public void SetQuantityPopupValue(int quantity, int minimum, int maximum) =>
            quantityPopupView?.SetQuantity(quantity, minimum, maximum);

        public void ShowCardInspect(PrototypeCardInspectModel model, Action dismiss)
        {
            ReleaseView(tabletopPopupView);
            popupLayer.SetActive(false);
            ReleaseView(quantityPopupView);
            EnsureCardInspectPopupView();
            componentToolboxView?.CloseToolbox();
            modalLayer.SetActive(true);
            cardInspectPopupView.Bind(model, dismiss);
            cardInspectPopupView.Show();
        }

        public void RefreshCardInspect(PrototypeCardInspectModel model) =>
            cardInspectPopupView?.Refresh(model);

        public void CloseCardInspect()
        {
            ReleaseView(cardInspectPopupView);
            if (!IsVisible(quantityPopupView))
            {
                modalLayer.SetActive(false);
            }
        }

        public void ShowActionAbilityPurchase(
            IReadOnlyList<PrototypeActionAbilityPurchaseOption> options,
            Action<string> purchase,
            Action dismiss,
            string statusMessage = "")
        {
            ReleaseView(tabletopPopupView);
            popupLayer.SetActive(false);
            ReleaseModalViews();
            EnsureActionAbilityPurchasePopupView();
            componentToolboxView?.CloseToolbox();
            modalLayer.SetActive(true);
            actionAbilityPurchasePopupView.Bind(options, purchase, dismiss, statusMessage);
            actionAbilityPurchasePopupView.Show();
        }

        public void CloseActionAbilityPurchase()
        {
            ReleaseView(actionAbilityPurchasePopupView);
            if (!IsVisible(quantityPopupView) && !IsVisible(cardInspectPopupView))
            {
                modalLayer.SetActive(false);
            }
        }

        public void SetActionAbilityPurchaseStatus(string message) =>
            actionAbilityPurchasePopupView?.SetStatus(message);

        public void ShowTrapFloorStatus(
            PrototypeTrapFloorStatusModel status,
            PrototypeFloorfallStatusModel floorfall,
            IReadOnlyList<PrototypePopupActionOption> actions)
        {
            if (!hudLayer.activeSelf)
            {
                return;
            }

            EnsureTrapFloorHudView();
            trapFloorHudView.Show(status, floorfall, actions);
        }

        public void ShowTrapFloorObjective(
            string keyProgress,
            string collapseStatus,
            bool isWon,
            IReadOnlyList<PrototypePopupActionOption> actions)
        {
            if (!hudLayer.activeSelf)
            {
                return;
            }

            EnsureTrapFloorHudView();
            trapFloorHudView.ShowObjective(keyProgress, collapseStatus, isWon, actions);
        }

        public void HideTrapFloorStatus() => ReleaseView(trapFloorHudView);

        public void ShowMergeDestinationPopup(
            Vector2 screenPosition,
            IReadOnlyList<PrototypePopupActionOption> destinations,
            Action back,
            Action dismiss,
            Action<Vector2> secondaryDismiss)
        {
            EnsureTabletopPopupView();
            ReleaseModalViews();
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
            ReleaseView(tabletopPopupView);
            ReleaseModalViews();
            popupLayer?.SetActive(false);
            modalLayer?.SetActive(false);
        }

        public void ClearActiveSessionTransientUi()
        {
            componentToolboxView?.CloseToolbox();
            componentToolboxView?.ClearPlacementHint();
            CloseTabletopPopup();
        }

        public void ReleaseBindings()
        {
            HideGameTemplatesPanel();
            activeSessionToolbarView?.Unbind();
            ReleaseView(componentToolboxView);
            HideTrapFloorStatus();
            ReleaseView(interactionGuideView);
            CloseTabletopPopup();
        }

        private void EnsureService()
        {
            if (uiService == null)
            {
                uiService = root != null
                    ? root.UiService
                    : throw new InvalidOperationException("PrototypeRuntimeUiController requires an authored UI root.");
            }
        }

        private void EnsureComponentToolboxView()
        {
            EnsureService();
            componentToolboxView = uiService.AcquireCached<PrototypeComponentToolboxView>(
                PrototypeUiPrefabIds.ComponentToolbox);
            componentToolboxView.ValidateReferences();
        }

        private void EnsureTabletopPopupView()
        {
            EnsureService();
            tabletopPopupView = uiService.AcquireCached<PrototypeTabletopPopupView>(
                PrototypeUiPrefabIds.TabletopPopup);
            tabletopPopupView.Initialize(uiService);
            tabletopPopupView.ValidateReferences();
        }

        private void EnsureQuantityPopupView()
        {
            EnsureService();
            quantityPopupView = uiService.AcquireCached<PrototypeQuantityPopupView>(
                PrototypeUiPrefabIds.QuantityPopup);
            quantityPopupView.ValidateReferences();
        }

        private void EnsureCardInspectPopupView()
        {
            EnsureService();
            cardInspectPopupView = uiService.AcquireCached<PrototypeCardInspectView>(
                PrototypeUiPrefabIds.CardInspect);
            cardInspectPopupView.ValidateReferences();
        }

        private void EnsureActionAbilityPurchasePopupView()
        {
            EnsureService();
            actionAbilityPurchasePopupView =
                uiService.AcquireCached<PrototypeActionAbilityPurchasePopupView>(
                    PrototypeUiPrefabIds.ActionAbilityPurchase);
            actionAbilityPurchasePopupView.Initialize(uiService);
            actionAbilityPurchasePopupView.ValidateReferences();
        }

        private void EnsureTrapFloorHudView()
        {
            EnsureService();
            trapFloorHudView = uiService.AcquireCached<PrototypeTrapFloorHudView>(
                PrototypeUiPrefabIds.TrapFloorHud);
            trapFloorHudView.Initialize(uiService);
            trapFloorHudView.ValidateReferences();
        }

        private void EnsureInteractionGuideView()
        {
            EnsureService();
            interactionGuideView = uiService.AcquireCached<PrototypeInteractionGuide>(
                PrototypeUiPrefabIds.InteractionGuide);
            interactionGuideView.ValidateReferences();
        }

        private void ReleaseModalViews()
        {
            ReleaseView(quantityPopupView);
            ReleaseView(cardInspectPopupView);
            ReleaseView(actionAbilityPurchasePopupView);
            modalLayer?.SetActive(false);
        }

        private void ReleaseView(ReusableUiView view)
        {
            if (view != null && view.IsAcquired)
            {
                uiService.Release(view);
            }
        }

        private static bool IsVisible(ReusableUiView view) =>
            view != null && view.IsAcquired && view.gameObject.activeSelf;
    }
}
