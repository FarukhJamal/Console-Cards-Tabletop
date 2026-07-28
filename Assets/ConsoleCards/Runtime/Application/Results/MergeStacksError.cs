namespace ConsoleCards.Application.Results
{
    public enum MergeStacksError
    {
        None,
        MatchMissing,
        CommandMissing,
        MatchMismatch,
        RevisionConflict,
        SameStack,
        SourceStackMissing,
        DestinationStackMissing,
        SourceContainerNotStack,
        DestinationContainerNotStack,
        SourceStackEmpty,
        DestinationCapacityExceeded,
        ObjectMissing,
        ObjectContainerMismatch,
        ObjectUserLocked,
        SourceContainerRemovalFailed,
        RevisionOverflow
    }
}
