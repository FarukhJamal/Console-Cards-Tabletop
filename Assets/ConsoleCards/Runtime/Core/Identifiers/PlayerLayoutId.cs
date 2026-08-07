using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct PlayerLayoutId : IEquatable<PlayerLayoutId>
    {
        public PlayerLayoutId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static PlayerLayoutId Empty => new PlayerLayoutId(Guid.Empty);

        public static bool TryParse(string value, out PlayerLayoutId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new PlayerLayoutId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(PlayerLayoutId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerLayoutId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(PlayerLayoutId left, PlayerLayoutId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerLayoutId left, PlayerLayoutId right)
        {
            return !left.Equals(right);
        }
    }
}
