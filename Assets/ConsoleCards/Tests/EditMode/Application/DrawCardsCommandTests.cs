using System;
using System.Linq;
using System.Reflection;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class DrawCardsCommandTests
    {
        [TestCase(1)]
        [TestCase(3)]
        public void Constructor_StoresContextSourceDestinationAndCount(int count)
        {
            CommandContext context = CreateContext();
            ContainerId sourceDeckId = ContainerId.New();
            ContainerId destinationId = ContainerId.New();

            DrawCardsCommand command = new DrawCardsCommand(context, sourceDeckId, destinationId, count);

            Assert.That(command.Context, Is.EqualTo(context));
            Assert.That(command.SourceDeckContainerId, Is.EqualTo(sourceDeckId));
            Assert.That(command.DestinationContainerId, Is.EqualTo(destinationId));
            Assert.That(command.Count, Is.EqualTo(count));
        }

        [Test]
        public void Constructor_WhenSourceDeckIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new DrawCardsCommand(CreateContext(), ContainerId.Empty, ContainerId.New(), 1),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenDestinationContainerIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new DrawCardsCommand(CreateContext(), ContainerId.New(), ContainerId.Empty, 1),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSourceAndDestinationMatch_ThrowsArgumentException()
        {
            ContainerId containerId = ContainerId.New();

            Assert.That(
                () => new DrawCardsCommand(CreateContext(), containerId, containerId, 1),
                Throws.ArgumentException);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_WhenCountIsNotPositive_ThrowsArgumentOutOfRangeException(int count)
        {
            Assert.That(
                () => new DrawCardsCommand(CreateContext(), ContainerId.New(), ContainerId.New(), count),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Command_ImplementsITabletopCommand()
        {
            ITabletopCommand command = new DrawCardsCommand(
                CreateContext(),
                ContainerId.New(),
                ContainerId.New(),
                1);

            Assert.That(command.Context, Is.EqualTo(((DrawCardsCommand)command).Context));
        }

        [Test]
        public void PublicContract_IsSealedAndImmutable()
        {
            Type type = typeof(DrawCardsCommand);

            Assert.That(type.IsSealed, Is.True);
            Assert.That(type.GetProperties().All(property => !property.CanWrite), Is.True);
            Assert.That(
                type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .All(field => field.IsInitOnly),
                Is.True);
        }

        private static CommandContext CreateContext()
        {
            return new CommandContext(CommandId.New(), MatchId.New(), PlayerId.New(), 0);
        }
    }
}
