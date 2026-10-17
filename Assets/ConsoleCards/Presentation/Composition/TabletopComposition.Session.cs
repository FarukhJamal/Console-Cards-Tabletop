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

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>Game templates, session UI and replacing the table (New table, Empty Table). (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
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
            if (!IsInitialized)
            {
                return;
            }

            if (gameTemplatesPanelVisible)
            {
                CloseGameTemplatesPanel();
                return;
            }

            gameTemplatesPanelVisible = true;
            RefreshGameTemplatesPanelUi();
        }

        // Table menu > New table… (UI-1): opens the game selection menu (L2-4); the older game list only when that
        // menu's prefab is missing.
        private void OpenGameTemplatesPanel()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (OpenGameSelect()) return;
            gameTemplatesPanelVisible = true;
            RefreshGameTemplatesPanelUi();
        }

        private void CloseGameTemplatesPanel()
        {
            gameTemplatesPanelVisible = false;
            runtimeUi?.HideGameTemplatesPanel();
        }

        private void PrepareTemplateCatalog()
        {
            sessionBootstrapService = new TabletopSessionBootstrapService();
            tableActionActorId = PlayerId.New();
            authoritativeRandomValueSource = new SystemRandomValueSource();
            templateCatalogError = null;
            gameTemplatesPanelError = null;
            try
            {
                RegisterFreshTrapFloorTemplates();
            }
            catch (Exception exception)
            {
                availableTrapFloorTemplates.Clear();
                sessionTemplateCatalog = new GameTemplateCatalog(Array.Empty<GameTemplateRegistration>());
                templateCatalogError = $"Trap Floor is unavailable: {exception.Message}";
            }
        }

        // With a deck of yours chosen (L2-5) that cannot set up the game, the game's own deck is used instead: a
        // choice never stops play.
        private void RegisterFreshTrapFloorTemplates()
        {
            try
            {
                RegisterTrapFloorTemplates(true);
            }
            catch (Exception exception) when (!string.IsNullOrEmpty(ChosenTrapFloorDeckId()))
            {
                string message = $"Your deck cannot set up Trap Floor ({exception.Message}); the game's own deck is used.";
                Debug.LogWarning("[Your decks] " + message, this);
                ShowMessage(message);
                RegisterTrapFloorTemplates(false);
            }
        }

        private void RegisterTrapFloorTemplates(bool useChosenDeck)
        {
            RequireReference(trapFloorGameDefinition, nameof(trapFloorGameDefinition));
            if (authoritativeRandomValueSource == null)
            {
                authoritativeRandomValueSource = new SystemRandomValueSource();
            }

            // Rules drive play (doc 23, R2a): the rule set in play (the default or agreed house rules, R3) feeds the
            // game data the template uses.
            EnsureHouseRulesLoaded();
            RuleSetDefinition rules = ActiveRuleSet;
            // Your cards (doc 27 L2-1) come first: changed default cards and new cards; house-rule costs win on top.
            GameContent content = BuildTrapFloorContent(useChosenDeck);
            GameDefinitionData gameDefinition;
            try
            {
                gameDefinition = content.ToData(rules);
            }
            catch
            {
                content.Destroy();
                throw;
            }

            GameContent previousContent = trapFloorContent;
            trapFloorContent = content;
            previousContent?.Destroy();
            if (gameDefinition.Modes.Count == 0)
                throw new InvalidOperationException("Trap Floor requires at least one authored Mode.");

            availableTrapFloorTemplates.Clear();
            List<GameTemplateRegistration> registrations = new List<GameTemplateRegistration>(gameDefinition.Modes.Count);
            for (int i = 0; i < gameDefinition.Modes.Count; i++)
            {
                TrapFloorTemplateDefinition template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                    authoritativeRandomValueSource,
                    gameDefinition,
                    gameDefinition.Modes[i].StableId,
                    GetConsoleLayout());
                availableTrapFloorTemplates.Add(template.Template.Id, template);
                registrations.Add(new GameTemplateRegistration(template.Template, template.ContentCatalog));
            }

            sessionTemplateCatalog = new GameTemplateCatalog(registrations);
            templatesRuleSet = rules;
        }

        private void InitializeRuntimeUi()
        {
            RequireReference(runtimeUi, nameof(runtimeUi));
            if (!runtimeUi.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "TabletopComposition requires runtimeUi to reference the scene-authored UI controller.");
            }

            runtimeUi.ValidateReferences();
        }

        private void RefreshGameTemplatesPanelUi()
        {
            if (runtimeUi == null || !gameTemplatesPanelVisible)
            {
                return;
            }

            List<GameTemplateOption> options = new List<GameTemplateOption>();
            if (sessionTemplateCatalog != null)
            {
                foreach (GameTemplateRegistration registration in sessionTemplateCatalog.Registrations.Values)
                {
                    GameTemplateId templateId = registration.Template.Id;
                    options.Add(new GameTemplateOption(
                        registration.Template.DisplayName,
                        () => RequestTableReplacement(
                            TabletopSessionSelection.FromGameTemplate(templateId))));
                }
            }

            runtimeUi.ShowGameTemplatesPanel(
                () => RequestTableReplacement(TabletopSessionSelection.EmptyCustom),
                options,
                gameTemplatesPanelError ?? templateCatalogError);
        }

        private void ShowActiveSessionUi()
        {
            if (runtimeUi == null || activeSession == null)
            {
                return;
            }

            // Session bar chip (UI-1): a game shows its name and mode ("Trap Floor — Easy"); Empty Table its name.
            bool isGameSession = activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom;
            if (ruleProposalOpen) CloseRuleProposal(null); // a new table has new players
            string sessionTitle = "EMPTY TABLE";
            string sessionSubtitle = string.Empty;
            if (isGameSession)
            {
                string displayName = activeSession.Template.DisplayName;
                int separator = displayName.IndexOf(" — ", StringComparison.Ordinal);
                sessionTitle = (separator < 0 ? displayName : displayName.Substring(0, separator)).ToUpperInvariant();
                sessionSubtitle = separator < 0 ? string.Empty : displayName.Substring(separator + 3);
            }
            else if (activePlayerGame != null)
            {
                // Doc 29: a game you made runs on an Empty Table; the chip shows its name.
                sessionTitle = (activePlayerGame.name ?? "Your game").ToUpperInvariant();
                sessionSubtitle = "Your game";
            }

            runtimeUi.ShowActiveSession(
                sessionTitle,
                sessionSubtitle,
                isGameSession,
                HandleUndoButtonPressed,
                HandleRedoButtonPressed,
                OpenGameTemplatesPanel,
                ResetTable,
                ClearTable,
                CurrentStatusText(),
                new ComponentToolboxBindings(
                    componentLibrary,
                    activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom ? null : trapFloorGameDefinition,
                    PlaceCatalogEntry,
                    activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom && !emptyTableHandContainerId.IsEmpty,
                    emptyTableHandEnabled,
                    SetEmptyTableHandEnabled));
            runtimeUi.ShowTableSettings(
                BuildRulesCardModel(),
                rulesCardEnabled,
                SetRulesCardEnabled,
                hintsEnabled,
                SetHintsEnabled);
            runtimeUi.ShowTipsSetting(tipsEnabled, SetTipsEnabled);
            runtimeUi.ShowPlaySettings(
                trapFloorTemplate != null && activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom,
                $"Seat: {PlayerLabel(localPlayerId)}",
                NextScreenSeat,
                hotSeatMode,
                SetHotSeatMode);
            // Card art, the card editor and the deck editor live in the game selection menu (L2-4), not on the table.
            runtimeUi.ShowCardArtEntry(null);
            RefreshCardEditorEntry(); // L2-2: the game's cards and yours, on every table
            RefreshDeckEditorEntry(); // L2-3: the game's deck and yours, on every table
            RefreshProfileEntry(); // doc 28, P2: who is playing, on every table
            RefreshTableShapeEntry(); // T1: the table's shape, on every table
            RefreshSaveGameEntry(); // doc 29: Save table as a game, on an Empty Table
            runtimeUi.ShowViewSettings(
                trapFloorTemplate != null && activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom,
                handsOnTable,
                SetHandsOnTable,
                fullFeedback,
                SetFullFeedback);
            RefreshUndoUi();
            RefreshTrapFloorStatusUi();
            RefreshGameHints();
            RefreshShopEntry();
            RaiseHint(HintTriggers.TableReady, isGameSession ? "game" : "empty");
        }

        private void HandleUndoButtonPressed()
        {
            UndoLatestAction();
        }

        private void HandleRedoButtonPressed()
        {
            RedoLatestAction();
        }

        private void RefreshRuntimeStatusUi()
        {
            if (runtimeUi != null && IsInitialized)
            {
                runtimeUi.SetStatusMessage(CurrentStatusText());
            }
        }

        private void RequestTableReplacement(TabletopSessionSelection selection)
        {
            if (!IsInitialized || !HasCurrentTableContent())
            {
                TryReplaceTable(selection);
                return;
            }

            string replacementName = ReplacementDisplayName(selection);
            runtimeUi.ShowContextMenu(
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
                        CloseContextMenu),
                },
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private bool TryReplaceTable(TabletopSessionSelection selection)
        {
            if (sessionBootstrapService == null || sessionTemplateCatalog == null)
            {
                gameTemplatesPanelError = "Game Template loading is not configured.";
                runtimeUi?.SetGameTemplatesError(gameTemplatesPanelError);
                return false;
            }

            if (selection.Kind == TabletopSessionKind.GameTemplate
                && availableTrapFloorTemplates.ContainsKey(selection.GameTemplateId))
            {
                try
                {
                    RegisterFreshTrapFloorTemplates();
                }
                catch (Exception exception)
                {
                    gameTemplatesPanelError = $"Trap Floor setup could not be prepared: {exception.Message}";
                    runtimeUi?.SetGameTemplatesError(gameTemplatesPanelError);
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
                sessionTemplateCatalog);
            if (!result.Succeeded)
            {
                gameTemplatesPanelError = FormatSessionBuildFailure(result.Issues);
                runtimeUi?.SetGameTemplatesError(gameTemplatesPanelError);
                return false;
            }

            TrapFloorTemplateDefinition candidateTemplate = null;
            TemplateContext candidateContext = null;
            try
            {
                if (selection.Kind == TabletopSessionKind.GameTemplate)
                {
                    if (!availableTrapFloorTemplates.TryGetValue(
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
                gameTemplatesPanelError = exception.Message;
                runtimeUi?.SetGameTemplatesError(gameTemplatesPanelError);
                Debug.LogError($"Table replacement validation failed: {exception.Message}", this);
                return false;
            }

            TabletopSession previousSession = activeSession;
            TemplateContext previousContext = templateContext;
            TrapFloorTemplateDefinition previousTemplate = trapFloorTemplate;
            bool hadActivePresentation = IsInitialized;

            try
            {
                if (hadActivePresentation)
                {
                    Shutdown();
                }

                activeSession = result.Session;
                emptyTableHandEnabled = true;
                templateContext = candidateContext;
                trapFloorTemplate = candidateTemplate;
                InitializeActiveSession(false);
                gameTemplatesPanelError = null;
                gameTemplatesPanelVisible = false;
                ShowActiveSessionUi();
                OnTableReplaced(selection); // L2-5: a deck of yours chosen for an Empty Table is placed now
                return true;
            }
            catch (Exception exception)
            {
                Shutdown();
                activeSession = previousSession;
                templateContext = previousContext;
                trapFloorTemplate = previousTemplate;
                gameTemplatesPanelError = $"Table replacement failed: {exception.Message}";
                Debug.LogError(gameTemplatesPanelError, this);

                if (hadActivePresentation && previousSession != null)
                {
                    try
                    {
                        InitializeActiveSession(false);
                        ShowActiveSessionUi();
                        gameTemplatesPanelVisible = true;
                        RefreshGameTemplatesPanelUi();
                    }
                    catch (Exception rollbackException)
                    {
                        Debug.LogError(
                            $"Previous table presentation could not be restored: {rollbackException.Message}",
                            this);
                    }
                }

                return false;
            }
        }

        private bool HasCurrentTableContent()
        {
            return matchState != null
                && (matchState.ObjectCount > 0
                    || matchState.Containers.Count > 0
                    || matchState.ContainerPlacements.Count > 0
                    || matchState.Seats.Count > 0
                    || matchState.PlacedConsoles.Count > 0
                    || matchState.PlayAreas.Count > 0);
        }

        private string ReplacementDisplayName(TabletopSessionSelection selection)
        {
            if (selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                return "a fresh Empty Table";
            }

            return sessionTemplateCatalog.TryGet(
                    selection.GameTemplateId,
                    out GameTemplateRegistration registration)
                ? registration.Template.DisplayName
                : "the selected Game Template";
        }

        private List<PlayerId> CreateDefaultActivePlayers(TabletopSessionSelection selection)
        {
            int playerCount = 1;
            if (selection.Kind == TabletopSessionKind.GameTemplate)
            {
                if (!sessionTemplateCatalog.TryGet(selection.GameTemplateId, out GameTemplateRegistration registration))
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

        private static string FormatSessionBuildFailure(
            IReadOnlyList<TabletopSessionBootstrapIssue> issues)
        {
            string message = "Authoritative session construction failed.";
            for (int i = 0; i < issues.Count; i++)
            {
                message += $" {issues[i]}";
            }

            return message;
        }
    }
}
