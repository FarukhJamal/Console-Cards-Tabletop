using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct PlayAreaId : IEquatable<PlayAreaId>
    {
        public PlayAreaId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static PlayAreaId Empty => new PlayAreaId(Guid.Empty);

        public static PlayAreaId New()
        {
            return new PlayAreaId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out PlayAreaId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new PlayAreaId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(PlayAreaId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayAreaId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(PlayAreaId left, PlayAreaId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayAreaId left, PlayAreaId right)
        {
            return !left.Equals(right);
        }
    }
}
