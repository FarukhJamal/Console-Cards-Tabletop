using System;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class IdentifierTests
    {
        [Test]
        public void TabletopObjectId_New_CreatesNonEmptyValue()
        {
            TabletopObjectId id = TabletopObjectId.New();

            Assert.That(id.IsEmpty, Is.False);
            Assert.That(id.Value, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void TabletopObjectId_New_CreatesDifferentValues()
        {
            TabletopObjectId first = TabletopObjectId.New();
            TabletopObjectId second = TabletopObjectId.New();

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first != second, Is.True);
        }

        [Test]
        public void TabletopObjectId_WhenWrappedGuidMatches_ComparesEqual()
        {
            Guid guid = Guid.NewGuid();
            TabletopObjectId first = new TabletopObjectId(guid);
            TabletopObjectId second = new TabletopObjectId(guid);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void TabletopObjectId_Empty_HasIsEmptyTrue()
        {
            TabletopObjectId id = TabletopObjectId.Empty;

            Assert.That(id.IsEmpty, Is.True);
            Assert.That(id.Value, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void TabletopObjectId_TryParse_AcceptsValidGuidString()
        {
            Guid guid = Guid.NewGuid();

            bool parsed = TabletopObjectId.TryParse(guid.ToString(), out TabletopObjectId result);

            Assert.That(parsed, Is.True);
            Assert.That(result.Value, Is.EqualTo(guid));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-guid")]
        public void TabletopObjectId_TryParse_RejectsInvalidInput(string value)
        {
            bool parsed = TabletopObjectId.TryParse(value, out TabletopObjectId result);

            Assert.That(parsed, Is.False);
            Assert.That(result, Is.EqualTo(TabletopObjectId.Empty));
        }

        [Test]
        public void TabletopObjectId_ToString_ReturnsWrappedGuidRepresentation()
        {
            Guid guid = Guid.NewGuid();
            TabletopObjectId id = new TabletopObjectId(guid);

            Assert.That(id.ToString(), Is.EqualTo(guid.ToString()));
        }

        [Test]
        public void GameTemplateId_WhenWrappedGuidMatches_ComparesEqual()
        {
            Guid guid = Guid.NewGuid();

            Assert.That(new GameTemplateId(guid), Is.EqualTo(new GameTemplateId(guid)));
        }

        [Test]
        public void MatchId_WhenWrappedGuidMatches_ComparesEqual()
        {
            Guid guid = Guid.NewGuid();

            Assert.That(new MatchId(guid), Is.EqualTo(new MatchId(guid)));
        }

        [Test]
        public void PlayerId_WhenWrappedGuidMatches_ComparesEqual()
        {
            Guid guid = Guid.NewGuid();

            Assert.That(new PlayerId(guid), Is.EqualTo(new PlayerId(guid)));
        }

        [Test]
        public void SeatId_WhenWrappedGuidMatches_ComparesEqual()
        {
            Guid guid = Guid.NewGuid();

            Assert.That(new SeatId(guid), Is.EqualTo(new SeatId(guid)));
        }

        [Test]
        public void ObjectDefinitionId_WhenWrappedGuidMatches_ComparesEqual()
        {
            Guid guid = Guid.NewGuid();

            Assert.That(new ObjectDefinitionId(guid), Is.EqualTo(new ObjectDefinitionId(guid)));
        }

        [Test]
        public void ContainerId_WhenWrappedGuidMatches_ComparesEqual()
        {
            Guid guid = Guid.NewGuid();

            Assert.That(new ContainerId(guid), Is.EqualTo(new ContainerId(guid)));
        }

        [Test]
        public void PlayAreaId_WhenWrappedGuidMatches_ComparesEqual()
        {
            Guid guid = Guid.NewGuid();

            Assert.That(new PlayAreaId(guid), Is.EqualTo(new PlayAreaId(guid)));
        }

        [Test]
        public void CommandId_WhenWrappedGuidMatches_ComparesEqual()
        {
            Guid guid = Guid.NewGuid();

            Assert.That(new CommandId(guid), Is.EqualTo(new CommandId(guid)));
        }
    }
}
