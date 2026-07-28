using System;
using System.Linq;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Application
{
    public sealed class SplitStackCommandTests
    {
        [Test]
        public void Constructor_WhenValid_ExposesValues()
        {
            CommandContext context = CreateContext();
            ContainerId source = ContainerId.New();
            ContainerId newStack = ContainerId.New();
            StackSplitSpecification specification = new StackSplitSpecification(2);
            TabletopPose pose = CreatePose(x: 1.0, y: 2.0, rotationDegrees: 450f);

            SplitStackCommand command = new SplitStackCommand(context, source, newStack, specification, pose);

            Assert.That(command.Context, Is.EqualTo(context));
            Assert.That(command.SourceStackContainerId, Is.EqualTo(source));
            Assert.That(command.NewStackContainerId, Is.EqualTo(newStack));
            Assert.That(command.SplitSpecification, Is.EqualTo(specification));
            Assert.That(command.NewStackPose, Is.EqualTo(pose));
        }

        [Test]
        public void Constructor_WhenSourceIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new SplitStackCommand(
                    CreateContext(),
                    ContainerId.Empty,
                    ContainerId.New(),
                    new StackSplitSpecification(1),
                    CreatePose()),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenNewStackIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new SplitStackCommand(
                    CreateContext(),
                    ContainerId.New(),
                    ContainerId.Empty,
                    new StackSplitSpecification(1),
                    CreatePose()),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenStackIdsMatch_ThrowsArgumentException()
        {
            ContainerId stackId = ContainerId.New();

            Assert.That(
                () => new SplitStackCommand(
                    CreateContext(),
                    stackId,
                    stackId,
                    new StackSplitSpecification(1),
                    CreatePose()),
                Throws.ArgumentException);
        }

        [Test]
        public void Constructor_WhenSplitSpecificationIsDefault_ThrowsArgumentOutOfRangeException()
        {
            Assert.That(
                () => new SplitStackCommand(
                    CreateContext(),
                    ContainerId.New(),
                    ContainerId.New(),
                    default,
                    CreatePose()),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(double.NaN, 0.0, 0f)]
        [TestCase(double.PositiveInfinity, 0.0, 0f)]
        [TestCase(0.0, double.NaN, 0f)]
        [TestCase(0.0, double.NegativeInfinity, 0f)]
        [TestCase(0.0, 0.0, float.NaN)]
        [TestCase(0.0, 0.0, float.PositiveInfinity)]
        public void Constructor_WhenPoseHasNonFiniteValue_ThrowsArgumentOutOfRangeException(
            double x,
            double y,
            float rotationDegrees)
        {
            Assert.That(
                () => new SplitStackCommand(
                    CreateContext(),
                    ContainerId.New(),
                    ContainerId.New(),
                    new StackSplitSpecification(1),
                    CreatePose(x, y, rotationDegrees)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(-90f)]
        [TestCase(810f)]
        public void Constructor_WhenRotationIsFiniteButNotNormalized_Accepts(float rotationDegrees)
        {
            SplitStackCommand command = new SplitStackCommand(
                CreateContext(),
                ContainerId.New(),
                ContainerId.New(),
                new StackSplitSpecification(1),
                CreatePose(rotationDegrees: rotationDegrees));

            Assert.That(command.NewStackPose.RotationDegrees, Is.EqualTo(rotationDegrees));
        }

        [Test]
        public void Type_IsSealedImmutableCommand()
        {
            Type type = typeof(SplitStackCommand);

            Assert.That(type.IsSealed, Is.True);
            Assert.That(typeof(ITabletopCommand).IsAssignableFrom(type), Is.True);
            Assert.That(type.GetProperties().All(property => property.SetMethod == null), Is.True);
            Assert.That(type.GetFields().Where(field => !field.IsStatic), Is.Empty);
        }

        private static CommandContext CreateContext()
        {
            return new CommandContext(CommandId.New(), MatchId.New(), PlayerId.New(), expectedRevision: 0);
        }

        private static TabletopPose CreatePose(
            double x = 0.0,
            double y = 0.0,
            float rotationDegrees = 0f)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, 0, 0);
        }
    }
}
