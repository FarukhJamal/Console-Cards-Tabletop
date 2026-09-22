using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.GameTemplates.ControllerInputs
{
    public enum ControllerInputHandDrawError
    {
        None = 0,
        MatchMissing = 1,
        GameDefinitionMissing = 2,
        MatchMismatch = 3,
        RevisionConflict = 4,
        SeatMissing = 5,
        ActorDoesNotOwnSeat = 6,
        HandMissing = 7,
        SourceDeckMissing = 8,
        SourceNotDeck = 9,
        SourceDeckOwnedByAnotherSeat = 10,
        InvalidControllerConfiguration = 11,
        DrawUpToDisabled = 12,
        CarryOverRequiresHandResolution = 13,
        HandCapacityBelowConfiguredMaximum = 14,
        InvalidControllerCardDefinition = 15,
        SourceContainsNonControllerCard = 16,
        DrawRejected = 17,
    }

    public readonly struct ControllerInputHandDrawResult
    {
        private ControllerInputHandDrawResult(
            bool succeeded,
            bool changed,
            long revision,
            int drawnCount,
            ControllerInputHandDrawError error)
        {
            Succeeded = succeeded;
            Changed = changed;
            Revision = revision;
            DrawnCount = drawnCount;
            Error = error;
        }

        public bool Succeeded { get; }
        public bool Changed { get; }
        public long Revision { get; }
        public int DrawnCount { get; }
        public ControllerInputHandDrawError Error { get; }

        public static ControllerInputHandDrawResult Accepted(long revision, int drawnCount)
        {
            if (drawnCount < 1) throw new ArgumentOutOfRangeException(nameof(drawnCount));
            return new ControllerInputHandDrawResult(true, true, revision, drawnCount, ControllerInputHandDrawError.None);
        }

        public static ControllerInputHandDrawResult NoChange(long revision)
        {
            return new ControllerInputHandDrawResult(true, false, revision, 0, ControllerInputHandDrawError.None);
        }

        public static ControllerInputHandDrawResult Failure(ControllerInputHandDrawError error)
        {
            if (error == ControllerInputHandDrawError.None)
                throw new ArgumentException("A failed Hand draw requires an error.", nameof(error));
            return new ControllerInputHandDrawResult(false, false, -1, 0, error);
        }
    }

    public sealed class DrawUpToConfiguredHandLimitCommand
    {
        public DrawUpToConfiguredHandLimitCommand(
            CommandContext context,
            SeatId playerSeatId,
            ContainerId sourceControllerDeckId)
        {
            if (playerSeatId.IsEmpty) throw new ArgumentException("Player Seat ID is required.", nameof(playerSeatId));
            if (sourceControllerDeckId.IsEmpty) throw new ArgumentException("Controller Deck ID is required.", nameof(sourceControllerDeckId));
            Context = context;
            PlayerSeatId = playerSeatId;
            SourceControllerDeckId = sourceControllerDeckId;
        }

        public CommandContext Context { get; }
        public SeatId PlayerSeatId { get; }
        public ContainerId SourceControllerDeckId { get; }
    }

    /// <summary>
    /// Performs an explicit draw-up-to request. It does not schedule turns. A zero-card draw is a
    /// successful no-op and therefore produces no revision or Undo transaction.
    /// </summary>
    public sealed class ControllerInputHandService
    {
        public ControllerInputHandDrawResult DrawUpToConfiguredHandLimit(
            MatchState matchState,
            GameDefinitionData gameDefinition,
            DrawUpToConfiguredHandLimitCommand command)
        {
            if (matchState == null)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.MatchMissing);
            if (gameDefinition == null)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.GameDefinitionMissing);
            if (command == null)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.MatchMismatch);
            if (command.Context.MatchId != matchState.Id)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.MatchMismatch);
            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.RevisionConflict);
            }

            if (!matchState.Seats.TryGetValue(command.PlayerSeatId, out SeatState seat))
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.SeatMissing);
            if (seat.OccupantPlayerId != command.Context.RequestedByPlayerId)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.ActorDoesNotOwnSeat);
            if (!matchState.TryGetSeatHand(command.PlayerSeatId, out ContainerState hand))
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.HandMissing);
            if (!matchState.Containers.TryGetValue(command.SourceControllerDeckId, out ContainerState sourceDeck))
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.SourceDeckMissing);
            if (sourceDeck.Kind != ContainerKind.Deck)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.SourceNotDeck);
            if (sourceDeck.OwnerSeatId != command.PlayerSeatId)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.SourceDeckOwnedByAnotherSeat);

            ControllerConfigurationData configuration = gameDefinition.ControllerConfiguration;
            if (configuration == null || configuration.MaximumHandSize < 0)
            {
                return ControllerInputHandDrawResult.Failure(
                    ControllerInputHandDrawError.InvalidControllerConfiguration);
            }

            if (!configuration.DrawToMaximumAtTurnStart)
                return ControllerInputHandDrawResult.Failure(ControllerInputHandDrawError.DrawUpToDisabled);
            if (!configuration.UnusedCardsCarryOver && hand.Count > 0)
            {
                return ControllerInputHandDrawResult.Failure(
                    ControllerInputHandDrawError.CarryOverRequiresHandResolution);
            }

            if (hand.Capacity > 0 && hand.Capacity < configuration.MaximumHandSize)
            {
                return ControllerInputHandDrawResult.Failure(
                    ControllerInputHandDrawError.HandCapacityBelowConfiguredMaximum);
            }

            int requestedCount = Math.Max(0, configuration.MaximumHandSize - hand.Count);
            int drawCount = Math.Min(requestedCount, sourceDeck.Count);
            if (drawCount == 0)
                return ControllerInputHandDrawResult.NoChange(matchState.Revision);

            if (!ControllerInputCardCatalog.TryCreate(gameDefinition, out ControllerInputCardCatalog catalog))
            {
                return ControllerInputHandDrawResult.Failure(
                    ControllerInputHandDrawError.InvalidControllerCardDefinition);
            }

            for (int offset = 0; offset < drawCount; offset++)
            {
                TabletopObjectId objectId = sourceDeck.GetObjectAt(sourceDeck.Count - 1 - offset);
                if (!matchState.Cards.TryGetValue(objectId, out CardInstanceState card)
                    || !catalog.TryGetInput(card.BaseState.DefinitionId, out _))
                {
                    return ControllerInputHandDrawResult.Failure(
                        ControllerInputHandDrawError.SourceContainsNonControllerCard);
                }
            }

            DrawCardsResult result = new DrawCardsUseCase().Execute(
                matchState,
                new DrawCardsCommand(
                    command.Context,
                    command.SourceControllerDeckId,
                    hand.Id,
                    drawCount));
            return result.Succeeded
                ? ControllerInputHandDrawResult.Accepted(result.Revision, drawCount)
                : ControllerInputHandDrawResult.Failure(MapDrawError(result.Status));
        }

        private static ControllerInputHandDrawError MapDrawError(CommandResultStatus status)
        {
            return status == CommandResultStatus.Conflict
                ? ControllerInputHandDrawError.RevisionConflict
                : ControllerInputHandDrawError.DrawRejected;
        }
    }
}
