using System;
using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// MENU-1 (owner, 2026-10-30: "Pause menu is not there. Settings is still the old one … the table menu opens outside
    /// the screen"): the Esc menu (design board 5) and the in-game Settings (board 6) for the colour-B HUD. The HUD's
    /// menu and gear buttons and Esc open them; without their prefabs the old Table and Settings menus open as before.
    /// The settings the table binds are remembered here, so both the old menus and the new panel show the same values.
    /// </summary>
    public sealed partial class TableUiController
    {
        private TablePauseMenuView pauseMenuView;
        private TableSettingsView tableSettingsView;
        private bool pauseMenuMissing;
        private bool tableSettingsMissing;
        private bool settingsFromPause;
        private bool overlayOpenLastFrame;
        private readonly TablePauseActions pauseActions = new TablePauseActions();
        private readonly TableSettingsModel settingsModel = new TableSettingsModel();

        private string pauseSubtitle = string.Empty;
        private Action pauseNewTable;
        private Action pauseReset;
        private Action pauseClear;
        private Action pauseSaveGame;
        private Action pauseMainMenu;
        private Action pauseExit;

        private bool recHints;
        private bool recTips;
        private bool recFeedback;
        private bool recHotSeat;
        private bool recHandsOnTable;
        private bool recHandShown;
        private string recSeat = string.Empty;
        private Action recNextSeat;
        private Action<bool> rawSetHints;
        private Action<bool> rawSetTips;
        private Action<bool> rawSetFeedback;
        private Action<bool> rawSetHotSeat;
        private Action<bool> rawSetHandsOnTable;
        private Action<bool> rawSetHandShown;
        private Action<bool> setHintsFromSettings;
        private Action<bool> setTipsFromSettings;
        private Action<bool> setFeedbackFromSettings;
        private Action<bool> setHotSeatFromSettings;
        private Action<bool> setHandsOnTableFromSettings;
        private Action<bool> setHandShownFromSettings;
        private Action backToPause;
        private Action closeSettings;

        /// <summary>True while the colour-B HUD is the table's HUD.</summary>
        public bool IsTableHudShown => UsesTableHud;

        public bool IsPauseMenuOpen => IsVisible(pauseMenuView);

        public bool IsTableSettingsOpen => IsVisible(tableSettingsView);

        /// <summary>MAIN MENU and EXIT GAME in the Esc menu (null hides a row).</summary>
        public void SetPauseExits(Action mainMenu, Action exitGame)
        {
            pauseMainMenu = mainMenu;
            pauseExit = exitGame;
        }

        /// <summary>
        /// Esc with nothing else open (no popup, panel, menu or Toolbox, this frame or the last, so the Esc that closes
        /// one never also opens this): the Esc menu. False when it did not open.
        /// </summary>
        public bool TryOpenPauseMenuFromEsc()
        {
            if (!UsesTableHud || overlayOpenLastFrame || IsTableOverlayOpen()) return false;
            return OpenPauseMenu();
        }

        private void LateUpdate()
        {
            overlayOpenLastFrame = IsTableOverlayOpen();
            menuOpenLastFrame = IsMenuOverTable();
            HideHudUnderMakeGame();
        }

        // Board 13: Make a game covers the screen without the HUD; the HUD fades back when it closes. The same while
        // the rulebook on the table is read (BOOK-1: the open book fills the view), and while the Toolbox is open
        // (board 8 shows it without the HUD).
        private CanvasGroup hudGroup;
        private bool hudHiddenForMakeGame;
        private float hudAlpha = 1f;

        private void HideHudUnderMakeGame()
        {
            bool hide = UsesTableHud
                && (IsVisible(gameSavePanelView) || physicalRulebookReading || IsVisible(tableSettingsView)
                    || IsVisible(tablePanelView) // board 12: the Table editor has its own bars
                    || IsVisible(pauseMenuView)
                    || (componentToolboxView != null && componentToolboxView.IsAcquired && componentToolboxView.IsPanelOpen));
            // Board 5: under the Esc menu the HUD stays as a dim ghost; under the others it goes.
            float alpha = !hide ? 1f : IsVisible(pauseMenuView) ? 0.22f : 0f;
            if (!UsesTableHud || (hide == hudHiddenForMakeGame && Mathf.Approximately(alpha, hudAlpha))) return;
            hudHiddenForMakeGame = hide;
            hudAlpha = alpha;
            if (hudGroup == null || hudGroup.gameObject != tableHudView.gameObject)
            {
                hudGroup = tableHudView.GetComponent<CanvasGroup>();
                if (hudGroup == null) hudGroup = tableHudView.gameObject.AddComponent<CanvasGroup>();
            }

            hudGroup.alpha = alpha;
            hudGroup.blocksRaycasts = !hide;
            hudGroup.interactable = !hide;
        }

        private bool IsTableOverlayOpen() =>
            AnyShown(popupLayer) || AnyShown(modalLayer)
            || (sessionBarView != null && sessionBarView.IsAcquired && sessionBarView.IsMenuOpen)
            || (componentToolboxView != null && componentToolboxView.IsAcquired && componentToolboxView.IsPanelOpen)
            || IsVisible(tableMilestoneView) || IsVisible(tablePanelView) || IsVisible(rulebookView)
            || IsVisible(pauseMenuView) || IsVisible(tableSettingsView) || physicalRulebookReading;

        /// <summary>HUD-B board 7B: the HUD's keys (B, Enter, R) wait while a popup, panel or menu is open.</summary>
        public bool HudKeysBlocked() => IsTableOverlayOpen();

        private bool menuOpenLastFrame;

        private bool IsMenuOverTable() =>
            AnyShown(modalLayer) || IsVisible(pauseMenuView) || IsVisible(tableSettingsView) || IsVisible(frontEndView);

        /// <summary>
        /// True while a menu or a panel (the Esc menu, Settings, the front end, a modal panel) is over the table, or was
        /// last frame: a press then is the menu's (MAIN MENU's second press must not reach the table). Popups and the
        /// Toolbox do not count (a press on the table still closes a piece's menu).
        /// </summary>
        public bool IsTableBehindMenu => menuOpenLastFrame || IsMenuOverTable() || Time.frameCount - popupShownFrame <= 1;

        // Board 9: a menu row that opens the next popup somewhere else (Draw… opens Draw how many?) leaves the pointer
        // over the table; that press is the row's, not the table's.
        private int popupShownFrame = -10;

        /// <summary>True while a popup, panel, menu or the Toolbox covers the table (or did last frame).</summary>
        public bool IsTableCovered => overlayOpenLastFrame || IsTableOverlayOpen();

        // ---------- BOOK-1: the rulebook on the table ----------

        private Func<bool> physicalRulebookOpen;
        private Action<RulesCardModel> physicalRulebookRules;
        private bool physicalRulebookReading;

        /// <summary>The rules the rulebook shows (null: the table has none).</summary>
        public RulesCardModel RulebookRules => rulebookRules;

        /// <summary>
        /// The book on the table: how to open it (true when it opened) and how to give it new rules. Null: no book, the
        /// HUD's Rulebook button opens the screen rulebook.
        /// </summary>
        public void SetPhysicalRulebook(Func<bool> open, Action<RulesCardModel> rulesChanged)
        {
            physicalRulebookOpen = open;
            physicalRulebookRules = rulesChanged;
        }

        /// <summary>While the book is read, Esc belongs to it and the table counts as covered.</summary>
        public void SetPhysicalRulebookReading(bool reading) => physicalRulebookReading = reading;

        /// <summary>Before the book rises: the menus, the Toolbox and the screen rulebook make way.</summary>
        public void ClosePanelsForBook()
        {
            ReleaseView(pauseMenuView);
            ReleaseView(tableSettingsView);
            componentToolboxView?.CloseToolbox();
            sessionBarView?.CloseMenu();
            if (IsVisible(rulebookView)) CloseRulebook();
            else CloseModalLayerIfIdle();
        }

        private static bool AnyShown(GameObject layer)
        {
            if (layer == null || !layer.activeInHierarchy) return false;
            Transform mount = layer.transform;
            for (int i = 0; i < mount.childCount; i++)
            {
                if (mount.GetChild(i).gameObject.activeSelf) return true;
            }

            return false;
        }

        // ---------- the Esc menu ----------

        private void OpenMenuFromHud()
        {
            if (IsPauseMenuOpen)
            {
                ClosePauseMenu();
                return;
            }

            if (!OpenPauseMenu()) sessionBarView?.OpenMenu();
        }

        private void OpenSettingsFromHud()
        {
            if (IsTableSettingsOpen)
            {
                CloseTableSettings();
                return;
            }

            if (!OpenTableSettings(false)) sessionBarView?.OpenSettingsMenu();
        }

        public bool OpenPauseMenu()
        {
            if (pauseMenuMissing) return false;
            try
            {
                EnsureService();
                if (pauseMenuView == null || !pauseMenuView.IsAcquired)
                {
                    pauseMenuView = uiService.AcquireCached<TablePauseMenuView>(UiPrefabIds.TablePauseMenu);
                }

                pauseMenuView.ValidateReferences();
            }
            catch (InvalidOperationException exception)
            {
                ReleaseView(pauseMenuView);
                pauseMenuView = null;
                pauseMenuMissing = true;
                Debug.LogWarning(
                    "The Esc menu is missing (the old Table menu opens): run Console Cards > UI > Create Missing Table HUD "
                    + "(HUD-B). " + exception.Message);
                return false;
            }

            ReleaseView(tableSettingsView);
            componentToolboxView?.CloseToolbox();
            sessionBarView?.CloseMenu();
            ReleaseView(rulebookView);
            modalLayer.SetActive(true);
            pauseMenuView.transform.SetAsLastSibling();
            pauseMenuView.Show();
            pauseActions.Resume = ClosePauseMenu;
            pauseActions.OpenRules = rulebookRules != null && (!rulebookMissing || physicalRulebookOpen != null)
                ? ThenClose(OpenRulebook)
                : null;
            pauseActions.OpenHowToPlay = interactionGuideView != null ? ThenClose(OpenHelp) : null;
            pauseActions.SaveAsGame = ThenClose(pauseSaveGame);
            pauseActions.OpenSettings = () => OpenTableSettings(true);
            pauseActions.NewTable = null; // NEW TABLE… opens the main menu: MAIN MENU is that row
            pauseActions.ResetTable = ThenClose(pauseReset);
            pauseActions.ClearTable = ThenClose(pauseClear);
            pauseActions.MainMenu = ThenClose(pauseMainMenu ?? pauseNewTable);
            pauseActions.ExitGame = pauseExit;
            pauseActions.AskBeforeReset = AskBeforeReset; // board 6: Settings › Ask before Reset
            pauseMenuView.Bind("PAUSED", pauseSubtitle, pauseActions);
            return true;
        }

        public void ClosePauseMenu()
        {
            ReleaseView(pauseMenuView);
            CloseModalLayerIfIdle();
        }

        // The menu closes first, so what the row opens (the rulebook, the save panel) is not under it.
        private Action ThenClose(Action action)
        {
            if (action == null) return null;
            return () =>
            {
                ClosePauseMenu();
                action();
            };
        }

        private void CloseModalLayerIfIdle()
        {
            if (!IsVisible(quantityPopupView)
                && !IsVisible(cardInspectPopupView)
                && !IsVisible(actionAbilityPurchasePopupView)
                && !IsVisible(focusedCardSelectionView))
            {
                SetModalLayerOffUnlessHandover();
            }
        }

        private void RememberTableRows(string title, string subtitle, Action newTable, Action reset, Action clear)
        {
            pauseSubtitle = string.IsNullOrEmpty(subtitle) ? title ?? string.Empty : $"{title} · {subtitle}";
            pauseNewTable = newTable;
            pauseReset = reset;
            pauseClear = clear;
        }

        /// <summary>Boards 7B and 14v2: news at the top centre of the table (it fades by itself).</summary>
        public void ShowTableNews(string text, Color dot)
        {
            if (UsesTableHud && tableHudView != null && tableHudView.IsAcquired) tableHudView.ShowNews(text, dot);
        }

        // ---------- Settings ----------

        public bool OpenTableSettings(bool fromPause)
        {
            if (tableSettingsMissing) return false;
            try
            {
                EnsureService();
                if (tableSettingsView == null || !tableSettingsView.IsAcquired)
                {
                    tableSettingsView = uiService.AcquireCached<TableSettingsView>(UiPrefabIds.TableSettings);
                }

                tableSettingsView.ValidateReferences();
            }
            catch (InvalidOperationException exception)
            {
                ReleaseView(tableSettingsView);
                tableSettingsView = null;
                tableSettingsMissing = true;
                Debug.LogWarning(
                    "The Settings panel is missing (the old Settings menu opens): run Console Cards > UI > Create Missing "
                    + "Table HUD (HUD-B). " + exception.Message);
                return false;
            }

            settingsFromPause = fromPause;
            ReleaseView(pauseMenuView);
            componentToolboxView?.CloseToolbox();
            sessionBarView?.CloseMenu();
            modalLayer.SetActive(true);
            tableSettingsView.transform.SetAsLastSibling();
            tableSettingsView.Show();
            FillSettingsModel();
            tableSettingsView.Bind(settingsModel, true);
            return true;
        }

        public void CloseTableSettings()
        {
            ReleaseView(tableSettingsView);
            CloseModalLayerIfIdle();
        }

        private void BackToPause()
        {
            ReleaseView(tableSettingsView);
            if (!OpenPauseMenu()) CloseModalLayerIfIdle();
        }

        private void FillSettingsModel()
        {
            settingsModel.HintsOn = recHints;
            settingsModel.SetHints = rawSetHints != null ? setHintsFromSettings ??= SetHintsFromSettings : null;
            settingsModel.TipsOn = recTips;
            settingsModel.SetTips = rawSetTips != null ? setTipsFromSettings ??= SetTipsFromSettings : null;
            settingsModel.FeedbackOn = recFeedback;
            settingsModel.SetFeedback = rawSetFeedback != null ? setFeedbackFromSettings ??= SetFeedbackFromSettings : null;
            settingsModel.SeatText = recSeat;
            settingsModel.NextSeat = recNextSeat;
            settingsModel.HotSeatOn = recHotSeat;
            settingsModel.SetHotSeat = rawSetHotSeat != null ? setHotSeatFromSettings ??= SetHotSeatFromSettings : null;
            settingsModel.HandsOnTableOn = recHandsOnTable;
            settingsModel.SetHandsOnTable =
                rawSetHandsOnTable != null ? setHandsOnTableFromSettings ??= SetHandsOnTableFromSettings : null;
            settingsModel.HandShown = recHandShown;
            settingsModel.SetHandShown = rawSetHandShown != null ? setHandShownFromSettings ??= SetHandShownFromSettings : null;
            settingsModel.Back = settingsFromPause ? backToPause ??= BackToPause : null;
            settingsModel.Close = closeSettings ??= CloseTableSettings;
            FillBoardSettings(settingsModel); // board 6 (TableUiController.Settings6.cs)
        }

        // A later bind (the table refreshing its UI) shows the new values in an open panel.
        private void RefreshSettingsView()
        {
            if (!IsVisible(tableSettingsView)) return;
            FillSettingsModel();
            tableSettingsView.Bind(settingsModel, false);
        }

        private void RememberHints(bool on, Action<bool> set)
        {
            recHints = on;
            rawSetHints = set;
            RefreshSettingsView();
        }

        private void RememberTips(bool on, Action<bool> set)
        {
            recTips = on;
            rawSetTips = set;
            EndFirstGamesTips(); // board 6: FIRST GAMES turns the tips off after the first games
            RefreshSettingsView();
        }

        private void RememberPlay(bool available, string seatText, Action nextSeat, bool hotSeatOn, Action<bool> setHotSeat)
        {
            recSeat = available ? seatText ?? string.Empty : string.Empty;
            recNextSeat = available ? nextSeat : null;
            recHotSeat = hotSeatOn;
            rawSetHotSeat = available ? setHotSeat : null;
            RefreshSettingsView();
        }

        private void RememberView(bool handsAvailable, bool handsOnTable, Action<bool> setHandsOnTable, bool feedback,
            Action<bool> setFeedback)
        {
            recHandsOnTable = handsOnTable;
            rawSetHandsOnTable = handsAvailable ? setHandsOnTable : null;
            recFeedback = feedback;
            rawSetFeedback = setFeedback;
            RefreshSettingsView();
        }

        private void RememberHandTray(bool available, bool shown, Action<bool> set)
        {
            recHandShown = shown;
            rawSetHandShown = available ? set : null;
            RefreshSettingsView();
        }

        private void SetHintsFromSettings(bool on)
        {
            recHints = on;
            rawSetHints?.Invoke(on);
        }

        private void SetTipsFromSettings(bool on)
        {
            recTips = on;
            rawSetTips?.Invoke(on);
        }

        private void SetFeedbackFromSettings(bool on)
        {
            recFeedback = on;
            rawSetFeedback?.Invoke(on);
        }

        private void SetHotSeatFromSettings(bool on)
        {
            recHotSeat = on;
            rawSetHotSeat?.Invoke(on);
        }

        private void SetHandsOnTableFromSettings(bool on)
        {
            recHandsOnTable = on;
            rawSetHandsOnTable?.Invoke(on);
        }

        private void SetHandShownFromSettings(bool on)
        {
            recHandShown = on;
            rawSetHandShown?.Invoke(on);
        }
    }
}
