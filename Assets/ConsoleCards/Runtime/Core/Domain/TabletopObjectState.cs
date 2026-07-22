using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain
{
    public sealed class TabletopObjectState
    {
        public TabletopObjectState(
            TabletopObjectId id,
            ObjectDefinitionId definitionId,
            TabletopObjectKind kind,
            TabletopPose pose,
            ContainerId containerId,
            PlayerId ownerPlayerId,
            ObjectVisibility visibility,
            bool isUserLocked)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", nameof(id));
            }

            if (definitionId.IsEmpty)
            {
                throw new ArgumentException("Object definition ID cannot be empty.", nameof(definitionId));
            }

            Id = id;
            DefinitionId = definitionId;
            Kind = kind;
            Pose = pose;
            ContainerId = containerId;
            OwnerPlayerId = ownerPlayerId;
            Visibility = visibility;
            IsUserLocked = isUserLocked;
        }

        public TabletopObjectId Id { get; }

        public ObjectDefinitionId DefinitionId { get; }

        public TabletopObjectKind Kind { get; }

        public TabletopPose Pose { get; private set; }

        public ContainerId ContainerId { get; private set; }

        public PlayerId OwnerPlayerId { get; private set; }

        public ObjectVisibility Visibility { get; private set; }

        public bool IsUserLocked { get; private set; }

        public void SetPose(TabletopPose pose)
        {
            Pose = pose;
        }

        public void SetContainer(ContainerId containerId)
        {
            ContainerId = containerId;
        }

        public void SetOwner(PlayerId ownerPlayerId)
        {
            OwnerPlayerId = ownerPlayerId;
        }

        public void SetVisibility(ObjectVisibility visibility)
        {
            Visibility = visibility;
        }

        public void SetUserLocked(bool isLocked)
        {
            IsUserLocked = isLocked;
        }
    }
}
