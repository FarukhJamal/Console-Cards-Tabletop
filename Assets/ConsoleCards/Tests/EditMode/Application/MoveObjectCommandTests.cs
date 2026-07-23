using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class MoveObjectCommandTests
    {
        [Test]
        public void Constructor_StoresContextObjectIdAndTargetPose()
        {
            CommandContext context = CreateContext();
            TabletopObjectId objectId = TabletopObjectId.New();
            TabletopPose targetPose = CreatePose(x: 2.5, y: -3.5, rotationDegrees: 45f, layer: 2, localOrder: 7);

            MoveObjectCommand command = new MoveObjectCommand(context, objectId, targetPose);

            Assert.That(command.Context, Is.EqualTo(context));
            Assert.That(command.ObjectId, Is.EqualTo(objectId));
            Assert.That(command.TargetPose, Is.EqualTo(targetPose));
        }

        [Test]
        public void Constructor_WhenObjectIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new MoveObjectCommand(CreateContext(), TabletopObjectId.Empty, TabletopPose.Default),
                Throws.ArgumentException);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Constructor_WhenTargetXIsNonFinite_ThrowsArgumentOutOfRangeException(double value)
        {
            Assert.That(
                () => CreateCommand(targetPose: CreatePose(x: value)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Constructor_WhenTargetYIsNonFinite_ThrowsArgumentOutOfRangeException(double value)
        {
            Assert.That(
                () => CreateCommand(targetPose: CreatePose(y: value)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenRotationIsNonFinite_ThrowsArgumentOutOfRangeException(float value)
        {
            Assert.That(
                () => CreateCommand(targetPose: CreatePose(rotationDegrees: value)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WhenRotationIsNegativeOrNonNormalized_AcceptsValue()
        {
            MoveObjectCommand negative = CreateCommand(targetPose: CreatePose(rotationDegrees: -45f));
            MoveObjectCommand nonNormalized = CreateCommand(targetPose: CreatePose(rotationDegrees: 765f));

            Assert.That(negative.TargetPose.RotationDegrees, Is.EqualTo(-45f));
            Assert.That(nonNormalized.TargetPose.RotationDegrees, Is.EqualTo(765f));
        }

        [Test]
        public void Constructor_WhenLayerAndLocalOrderAreNegative_AcceptsValues()
        {
            MoveObjectCommand command = CreateCommand(targetPose: CreatePose(layer: -3, localOrder: -8));

            Assert.That(command.TargetPose.Layer, Is.EqualTo(-3));
            Assert.That(command.TargetPose.LocalOrder, Is.EqualTo(-8));
        }

        [Test]
        public void Command_ImplementsITabletopCommand()
        {
            ITabletopCommand command = CreateCommand();

            Assert.That(command.Context, Is.EqualTo(((MoveObjectCommand)command).Context));
        }

        private static MoveObjectCommand CreateCommand(TabletopPose? targetPose = null)
        {
            return new MoveObjectCommand(CreateContext(), TabletopObjectId.New(), targetPose ?? TabletopPose.Default);
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
