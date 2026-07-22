using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct GameTemplateId : IEquatable<GameTemplateId>
    {
        public GameTemplateId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static GameTemplateId Empty => new GameTemplateId(Guid.Empty);

        public static GameTemplateId New()
        {
            return new GameTemplateId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out GameTemplateId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new GameTemplateId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(GameTemplateId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is GameTemplateId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(GameTemplateId left, GameTemplateId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameTemplateId left, GameTemplateId right)
        {
            return !left.Equals(right);
        }
    }
}
