namespace ConsoleCards.Core.Domain.Containers
{
    /// <summary>
    /// The face a Card takes when it is transferred into a Container. Declared by the pile style
    /// (a Discard Pile turns arriving Cards face down by default); Unchanged keeps the Card's face.
    /// </summary>
    public enum ContainerArrivalFace
    {
        Unchanged,
        FaceDown,
        FaceUp
    }
}
