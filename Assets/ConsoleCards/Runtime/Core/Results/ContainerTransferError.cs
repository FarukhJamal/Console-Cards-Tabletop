namespace ConsoleCards.Core.Results
{
    public enum ContainerTransferError
    {
        None,
        ObjectStateRequired,
        SourceRequired,
        DestinationRequired,
        ObjectIdEmpty,
        ObjectAlreadyContained,
        SourceContainerMismatch,
        SourceDoesNotContainObject,
        DestinationFull,
        InvalidDestinationIndex,
        SameContainer
    }
}
