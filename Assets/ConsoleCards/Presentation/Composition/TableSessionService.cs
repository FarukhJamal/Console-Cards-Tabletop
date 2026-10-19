using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// The table session: starting, replacing and resetting the table, and Undo and Redo with their history.
    /// (R1f: moved out of TabletopComposition; the table reaches it as <c>SessionService</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class TableSessionService
    {
        private readonly TabletopComposition host;

        internal TableSessionService(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.Session.cs ----------

        public void ClearTable()
        {
            RequestTableReplacement(TabletopSessionSelection.EmptyCustom);
        }

        public void LoadGameTemplate(GameTemplateId gameTemplateId)
        {
            RequestTableReplacement(TabletopSessionSelection.FromGameTemplate(gameTemplateId));
        }

        public void ToggleGameTemplatesPanel()
        {
            if (!host.IsInitialized)
            {
                return;
            }

            if (host.gameTemplatesPanelVisible)
            {
                CloseGameTemplatesPanel();
                return;
            }

            host.gameTemplatesPanelVisible = true;
            RefreshGameTemplatesPanelUi();
        }

        // Table menu > New table… (UI-1): opens the game selection menu (L2-4); the older game list only when that
        // menu's prefab is missing.
        internal void OpenGameTemplatesPanel()
        {
            if (!host.IsInitialized)
            {
                return;
            }

            if (host.GameSelectPanel.OpenGameSelect()) return;
            host.gameTemplatesPanelVisible = true;
            RefreshGameTemplatesPanelUi();
        }

        internal void CloseGameTemplatesPanel()
        {
            host.gameTemplatesPanelVisible = false;
            host.runtimeUi?.HideGameTemplatesPanel();
        }

        internal void PrepareTemplateCatalog()
        {
            sessionBootstrapService = new TabletopSessionBootstrapService();
            tableActionActorId = PlayerId.New();
            host.authoritativeRandomValueSource = new SystemRandomValueSource();
            templateCatalogError = null;
            host.gameTemplatesPanelError = null;
            try
            {
                RegisterGameTemplates();
            }
            catch (Exception exception)
            {
                host.availableTrapFloorTemplates.Clear();
                host.sessionTemplateCatalog = new GameTemplateCatalog(Array.Empty<GameTemplateRegistration>());
                templateCatalogError = exception.Message;
            }
        }

        // CART-1a: every code cartridge adds its table setups (one per mode) to the catalog New table… starts from. A
        // cartridge that cannot set up says so by name.
        internal void RegisterGameTemplates()
        {
            List<GameTemplateRegistration> registrations = new List<GameTemplateRegistration>();
            IReadOnlyList<IGameCartridge> cartridges = host.Cartridges.CodeCartridges;
            for (int i = 0; i < cartridges.Count; i++)
            {
                try
                {
                    cartridges[i].RegisterTemplates(registrations);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException($"{cartridges[i].DisplayName} is unavailable: {exception.Message}", exception);
                }
            }

            host.sessionTemplateCatalog = new GameTemplateCatalog(registrations);
        }

        internal void InitializeRuntimeUi()
        {
            RequireReference(host.runtimeUi, nameof(host.runtimeUi));
            if (!host.runtimeUi.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "TabletopComposition requires runtimeUi to reference the scene-authored UI controller.");
            }

            host.runtimeUi.ValidateReferences();
        }

        internal void RefreshGameTemplatesPanelUi()
        {
            if (host.runtimeUi == null || !host.gameTemplatesPanelVisible)
            {
                return;
            }

            List<GameTemplateOption> options = new List<GameTemplateOption>();
            if (host.sessionTemplateCatalog != null)
            {
                foreach (GameTemplateRegistration registration in host.sessionTemplateCatalog.Registrations.Values)
                {
                    GameTemplateId templateId = registration.Template.Id;
                    options.Add(new GameTemplateOption(
                        registration.Template.DisplayName,
                        () => RequestTableReplacement(
                            TabletopSessionSelection.FromGameTemplate(templateId))));
                }
            }

            host.runtimeUi.ShowGameTemplatesPanel(
                () => RequestTableReplacement(TabletopSessionSelection.EmptyCustom),
                options,
                host.gameTemplatesPanelError ?? templateCatalogError);
        }

        internal void ShowActiveSessionUi()
        {
            if (host.runtimeUi == null || host.activeSession == null)
            {
                return;
            }

            // Session bar chip (UI-1): a game shows its name and mode ("Trap Floor — Easy"); Empty Table its name.
            bool isGameSession = host.activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom;
            if (host.ruleProposalOpen) host.RulesCardPanel.CloseRuleProposal(null); // a new table has new players
            string sessionTitle = "EMPTY TABLE";
            string sessionSubtitle = string.Empty;
            if (isGameSession)
            {
                string displayName = host.activeSession.Template.DisplayName;
                int separator = displayName.IndexOf(" — ", StringComparison.Ordinal);
                sessionTitle = (separator < 0 ? displayName : displayName.Substring(0, separator)).ToUpperInvariant();
                sessionSubtitle = separator < 0 ? string.Empty : displayName.Substring(separator + 3);
            }
            else if (host.PlayerGamesPanel.activePlayerGame != null)
            {
                // Doc 29: a game you made runs on an Empty Table; the chip shows its name.
                sessionTitle = (host.PlayerGamesPanel.activePlayerGame.name ?? "Your game").ToUpperInvariant();
                sessionSubtitle = "Your game";
            }

            host.runtimeUi.ShowActiveSession(
                sessionTitle,
                sessionSubtitle,
                isGameSession,
                HandleUndoButtonPressed,
                HandleRedoButtonPressed,
                OpenGameTemplatesPanel,
                ResetTable,
                ClearTable,
                host.ActiveCartridge.StatusText(),
                new ComponentToolboxBindings(
                    host.componentLibrary,
                    host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom ? null : host.trapFloorGameDefinition,
                    host.PlaceCatalogEntry,
                    host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom && !host.emptyTableHandContainerId.IsEmpty,
                    host.emptyTableHandEnabled,
                    host.SetEmptyTableHandEnabled));
            host.runtimeUi.ShowTableSettings(
                host.RulesCardPanel.BuildRulesCardModel(),
                host.rulesCardEnabled,
                host.RulesCardPanel.SetRulesCardEnabled,
                host.hintsEnabled,
                host.RulesCardPanel.SetHintsEnabled);
            host.runtimeUi.ShowTipsSetting(host.tipsEnabled, host.RulesCardPanel.SetTipsEnabled);
            host.runtimeUi.ShowPlaySettings(
                host.trapFloorTemplate != null && host.activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom,
                $"Seat: {host.PlayerLabel(host.localPlayerId)}",
                host.NextScreenSeat,
                host.hotSeatMode,
                host.SetHotSeatMode);
            // Card art, the card editor and the deck editor live in the game selection menu (L2-4), not on the table.
            host.runtimeUi.ShowCardArtEntry(null);
            host.CardEditorPanel.RefreshCardEditorEntry(); // L2-2: the game's cards and yours, on every table
            host.DeckEditorPanel.RefreshDeckEditorEntry(); // L2-3: the game's deck and yours, on every table
            host.ProfilePanel.RefreshProfileEntry(); // doc 28, P2: who is playing, on every table
            host.RefreshTableShapeEntry(); // T1: the table's shape, on every table
            host.PlayerGamesPanel.RefreshSaveGameEntry(); // doc 29: Save table as a game, on an Empty Table
            host.runtimeUi.ShowViewSettings(
                host.trapFloorTemplate != null && host.activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom,
                host.handsOnTable,
                host.SetHandsOnTable,
                host.fullFeedback,
                host.SetFullFeedback);
            RefreshUndoUi();
            host.ActiveCartridge.RefreshStatusUi();
            host.RulesCardPanel.RefreshGameHints();
            host.ActiveCartridge.RefreshShopEntry();
            host.RefreshToolBar(); // TOOLS-1: the tool bar and its keys strip (after the Hints switch is applied)
            host.RefreshTableOverlay(); // POL-3 / POL-5: tooltips, the card peek and die results
            host.RulesCardPanel.RaiseHint(HintTriggers.TableReady, isGameSession ? "game" : "empty");
        }

        internal void HandleUndoButtonPressed()
        {
            UndoLatestAction();
        }

        internal void HandleRedoButtonPressed()
        {
            RedoLatestAction();
        }

        internal void RefreshRuntimeStatusUi()
        {
            if (host.runtimeUi != null && host.IsInitialized)
            {
                host.runtimeUi.SetStatusMessage(host.ActiveCartridge.StatusText());
            }
        }

        internal void RequestTableReplacement(TabletopSessionSelection selection)
        {
            if (!host.IsInitialized || !HasCurrentTableContent())
            {
                TryReplaceTable(selection);
                return;
            }

            string replacementName = ReplacementDisplayName(selection);
            host.runtimeUi.ShowContextMenu(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                "REPLACE CURRENT TABLE?",
                $"The current table contains content. Loading {replacementName} will replace it and establish a new Reset baseline.",
                new[]
                {
                    new PopupActionOption(
                        selection.Kind == TabletopSessionKind.EmptyCustom
                            ? "Clear Table"
                            : $"Load {replacementName}",
                        true,
                        () => TryReplaceTable(selection)),
                    new PopupActionOption(
                        "Cancel",
                        true,
                        host.ContextMenus.CloseContextMenu),
                },
                host.ContextMenus.CloseContextMenu,
                host.ContextMenus.DismissPopupFromSecondary);
        }

        internal bool TryReplaceTable(TabletopSessionSelection selection)
        {
            if (sessionBootstrapService == null || host.sessionTemplateCatalog == null)
            {
                host.gameTemplatesPanelError = "Game Template loading is not configured.";
                host.runtimeUi?.SetGameTemplatesError(host.gameTemplatesPanelError);
                return false;
            }

            if (selection.Kind == TabletopSessionKind.GameTemplate
                && host.availableTrapFloorTemplates.ContainsKey(selection.GameTemplateId))
            {
                try
                {
                    RegisterGameTemplates();
                }
                catch (Exception exception)
                {
                    host.gameTemplatesPanelError = $"The game's setup could not be prepared: {exception.Message}";
                    host.runtimeUi?.SetGameTemplatesError(host.gameTemplatesPanelError);
                    return false;
                }
            }

            List<PlayerId> activePlayerIds = CreateDefaultActivePlayers(selection);
            TabletopSessionBootstrapRequest request = new TabletopSessionBootstrapRequest(
                tableActionActorId,
                selection,
                activePlayerIds,
                MatchId.New());
            TabletopSessionBootstrapResult result = sessionBootstrapService.TryCreate(
                request,
                host.sessionTemplateCatalog);
            if (!result.Succeeded)
            {
                host.gameTemplatesPanelError = FormatSessionBuildFailure(result.Issues);
                host.runtimeUi?.SetGameTemplatesError(host.gameTemplatesPanelError);
                return false;
            }

            TrapFloorTemplateDefinition candidateTemplate = null;
            TemplateContext candidateContext = null;
            try
            {
                if (selection.Kind == TabletopSessionKind.GameTemplate)
                {
                    if (!host.availableTrapFloorTemplates.TryGetValue(
                            selection.GameTemplateId,
                            out candidateTemplate))
                    {
                        throw new InvalidOperationException(
                            "The selected Game Template has no registered prototype Presentation wiring.");
                    }

                    candidateContext = CreateTrapFloorTemplateContext(
                        result.Session,
                        candidateTemplate,
                        tableActionActorId);
                }
            }
            catch (Exception exception)
            {
                host.gameTemplatesPanelError = exception.Message;
                host.runtimeUi?.SetGameTemplatesError(host.gameTemplatesPanelError);
                Debug.LogError($"Table replacement validation failed: {exception.Message}", host);
                return false;
            }

            TabletopSession previousSession = host.activeSession;
            TemplateContext previousContext = host.templateContext;
            TrapFloorTemplateDefinition previousTemplate = host.trapFloorTemplate;
            bool hadActivePresentation = host.IsInitialized;

            try
            {
                if (hadActivePresentation)
                {
                    host.Shutdown();
                }

                host.activeSession = result.Session;
                host.emptyTableHandEnabled = true;
                host.templateContext = candidateContext;
                host.trapFloorTemplate = candidateTemplate;
                host.InitializeActiveSession(false);
                host.gameTemplatesPanelError = null;
                host.gameTemplatesPanelVisible = false;
                ShowActiveSessionUi();
                host.GameSelectPanel.OnTableReplaced(selection); // L2-5: a deck of yours chosen for an Empty Table is placed now
                return true;
            }
            catch (Exception exception)
            {
                host.Shutdown();
                host.activeSession = previousSession;
                host.templateContext = previousContext;
                host.trapFloorTemplate = previousTemplate;
                host.gameTemplatesPanelError = $"Table replacement failed: {exception.Message}";
                Debug.LogError(host.gameTemplatesPanelError, host);

                if (hadActivePresentation && previousSession != null)
                {
                    try
                    {
                        host.InitializeActiveSession(false);
                        ShowActiveSessionUi();
                        host.gameTemplatesPanelVisible = true;
                        RefreshGameTemplatesPanelUi();
                    }
                    catch (Exception rollbackException)
                    {
                        Debug.LogError(
                            $"Previous table presentation could not be restored: {rollbackException.Message}",
                            host);
                    }
                }

                return false;
            }
        }

        internal bool HasCurrentTableContent()
        {
            return host.matchState != null
                && (host.matchState.ObjectCount > 0
                    || host.matchState.Containers.Count > 0
                    || host.matchState.ContainerPlacements.Count > 0
                    || host.matchState.Seats.Count > 0
                    || host.matchState.PlacedConsoles.Count > 0
                    || host.matchState.PlayAreas.Count > 0);
        }

        internal string ReplacementDisplayName(TabletopSessionSelection selection)
        {
            if (selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                return "a fresh Empty Table";
            }

            return host.sessionTemplateCatalog.TryGet(
                    selection.GameTemplateId,
                    out GameTemplateRegistration registration)
                ? registration.Template.DisplayName
                : "the selected Game Template";
        }

        internal List<PlayerId> CreateDefaultActivePlayers(TabletopSessionSelection selection)
        {
            int playerCount = 1;
            if (selection.Kind == TabletopSessionKind.GameTemplate)
            {
                if (!host.sessionTemplateCatalog.TryGet(selection.GameTemplateId, out GameTemplateRegistration registration))
                {
                    return new List<PlayerId> { tableActionActorId };
                }

                playerCount = registration.Template.RequiredPlayerCount;
            }

            List<PlayerId> players = new List<PlayerId>(playerCount)
            {
                tableActionActorId,
            };
            for (int i = 1; i < playerCount; i++)
            {
                players.Add(PlayerId.New());
            }

            return players;
        }

        internal static string FormatSessionBuildFailure(
            IReadOnlyList<TabletopSessionBootstrapIssue> issues)
        {
            string message = "Authoritative session construction failed.";
            for (int i = 0; i < issues.Count; i++)
            {
                message += $" {issues[i]}";
            }

            return message;
        }
        // ---------- from TabletopComposition.Undo.cs ----------

        public void ResetTable()
        {
            host.EnsureInitialized();
            MatchState previousMatch = host.activeSession.CurrentMatch;
            SessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                // Reset returns the table to its start, Empty Table Hand switched on included (H-E).
                host.emptyTableHandEnabled = true;
                host.Shutdown(true);
                host.InitializeActiveSession(true);
                ShowActiveSessionUi();
            }
            catch (Exception exception)
            {
                host.RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Reset");
                Debug.LogError($"Reset Presentation rebuild failed: {exception.Message}", host);
                throw;
            }

            host.ActiveCartridge.RefreshStatusUi();
        }

        public bool UndoLatestAction()
        {
            return UndoLatestAction(host.localPlayerId);
        }

        public bool UndoLatestAction(PlayerId requestingPlayerId)
        {
            if (!IsActiveSessionPlayer(requestingPlayerId)
                || !CanUndoCurrentAction()
                || !host.undoHistory.TryPeekUndo(
                    out SessionUndoSnapshot snapshot,
                    out ActiveSessionUndoTransaction transaction))
            {
                return false;
            }

#if CONSOLECARDS_LOG_UNDO
            Debug.Log(
                $"[Undo] Selecting state index {host.undoHistory.CurrentStateIndex - 1} "
                + $"from {host.undoHistory.TransactionCount} transactions: {transaction.Description}; "
                + $"objects={snapshot.Match.ObjectCount}; ids={FormatUndoObjectIds(snapshot.Match)}.",
                host);
#endif

            long undoRevision;
            try
            {
                undoRevision = checked(host.matchState.Revision + 1L);
            }
            catch (OverflowException)
            {
                host.ShowMessage("Undo rejected: Match revision cannot advance.");
                return false;
            }

            MatchState replacement;
            TrapFloorSessionState restoredTrapFloor = null;
            PendingControllerPurchaseState restoredPurchase = null;
            try
            {
                replacement = snapshot.Match.Restore(undoRevision);
                restoredTrapFloor = snapshot.TrapFloor?.Restore();
                restoredPurchase = snapshot.PendingControllerPurchase?.Restore();
            }
            catch (Exception exception)
            {
                host.ShowMessage($"Undo rejected: {exception.Message}");
                return false;
            }

            host.rebuildingFromUndo = true;
            MatchState previousMatch = host.activeSession.CurrentMatch;
            SessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                host.Shutdown(true);
                host.activeSession.ReplaceCurrentMatch(replacement);
                host.pendingRestoredTrapFloorState = restoredTrapFloor;
                host.pendingRestoredControllerPurchaseState = restoredPurchase;
                host.InitializeActiveSession(false);
                host.undoHistory.CommitUndo();
                host.undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                Debug.Log(
                    $"[Undo] Restored state index {host.undoHistory.CurrentStateIndex}; "
                    + $"history states={host.undoHistory.CurrentStateIndex}/{host.undoHistory.TransactionCount}; "
                    + $"objects={host.matchState.ObjectCount}.",
                    host);
                ShowActiveSessionUi();
                host.ShowMessage($"Undid {transaction.Description}.");
                return true;
            }
            catch (Exception exception)
            {
                host.RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Undo");
                Debug.LogError($"Undo Presentation rebuild failed: {exception.Message}", host);
                throw;
            }
            finally
            {
                host.pendingRestoredTrapFloorState = null;
                host.pendingRestoredControllerPurchaseState = null;
                host.rebuildingFromUndo = false;
                RefreshUndoUi();
            }
        }

        public bool RedoLatestAction()
        {
            return RedoLatestAction(host.localPlayerId);
        }

        public bool RedoLatestAction(PlayerId requestingPlayerId)
        {
            if (!IsActiveSessionPlayer(requestingPlayerId)
                || !CanRedoCurrentAction()
                || !host.undoHistory.TryPeekRedo(
                    out SessionUndoSnapshot snapshot,
                    out ActiveSessionUndoTransaction transaction))
            {
                return false;
            }

            long redoRevision;
            try
            {
                redoRevision = checked(host.matchState.Revision + 1L);
            }
            catch (OverflowException)
            {
                host.ShowMessage("Redo rejected: Match revision cannot advance.");
                return false;
            }

            MatchState replacement;
            TrapFloorSessionState restoredTrapFloor = null;
            PendingControllerPurchaseState restoredPurchase = null;
            try
            {
                replacement = snapshot.Match.Restore(redoRevision);
                restoredTrapFloor = snapshot.TrapFloor?.Restore();
                restoredPurchase = snapshot.PendingControllerPurchase?.Restore();
            }
            catch (Exception exception)
            {
                host.ShowMessage($"Redo rejected: {exception.Message}");
                return false;
            }

            host.rebuildingFromUndo = true;
            MatchState previousMatch = host.activeSession.CurrentMatch;
            SessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                host.Shutdown(true);
                host.activeSession.ReplaceCurrentMatch(replacement);
                host.pendingRestoredTrapFloorState = restoredTrapFloor;
                host.pendingRestoredControllerPurchaseState = restoredPurchase;
                host.InitializeActiveSession(false);
                host.undoHistory.CommitRedo();
                host.undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                ShowActiveSessionUi();
                host.ShowMessage($"Redid {transaction.Description}.");
                return true;
            }
            catch (Exception exception)
            {
                host.RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Redo");
                Debug.LogError($"Redo Presentation rebuild failed: {exception.Message}", host);
                throw;
            }
            finally
            {
                host.pendingRestoredTrapFloorState = null;
                host.pendingRestoredControllerPurchaseState = null;
                host.rebuildingFromUndo = false;
                RefreshUndoUi();
            }
        }

        internal bool IsActiveSessionPlayer(PlayerId playerId)
        {
            if (playerId.IsEmpty || host.activeSession == null) return false;
            IReadOnlyList<PlayerId> players = host.activeSession.Request.ActivePlayerIds;
            for (int i = 0; i < players.Count; i++)
                if (players[i] == playerId) return true;
            return false;
        }

        // POL-0: the keys come from the Input Actions asset (Undo: Ctrl+Z).
        internal bool HandleUndoShortcut()
        {
            if (!host.IsInitialized || !host.Keys.UndoPressedThisFrame)
            {
                return false;
            }

            UndoLatestAction();
            return true;
        }

        // POL-0: the keys come from the Input Actions asset (Redo: Ctrl+Y or Ctrl+Shift+Z).
        internal bool HandleRedoShortcut()
        {
            if (!host.IsInitialized || !host.Keys.RedoPressedThisFrame)
            {
                return false;
            }

            RedoLatestAction();
            return true;
        }

        internal bool CanUndoCurrentAction()
        {
            return host.IsInitialized
                && !host.rebuildingFromUndo
                && !host.undoTransactionInProgress
                && host.undoHistory.CanUndo;
        }

        internal bool CanRedoCurrentAction()
        {
            return host.IsInitialized
                && !host.rebuildingFromUndo
                && !host.undoTransactionInProgress
                && host.undoHistory.CanRedo;
        }

        internal void BeginUndoTrackingForCurrentMatch()
        {
            StopUndoTrackingCurrentMatch();
            host.undoTrackedMatch = host.matchState;
            host.undoTrackedMatch.AuthoritativeActionAccepted += HandleAuthoritativeActionAccepted;
            host.undoTransactionInProgress = false;
            if (!host.rebuildingFromUndo)
            {
                host.undoHistory.EstablishBaseline(CaptureUndoSnapshot());
            }
            RefreshUndoUi();
        }

        internal void StopUndoTrackingCurrentMatch()
        {
            if (host.undoTrackedMatch != null)
            {
                host.undoTrackedMatch.AuthoritativeActionAccepted -= HandleAuthoritativeActionAccepted;
                host.undoTrackedMatch = null;
            }
            host.undoTransactionInProgress = false;
        }

        internal void HandleAuthoritativeActionAccepted(AuthoritativeActionAcceptance acceptance)
        {
            host.pileCountCheckPending = true; // landing taps and small-stack tidy-up (TabletopComposition.Stacking.cs)

            if (acceptance.Kind == AuthoritativeActionKind.TransferCard
                || acceptance.Kind == AuthoritativeActionKind.DrawCards)
            {
                host.RefreshAcceptedCardFaces(); // a Hand turns arriving cards face up (H1b)
            }

            if ((acceptance.Kind == AuthoritativeActionKind.MoveObject
                    || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled)
                && host.TrapFloorGame.IsGuidedMoveShown())
            {
                // R4: a move made while an Ability or a Trap shows where to go is not a regular move.
                host.TrapFloorGame.EnsureMoveTurn();
                host.TrapFloorGame.guidedMoveThisTurn = true;
            }

            if ((acceptance.Kind == AuthoritativeActionKind.MoveObject
                    || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled)
                && host.trapFloorAbilityResolutionService != null
                && host.trapFloorAbilityResolutionService.ClearMovementAssistanceIfPawnMoved(host.matchState))
            {
                host.abilityTargetPresenter?.ClearCurrent();
            }

            if ((acceptance.Kind == AuthoritativeActionKind.MoveObject
                    || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled)
                && host.trapFloorTemplate != null)
            {
                host.landingCheckPending = true;
            }

            if (acceptance.Kind == AuthoritativeActionKind.MoveObject
                || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled)
            {
                host.consoleDieCheckPending = true; // C3d: a die may have come to rest on a Console D-pad arrow
                host.boardSnapCheckPending = true; // C3e: a card may have come to rest on a board space
            }

            if (host.undoBatchActive)
            {
                undoBatchLastAcceptance = acceptance;
                return;
            }

            switch (acceptance.RecordMode)
            {
                case AuthoritativeActionRecordMode.Intermediate:
                    host.undoTransactionInProgress = true;
                    RefreshUndoUi();
                    break;
                case AuthoritativeActionRecordMode.Transaction:
                    SessionUndoSnapshot beforeState = host.undoHistory.CurrentState;
                    SessionUndoSnapshot afterState = CaptureUndoSnapshot();
                    host.undoHistory.RecordAccepted(
                        beforeState,
                        afterState,
                        acceptance,
                        DescribeUndoAction(acceptance));
#if CONSOLECARDS_LOG_UNDO
                    // SMOOTH-1: off by default. Building this line (every object id on the table) and logging it with a
                    // stack trace on every accepted action cost a few milliseconds per drop on a full table.
                    Debug.Log(
                        $"[Undo] Recorded state index {host.undoHistory.CurrentStateIndex}; "
                        + $"transactions={host.undoHistory.TransactionCount}; "
                        + $"action={DescribeUndoAction(acceptance)}; "
                        + $"beforeObjects={beforeState.Match.ObjectCount}; "
                        + $"afterObjects={afterState.Match.ObjectCount}; "
                        + $"afterIds={FormatUndoObjectIds(afterState.Match)}.",
                        host);
#endif
                    host.undoTransactionInProgress = false;
                    RefreshUndoUi();
                    host.RulesCardPanel.RaiseHint(RulesCardPresenter.ActionHintTrigger(acceptance.Kind));
                    host.RulesCardPanel.RaiseHint(HintTriggers.UndoAvailable);
                    break;
                case AuthoritativeActionRecordMode.CancelTransaction:
                    host.undoTransactionInProgress = false;
                    host.undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                    RefreshUndoUi();
                    break;
                default:
                    if (!host.undoTransactionInProgress)
                    {
                        host.undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                        RefreshUndoUi();
                    }
                    break;
            }
        }

        internal void BeginUndoBatch()
        {
            host.undoBatchActive = host.undoTrackedMatch == host.matchState && host.undoHistory.CurrentStateIndex >= 0;
            undoBatchBefore = host.undoBatchActive ? host.undoHistory.CurrentState : null;
            undoBatchLastAcceptance = null;
        }

        internal void EndUndoBatch()
        {
            if (!host.undoBatchActive) return;
            host.undoBatchActive = false;
            if (undoBatchLastAcceptance.HasValue && host.undoTrackedMatch == host.matchState)
            {
                AuthoritativeActionAcceptance last = undoBatchLastAcceptance.Value;
                host.undoHistory.RecordAccepted(
                    undoBatchBefore,
                    CaptureUndoSnapshot(),
                    last,
                    DescribeUndoAction(last));
                host.undoTransactionInProgress = false;
                RefreshUndoUi();
            }

            undoBatchBefore = null;
            undoBatchLastAcceptance = null;
        }

        // Folds follow-up tidying (a stack left with one card turned back into a card) into the step the player
        // just made, so Undo never brings back the in-between state (a stack of one). When that step is still
        // being recorded (the card taken off is still falling), nothing is replaced: the step is recorded when the
        // card lands, with the tidy-up in it. Snapshots are never taken of a card in mid-air.
        internal void BeginUndoAmend()
        {
            BeginUndoBatch();
        }

        internal void EndUndoAmend()
        {
            if (!host.undoBatchActive) return;
            host.undoBatchActive = false;
            if (undoBatchLastAcceptance.HasValue && host.undoTrackedMatch == host.matchState && !host.undoTransactionInProgress)
            {
                host.undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                RefreshUndoUi();
            }

            undoBatchBefore = null;
            undoBatchLastAcceptance = null;
        }

        internal SessionUndoSnapshot CaptureUndoSnapshot()
        {
            return SessionUndoSnapshot.Capture(
                host.matchState,
                host.trapFloorActivityFeed,
                host.trapFloorObjectiveState,
                host.trapFloorCollapseState,
                host.trapFloorTurnState,
                host.trapFloorAbilityResolutionState,
                host.pendingControllerPurchaseState);
        }

        internal static string FormatUndoObjectIds(GameTemplateInitialSnapshot snapshot)
        {
            IReadOnlyList<TabletopObjectId> ids = snapshot.CopyObjectIds();
            if (ids.Count == 0) return "<none>";
            string result = ids[0].ToString();
            for (int i = 1; i < ids.Count; i++) result += $",{ids[i]}";
            return result;
        }

        internal string DescribeUndoAction(AuthoritativeActionAcceptance acceptance)
        {
            string actor = FormatUndoActor(acceptance.ActorPlayerId);
            switch (acceptance.Kind)
            {
                case AuthoritativeActionKind.MoveObject: return $"{actor} moved an object";
                case AuthoritativeActionKind.MoveContainer: return $"{actor} moved a Container";
                case AuthoritativeActionKind.RotateObject: return $"{actor} rotated an object";
                case AuthoritativeActionKind.FlipCard: return $"{actor} flipped a Card";
                case AuthoritativeActionKind.TransferCard: return $"{actor} transferred a Card";
                case AuthoritativeActionKind.TransferToken: return $"{actor} transferred a Token";
                case AuthoritativeActionKind.ShuffleDeck: return $"{actor} shuffled a Deck";
                case AuthoritativeActionKind.DrawCards: return $"{actor} drew Cards";
                case AuthoritativeActionKind.ReorderContainer: return $"{actor} reordered a Container";
                case AuthoritativeActionKind.MergeStacks: return $"{actor} merged Stacks";
                case AuthoritativeActionKind.SplitStack: return $"{actor} split a Stack";
                case AuthoritativeActionKind.CreateComponent: return $"{actor} created a Component";
                case AuthoritativeActionKind.DuplicateComponent: return $"{actor} duplicated a Component";
                case AuthoritativeActionKind.DeleteComponent: return $"{actor} deleted a Component";
                case AuthoritativeActionKind.PopulateDeck: return $"{actor} populated a Deck";
                case AuthoritativeActionKind.PhysicalObjectSettled: return $"{actor} moved a physical object";
                case AuthoritativeActionKind.TrapFloorReveal: return $"{actor} searched a Floor";
                case AuthoritativeActionKind.TrapFloorClaimKey: return $"{actor} claimed a Key";
                case AuthoritativeActionKind.TrapFloorAttemptEscape: return $"{actor} attempted Escape";
                case AuthoritativeActionKind.TrapFloorCollapse: return $"{actor} collapsed a Floor";
                case AuthoritativeActionKind.PurchaseActionOrAbility: return $"{actor} bought or took an Ability";
                case AuthoritativeActionKind.FlipConsole: return $"{actor} turned a Console over";
                case AuthoritativeActionKind.MoveBoard: return $"{actor} moved a Board";
                case AuthoritativeActionKind.ResizeBoardGrid: return $"{actor} changed a Board's grid";
                default:
                    return host.trapFloorTurnState == null
                        ? $"{actor} performed a table action"
                        : $"{actor} advanced the Trap Floor turn";
            }
        }

        internal string FormatUndoActor(PlayerId playerId)
        {
            if (host.activeSession != null)
            {
                IReadOnlyList<PlayerId> players = host.activeSession.Request.ActivePlayerIds;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] == playerId) return $"P{i + 1}";
                }
            }
            return "Player";
        }

        internal void RefreshUndoUi()
        {
            ActiveSessionUndoTransaction nextUndo = host.undoHistory.NextUndo;
            host.runtimeUi?.SetUndoState(
                CanUndoCurrentAction(),
                nextUndo == null ? "Undo" : $"Undo: {nextUndo.Description}");
            ActiveSessionUndoTransaction nextRedo = host.undoHistory.NextRedo;
            host.runtimeUi?.SetRedoState(
                CanRedoCurrentAction(),
                nextRedo == null ? "Redo" : $"Redo: {nextRedo.Description}");
        }
        // ---------- from TabletopComposition.cs ----------

        internal TabletopSessionBootstrapService sessionBootstrapService;
        internal PlayerId tableActionActorId;
        internal string templateCatalogError;
        internal SessionUndoSnapshot undoBatchBefore;
        internal AuthoritativeActionAcceptance? undoBatchLastAcceptance;
    }
}
