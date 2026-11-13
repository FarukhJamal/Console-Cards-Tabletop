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
            bool isUserLocked, PhysicalObjectState physicalState = null, long physicalRevision = 0)
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
            if (physicalRevision < 0 || (!containerId.IsEmpty && physicalState != null))
                throw new ArgumentException("Invalid initial physical state.");
            PhysicalState = physicalState;
            PhysicalRevision = physicalRevision;
        }

        public TabletopObjectId Id { get; }

        public ObjectDefinitionId DefinitionId { get; }

        public TabletopObjectKind Kind { get; }

        public TabletopPose Pose { get; private set; }

        public PhysicalObjectState PhysicalState { get; private set; }
        public long PhysicalRevision { get; private set; }

        public void SetPhysicalState(PhysicalObjectState state)
        {
            if (!ContainerId.IsEmpty && state != null) throw new InvalidOperationException("Contained objects use Container layout.");
            if (ReferenceEquals(PhysicalState, state)) return;
            if (PhysicalRevision == long.MaxValue) throw new InvalidOperationException("Physical revision overflow.");
            PhysicalState = state;
            PhysicalRevision++;
        }

        /// <summary>
        /// AUTH-DICE (2026-11-06): the host replaced the resting place of a step this table already ran (a rules die turned
        /// to the host's result). The same step, so the physical revision does not move: the next step from here still
        /// names the revision the host has.
        /// </summary>
        public void CorrectPhysicalState(PhysicalObjectState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!ContainerId.IsEmpty) throw new InvalidOperationException("Contained objects use Container layout.");
            PhysicalState = state;
        }

        /// <summary>
        /// P8A (owner, 2026-11-13): a joined computer takes the host's own record of one loose piece after the host
        /// refused that computer's move of it: the host's physical state AND its physical revision, so the two tables'
        /// records of the piece are the same again without the whole table being sent and rebuilt.
        /// </summary>
        public void CorrectPhysicalState(PhysicalObjectState state, long physicalRevision)
        {
            if (physicalRevision < 0) throw new ArgumentOutOfRangeException(nameof(physicalRevision));
            CorrectPhysicalState(state);
            PhysicalRevision = physicalRevision;
        }

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
            if (!containerId.IsEmpty) SetPhysicalState(null);
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
