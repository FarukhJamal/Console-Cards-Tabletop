using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// AUTH-ET (owner approval, 2026-11-06): a joined computer's request to end the turn it plays. It carries only what
    /// the sender believes the turn is (round, phase, whose turn) and a request id; never who sends it. The host takes
    /// the sender from the connection it arrived on.
    /// </summary>
    public readonly struct TrapFloorEndTurnRequest
    {
        public TrapFloorEndTurnRequest(Guid requestId, int round, TrapFloorTurnPhase phase, PlayerId turnOwner, bool skipped)
        {
            RequestId = requestId;
            Round = round;
            Phase = phase;
            TurnOwner = turnOwner;
            Skipped = skipped;
        }

        public Guid RequestId { get; }
        public int Round { get; }
        public TrapFloorTurnPhase Phase { get; }
        public PlayerId TurnOwner { get; }
        public bool Skipped { get; }

        /// <summary>The request for the turn as this computer sees it now.</summary>
        public static TrapFloorEndTurnRequest For(TrapFloorTurnState state, bool skipped)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new TrapFloorEndTurnRequest(Guid.NewGuid(), state.CurrentRound, state.Phase, TrapFloorEndTurnAuthority.TurnOwnerOf(state), skipped);
        }
    }

    public enum TrapFloorEndTurnVerdict
    {
        Accepted = 0,
        /// <summary>This request id was already accepted once: it is not run again.</summary>
        Duplicate = 1,
        /// <summary>The connection has no game seat (a watcher).</summary>
        NotSeated = 2,
        /// <summary>The turn belongs to another player (or another floor operator).</summary>
        NotTurnOwner = 3,
        /// <summary>The turn has moved on since the request was made (a repeat, or a request crossing another change).</summary>
        StaleTurn = 4,
        /// <summary>The floor has ended (failed or won): no turn is ended any more.</summary>
        FloorEnded = 5,
        /// <summary>No Trap Floor turn on this table.</summary>
        NoTurn = 6,
        MissingRequestId = 7,
        /// <summary>P7: the request was the turn owner's, but the game's own rule refused to end the turn yet (Full: the
        /// floor turn still owes its collapse). Nothing changed.</summary>
        RuleRefused = 8,
    }

    /// <summary>
    /// AUTH-ET: the host's check of an End Turn request. Pure (no Unity, no network) so it is unit tested. The sender is
    /// the player the host's own seat table gives the connection, never a value read from the request. A request is
    /// accepted once: its id is remembered (the last <see cref="Remembered"/> accepted ids).
    /// </summary>
    public sealed class TrapFloorEndTurnAuthority
    {
        public const int Remembered = 256;

        private readonly HashSet<Guid> accepted = new HashSet<Guid>();
        private readonly Queue<Guid> order = new Queue<Guid>();

        public static PlayerId TurnOwnerOf(TrapFloorTurnState state) =>
            state == null
                ? PlayerId.Empty
                : state.Phase == TrapFloorTurnPhase.FloorTurn ? state.FloorOperatorPlayerId : state.ActivePlayerId;

        public TrapFloorEndTurnVerdict Check(
            TrapFloorTurnState state,
            PlayerId sender,
            TrapFloorEndTurnRequest request,
            bool floorWon = false)
        {
            if (request.RequestId == Guid.Empty) return TrapFloorEndTurnVerdict.MissingRequestId;
            if (accepted.Contains(request.RequestId)) return TrapFloorEndTurnVerdict.Duplicate;
            if (sender.IsEmpty) return TrapFloorEndTurnVerdict.NotSeated;
            if (state == null) return TrapFloorEndTurnVerdict.NoTurn;
            if (state.IsCurrentFloorFailed || floorWon) return TrapFloorEndTurnVerdict.FloorEnded;
            PlayerId owner = TurnOwnerOf(state);
            if (owner.IsEmpty) return TrapFloorEndTurnVerdict.NoTurn;
            if (owner != sender) return TrapFloorEndTurnVerdict.NotTurnOwner;
            if (request.Round != state.CurrentRound || request.Phase != state.Phase || request.TurnOwner != owner)
                return TrapFloorEndTurnVerdict.StaleTurn;
            Remember(request.RequestId);
            return TrapFloorEndTurnVerdict.Accepted;
        }

        public void Clear()
        {
            accepted.Clear();
            order.Clear();
        }

        private void Remember(Guid id)
        {
            accepted.Add(id);
            order.Enqueue(id);
            while (order.Count > Remembered) accepted.Remove(order.Dequeue());
        }
    }
}
