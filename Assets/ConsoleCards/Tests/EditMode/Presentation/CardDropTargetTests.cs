using System;
using System.Linq;
using System.Reflection;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class CardDropTargetTests
    {
        [Test]
        public void None_CreatesNeutralInvalidTarget()
        {
            CardDropTarget target = CardDropTarget.None();

            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.None));
            Assert.That(target.ContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(target.TabletopPose, Is.EqualTo(TabletopPose.Default));
            Assert.That(target.IsValid, Is.False);
            Assert.That(target.IsContainer, Is.False);
            Assert.That(target.IsTabletop, Is.False);
        }

        [Test]
        public void ForContainer_StoresContainerIdOnly()
        {
            ContainerId containerId = ContainerId.New();

            CardDropTarget target = CardDropTarget.ForContainer(containerId);

            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Container));
            Assert.That(target.ContainerId, Is.EqualTo(containerId));
            Assert.That(target.TabletopPose, Is.EqualTo(TabletopPose.Default));
            Assert.That(target.IsValid, Is.True);
            Assert.That(target.IsContainer, Is.True);
            Assert.That(target.IsTabletop, Is.False);
        }

        [Test]
        public void ForTabletop_StoresPoseOnly()
        {
            TabletopPose pose = CreatePose(1.5, -2.25, 30f);

            CardDropTarget target = CardDropTarget.ForTabletop(pose);

            Assert.That(target.Kind, Is.EqualTo(CardDropTargetKind.Tabletop));
            Assert.That(target.ContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(target.TabletopPose, Is.EqualTo(pose));
            Assert.That(target.IsValid, Is.True);
            Assert.That(target.IsContainer, Is.False);
            Assert.That(target.IsTabletop, Is.True);
        }

        [Test]
        public void ForContainer_WhenContainerIdIsEmpty_Rejects()
        {
            Assert.Throws<ArgumentException>(() => CardDropTarget.ForContainer(ContainerId.Empty));
        }

        [TestCase(double.NaN, 0.0, 0f)]
        [TestCase(double.PositiveInfinity, 0.0, 0f)]
        [TestCase(double.NegativeInfinity, 0.0, 0f)]
        [TestCase(0.0, double.NaN, 0f)]
        [TestCase(0.0, double.PositiveInfinity, 0f)]
        [TestCase(0.0, double.NegativeInfinity, 0f)]
        [TestCase(0.0, 0.0, float.NaN)]
        [TestCase(0.0, 0.0, float.PositiveInfinity)]
        [TestCase(0.0, 0.0, float.NegativeInfinity)]
        public void ForTabletop_WhenPoseIsNonFinite_Rejects(double x, double y, float rotation)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CardDropTarget.ForTabletop(CreatePose(x, y, rotation)));
        }

        [Test]
        public void Equality_WhenValuesMatch_ComparesEqual()
        {
            ContainerId containerId = ContainerId.New();

            CardDropTarget left = CardDropTarget.ForContainer(containerId);
            CardDropTarget right = CardDropTarget.ForContainer(containerId);

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left == right, Is.True);
            Assert.That(left != right, Is.False);
        }

        [Test]
        public void Equality_WhenValuesDiffer_ComparesUnequal()
        {
            CardDropTarget container = CardDropTarget.ForContainer(ContainerId.New());
            CardDropTarget tabletop = CardDropTarget.ForTabletop(CreatePose(1.0, 2.0, 0f));

            Assert.That(container, Is.Not.EqualTo(tabletop));
            Assert.That(container == tabletop, Is.False);
            Assert.That(container != tabletop, Is.True);
        }

        [Test]
        public void GetHashCode_WhenValuesMatch_IsConsistent()
        {
            TabletopPose pose = CreatePose(2.0, 3.0, -45f);

            Assert.That(
                CardDropTarget.ForTabletop(pose).GetHashCode(),
                Is.EqualTo(CardDropTarget.ForTabletop(pose).GetHashCode()));
        }

        [Test]
        public void ToString_ContainsDiagnosticContent()
        {
            ContainerId containerId = ContainerId.New();

            string text = CardDropTarget.ForContainer(containerId).ToString();

            Assert.That(text, Does.Contain(CardDropTargetKind.Container.ToString()));
            Assert.That(text, Does.Contain(containerId.ToString()));
        }

        [Test]
        public void CardDropTargetKind_ContainsOnlyApprovedValues()
        {
            CardDropTargetKind[] values = Enum.GetValues(typeof(CardDropTargetKind))
                .Cast<CardDropTargetKind>()
                .ToArray();

            Assert.That(values, Is.EquivalentTo(new[]
            {
                CardDropTargetKind.None,
                CardDropTargetKind.Tabletop,
                CardDropTargetKind.Container
            }));
            Assert.That((int)CardDropTargetKind.None, Is.EqualTo(0));
            Assert.That((int)CardDropTargetKind.Tabletop, Is.EqualTo(1));
            Assert.That((int)CardDropTargetKind.Container, Is.EqualTo(2));
        }

        [Test]
        public void CardDropTarget_StoresNoUnityObjectReferences()
        {
            FieldInfo[] fields = typeof(CardDropTarget).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(fields.Any(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)), Is.False);
        }

        private static TabletopPose CreatePose(double x, double y, float rotation)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotation, 1, 2);
        }
    }
}
