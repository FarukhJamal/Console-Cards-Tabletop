using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.UseCases
{
    public sealed class ShuffleDeckUseCase
    {
        /// <summary>
        /// Shuffles one Deck using deterministic Fisher-Yates over bottom-to-top container order.
        /// For index i from Count - 1 down to 1, a stable xorshift32 generator selects j in [0, i],
        /// then positions i and j are swapped.
        /// </summary>
        public ShuffleDeckResult Execute(MatchState matchState, ShuffleDeckCommand command)
        {
            if (matchState == null)
            {
                return ShuffleDeckResult.Failure(CommandResultStatus.Invalid, ShuffleDeckError.MatchMissing);
            }

            if (command == null)
            {
                return ShuffleDeckResult.Failure(CommandResultStatus.Invalid, ShuffleDeckError.CommandMissing);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return ShuffleDeckResult.Failure(CommandResultStatus.Invalid, ShuffleDeckError.MatchMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return ShuffleDeckResult.Failure(CommandResultStatus.Conflict, ShuffleDeckError.RevisionConflict);
            }

            if (!matchState.Containers.TryGetValue(command.DeckContainerId, out ContainerState deck))
            {
                return ShuffleDeckResult.Failure(CommandResultStatus.Rejected, ShuffleDeckError.ContainerMissing);
            }

            if (deck.Kind != ContainerKind.Deck)
            {
                return ShuffleDeckResult.Failure(CommandResultStatus.Rejected, ShuffleDeckError.ContainerNotDeck);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return ShuffleDeckResult.Failure(CommandResultStatus.Conflict, ShuffleDeckError.RevisionOverflow);
            }

            List<TabletopObjectId> shuffledObjectIds = new List<TabletopObjectId>(deck.ObjectIds);
            Shuffle(shuffledObjectIds, command.Seed);
            deck.ReplaceOrder(shuffledObjectIds);
            long revision = matchState.AdvanceRevision();

            return ShuffleDeckResult.Accepted(revision);
        }

        private static void Shuffle(List<TabletopObjectId> objectIds, int seed)
        {
            StableShuffleRandom random = new StableShuffleRandom(seed);

            for (int index = objectIds.Count - 1; index > 0; index--)
            {
                int swapIndex = random.NextInclusiveUpperExclusive(index + 1);
                TabletopObjectId objectId = objectIds[index];
                objectIds[index] = objectIds[swapIndex];
                objectIds[swapIndex] = objectId;
            }
        }
    }
}
