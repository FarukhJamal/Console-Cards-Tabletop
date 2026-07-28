namespace ConsoleCards.Application.Results
{
    public enum ShuffleDeckError
    {
        None,
        MatchMissing,
        CommandMissing,
        MatchMismatch,
        RevisionConflict,
        ContainerMissing,
        ContainerNotDeck,
        RevisionOverflow
    }
}
