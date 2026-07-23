namespace ConsoleCards.Presentation.Interaction
{
    public enum FlipInteractionStatus
    {
        NoSelection,
        SelectionUnavailable,
        SelectionNotCard,
        ObjectUserLocked,
        LocalLockConflict,
        FlipAccepted,
        FlipRejected
    }
}
