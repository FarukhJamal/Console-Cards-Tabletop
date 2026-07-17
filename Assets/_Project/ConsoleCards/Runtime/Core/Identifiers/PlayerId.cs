using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static PlayerId Empty => new PlayerId(Guid.Empty);

        public static PlayerId New()
        {
            return new PlayerId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out PlayerId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new PlayerId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(PlayerId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(PlayerId left, PlayerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerId left, PlayerId right)
        {
            return !left.Equals(right);
        }
    }
}
