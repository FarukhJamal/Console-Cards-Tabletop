using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Randomness;

namespace ConsoleCards.Application.UseCases
{
    public sealed class ShuffleDeckUseCase
    {
        private readonly IRandomValueSource randomValueSource;

        public ShuffleDeckUseCase()
        {
        }

        public ShuffleDeckUseCase(IRandomValueSource randomValueSource)
        {
            this.randomValueSource = randomValueSource
                ?? throw new System.ArgumentNullException(nameof(randomValueSource));
        }

        /// <summary>
        /// Shuffles one Deck using Fisher-Yates over bottom-to-top container order. Normal requests
        /// consume the injected authoritative random source; explicitly seeded replay/legacy requests
        /// retain the stable deterministic generator.
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

            if (!command.Seed.HasValue && randomValueSource == null)
            {
                return ShuffleDeckResult.Failure(
                    CommandResultStatus.Invalid,
                    ShuffleDeckError.RandomSourceMissing);
            }

            List<TabletopObjectId> shuffledObjectIds = new List<TabletopObjectId>(deck.ObjectIds);
            if (command.Seed.HasValue)
            {
                ShuffleDeterministically(shuffledObjectIds, command.Seed.Value);
            }
            else
            {
                ShuffleAuthoritatively(shuffledObjectIds, randomValueSource);
            }

            deck.ReplaceOrder(shuffledObjectIds);
            long revision = matchState.AdvanceRevision();

            return ShuffleDeckResult.Accepted(revision);
        }

        private static void ShuffleDeterministically(List<TabletopObjectId> objectIds, int seed)
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

        private static void ShuffleAuthoritatively(
            List<TabletopObjectId> objectIds,
            IRandomValueSource randomValueSource)
        {
            for (int index = objectIds.Count - 1; index > 0; index--)
            {
                int swapIndex = randomValueSource.NextInt(0, index + 1);
                TabletopObjectId objectId = objectIds[index];
                objectIds[index] = objectIds[swapIndex];
                objectIds[swapIndex] = objectId;
            }
        }
    }
}
