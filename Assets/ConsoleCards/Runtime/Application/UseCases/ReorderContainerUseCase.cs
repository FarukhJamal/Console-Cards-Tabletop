using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    public sealed class ReorderContainerUseCase
    {
        public ReorderContainerResult Execute(MatchState matchState, ReorderContainerCommand command)
        {
            if (matchState == null)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Invalid, ReorderContainerError.MatchMissing);
            }

            if (command == null)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Invalid, ReorderContainerError.CommandMissing);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Invalid, ReorderContainerError.MatchMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Conflict, ReorderContainerError.RevisionConflict);
            }

            if (!matchState.Containers.TryGetValue(command.ContainerId, out ContainerState container))
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ContainerMissing);
            }

            if (command.FromIndex < 0 || command.FromIndex >= container.Count)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Invalid, ReorderContainerError.InvalidFromIndex);
            }

            if (command.ToIndex < 0 || command.ToIndex >= container.Count)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Invalid, ReorderContainerError.InvalidToIndex);
            }

            if (!matchState.ContainsObject(command.ObjectId))
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing);
            }

            TabletopObjectState objectState = matchState.GetObject(command.ObjectId);
            if (objectState.ContainerId != command.ContainerId)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectContainerMismatch);
            }

            if (!container.Contains(command.ObjectId))
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMembershipMissing);
            }

            if (container.GetObjectAt(command.FromIndex) != command.ObjectId)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectIndexMismatch);
            }

            if (objectState.IsUserLocked)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectUserLocked);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return ReorderContainerResult.Failure(CommandResultStatus.Conflict, ReorderContainerError.RevisionOverflow);
            }

            List<TabletopObjectId> originalOrder = new List<TabletopObjectId>(container.ObjectIds);

            try
            {
                container.Reorder(command.FromIndex, command.ToIndex);
                long revision = matchState.AdvanceRevision();
                return ReorderContainerResult.Accepted(revision);
            }
            catch
            {
                container.ReplaceOrder(originalOrder);
                throw;
            }
        }
    }
}
