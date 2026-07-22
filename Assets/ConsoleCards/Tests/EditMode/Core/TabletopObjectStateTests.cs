using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class TabletopObjectStateTests
    {
        [Test]
        public void Constructor_StoresAllSuppliedValues()
        {
            TabletopObjectId id = TabletopObjectId.New();
            ObjectDefinitionId definitionId = ObjectDefinitionId.New();
            TabletopPose pose = new TabletopPose(new TableCoordinate(10.5, 20.25), 45.0f, 2, 6);
            ContainerId containerId = ContainerId.New();
            PlayerId ownerPlayerId = PlayerId.New();

            TabletopObjectState state = new TabletopObjectState(
                id,
                definitionId,
                TabletopObjectKind.Card,
                pose,
                containerId,
                ownerPlayerId,
                ObjectVisibility.OwnerOnly,
                true);

            Assert.That(state.Id, Is.EqualTo(id));
            Assert.That(state.DefinitionId, Is.EqualTo(definitionId));
            Assert.That(state.Kind, Is.EqualTo(TabletopObjectKind.Card));
            Assert.That(state.Pose, Is.EqualTo(pose));
            Assert.That(state.ContainerId, Is.EqualTo(containerId));
            Assert.That(state.OwnerPlayerId, Is.EqualTo(ownerPlayerId));
            Assert.That(state.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
            Assert.That(state.IsUserLocked, Is.True);
        }

        [Test]
        public void Constructor_WhenIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateState(id: TabletopObjectId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenDefinitionIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => CreateState(definitionId: ObjectDefinitionId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenContainerIdIsEmpty_AcceptsValue()
        {
            TabletopObjectState state = CreateState(containerId: ContainerId.Empty);

            Assert.That(state.ContainerId, Is.EqualTo(ContainerId.Empty));
        }

        [Test]
        public void Constructor_WhenOwnerPlayerIdIsEmpty_AcceptsValue()
        {
            TabletopObjectState state = CreateState(ownerPlayerId: PlayerId.Empty);

            Assert.That(state.OwnerPlayerId, Is.EqualTo(PlayerId.Empty));
        }

        [Test]
        public void SetPose_UpdatesPose()
        {
            TabletopObjectState state = CreateState();
            TabletopPose pose = new TabletopPose(new TableCoordinate(3.0, 4.0), 90.0f, 5, 7);

            state.SetPose(pose);

            Assert.That(state.Pose, Is.EqualTo(pose));
        }

        [Test]
        public void SetContainer_UpdatesContainerId()
        {
            TabletopObjectState state = CreateState();
            ContainerId containerId = ContainerId.New();

            state.SetContainer(containerId);

            Assert.That(state.ContainerId, Is.EqualTo(containerId));
        }

        [Test]
        public void SetOwner_UpdatesOwnerPlayerId()
        {
            TabletopObjectState state = CreateState();
            PlayerId ownerPlayerId = PlayerId.New();

            state.SetOwner(ownerPlayerId);

            Assert.That(state.OwnerPlayerId, Is.EqualTo(ownerPlayerId));
        }

        [Test]
        public void SetVisibility_UpdatesVisibility()
        {
            TabletopObjectState state = CreateState();

            state.SetVisibility(ObjectVisibility.HiddenIdentityWithPublicBack);

            Assert.That(state.Visibility, Is.EqualTo(ObjectVisibility.HiddenIdentityWithPublicBack));
        }

        [Test]
        public void SetUserLocked_UpdatesIsUserLocked()
        {
            TabletopObjectState state = CreateState(isUserLocked: false);

            state.SetUserLocked(true);

            Assert.That(state.IsUserLocked, Is.True);
        }

        private static TabletopObjectState CreateState(
            TabletopObjectId? id = null,
            ObjectDefinitionId? definitionId = null,
            TabletopObjectKind kind = TabletopObjectKind.Card,
            TabletopPose? pose = null,
            ContainerId? containerId = null,
            PlayerId? ownerPlayerId = null,
            ObjectVisibility visibility = ObjectVisibility.Public,
            bool isUserLocked = false)
        {
            return new TabletopObjectState(
                id ?? TabletopObjectId.New(),
                definitionId ?? ObjectDefinitionId.New(),
                kind,
                pose ?? TabletopPose.Default,
                containerId ?? ContainerId.New(),
                ownerPlayerId ?? PlayerId.New(),
                visibility,
                isUserLocked);
        }
    }
}
