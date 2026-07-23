namespace ConsoleCards.Application.Results
{
    public enum FlipCardError
    {
        None,
        MatchRequired,
        CommandRequired,
        MatchIdMismatch,
        RevisionConflict,
        ObjectNotFound,
        ObjectNotCard,
        ObjectUserLocked,
        RevisionOverflow
    }
}
