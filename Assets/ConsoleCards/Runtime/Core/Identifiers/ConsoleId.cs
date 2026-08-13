using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct ConsoleId : IEquatable<ConsoleId>
    {
        public ConsoleId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static ConsoleId Empty => new ConsoleId(Guid.Empty);

        public static ConsoleId New()
        {
            return new ConsoleId(Guid.NewGuid());
        }

        public bool Equals(ConsoleId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ConsoleId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(ConsoleId left, ConsoleId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ConsoleId left, ConsoleId right)
        {
            return !left.Equals(right);
        }
    }
}
