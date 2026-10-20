using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Authority;
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
    /// The Trap Floor cartridge's side of the contract added in CART-1b (doc 30 §A4): its templates and seat
    /// context, building and clearing its state on the table, its commands, its seats and turns, its floor tiles
    /// (look, menu, inspect text) and its Undo state. The code moved here from the table's own files.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        // The seats of the table in play, made once when the table is built from a template (never per frame).
        private readonly List<CartridgeSeat> seats = new List<CartridgeSeat>(4);

        // ---------- templates and seat context ----------

        public bool TryGetTemplateSetup(GameTemplateId templateId, out object setup)
        {
            if (host.availableTrapFloorTemplates.TryGetValue(templateId, out TrapFloorTemplateDefinition template))
            {
                setup = template;
                return true;
            }

            setup = null;
            return false;
        }

        // Each template is one mode ("Trap Floor — Easy").
        public void ListTemplates(List<GameTemplateId> templates, List<string> modeNames)
        {
            foreach (KeyValuePair<GameTemplateId, TrapFloorTemplateDefinition> pair in host.availableTrapFloorTemplates)
            {
                templates.Add(pair.Key);
                string mode = pair.Value.ActiveMode != null ? pair.Value.ActiveMode.DisplayName : string.Empty;
                modeNames.Add(string.IsNullOrWhiteSpace(mode) ? pair.Value.Template.DisplayName : mode);
            }
        }

        public void ForgetTemplates() => host.availableTrapFloorTemplates.Clear();

        public TemplateContext CreateTemplateContext(TabletopSession session, object setup, PlayerId requestingPlayerId)
        {
            if (!(setup is TrapFloorTemplateDefinition template))
            {
                throw new ArgumentException("The setup is not a Trap Floor template.", nameof(setup));
            }

            return host.CreateTrapFloorTemplateContext(session, template, requestingPlayerId);
        }

        // A changed setting or a deck of yours must still fill the floor: the game is set up once, off the table.
        public void CheckSetUp(GameDefinitionData data, string modeStableId)
        {
            TrapFloorTemplateFactory.CreateStandardFourPlayer(
                new SystemRandomValueSource(), data, modeStableId, host.GetConsoleLayout());
        }

        // Sets the game up once, off the table, with the chosen deck. Null when it works, else what went wrong.
        public string TrialChosenDeck(GameTemplateId templateId)
        {
            if (!host.availableTrapFloorTemplates.TryGetValue(templateId, out TrapFloorTemplateDefinition template)) return null;
            host.RulesCardPanel.EnsureHouseRulesLoaded();
            GameContent content = BuildTrapFloorContent(true);
            try
            {
                GameDefinitionData data = content.ToData(host.RulesCardPanel.ActiveRuleSet);
                TrapFloorTemplateFactory.CreateStandardFourPlayer(
                    new SystemRandomValueSource(), data, template.ActiveMode.StableId, host.GetConsoleLayout());
                return null;
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
            finally
            {
                content.Destroy();
            }
        }

        // ---------- building and clearing the table ----------

        public void UseTemplate(TemplateContext context)
        {
            host.trapFloorTemplate = context.GameSetup as TrapFloorTemplateDefinition
                ?? throw new InvalidOperationException("The table's setup is not a Trap Floor template.");
            seats.Clear();
            IReadOnlyList<TrapFloorPlayerSetupDefinition> players = host.trapFloorTemplate.Players;
            for (int i = 0; i < players.Count; i++)
            {
                seats.Add(SeatFor(players[i]));
            }
        }

        // A player's seat as the table sees it; its Console's home is where the template puts it (CART-1c).
        private CartridgeSeat SeatFor(TrapFloorPlayerSetupDefinition player)
        {
            TabletopPose home = host.trapFloorTemplate != null
                && host.trapFloorTemplate.PlayerLayout.TryGetSeat(player.LayoutSeatIndex, out PlayerSeatLayoutEntry layoutSeat)
                    ? TrapFloorTemplateFactory.GetConsolePose(layoutSeat)
                    : TabletopPose.Default;
            return new CartridgeSeat(
                player.LayoutSeatIndex,
                player.SeatId,
                player.HandContainerId,
                player.ControllerDeckId,
                player.ActionAbilityAreaContainerId,
                player.PawnId,
                home);
        }

        public void ValidateConfiguration() =>
            RequireReference(host.trapFloorGameDefinition, nameof(host.trapFloorGameDefinition));

        public void BuildRules() => host.BuildTrapFloorRevealRuntime();

        public void BuildServices() => host.BuildFloorfallRuntime();

        public void OnObjectViewsBound() => HoldWaitingLandingTileView();

        // Trap Floor's commands at the table's door, and the views that follow the ones that grant a card.
        public void AttachToAuthority(LocalTableAuthority authority)
        {
            authority.Register<TrapFloorBeginCollapseCommand, TrapFloorCollapseResult>(
                command => host.trapFloorCollapseUseCase.Begin(host.matchState, command), result => result.Succeeded);
            authority.Register<TrapFloorResolveCollapseCommand, TrapFloorCollapseResult>(
                command => host.trapFloorCollapseUseCase.ResolveSettled(host.matchState, command), result => result.Succeeded);
            authority.Register<TrapFloorRevealFloorCommand, TrapFloorRevealFloorResult>(
                command => host.trapFloorRevealFloorUseCase.Execute(host.matchState, command), result => result.Succeeded);
            authority.Register<TrapFloorClaimKeyCommand, TrapFloorObjectiveResult>(
                command => host.trapFloorObjectiveUseCase.ClaimKey(host.matchState, command), result => result.Succeeded);
            authority.Register<TrapFloorAttemptEscapeCommand, TrapFloorObjectiveResult>(
                command => host.trapFloorObjectiveUseCase.AttemptEscape(host.matchState, command), result => result.Succeeded);
            authority.Register<TrapFloorTakePrizeCommand, TrapFloorTakePrizeResult>(
                command => host.trapFloorTakePrizeUseCase.Execute(host.matchState, command), result => result.Succeeded);
            authority.Register<TrapFloorBuyFromShopCommand, TrapFloorBuyFromShopResult>(
                command => host.trapFloorShopUseCase.Execute(host.matchState, command), result => result.Succeeded);

            authority.Observe<TrapFloorBuyFromShopCommand, TrapFloorBuyFromShopResult>(OnShopCardBought);
            authority.Observe<TrapFloorTakePrizeCommand, TrapFloorTakePrizeResult>(OnPrizeTaken);
        }

        private void OnShopCardBought(TrapFloorBuyFromShopCommand command, TrapFloorBuyFromShopResult result)
        {
            if (!host.ViewSync.Ready) return;
            PresentGrantedCard(result.GrantedCardId, result.Card.DisplayName, result.DestinationContainerId);
        }

        private void OnPrizeTaken(TrapFloorTakePrizeCommand command, TrapFloorTakePrizeResult result)
        {
            if (!host.ViewSync.Ready) return;
            PresentGrantedCard(
                result.GrantedCardId,
                result.FloorCard.Content.DisplayName,
                result.DestinationContainerId);
        }

        public void OnTableShutdown() => FinishPawnHops();

        // Before the views go: landing tiles (a new or reset table only), physical dice in progress, floor markers.
        public void ClearTableMarkers()
        {
            if (!host.rebuildingFromUndo)
            {
                host.landedFloorCards.Clear();
                host.waitingLandingFloorCardId = TabletopObjectId.Empty;
                host.waitingLandingActor = PlayerId.Empty;
            }

            host.physicalFloorfallPending = false;
            host.physicalFloorfallActor = PlayerId.Empty;
            host.physicalFloorCollapsePending = false;
            host.physicalFloorCollapseActor = PlayerId.Empty;
            host.physicalBlindDirectionPending = false;
            host.physicalBlindDirectionActor = PlayerId.Empty;
            host.physicalBlindDirectionDieId = TabletopObjectId.Empty;
            host.floorfallTargetPresenter?.Clear();
            host.abilityTargetPresenter?.Clear();
            host.floorfallState?.Clear();
        }

        public void ClearTableState()
        {
            host.trapFloorTemplate = null;
            seats.Clear();
            host.trapFloorActivityFeed?.Clear();
            host.trapFloorActivityFeed = null;
            host.trapFloorRevealFloorUseCase = null;
            host.trapFloorObjectiveState?.Clear();
            host.trapFloorObjectiveState = null;
            host.trapFloorObjectiveUseCase = null;
            host.trapFloorCollapseState?.Clear();
            host.trapFloorCollapseState = null;
            host.trapFloorCollapseUseCase = null;
            host.trapFloorTurnState = null;
            host.trapFloorTurnService = null;
            host.trapFloorAbilityResolutionState = null;
            host.trapFloorAbilityResolutionService = null;
            host.activeFloorRevealActivity = null;
            host.floorfallState = null;
            host.floorfallService = null;
            host.floorfallTargetPresenter = null;
            host.abilityTargetPresenter = null;
            host.floormasterLifecycleState = null;
            host.floormasterLifecycleService = null;
            host.trapFloorRoundState = null;
            host.trapFloorRoundOrchestrationService = null;
        }

        // The rules state of one Undo step: all five parts, or none (a table without Trap Floor's state).
        public ICartridgeUndoState CaptureUndoState()
        {
            if (host.trapFloorActivityFeed == null
                && host.trapFloorObjectiveState == null
                && host.trapFloorCollapseState == null
                && host.trapFloorTurnState == null
                && host.trapFloorAbilityResolutionState == null)
            {
                return null;
            }

            if (host.trapFloorActivityFeed == null
                || host.trapFloorObjectiveState == null
                || host.trapFloorCollapseState == null
                || host.trapFloorTurnState == null
                || host.trapFloorAbilityResolutionState == null)
                throw new InvalidOperationException("Trap Floor Undo capture requires its complete Match-scoped state.");
            return new TrapFloorUndoState(TrapFloorSessionStateSnapshot.Capture(
                host.trapFloorActivityFeed,
                host.trapFloorObjectiveState,
                host.trapFloorCollapseState,
                host.trapFloorTurnState,
                host.trapFloorAbilityResolutionState));
        }

        private sealed class TrapFloorUndoState : ICartridgeUndoState
        {
            private readonly TrapFloorSessionStateSnapshot snapshot;

            internal TrapFloorUndoState(TrapFloorSessionStateSnapshot snapshot)
            {
                this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            }

            public object Restore() => snapshot.Restore();
        }

        // ---------- seats and turns ----------

        public IReadOnlyList<CartridgeSeat> Seats => seats;

        public PlayerId TurnPlayerId => host.trapFloorTurnState != null ? host.trapFloorTurnState.ActivePlayerId : PlayerId.Empty;

        // A player's turn (not a Floor turn, and the floor has not failed).
        public bool IsPlayerTurnOpen =>
            host.trapFloorTurnState != null
            && !host.trapFloorTurnState.IsCurrentFloorFailed
            && host.trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn;

        public int CurrentRound => host.trapFloorTurnState != null ? host.trapFloorTurnState.CurrentRound : 0;

        public string ActiveModeStableId => host.trapFloorTemplate != null ? host.trapFloorTemplate.ActiveMode.StableId : string.Empty;

        // Trap Floor's seats go to the table's edge on any shape (owner, 2026-10-20: CART-1c); the floor stays in the middle.
        public SeatPlacement SeatPlacement => SeatPlacement.AlongEdge;

        public CartridgeSeat GetAssistedSeat() => SeatFor(GetAssistedTrapFloorPlayerSetup());

        public ControllerInputHandDrawResult DrawControllerHandForTurn(CommandContext context) =>
            host.trapFloorTurnService.DrawForCurrentPlayer(host.matchState, context);

        public ControllerConfigurationData ControllerConfiguration =>
            host.trapFloorTemplate != null ? host.trapFloorTemplate.GameDefinition.ControllerConfiguration : null;

        // ---------- floor tiles ----------

        public bool IsFixedBoardPiece(TabletopObjectId objectId) =>
            host.trapFloorTemplate != null && host.trapFloorTemplate.IsFloorCard(objectId);

        public bool TryGetBoardGrid(out GridDefinitionData grid)
        {
            grid = host.trapFloorTemplate != null ? host.trapFloorTemplate.GameDefinition.Grid : null;
            return grid != null;
        }

        // Floor tiles light as move targets (Rush, Dodge, Spring, Crane).
        public void OnBoardPieceViewCreated(TabletopObjectId objectId, CardVisualReferences references)
        {
            host.abilityTargetPresenter.Register(
                objectId,
                references.FaceUpRenderer,
                references.FaceDownRenderer);
        }

        // A floor tile: a MYSTERY back with the round's board back, its content's colour and art face up, and a hole
        // once it collapsed.
        public void StyleBoardPiece(CardInstanceState card, ref CardLook look)
        {
            look.BackLabel = "MYSTERY";
            if (host.trapFloorTemplate != null
                && host.trapFloorTemplate.TryGetFloorCardState(
                    host.matchState,
                    card.BaseState.Id,
                    out TrapFloorFloorCardState floorCard))
            {
                if (host.trapFloorCollapseState != null
                    && host.trapFloorCollapseState.IsCollapsed(card.BaseState.Id))
                {
                    look.FrontColor = new Color(0.025f, 0.03f, 0.04f);
                    look.BackColor = look.FrontColor;
                    look.FrontLabel = $"HOLE\n{floorCard.Coordinate}";
                    look.BackLabel = look.FrontLabel;
                    look.BackLabelIsPlaceholder = false;
                    look.FrontArtwork = null;
                    look.BackArtwork = null;
                }
                else
                {
                    look.FrontColor = TrapFloorContentColor(floorCard.Content.Category);
                    Texture2D contentFront = host.TryGetAuthoredCardDefinition(floorCard.Content.Id, out CardDefinition contentDefinition)
                        ? host.FrontArt(contentDefinition)
                        : null;
                    if (contentFront != null)
                    {
                        look.FrontArtwork = contentFront;
                    }

                    look.BackArtwork = host.CurrentBoardCardBack();
                    if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                        && host.trapFloorObjectiveState != null)
                    {
                        bool isClaimed = host.trapFloorObjectiveState.TryGetClaim(card.BaseState.Id, out _);
                        look.FrontLabel = isClaimed
                            ? $"KEY — CLAIMED\n{floorCard.Content.DisplayName}"
                            : $"KEY — UNCLAIMED\n{floorCard.Content.DisplayName}";
                    }
                    else
                    {
                        look.FrontLabel = $"{floorCard.Content.Category.ToString().ToUpperInvariant()}\n"
                            + floorCard.Content.DisplayName;
                    }
                }
            }
        }

        public bool TryGetCardMenuMode(TabletopObjectId cardId, out ContextMenuMode mode)
        {
            if (host.floormasterLifecycleState?.PendingCard != null
                && host.floormasterLifecycleState.PendingCard.CardId == cardId)
            {
                mode = ContextMenuMode.GamePendingCard;
                return true;
            }

            if (IsFixedBoardPiece(cardId))
            {
                mode = ContextMenuMode.BoardPiece;
                return true;
            }

            mode = ContextMenuMode.None;
            return false;
        }

        // A floor tile's inspect text: its content, the Trap or Key state, and a Resolve Trap or Claim Key action.
        public bool TryBuildInspectModel(TabletopObjectId targetCardId, CardInstanceState card, out CardInspectModel model)
        {
            if (host.trapFloorTemplate != null
                && host.trapFloorTemplate.TryGetFloorCardState(
                    host.matchState,
                    targetCardId,
                    out TrapFloorFloorCardState floorCard))
            {
                if (host.trapFloorCollapseState != null
                    && host.trapFloorCollapseState.IsCollapsed(targetCardId))
                {
                    Color holeColor = new Color(0.025f, 0.03f, 0.04f);
                    model = new CardInspectModel(
                        $"Collapsed Floor {floorCard.Coordinate} | {targetCardId}",
                        card.Face,
                        new CardInspectSideModel(
                            "HOLE",
                            $"Floor {floorCard.Coordinate} is permanently collapsed and unusable.",
                            null,
                            holeColor,
                            Color.white),
                        new CardInspectSideModel(
                            "HOLE",
                            $"Floor {floorCard.Coordinate} is permanently collapsed and unusable.",
                            null,
                            holeColor,
                            Color.white),
                        false);
                    return true;
                }

                TrapFloorActivityEntry revealActivity = host.activeFloorRevealActivity != null
                    && host.activeFloorRevealActivity.FloorCardId == targetCardId
                        ? host.activeFloorRevealActivity
                        : null;
                string revealContext = revealActivity == null
                    ? string.Empty
                    : $"Revealed by: {FormatPlayerName(revealActivity.ActorPlayerId)}\n"
                        + $"Floor coordinate: {floorCard.Coordinate}\n";
                string objectiveContext = string.Empty;
                string frontTitle = floorCard.Content.DisplayName;
                string inputCostContext = host.TryGetAuthoredCardDefinition(
                        card.BaseState.DefinitionId,
                        out CardDefinition floorCardDefinition)
                    ? FormatInputCost(floorCardDefinition)
                    : string.Empty;
                PopupActionOption? primaryAction = null;
                if (floorCard.Content.Category == TrapFloorFloorContentCategory.Trap)
                {
                    bool safelyRevealed = WasTrapSafelyRevealed(targetCardId);
                    bool neutralized = IsTrapFloorTrapNeutralized(targetCardId);
                    objectiveContext = safelyRevealed
                        ? "CAREFUL SEARCH — Trap did not trigger during this reveal.\nThe Floor remains a Trap.\n"
                        : neutralized
                        ? "TRAP NEUTRALIZED\nThis Floor is safe from assisted Trap consequences.\n"
                        : TrapFloorCartridge.TrapFloorTrapEffectDescription(floorCard.Content) + "\n";
                    if (safelyRevealed)
                    {
                        frontTitle = $"{floorCard.Content.DisplayName} — REVEALED SAFELY";
                    }
                    else if (neutralized)
                    {
                        frontTitle = $"{floorCard.Content.DisplayName} — SAFE";
                    }
                    else if (IsTrapFloorTrapResolved(targetCardId))
                    {
                        frontTitle = $"{floorCard.Content.DisplayName} — RESOLVED";
                        objectiveContext = "TRAP RESOLVED\nIts assisted consequence has already been applied or prevented.\n";
                    }
                    else if (floorCard.Content.HasSupportedAssistedTrapEffect)
                    {
                        primaryAction = new PopupActionOption(
                            "Resolve Trap",
                            CanResolveTrapFloorTrap(targetCardId),
                            () => ResolveTrapFloorTrap(targetCardId));
                    }
                }
                else if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                    && host.trapFloorObjectiveState != null)
                {
                    if (host.trapFloorObjectiveState.TryGetClaim(
                            targetCardId,
                            out TrapFloorCollectedKeyState claim))
                    {
                        frontTitle = "KEY — CLAIMED";
                        objectiveContext = $"Key status: CLAIMED\n"
                            + $"Claimed by: {FormatPlayerName(claim.ClaimedByPlayerId)}\n";
                    }
                    else
                    {
                        frontTitle = "KEY — UNCLAIMED";
                        objectiveContext = "Key status: UNCLAIMED\n"
                            + "Claim this Key before attempting to escape.\n";
                        primaryAction = new PopupActionOption(
                            "Claim Key",
                            !host.trapFloorObjectiveState.IsWon,
                            () => ClaimTrapFloorKey(targetCardId));
                    }
                }

                string frontBody = $"{revealContext}{objectiveContext}"
                    + $"Category: {floorCard.Content.Category}\n"
                    + $"Content: {floorCard.Content.DisplayName}\n\n"
                    + floorCard.Content.DisplayText
                    + inputCostContext;
                model = new CardInspectModel(
                    $"Floor {floorCard.Coordinate} | {targetCardId}",
                    card.Face,
                    new CardInspectSideModel(
                        frontTitle,
                        frontBody,
                        null,
                        TrapFloorContentColor(floorCard.Content.Category),
                        new Color(0.04f, 0.06f, 0.08f)),
                    new CardInspectSideModel(
                        "MYSTERY",
                        $"Floor {floorCard.Coordinate}\nContent stays hidden until a pawn lands on it.",
                        null,
                        new Color(0.10f, 0.19f, 0.42f),
                        Color.white),
                    false,
                    primaryAction);
                return true;
            }

            model = null;
            return false;
        }

        public void ForgetInspectContext() => host.activeFloorRevealActivity = null;

        string IGameCartridge.FormatInputCost(CardDefinition definition) => FormatInputCost(definition);

        // ---------- actions and feedback ----------

        public void TickFeedback()
        {
            host.AnnounceTrapFloorFeedback();
            host.RefreshTrapFloorBoardFeedback();
        }

        public void OnActionAccepted(AuthoritativeActionAcceptance acceptance)
        {
            if (acceptance.Kind != AuthoritativeActionKind.MoveObject
                && acceptance.Kind != AuthoritativeActionKind.PhysicalObjectSettled)
            {
                return;
            }

            if (IsGuidedMoveShown())
            {
                // R4: a move made while an Ability or a Trap shows where to go is not a regular move.
                EnsureMoveTurn();
                guidedMoveThisTurn = true;
            }

            if (host.trapFloorAbilityResolutionService != null
                && host.trapFloorAbilityResolutionService.ClearMovementAssistanceIfPawnMoved(host.matchState))
            {
                host.abilityTargetPresenter?.ClearCurrent();
            }

            if (host.trapFloorTemplate != null)
            {
                host.landingCheckPending = true;
            }
        }

        public string DescribeAction(AuthoritativeActionAcceptance acceptance, string actor)
        {
            switch (acceptance.Kind)
            {
                case AuthoritativeActionKind.TrapFloorReveal: return $"{actor} searched a Floor";
                case AuthoritativeActionKind.TrapFloorClaimKey: return $"{actor} claimed a Key";
                case AuthoritativeActionKind.TrapFloorAttemptEscape: return $"{actor} attempted Escape";
                case AuthoritativeActionKind.TrapFloorCollapse: return $"{actor} collapsed a Floor";
                default:
                    return host.trapFloorTurnState == null ? null : $"{actor} advanced the Trap Floor turn";
            }
        }
    }
}
