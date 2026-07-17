using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct ObjectDefinitionId : IEquatable<ObjectDefinitionId>
    {
        public ObjectDefinitionId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static ObjectDefinitionId Empty => new ObjectDefinitionId(Guid.Empty);

        public static ObjectDefinitionId New()
        {
            return new ObjectDefinitionId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out ObjectDefinitionId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new ObjectDefinitionId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(ObjectDefinitionId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ObjectDefinitionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(ObjectDefinitionId left, ObjectDefinitionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ObjectDefinitionId left, ObjectDefinitionId right)
        {
            return !left.Equals(right);
        }
    }
}
