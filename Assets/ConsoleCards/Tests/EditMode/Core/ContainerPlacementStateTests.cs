using System;
using System.Linq;
using System.Reflection;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.Core
{
    public sealed class ContainerPlacementStateTests
    {
        [Test]
        public void Constructor_WhenValid_StoresContainerIdAndPose()
        {
            ContainerId containerId = ContainerId.New();
            TabletopPose pose = CreatePose(1, 2, 45);

            ContainerPlacementState state = new ContainerPlacementState(containerId, pose);

            Assert.That(state.ContainerId, Is.EqualTo(containerId));
            Assert.That(state.Pose, Is.EqualTo(pose));
        }

        [Test]
        public void Constructor_WhenContainerIdIsEmpty_ThrowsArgumentException()
        {
            Assert.That(
                () => new ContainerPlacementState(ContainerId.Empty, TabletopPose.Default),
                Throws.ArgumentException);
        }

        [TestCase(double.NaN, 0, 0, TestName = "Constructor_WhenXIsNaN_ThrowsArgumentOutOfRangeException")]
        [TestCase(double.PositiveInfinity, 0, 0, TestName = "Constructor_WhenXIsPositiveInfinity_ThrowsArgumentOutOfRangeException")]
        [TestCase(0, double.NegativeInfinity, 0, TestName = "Constructor_WhenYIsNegativeInfinity_ThrowsArgumentOutOfRangeException")]
        [TestCase(0, 0, float.NaN, TestName = "Constructor_WhenRotationIsNaN_ThrowsArgumentOutOfRangeException")]
        [TestCase(0, 0, float.PositiveInfinity, TestName = "Constructor_WhenRotationIsPositiveInfinity_ThrowsArgumentOutOfRangeException")]
        public void Constructor_WhenPoseHasNonFiniteValue_ThrowsArgumentOutOfRangeException(
            double x,
            double y,
            float rotationDegrees)
        {
            Assert.That(
                () => new ContainerPlacementState(ContainerId.New(), CreatePose(x, y, rotationDegrees)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SetPose_WhenValid_UpdatesPose()
        {
            ContainerPlacementState state = new ContainerPlacementState(
                ContainerId.New(),
                TabletopPose.Default);
            TabletopPose updatedPose = CreatePose(3, 4, 90, layer: 2, localOrder: 5);

            state.SetPose(updatedPose);

            Assert.That(state.Pose, Is.EqualTo(updatedPose));
        }

        [Test]
        public void SetPose_WhenRotationIsNegativeAndNotNormalized_AcceptsValue()
        {
            ContainerPlacementState state = new ContainerPlacementState(
                ContainerId.New(),
                TabletopPose.Default);
            TabletopPose updatedPose = CreatePose(3, 4, -725);

            state.SetPose(updatedPose);

            Assert.That(state.Pose.RotationDegrees, Is.EqualTo(-725));
        }

        [Test]
        public void SetPose_WhenInvalid_PreservesPriorPose()
        {
            TabletopPose originalPose = CreatePose(1, 2, 30);
            ContainerPlacementState state = new ContainerPlacementState(ContainerId.New(), originalPose);

            Assert.That(
                () => state.SetPose(CreatePose(double.NaN, 5, 60)),
                Throws.TypeOf<ArgumentOutOfRangeException>());

            Assert.That(state.Pose, Is.EqualTo(originalPose));
        }

        [Test]
        public void SetPose_WhenSamePose_IsAllowed()
        {
            TabletopPose pose = CreatePose(1, 2, 30);
            ContainerPlacementState state = new ContainerPlacementState(ContainerId.New(), pose);

            state.SetPose(pose);

            Assert.That(state.Pose, Is.EqualTo(pose));
        }

        [Test]
        public void PublicContract_ContainsNoMembershipCollection()
        {
            Type placementType = typeof(ContainerPlacementState);

            bool exposesObjectIds = placementType
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(member => member.MemberType == MemberTypes.Property || member.MemberType == MemberTypes.Field)
                .Select(member => member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType)
                .Any(ContainsTabletopObjectId);

            Assert.That(exposesObjectIds, Is.False);
        }

        [Test]
        public void CoreAssembly_DoesNotReferenceUnityEngine()
        {
            bool referencesUnityEngine = typeof(ContainerPlacementState)
                .Assembly
                .GetReferencedAssemblies()
                .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal));

            Assert.That(referencesUnityEngine, Is.False);
        }

        private static bool ContainsTabletopObjectId(Type type)
        {
            if (type == typeof(TabletopObjectId))
            {
                return true;
            }

            if (!type.IsGenericType)
            {
                return false;
            }

            return type.GetGenericArguments().Any(ContainsTabletopObjectId);
        }

        private static TabletopPose CreatePose(
            double x,
            double y,
            float rotationDegrees,
            int layer = 0,
            int localOrder = 0)
        {
            return new TabletopPose(
                new TableCoordinate(x, y),
                rotationDegrees,
                layer,
                localOrder);
        }
    }
}
