using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct BoardId : IEquatable<BoardId>
    {
        public BoardId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static BoardId Empty => new BoardId(Guid.Empty);

        public static BoardId New()
        {
            return new BoardId(Guid.NewGuid());
        }

        public bool Equals(BoardId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is BoardId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(BoardId left, BoardId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BoardId left, BoardId right)
        {
            return !left.Equals(right);
        }
    }
}
