using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct TabletopObjectId : IEquatable<TabletopObjectId>
    {
        public TabletopObjectId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static TabletopObjectId Empty => new TabletopObjectId(Guid.Empty);

        public static TabletopObjectId New()
        {
            return new TabletopObjectId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out TabletopObjectId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new TabletopObjectId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(TabletopObjectId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is TabletopObjectId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(TabletopObjectId left, TabletopObjectId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TabletopObjectId left, TabletopObjectId right)
        {
            return !left.Equals(right);
        }
    }
}
