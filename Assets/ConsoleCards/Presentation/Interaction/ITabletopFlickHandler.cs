using UnityEngine;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// UX-1, flick: hold Shift and press on a loose card, coin, token, pawn or die, pull back and let go; an arrow
    /// shows the direction and strength, and the piece slides across the table. The router starts a flick on a
    /// Shift-press over a piece the table says can be flicked; the table (the composition) aims and launches it.
    /// </summary>
    public interface ITabletopFlickHandler
    {
        /// <summary>True while the flick key is held.</summary>
        bool WantsFlick();

        /// <summary>Starts aiming this piece; false leaves nothing active (the press is handled as usual).</summary>
        bool TryBeginFlick(TabletopObjectView view, Vector2 screenPosition);

        /// <summary>Called every frame while the button is held: the pull sets direction and strength.</summary>
        void UpdateFlick(Vector2 screenPosition);

        /// <summary>The button was let go: the piece goes (a tiny pull does nothing).</summary>
        void ReleaseFlick(Vector2 screenPosition);

        /// <summary>Escape or a reset: no flick.</summary>
        void CancelFlick();
    }
}
