using System;

namespace ConsoleCards.Core.Coordinates
{
    public readonly struct TabletopPose : IEquatable<TabletopPose>
    {
        public TabletopPose(TableCoordinate position, float rotationDegrees, int layer, int localOrder)
        {
            Position = position;
            RotationDegrees = rotationDegrees;
            Layer = layer;
            LocalOrder = localOrder;
        }

        public TableCoordinate Position { get; }

        public float RotationDegrees { get; }

        public int Layer { get; }

        public int LocalOrder { get; }

        public static TabletopPose Default => new TabletopPose(TableCoordinate.Zero, 0, 0, 0);

        public bool Equals(TabletopPose other)
        {
            return Position.Equals(other.Position)
                && RotationDegrees.Equals(other.RotationDegrees)
                && Layer == other.Layer
                && LocalOrder == other.LocalOrder;
        }

        public override bool Equals(object obj)
        {
            return obj is TabletopPose other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Position, RotationDegrees, Layer, LocalOrder);
        }

        public override string ToString()
        {
            return $"Position: {Position}, RotationDegrees: {RotationDegrees}, Layer: {Layer}, LocalOrder: {LocalOrder}";
        }

        public static bool operator ==(TabletopPose left, TabletopPose right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TabletopPose left, TabletopPose right)
        {
            return !left.Equals(right);
        }
    }
}
