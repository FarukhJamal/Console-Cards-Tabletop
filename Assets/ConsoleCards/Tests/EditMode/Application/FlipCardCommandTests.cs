using System;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class FlipCardCommandTests
    {
        [Test]
        public void Constructor_StoresContextObjectIdAndTargetFace()
        {
            CommandContext context = CreateContext();
            TabletopObjectId objectId = TabletopObjectId.New();

            FlipCardCommand command = new FlipCardCommand(context, objectId, CardFace.FaceDown);

            Assert.That(command.Context, Is.EqualTo(context));
            Assert.That(command.ObjectId, Is.EqualTo(objectId));
            Assert.That(command.TargetFace, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void Constructor_WhenObjectIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new FlipCardCommand(CreateContext(), TabletopObjectId.Empty, CardFace.FaceUp),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenTargetFaceIsFaceUp_AcceptsValue()
        {
            FlipCardCommand command = CreateCommand(CardFace.FaceUp);

            Assert.That(command.TargetFace, Is.EqualTo(CardFace.FaceUp));
        }

        [Test]
        public void Constructor_WhenTargetFaceIsFaceDown_AcceptsValue()
        {
            FlipCardCommand command = CreateCommand(CardFace.FaceDown);

            Assert.That(command.TargetFace, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void Constructor_WhenTargetFaceIsUndefined_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => CreateCommand((CardFace)999),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Command_ImplementsITabletopCommand()
        {
            ITabletopCommand command = CreateCommand(CardFace.FaceUp);

            Assert.That(command.Context, Is.EqualTo(((FlipCardCommand)command).Context));
        }

        private static FlipCardCommand CreateCommand(CardFace targetFace)
        {
            return new FlipCardCommand(CreateContext(), TabletopObjectId.New(), targetFace);
        }

        private static CommandContext CreateContext()
        {
            return new CommandContext(CommandId.New(), MatchId.New(), PlayerId.New(), 0);
        }
    }
}
