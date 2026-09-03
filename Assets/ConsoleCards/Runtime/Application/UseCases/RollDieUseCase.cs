using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Randomness;

namespace ConsoleCards.Application.UseCases
{
    public enum RollDieError
    {
        None,
        MatchRequired,
        RequestRequired,
        MatchIdMismatch,
        RevisionConflict,
        ActorNotActive,
        DieNotFound,
        RevisionOverflow,
        PhysicalSimulationRequired,
    }

    public sealed class RollDieRequest
    {
        public RollDieRequest(CommandContext context, TabletopObjectId dieId)
        {
            if (dieId.IsEmpty)
            {
                throw new ArgumentException("Die ID cannot be empty.", nameof(dieId));
            }

            Context = context;
            DieId = dieId;
        }

        public CommandContext Context { get; }
        public TabletopObjectId DieId { get; }
    }

    public readonly struct RollDieResult
    {
        private RollDieResult(CommandResult commandResult, RollDieError error, DieRoll roll)
        {
            CommandResult = commandResult;
            Error = error;
            Roll = roll;
        }

        public CommandResult CommandResult { get; }
        public RollDieError Error { get; }
        public DieRoll Roll { get; }
        public bool Succeeded => CommandResult.Succeeded;
        public long Revision => CommandResult.Revision;

        internal static RollDieResult Accepted(long revision, DieRoll roll)
        {
            return new RollDieResult(CommandResult.Accepted(revision), RollDieError.None, roll);
        }

        internal static RollDieResult Failure(CommandResultStatus status, RollDieError error)
        {
            return new RollDieResult(CommandResult.Failure(status), error, default);
        }
    }

    /// <summary>
    /// Validates an actor request, chooses an authoritative result, then commits it to one Die.
    /// </summary>
    public sealed class RollDieUseCase
    {
        private readonly IRandomValueSource randomValueSource;

        public RollDieUseCase(IRandomValueSource randomValueSource)
        {
            this.randomValueSource = randomValueSource ?? throw new ArgumentNullException(nameof(randomValueSource));
        }

        public RollDieResult Execute(
            MatchState matchState,
            IReadOnlyList<PlayerId> activePlayerIds,
            RollDieRequest request)
        {
            if (matchState == null)
            {
                return RollDieResult.Failure(CommandResultStatus.Invalid, RollDieError.MatchRequired);
            }

            if (request == null)
            {
                return RollDieResult.Failure(CommandResultStatus.Invalid, RollDieError.RequestRequired);
            }

            if (request.Context.MatchId != matchState.Id)
            {
                return RollDieResult.Failure(CommandResultStatus.Invalid, RollDieError.MatchIdMismatch);
            }

            if (request.Context.ExpectedRevision.HasValue
                && request.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return RollDieResult.Failure(CommandResultStatus.Conflict, RollDieError.RevisionConflict);
            }

            if (!Contains(activePlayerIds, request.Context.RequestedByPlayerId))
            {
                return RollDieResult.Failure(CommandResultStatus.Rejected, RollDieError.ActorNotActive);
            }

            if (!matchState.Dice.TryGetValue(request.DieId, out DieState dieState))
            {
                return RollDieResult.Failure(CommandResultStatus.Rejected, RollDieError.DieNotFound);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return RollDieResult.Failure(CommandResultStatus.Conflict, RollDieError.RevisionOverflow);
            }

            if (dieState.BaseState.PhysicalState != null)
                return RollDieResult.Failure(CommandResultStatus.Rejected, RollDieError.PhysicalSimulationRequired);

            DieRoll roll = new Die(dieState.SideCount).Roll(randomValueSource);
            dieState.SetAcceptedRoll(roll);
            long revision = matchState.AdvanceRevision();
            return RollDieResult.Accepted(revision, roll);
        }

        private static bool Contains(IReadOnlyList<PlayerId> players, PlayerId playerId)
        {
            if (players == null)
            {
                return false;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == playerId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
