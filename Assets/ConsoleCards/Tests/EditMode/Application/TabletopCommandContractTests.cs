using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class TabletopCommandContractTests
    {
        [Test]
        public void TestOnlyCommand_ExposesApprovedContext()
        {
            CommandContext context = new CommandContext(
                CommandId.New(),
                MatchId.New(),
                PlayerId.New(),
                5);

            ITabletopCommand command = new TestCommand(context);

            Assert.That(command.Context, Is.EqualTo(context));
        }

        private sealed class TestCommand : ITabletopCommand
        {
            public TestCommand(CommandContext context)
            {
                Context = context;
            }

            public CommandContext Context { get; }
        }
    }
}
