using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class DomainEventContractTests
    {
        [Test]
        public void TestOnlyDomainEvent_ExposesApprovedContext()
        {
            DomainEventContext context = new DomainEventContext(MatchId.New(), 5);

            IDomainEvent domainEvent = new TestDomainEvent(context);

            Assert.That(domainEvent.Context, Is.EqualTo(context));
        }

        private sealed class TestDomainEvent : IDomainEvent
        {
            public TestDomainEvent(DomainEventContext context)
            {
                Context = context;
            }

            public DomainEventContext Context { get; }
        }
    }
}
