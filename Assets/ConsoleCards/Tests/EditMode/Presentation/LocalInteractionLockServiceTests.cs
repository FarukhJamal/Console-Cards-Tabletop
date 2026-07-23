using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Interaction;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class LocalInteractionLockServiceTests
    {
        public enum ObjectIdOperation
        {
            IsLocked,
            IsOwnedBy,
            TryGetOwner,
            Acquire,
            Release,
            ReleaseObject
        }

        public enum OwnerIdOperation
        {
            IsOwnedBy,
            Acquire,
            Release,
            ReleaseAllForOwner
        }

        [Test]
        public void NewService_HasCountZero()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            Assert.That(service.Count, Is.EqualTo(0));
        }

        [Test]
        public void IsLocked_WhenObjectIsUnlocked_ReturnsFalse()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            Assert.That(service.IsLocked(ObjectId(1)), Is.False);
        }

        [Test]
        public void TryGetOwner_WhenObjectIsUnlocked_ReturnsFalseAndEmpty()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            bool found = service.TryGetOwner(ObjectId(1), out InteractionOwnerId ownerId);

            Assert.That(found, Is.False);
            Assert.That(ownerId, Is.EqualTo(InteractionOwnerId.Empty));
        }

        [Test]
        public void Acquire_WhenObjectIsUnlocked_ReturnsAcquired()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            InteractionLockAcquireResult result = service.Acquire(ObjectId(1), OwnerId(1));

            Assert.That(result.Status, Is.EqualTo(InteractionLockAcquireStatus.Acquired));
            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public void Acquire_StoresRequesterAsOwner()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);

            service.Acquire(objectId, ownerId);

            Assert.That(service.IsLocked(objectId), Is.True);
            Assert.That(service.IsOwnedBy(objectId, ownerId), Is.True);
            Assert.That(service.TryGetOwner(objectId, out InteractionOwnerId storedOwnerId), Is.True);
            Assert.That(storedOwnerId, Is.EqualTo(ownerId));
        }

        [Test]
        public void Acquire_WhenObjectIsUnlocked_IncrementsCountOnce()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            service.Acquire(ObjectId(1), OwnerId(1));

            Assert.That(service.Count, Is.EqualTo(1));
        }

        [Test]
        public void Acquire_WhenSameOwnerReacquires_ReturnsAlreadyOwned()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(objectId, ownerId);

            InteractionLockAcquireResult result = service.Acquire(objectId, ownerId);

            Assert.That(result.Status, Is.EqualTo(InteractionLockAcquireStatus.AlreadyOwned));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.CurrentOwnerId, Is.EqualTo(ownerId));
        }

        [Test]
        public void Acquire_WhenSameOwnerReacquires_DoesNotIncreaseCount()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(objectId, ownerId);

            service.Acquire(objectId, ownerId);

            Assert.That(service.Count, Is.EqualTo(1));
        }

        [Test]
        public void Acquire_WhenDifferentOwnerRequestsLockedObject_ReturnsConflict()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(objectId, ownerId);

            InteractionLockAcquireResult result = service.Acquire(objectId, OwnerId(2));

            Assert.That(result.Status, Is.EqualTo(InteractionLockAcquireStatus.Conflict));
            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        public void Acquire_WhenConflictOccurs_ReportsActualCurrentOwner()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(objectId, ownerId);

            InteractionLockAcquireResult result = service.Acquire(objectId, OwnerId(2));

            Assert.That(result.CurrentOwnerId, Is.EqualTo(ownerId));
        }

        [Test]
        public void Acquire_WhenConflictOccurs_DoesNotReplaceExistingOwner()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(objectId, ownerId);

            service.Acquire(objectId, OwnerId(2));

            Assert.That(service.IsOwnedBy(objectId, ownerId), Is.True);
        }

        [Test]
        public void Acquire_OneOwnerMayAcquireMultipleObjects()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            InteractionOwnerId ownerId = OwnerId(1);

            service.Acquire(ObjectId(1), ownerId);
            service.Acquire(ObjectId(2), ownerId);

            Assert.That(service.Count, Is.EqualTo(2));
            Assert.That(service.IsOwnedBy(ObjectId(1), ownerId), Is.True);
            Assert.That(service.IsOwnedBy(ObjectId(2), ownerId), Is.True);
        }

        [Test]
        public void Acquire_DifferentOwnersMayAcquireDifferentObjects()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId firstObjectId = ObjectId(1);
            TabletopObjectId secondObjectId = ObjectId(2);
            InteractionOwnerId firstOwnerId = OwnerId(1);
            InteractionOwnerId secondOwnerId = OwnerId(2);

            service.Acquire(firstObjectId, firstOwnerId);
            service.Acquire(secondObjectId, secondOwnerId);

            Assert.That(service.Count, Is.EqualTo(2));
            Assert.That(service.IsOwnedBy(firstObjectId, firstOwnerId), Is.True);
            Assert.That(service.IsOwnedBy(secondObjectId, secondOwnerId), Is.True);
        }

        [Test]
        public void Release_WhenRequesterOwnsObject_ReturnsTrueAndRemovesLock()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(objectId, ownerId);

            bool released = service.Release(objectId, ownerId);

            Assert.That(released, Is.True);
            Assert.That(service.IsLocked(objectId), Is.False);
        }

        [Test]
        public void Release_WhenSuccessful_DecrementsCount()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(objectId, ownerId);

            service.Release(objectId, ownerId);

            Assert.That(service.Count, Is.EqualTo(0));
        }

        [Test]
        public void Release_WhenObjectIsUnlocked_ReturnsFalse()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            bool released = service.Release(ObjectId(1), OwnerId(1));

            Assert.That(released, Is.False);
        }

        [Test]
        public void Release_WhenRequesterIsNotOwner_ReturnsFalse()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            service.Acquire(objectId, OwnerId(1));

            bool released = service.Release(objectId, OwnerId(2));

            Assert.That(released, Is.False);
        }

        [Test]
        public void Release_WhenRequesterIsNotOwner_PreservesCurrentOwner()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(objectId, ownerId);

            service.Release(objectId, OwnerId(2));

            Assert.That(service.IsOwnedBy(objectId, ownerId), Is.True);
            Assert.That(service.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseObject_RemovesRegardlessOfOwner()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            service.Acquire(objectId, OwnerId(1));

            bool released = service.ReleaseObject(objectId);

            Assert.That(released, Is.True);
            Assert.That(service.IsLocked(objectId), Is.False);
        }

        [Test]
        public void ReleaseObject_WhenObjectIsUnlocked_ReturnsFalse()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            bool released = service.ReleaseObject(ObjectId(1));

            Assert.That(released, Is.False);
        }

        [Test]
        public void ReleaseAllForOwner_RemovesEveryLockForOwner()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            InteractionOwnerId ownerId = OwnerId(1);
            TabletopObjectId firstObjectId = ObjectId(1);
            TabletopObjectId secondObjectId = ObjectId(2);
            service.Acquire(firstObjectId, ownerId);
            service.Acquire(secondObjectId, ownerId);

            int removed = service.ReleaseAllForOwner(ownerId);

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(service.IsLocked(firstObjectId), Is.False);
            Assert.That(service.IsLocked(secondObjectId), Is.False);
        }

        [Test]
        public void ReleaseAllForOwner_ReturnsExactRemovedCount()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(ObjectId(1), ownerId);
            service.Acquire(ObjectId(2), ownerId);
            service.Acquire(ObjectId(3), OwnerId(2));

            int removed = service.ReleaseAllForOwner(ownerId);

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(service.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseAllForOwner_PreservesOtherOwnersLocks()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            InteractionOwnerId firstOwnerId = OwnerId(1);
            InteractionOwnerId secondOwnerId = OwnerId(2);
            TabletopObjectId otherObjectId = ObjectId(3);
            service.Acquire(ObjectId(1), firstOwnerId);
            service.Acquire(ObjectId(2), firstOwnerId);
            service.Acquire(otherObjectId, secondOwnerId);

            service.ReleaseAllForOwner(firstOwnerId);

            Assert.That(service.IsOwnedBy(otherObjectId, secondOwnerId), Is.True);
            Assert.That(service.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseAllForOwner_WhenOwnerHasNoLocks_ReturnsZero()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            service.Acquire(ObjectId(1), OwnerId(1));

            int removed = service.ReleaseAllForOwner(OwnerId(2));

            Assert.That(removed, Is.EqualTo(0));
            Assert.That(service.Count, Is.EqualTo(1));
        }

        [Test]
        public void Clear_RemovesAllLocks()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            service.Acquire(ObjectId(1), OwnerId(1));
            service.Acquire(ObjectId(2), OwnerId(2));

            service.Clear();

            Assert.That(service.Count, Is.EqualTo(0));
            Assert.That(service.IsLocked(ObjectId(1)), Is.False);
            Assert.That(service.IsLocked(ObjectId(2)), Is.False);
        }

        [Test]
        public void Clear_WhenServiceIsEmpty_IsSafe()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            service.Clear();

            Assert.That(service.Count, Is.EqualTo(0));
        }

        [TestCase(ObjectIdOperation.IsLocked)]
        [TestCase(ObjectIdOperation.IsOwnedBy)]
        [TestCase(ObjectIdOperation.TryGetOwner)]
        [TestCase(ObjectIdOperation.Acquire)]
        [TestCase(ObjectIdOperation.Release)]
        [TestCase(ObjectIdOperation.ReleaseObject)]
        public void PublicOperation_WhenObjectIdIsEmpty_ThrowsArgumentException(ObjectIdOperation operation)
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            Assert.Throws<ArgumentException>(() => InvokeWithEmptyObjectId(service, operation));
        }

        [TestCase(OwnerIdOperation.IsOwnedBy)]
        [TestCase(OwnerIdOperation.Acquire)]
        [TestCase(OwnerIdOperation.Release)]
        [TestCase(OwnerIdOperation.ReleaseAllForOwner)]
        public void PublicOperation_WhenOwnerIdIsEmpty_ThrowsArgumentException(OwnerIdOperation operation)
        {
            LocalInteractionLockService service = new LocalInteractionLockService();

            Assert.Throws<ArgumentException>(() => InvokeWithEmptyOwnerId(service, operation));
        }

        [Test]
        public void ValidationFailures_DoNotMutateExistingLocks()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectId objectId = ObjectId(1);
            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(objectId, ownerId);

            Assert.Throws<ArgumentException>(() => service.Acquire(TabletopObjectId.Empty, OwnerId(2)));
            Assert.Throws<ArgumentException>(() => service.Acquire(ObjectId(2), InteractionOwnerId.Empty));
            Assert.Throws<ArgumentException>(() => service.Release(objectId, InteractionOwnerId.Empty));
            Assert.Throws<ArgumentException>(() => service.ReleaseAllForOwner(InteractionOwnerId.Empty));

            Assert.That(service.Count, Is.EqualTo(1));
            Assert.That(service.IsOwnedBy(objectId, ownerId), Is.True);
        }

        [Test]
        public void AcquireAndRelease_DoNotMutateExistingTabletopObjectState()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectState state = CreateObjectState();
            TabletopPose originalPose = state.Pose;
            ContainerId originalContainerId = state.ContainerId;
            PlayerId originalOwnerPlayerId = state.OwnerPlayerId;
            ObjectVisibility originalVisibility = state.Visibility;
            bool originalUserLocked = state.IsUserLocked;

            InteractionOwnerId ownerId = OwnerId(1);
            service.Acquire(state.Id, ownerId);
            service.Release(state.Id, ownerId);

            Assert.That(state.IsUserLocked, Is.EqualTo(originalUserLocked));
            Assert.That(state.OwnerPlayerId, Is.EqualTo(originalOwnerPlayerId));
            Assert.That(state.ContainerId, Is.EqualTo(originalContainerId));
            Assert.That(state.Pose, Is.EqualTo(originalPose));
            Assert.That(state.Visibility, Is.EqualTo(originalVisibility));
        }

        [Test]
        public void LocalInteractionLock_DoesNotChangePersistentUserLock()
        {
            LocalInteractionLockService service = new LocalInteractionLockService();
            TabletopObjectState state = CreateObjectState(isUserLocked: true);

            service.Acquire(state.Id, OwnerId(1));
            service.ReleaseObject(state.Id);

            Assert.That(state.IsUserLocked, Is.True);
        }

        private static void InvokeWithEmptyObjectId(
            LocalInteractionLockService service,
            ObjectIdOperation operation)
        {
            switch (operation)
            {
                case ObjectIdOperation.IsLocked:
                    service.IsLocked(TabletopObjectId.Empty);
                    break;
                case ObjectIdOperation.IsOwnedBy:
                    service.IsOwnedBy(TabletopObjectId.Empty, OwnerId(1));
                    break;
                case ObjectIdOperation.TryGetOwner:
                    service.TryGetOwner(TabletopObjectId.Empty, out _);
                    break;
                case ObjectIdOperation.Acquire:
                    service.Acquire(TabletopObjectId.Empty, OwnerId(1));
                    break;
                case ObjectIdOperation.Release:
                    service.Release(TabletopObjectId.Empty, OwnerId(1));
                    break;
                case ObjectIdOperation.ReleaseObject:
                    service.ReleaseObject(TabletopObjectId.Empty);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        private static void InvokeWithEmptyOwnerId(
            LocalInteractionLockService service,
            OwnerIdOperation operation)
        {
            switch (operation)
            {
                case OwnerIdOperation.IsOwnedBy:
                    service.IsOwnedBy(ObjectId(1), InteractionOwnerId.Empty);
                    break;
                case OwnerIdOperation.Acquire:
                    service.Acquire(ObjectId(1), InteractionOwnerId.Empty);
                    break;
                case OwnerIdOperation.Release:
                    service.Release(ObjectId(1), InteractionOwnerId.Empty);
                    break;
                case OwnerIdOperation.ReleaseAllForOwner:
                    service.ReleaseAllForOwner(InteractionOwnerId.Empty);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        private static TabletopObjectState CreateObjectState(bool isUserLocked = false)
        {
            return new TabletopObjectState(
                ObjectId(100),
                new ObjectDefinitionId(GuidFromSeed(200)),
                TabletopObjectKind.Card,
                new TabletopPose(new TableCoordinate(1.5, -2.5), 30f, 1, 2),
                new ContainerId(GuidFromSeed(300)),
                new PlayerId(GuidFromSeed(400)),
                ObjectVisibility.OwnerOnly,
                isUserLocked);
        }

        private static TabletopObjectId ObjectId(int seed)
        {
            return new TabletopObjectId(GuidFromSeed(seed));
        }

        private static InteractionOwnerId OwnerId(int seed)
        {
            return new InteractionOwnerId(GuidFromSeed(seed + 1000));
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }
    }
}
