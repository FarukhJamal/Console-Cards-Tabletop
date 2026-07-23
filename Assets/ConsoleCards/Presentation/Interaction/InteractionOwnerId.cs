using System;

namespace ConsoleCards.Presentation.Interaction
{
    public readonly struct InteractionOwnerId : IEquatable<InteractionOwnerId>
    {
        public InteractionOwnerId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static InteractionOwnerId Empty => new InteractionOwnerId(Guid.Empty);

        public static InteractionOwnerId New()
        {
            return new InteractionOwnerId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out InteractionOwnerId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new InteractionOwnerId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(InteractionOwnerId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is InteractionOwnerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(InteractionOwnerId left, InteractionOwnerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InteractionOwnerId left, InteractionOwnerId right)
        {
            return !left.Equals(right);
        }
    }
}
