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

            // Owner, 2026-10-26: the Table menu's first row goes back to the main menu (the front end), where a
            // game is chosen (PLAY), the editors and the table are made (CREATE), or a Sandbox starts.
            if (host.GameSelectPanel.OpenFrontEnd(FrontEndPage.Home)) return;
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
                host.Cartridges.ForgetTemplates();
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
                    host.activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom ? null : host.ActiveCartridge.Game,
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
                host.ActiveCartridge.Seats.Count > 0 && host.activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom,
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
                host.ActiveCartridge.Seats.Count > 0 && host.activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom,
                host.handsOnTable,
                host.SetHandsOnTable,
                host.fullFeedback,
                host.SetFullFeedback);
            host.RefreshLookSettingsUi(); // V1/V5/V6/V7: Light, Colours, Quality, Room sound
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

        // PLACE-1: playerCount (0: keep the game's current count) is how many players a game's table is dealt for.
        internal void RequestTableReplacement(TabletopSessionSelection selection, int playerCount = 0)
        {
            // Owner, 2026-10-24: no "Replace current table?" question; the choice was already made in New table…
            TryReplaceTable(selection, playerCount);
        }

        // PLACE-1: deals the game's table for playerCount players (0: its current count). When the table cannot be
        // replaced the game goes back to the count it had, so the table in play keeps matching its setups.
        internal bool TryReplaceTable(TabletopSessionSelection selection, int playerCount)
        {
            if (playerCount <= 0
                || selection.Kind != TabletopSessionKind.GameTemplate
                || !host.Cartridges.TryGetTemplateSetup(selection.GameTemplateId, out IGameCartridge owner, out _)
                || owner.DealPlayerCount == playerCount)
            {
                return TryReplaceTable(selection);
            }

            int previousCount = owner.DealPlayerCount;
            owner.ChooseDealPlayerCount(playerCount);
            if (TryReplaceTable(selection)) return true;
            owner.ChooseDealPlayerCount(previousCount);
            try
            {
                RegisterGameTemplates();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Players] The game's setups could not be prepared again for {previousCount} players: {exception.Message}");
            }

            return false;
        }

        internal bool TryReplaceTable(TabletopSessionSelection selection)
        {
            if (sessionBootstrapService == null || host.sessionTemplateCatalog == null)
            {
                host.gameTemplatesPanelError = "Game Template loading is not configured.";
                host.runtimeUi?.SetGameTemplatesError(host.gameTemplatesPanelError);
                return false;
            }

            // MP-2: in online play a joined computer asks the host for a new table; the deal's seed is shared.
            if (host.HasOnline && !host.Online.AllowTableReplacement(selection)) return false;
            if (host.HasOnline) host.Online.PrepareTableBuild();

            if (selection.Kind == TabletopSessionKind.GameTemplate
                && host.Cartridges.TryGetTemplateSetup(selection.GameTemplateId, out _, out _))
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
            PlayerId requestingPlayerId = tableActionActorId;
            MatchId matchId = MatchId.New();
            if (host.HasOnline) host.Online.OverrideTablePlayers(selection, activePlayerIds, ref requestingPlayerId, ref matchId); // MP-2: the room's seats
            TabletopSessionBootstrapRequest request = new TabletopSessionBootstrapRequest(
                requestingPlayerId,
                selection,
                activePlayerIds,
                matchId);
            TabletopSessionBootstrapResult result = sessionBootstrapService.TryCreate(
                request,
                host.sessionTemplateCatalog);
            if (!result.Succeeded)
            {
                host.gameTemplatesPanelError = FormatSessionBuildFailure(result.Issues);
                host.runtimeUi?.SetGameTemplatesError(host.gameTemplatesPanelError);
                return false;
            }

            TemplateContext candidateContext = null;
            try
            {
                if (selection.Kind == TabletopSessionKind.GameTemplate)
                {
                    // CART-1b: the cartridge that registered the template makes the table's context for it.
                    if (!host.Cartridges.TryGetTemplateSetup(
                            selection.GameTemplateId,
                            out IGameCartridge candidateCartridge,
                            out object candidateSetup))
                    {
                        throw new InvalidOperationException(
                            "The selected Game Template belongs to no game on this table.");
                    }

                    candidateContext = candidateCartridge.CreateTemplateContext(
                        result.Session,
                        candidateSetup,
                        requestingPlayerId); // MP-2: this computer's seat (offline: the first, as before)
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
            TableMarkSet previousMarks = host.TableMarks;
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
                host.ShowTableMarks(TableMarkSet.Empty); // drawing tool: a new table starts clean
                host.InitializeActiveSession(false);
                host.gameTemplatesPanelError = null;
                host.gameTemplatesPanelVisible = false;
                ShowActiveSessionUi();
                host.ArrangeSeatsAtStart(); // CART-1c: on a bigger or other-shaped table the seats start at its edge
                host.GameSelectPanel.OnTableReplaced(selection); // L2-5: a deck of yours chosen for an Empty Table is placed now
                return true;
            }
            catch (Exception exception)
            {
                host.Shutdown();
                host.activeSession = previousSession;
                host.templateContext = previousContext;
                host.ShowTableMarks(previousMarks);
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
            if (!host.MayChangeWholeTable("Reset")) return; // MP-2
            host.EnsureInitialized();
            MatchState previousMatch = host.activeSession.CurrentMatch;
            SessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                // Reset returns the table to its start, Empty Table Hand switched on included (H-E).
                host.emptyTableHandEnabled = true;
                host.Shutdown(true);
                host.ShowTableMarks(TableMarkSet.Empty); // drawing tool: the table's start has no drawings
                host.InitializeActiveSession(true);
                ShowActiveSessionUi();
            }
            catch (Exception exception)
            {
                host.ShowTableMarks(previousSnapshot.TableMarks);
                host.RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Reset");
                Debug.LogError($"Reset Presentation rebuild failed: {exception.Message}", host);
                throw;
            }

            host.ArrangeSeatsAtStart(); // CART-1c: the seats start at the table's edge again
            host.ActiveCartridge.RefreshStatusUi();
        }

        public bool UndoLatestAction()
        {
            return UndoLatestAction(host.localPlayerId);
        }

        public bool UndoLatestAction(PlayerId requestingPlayerId)
        {
            if (host.IsOnlineTable && host.Online.IsClientTable && !host.Online.ApplyingTable)
            {
                host.Online.AskHostToUndo(false); // MP-5: a joined player's Undo is run by the host, for their own moves
                return false;
            }

            if (!host.MayChangeWholeTable("Undo")) return false; // MP-2: the host's Undo is everyone's
            if (!IsActiveSessionPlayer(requestingPlayerId)
                || !CanUndoCurrentAction()
                || !host.undoHistory.TryPeekUndo(
                    out SessionUndoSnapshot snapshot,
                    out ActiveSessionUndoTransaction transaction))
            {
                return false;
            }

            if (host.IsOnlineTable && !host.Online.MayUndo(transaction, false)) return false; // MP-5: your own moves only

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
            object restoredGameState = null;
            PendingControllerPurchaseState restoredPurchase = null;
            try
            {
                replacement = snapshot.Match.Restore(undoRevision);
                restoredGameState = snapshot.GameState?.Restore();
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
                host.pendingRestoredGameState = restoredGameState;
                host.pendingRestoredControllerPurchaseState = restoredPurchase;
                host.ShowTableMarks(snapshot.TableMarks); // drawing tool: the drawings as they were at that step
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
                host.ShowTableMarks(previousSnapshot.TableMarks);
                host.RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Undo");
                Debug.LogError($"Undo Presentation rebuild failed: {exception.Message}", host);
                throw;
            }
            finally
            {
                host.pendingRestoredGameState = null;
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
            if (host.IsOnlineTable && host.Online.IsClientTable && !host.Online.ApplyingTable)
            {
                host.Online.AskHostToUndo(true); // MP-5
                return false;
            }

            if (!host.MayChangeWholeTable("Redo")) return false; // MP-2
            if (!IsActiveSessionPlayer(requestingPlayerId)
                || !CanRedoCurrentAction()
                || !host.undoHistory.TryPeekRedo(
                    out SessionUndoSnapshot snapshot,
                    out ActiveSessionUndoTransaction transaction))
            {
                return false;
            }

            if (host.IsOnlineTable && !host.Online.MayUndo(transaction, true)) return false; // MP-5

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
            object restoredGameState = null;
            PendingControllerPurchaseState restoredPurchase = null;
            try
            {
                replacement = snapshot.Match.Restore(redoRevision);
                restoredGameState = snapshot.GameState?.Restore();
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
                host.pendingRestoredGameState = restoredGameState;
                host.pendingRestoredControllerPurchaseState = restoredPurchase;
                host.ShowTableMarks(snapshot.TableMarks); // drawing tool: the drawings as they were at that step
                host.InitializeActiveSession(false);
                host.undoHistory.CommitRedo();
                host.undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                ShowActiveSessionUi();
                host.ShowMessage($"Redid {transaction.Description}.");
                return true;
            }
            catch (Exception exception)
            {
                host.ShowTableMarks(previousSnapshot.TableMarks);
                host.RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Redo");
                Debug.LogError($"Redo Presentation rebuild failed: {exception.Message}", host);
                throw;
            }
            finally
            {
                host.pendingRestoredGameState = null;
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
            if (!host.IsInitialized || !host.Keys.UndoPressedThisFrame || TableKeys.TypingInUi)
            {
                return false;
            }

            UndoLatestAction();
            return true;
        }

        // POL-0: the keys come from the Input Actions asset (Redo: Ctrl+Y or Ctrl+Shift+Z).
        internal bool HandleRedoShortcut()
        {
            if (!host.IsInitialized || !host.Keys.RedoPressedThisFrame || TableKeys.TypingInUi)
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
            // MP-2: a move's follow-ups (tidying a stack, a die on a Console arrow, a card on a board space, the game's
            // own reaction) are made by the computer of the player who moved; the other computers get them as changes.
            bool madeHere = !host.IsOnlineTable
                || (!host.Replicated.Replaying && host.IsPlayerOnThisComputer(acceptance.ActorPlayerId));
            if (madeHere) host.pileCountCheckPending = true; // landing taps and small-stack tidy-up (TabletopComposition.Stacking.cs)

            if (acceptance.Kind == AuthoritativeActionKind.TransferCard
                || acceptance.Kind == AuthoritativeActionKind.DrawCards)
            {
                host.RefreshAcceptedCardFaces(); // a Hand turns arriving cards face up (H1b)
            }

            if (madeHere) host.ActiveCartridge.OnActionAccepted(acceptance); // CART-1b: the game follows its own moves (landing, guided moves)

            if (madeHere
                && (acceptance.Kind == AuthoritativeActionKind.MoveObject
                    || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled))
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
                host.ActiveCartridge.CaptureUndoState(), // CART-1b: the game's own part of the step
                host.pendingControllerPurchaseState,
                host.TableMarks); // drawing tool: the drawings at this step
        }

        /// <summary>
        /// Drawing tool: a line drawn, a label placed or drawings erased becomes one Undo step. The drawings are the
        /// table's own data (not part of the match), so the step is recorded here with the table's newest state. While
        /// another step is still being made (a die still rolling) nothing is recorded; that step takes the drawings
        /// with it when it lands. False when no step was recorded.
        /// </summary>
        internal bool RecordTableMarkStep(string description)
        {
            if (host.HasOnline) host.Online.MarkDirty(); // MP-2: drawings go to the other computers with the table
            if (!host.IsInitialized
                || host.rebuildingFromUndo
                || host.undoBatchActive
                || host.undoTransactionInProgress
                || host.undoTrackedMatch != host.matchState
                || host.undoHistory.CurrentStateIndex < 0)
            {
                return false;
            }

            PlayerId actor = host.localPlayerId.IsEmpty ? tableActionActorId : host.localPlayerId;
            if (actor.IsEmpty) return false;
            AuthoritativeActionAcceptance acceptance = new AuthoritativeActionAcceptance(
                Math.Max(1L, host.matchState.Revision),
                CommandId.New(),
                actor,
                AuthoritativeActionKind.TableMark,
                AuthoritativeActionRecordMode.Transaction);
            host.undoHistory.RecordAccepted(
                host.undoHistory.CurrentState,
                CaptureUndoSnapshot(),
                acceptance,
                $"{FormatUndoActor(actor)} {description}");
            RefreshUndoUi();
            host.RulesCardPanel.RaiseHint(HintTriggers.UndoAvailable);
            return true;
        }

        /// <summary>Drawing tool: drawings put on the table without a step of their own (a saved game laid out).</summary>
        internal void FoldTableMarksIntoCurrentStep()
        {
            if (!host.IsInitialized
                || host.undoBatchActive
                || host.undoTransactionInProgress
                || host.undoTrackedMatch != host.matchState
                || host.undoHistory.CurrentStateIndex < 0)
            {
                return;
            }

            host.undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
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
                case AuthoritativeActionKind.PurchaseActionOrAbility: return $"{actor} bought or took an Ability";
                case AuthoritativeActionKind.FlipConsole: return $"{actor} turned a Console over";
                case AuthoritativeActionKind.MoveBoard: return $"{actor} moved a Board";
                case AuthoritativeActionKind.ResizeBoardGrid: return $"{actor} changed a Board's grid";
                case AuthoritativeActionKind.TableMark: return $"{actor} drew on the table";
                default:
                    // CART-1b: a game names its own actions; anything else is a table action.
                    return host.ActiveCartridge.DescribeAction(acceptance, actor) ?? $"{actor} performed a table action";
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
