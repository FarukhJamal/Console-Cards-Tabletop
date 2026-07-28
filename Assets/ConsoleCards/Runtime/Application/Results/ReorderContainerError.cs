namespace ConsoleCards.Application.Results
{
    public enum ReorderContainerError
    {
        None,
        MatchMissing,
        CommandMissing,
        MatchMismatch,
        RevisionConflict,
        ContainerMissing,
        ObjectMissing,
        ObjectContainerMismatch,
        ObjectMembershipMissing,
        ObjectIndexMismatch,
        InvalidFromIndex,
        InvalidToIndex,
        ObjectUserLocked,
        RevisionOverflow
    }
}
