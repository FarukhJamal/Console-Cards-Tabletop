using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct ContainerId : IEquatable<ContainerId>
    {
        public ContainerId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static ContainerId Empty => new ContainerId(Guid.Empty);

        public static ContainerId New()
        {
            return new ContainerId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out ContainerId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new ContainerId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(ContainerId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is ContainerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(ContainerId left, ContainerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContainerId left, ContainerId right)
        {
            return !left.Equals(right);
        }
    }
}
