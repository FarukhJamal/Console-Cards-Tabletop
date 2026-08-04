namespace ConsoleCards.Presentation.Interaction
{
    public enum CardTransferInteractionStatus
    {
        NoTarget = 0,
        CardUnavailable = 1,
        CardNotTransferable = 2,
        SameLocation = 3,
        SourceLayoutUnavailable = 4,
        DestinationLayoutUnavailable = 5,
        LocalLockConflict = 6,
        TransferAccepted = 7,
        TransferRejected = 8
    }
}
