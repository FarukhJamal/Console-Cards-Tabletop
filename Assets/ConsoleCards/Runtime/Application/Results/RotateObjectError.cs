namespace ConsoleCards.Application.Results
{
    public enum RotateObjectError
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
