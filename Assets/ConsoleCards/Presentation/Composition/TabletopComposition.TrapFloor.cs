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
    /// <summary>
    /// Trap Floor's half of the table (CART-1b): the game's state on the table (its rules state, use cases and
    /// services, landing and physical dice in progress), building it, its seat context, its feedback (turn banners,
    /// activity lines, board markers) and its board card backs. The table's own files reach all of it only through
    /// the cartridge contract (<see cref="IGameCartridge"/>); <see cref="TrapFloorCartridge"/> is the game itself.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        // The Trap Floor game, the first cartridge (doc 30 A4): turns, dice, waiting Traps, landing, prizes, Keys,
        // escape, its status texts and turn menu, the Shop and its hint helpers.
        private TrapFloorCartridge trapFloorCartridgeInstance;
        internal TrapFloorCartridge TrapFloorGame => trapFloorCartridgeInstance ??= new TrapFloorCartridge(this);

        // ---------- Trap Floor's state on the table (moved from TabletopComposition.cs) ----------

        internal bool physicalFloorfallPending;
        internal PlayerId physicalFloorfallActor;
        internal bool physicalFloorCollapsePending;
        internal PlayerId physicalFloorCollapseActor;
        internal bool physicalBlindDirectionPending;
        internal PlayerId physicalBlindDirectionActor;
        internal TabletopObjectId physicalBlindDirectionDieId;

        [Tooltip("Designer-authored Trap Floor identity, content, Grid, Mode, Console, and presentation data.")]
        [SerializeField] internal GameDefinition trapFloorGameDefinition;

        internal readonly Dictionary<GameTemplateId, TrapFloorTemplateDefinition> availableTrapFloorTemplates =
            new Dictionary<GameTemplateId, TrapFloorTemplateDefinition>();

        internal TrapFloorTemplateDefinition trapFloorTemplate;
        internal TrapFloorActivityFeedState trapFloorActivityFeed;
        internal TrapFloorRevealFloorUseCase trapFloorRevealFloorUseCase;
        internal TrapFloorObjectiveState trapFloorObjectiveState;
        internal TrapFloorObjectiveUseCase trapFloorObjectiveUseCase;
        internal TrapFloorCollapseState trapFloorCollapseState;
        internal TrapFloorCollapseUseCase trapFloorCollapseUseCase;
        internal TrapFloorTurnState trapFloorTurnState;
        internal TrapFloorTurnService trapFloorTurnService;
        internal TrapFloorTakePrizeUseCase trapFloorTakePrizeUseCase;
        // The Shop (doc 18 §15.4, owner 2026-10-15): every Ability for sale on your turn, open while the rule
        // "shop.enabled" is on. The source says whether the Hand picker pays for a Shop card or a floor prize.
        internal TrapFloorBuyFromShopUseCase trapFloorShopUseCase;
        internal bool landingCheckPending;
        // FLOORS-1: the floor the next table replacement deals (0 = the first, full-size floor). Set only while
        // the next floor is being dealt, so New table and Restart always start at the first floor.
        internal int trapFloorNextFloorIndex;
        // Owner, 2026-10-15: a tile the active pawn lands on turns over only once no pawn stands on it (the pawn
        // moves aside or ahead), or when the turn ends. Its card still counts for the player who landed.
        internal readonly HashSet<TabletopObjectId> landedFloorCards = new HashSet<TabletopObjectId>();
        internal TabletopObjectId waitingLandingFloorCardId;
        internal PlayerId waitingLandingActor;
        internal TrapFloorAbilityResolutionState trapFloorAbilityResolutionState;
        internal TrapFloorAbilityResolutionService trapFloorAbilityResolutionService;

        internal TrapFloorActivityEntry activeFloorRevealActivity;
        internal TrapFloorFloorfallState floorfallState;
        internal TrapFloorFloorfallService floorfallService;
        internal TrapFloorFloorfallTargetPresenter floorfallTargetPresenter;
        internal TrapFloorAbilityTargetPresenter abilityTargetPresenter;
        internal TrapFloorFloormasterLifecycleState floormasterLifecycleState;
        internal TrapFloorFloormasterLifecycleService floormasterLifecycleService;
        internal TrapFloorRoundState trapFloorRoundState;
        private int boardCardBacksRound;
        // Player feedback (doc 25, F1): what has already been announced.
        private TrapFloorActivityFeedState announcedFeed;
        private TrapFloorTurnState announcedTurnState;
        private long announcedSequence;
        private int announcedRound;
        private PlayerId announcedActivePlayer;
        private TrapFloorTurnPhase announcedPhase;

        internal TrapFloorRoundOrchestrationService trapFloorRoundOrchestrationService;

        public TrapFloorFloorfallState FloorfallState => floorfallState;

        public TrapFloorFloormasterLifecycleState FloormasterLifecycleState => floormasterLifecycleState;

        public TrapFloorRoundState TrapFloorRoundState => trapFloorRoundState;

        public TrapFloorActivityFeedState TrapFloorActivityFeed => trapFloorActivityFeed;

        public TrapFloorObjectiveState TrapFloorObjectiveState => trapFloorObjectiveState;

        public TrapFloorCollapseState TrapFloorCollapseState => trapFloorCollapseState;

        public TrapFloorTurnState TrapFloorTurnState => trapFloorTurnState;

        // ---------- building it (moved from TabletopComposition.Build.cs) ----------

        internal void BuildTrapFloorRevealRuntime()
        {
            // CART-1b: after Undo, Redo or a rebuild the table hands back the state kept with the step.
            if (pendingRestoredGameState is TrapFloorSessionState restored)
            {
                trapFloorActivityFeed = restored.Activity;
                trapFloorCollapseState = restored.Collapse;
                trapFloorObjectiveState = restored.Objective;
                trapFloorTurnState = restored.Turn;
                trapFloorAbilityResolutionState = restored.AbilityResolution;
            }
            else
            {
                trapFloorActivityFeed = new TrapFloorActivityFeedState(matchState.Id);
                trapFloorCollapseState = new TrapFloorCollapseState(
                    matchState.Id,
                    trapFloorTemplate.FloorCardIds.Count);
                trapFloorObjectiveState = new TrapFloorObjectiveState(
                    matchState.Id,
                    trapFloorTemplate.ActiveMode.RequiredKeyCount,
                    trapFloorTemplate.ActiveMode.Behavior);
                trapFloorTurnState = new TrapFloorTurnState(
                    matchState.Id,
                    activeSession.Request.ActivePlayerIds);
                trapFloorAbilityResolutionState =
                    new TrapFloorAbilityResolutionState(matchState.Id);
            }
            trapFloorCollapseUseCase = new TrapFloorCollapseUseCase(
                trapFloorTemplate,
                trapFloorCollapseState,
                trapFloorActivityFeed);
            trapFloorRevealFloorUseCase = new TrapFloorRevealFloorUseCase(
                trapFloorTemplate,
                trapFloorActivityFeed,
                trapFloorCollapseState,
                trapFloorAbilityResolutionState,
                trapFloorTurnState);
            trapFloorObjectiveUseCase = new TrapFloorObjectiveUseCase(
                trapFloorTemplate,
                trapFloorObjectiveState,
                trapFloorActivityFeed);
            trapFloorTurnService = new TrapFloorTurnService(
                trapFloorTemplate,
                trapFloorTurnState,
                controllerInputHandService,
                trapFloorActivityFeed,
                trapFloorAbilityResolutionState);
            trapFloorAbilityResolutionService = new TrapFloorAbilityResolutionService(
                trapFloorAbilityResolutionState,
                trapFloorTurnState,
                trapFloorActivityFeed,
                trapFloorTemplate,
                trapFloorCollapseState);
            // Rush/Dodge/Spring help reads where the Pawn really is (physical state first), not its placed pose.
            trapFloorAbilityResolutionService.PawnPositionResolver = TrapFloorGame.PawnTablePosition;
            trapFloorTakePrizeUseCase = new TrapFloorTakePrizeUseCase(
                trapFloorTemplate,
                trapFloorCollapseState,
                trapFloorTurnState);
            trapFloorShopUseCase = new TrapFloorBuyFromShopUseCase(trapFloorTemplate, trapFloorTurnState);
            activeFloorRevealActivity = null;
            TrapFloorGame.SyncLandingPawnTiles();
        }

        internal void BuildFloorfallRuntime()
        {
            floorfallState = new TrapFloorFloorfallState();
            floorfallService = new TrapFloorFloorfallService(
                trapFloorTemplate,
                matchState,
                authoritativeRandomValueSource,
                floorfallState);
            floorfallTargetPresenter = new TrapFloorFloorfallTargetPresenter();
            abilityTargetPresenter = new TrapFloorAbilityTargetPresenter();
            TrapFloorGame.reachHintShown = false;
            boardFeedback?.HideReach();
        }

        private void BuildFloormasterLifecycleRuntime()
        {
            floormasterLifecycleState = new TrapFloorFloormasterLifecycleState(matchState.Id);
            floormasterLifecycleService = new TrapFloorFloormasterLifecycleService(
                trapFloorTemplate,
                authoritativeRandomValueSource,
                floormasterLifecycleState);
        }

        private void BuildTrapFloorRoundRuntime()
        {
            trapFloorRoundState = new TrapFloorRoundState(
                matchState.Id,
                activeSession.Request.ActivePlayerIds);
            trapFloorRoundOrchestrationService = new TrapFloorRoundOrchestrationService(
                trapFloorTemplate,
                matchState,
                trapFloorRoundState,
                floormasterLifecycleState,
                floormasterLifecycleService,
                floorfallService);
        }

        internal TemplateContext CreateTrapFloorTemplateContext(
            TabletopSession session,
            TrapFloorTemplateDefinition templateDefinition,
            PlayerId requestingPlayerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (templateDefinition == null)
            {
                throw new ArgumentNullException(nameof(templateDefinition));
            }

            TrapFloorPlayerSetupDefinition localPlayer = null;
            for (int i = 0; i < templateDefinition.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition candidate = templateDefinition.Players[i];
                if (session.CurrentMatch.GetSeat(candidate.SeatId).OccupantPlayerId == requestingPlayerId)
                {
                    localPlayer = candidate;
                    break;
                }
            }

            if (localPlayer == null)
            {
                throw new InvalidOperationException(
                    "The requesting Player is not assigned to a Trap Floor Seat.");
            }

            return new TemplateContext(
                session,
                TrapFloorGame,
                templateDefinition.Template,
                templateDefinition,
                requestingPlayerId,
                localPlayer.SeatId,
                localPlayer.LayoutSeatIndex,
                localPlayer.HandContainerId,
                templateDefinition.BoardPlayAreaId,
                localPlayer.AvatarCardId,
                localPlayer.PawnId,
                TabletopObjectId.Empty,
                templateDefinition.CardLabels,
                CreateControllerInputDefinitions(templateDefinition.GameDefinition));
        }

        // ---------- feedback: banners, activity lines, board markers (moved from TabletopComposition.Feedback.cs) ----------

        // Player feedback (doc 25, F1). Turns new activity-feed entries and turn changes into banners and
        // messages. A rebuilt or reset session (new or shorter feed) is taken as already announced.
        internal void AnnounceTrapFloorFeedback()
        {
            if (!playerFeedback
                || runtimeUi == null
                || trapFloorActivityFeed == null
                || trapFloorTurnState == null
                || rebuildingFromUndo)
            {
                return;
            }

            IReadOnlyList<TrapFloorActivityEntry> entries = trapFloorActivityFeed.Entries;
            long newest = entries.Count > 0 ? entries[entries.Count - 1].Sequence : 0L;
            if (!ReferenceEquals(announcedFeed, trapFloorActivityFeed)
                || !ReferenceEquals(announcedTurnState, trapFloorTurnState)
                || newest < announcedSequence)
            {
                announcedFeed = trapFloorActivityFeed;
                announcedTurnState = trapFloorTurnState;
                announcedSequence = newest;
                RememberAnnouncedTurn();
                if (entries.Count == 0 || announceTurnAfterRebuild) AnnounceTurn(); // new game, or the hand moved on
                announceTurnAfterRebuild = false;
                return;
            }

            if (newest > announcedSequence)
            {
                int first = entries.Count;
                while (first > 0 && entries[first - 1].Sequence > announcedSequence) first--;
                for (int i = first; i < entries.Count; i++) AnnounceActivity(entries[i]);
                announcedSequence = newest;
            }

            if (trapFloorTurnState.CurrentRound != announcedRound
                || trapFloorTurnState.Phase != announcedPhase
                || trapFloorTurnState.ActivePlayerId != announcedActivePlayer)
            {
                RememberAnnouncedTurn();
                AnnounceTurn();
            }
        }

        private void RememberAnnouncedTurn()
        {
            announcedRound = trapFloorTurnState.CurrentRound;
            announcedPhase = trapFloorTurnState.Phase;
            announcedActivePlayer = trapFloorTurnState.ActivePlayerId;
        }

        private void AnnounceTurn()
        {
            if (trapFloorTurnState.IsCurrentFloorFailed) return;
            RulesCardPanel.RaiseHint(HintTriggers.TurnStart, trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn ? "player" : "floor");
            if (trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn)
            {
                PlayerId active = trapFloorTurnState.ActivePlayerId;
                string colour = TryGetSeatIndex(active, out int seat) ? $" ({PlayerColorName(seat)})" : string.Empty;
                runtimeUi.ShowFeedbackBanner(new FeedbackBannerModel(
                    FeedbackEventKind.TurnStart,
                    $"{FormatPlayerName(active).ToUpperInvariant()}{colour} — YOUR TURN",
                    $"ROUND {trapFloorTurnState.CurrentRound}",
                    FeedbackColour(active)));
            }
            else if (trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
            {
                runtimeUi.ShowFeedbackBanner(new FeedbackBannerModel(
                    FeedbackEventKind.RoundStart,
                    "FLOOR TURN",
                    $"{FormatPlayerName(trapFloorTurnState.FloorOperatorPlayerId)} rolls the floor",
                    new Color(0.878f, 0.325f, 0.122f)));
            }
        }

        private void AnnounceActivity(TrapFloorActivityEntry entry)
        {
            PlayerId actor = entry.ActorPlayerId;
            string who = actor.IsEmpty ? "A player" : FormatPlayerName(actor);
            Color colour = FeedbackColour(actor);
            string name = entry.ContentName ?? string.Empty;
            BoardFeedbackAt(entry, colour);
            RulesCardPanel.RaiseHint(HintTriggers.GameActivity, entry.Kind.ToString());
            if (entry.Kind == TrapFloorActivityKind.RevealedFloorContent)
                RulesCardPanel.RaiseHint(HintTriggers.ContentRevealed, entry.ContentCategory.ToString());
            switch (entry.Kind)
            {
                case TrapFloorActivityKind.RevealedFloorContent:
                    if (entry.ContentCategory == TrapFloorFloorContentCategory.Trap && !entry.TrapSuppressed)
                    {
                        Banner(FeedbackEventKind.Trap, $"TRAP! {name.ToUpperInvariant()}", $"{who} flipped it", colour);
                    }
                    else if (entry.ContentCategory == TrapFloorFloorContentCategory.Key
                        || entry.ContentCategory == TrapFloorFloorContentCategory.Entry
                        || entry.ContentCategory == TrapFloorFloorContentCategory.SecretExit)
                    {
                        Banner(FeedbackEventKind.Reward, $"FOUND: {name.ToUpperInvariant()}", who, colour);
                    }
                    else if (entry.ContentCategory != TrapFloorFloorContentCategory.Trap)
                    {
                        FeedbackLine($"{who} found {name}.", FeedbackSeverity.Gain);
                    }
                    break;
                case TrapFloorActivityKind.ClaimedKey:
                    Banner(FeedbackEventKind.Reward, "KEY CLAIMED", $"{who} — {name}", colour);
                    break;
                case TrapFloorActivityKind.CollapsedFloor:
                    Banner(FeedbackEventKind.Collapse, "FLOOR COLLAPSED",
                        entry.HasCoordinate ? $"Tile {entry.Coordinate} is now a hole" : string.Empty, colour);
                    break;
                case TrapFloorActivityKind.PlayerEliminated:
                    Banner(FeedbackEventKind.Eliminated, $"{who.ToUpperInvariant()} ELIMINATED",
                        "Out for the rest of this floor", colour);
                    break;
                case TrapFloorActivityKind.AllPlayersEliminated:
                    Banner(FeedbackEventKind.Eliminated, "EVERYONE IS OUT", "This floor has failed",
                        new Color(0.878f, 0.325f, 0.122f));
                    break;
                case TrapFloorActivityKind.PlayersReactivated:
                    FeedbackLine("Everyone is back in for the new round.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.PlayerEscaped:
                    Banner(FeedbackEventKind.Reward, $"{who.ToUpperInvariant()} ESCAPED", string.Empty, colour);
                    break;
                case TrapFloorActivityKind.WonGame:
                    Banner(FeedbackEventKind.Reward,
                        trapFloorTemplate == null || trapFloorTemplate.IsLastFloor ? "YOU WIN!" : "FLOOR WON", who, colour);
                    break;
                case TrapFloorActivityKind.UsedShield:
                    FeedbackLine($"{who} used Shield.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.UsedDodge:
                    FeedbackLine($"{who} used Dodge — move to a lit tile.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.UsedCheck:
                    FeedbackLine($"{who} used Check — the Trap is harmless.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.UsedDisarm:
                    FeedbackLine($"{who} used Disarm.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.UsedRush:
                    FeedbackLine($"{who} used Rush — move to a lit tile.", FeedbackSeverity.Gain);
                    break;
                case TrapFloorActivityKind.BlindApplied:
                    FeedbackLine($"{who} will be BLIND next round.", FeedbackSeverity.Warning);
                    break;
                case TrapFloorActivityKind.SlowApplied:
                    FeedbackLine($"{who} will be SLOW next round.", FeedbackSeverity.Warning);
                    break;
                case TrapFloorActivityKind.StickyApplied:
                    FeedbackLine($"{who} is STUCK next round.", FeedbackSeverity.Warning);
                    break;
                case TrapFloorActivityKind.SkippedTurn:
                    FeedbackLine($"{who} skipped their turn.", FeedbackSeverity.Info);
                    break;
            }
        }

        // F2: a flash where a tile flipped, a burst and a glance where the floor collapsed, and short camera
        // kicks for a Trap or an elimination. The camera always returns to where it was.
        private void BoardFeedbackAt(TrapFloorActivityEntry entry, Color colour)
        {
            if (!EnsureBoardFeedback()) return;
            BoardFeedbackSet set = boardFeedback.Set;
            // Minimal feedback (F3) keeps the board markers but no camera nudges.
            TabletopCameraController cameraController = fullFeedback && cameraInputAdapter != null
                ? cameraInputAdapter.CameraController
                : null;
            CardView tileView = null;
            bool hasTile = !entry.FloorCardId.IsEmpty && Registry.TryGetCardView(entry.FloorCardId, out tileView);
            Vector3 tile = hasTile ? tileView.transform.position : Vector3.zero;
            switch (entry.Kind)
            {
                case TrapFloorActivityKind.RevealedFloorContent:
                    bool trap = entry.ContentCategory == TrapFloorFloorContentCategory.Trap && !entry.TrapSuppressed;
                    if (hasTile) boardFeedback.Flash(tile, trap ? new Color(0.94f, 0.28f, 0.24f, 1f) : colour);
                    if (trap) cameraController?.Kick(set.TrapKick, set.KickSeconds);
                    break;
                case TrapFloorActivityKind.CollapsedFloor:
                    if (hasTile)
                    {
                        boardFeedback.Burst(tile, new Color(0.878f, 0.325f, 0.122f, 1f));
                        cameraController?.Glance(tile, set.CollapseGlanceSeconds);
                    }

                    cameraController?.Kick(set.CollapseKick, set.KickSeconds);
                    break;
                case TrapFloorActivityKind.PlayerEliminated:
                case TrapFloorActivityKind.AllPlayersEliminated:
                    cameraController?.Kick(set.EliminatedKick, set.KickSeconds * 1.4f);
                    break;
            }
        }

        // F2 markers every frame: a ring on the active pawn, a warning on each waiting Trap, a calm green outline on
        // each Trap made harmless, and the tiles a move must reach (Spring, Crane, Dodge, Rush) with an arrow from the
        // pawn when there is one target. (2026-10-20: the green outline replaces a tint nobody could see.)
        private readonly List<Vector3> boardFeedbackNeutralized = new List<Vector3>(8);
        private static readonly Color NeutralizedTrapColour = new Color(0.32f, 0.78f, 0.52f, 0.9f);

        internal void RefreshTrapFloorBoardFeedback()
        {
            if (!playerFeedback
                || trapFloorTemplate == null
                || trapFloorTurnState == null
                || matchState == null)
            {
                boardFeedback?.HideAll();
                return;
            }

            if (!EnsureBoardFeedback()) return;
            bool playerTurn = !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn;
            PlayerId active = trapFloorTurnState.ActivePlayerId;
            Color colour = FeedbackColour(active);
            bool pawnFound = false;
            Vector3 pawnPosition = Vector3.zero;
            if (playerTurn && TrapFloorGame.TryGetTrapFloorPlayerSetup(active, out TrapFloorPlayerSetupDefinition setup))
            {
                for (int i = 0; i < Registry.pawnViews.Count; i++)
                {
                    PawnView pawnView = Registry.pawnViews[i];
                    if (pawnView != null && pawnView.IsBound && pawnView.ObjectId == setup.PawnId)
                    {
                        pawnPosition = pawnView.transform.position;
                        pawnFound = true;
                        break;
                    }
                }
            }

            boardFeedback.ShowActivePawn(pawnFound, pawnPosition, colour);
            boardFeedbackTraps.Clear();
            if (trapFloorAbilityResolutionState != null)
            {
                IReadOnlyList<TrapFloorTrapResolutionRecord> records = trapFloorAbilityResolutionState.TrapRecords;
                for (int i = 0; i < records.Count; i++)
                {
                    TrapFloorTrapResolutionRecord record = records[i];
                    if (record.IsPending
                        && record.Content.HasSupportedAssistedTrapEffect
                        && Registry.TryGetCardView(record.FloorCardId, out CardView trapView))
                    {
                        boardFeedbackTraps.Add(trapView.transform.position);
                    }
                }
            }

            boardFeedback.ShowTrapWarnings(boardFeedbackTraps);
            boardFeedbackNeutralized.Clear();
            if (trapFloorAbilityResolutionState != null)
            {
                IReadOnlyList<TrapFloorTrapResolutionRecord> neutralRecords = trapFloorAbilityResolutionState.TrapRecords;
                for (int i = 0; i < neutralRecords.Count; i++)
                {
                    TrapFloorTrapResolutionRecord record = neutralRecords[i];
                    if (record.Disposition == TrapFloorTrapResolutionDisposition.Neutralized
                        && Registry.TryGetCardView(record.FloorCardId, out CardView neutralView))
                    {
                        boardFeedbackNeutralized.Add(neutralView.transform.position);
                    }
                }
            }

            boardFeedback.ShowNeutralized(boardFeedbackNeutralized, NeutralizedTrapColour);
            boardFeedbackTargets.Clear();
            IReadOnlyList<TabletopObjectId> targetIds = trapFloorAbilityResolutionState?.ActiveRushAssistance?.TargetFloorCardIds
                ?? trapFloorAbilityResolutionState?.ActiveDodgeAssistance?.TargetFloorCardIds;
            if (playerTurn && targetIds != null)
            {
                for (int i = 0; i < targetIds.Count; i++)
                {
                    if (Registry.TryGetCardView(targetIds[i], out CardView targetView))
                        boardFeedbackTargets.Add(targetView.transform.position);
                }
            }

            boardFeedback.ShowMoveTargets(boardFeedbackTargets, colour, pawnFound, pawnPosition);
        }

        // ---------- board card backs and floor colours (moved from TabletopComposition.CardArt.cs) ----------

        // Board (floor) card backs change per round: round 1 uses the first back, round 2 the second,
        // and so on, repeating after the last (doc 24). Null when the game has no backs.
        internal Texture2D CurrentBoardCardBack()
        {
            if (trapFloorGameDefinition == null)
            {
                return null;
            }

            int count = BoardCardBackCount();
            if (count == 0)
            {
                return null;
            }

            int round = trapFloorRoundState != null ? Math.Max(1, trapFloorRoundState.CurrentRoundNumber) : 1;
            int index = (round - 1) % count;
            IReadOnlyList<Texture2D> backs = trapFloorGameDefinition.BoardCardBacks;
            IReadOnlyList<string> keys = trapFloorGameDefinition.BoardCardBackReferences;
            return Art(
                keys != null && index < keys.Count ? keys[index] : null,
                backs != null && index < backs.Count ? backs[index] : null);
        }

        // A back counts when it has a texture or a key (a pack may supply the picture for a key).
        private int BoardCardBackCount()
        {
            if (trapFloorGameDefinition == null) return 0;
            int textures = trapFloorGameDefinition.BoardCardBacks != null ? trapFloorGameDefinition.BoardCardBacks.Count : 0;
            int keys = trapFloorGameDefinition.BoardCardBackReferences != null
                ? trapFloorGameDefinition.BoardCardBackReferences.Count
                : 0;
            return Math.Max(textures, keys);
        }

        // Only does work on the frame the round number changes; re-applies floor backs then.
        internal void RefreshBoardCardBacksIfRoundChanged()
        {
            int round = trapFloorRoundState != null ? trapFloorRoundState.CurrentRoundNumber : 0;
            if (round == boardCardBacksRound)
            {
                return;
            }

            boardCardBacksRound = round;
            if (trapFloorTemplate == null
                || matchState == null
                || trapFloorGameDefinition == null
                || BoardCardBackCount() < 2)
            {
                return;
            }

            foreach (TabletopObjectId floorCardId in trapFloorTemplate.FloorCardIds.Values)
            {
                TrapFloorGame.RefreshTrapFloorFloorCardPresentation(floorCardId);
            }
        }

        internal static Color TrapFloorContentColor(TrapFloorFloorContentCategory category)
        {
            switch (category)
            {
                case TrapFloorFloorContentCategory.Trap:
                    return new Color(0.86f, 0.34f, 0.28f);
                case TrapFloorFloorContentCategory.Friend:
                    return new Color(0.40f, 0.76f, 0.70f);
                case TrapFloorFloorContentCategory.Key:
                    return new Color(0.95f, 0.78f, 0.26f);
                case TrapFloorFloorContentCategory.SecretExit:
                    return new Color(0.56f, 0.48f, 0.84f);
                case TrapFloorFloorContentCategory.Entry:
                    return new Color(0.40f, 0.70f, 0.42f);
                case TrapFloorFloorContentCategory.Ability:
                    return new Color(0.42f, 0.66f, 0.90f);
                default:
                    return new Color(0.95f, 0.88f, 0.42f);
            }
        }
    }
}
