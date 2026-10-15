using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Doc 22 §14: lets a card drop target follow the dragged card's Console symbol. A card with a symbol is
    /// pulled into a nearby slot with the same symbol and cannot go into a slot with a different one.
    /// Presentation-only; it never changes Runtime State.
    /// </summary>
    public interface ICardDropMagnet
    {
        /// <summary>True while a card is being dragged, so the resolver asks <see cref="TryAdjust"/>.</summary>
        bool IsActive { get; }

        /// <summary>
        /// directContainerId is the Container under the pointer (Empty when none); point is the pointer on the
        /// table when hasPoint. Returns true to replace the target with adjustedContainerId, where Empty means
        /// no Container (the card lands on the table).
        /// </summary>
        bool TryAdjust(
            ContainerId directContainerId,
            bool hasPoint,
            TableCoordinate point,
            out ContainerId adjustedContainerId);
    }
}
