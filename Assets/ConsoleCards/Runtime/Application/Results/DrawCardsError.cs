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
        RevisionOverflow,
        /// <summary>P8A: a game's own rule refuses it (Trap Floor, Full: a Blind player draws no Controller Cards).</summary>
        RefusedByGameRule
    }
}
