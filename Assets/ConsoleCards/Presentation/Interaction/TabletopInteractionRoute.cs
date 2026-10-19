namespace ConsoleCards.Presentation.Interaction
{
    public enum TabletopInteractionRoute
    {
        None = 0,
        TabletopMove = 1,
        ContainedCardDrag = 2,
        // A whole stack picked up by pressing and holding (owner, 2026-10-18).
        StackCarry = 3,
        // A loose piece aimed and flicked with Shift (UX-1).
        Flick = 4
    }
}
