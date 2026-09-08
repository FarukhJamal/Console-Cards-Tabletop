using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Randomness;
using ConsoleCards.GameTemplates;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests.PlayMode.Games.TrapFloor
{
    public sealed class TrapFloorHiddenContentTests
    {
        [Test]
        public void Factory_AssignsCompleteStage03PoolToThirtySixUnrevealedFloorCards()
        {
            TrapFloorTemplateDefinition template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                new CyclingRandomValueSource());
            MatchState match = CreateMatch(template);
            Dictionary<TrapFloorFloorContentCategory, int> counts =
                new Dictionary<TrapFloorFloorContentCategory, int>();

            foreach (TabletopObjectId floorCardId in template.FloorCardIds.Values)
            {
                Assert.That(
                    template.TryGetFloorCardState(match, floorCardId, out TrapFloorFloorCardState floorCard),
                    Is.True);
                Assert.That(floorCard.IsRevealed, Is.False);
                Assert.That(match.Cards[floorCardId].Face, Is.EqualTo(CardFace.FaceDown));
                counts.TryGetValue(floorCard.Content.Category, out int count);
                counts[floorCard.Content.Category] = count + 1;
            }

            Assert.That(template.FloorCardIds.Count, Is.EqualTo(36));
            Assert.That(counts[TrapFloorFloorContentCategory.Trap], Is.EqualTo(18));
            Assert.That(counts[TrapFloorFloorContentCategory.Friend], Is.EqualTo(4));
            Assert.That(counts[TrapFloorFloorContentCategory.Key], Is.EqualTo(6));
            Assert.That(counts[TrapFloorFloorContentCategory.SecretExit], Is.EqualTo(1));
            Assert.That(counts[TrapFloorFloorContentCategory.Entry], Is.EqualTo(1));
            Assert.That(counts[TrapFloorFloorContentCategory.Ability], Is.EqualTo(6));
        }

        [Test]
        public void Reset_RestoresAcceptedFloorAssignmentWithoutLegacyFloormasterOrCoinState()
        {
            TrapFloorTemplateDefinition template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                new CyclingRandomValueSource());
            GameTemplateMatchBuildResult build = template.TryCreateMatch(CreatePlayers(), MatchId.New());
            Assert.That(build.Succeeded, Is.True);

            MatchState initial = build.Session.CurrentMatch;
            MatchState reset = build.Session.Reset();
            foreach (TabletopObjectId floorCardId in template.FloorCardIds.Values)
            {
                Assert.That(
                    reset.Cards[floorCardId].BaseState.DefinitionId,
                    Is.EqualTo(initial.Cards[floorCardId].BaseState.DefinitionId));
                Assert.That(reset.Cards[floorCardId].Face, Is.EqualTo(CardFace.FaceDown));
            }

            Assert.That(reset.Tokens.Count, Is.EqualTo(0));
            Assert.That(template.FloormasterCardIds, Is.Empty);
            Assert.That(template.FloormasterDeckId.IsEmpty, Is.True);
            Assert.That(template.FloormasterDiscardId.IsEmpty, Is.True);
            Assert.That(template.SharedCoinSupplyId.IsEmpty, Is.True);
        }

        [Test]
        public void SearchReveal_RevealsExactFloorOnceAndRecordsActorObjectActivity()
        {
            TrapFloorTemplateDefinition template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                new CyclingRandomValueSource());
            IReadOnlyList<PlayerId> players = CreatePlayers();
            GameTemplateMatchBuildResult build = template.TryCreateMatch(players, MatchId.New());
            Assert.That(build.Succeeded, Is.True);
            MatchState match = build.Session.CurrentMatch;
            TabletopObjectId floorCardId = FirstFloorCardId(template);
            ObjectDefinitionId assignedContentId = match.Cards[floorCardId].BaseState.DefinitionId;
            long startingRevision = match.Revision;
            TrapFloorActivityFeedState activityFeed = new TrapFloorActivityFeedState(match.Id);
            TrapFloorRevealFloorUseCase useCase = new TrapFloorRevealFloorUseCase(template, activityFeed);

            TrapFloorRevealFloorResult result = useCase.Execute(
                match,
                new TrapFloorRevealFloorCommand(
                    new CommandContext(CommandId.New(), match.Id, players[0], match.Revision),
                    floorCardId));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Revision, Is.EqualTo(startingRevision + 1));
            Assert.That(match.Revision, Is.EqualTo(startingRevision + 1));
            Assert.That(match.Cards[floorCardId].Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(match.Cards[floorCardId].BaseState.DefinitionId, Is.EqualTo(assignedContentId));
            Assert.That(activityFeed.Entries.Count, Is.EqualTo(2));
            Assert.That(activityFeed.Entries[0].Kind, Is.EqualTo(TrapFloorActivityKind.SearchedFloor));
            Assert.That(activityFeed.Entries[1].Kind, Is.EqualTo(TrapFloorActivityKind.RevealedFloorContent));
            Assert.That(activityFeed.Entries[0].ActorPlayerId, Is.EqualTo(players[0]));
            Assert.That(activityFeed.Entries[1].FloorCardId, Is.EqualTo(floorCardId));
            Assert.That(activityFeed.Entries[1].ContentDefinitionId, Is.EqualTo(assignedContentId));

            TrapFloorRevealFloorResult repeated = useCase.Execute(
                match,
                new TrapFloorRevealFloorCommand(
                    new CommandContext(CommandId.New(), match.Id, players[0], match.Revision),
                    floorCardId));

            Assert.That(repeated.Succeeded, Is.False);
            Assert.That(repeated.CommandResult.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(repeated.Error, Is.EqualTo(TrapFloorRevealFloorError.FloorAlreadyRevealed));
            Assert.That(match.Revision, Is.EqualTo(startingRevision + 1));
            Assert.That(activityFeed.Entries.Count, Is.EqualTo(2));

            MatchState reset = build.Session.Reset();
            activityFeed.Clear();
            Assert.That(reset.Cards[floorCardId].Face, Is.EqualTo(CardFace.FaceDown));
            Assert.That(reset.Cards[floorCardId].BaseState.DefinitionId, Is.EqualTo(assignedContentId));
            Assert.That(activityFeed.Entries, Is.Empty);
        }

        [Test]
        public void Objective_ClaimsConfiguredKeysThenAcceptsOneAuthoritativeEscape()
        {
            TrapFloorTemplateDefinition template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                new CyclingRandomValueSource(),
                new TrapFloorStage03Configuration(2));
            IReadOnlyList<PlayerId> players = CreatePlayers();
            GameTemplateMatchBuildResult build = template.TryCreateMatch(players, MatchId.New());
            Assert.That(build.Succeeded, Is.True);
            MatchState match = build.Session.CurrentMatch;
            TrapFloorActivityFeedState activityFeed = new TrapFloorActivityFeedState(match.Id);
            TrapFloorRevealFloorUseCase reveal = new TrapFloorRevealFloorUseCase(template, activityFeed);
            TrapFloorObjectiveState objective = new TrapFloorObjectiveState(
                match.Id,
                template.Stage03Configuration.RequiredKeyCount);
            TrapFloorObjectiveUseCase objectiveUseCase = new TrapFloorObjectiveUseCase(
                template,
                objective,
                activityFeed);
            List<TabletopObjectId> keyIds = FindFloorCards(
                template,
                match,
                TrapFloorFloorContentCategory.Key);
            TabletopObjectId exitId = FindFloorCards(
                template,
                match,
                TrapFloorFloorContentCategory.SecretExit)[0];

            Reveal(reveal, match, players[0], exitId);
            long beforeRejectedEscape = match.Revision;
            TrapFloorObjectiveResult rejectedEscape = objectiveUseCase.AttemptEscape(
                match,
                new TrapFloorAttemptEscapeCommand(
                    CommandFor(match, players[0]),
                    exitId));
            Assert.That(rejectedEscape.Succeeded, Is.False);
            Assert.That(rejectedEscape.Error, Is.EqualTo(TrapFloorObjectiveError.RequiredKeysMissing));
            Assert.That(rejectedEscape.CollectedKeyCount, Is.EqualTo(0));
            Assert.That(rejectedEscape.RequiredKeyCount, Is.EqualTo(2));
            Assert.That(match.Revision, Is.EqualTo(beforeRejectedEscape));

            for (int i = 0; i < objective.RequiredKeyCount; i++)
            {
                Reveal(reveal, match, players[i], keyIds[i]);
                ObjectDefinitionId assignedKeyId = match.Cards[keyIds[i]].BaseState.DefinitionId;
                long beforeClaim = match.Revision;
                TrapFloorObjectiveResult claim = objectiveUseCase.ClaimKey(
                    match,
                    new TrapFloorClaimKeyCommand(
                        CommandFor(match, players[i]),
                        keyIds[i]));
                Assert.That(claim.Succeeded, Is.True);
                Assert.That(match.Revision, Is.EqualTo(beforeClaim + 1));
                Assert.That(claim.ClaimedKey.ContentDefinitionId, Is.EqualTo(assignedKeyId));
                Assert.That(claim.ClaimedKey.ClaimedByPlayerId, Is.EqualTo(players[i]));
                Assert.That(match.Cards[keyIds[i]].Face, Is.EqualTo(CardFace.FaceUp));
                Assert.That(claim.Activity.Kind, Is.EqualTo(TrapFloorActivityKind.ClaimedKey));
                Assert.That(claim.Activity.FloorCardId, Is.EqualTo(keyIds[i]));
            }

            Assert.That(objective.CollectedKeyCount, Is.EqualTo(2));
            Assert.That(objective.HasRequiredKeys, Is.True);
            long beforeVictory = match.Revision;
            TrapFloorObjectiveResult victory = objectiveUseCase.AttemptEscape(
                match,
                new TrapFloorAttemptEscapeCommand(
                    CommandFor(match, players[3]),
                    exitId));
            Assert.That(victory.Succeeded, Is.True);
            Assert.That(match.Revision, Is.EqualTo(beforeVictory + 1));
            Assert.That(objective.IsWon, Is.True);
            Assert.That(objective.WinningPlayerId, Is.EqualTo(players[3]));
            Assert.That(objective.ExitFloorCardId, Is.EqualTo(exitId));
            Assert.That(victory.Activity.Kind, Is.EqualTo(TrapFloorActivityKind.WonGame));

            MatchState reset = build.Session.Reset();
            objective.Clear();
            activityFeed.Clear();
            Assert.That(objective.CollectedKeys, Is.Empty);
            Assert.That(objective.IsWon, Is.False);
            Assert.That(activityFeed.Entries, Is.Empty);
            Assert.That(reset.Cards[exitId].Face, Is.EqualTo(CardFace.FaceDown));
            Assert.That(reset.Cards[keyIds[0]].Face, Is.EqualTo(CardFace.FaceDown));
        }

        private static TabletopObjectId FirstFloorCardId(TrapFloorTemplateDefinition template)
        {
            foreach (TabletopObjectId floorCardId in template.FloorCardIds.Values)
            {
                return floorCardId;
            }

            Assert.Fail("Trap Floor Template has no Floor Cards.");
            return TabletopObjectId.Empty;
        }

        private static MatchState CreateMatch(TrapFloorTemplateDefinition template)
        {
            GameTemplateMatchBuildResult build = template.TryCreateMatch(CreatePlayers(), MatchId.New());
            Assert.That(build.Succeeded, Is.True);
            return build.Session.CurrentMatch;
        }

        private static List<TabletopObjectId> FindFloorCards(
            TrapFloorTemplateDefinition template,
            MatchState match,
            TrapFloorFloorContentCategory category)
        {
            List<TabletopObjectId> matches = new List<TabletopObjectId>();
            foreach (TabletopObjectId floorCardId in template.FloorCardIds.Values)
            {
                if (template.TryGetFloorCardState(match, floorCardId, out TrapFloorFloorCardState floorCard)
                    && floorCard.Content.Category == category)
                {
                    matches.Add(floorCardId);
                }
            }

            return matches;
        }

        private static void Reveal(
            TrapFloorRevealFloorUseCase useCase,
            MatchState match,
            PlayerId playerId,
            TabletopObjectId floorCardId)
        {
            TrapFloorRevealFloorResult result = useCase.Execute(
                match,
                new TrapFloorRevealFloorCommand(
                    CommandFor(match, playerId),
                    floorCardId));
            Assert.That(result.Succeeded, Is.True);
        }

        private static CommandContext CommandFor(MatchState match, PlayerId playerId)
        {
            return new CommandContext(CommandId.New(), match.Id, playerId, match.Revision);
        }

        private static IReadOnlyList<PlayerId> CreatePlayers()
        {
            return new[]
            {
                PlayerId.New(),
                PlayerId.New(),
                PlayerId.New(),
                PlayerId.New(),
            };
        }

        private sealed class CyclingRandomValueSource : IRandomValueSource
        {
            private int value;

            public int NextInt(int minimumInclusive, int maximumExclusive)
            {
                int range = maximumExclusive - minimumInclusive;
                int result = minimumInclusive + (value % range);
                value++;
                return result;
            }
        }
    }
}
