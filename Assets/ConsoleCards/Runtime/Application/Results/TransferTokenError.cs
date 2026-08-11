namespace ConsoleCards.Application.Results
{
    public enum TransferTokenError
    {
        None,
        MatchMissing,
        CommandMissing,
        MatchMismatch,
        RevisionConflict,
        ObjectMissing,
        ObjectNotToken,
        ObjectUserLocked,
        SourceContainerMissing,
        SourceMembershipMissing,
        SourceContainerMismatch,
        DestinationContainerMissing,
        DestinationCapacityExceeded,
        DestinationAlreadyContainsObject,
        ObjectFoundInUnexpectedContainer,
        SameLocation,
        TargetTablePoseMissing,
        RevisionOverflow
    }
}
