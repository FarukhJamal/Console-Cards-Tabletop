using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;

namespace ConsoleCards.Application.UseCases
{
    public sealed class MoveObjectUseCase
    {
        public MoveObjectResult Execute(MatchState matchState, MoveObjectCommand command)
        {
            if (matchState == null)
            {
                return MoveObjectResult.Failure(CommandResultStatus.Invalid, MoveObjectError.MatchRequired);
            }

            if (command == null)
            {
                return MoveObjectResult.Failure(CommandResultStatus.Invalid, MoveObjectError.CommandRequired);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return MoveObjectResult.Failure(CommandResultStatus.Invalid, MoveObjectError.MatchIdMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return MoveObjectResult.Failure(CommandResultStatus.Conflict, MoveObjectError.RevisionConflict);
            }

            if (!matchState.ContainsObject(command.ObjectId))
            {
                return MoveObjectResult.Failure(CommandResultStatus.Rejected, MoveObjectError.ObjectNotFound);
            }

            TabletopObjectState objectState = matchState.GetObject(command.ObjectId);
            if (objectState.IsUserLocked)
            {
                return MoveObjectResult.Failure(CommandResultStatus.Rejected, MoveObjectError.ObjectUserLocked);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return MoveObjectResult.Failure(CommandResultStatus.Conflict, MoveObjectError.RevisionOverflow);
            }

            objectState.SetPose(command.TargetPose);
            long revision = matchState.AdvanceRevision();

            return MoveObjectResult.Accepted(revision);
        }
    }
}
