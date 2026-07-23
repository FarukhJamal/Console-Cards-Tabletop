using System;
using ConsoleCards.Presentation.Interaction;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class InteractionLockAcquireResultTests
    {
        [Test]
        public void Acquired_ReportsSuccessAndOwner()
        {
            InteractionOwnerId ownerId = OwnerId(1);

            InteractionLockAcquireResult result = InteractionLockAcquireResult.Acquired(ownerId);

            Assert.That(result.Status, Is.EqualTo(InteractionLockAcquireStatus.Acquired));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.CurrentOwnerId, Is.EqualTo(ownerId));
        }

        [Test]
        public void AlreadyOwned_ReportsSuccessAndOwner()
        {
            InteractionOwnerId ownerId = OwnerId(2);

            InteractionLockAcquireResult result = InteractionLockAcquireResult.AlreadyOwned(ownerId);

            Assert.That(result.Status, Is.EqualTo(InteractionLockAcquireStatus.AlreadyOwned));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.CurrentOwnerId, Is.EqualTo(ownerId));
        }

        [Test]
        public void Conflict_ReportsFailureAndCurrentOwner()
        {
            InteractionOwnerId ownerId = OwnerId(3);

            InteractionLockAcquireResult result = InteractionLockAcquireResult.Conflict(ownerId);

            Assert.That(result.Status, Is.EqualTo(InteractionLockAcquireStatus.Conflict));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.CurrentOwnerId, Is.EqualTo(ownerId));
        }

        [Test]
        public void Factories_WhenOwnerIsEmpty_ThrowArgumentException()
        {
            Assert.Throws<ArgumentException>(() => InteractionLockAcquireResult.Acquired(InteractionOwnerId.Empty));
            Assert.Throws<ArgumentException>(() => InteractionLockAcquireResult.AlreadyOwned(InteractionOwnerId.Empty));
            Assert.Throws<ArgumentException>(() => InteractionLockAcquireResult.Conflict(InteractionOwnerId.Empty));
        }

        [Test]
        public void EqualityHashCodeAndToString_BehaveCorrectly()
        {
            InteractionOwnerId ownerId = OwnerId(4);
            InteractionLockAcquireResult first = InteractionLockAcquireResult.Acquired(ownerId);
            InteractionLockAcquireResult matching = InteractionLockAcquireResult.Acquired(ownerId);
            InteractionLockAcquireResult differentStatus = InteractionLockAcquireResult.AlreadyOwned(ownerId);
            InteractionLockAcquireResult differentOwner = InteractionLockAcquireResult.Acquired(OwnerId(5));

            Assert.That(first.Equals(matching), Is.True);
            Assert.That(first.Equals((object)matching), Is.True);
            Assert.That(first == matching, Is.True);
            Assert.That(first != matching, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(matching.GetHashCode()));
            Assert.That(first.Equals(differentStatus), Is.False);
            Assert.That(first.Equals(differentOwner), Is.False);
            Assert.That(first == differentStatus, Is.False);
            Assert.That(first != differentOwner, Is.True);
            Assert.That(first.ToString(), Does.Contain(InteractionLockAcquireStatus.Acquired.ToString()));
            Assert.That(first.ToString(), Does.Contain(ownerId.ToString()));
            Assert.That(first.ToString(), Does.Contain("True"));
        }

        private static InteractionOwnerId OwnerId(int seed)
        {
            return new InteractionOwnerId(GuidFromSeed(seed));
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }
    }
}
