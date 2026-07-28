using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;

namespace ConsoleCards.Application.UseCases
{
    public sealed class DrawCardsUseCase
    {
        public DrawCardsResult Execute(MatchState matchState, DrawCardsCommand command)
        {
            if (matchState == null)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Invalid, DrawCardsError.MatchMissing);
            }

            if (command == null)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Invalid, DrawCardsError.CommandMissing);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Invalid, DrawCardsError.MatchMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Conflict, DrawCardsError.RevisionConflict);
            }

            if (command.Count <= 0)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Invalid, DrawCardsError.InvalidCount);
            }

            if (command.SourceDeckContainerId == command.DestinationContainerId)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Invalid, DrawCardsError.SameContainer);
            }

            if (!matchState.Containers.TryGetValue(command.SourceDeckContainerId, out ContainerState sourceDeck))
            {
                return DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.SourceContainerMissing);
            }

            if (sourceDeck.Kind != ContainerKind.Deck)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.SourceContainerNotDeck);
            }

            if (!matchState.Containers.TryGetValue(command.DestinationContainerId, out ContainerState destination))
            {
                return DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.DestinationContainerMissing);
            }

            if (sourceDeck.Count < command.Count)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.InsufficientCards);
            }

            if (destination.Capacity > 0 && destination.Count + command.Count > destination.Capacity)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.DestinationCapacityExceeded);
            }

            List<TabletopObjectId> drawnObjectIds = SelectDrawnObjectIds(sourceDeck, command.Count);
            Dictionary<TabletopObjectId, TabletopObjectState> selectedObjects =
                new Dictionary<TabletopObjectId, TabletopObjectState>();

            foreach (TabletopObjectId objectId in drawnObjectIds)
            {
                if (!matchState.ContainsObject(objectId))
                {
                    return DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.ObjectMissing);
                }

                TabletopObjectState objectState = matchState.GetObject(objectId);
                if (objectState.ContainerId != command.SourceDeckContainerId)
                {
                    return DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.ObjectContainerMismatch);
                }

                if (objectState.IsUserLocked)
                {
                    return DrawCardsResult.Failure(CommandResultStatus.Rejected, DrawCardsError.ObjectUserLocked);
                }

                selectedObjects.Add(objectId, objectState);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return DrawCardsResult.Failure(CommandResultStatus.Conflict, DrawCardsError.RevisionOverflow);
            }

            ContainerBatchTransferService transferService = new ContainerBatchTransferService();
            ContainerTransferResult transferResult = transferService.TransferOrdered(
                selectedObjects,
                sourceDeck,
                destination,
                drawnObjectIds);

            if (!transferResult.Succeeded)
            {
                return MapTransferFailure(transferResult.Error);
            }

            long revision = matchState.AdvanceRevision();
            return DrawCardsResult.Accepted(revision);
        }

        private static List<TabletopObjectId> SelectDrawnObjectIds(ContainerState sourceDeck, int count)
        {
            List<TabletopObjectId> drawnObjectIds = new List<TabletopObjectId>(count);

            for (int offset = 0; offset < count; offset++)
            {
                int index = sourceDeck.Count - 1 - offset;
                drawnObjectIds.Add(sourceDeck.GetObjectAt(index));
            }

            return drawnObjectIds;
        }

        private static DrawCardsResult MapTransferFailure(ContainerTransferError error)
        {
            switch (error)
            {
                case ContainerTransferError.DestinationFull:
                    return DrawCardsResult.Failure(
                        CommandResultStatus.Rejected,
                        DrawCardsError.DestinationCapacityExceeded);

                case ContainerTransferError.SourceDoesNotContainObject:
                    return DrawCardsResult.Failure(
                        CommandResultStatus.Rejected,
                        DrawCardsError.InsufficientCards);

                case ContainerTransferError.ObjectStateMissing:
                case ContainerTransferError.ObjectStateRequired:
                    return DrawCardsResult.Failure(
                        CommandResultStatus.Rejected,
                        DrawCardsError.ObjectMissing);

                case ContainerTransferError.SourceContainerMismatch:
                    return DrawCardsResult.Failure(
                        CommandResultStatus.Rejected,
                        DrawCardsError.ObjectContainerMismatch);

                case ContainerTransferError.SameContainer:
                    return DrawCardsResult.Failure(
                        CommandResultStatus.Invalid,
                        DrawCardsError.SameContainer);

                default:
                    return DrawCardsResult.Failure(
                        CommandResultStatus.Rejected,
                        DrawCardsError.ObjectContainerMismatch);
            }
        }
    }
}
