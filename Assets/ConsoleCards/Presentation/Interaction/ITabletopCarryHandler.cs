using UnityEngine;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Carrying a whole pile (owner, 2026-10-18): press and hold a card in a stack without moving and the whole
    /// stack is picked up; let go over another stack and the two are merged, anywhere else and the stack is put
    /// down there. The router starts a carry when a contained-card press is held still long enough; the table
    /// (the composition) moves the pile, shows where it would go and commits it.
    /// </summary>
    public interface ITabletopCarryHandler
    {
        /// <summary>True when the pile this card is in can be picked up whole.</summary>
        bool CanCarry(CardView pressedCard);

        /// <summary>Picks the pile up; false leaves nothing active.</summary>
        bool TryBeginCarry(CardView pressedCard, Vector2 screenPosition);

        /// <summary>Called every frame while the button is held.</summary>
        void UpdateCarry(Vector2 screenPosition);

        /// <summary>The button was let go: merge, or put the pile down here.</summary>
        void ReleaseCarry(Vector2 screenPosition);

        /// <summary>Escape or a reset: the pile goes back where it was.</summary>
        void CancelCarry();
    }
}
