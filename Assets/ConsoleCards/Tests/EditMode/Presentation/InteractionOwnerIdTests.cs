using System;
using ConsoleCards.Presentation.Interaction;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class InteractionOwnerIdTests
    {
        [Test]
        public void New_CreatesNonEmptyOwnerId()
        {
            InteractionOwnerId ownerId = InteractionOwnerId.New();

            Assert.That(ownerId.IsEmpty, Is.False);
            Assert.That(ownerId.Value, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void New_WhenCalledTwice_CreatesDifferentValues()
        {
            InteractionOwnerId first = InteractionOwnerId.New();
            InteractionOwnerId second = InteractionOwnerId.New();

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void EqualGuidValues_CompareEqual()
        {
            Guid value = GuidFromSeed(1);

            InteractionOwnerId first = new InteractionOwnerId(value);
            InteractionOwnerId second = new InteractionOwnerId(value);

            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void Empty_ReportsIsEmpty()
        {
            InteractionOwnerId ownerId = InteractionOwnerId.Empty;

            Assert.That(ownerId.IsEmpty, Is.True);
            Assert.That(ownerId.Value, Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void TryParse_WhenGuidIsValid_ReturnsTrue()
        {
            Guid value = GuidFromSeed(2);

            bool parsed = InteractionOwnerId.TryParse(value.ToString(), out InteractionOwnerId result);

            Assert.That(parsed, Is.True);
            Assert.That(result, Is.EqualTo(new InteractionOwnerId(value)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-a-guid")]
        public void TryParse_WhenValueIsInvalid_ReturnsFalseAndEmpty(string value)
        {
            bool parsed = InteractionOwnerId.TryParse(value, out InteractionOwnerId result);

            Assert.That(parsed, Is.False);
            Assert.That(result, Is.EqualTo(InteractionOwnerId.Empty));
        }

        [Test]
        public void ToString_ReturnsWrappedGuidRepresentation()
        {
            Guid value = GuidFromSeed(3);
            InteractionOwnerId ownerId = new InteractionOwnerId(value);

            Assert.That(ownerId.ToString(), Is.EqualTo(value.ToString()));
        }

        [Test]
        public void EqualityOperatorsAndHashCodes_AreConsistent()
        {
            InteractionOwnerId first = new InteractionOwnerId(GuidFromSeed(4));
            InteractionOwnerId matching = new InteractionOwnerId(GuidFromSeed(4));
            InteractionOwnerId different = new InteractionOwnerId(GuidFromSeed(5));

            Assert.That(first == matching, Is.True);
            Assert.That(first != matching, Is.False);
            Assert.That(first == different, Is.False);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(matching.GetHashCode()));
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }
    }
}
