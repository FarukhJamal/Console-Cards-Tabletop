using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;

namespace ConsoleCards.Application.UseCases
{
    public sealed class FlipCardUseCase
    {
        public FlipCardResult Execute(MatchState matchState, FlipCardCommand command)
        {
            if (matchState == null)
            {
                return FlipCardResult.Failure(CommandResultStatus.Invalid, FlipCardError.MatchRequired);
            }

            if (command == null)
            {
                return FlipCardResult.Failure(CommandResultStatus.Invalid, FlipCardError.CommandRequired);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return FlipCardResult.Failure(CommandResultStatus.Invalid, FlipCardError.MatchIdMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return FlipCardResult.Failure(CommandResultStatus.Conflict, FlipCardError.RevisionConflict);
            }

            if (!matchState.ContainsObject(command.ObjectId))
            {
                return FlipCardResult.Failure(CommandResultStatus.Rejected, FlipCardError.ObjectNotFound);
            }

            if (!matchState.Cards.TryGetValue(command.ObjectId, out CardInstanceState card))
            {
                return FlipCardResult.Failure(CommandResultStatus.Rejected, FlipCardError.ObjectNotCard);
            }

            if (card.BaseState.IsUserLocked)
            {
                return FlipCardResult.Failure(CommandResultStatus.Rejected, FlipCardError.ObjectUserLocked);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return FlipCardResult.Failure(CommandResultStatus.Conflict, FlipCardError.RevisionOverflow);
            }

            card.SetFace(command.TargetFace);
            long revision = matchState.AdvanceRevision();

            return FlipCardResult.Accepted(revision);
        }
    }
}
