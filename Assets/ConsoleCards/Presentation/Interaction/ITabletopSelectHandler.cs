using UnityEngine;
using ConsoleCards.Presentation.Views;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// TOOLS-1, selecting several components as in Tabletop Simulator (everything but game boards and Consoles): with
    /// the Select tool, or Ctrl held, a click on a piece adds or removes it, a click on the table clears the selection,
    /// and a drag draws a box that selects everything inside. The router hands those presses to the table.
    /// </summary>
    public interface ITabletopSelectHandler
    {
        /// <summary>True while the Select tool is chosen or the select key is held.</summary>
        bool WantsSelect();

        /// <summary>A press: the piece under the pointer (or null for the table).</summary>
        bool TryBeginSelect(TabletopObjectView viewUnderPointer, Vector2 screenPosition);

        /// <summary>Every frame while the button is held: the selection box follows.</summary>
        void UpdateSelect(Vector2 screenPosition);

        /// <summary>The button was let go: a click toggles, a box selects.</summary>
        void ReleaseSelect(Vector2 screenPosition);

        /// <summary>Escape or a reset: the box goes away; the selection stays.</summary>
        void CancelSelect();
    }
}
