namespace ConsoleCards.Application.Results
{
    public enum DrawCardsError
    {
        None,
        MatchMissing,
        CommandMissing,
        MatchMismatch,
        RevisionConflict,
        SourceContainerMissing,
        SourceContainerNotDeck,
        DestinationContainerMissing,
        SameContainer,
        InvalidCount,
        InsufficientCards,
        DestinationCapacityExceeded,
        ObjectMissing,
        ObjectContainerMismatch,
        ObjectUserLocked,
        RevisionOverflow
    }
}
