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
        private FeedbackBannerView feedbackBannerView;
        private FeedbackMessageStackView feedbackMessageStackView;
        private bool feedbackUnavailable;
        private HotSeatHandoverView hotSeatHandoverView;
        private bool hotSeatHandoverUnavailable;
        private bool playSettingsWarned;
        private bool viewSettingsWarned;
        private ActivityLogView activityLogView;
        private bool activityLogUnavailable;
        private ContentArtPanelView contentArtPanelView;
        private RuleProposalPanelView ruleProposalPanelView;
        private ProfilePanelView profilePanelView;
        private CardEditorPanelView cardEditorPanelView;
        private bool cardEditorEntryWarned;
        private bool profileEntryWarned;
        private HintStripView hintStripView;
        private bool hintStripWarned;
        private bool tipsSettingWarned;
        private bool cardArtWarned;
        private bool shopEntryWarned;

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

        /// <summary>
        /// Binds the Table menu's play rows (doc 18 §15.11): the Seat this screen plays and the Hot-seat switch.
        /// Pass available false for tables without seats. Warns once when the SessionBar prefab lacks the rows.
        /// </summary>
        public void ShowPlaySettings(
            bool available,
            string seatText,
            Action nextSeat,
            bool hotSeatOn,
            Action<bool> setHotSeat)
        {
            EnsureSessionBarView();
            if (!sessionBarView.BindPlaySettings(available, seatText, nextSeat, hotSeatOn, setHotSeat)
                && available
                && !playSettingsWarned)
            {
                playSettingsWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Seat or Hot-seat rows: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Binds the Table menu's view rows (doc 25, F3): Hands on table and Full feedback.</summary>
        /// <summary>Binds the Table menu's Tips switch (doc 23 §10); warns once when the prefab lacks the row.</summary>
        public void ShowTipsSetting(bool tipsOn, Action<bool> setTips)
        {
            EnsureSessionBarView();
            if (!sessionBarView.BindTips(tipsOn, setTips) && !tipsSettingWarned)
            {
                tipsSettingWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Tips switch: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        public void ShowViewSettings(
            bool handsOnTableAvailable,
            bool handsOnTable,
            Action<bool> setHandsOnTable,
            bool fullFeedback,
            Action<bool> setFullFeedback)
        {
            EnsureSessionBarView();
            if (!sessionBarView.BindViewSettings(handsOnTableAvailable, handsOnTable, setHandsOnTable, fullFeedback, setFullFeedback)
                && !viewSettingsWarned)
            {
                viewSettingsWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Hands on table or Feedback rows: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Binds the Table menu's "Card art…" row (doc 24, A3/A4); null hides it.</summary>
        public void ShowCardArtEntry(Action openCardArt)
        {
            EnsureSessionBarView();
            if (!sessionBarView.BindCardArt(openCardArt) && openCardArt != null && !cardArtWarned)
            {
                cardArtWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Card art row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Binds the Table menu's "Card editor…" row (L2-2); null hides it.</summary>
        public void ShowCardEditorEntry(Action openCardEditor)
        {
            EnsureSessionBarView();
            if (!sessionBarView.BindCardEditor(openCardEditor) && openCardEditor != null && !cardEditorEntryWarned)
            {
                cardEditorEntryWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Card editor row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Shows or refreshes the card editor (L2-2). False when its prefab is missing.</summary>
        public bool ShowCardEditor(CardEditorModel model, CardEditorActions actions)
        {
            try
            {
                EnsureService();
                if (cardEditorPanelView == null || !cardEditorPanelView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    ReleaseModalViews();
                    componentToolboxView?.CloseToolbox();
                    cardEditorPanelView = uiService.AcquireCached<CardEditorPanelView>(PrototypeUiPrefabIds.CardEditorPanel);
                    cardEditorPanelView.Initialize(uiService);
                }

                modalLayer.SetActive(true);
                cardEditorPanelView.Bind(model, actions);
                cardEditorPanelView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    "The card editor is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        public bool IsCardEditorOpen => IsVisible(cardEditorPanelView);

        public void CloseCardEditor()
        {
            ReleaseView(cardEditorPanelView);
            SetModalLayerOffUnlessHandover();
        }

        /// <summary>Binds the Table menu's "Profile: name…" row (doc 28, P2); null hides it.</summary>
        public void ShowProfileEntry(string profileName, Action openProfiles)
        {
            EnsureSessionBarView();
            if (!sessionBarView.BindProfile(profileName, openProfiles) && openProfiles != null && !profileEntryWarned)
            {
                profileEntryWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Profile row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Shows or refreshes the profile panel (doc 28, P2). False when its prefab is missing.</summary>
        public bool ShowProfilePanel(ProfilePanelModel model, ProfilePanelActions actions, bool clearName)
        {
            try
            {
                EnsureService();
                if (profilePanelView == null || !profilePanelView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    ReleaseModalViews();
                    componentToolboxView?.CloseToolbox();
                    profilePanelView = uiService.AcquireCached<ProfilePanelView>(PrototypeUiPrefabIds.ProfilePanel);
                    profilePanelView.Initialize(uiService);
                }

                modalLayer.SetActive(true);
                profilePanelView.Bind(model, actions, clearName);
                profilePanelView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    "The profile panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        public bool IsProfilePanelOpen => IsVisible(profilePanelView);

        public void CloseProfilePanel()
        {
            ReleaseView(profilePanelView);
            SetModalLayerOffUnlessHandover();
        }

        /// <summary>Binds the Shop entry on the session bar (doc 18 §15.4); null hides it.</summary>
        public void ShowShopEntry(Action openShop)
        {
            EnsureSessionBarView();
            if (!sessionBarView.BindShop(openShop) && openShop != null && !shopEntryWarned)
            {
                shopEntryWarned = true;
                Debug.LogWarning(
                    "The session bar has no Shop entry: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Shows or refreshes the Card Art panel (doc 24, A3/A4). False when its prefab is missing.</summary>
        public bool ShowContentArtPanel(ContentArtPanelModel model, ContentArtPanelActions actions)
        {
            try
            {
                EnsureService();
                if (contentArtPanelView == null || !contentArtPanelView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    ReleaseModalViews();
                    componentToolboxView?.CloseToolbox();
                    contentArtPanelView = uiService.AcquireCached<ContentArtPanelView>(PrototypeUiPrefabIds.ContentArtPanel);
                    contentArtPanelView.Initialize(uiService);
                }

                modalLayer.SetActive(true);
                contentArtPanelView.Bind(model, actions);
                contentArtPanelView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    "The Card Art panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        public bool IsContentArtPanelOpen => IsVisible(contentArtPanelView);

        /// <summary>Shows or refreshes the house-rule proposal (doc 23, R3). False when its prefab is missing.</summary>
        public bool ShowRuleProposal(RuleProposalModel model, RuleProposalActions actions)
        {
            try
            {
                EnsureService();
                if (ruleProposalPanelView == null || !ruleProposalPanelView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    componentToolboxView?.CloseToolbox();
                    ruleProposalPanelView = uiService.AcquireCached<RuleProposalPanelView>(PrototypeUiPrefabIds.RuleProposalPanel);
                }

                modalLayer.SetActive(true);
                ruleProposalPanelView.Bind(model, actions);
                ruleProposalPanelView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    "The rule proposal panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        public bool IsRuleProposalOpen => IsVisible(ruleProposalPanelView);

        /// <summary>Shows one hint in the hint strip (doc 23 §10). False when its prefab is missing (warned once).</summary>
        public bool ShowHint(string kind, string text, Action gotIt, Action later)
        {
            try
            {
                EnsureService();
                if (hintStripView == null || !hintStripView.IsAcquired)
                {
                    hintStripView = uiService.AcquireCached<HintStripView>(PrototypeUiPrefabIds.HintStrip);
                }

                hintStripView.Bind(kind, text, gotIt, later);
                hintStripView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                if (!hintStripWarned)
                {
                    hintStripWarned = true;
                    Debug.LogWarning(
                        "The hint strip is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                        + exception.Message);
                }

                return false;
            }
        }

        public bool IsHintShown => IsVisible(hintStripView);

        public void HideHint()
        {
            ReleaseView(hintStripView);
        }

        public void CloseRuleProposal()
        {
            ReleaseView(ruleProposalPanelView);
            SetModalLayerOffUnlessHandover();
        }

        public void CloseContentArtPanel()
        {
            ReleaseView(contentArtPanelView);
            if (!IsVisible(quantityPopupView)
                && !IsVisible(cardInspectPopupView)
                && !IsVisible(actionAbilityPurchasePopupView)
                && !IsVisible(focusedCardSelectionView))
            {
                SetModalLayerOffUnlessHandover();
            }
        }

        /// <summary>Shows the "What happened" log tab with this text (doc 25, F4). False when its prefab is missing.</summary>
        public bool ShowActivityLog(string text)
        {
            if (activityLogUnavailable) return false;
            try
            {
                EnsureService();
                if (activityLogView == null || !activityLogView.IsAcquired)
                {
                    activityLogView = uiService.AcquireCached<ActivityLogView>(PrototypeUiPrefabIds.ActivityLog);
                    activityLogView.Show();
                }

                activityLogView.SetText(text);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                activityLogUnavailable = true;
                Debug.LogWarning(
                    "The What happened log is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        /// <summary>Shows the hot-seat handover cover (doc 25, H2). False when its Final UI prefab is missing.</summary>
        public bool ShowHotSeatHandover(HotSeatHandoverModel model, Action confirm)
        {
            if (hotSeatHandoverUnavailable) return false;
            try
            {
                EnsureService();
                if (hotSeatHandoverView == null || !hotSeatHandoverView.IsAcquired)
                {
                    hotSeatHandoverView = uiService.AcquireCached<HotSeatHandoverView>(PrototypeUiPrefabIds.HotSeatHandover);
                }

                // The handover lives on the Modal layer, which is off unless a modal is up.
                modalLayer.SetActive(true);
                hotSeatHandoverView.Bind(model, confirm);
                hotSeatHandoverView.Show();
                root.ClearSelectedUiObject();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                hotSeatHandoverUnavailable = true;
                Debug.LogWarning(
                    "The hot-seat handover screen is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        /// <summary>Fades the handover cover away, then releases it.</summary>
        public void FadeOutHotSeatHandover()
        {
            if (IsVisible(hotSeatHandoverView))
            {
                hotSeatHandoverView.FadeOut(HideHotSeatHandover);
            }
        }

        public void HideHotSeatHandover()
        {
            ReleaseView(hotSeatHandoverView);
            if (!IsVisible(quantityPopupView)
                && !IsVisible(cardInspectPopupView)
                && !IsVisible(actionAbilityPurchasePopupView)
                && !IsVisible(focusedCardSelectionView))
            {
                SetModalLayerOffUnlessHandover();
            }
        }

        // Closing another modal never hides the hot-seat handover cover that may share the Modal layer.
        private void SetModalLayerOffUnlessHandover()
        {
            if (modalLayer != null && !IsVisible(hotSeatHandoverView) && !IsVisible(contentArtPanelView)
                && !IsVisible(ruleProposalPanelView) && !IsVisible(profilePanelView) && !IsVisible(cardEditorPanelView))
            {
                modalLayer.SetActive(false);
            }
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

        /// <summary>Queues an announcement banner (doc 25). False when the Final UI feedback prefabs are missing.</summary>
        public bool ShowFeedbackBanner(FeedbackBannerModel model)
        {
            if (!EnsureFeedbackViews()) return false;
            feedbackBannerView.Enqueue(model);
            return true;
        }

        /// <summary>Adds a message to the message stack (doc 25). False when the prefab is missing.</summary>
        public bool ShowFeedbackMessage(string message, FeedbackSeverity severity)
        {
            if (!EnsureFeedbackViews()) return false;
            feedbackMessageStackView.Push(message, severity);
            return true;
        }

        private bool EnsureFeedbackViews()
        {
            if (feedbackUnavailable) return false;
            if (feedbackBannerView != null && feedbackBannerView.IsAcquired
                && feedbackMessageStackView != null && feedbackMessageStackView.IsAcquired)
            {
                return true;
            }

            try
            {
                EnsureService();
                feedbackBannerView = uiService.AcquireCached<FeedbackBannerView>(PrototypeUiPrefabIds.FeedbackBanner);
                feedbackBannerView.ValidateReferences();
                feedbackMessageStackView = uiService.AcquireCached<FeedbackMessageStackView>(
                    PrototypeUiPrefabIds.FeedbackMessages);
                feedbackMessageStackView.ValidateReferences();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                feedbackUnavailable = true;
                Debug.LogWarning(
                    "Player feedback is off: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

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
                SetModalLayerOffUnlessHandover();
            }
        }

        public void ShowActionAbilityPurchase(
            IReadOnlyList<PrototypeActionAbilityPurchaseOption> options,
            Action<string> purchase,
            Action dismiss,
            string statusMessage = "",
            string title = null,
            string listTitle = null)
        {
            ReleaseView(tabletopPopupView);
            popupLayer.SetActive(false);
            ReleaseModalViews();
            EnsureActionAbilityPurchasePopupView();
            componentToolboxView?.CloseToolbox();
            modalLayer.SetActive(true);
            actionAbilityPurchasePopupView.Bind(options, purchase, dismiss, statusMessage, title, listTitle);
            actionAbilityPurchasePopupView.Show();
        }

        public void CloseActionAbilityPurchase()
        {
            ReleaseView(actionAbilityPurchasePopupView);
            if (!IsVisible(quantityPopupView) && !IsVisible(cardInspectPopupView))
            {
                SetModalLayerOffUnlessHandover();
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
                SetModalLayerOffUnlessHandover();
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
            SetModalLayerOffUnlessHandover();
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
            ReleaseView(feedbackBannerView);
            ReleaseView(feedbackMessageStackView);
            ReleaseView(hotSeatHandoverView);
            ReleaseView(activityLogView);
            ReleaseView(contentArtPanelView);
            ReleaseView(ruleProposalPanelView);
            ReleaseView(profilePanelView);
            ReleaseView(cardEditorPanelView);
            ReleaseView(hintStripView);
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
            // The open menu draws over the rules card that shares the HUD layer.
            sessionBarView?.transform.SetAsLastSibling();
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
            ReleaseView(contentArtPanelView);
            ReleaseView(profilePanelView);
            ReleaseView(cardEditorPanelView);
            ReleaseView(quantityPopupView);
            ReleaseView(cardInspectPopupView);
            ReleaseView(actionAbilityPurchasePopupView);
            ReleaseView(focusedCardSelectionView);
            SetModalLayerOffUnlessHandover();
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
