using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct SeatId : IEquatable<SeatId>
    {
        public SeatId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static SeatId Empty => new SeatId(Guid.Empty);

        public static SeatId New()
        {
            return new SeatId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out SeatId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new SeatId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(SeatId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is SeatId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(SeatId left, SeatId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SeatId left, SeatId right)
        {
            return !left.Equals(right);
        }
    }
}
