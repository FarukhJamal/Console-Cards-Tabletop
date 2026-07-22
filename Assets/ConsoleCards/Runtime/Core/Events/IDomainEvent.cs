namespace ConsoleCards.Core.Events
{
    public interface IDomainEvent
    {
        DomainEventContext Context { get; }
    }
}
