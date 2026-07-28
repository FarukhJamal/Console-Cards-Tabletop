namespace ConsoleCards.Application.Results
{
    public enum SplitStackError
    {
        None,
        MatchMissing,
        CommandMissing,
        MatchMismatch,
        RevisionConflict,
        SameStack,
        SourceStackMissing,
        SourceContainerNotStack,
        SourceStackTooSmall,
        InvalidSplitIndex,
        NewStackAlreadyExists,
        NewStackPlacementAlreadyExists,
        ObjectMissing,
        ObjectContainerMismatch,
        ObjectUserLocked,
        NewStackCreationFailed,
        RevisionOverflow
    }
}
