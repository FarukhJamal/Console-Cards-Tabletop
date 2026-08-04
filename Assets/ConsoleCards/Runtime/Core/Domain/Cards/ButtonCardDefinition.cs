using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Cards
{
    public readonly struct ButtonCardDefinition : IEquatable<ButtonCardDefinition>
    {
        public ButtonCardDefinition(ObjectDefinitionId definitionId, ButtonCardKind kind)
        {
            if (definitionId.IsEmpty)
            {
                throw new ArgumentException("Button Card definition ID cannot be empty.", nameof(definitionId));
            }

            if (!IsDefinedKind(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Button Card kind must be one of the eight universal Button Card identities.");
            }

            DefinitionId = definitionId;
            Kind = kind;
        }

        public ObjectDefinitionId DefinitionId { get; }

        public ButtonCardKind Kind { get; }

        public bool Equals(ButtonCardDefinition other)
        {
            return DefinitionId == other.DefinitionId && Kind == other.Kind;
        }

        public override bool Equals(object obj)
        {
            return obj is ButtonCardDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DefinitionId.GetHashCode() * 397) ^ (int)Kind;
            }
        }

        public override string ToString()
        {
            return $"DefinitionId: {DefinitionId}, Kind: {Kind}";
        }

        public static bool operator ==(ButtonCardDefinition left, ButtonCardDefinition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ButtonCardDefinition left, ButtonCardDefinition right)
        {
            return !left.Equals(right);
        }

        private static bool IsDefinedKind(ButtonCardKind kind)
        {
            return kind == ButtonCardKind.Up
                || kind == ButtonCardKind.Down
                || kind == ButtonCardKind.Left
                || kind == ButtonCardKind.Right
                || kind == ButtonCardKind.A
                || kind == ButtonCardKind.B
                || kind == ButtonCardKind.X
                || kind == ButtonCardKind.Y;
        }
    }
}
