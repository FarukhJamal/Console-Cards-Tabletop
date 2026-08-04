using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Presentation.Interaction
{
    public readonly struct CardDropTarget : IEquatable<CardDropTarget>
    {
        private CardDropTarget(
            CardDropTargetKind kind,
            ContainerId containerId,
            TabletopPose tabletopPose)
        {
            Kind = kind;
            ContainerId = containerId;
            TabletopPose = tabletopPose;
        }

        public CardDropTargetKind Kind { get; }

        public ContainerId ContainerId { get; }

        public TabletopPose TabletopPose { get; }

        public bool IsValid => Kind != CardDropTargetKind.None;

        public bool IsContainer => Kind == CardDropTargetKind.Container;

        public bool IsTabletop => Kind == CardDropTargetKind.Tabletop;

        public static CardDropTarget None()
        {
            return new CardDropTarget(CardDropTargetKind.None, ContainerId.Empty, TabletopPose.Default);
        }

        public static CardDropTarget ForContainer(ContainerId containerId)
        {
            if (containerId.IsEmpty)
            {
                throw new ArgumentException("Container drop target ID cannot be empty.", nameof(containerId));
            }

            return new CardDropTarget(CardDropTargetKind.Container, containerId, TabletopPose.Default);
        }

        public static CardDropTarget ForTabletop(TabletopPose tabletopPose)
        {
            ValidateFinitePose(tabletopPose, nameof(tabletopPose));

            return new CardDropTarget(CardDropTargetKind.Tabletop, ContainerId.Empty, tabletopPose);
        }

        public bool Equals(CardDropTarget other)
        {
            return Kind == other.Kind
                && ContainerId == other.ContainerId
                && TabletopPose == other.TabletopPose;
        }

        public override bool Equals(object obj)
        {
            return obj is CardDropTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Kind.GetHashCode();
                hash = (hash * 31) + ContainerId.GetHashCode();
                hash = (hash * 31) + TabletopPose.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return $"Kind: {Kind}, ContainerId: {ContainerId}, TabletopPose: {TabletopPose}";
        }

        public static bool operator ==(CardDropTarget left, CardDropTarget right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CardDropTarget left, CardDropTarget right)
        {
            return !left.Equals(right);
        }

        private static void ValidateFinitePose(TabletopPose pose, string parameterName)
        {
            if (!IsFinite(pose.Position.X)
                || !IsFinite(pose.Position.Y)
                || !IsFinite(pose.RotationDegrees))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
