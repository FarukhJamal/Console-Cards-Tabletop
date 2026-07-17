using System;

namespace ConsoleCards.Core.Identifiers
{
    public readonly struct CommandId : IEquatable<CommandId>
    {
        public CommandId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public bool IsEmpty => Value == Guid.Empty;

        public static CommandId Empty => new CommandId(Guid.Empty);

        public static CommandId New()
        {
            return new CommandId(Guid.NewGuid());
        }

        public static bool TryParse(string value, out CommandId result)
        {
            if (Guid.TryParse(value, out Guid guid))
            {
                result = new CommandId(guid);
                return true;
            }

            result = Empty;
            return false;
        }

        public bool Equals(CommandId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is CommandId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static bool operator ==(CommandId left, CommandId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandId left, CommandId right)
        {
            return !left.Equals(right);
        }
    }
}
