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
    public sealed class TableUiController : MonoBehaviour
    {
        [SerializeField] private TableUiRoot root;
        [SerializeField] private GameObject screenLayer;
        [SerializeField] private GameObject hudLayer;
        [SerializeField] private GameObject popupLayer;
        [SerializeField] private GameObject modalLayer;
        [SerializeField] private GameTemplatesPanelView gameTemplatesPanelView;
        [SerializeField] private StatusMessageView statusMessageView;

        private IRuntimeUiService uiService;
        private ComponentToolboxView componentToolboxView;
        private SessionBarView sessionBarView;
        private RulesCardView rulesCardView;
        private bool rulesCardShown;
        private TabletopPopupView tabletopPopupView;
        private QuantityPopupView quantityPopupView;
        private CardInspectView cardInspectPopupView;
        private ActionAbilityPurchasePopupView actionAbilityPurchasePopupView;
        private FocusedCardSelectionView focusedCardSelectionView;
        private TrapFloorHudView trapFloorHudView;
        private InteractionGuideView interactionGuideView;
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
        private PersonaPanelView personaPanelView;
        private TablePanelView tablePanelView;
        private CardEditorPanelView cardEditorPanelView;
        private bool cardEditorEntryWarned;
        private DeckEditorPanelView deckEditorPanelView;
        private GameSelectPanelView gameSelectPanelView;
        private FrontEndView frontEndView; // UI-2: the front end shows the New table… choices as the user journey
        private bool frontEndMissing;
        private GameSavePanelView gameSavePanelView;
        private bool saveGameEntryWarned;
        private bool deckEditorEntryWarned;
        private bool profileEntryWarned;
        private bool tableShapeEntryWarned;
        private bool tableEdgeEntryWarned;
        private bool tableToolsEntryWarned;
        private bool arrangeTableEntryWarned;
        private HintStripView hintStripView;
        private bool hintStripWarned;
        private ToolBarView toolBarView;
        private bool toolBarWarned;
        private TableOverlayView tableOverlayView;
        private bool tableOverlayWarned;
        private bool hintsVisible = true;
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
                    "TableUiController requires its authored root, layers, and persistent View references.");
            }

            root.ValidateReferences();
            EnsureService();
            gameTemplatesPanelView.Initialize(uiService);
            gameTemplatesPanelView.ValidateReferences();
            statusMessageView.ValidateReferences();
        }

        public void ShowGameTemplatesPanel(
            Action clearTable,
            IReadOnlyList<GameTemplateOption> templateOptions,
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
            // Unity-aware null checks: on leaving Play mode these may already be destroyed (?. does not see that).
            if (gameTemplatesPanelView != null) gameTemplatesPanelView.Release();
            if (screenLayer != null) screenLayer.SetActive(false);
            if (root != null) root.ClearSelectedUiObject();
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
            interactionGuideView.SetStripVisible(hintsVisible && !IsVisible(toolBarView)); // the Controls bar has the keys
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
            openCardArt = null; // owner, 2026-10-26: the editors are on the front end's CREATE, not the Table menu
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
            openCardEditor = null; // owner, 2026-10-26: CREATE on the front end only
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
                    cardEditorPanelView = uiService.AcquireCached<CardEditorPanelView>(UiPrefabIds.CardEditorPanel);
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

        /// <summary>Shows or refreshes the game selection menu (L2-4). False when its prefab is missing.</summary>
        public bool ShowGameSelect(GameSelectModel model, GameSelectActions actions)
        {
            if (!frontEndMissing && ShowFrontEnd(model, actions)) return true;
            try
            {
                EnsureService();
                if (gameSelectPanelView == null || !gameSelectPanelView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    ReleaseModalViews();
                    componentToolboxView?.CloseToolbox();
                    HideGameTemplatesPanel();
                    gameSelectPanelView = uiService.AcquireCached<GameSelectPanelView>(UiPrefabIds.GameSelectPanel);
                    gameSelectPanelView.Initialize(uiService);
                }

                modalLayer.SetActive(true);
                gameSelectPanelView.Bind(model, actions);
                gameSelectPanelView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    "The game selection menu is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        public bool IsGameSelectOpen => IsVisible(gameSelectPanelView) || IsVisible(frontEndView);

        // UI-2 (owner, 2026-10-24): the front end (main menu, profile, library, details, setup, players, review).
        // Owner, 2026-10-26: CREATE's editors (card art, card editor, deck editor) open over the front end's
        // background, not over the table: the front end stays, showing only its background, under the editor.
        private bool frontEndBackdrop;

        /// <summary>Keeps the front end open as a background only (true when it is open).</summary>
        public bool ShowFrontEndBackdrop()
        {
            if (!IsVisible(frontEndView)) return false;
            frontEndBackdrop = true;
            frontEndView.ShowBackdropOnly(true);
            frontEndView.transform.SetAsFirstSibling();
            return true;
        }

        /// <summary>True when the front end shows only its background and nothing has opened over it.</summary>
        public bool IsFrontEndBackdropAlone => frontEndBackdrop && IsVisible(frontEndView)
            && !IsVisible(contentArtPanelView) && !IsVisible(cardEditorPanelView) && !IsVisible(deckEditorPanelView)
            && !IsVisible(profilePanelView) && !IsVisible(personaPanelView) && !IsVisible(gameSavePanelView);

        private bool ShowFrontEnd(GameSelectModel model, GameSelectActions actions)
        {
            frontEndBackdrop = false;
            try
            {
                EnsureService();
                if (frontEndView == null || !frontEndView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    ReleaseModalViews();
                    componentToolboxView?.CloseToolbox();
                    HideGameTemplatesPanel();
                    frontEndView = uiService.AcquireCached<FrontEndView>(UiPrefabIds.FrontEnd);
                    frontEndView.ValidateReferences();
                }

                modalLayer.SetActive(true);
                frontEndView.Bind(model, actions, OpenGuideFromFrontEnd);
                frontEndView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                frontEndMissing = true;
                Debug.LogWarning("The front end is missing: run Console Cards > UI > Apply Kit Look (UI-1). " + exception.Message);
                return false;
            }
        }

        private void OpenGuideFromFrontEnd()
        {
            ReleaseView(frontEndView);
            SetModalLayerOffUnlessHandover();
            OpenHelp();
        }

        /// <summary>Binds the Table menu's "Save table as a game…" row (doc 29); null hides it.</summary>
        public void ShowSaveGameEntry(Action openSaveGame)
        {
            EnsureSessionBarView();
            if (!sessionBarView.BindSaveGame(openSaveGame) && openSaveGame != null && !saveGameEntryWarned)
            {
                saveGameEntryWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Save table as a game row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Shows or refreshes the Save table as a game panel (doc 29). False when its prefab is missing.</summary>
        public bool ShowGameSave(GameSaveModel model, GameSaveActions actions)
        {
            try
            {
                EnsureService();
                if (gameSavePanelView == null || !gameSavePanelView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    ReleaseModalViews();
                    componentToolboxView?.CloseToolbox();
                    gameSavePanelView = uiService.AcquireCached<GameSavePanelView>(UiPrefabIds.GameSavePanel);
                }

                modalLayer.SetActive(true);
                gameSavePanelView.Bind(model, actions);
                gameSavePanelView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    "The Save table as a game panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        public void CloseGameSave()
        {
            ReleaseView(gameSavePanelView);
            SetModalLayerOffUnlessHandover();
        }

        public void CloseGameSelect()
        {
            frontEndBackdrop = false;
            ReleaseView(frontEndView);
            ReleaseView(gameSelectPanelView);
            SetModalLayerOffUnlessHandover();
        }

        /// <summary>Binds the Table menu's "Deck editor…" row (L2-3); null hides it.</summary>
        public void ShowDeckEditorEntry(Action openDeckEditor)
        {
            EnsureSessionBarView();
            openDeckEditor = null; // owner, 2026-10-26: CREATE on the front end only
            if (!sessionBarView.BindDeckEditor(openDeckEditor) && openDeckEditor != null && !deckEditorEntryWarned)
            {
                deckEditorEntryWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Deck editor row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Shows or refreshes the deck editor (L2-3). False when its prefab is missing.</summary>
        public bool ShowDeckEditor(DeckEditorModel model, DeckEditorActions actions)
        {
            try
            {
                EnsureService();
                if (deckEditorPanelView == null || !deckEditorPanelView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    ReleaseModalViews();
                    componentToolboxView?.CloseToolbox();
                    deckEditorPanelView = uiService.AcquireCached<DeckEditorPanelView>(UiPrefabIds.DeckEditorPanel);
                    deckEditorPanelView.Initialize(uiService);
                }

                modalLayer.SetActive(true);
                deckEditorPanelView.Bind(model, actions);
                deckEditorPanelView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    "The deck editor is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        public bool IsDeckEditorOpen => IsVisible(deckEditorPanelView);

        public void CloseDeckEditor()
        {
            ReleaseView(deckEditorPanelView);
            SetModalLayerOffUnlessHandover();
        }

        /// <summary>Binds the Table menu's "Profile: name…" row (doc 28, P2); null hides it.</summary>
        public void ShowProfileEntry(string profileName, Action openProfiles)
        {
            EnsureSessionBarView();
            openProfiles = null; // owner, 2026-10-26: the profile is chosen on the front end, not during play
            if (!sessionBarView.BindProfile(profileName, openProfiles) && openProfiles != null && !profileEntryWarned)
            {
                profileEntryWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Profile row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>
        /// Binds the Table menu's "Table: shape" row (T1); each press cycles the shape and shows the new name. Null
        /// hides it.
        /// </summary>
        public void ShowTableShapeEntry(string label, Func<string> cycleShape)
        {
            EnsureSessionBarView();
            cycleShape = null; // owner, 2026-10-26: shape and edge are chosen in one menu (Table tools)
            if (!sessionBarView.BindTableShape(label, cycleShape) && cycleShape != null && !tableShapeEntryWarned)
            {
                tableShapeEntryWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Table shape row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Binds the Table menu's "Edge: kind" row (T2); each press cycles the edge. Null hides it.</summary>
        public void ShowTableEdgeEntry(string label, Func<string> cycleEdge)
        {
            EnsureSessionBarView();
            cycleEdge = null; // owner, 2026-10-26: shape and edge are chosen in one menu (Table tools)
            if (!sessionBarView.BindTableEdge(label, cycleEdge) && cycleEdge != null && !tableEdgeEntryWarned)
            {
                tableEdgeEntryWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Edge row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
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
                    profilePanelView = uiService.AcquireCached<ProfilePanelView>(UiPrefabIds.ProfilePanel);
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

        /// <summary>PERSONA-2: shows or refreshes the Persona panel (it replaces the profile panel). False when its prefab is missing.</summary>
        public bool ShowPersonaPanel(PersonaPanelModel model, PersonaPanelActions actions)
        {
            try
            {
                EnsureService();
                if (personaPanelView == null || !personaPanelView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    ReleaseModalViews();
                    componentToolboxView?.CloseToolbox();
                    personaPanelView = uiService.AcquireCached<PersonaPanelView>(UiPrefabIds.PersonaPanel);
                }

                modalLayer.SetActive(true);
                personaPanelView.Bind(model, actions);
                personaPanelView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    "The Persona panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        public bool IsPersonaPanelOpen => IsVisible(personaPanelView);

        public void ClosePersonaPanel()
        {
            ReleaseView(personaPanelView);
            SetModalLayerOffUnlessHandover();
        }

        /// <summary>Binds the Table menu's "Table tools…" row (T3), which opens Table mode; null hides it.</summary>
        public void ShowTableToolsEntry(Action openTableTools)
        {
            openTableTools = null; // owner, 2026-10-26: the table is made from CREATE > TABLE on the front end
            EnsureSessionBarView();
            if (!sessionBarView.BindTableTools(openTableTools) && openTableTools != null && !tableToolsEntryWarned)
            {
                tableToolsEntryWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Table tools row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>Binds the Table menu's "Arrange table…" row (LAYOUT-1); null hides it.</summary>
        public void ShowArrangeTableEntry(Action openArrangeTable)
        {
            EnsureSessionBarView();
            if (!sessionBarView.BindArrangeTable(openArrangeTable) && openArrangeTable != null && !arrangeTableEntryWarned)
            {
                arrangeTableEntryWarned = true;
                Debug.LogWarning(
                    "The Table menu has no Arrange table row: run Console Cards > UI > Add Missing Table Menu Rows (Final UI).");
            }
        }

        /// <summary>
        /// Shows or refreshes the Table mode panel (T3), a side panel that leaves the table in view. False when its
        /// prefab is missing.
        /// </summary>
        public bool ShowTablePanel(TablePanelModel model, TablePanelActions actions)
        {
            try
            {
                EnsureService();
                if (tablePanelView == null || !tablePanelView.IsAcquired)
                {
                    ReleaseView(tabletopPopupView);
                    popupLayer.SetActive(false);
                    ReleaseModalViews();
                    componentToolboxView?.CloseToolbox();
                    tablePanelView = uiService.AcquireCached<TablePanelView>(UiPrefabIds.TablePanel);
                }

                modalLayer.SetActive(true);
                tablePanelView.Bind(model, actions);
                tablePanelView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning(
                    "The Table panel is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                    + exception.Message);
                return false;
            }
        }

        public bool IsTablePanelOpen => IsVisible(tablePanelView);

        public void CloseTablePanel()
        {
            ReleaseView(tablePanelView);
            SetModalLayerOffUnlessHandover();
        }

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
                    contentArtPanelView = uiService.AcquireCached<ContentArtPanelView>(UiPrefabIds.ContentArtPanel);
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
                    ruleProposalPanelView = uiService.AcquireCached<RuleProposalPanelView>(UiPrefabIds.RuleProposalPanel);
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
                    hintStripView = uiService.AcquireCached<HintStripView>(UiPrefabIds.HintStrip);
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

        /// <summary>
        /// Shows the tool bar (TOOLS-1) with its keys strip; while it is shown the old controls strip stays hidden.
        /// False when its prefab is missing (warned once): then the old controls strip carries on as before.
        /// </summary>
        public bool ShowToolBar(
            Action<TableToolKind> chooseTool,
            Action toggleKeysStrip,
            Action groupSelection,
            Action clearSelection,
            Action toggleHandStyle = null)
        {
            try
            {
                EnsureService();
                if (toolBarView == null || !toolBarView.IsAcquired)
                {
                    toolBarView = uiService.AcquireCached<ToolBarView>(UiPrefabIds.ToolBar);
                }

                toolBarView.Bind(chooseTool, toggleKeysStrip, OpenHelp, groupSelection, clearSelection, toggleHandStyle);
                toolBarView.SetKeysAllowed(hintsVisible);
                toolBarView.Show();
                interactionGuideView?.SetStripVisible(false);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                if (!toolBarWarned)
                {
                    toolBarWarned = true;
                    Debug.LogWarning(
                        "The tool bar is missing: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI). "
                        + exception.Message);
                }

                return false;
            }
        }

        public bool IsToolBarShown => IsVisible(toolBarView);

        public void SetActiveTool(TableToolKind kind) => VisibleToolBar()?.SetActiveTool(kind);

        public void SetToolEnabled(TableToolKind kind, bool enabled) => VisibleToolBar()?.SetToolEnabled(kind, enabled);

        public void SetToolKeys(string text) => VisibleToolBar()?.SetKeys(text);

        public void SetToolKeysWanted(bool wanted) => VisibleToolBar()?.SetKeysWanted(wanted);

        public void SetSelectionCount(int count) => VisibleToolBar()?.SetSelectionCount(count);

        public void SetHandStyle(bool fan) => VisibleToolBar()?.SetHandStyle(fan);

        public void ShowSelectionBox(Vector2 screenA, Vector2 screenB) => VisibleToolBar()?.ShowSelectionBox(screenA, screenB);

        public void HideSelectionBox() => VisibleToolBar()?.HideSelectionBox();

        private ToolBarView VisibleToolBar() => IsVisible(toolBarView) ? toolBarView : null;

        /// <summary>The tool bar while it is shown (the Draw tool's panel is filled through it), or null.</summary>
        public ToolBarView ShownToolBar => VisibleToolBar();

        /// <summary>
        /// POL-3 / POL-5: the table's information layer (tooltip, card peek, floating labels). False when its prefab is
        /// missing (warned once); then those simply do not show.
        /// </summary>
        public bool ShowTableOverlay(UnityEngine.Camera worldCamera)
        {
            try
            {
                EnsureService();
                if (tableOverlayView == null || !tableOverlayView.IsAcquired)
                {
                    tableOverlayView = uiService.AcquireCached<TableOverlayView>(UiPrefabIds.TableOverlay);
                }

                tableOverlayView.Bind(worldCamera);
                tableOverlayView.Show();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                if (!tableOverlayWarned)
                {
                    tableOverlayWarned = true;
                    Debug.LogWarning(
                        "The table overlay (tooltips, card peek, die results) is missing: run Console Cards > UI > "
                        + "Create Missing Gameplay UI Prefabs (Final UI). " + exception.Message);
                }

                return false;
            }
        }

        public void ShowTooltip(Vector2 screenPosition, string title, string detail) =>
            VisibleOverlay()?.ShowTooltip(screenPosition, title, detail);

        public void HideTooltip() => VisibleOverlay()?.HideTooltip();

        public void ShowPeek(string title, string body, Sprite artwork, Color surface, Color text) =>
            VisibleOverlay()?.ShowPeek(title, body, artwork, surface, text);

        public void HidePeek() => VisibleOverlay()?.HidePeek();

        public void ShowFloatLabel(Vector3 worldPosition, string text, Color colour, float seconds) =>
            VisibleOverlay()?.ShowFloat(worldPosition, text, colour, seconds);

        private TableOverlayView VisibleOverlay() => IsVisible(tableOverlayView) ? tableOverlayView : null;

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
                    activityLogView = uiService.AcquireCached<ActivityLogView>(UiPrefabIds.ActivityLog);
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
                    hotSeatHandoverView = uiService.AcquireCached<HotSeatHandoverView>(UiPrefabIds.HotSeatHandover);
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
            if (quitting) return;
            if (modalLayer != null && !IsVisible(hotSeatHandoverView) && !IsVisible(contentArtPanelView)
                && !IsVisible(ruleProposalPanelView) && !IsVisible(profilePanelView) && !IsVisible(personaPanelView)
                && !IsVisible(cardEditorPanelView)
                && !IsVisible(deckEditorPanelView) && !IsVisible(gameSelectPanelView) && !IsVisible(frontEndView)
                && !IsVisible(gameSavePanelView) && !IsVisible(tablePanelView))
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
            rulesCardView = uiService.AcquireCached<RulesCardView>(UiPrefabIds.RulesCard);
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
            hintsVisible = on;
            bool toolBarShown = IsVisible(toolBarView);
            interactionGuideView?.SetStripVisible(on && !toolBarShown);
            if (toolBarShown) toolBarView.SetKeysAllowed(on);
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
                feedbackBannerView = uiService.AcquireCached<FeedbackBannerView>(UiPrefabIds.FeedbackBanner);
                feedbackBannerView.ValidateReferences();
                feedbackMessageStackView = uiService.AcquireCached<FeedbackMessageStackView>(
                    UiPrefabIds.FeedbackMessages);
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
            IReadOnlyList<PopupActionOption> actions,
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

        public void ShowCardInspect(CardInspectModel model, Action dismiss)
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

        public void RefreshCardInspect(CardInspectModel model) =>
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
            IReadOnlyList<ActionAbilityPurchaseOption> options,
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

        public void ShowFocusedCardSelection(FocusedCardSelectionModel model)
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

        public void ShowFocusedCardReveal(FocusedCardRevealModel model)
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
            TrapFloorStatusModel status,
            FloorfallStatusModel floorfall,
            IReadOnlyList<PopupActionOption> actions)
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
            IReadOnlyList<PopupActionOption> actions)
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
            IReadOnlyList<PopupActionOption> destinations,
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
            if (popupLayer != null) popupLayer.SetActive(false); // Unity's null check: gone when Play mode ends
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
            ReleaseView(personaPanelView);
            ReleaseView(cardEditorPanelView);
            ReleaseView(deckEditorPanelView);
            ReleaseView(gameSelectPanelView);
            ReleaseView(frontEndView);
            ReleaseView(gameSavePanelView);
            ReleaseView(tablePanelView);
            ReleaseView(hintStripView);
            ReleaseView(toolBarView);
            ReleaseView(tableOverlayView);
            CloseTabletopPopup();
        }

        private void EnsureService()
        {
            if (uiService == null)
            {
                uiService = root != null
                    ? root.UiService
                    : throw new InvalidOperationException("TableUiController requires an authored UI root.");
            }
        }

        private void EnsureComponentToolboxView()
        {
            EnsureService();
            componentToolboxView = uiService.AcquireCached<ComponentToolboxView>(
                UiPrefabIds.ComponentToolbox);
            componentToolboxView.ValidateReferences();
        }

        private void EnsureSessionBarView()
        {
            EnsureService();
            sessionBarView = uiService.AcquireCached<SessionBarView>(UiPrefabIds.SessionBar);
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
            tabletopPopupView = uiService.AcquireCached<TabletopPopupView>(
                UiPrefabIds.TabletopPopup);
            tabletopPopupView.Initialize(uiService);
            tabletopPopupView.ValidateReferences();
        }

        private void EnsureQuantityPopupView()
        {
            EnsureService();
            quantityPopupView = uiService.AcquireCached<QuantityPopupView>(
                UiPrefabIds.QuantityPopup);
            quantityPopupView.ValidateReferences();
        }

        private void EnsureCardInspectPopupView()
        {
            EnsureService();
            cardInspectPopupView = uiService.AcquireCached<CardInspectView>(
                UiPrefabIds.CardInspect);
            cardInspectPopupView.ValidateReferences();
        }

        private void EnsureActionAbilityPurchasePopupView()
        {
            EnsureService();
            actionAbilityPurchasePopupView =
                uiService.AcquireCached<ActionAbilityPurchasePopupView>(
                    UiPrefabIds.ActionAbilityPurchase);
            actionAbilityPurchasePopupView.Initialize(uiService);
            actionAbilityPurchasePopupView.ValidateReferences();
        }

        private void EnsureFocusedCardSelectionView()
        {
            EnsureService();
            focusedCardSelectionView =
                uiService.AcquireCached<FocusedCardSelectionView>(
                    UiPrefabIds.FocusedCardSelection);
            focusedCardSelectionView.Initialize(uiService);
            focusedCardSelectionView.ValidateReferences();
        }

        private void EnsureTrapFloorHudView()
        {
            EnsureService();
            trapFloorHudView = uiService.AcquireCached<TrapFloorHudView>(
                UiPrefabIds.TrapFloorHud);
            trapFloorHudView.Initialize(uiService);
            trapFloorHudView.ValidateReferences();
        }

        private void EnsureInteractionGuideView()
        {
            EnsureService();
            interactionGuideView = uiService.AcquireCached<InteractionGuideView>(
                UiPrefabIds.InteractionGuide);
            interactionGuideView.ValidateReferences();
        }

        private void ReleaseModalViews()
        {
            if (quitting) return;
            ReleaseView(contentArtPanelView);
            ReleaseView(profilePanelView);
            ReleaseView(personaPanelView);
            ReleaseView(cardEditorPanelView);
            ReleaseView(deckEditorPanelView);
            ReleaseView(gameSelectPanelView);
            if (!frontEndBackdrop) ReleaseView(frontEndView);
            ReleaseView(gameSavePanelView);
            ReleaseView(quantityPopupView);
            ReleaseView(cardInspectPopupView);
            ReleaseView(actionAbilityPurchasePopupView);
            ReleaseView(focusedCardSelectionView);
            SetModalLayerOffUnlessHandover();
        }

        // Known issue fixed (2026-10-20): stopping Play mode tears the scene down in no set order, so a panel (or a part
        // of it) can already be gone when the table closes its panels. Then nothing is released: the scene is going.
        private static bool quitting;

        /// <summary>True while Play mode stops or the game quits (the scene's objects are being destroyed).</summary>
        public static bool IsQuitting => quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetQuitting()
        {
            quitting = false;
            UnityEngine.Application.quitting -= MarkQuitting;
            UnityEngine.Application.quitting += MarkQuitting;
        }

        private static void MarkQuitting() => quitting = true;

        private void ReleaseView(ReusableUiView view)
        {
            if (quitting) return;
            if (view != null && view.IsAcquired)
            {
                uiService.Release(view);
            }
        }

        private static bool IsVisible(ReusableUiView view) =>
            view != null && view.IsAcquired && view.gameObject.activeSelf;
    }
}
