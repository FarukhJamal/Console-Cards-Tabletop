using ConsoleCards.Presentation.UI;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>MAKE-B: the table's shape for Make a game › WHAT GETS SAVED ("Hexagon").</summary>
    public sealed partial class TabletopComposition
    {
        internal string CurrentTableShapeName => TableShapeName(tableSpec);
    }
}
