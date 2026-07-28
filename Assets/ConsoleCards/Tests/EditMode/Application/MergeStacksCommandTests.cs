using System;
using System.Linq;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class MergeStacksCommandTests
    {
        [Test]
        public void Constructor_WhenValid_ExposesValues()
        {
            CommandContext context = CreateContext();
            ContainerId source = ContainerId.New();
            ContainerId destination = ContainerId.New();

            MergeStacksCommand command = new MergeStacksCommand(context, source, destination);

            Assert.That(command.Context, Is.EqualTo(context));
            Assert.That(command.SourceStackContainerId, Is.EqualTo(source));
            Assert.That(command.DestinationStackContainerId, Is.EqualTo(destination));
        }

        [Test]
        public void Constructor_WhenSourceIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new MergeStacksCommand(CreateContext(), ContainerId.Empty, ContainerId.New()),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenDestinationIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new MergeStacksCommand(CreateContext(), ContainerId.New(), ContainerId.Empty),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenStacksMatch_ThrowsArgumentException()
        {
            ContainerId stackId = ContainerId.New();

            Assert.That(
                () => new MergeStacksCommand(CreateContext(), stackId, stackId),
                Throws.ArgumentException);
        }

        [Test]
        public void Type_IsSealedImmutableCommand()
        {
            Type type = typeof(MergeStacksCommand);

            Assert.That(type.IsSealed, Is.True);
            Assert.That(typeof(ITabletopCommand).IsAssignableFrom(type), Is.True);
            Assert.That(type.GetProperties().All(property => property.SetMethod == null), Is.True);
            Assert.That(type.GetFields().Where(field => !field.IsStatic), Is.Empty);
        }

        private static CommandContext CreateContext()
        {
            return new CommandContext(CommandId.New(), MatchId.New(), PlayerId.New(), expectedRevision: 0);
        }
    }
}
