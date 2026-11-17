using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorTurnPhase
    {
        PlayerTurn = 0,
        FloorTurn = 1,
        /// <summary>
        /// Rules v1.151126 (owner, 2026-11-16): everyone buys at the same time, before round 1 and after every collapse.
        /// The player who plays next is kept (<see cref="TrapFloorTurnState.NextPlayerId"/>); nobody has the turn until
        /// every player still in has finished buying.
        /// </summary>
        BuyPhase = 2,
    }

    public enum TrapFloorCurrentFloorPlayerState
    {
        Active = 0,
        EliminatedForCurrentRound = 1,
    }

    /// <summary>Match-scoped assistance state. It never restricts freeform tabletop actions.</summary>
    public sealed class TrapFloorTurnState
    {
        private readonly ReadOnlyCollection<PlayerId> playerOrder;
        private readonly HashSet<PlayerId> eliminatedPlayerIds;
        // Buy Phase: the players who have finished buying (empty outside it).
        private readonly HashSet<PlayerId> doneBuyingPlayerIds = new HashSet<PlayerId>();
        // Stage 2 (owner, 2026-11-17): the players lay the floor out themselves; the floor tiles in the order of the cells
        // once they have (empty: the dealt floor).
        private TabletopObjectId[] floorArrangement = Array.Empty<TabletopObjectId>();
        private int activePlayerIndex;
        private int floorOperatorIndex;
        // IMP-1: in a floor turn between two player turns, the index of the player who plays next (-1: the floor turn
        // ends the round).
        private int floorResumeIndex = -1;

        public TrapFloorTurnState(MatchId matchId, IEnumerable<PlayerId> participatingPlayerIds)
            : this(
                matchId,
                participatingPlayerIds,
                1,
                TrapFloorTurnPhase.PlayerTurn,
                0,
                -1,
                Array.Empty<PlayerId>(),
                false)
        {
        }

        internal TrapFloorTurnState(
            MatchId matchId,
            IEnumerable<PlayerId> participatingPlayerIds,
            int currentRound,
            TrapFloorTurnPhase phase,
            int activePlayerIndex,
            int floorOperatorIndex,
            IEnumerable<PlayerId> eliminatedPlayerIds,
            bool isCurrentFloorFailed)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Trap Floor turn state requires a Match ID.", nameof(matchId));
            if (participatingPlayerIds == null) throw new ArgumentNullException(nameof(participatingPlayerIds));
            if (currentRound < 1) throw new ArgumentOutOfRangeException(nameof(currentRound));
            if (!Enum.IsDefined(typeof(TrapFloorTurnPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));

            List<PlayerId> players = new List<PlayerId>(participatingPlayerIds);
            if (players.Count < 1)
                throw new ArgumentException("Trap Floor turn state requires at least one participating Player.", nameof(participatingPlayerIds));
            HashSet<PlayerId> uniquePlayers = new HashSet<PlayerId>();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].IsEmpty || !uniquePlayers.Add(players[i]))
                    throw new ArgumentException("Trap Floor turn Players must be non-empty and unique.", nameof(participatingPlayerIds));
            }

            if (eliminatedPlayerIds == null) throw new ArgumentNullException(nameof(eliminatedPlayerIds));
            HashSet<PlayerId> eliminated = new HashSet<PlayerId>();
            foreach (PlayerId eliminatedPlayerId in eliminatedPlayerIds)
            {
                if (!uniquePlayers.Contains(eliminatedPlayerId) || !eliminated.Add(eliminatedPlayerId))
                {
                    throw new ArgumentException(
                        "Eliminated Trap Floor Players must be unique participants in this turn state.",
                        nameof(eliminatedPlayerIds));
                }
            }

            if (isCurrentFloorFailed != (eliminated.Count == players.Count))
            {
                throw new ArgumentException(
                    "A failed Trap Floor round requires every participating Player to be eliminated for the current round.",
                    nameof(isCurrentFloorFailed));
            }

            if (!isCurrentFloorFailed
                && (phase == TrapFloorTurnPhase.PlayerTurn || phase == TrapFloorTurnPhase.BuyPhase)
                && (activePlayerIndex < 0 || activePlayerIndex >= players.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(activePlayerIndex));
            }

            if (!isCurrentFloorFailed
                && phase == TrapFloorTurnPhase.FloorTurn
                && (floorOperatorIndex < 0 || floorOperatorIndex >= players.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(floorOperatorIndex));
            }

            if (isCurrentFloorFailed && (activePlayerIndex != -1 || floorOperatorIndex != -1))
            {
                throw new ArgumentException("A failed Trap Floor cannot retain an assisted turn actor.");
            }
            if (!isCurrentFloorFailed
                && (phase == TrapFloorTurnPhase.PlayerTurn || phase == TrapFloorTurnPhase.BuyPhase)
                && eliminated.Contains(players[activePlayerIndex]))
            {
                throw new ArgumentException("The active Trap Floor turn Player cannot be eliminated.");
            }
            if (!isCurrentFloorFailed
                && phase == TrapFloorTurnPhase.FloorTurn
                && eliminated.Contains(players[floorOperatorIndex]))
            {
                throw new ArgumentException("The Trap Floor operator cannot be eliminated.");
            }

            MatchId = matchId;
            playerOrder = new ReadOnlyCollection<PlayerId>(players);
            this.eliminatedPlayerIds = eliminated;
            CurrentRound = currentRound;
            Phase = phase;
            IsCurrentFloorFailed = isCurrentFloorFailed;
            this.activePlayerIndex = !isCurrentFloorFailed
                && (phase == TrapFloorTurnPhase.PlayerTurn || phase == TrapFloorTurnPhase.BuyPhase)
                ? activePlayerIndex
                : -1;
            this.floorOperatorIndex = !isCurrentFloorFailed && phase == TrapFloorTurnPhase.FloorTurn
                ? floorOperatorIndex
                : -1;
        }

        // IMP-1: the restored turn state of a floor turn between players (Undo, Redo).
        internal TrapFloorTurnState(
            MatchId matchId,
            IEnumerable<PlayerId> participatingPlayerIds,
            int currentRound,
            TrapFloorTurnPhase phase,
            int activePlayerIndex,
            int floorOperatorIndex,
            IEnumerable<PlayerId> eliminatedPlayerIds,
            bool isCurrentFloorFailed,
            int floorResumeIndex)
            : this(
                matchId,
                participatingPlayerIds,
                currentRound,
                phase,
                activePlayerIndex,
                floorOperatorIndex,
                eliminatedPlayerIds,
                isCurrentFloorFailed)
        {
            this.floorResumeIndex = !isCurrentFloorFailed
                && phase == TrapFloorTurnPhase.FloorTurn
                && floorResumeIndex >= 0
                && floorResumeIndex < playerOrder.Count
                    ? floorResumeIndex
                    : -1;
        }

        public MatchId MatchId { get; }
        public int CurrentRound { get; private set; }
        public TrapFloorTurnPhase Phase { get; private set; }
        public bool IsCurrentFloorFailed { get; private set; }
        public IReadOnlyList<PlayerId> PlayerOrder => playerOrder;
        public PlayerId ActivePlayerId =>
            !IsCurrentFloorFailed && Phase == TrapFloorTurnPhase.PlayerTurn
                ? playerOrder[activePlayerIndex]
                : PlayerId.Empty;
        public PlayerId FloorOperatorPlayerId =>
            !IsCurrentFloorFailed && Phase == TrapFloorTurnPhase.FloorTurn
                ? playerOrder[floorOperatorIndex]
                : PlayerId.Empty;

        // ---------- Buy Phase (rules v1.151126) ----------

        public bool IsBuyPhase => !IsCurrentFloorFailed && Phase == TrapFloorTurnPhase.BuyPhase;

        /// <summary>The player whose turn comes once the Buy Phase ends (the turn player outside it).</summary>
        public PlayerId NextPlayerId => IsBuyPhase ? playerOrder[activePlayerIndex] : ActivePlayerId;

        /// <summary>
        /// True once the computer that deals (offline, or the host) has collected and dealt the Console Cards for this
        /// Buy Phase. Kept with the turn so an Undo step or a rebuilt table never deals twice.
        /// </summary>
        public bool IsBuyPhaseDealt { get; private set; }

        public bool HasFinishedBuying(PlayerId playerId) => doneBuyingPlayerIds.Contains(playerId);

        /// <summary>In the Buy Phase: a player still in for the floor who has not finished buying.</summary>
        public bool IsBuyingPlayer(PlayerId playerId) =>
            IsBuyPhase
            && playerOrder.Contains(playerId)
            && !eliminatedPlayerIds.Contains(playerId)
            && !doneBuyingPlayerIds.Contains(playerId);

        internal PlayerId[] CopyDoneBuyingPlayerIds()
        {
            List<PlayerId> done = new List<PlayerId>();
            for (int i = 0; i < playerOrder.Count; i++)
            {
                if (doneBuyingPlayerIds.Contains(playerOrder[i])) done.Add(playerOrder[i]);
            }
            return done.ToArray();
        }

        /// <summary>Opens the Buy Phase in front of the coming player turn (nothing else moves).</summary>
        public void OpenBuyPhase()
        {
            if (IsCurrentFloorFailed || Phase != TrapFloorTurnPhase.PlayerTurn)
                throw new InvalidOperationException("The Buy Phase opens only in front of a player turn.");
            Phase = TrapFloorTurnPhase.BuyPhase;
            doneBuyingPlayerIds.Clear();
            IsBuyPhaseDealt = false;
        }

        public void MarkBuyPhaseDealt()
        {
            if (IsBuyPhase) IsBuyPhaseDealt = true;
        }

        /// <summary>
        /// Stage 2: the players are laying the floor out themselves. The Buy Phase cannot end (the last Done Buying is
        /// refused) until they have (<see cref="CompleteFloorSetup"/>).
        /// </summary>
        public bool IsFloorSetupPending { get; private set; }

        /// <summary>Stage 2: the floor tiles in cell order as the players laid them out (empty: the dealt floor).</summary>
        public IReadOnlyList<TabletopObjectId> FloorArrangement => floorArrangement;

        /// <summary>Stage 2: the dealing computer hands the floor to the players (in the Buy Phase before round 1).</summary>
        public void BeginFloorSetup()
        {
            if (!IsBuyPhase || floorArrangement.Length > 0) return;
            IsFloorSetupPending = true;
        }

        internal void CompleteFloorSetup(IReadOnlyList<TabletopObjectId> cardIdsByCell)
        {
            TabletopObjectId[] copy = new TabletopObjectId[cardIdsByCell.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = cardIdsByCell[i];
            floorArrangement = copy;
            IsFloorSetupPending = false;
        }

        internal TabletopObjectId[] CopyFloorArrangement()
        {
            TabletopObjectId[] copy = new TabletopObjectId[floorArrangement.Length];
            Array.Copy(floorArrangement, copy, copy.Length);
            return copy;
        }

        // Undo, Redo, a table received: the floor setup as it was kept.
        internal void RestoreFloorSetup(bool pending, IReadOnlyList<TabletopObjectId> cardIdsByCell)
        {
            TabletopObjectId[] copy = new TabletopObjectId[cardIdsByCell != null ? cardIdsByCell.Count : 0];
            for (int i = 0; i < copy.Length; i++) copy[i] = cardIdsByCell[i];
            floorArrangement = copy;
            IsFloorSetupPending = pending && copy.Length == 0;
        }

        /// <summary>Stage 2: whether this player's Done Buying would end the Buy Phase.</summary>
        public bool WouldEndBuyPhase(PlayerId playerId)
        {
            if (!IsBuyingPlayer(playerId)) return false;
            for (int i = 0; i < playerOrder.Count; i++)
            {
                PlayerId other = playerOrder[i];
                if (other != playerId && !eliminatedPlayerIds.Contains(other) && !doneBuyingPlayerIds.Contains(other)) return false;
            }

            return true;
        }

        // Undo, Redo, a table received: the Buy Phase as it was kept.
        internal void RestoreBuyPhase(IEnumerable<PlayerId> donePlayerIds, bool dealt)
        {
            doneBuyingPlayerIds.Clear();
            IsBuyPhaseDealt = IsBuyPhase && dealt;
            if (!IsBuyPhase || donePlayerIds == null) return;
            foreach (PlayerId playerId in donePlayerIds)
            {
                if (playerOrder.Contains(playerId)) doneBuyingPlayerIds.Add(playerId);
            }
        }

        /// <summary>The player has finished buying. True when that ended the Buy Phase (the coming turn begins).</summary>
        internal bool MarkFinishedBuying(PlayerId playerId)
        {
            if (!IsBuyingPlayer(playerId)) throw new InvalidOperationException("The Player is not buying.");
            doneBuyingPlayerIds.Add(playerId);
            if (!EveryoneHasBought()) return false;
            CloseBuyPhase();
            return true;
        }

        private bool EveryoneHasBought()
        {
            for (int i = 0; i < playerOrder.Count; i++)
            {
                PlayerId playerId = playerOrder[i];
                if (!eliminatedPlayerIds.Contains(playerId) && !doneBuyingPlayerIds.Contains(playerId)) return false;
            }
            return true;
        }

        private void CloseBuyPhase()
        {
            Phase = TrapFloorTurnPhase.PlayerTurn;
            doneBuyingPlayerIds.Clear();
            IsBuyPhaseDealt = false;
        }

        internal bool ContainsPlayer(PlayerId playerId) => playerOrder.Contains(playerId);

        public TrapFloorCurrentFloorPlayerState GetPlayerState(PlayerId playerId)
        {
            if (!playerOrder.Contains(playerId))
                throw new ArgumentException("Player is not part of this Trap Floor session.", nameof(playerId));
            return eliminatedPlayerIds.Contains(playerId)
                ? TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                : TrapFloorCurrentFloorPlayerState.Active;
        }

        internal PlayerId[] CopyEliminatedPlayerIds()
        {
            List<PlayerId> eliminated = new List<PlayerId>();
            for (int i = 0; i < playerOrder.Count; i++)
            {
                if (eliminatedPlayerIds.Contains(playerOrder[i])) eliminated.Add(playerOrder[i]);
            }
            return eliminated.ToArray();
        }

        internal TrapFloorTurnPosition CapturePosition() =>
            new TrapFloorTurnPosition(
                CurrentRound,
                Phase,
                activePlayerIndex,
                floorOperatorIndex,
                IsCurrentFloorFailed,
                floorResumeIndex);

        /// <summary>
        /// IMP-1 (owner, 2026-10-22, Impossible: "once a player exits the turn order is floor, player, floor, player"):
        /// while true, a floor turn follows every player turn, and the next player plays after it in the same round.
        /// The table sets it from the mode and the escapes before each advance (it is derived, so Undo needs nothing).
        /// </summary>
        public bool FloorTurnAfterEachPlayer { get; set; }

        /// <summary>IMP-1: a floor turn between two player turns (not the one that ends the round).</summary>
        public bool IsFloorTurnBetweenPlayers =>
            !IsCurrentFloorFailed && Phase == TrapFloorTurnPhase.FloorTurn && floorResumeIndex >= 0;

        internal void Advance()
        {
            if (Phase == TrapFloorTurnPhase.BuyPhase)
                throw new InvalidOperationException("The Buy Phase ends when every player has finished buying.");
            if (Phase == TrapFloorTurnPhase.PlayerTurn)
            {
                if (IsCurrentFloorFailed)
                    throw new InvalidOperationException("A failed Trap Floor round must be completed from Floor Turn.");

                int nextPlayerIndex = FindNextActivePlayerIndex(activePlayerIndex + 1, false);
                if (nextPlayerIndex >= 0 && FloorTurnAfterEachPlayer)
                {
                    // IMP-1: a floor turn between players; the next player still in plays after it (same round). Any
                    // player rolls, so the operator is simply a player still in.
                    Phase = TrapFloorTurnPhase.FloorTurn;
                    activePlayerIndex = -1;
                    floorOperatorIndex = nextPlayerIndex;
                    floorResumeIndex = nextPlayerIndex;
                    return;
                }

                if (nextPlayerIndex >= 0)
                {
                    activePlayerIndex = nextPlayerIndex;
                    return;
                }

                Phase = TrapFloorTurnPhase.FloorTurn;
                activePlayerIndex = -1;
                floorOperatorIndex = FindNextActivePlayerIndex((CurrentRound - 1) % playerOrder.Count, true);
                floorResumeIndex = -1;
                return;
            }

            if (!IsCurrentFloorFailed && floorResumeIndex >= 0)
            {
                // IMP-1: after a floor turn between players, the next player still in plays (same round).
                int resumeIndex = FindNextActivePlayerIndex(floorResumeIndex, false);
                floorResumeIndex = -1;
                if (resumeIndex >= 0)
                {
                    Phase = TrapFloorTurnPhase.PlayerTurn;
                    activePlayerIndex = resumeIndex;
                    floorOperatorIndex = -1;
                    return;
                }
            }

            CurrentRound = checked(CurrentRound + 1);
            // Owner, 2026-10-21: a knocked-out player stays out for the rest of the floor; only a failed floor
            // (everyone out) brings everyone back.
            if (IsCurrentFloorFailed) eliminatedPlayerIds.Clear();
            IsCurrentFloorFailed = false;
            Phase = TrapFloorTurnPhase.PlayerTurn;
            activePlayerIndex = FindNextActivePlayerIndex(0, false);
            floorOperatorIndex = -1;
            floorResumeIndex = -1;
        }

        internal PlayerId MarkPlayerEliminatedForCurrentRound(PlayerId playerId)
        {
            int playerIndex = playerOrder.IndexOf(playerId);
            if (playerIndex < 0)
                throw new ArgumentException("Player is not part of this Trap Floor session.", nameof(playerId));
            if (IsCurrentFloorFailed)
                throw new InvalidOperationException("Every Player is already eliminated for the current round.");

            if (!eliminatedPlayerIds.Add(playerId))
                throw new InvalidOperationException("The Player is already eliminated for the current round.");

            if (eliminatedPlayerIds.Count == playerOrder.Count)
            {
                IsCurrentFloorFailed = true;
                Phase = TrapFloorTurnPhase.FloorTurn;
                activePlayerIndex = -1;
                floorOperatorIndex = -1;
                floorResumeIndex = -1;
                return playerId;
            }

            if (Phase == TrapFloorTurnPhase.BuyPhase)
            {
                // Buy Phase: a player out for the floor no longer buys; when it was the coming turn player, the turn
                // goes to the next player still in (or, with nobody left this round, to the floor turn).
                doneBuyingPlayerIds.Remove(playerId);
                if (playerIndex == activePlayerIndex)
                {
                    int nextIndex = FindNextActivePlayerIndex(playerIndex + 1, false);
                    if (nextIndex < 0)
                    {
                        CloseBuyPhase();
                        Advance();
                        return playerId;
                    }

                    activePlayerIndex = nextIndex;
                }

                if (EveryoneHasBought()) CloseBuyPhase();
                return playerId;
            }

            if (Phase == TrapFloorTurnPhase.PlayerTurn && playerIndex == activePlayerIndex)
            {
                Advance();
            }
            else if (Phase == TrapFloorTurnPhase.FloorTurn && playerIndex == floorOperatorIndex)
            {
                floorOperatorIndex = FindNextActivePlayerIndex(playerIndex + 1, true);
            }

            return playerId;
        }

        internal void RollBackElimination(PlayerId playerId, TrapFloorTurnPosition previousPosition)
        {
            if (!eliminatedPlayerIds.Remove(playerId))
                throw new InvalidOperationException("Cannot roll back a Player who was not eliminated.");
            RestorePosition(previousPosition);
        }

        internal void RestorePosition(TrapFloorTurnPosition position)
        {
            CurrentRound = position.Round;
            Phase = position.Phase;
            activePlayerIndex = position.ActivePlayerIndex;
            floorOperatorIndex = position.FloorOperatorIndex;
            IsCurrentFloorFailed = position.IsCurrentFloorFailed;
            floorResumeIndex = position.FloorResumeIndex;
        }

        private int FindNextActivePlayerIndex(int startingIndex, bool wrap)
        {
            int checkedCount = wrap ? playerOrder.Count : playerOrder.Count - startingIndex;
            for (int offset = 0; offset < checkedCount; offset++)
            {
                int index = wrap
                    ? (startingIndex + offset) % playerOrder.Count
                    : startingIndex + offset;
                if (!eliminatedPlayerIds.Contains(playerOrder[index])) return index;
            }
            return -1;
        }
    }

    internal readonly struct TrapFloorTurnPosition
    {
        public TrapFloorTurnPosition(
            int round,
            TrapFloorTurnPhase phase,
            int activePlayerIndex,
            int floorOperatorIndex,
            bool isCurrentFloorFailed,
            int floorResumeIndex = -1)
        {
            Round = round;
            Phase = phase;
            ActivePlayerIndex = activePlayerIndex;
            FloorOperatorIndex = floorOperatorIndex;
            IsCurrentFloorFailed = isCurrentFloorFailed;
            FloorResumeIndex = floorResumeIndex;
        }

        public int Round { get; }
        public TrapFloorTurnPhase Phase { get; }
        public int ActivePlayerIndex { get; }
        public int FloorOperatorIndex { get; }
        public bool IsCurrentFloorFailed { get; }
        public int FloorResumeIndex { get; }
    }

    public enum TrapFloorTurnAdvanceError
    {
        None = 0,
        MatchMismatch = 1,
        RevisionConflict = 2,
        ActorIsNotActivePlayer = 3,
        ActorIsNotFloorOperator = 4,
        PlayerSetupMissing = 5,
        ControllerDrawRejected = 6,
        ActivityFeedMismatch = 7,
        CurrentFloorFailed = 8,
        PlayerIsNotParticipant = 9,
        PlayerAlreadyEliminatedForCurrentRound = 10,
        /// <summary>Stage 2: the players are still laying the floor out; the Buy Phase cannot end yet.</summary>
        FloorSetupPending = 11,
        /// <summary>Stage 2: no floor setup is waiting.</summary>
        FloorSetupNotPending = 12,
        /// <summary>Stage 2: the layout does not use every floor tile once, or a tile is face up.</summary>
        FloorArrangementInvalid = 13,
    }

    public readonly struct TrapFloorTurnAdvanceResult
    {
        private TrapFloorTurnAdvanceResult(
            bool succeeded,
            long revision,
            int drawnCount,
            TrapFloorTurnAdvanceError error,
            ControllerInputHandDrawError drawError)
        {
            Succeeded = succeeded;
            Revision = revision;
            DrawnCount = drawnCount;
            Error = error;
            DrawError = drawError;
        }

        public bool Succeeded { get; }
        public long Revision { get; }
        public int DrawnCount { get; }
        public TrapFloorTurnAdvanceError Error { get; }
        public ControllerInputHandDrawError DrawError { get; }

        internal static TrapFloorTurnAdvanceResult Accepted(long revision, int drawnCount) =>
            new TrapFloorTurnAdvanceResult(true, revision, drawnCount, TrapFloorTurnAdvanceError.None, ControllerInputHandDrawError.None);

        internal static TrapFloorTurnAdvanceResult Failure(
            TrapFloorTurnAdvanceError error,
            ControllerInputHandDrawError drawError = ControllerInputHandDrawError.None) =>
            new TrapFloorTurnAdvanceResult(false, -1, 0, error, drawError);
    }

    /// <summary>
    /// Advances only the optional Trap Floor turn helper. It never draws or moves tabletop objects.
    /// </summary>
    public sealed class TrapFloorTurnService
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly TrapFloorTurnState state;
        private readonly ControllerInputHandService handService;
        private readonly TrapFloorActivityFeedState activityFeed;
        private readonly TrapFloorAbilityResolutionState abilityResolutionState;

        public TrapFloorTurnService(
            TrapFloorTemplateDefinition template,
            TrapFloorTurnState state,
            ControllerInputHandService handService,
            TrapFloorActivityFeedState activityFeed)
            : this(template, state, handService, activityFeed, null)
        {
        }

        public TrapFloorTurnService(
            TrapFloorTemplateDefinition template,
            TrapFloorTurnState state,
            ControllerInputHandService handService,
            TrapFloorActivityFeedState activityFeed,
            TrapFloorAbilityResolutionState abilityResolutionState)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.handService = handService ?? throw new ArgumentNullException(nameof(handService));
            this.activityFeed = activityFeed ?? throw new ArgumentNullException(nameof(activityFeed));
            this.abilityResolutionState = abilityResolutionState;
            if (abilityResolutionState != null && abilityResolutionState.MatchId != state.MatchId)
                throw new ArgumentException("Trap Floor temporary status state must belong to the same Match.", nameof(abilityResolutionState));
        }

        public ControllerInputHandDrawResult DrawForCurrentPlayer(
            MatchState matchState,
            CommandContext context)
        {
            if (matchState == null || context.MatchId != matchState.Id || state.MatchId != matchState.Id)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.MatchMismatch);
            if (state.Phase != TrapFloorTurnPhase.PlayerTurn
                || context.RequestedByPlayerId != state.ActivePlayerId
                || !TryGetPlayerSetup(matchState, state.ActivePlayerId, out TrapFloorPlayerSetupDefinition player))
            {
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.ActorDoesNotOwnSeat);
            }

            return handService.DrawUpToConfiguredHandLimit(
                matchState,
                template.GameDefinition,
                new DrawUpToConfiguredHandLimitCommand(context, player.SeatId, player.ControllerDeckId));
        }

        public TrapFloorTurnAdvanceResult Advance(MatchState matchState, CommandContext context)
        {
            return Advance(matchState, context, false);
        }

        public TrapFloorTurnAdvanceResult Skip(MatchState matchState, CommandContext context)
        {
            if (state.Phase != TrapFloorTurnPhase.PlayerTurn)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);
            return Advance(matchState, context, true);
        }

        /// <summary>
        /// Authoritative boundary for Trap resolution to mark its affected Player eliminated.
        /// Trap failure conditions remain outside this service until they are explicitly authored.
        /// </summary>
        public TrapFloorTurnAdvanceResult MarkPlayerEliminatedForCurrentRound(
            MatchState matchState,
            CommandContext context,
            PlayerId affectedPlayerId)
        {
            return MarkPlayerEliminatedForCurrentRound(
                matchState,
                context,
                affectedPlayerId,
                null);
        }

        internal TrapFloorTurnAdvanceResult MarkPlayerEliminatedForCurrentRound(
            MatchState matchState,
            CommandContext context,
            PlayerId affectedPlayerId,
            TrapFloorTrapResolutionRecord trap)
        {
            TrapFloorTurnAdvanceResult validation = ValidateSessionOperation(matchState, context);
            if (!validation.Succeeded) return validation;
            if (!state.ContainsPlayer(affectedPlayerId))
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.PlayerIsNotParticipant);
            if (state.GetPlayerState(affectedPlayerId)
                == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                return TrapFloorTurnAdvanceResult.Failure(
                    TrapFloorTurnAdvanceError.PlayerAlreadyEliminatedForCurrentRound);

            long acceptedRevision = checked(matchState.Revision + 1L);
            state.MarkPlayerEliminatedForCurrentRound(affectedPlayerId);
            // P8A eliminated-pawn fix (owner, 2026-11-13): the out player's pawn rests where it is, in this same command,
            // on every computer (no moving state left for physics to settle and commit; see TrapFloorEliminatedPawnRest).
            if (TryGetPlayerSetup(matchState, affectedPlayerId, out TrapFloorPlayerSetupDefinition eliminatedSetup))
                TrapFloorEliminatedPawnRest.Rest(matchState, eliminatedSetup.PawnId);
            if (trap == null)
            {
                activityFeed.RecordPlayerEliminated(
                    acceptedRevision,
                    affectedPlayerId);
            }
            else
            {
                activityFeed.RecordPlayerEliminated(
                    acceptedRevision,
                    affectedPlayerId,
                    trap);
            }
            if (state.IsCurrentFloorFailed)
                activityFeed.RecordAllPlayersEliminated(acceptedRevision, affectedPlayerId);

            long revision = matchState.AdvanceRevision(
                context.Id,
                context.RequestedByPlayerId,
                AuthoritativeActionKind.Unspecified);
            return TrapFloorTurnAdvanceResult.Accepted(revision, 0);
        }

        /// <summary>
        /// Rules v1.151126 Buy Phase: the requesting player has finished buying. When every player still in has, the
        /// coming player's turn begins. Every computer runs it the same way (the host replays a joined computer's).
        /// </summary>
        public TrapFloorTurnAdvanceResult FinishBuying(MatchState matchState, CommandContext context)
        {
            TrapFloorTurnAdvanceResult validation = ValidateSessionOperation(matchState, context);
            if (!validation.Succeeded) return validation;
            PlayerId buyer = context.RequestedByPlayerId;
            if (!state.ContainsPlayer(buyer))
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.PlayerIsNotParticipant);
            if (!state.IsBuyingPlayer(buyer))
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);
            if (state.IsFloorSetupPending && state.WouldEndBuyPhase(buyer))
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.FloorSetupPending);
            state.MarkFinishedBuying(buyer);
            long revision = matchState.AdvanceRevision(
                context.Id,
                buyer,
                AuthoritativeActionKind.Unspecified);
            return TrapFloorTurnAdvanceResult.Accepted(revision, 0);
        }

        /// <summary>
        /// Stage 2: the players have laid the floor out; from now on each cell is the tile they put there. Every computer
        /// runs it the same way (only the host, or one computer offline, sends it). Every floor tile must still be face
        /// down and lie loose on the table.
        /// </summary>
        public TrapFloorTurnAdvanceResult ArrangeFloor(MatchState matchState, TrapFloorArrangeFloorCommand command)
        {
            if (command == null) return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.FloorArrangementInvalid);
            TrapFloorTurnAdvanceResult validation = ValidateSessionOperation(matchState, command.Context);
            if (!validation.Succeeded) return validation;
            if (!state.IsFloorSetupPending)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.FloorSetupNotPending);
            if (!template.IsFloorArrangement(command.CardIdsByCell))
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.FloorArrangementInvalid);
            for (int i = 0; i < command.CardIdsByCell.Length; i++)
            {
                if (!matchState.Cards.TryGetValue(command.CardIdsByCell[i], out CardInstanceState tile)
                    || tile.Face != CardFace.FaceDown
                    || !tile.BaseState.ContainerId.IsEmpty)
                    return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.FloorArrangementInvalid);
            }

            template.TryArrangeFloor(command.CardIdsByCell);
            state.CompleteFloorSetup(command.CardIdsByCell);
            long revision = matchState.AdvanceRevision(
                command.Context.Id,
                command.Context.RequestedByPlayerId,
                AuthoritativeActionKind.Unspecified);
            return TrapFloorTurnAdvanceResult.Accepted(revision, 0);
        }

        private TrapFloorTurnAdvanceResult Advance(
            MatchState matchState,
            CommandContext context,
            bool recordSkip)
        {
            TrapFloorTurnAdvanceResult validation = ValidateTurnActor(matchState, context);
            if (!validation.Succeeded) return validation;

            if (recordSkip)
            {
                activityFeed.RecordSkippedTurn(
                    checked(matchState.Revision + 1L),
                    context.RequestedByPlayerId);
            }

            bool startsNextRound = state.Phase == TrapFloorTurnPhase.FloorTurn && !state.IsFloorTurnBetweenPlayers;
            bool reactivatesPlayers = startsNextRound && state.IsCurrentFloorFailed;
            state.Advance();
            if (startsNextRound)
            {
                abilityResolutionState?.ExpireBlindBeforeRound(state.CurrentRound);
                abilityResolutionState?.ExpireSlowBeforeRound(state.CurrentRound);
                abilityResolutionState?.ExpireStickyBeforeRound(state.CurrentRound);
            }
            if (reactivatesPlayers)
            {
                activityFeed.RecordPlayersReactivated(
                    checked(matchState.Revision + 1L),
                    context.RequestedByPlayerId);
            }

            long revision = matchState.AdvanceRevision(
                context.Id,
                context.RequestedByPlayerId,
                AuthoritativeActionKind.Unspecified);
            return TrapFloorTurnAdvanceResult.Accepted(revision, 0);
        }

        private TrapFloorTurnAdvanceResult ValidateTurnActor(
            MatchState matchState,
            CommandContext context)
        {
            TrapFloorTurnAdvanceResult validation = ValidateSessionOperation(matchState, context);
            if (!validation.Succeeded) return validation;
            if (!state.IsCurrentFloorFailed && state.Phase == TrapFloorTurnPhase.BuyPhase)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);
            if (state.IsCurrentFloorFailed)
            {
                return state.Phase == TrapFloorTurnPhase.FloorTurn
                    && state.ContainsPlayer(context.RequestedByPlayerId)
                        ? TrapFloorTurnAdvanceResult.Accepted(-1, 0)
                        : TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.CurrentFloorFailed);
            }
            if (state.Phase == TrapFloorTurnPhase.PlayerTurn
                && context.RequestedByPlayerId != state.ActivePlayerId)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);
            if (state.Phase == TrapFloorTurnPhase.FloorTurn
                && context.RequestedByPlayerId != state.FloorOperatorPlayerId)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotFloorOperator);
            return TrapFloorTurnAdvanceResult.Accepted(-1, 0);
        }

        private TrapFloorTurnAdvanceResult ValidateSessionOperation(
            MatchState matchState,
            CommandContext context)
        {
            if (matchState == null || context.MatchId != matchState.Id || state.MatchId != matchState.Id)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.MatchMismatch);
            if (activityFeed.MatchId != matchState.Id)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActivityFeedMismatch);
            if (context.ExpectedRevision.HasValue && context.ExpectedRevision.Value != matchState.Revision)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.RevisionConflict);
            return TrapFloorTurnAdvanceResult.Accepted(-1, 0);
        }

        private bool TryGetPlayerSetup(
            MatchState matchState,
            PlayerId playerId,
            out TrapFloorPlayerSetupDefinition player)
        {
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition candidate = template.Players[i];
                if (matchState.Seats.TryGetValue(candidate.SeatId, out var seat)
                    && seat.OccupantPlayerId == playerId)
                {
                    player = candidate;
                    return true;
                }
            }

            player = null;
            return false;
        }
    }
}
