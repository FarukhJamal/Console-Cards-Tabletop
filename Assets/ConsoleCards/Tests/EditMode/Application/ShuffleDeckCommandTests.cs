using System;
using System.Linq;
using System.Reflection;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class ShuffleDeckCommandTests
    {
        [Test]
        public void Constructor_StoresContextDeckContainerIdAndSeed()
        {
            CommandContext context = CreateContext();
            ContainerId deckContainerId = ContainerId.New();

            ShuffleDeckCommand command = new ShuffleDeckCommand(context, deckContainerId, 42);

            Assert.That(command.Context, Is.EqualTo(context));
            Assert.That(command.DeckContainerId, Is.EqualTo(deckContainerId));
            Assert.That(command.Seed, Is.EqualTo(42));
        }

        [Test]
        public void Constructor_WhenDeckContainerIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new ShuffleDeckCommand(CreateContext(), ContainerId.Empty, 1),
                Throws.ArgumentException);
        }

        [TestCase(0)]
        [TestCase(-12)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void Constructor_AcceptsAnyIntSeed(int seed)
        {
            ShuffleDeckCommand command = new ShuffleDeckCommand(CreateContext(), ContainerId.New(), seed);

            Assert.That(command.Seed, Is.EqualTo(seed));
        }

        [Test]
        public void Command_ImplementsITabletopCommand()
        {
            ITabletopCommand command = new ShuffleDeckCommand(CreateContext(), ContainerId.New(), 5);

            Assert.That(command.Context, Is.EqualTo(((ShuffleDeckCommand)command).Context));
        }

        [Test]
        public void PublicContract_IsSealedAndImmutable()
        {
            Type type = typeof(ShuffleDeckCommand);

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
