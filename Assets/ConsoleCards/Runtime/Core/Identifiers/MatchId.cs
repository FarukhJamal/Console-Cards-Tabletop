using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct MatchId : IEquatable<MatchId>
    {
        public MatchId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static MatchId Empty => new MatchId(Guid.Empty);

        public static MatchId New()
        {
            return new MatchId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out MatchId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new MatchId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(MatchId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is MatchId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(MatchId left, MatchId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MatchId left, MatchId right)
        {
            return !left.Equals(right);
        }
    }
}
