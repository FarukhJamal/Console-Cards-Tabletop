using System;
using System.Linq;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class TransferCardCommandTests
    {
        [Test]
        public void Constructor_WhenTabletopToContainerIsValid_StoresValues()
        {
            CommandContext context = CreateContext();
            TabletopObjectId cardId = TabletopObjectId.New();
            ContainerId destinationId = ContainerId.New();

            TransferCardCommand command = new TransferCardCommand(
                context,
                cardId,
                ContainerId.Empty,
                destinationId,
                null);

            Assert.That(command.Context, Is.EqualTo(context));
            Assert.That(command.CardObjectId, Is.EqualTo(cardId));
            Assert.That(command.ExpectedSourceContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(command.DestinationContainerId, Is.EqualTo(destinationId));
            Assert.That(command.TargetTablePose.HasValue, Is.False);
        }

        [Test]
        public void Constructor_WhenContainerToTabletopIsValid_StoresTargetPose()
        {
            TabletopPose pose = CreatePose(x: 7.5, y: -2.25, rotationDegrees: -450f);
            ContainerId sourceId = ContainerId.New();

            TransferCardCommand command = new TransferCardCommand(
                CreateContext(),
                TabletopObjectId.New(),
                sourceId,
                ContainerId.Empty,
                pose);

            Assert.That(command.ExpectedSourceContainerId, Is.EqualTo(sourceId));
            Assert.That(command.DestinationContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(command.TargetTablePose, Is.EqualTo(pose));
        }

        [Test]
        public void Constructor_WhenContainerToContainerIsValid_StoresContainerIds()
        {
            ContainerId sourceId = ContainerId.New();
            ContainerId destinationId = ContainerId.New();

            TransferCardCommand command = new TransferCardCommand(
                CreateContext(),
                TabletopObjectId.New(),
                sourceId,
                destinationId,
                null);

            Assert.That(command.ExpectedSourceContainerId, Is.EqualTo(sourceId));
            Assert.That(command.DestinationContainerId, Is.EqualTo(destinationId));
        }

        [Test]
        public void Factories_CreateExpectedCommands()
        {
            CommandContext context = CreateContext();
            TabletopObjectId cardId = TabletopObjectId.New();
            ContainerId sourceId = ContainerId.New();
            ContainerId destinationId = ContainerId.New();
            TabletopPose pose = CreatePose();

            TransferCardCommand toContainer = TransferCardCommand.ToContainer(
                context,
                cardId,
                sourceId,
                destinationId);
            TransferCardCommand toTabletop = TransferCardCommand.ToTabletop(
                context,
                cardId,
                sourceId,
                pose);

            Assert.That(toContainer.DestinationContainerId, Is.EqualTo(destinationId));
            Assert.That(toContainer.TargetTablePose.HasValue, Is.False);
            Assert.That(toTabletop.DestinationContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(toTabletop.TargetTablePose, Is.EqualTo(pose));
        }

        [Test]
        public void Constructor_WhenCardIdIsEmpty_Rejects()
        {
            Assert.Throws<ArgumentException>(() => new TransferCardCommand(
                CreateContext(),
                TabletopObjectId.Empty,
                ContainerId.Empty,
                ContainerId.New(),
                null));
        }

        [Test]
        public void Constructor_WhenTabletopToTabletop_Rejects()
        {
            Assert.Throws<ArgumentException>(() => new TransferCardCommand(
                CreateContext(),
                TabletopObjectId.New(),
                ContainerId.Empty,
                ContainerId.Empty,
                CreatePose()));
        }

        [Test]
        public void Constructor_WhenSameContainer_Rejects()
        {
            ContainerId containerId = ContainerId.New();

            Assert.Throws<ArgumentException>(() => new TransferCardCommand(
                CreateContext(),
                TabletopObjectId.New(),
                containerId,
                containerId,
                null));
        }

        [Test]
        public void Constructor_WhenTabletopDestinationWithoutPose_Rejects()
        {
            Assert.Throws<ArgumentException>(() => new TransferCardCommand(
                CreateContext(),
                TabletopObjectId.New(),
                ContainerId.New(),
                ContainerId.Empty,
                null));
        }

        [Test]
        public void Constructor_WhenContainerDestinationHasPose_Rejects()
        {
            Assert.Throws<ArgumentException>(() => new TransferCardCommand(
                CreateContext(),
                TabletopObjectId.New(),
                ContainerId.Empty,
                ContainerId.New(),
                CreatePose()));
        }

        [TestCase(double.NaN, 0.0, 0f)]
        [TestCase(0.0, double.PositiveInfinity, 0f)]
        [TestCase(0.0, 0.0, float.NegativeInfinity)]
        public void Constructor_WhenTargetPoseHasNonFiniteValue_Rejects(
            double x,
            double y,
            float rotationDegrees)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TransferCardCommand(
                CreateContext(),
                TabletopObjectId.New(),
                ContainerId.New(),
                ContainerId.Empty,
                CreatePose(x, y, rotationDegrees)));
        }

        [Test]
        public void Constructor_WhenTargetRotationIsFiniteButNotNormalized_Accepts()
        {
            TransferCardCommand command = new TransferCardCommand(
                CreateContext(),
                TabletopObjectId.New(),
                ContainerId.New(),
                ContainerId.Empty,
                CreatePose(rotationDegrees: -810f));

            Assert.That(command.TargetTablePose.Value.RotationDegrees, Is.EqualTo(-810f));
        }

        [Test]
        public void Command_IsSealedImmutableAndImplementsITabletopCommand()
        {
            Type type = typeof(TransferCardCommand);

            Assert.That(type.IsSealed, Is.True);
            Assert.That(typeof(ITabletopCommand).IsAssignableFrom(type), Is.True);
            Assert.That(type.GetProperties().All(property => property.SetMethod == null), Is.True);
        }

        private static CommandContext CreateContext()
        {
            return new CommandContext(CommandId.New(), MatchId.New(), PlayerId.New(), 0);
        }

        private static TabletopPose CreatePose(
            double x = 1.0,
            double y = 2.0,
            float rotationDegrees = 30f,
            int layer = 0,
            int localOrder = 0)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, layer, localOrder);
        }
    }
}
