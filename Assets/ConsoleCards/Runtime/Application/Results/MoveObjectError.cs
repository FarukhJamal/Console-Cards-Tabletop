namespace ConsoleCards.Application.Results
{
    public enum MoveObjectError
    {
        None,
        MatchRequired,
        CommandRequired,
        MatchIdMismatch,
        RevisionConflict,
        ObjectNotFound,
        ObjectUserLocked,
        RevisionOverflow
    }
}
