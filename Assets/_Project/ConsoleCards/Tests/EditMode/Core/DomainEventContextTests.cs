using System;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class DomainEventContextTests
    {
        [Test]
        public void Constructor_StoresMatchIdAndRevision()
        {
            MatchId matchId = MatchId.New();

            DomainEventContext context = new DomainEventContext(matchId, 6);

            Assert.That(context.MatchId, Is.EqualTo(matchId));
            Assert.That(context.Revision, Is.EqualTo(6));
        }

        [Test]
        public void Constructor_WhenMatchIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new DomainEventContext(MatchId.Empty, 0),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenRevisionIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => new DomainEventContext(MatchId.New(), -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WhenRevisionIsZero_AcceptsValue()
        {
            DomainEventContext context = new DomainEventContext(MatchId.New(), 0);

            Assert.That(context.Revision, Is.EqualTo(0));
        }

        [Test]
        public void Equality_WhenValuesMatch_ComparesEqual()
        {
            MatchId matchId = MatchId.New();
            DomainEventContext first = new DomainEventContext(matchId, 3);
            DomainEventContext second = new DomainEventContext(matchId, 3);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void Equality_WhenValuesDiffer_ComparesUnequal()
        {
            MatchId matchId = MatchId.New();
            DomainEventContext first = new DomainEventContext(matchId, 3);
            DomainEventContext differentMatch = new DomainEventContext(MatchId.New(), 3);
            DomainEventContext differentRevision = new DomainEventContext(matchId, 4);

            Assert.That(first != differentMatch, Is.True);
            Assert.That(first != differentRevision, Is.True);
        }

        [Test]
        public void ToString_ContainsUsefulValues()
        {
            DomainEventContext context = new DomainEventContext(MatchId.New(), 9);

            string text = context.ToString();

            Assert.That(text, Does.Contain(context.MatchId.ToString()));
            Assert.That(text, Does.Contain("9"));
        }
    }
}
