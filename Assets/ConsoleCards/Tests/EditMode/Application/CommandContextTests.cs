using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class CommandContextTests
    {
        [Test]
        public void Constructor_StoresAllValues()
        {
            CommandId id = CommandId.New();
            MatchId matchId = MatchId.New();
            PlayerId requestedByPlayerId = PlayerId.New();

            CommandContext context = new CommandContext(id, matchId, requestedByPlayerId, 7);

            Assert.That(context.Id, Is.EqualTo(id));
            Assert.That(context.MatchId, Is.EqualTo(matchId));
            Assert.That(context.RequestedByPlayerId, Is.EqualTo(requestedByPlayerId));
            Assert.That(context.ExpectedRevision, Is.EqualTo(7));
        }

        [Test]
        public void Constructor_WhenExpectedRevisionIsNull_AcceptsValue()
        {
            CommandContext context = CreateContext(expectedRevision: null);

            Assert.That(context.ExpectedRevision, Is.Null);
        }

        [Test]
        public void Constructor_WhenCommandIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateContext(id: CommandId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenMatchIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateContext(matchId: MatchId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenRequestedPlayerIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateContext(requestedByPlayerId: PlayerId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenExpectedRevisionIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => CreateContext(expectedRevision: -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WhenExpectedRevisionIsZero_AcceptsValue()
        {
            CommandContext context = CreateContext(expectedRevision: 0);

            Assert.That(context.ExpectedRevision, Is.EqualTo(0));
        }

        [Test]
        public void Equality_WhenValuesMatch_ComparesEqual()
        {
            CommandId id = CommandId.New();
            MatchId matchId = MatchId.New();
            PlayerId requestedByPlayerId = PlayerId.New();
            CommandContext first = new CommandContext(id, matchId, requestedByPlayerId, 3);
            CommandContext second = new CommandContext(id, matchId, requestedByPlayerId, 3);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void Equality_WhenValuesDiffer_ComparesUnequal()
        {
            CommandContext first = CreateContext(expectedRevision: 3);
            CommandContext differentId = new CommandContext(CommandId.New(), first.MatchId, first.RequestedByPlayerId, first.ExpectedRevision);
            CommandContext differentMatch = new CommandContext(first.Id, MatchId.New(), first.RequestedByPlayerId, first.ExpectedRevision);
            CommandContext differentPlayer = new CommandContext(first.Id, first.MatchId, PlayerId.New(), first.ExpectedRevision);
            CommandContext differentRevision = new CommandContext(first.Id, first.MatchId, first.RequestedByPlayerId, 4);

            Assert.That(first != differentId, Is.True);
            Assert.That(first != differentMatch, Is.True);
            Assert.That(first != differentPlayer, Is.True);
            Assert.That(first != differentRevision, Is.True);
        }

        [Test]
        public void ToString_IncludesUsefulIdentifierAndRevisionInformation()
        {
            CommandContext context = CreateContext(expectedRevision: 12);

            string text = context.ToString();

            Assert.That(text, Does.Contain(context.Id.ToString()));
            Assert.That(text, Does.Contain(context.MatchId.ToString()));
            Assert.That(text, Does.Contain(context.RequestedByPlayerId.ToString()));
            Assert.That(text, Does.Contain("12"));
        }

        private static CommandContext CreateContext(
            CommandId? id = null,
            MatchId? matchId = null,
            PlayerId? requestedByPlayerId = null,
            long? expectedRevision = 1)
        {
            return new CommandContext(
                id ?? CommandId.New(),
                matchId ?? MatchId.New(),
                requestedByPlayerId ?? PlayerId.New(),
                expectedRevision);
        }
    }
}
