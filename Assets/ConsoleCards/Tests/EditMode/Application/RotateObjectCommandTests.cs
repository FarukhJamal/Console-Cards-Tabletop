using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class RotateObjectCommandTests
    {
        [Test]
        public void Constructor_StoresContextObjectIdAndTargetRotation()
        {
            CommandContext context = CreateContext();
            TabletopObjectId objectId = TabletopObjectId.New();

            RotateObjectCommand command = new RotateObjectCommand(context, objectId, 45.5f);

            Assert.That(command.Context, Is.EqualTo(context));
            Assert.That(command.ObjectId, Is.EqualTo(objectId));
            Assert.That(command.TargetRotationDegrees, Is.EqualTo(45.5f));
        }

        [Test]
        public void Constructor_WhenObjectIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new RotateObjectCommand(CreateContext(), TabletopObjectId.Empty, 0f),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenRotationIsNaN_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => CreateCommand(float.NaN),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WhenRotationIsPositiveInfinity_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => CreateCommand(float.PositiveInfinity),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WhenRotationIsNegativeInfinity_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => CreateCommand(float.NegativeInfinity),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_WhenRotationIsNegativeFinite_AcceptsValue()
        {
            RotateObjectCommand command = CreateCommand(-45f);

            Assert.That(command.TargetRotationDegrees, Is.EqualTo(-45f));
        }

        [Test]
        public void Constructor_WhenRotationIsAbove360_AcceptsValue()
        {
            RotateObjectCommand command = CreateCommand(765f);

            Assert.That(command.TargetRotationDegrees, Is.EqualTo(765f));
        }

        [Test]
        public void Constructor_WhenRotationIsBelowNegative360_AcceptsValue()
        {
            RotateObjectCommand command = CreateCommand(-765f);

            Assert.That(command.TargetRotationDegrees, Is.EqualTo(-765f));
        }

        [Test]
        public void Command_ImplementsITabletopCommand()
        {
            ITabletopCommand command = CreateCommand(90f);

            Assert.That(command.Context, Is.EqualTo(((RotateObjectCommand)command).Context));
        }

        private static RotateObjectCommand CreateCommand(float targetRotationDegrees)
        {
            return new RotateObjectCommand(CreateContext(), TabletopObjectId.New(), targetRotationDegrees);
        }

        private static CommandContext CreateContext()
        {
            return new CommandContext(CommandId.New(), MatchId.New(), PlayerId.New(), 0);
        }
    }
}
