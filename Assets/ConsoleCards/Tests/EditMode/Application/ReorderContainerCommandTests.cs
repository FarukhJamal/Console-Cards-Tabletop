using System;
using System.Linq;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class ReorderContainerCommandTests
    {
        [Test]
        public void Constructor_WhenValid_ExposesValues()
        {
            CommandContext context = CreateContext();
            ContainerId containerId = ContainerId.New();
            TabletopObjectId objectId = TabletopObjectId.New();

            ReorderContainerCommand command = new ReorderContainerCommand(
                context,
                containerId,
                objectId,
                fromIndex: 1,
                toIndex: 3);

            Assert.That(command.Context, Is.EqualTo(context));
            Assert.That(command.ContainerId, Is.EqualTo(containerId));
            Assert.That(command.ObjectId, Is.EqualTo(objectId));
            Assert.That(command.FromIndex, Is.EqualTo(1));
            Assert.That(command.ToIndex, Is.EqualTo(3));
        }

        [Test]
        public void Constructor_WhenSameIndices_Accepts()
        {
            ReorderContainerCommand command = new ReorderContainerCommand(
                CreateContext(),
                ContainerId.New(),
                TabletopObjectId.New(),
                fromIndex: 2,
                toIndex: 2);

            Assert.That(command.FromIndex, Is.EqualTo(2));
            Assert.That(command.ToIndex, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_WhenContainerIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new ReorderContainerCommand(
                    CreateContext(),
                    ContainerId.Empty,
                    TabletopObjectId.New(),
                    fromIndex: 0,
                    toIndex: 1),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenObjectIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new ReorderContainerCommand(
                    CreateContext(),
                    ContainerId.New(),
                    TabletopObjectId.Empty,
                    fromIndex: 0,
                    toIndex: 1),
                Throws.ArgumentException);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void Constructor_WhenIndexIsNegative_ThrowsArgumentOutOfRangeException(int fromIndex, int toIndex)
        {
            Assert.That(
                () => new ReorderContainerCommand(
                    CreateContext(),
                    ContainerId.New(),
                    TabletopObjectId.New(),
                    fromIndex,
                    toIndex),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Type_IsSealedImmutableCommand()
        {
            Type type = typeof(ReorderContainerCommand);

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
