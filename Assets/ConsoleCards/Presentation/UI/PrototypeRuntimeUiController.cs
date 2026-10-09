using System;
using System.Collections.Generic;
using ConsoleCards.Presentation.UI.Toolbox;
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
        [SerializeField] private PrototypeStatusMessageView statusMessageView;

        private IRuntimeUiService uiService;
        private ComponentToolboxView componentToolboxView;
        private SessionBarView sessionBarView;
        private RulesCardView rulesCardView;
        private bool rulesCardShown;
        private PrototypeTabletopPopupView tabletopPopupView;
        private PrototypeQuantityPopupView quantityPopupView;
        private PrototypeCardInspectView cardInspectPopupView;
        private PrototypeActionAbilityPurchasePopupView actionAbilityPurchasePopupView;
        private PrototypeFocusedCardSelectionView focusedCardSelectionView;
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
                || statusMessageView == null)
            {
                throw new InvalidOperationException(
                    "PrototypeRuntimeUiController requires its authored root, layers, and persistent View references.");
            }

            root.ValidateReferences();
            EnsureService();
            gameTemplatesPanelView.Initialize(uiService);
            gameTemplatesPanelView.ValidateReferences();
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

        /// <summary>
        /// Shows the table's HUD: the session bar (UI-1: game chip, Undo, Redo, Table menu), the Toolbox and the
        /// interaction guide. <paramref name="isGameSession"/> picks the game chip with its mode line.
        /// </summary>
        public void ShowActiveSession(
            string sessionTitle,
            string sessionSubtitle,
            bool isGameSession,
            Action undo,
            Action redo,
            Action openGameTemplates,
            Action resetSession,
            Action clearTable,
            string statusMessage,
            ComponentToolboxBindings componentToolboxBindings)
        {
            ValidateReferences();
            EnsureComponentToolboxView();
            EnsureSessionBarView();
            EnsureInteractionGuideView();
            HideGameTemplatesPanel();
            CloseTabletopPopup();
            hudLayer.SetActive(true);
            sessionBarView.Bind(
                sessionTitle,
                sessionSubtitle,
                isGameSession,
                undo,
                redo,
                openGameTemplates,
                resetSession,
                clearTable,
                BeforeTableMenuOpens);
            sessionBarView.Show();
            componentToolboxView.Bind(componentToolboxBindings, BeforeToolboxOpens);
            componentToolboxView.SetPanelOpenListener(HandleToolboxPanelOpenChanged);
            componentToolboxView.Show();
            interactionGuideView.Bind();
            interactionGuideView.Show();
            HideTrapFloorStatus();
            statusMessageView.SetMessage(statusMessage);
            root.ClearSelectedUiObject();
        }

        /// <summary>
        /// Binds the Table menu's view settings and applies them (doc 23, R1): the rules card for this table
        /// (null when it has no rules), whether it is shown, and whether Hints are on. Call after ShowActiveSession.
        /// </summary>
        public void ShowTableSettings(
            RulesCardModel rules,
            bool rulesCardOn,
            Action<bool> setRulesCard,
            bool hintsOn,
            Action<bool> setHints)
        {
            EnsureSessionBarView();
            sessionBarView.BindTableSettings(rules != null, rulesCardOn, setRulesCard, hintsOn, setHints, OpenHelp);
            ShowRulesCard(rules, rulesCardOn);
            SetHintsVisible(hintsOn);
        }

        /// <summary>Shows the rules card with these rules, or hides it (null rules or switched off).</summary>
        public void ShowRulesCard(RulesCardModel rules, bool visible)
        {
            if (rules == null || !visible)
            {
                rulesCardShown = false;
                ReleaseView(rulesCardView);
                return;
            }

            EnsureService();
            rulesCardView = uiService.AcquireCached<RulesCardView>(PrototypeUiPrefabIds.RulesCard);
            rulesCardView.Initialize(uiService);
            rulesCardView.ValidateReferences();
            rulesCardView.Bind(rules);
            rulesCardView.Show();
            rulesCardShown = true;
            if (componentToolboxView != null && componentToolboxView.IsPanelOpen)
            {
                rulesCardView.Hide();
            }
        }

        /// <summary>Hints switch: the controls strip and the Toolbox placing bar (guidance text only).</summary>
        public void SetHintsVisible(bool on)
        {
            interactionGuideView?.SetStripVisible(on);
            componentToolboxView?.SetPlacingHintVisible(on);
        }

        /// <summary>Opens the Toolbox panel (after a table rebuild started from inside it, such as the Hand switch).</summary>
        public void OpenComponentToolbox()
        {
            componentToolboxView?.OpenToolbox();
        }

        public void SetUndoState(bool enabled, string label) =>
            sessionBarView?.SetUndoState(enabled, label);

        public void SetRedoState(bool enabled, string label) =>
            sessionBarView?.SetRedoState(enabled, label);

        public void SetGameTemplatesError(string errorMessage) =>
            gameTemplatesPanelView.SetError(errorMessage);

        public void SetStatusMessage(string statusMessage)
        {
            if (hudLayer.activeSelf)
            {
                statusMessageView.SetMessage(statusMessage);
            }
        }

        public void ShowPlacementHint(string placementSubject, float rotationDegrees, Sprite icon = null)
        {
            EnsureComponentToolboxView();
            componentToolboxView.ShowPlacementHint(placementSubject, rotationDegrees, icon);
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

        public void ShowFocusedCardSelection(PrototypeFocusedCardSelectionModel model)
        {
            ReleaseView(tabletopPopupView);
            popupLayer.SetActive(false);
            ReleaseModalViews();
            EnsureFocusedCardSelectionView();
            componentToolboxView?.CloseToolbox();
            modalLayer.SetActive(true);
            focusedCardSelectionView.Bind(model);
            focusedCardSelectionView.Show();
        }

        public void ShowFocusedCardReveal(PrototypeFocusedCardRevealModel model)
        {
            ReleaseView(tabletopPopupView);
            popupLayer.SetActive(false);
            ReleaseModalViews();
            EnsureFocusedCardSelectionView();
            componentToolboxView?.CloseToolbox();
            modalLayer.SetActive(true);
            focusedCardSelectionView.Show();
            focusedCardSelectionView.ShowReveal(model);
        }

        public void CloseFocusedCardSelection()
        {
            ReleaseView(focusedCardSelectionView);
            if (!IsVisible(quantityPopupView)
                && !IsVisible(cardInspectPopupView)
                && !IsVisible(actionAbilityPurchasePopupView))
            {
                modalLayer.SetActive(false);
            }
        }

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
            sessionBarView?.CloseMenu();
            componentToolboxView?.CloseToolbox();
            componentToolboxView?.ClearPlacementHint();
            CloseTabletopPopup();
        }

        public void ReleaseBindings()
        {
            HideGameTemplatesPanel();
            rulesCardShown = false;
            ReleaseView(rulesCardView);
            ReleaseView(sessionBarView);
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
            componentToolboxView = uiService.AcquireCached<ComponentToolboxView>(
                PrototypeUiPrefabIds.ComponentToolbox);
            componentToolboxView.ValidateReferences();
        }

        private void EnsureSessionBarView()
        {
            EnsureService();
            sessionBarView = uiService.AcquireCached<SessionBarView>(PrototypeUiPrefabIds.SessionBar);
            sessionBarView.ValidateReferences();
        }

        // The Toolbox panel and the Table menu close each other; the bar sits beside the open panel.
        private void BeforeToolboxOpens()
        {
            CloseTabletopPopup();
            sessionBarView?.CloseMenu();
        }

        private void BeforeTableMenuOpens()
        {
            componentToolboxView?.CloseToolbox();
            CloseTabletopPopup();
        }

        // The open Toolbox panel covers the left column, so the rules card steps aside while it is open.
        private void HandleToolboxPanelOpenChanged(bool open)
        {
            sessionBarView?.SetShiftedForPanel(open);
            if (rulesCardShown && rulesCardView != null && rulesCardView.IsAcquired)
            {
                if (open)
                {
                    rulesCardView.Hide();
                }
                else
                {
                    rulesCardView.Show();
                }
            }
        }

        private void OpenHelp() => interactionGuideView?.OpenGuide();

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

        private void EnsureFocusedCardSelectionView()
        {
            EnsureService();
            focusedCardSelectionView =
                uiService.AcquireCached<PrototypeFocusedCardSelectionView>(
                    PrototypeUiPrefabIds.FocusedCardSelection);
            focusedCardSelectionView.Initialize(uiService);
            focusedCardSelectionView.ValidateReferences();
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
            ReleaseView(focusedCardSelectionView);
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
