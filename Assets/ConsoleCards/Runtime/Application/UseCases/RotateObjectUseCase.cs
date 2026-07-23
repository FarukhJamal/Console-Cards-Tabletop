using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;

namespace ConsoleCards.Application.UseCases
{
    public sealed class RotateObjectUseCase
    {
        public RotateObjectResult Execute(MatchState matchState, RotateObjectCommand command)
        {
            if (matchState == null)
            {
                return RotateObjectResult.Failure(CommandResultStatus.Invalid, RotateObjectError.MatchRequired);
            }

            if (command == null)
            {
                return RotateObjectResult.Failure(CommandResultStatus.Invalid, RotateObjectError.CommandRequired);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return RotateObjectResult.Failure(CommandResultStatus.Invalid, RotateObjectError.MatchIdMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return RotateObjectResult.Failure(CommandResultStatus.Conflict, RotateObjectError.RevisionConflict);
            }

            if (!matchState.ContainsObject(command.ObjectId))
            {
                return RotateObjectResult.Failure(CommandResultStatus.Rejected, RotateObjectError.ObjectNotFound);
            }

            TabletopObjectState objectState = matchState.GetObject(command.ObjectId);
            if (objectState.IsUserLocked)
            {
                return RotateObjectResult.Failure(CommandResultStatus.Rejected, RotateObjectError.ObjectUserLocked);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return RotateObjectResult.Failure(CommandResultStatus.Conflict, RotateObjectError.RevisionOverflow);
            }

            TabletopPose currentPose = objectState.Pose;
            TabletopPose targetPose = new TabletopPose(
                currentPose.Position,
                command.TargetRotationDegrees,
                currentPose.Layer,
                currentPose.LocalOrder);

            objectState.SetPose(targetPose);
            long revision = matchState.AdvanceRevision();

            return RotateObjectResult.Accepted(revision);
        }
    }
}
