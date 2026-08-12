namespace ConsoleCards.Application.Results
{
    public enum TransferCardError
    {
        None,
        MatchMissing,
        CommandMissing,
        MatchMismatch,
        RevisionConflict,
        ObjectMissing,
        ObjectNotCard,
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
        LooseCardOrderOverflow,
        RevisionOverflow
    }
}
