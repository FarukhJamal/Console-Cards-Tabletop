using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// The Draw tool (the drawing tool, client checklist item 5): while it is chosen a press draws a line, places a
    /// label or erases, instead of picking pieces up.
    /// </summary>
    public interface ITabletopDrawHandler
    {
        /// <summary>True while the Draw tool is chosen.</summary>
        bool WantsDraw();

        /// <summary>A press on the table (anywhere outside the screen UI).</summary>
        bool TryBeginDraw(Vector2 screenPosition);

        /// <summary>Every frame while the button is held: the line follows, or the eraser rubs.</summary>
        void UpdateDraw(Vector2 screenPosition);

        /// <summary>The button was let go: the line is kept, or the label is placed.</summary>
        void ReleaseDraw(Vector2 screenPosition);

        /// <summary>Escape or a reset: a line being drawn goes away.</summary>
        void CancelDraw();
    }
}
