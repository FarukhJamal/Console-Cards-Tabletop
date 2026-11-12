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
        RevisionOverflow,
        PhysicalStateInvalid,
        /// <summary>P8A: a game's own rule refuses it (Trap Floor, Full: a Blind player draws no Controller Cards).</summary>
        RefusedByGameRule
    }
}
