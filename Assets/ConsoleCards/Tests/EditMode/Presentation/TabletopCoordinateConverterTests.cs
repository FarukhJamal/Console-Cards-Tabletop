using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Coordinates;
using NUnit.Framework;
using UnityEngine;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class TabletopCoordinateConverterTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Constructor_StoresSuppliedConfigurationValues()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(2.5f, 0.25f, 0.02f, 0.001f);

            Assert.That(converter.WorldUnitsPerTableUnit, Is.EqualTo(2.5f));
            Assert.That(converter.BaseHeight, Is.EqualTo(0.25f));
            Assert.That(converter.LayerHeight, Is.EqualTo(0.02f));
            Assert.That(converter.LocalOrderHeight, Is.EqualTo(0.001f));
        }

        [Test]
        public void Constructor_WhenWorldScaleIsZero_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopCoordinateConverter(0f, 0f, 0f, 0f));
        }

        [Test]
        public void Constructor_WhenWorldScaleIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopCoordinateConverter(-1f, 0f, 0f, 0f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenWorldScaleIsNonFinite_ThrowsArgumentOutOfRangeException(float worldUnitsPerTableUnit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCoordinateConverter(worldUnitsPerTableUnit, 0f, 0f, 0f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenBaseHeightIsNonFinite_ThrowsArgumentOutOfRangeException(float baseHeight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCoordinateConverter(1f, baseHeight, 0f, 0f));
        }

        [Test]
        public void Constructor_WhenLayerHeightIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopCoordinateConverter(1f, 0f, -0.01f, 0f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenLayerHeightIsNonFinite_ThrowsArgumentOutOfRangeException(float layerHeight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCoordinateConverter(1f, 0f, layerHeight, 0f));
        }

        [Test]
        public void Constructor_WhenLocalOrderHeightIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopCoordinateConverter(1f, 0f, 0f, -0.01f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenLocalOrderHeightIsNonFinite_ThrowsArgumentOutOfRangeException(float localOrderHeight)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TabletopCoordinateConverter(1f, 0f, 0f, localOrderHeight));
        }

        [Test]
        public void ToWorldPosition_WithLogicalOrigin_MapsToWorldOriginAndBaseHeight()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(2f, 0.5f, 0.1f, 0.01f);

            Vector3 position = converter.ToWorldPosition(TableCoordinate.Zero);

            AssertVector3(position, 0f, 0.5f, 0f);
        }

        [Test]
        public void ToWorldPosition_WithPositiveLogicalCoordinate_MapsToPositiveUnityXAndZ()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);

            Vector3 position = converter.ToWorldPosition(new TableCoordinate(3.25, 4.5));

            AssertVector3(position, 3.25f, 0f, 4.5f);
        }

        [Test]
        public void ToWorldPosition_WithNegativeLogicalCoordinate_MapsToNegativeUnityXAndZ()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);

            Vector3 position = converter.ToWorldPosition(new TableCoordinate(-3.25, -4.5));

            AssertVector3(position, -3.25f, 0f, -4.5f);
        }

        [Test]
        public void ToWorldPosition_AppliesWorldUnitsPerTableUnit()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(2.5f, 0f, 0f, 0f);

            Vector3 position = converter.ToWorldPosition(new TableCoordinate(2.0, -4.0));

            AssertVector3(position, 5f, 0f, -10f);
        }

        [Test]
        public void ToWorldPosition_WithTableCoordinate_UsesBaseHeightOnly()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0.3f, 10f, 5f);

            Vector3 position = converter.ToWorldPosition(new TableCoordinate(1.0, 2.0));

            AssertVector3(position, 1f, 0.3f, 2f);
        }

        [Test]
        public void ToWorldPosition_WithLargeFiniteLogicalCoordinate_MapsToExpectedFloatValues()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(0.5f, 0f, 0f, 0f);

            Vector3 position = converter.ToWorldPosition(new TableCoordinate(1_000_000.25, -2_000_000.5));

            AssertVector3(position, 500_000.125f, 0f, -1_000_000.25f);
        }

        [TestCase(double.NaN, 0.0)]
        [TestCase(0.0, double.NaN)]
        public void ToWorldPosition_WhenCoordinateContainsNaN_ThrowsArgumentOutOfRangeException(double x, double y)
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => converter.ToWorldPosition(new TableCoordinate(x, y)));
        }

        [TestCase(double.PositiveInfinity, 0.0)]
        [TestCase(double.NegativeInfinity, 0.0)]
        [TestCase(0.0, double.PositiveInfinity)]
        [TestCase(0.0, double.NegativeInfinity)]
        public void ToWorldPosition_WhenCoordinateContainsInfinity_ThrowsArgumentOutOfRangeException(double x, double y)
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);

            Assert.Throws<ArgumentOutOfRangeException>(() => converter.ToWorldPosition(new TableCoordinate(x, y)));
        }

        [TestCase(double.MaxValue, 0.0)]
        [TestCase(0.0, double.MinValue)]
        public void ToWorldPosition_WhenCoordinateOverflowsUnityFloatPosition_ThrowsOverflowException(double x, double y)
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);

            Assert.Throws<OverflowException>(() => converter.ToWorldPosition(new TableCoordinate(x, y)));
        }

        [Test]
        public void ToWorldPosition_WithPose_UsesApprovedXZMapping()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(3f, 0f, 0f, 0f);
            TabletopPose pose = new TabletopPose(new TableCoordinate(2.0, -4.0), 0f, 0, 0);

            Vector3 position = converter.ToWorldPosition(pose);

            AssertVector3(position, 6f, 0f, -12f);
        }

        [Test]
        public void ToWorldPosition_WithPose_AppliesBaseHeight()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0.4f, 0f, 0f);

            Vector3 position = converter.ToWorldPosition(TabletopPose.Default);

            AssertVector3(position, 0f, 0.4f, 0f);
        }

        [Test]
        public void ToWorldPosition_WithPose_AppliesLayerHeight()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0.4f, 0.2f, 0f);
            TabletopPose pose = new TabletopPose(TableCoordinate.Zero, 0f, 3, 0);

            Vector3 position = converter.ToWorldPosition(pose);

            AssertVector3(position, 0f, 1.0f, 0f);
        }

        [Test]
        public void ToWorldPosition_WithPose_AppliesLocalOrderHeight()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0.4f, 0f, 0.03f);
            TabletopPose pose = new TabletopPose(TableCoordinate.Zero, 0f, 0, 5);

            Vector3 position = converter.ToWorldPosition(pose);

            AssertVector3(position, 0f, 0.55f, 0f);
        }

        [Test]
        public void ToWorldPosition_WithNegativeLayerAndLocalOrder_DoesNotClampVerticalPosition()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 1.0f, 0.2f, 0.03f);
            TabletopPose pose = new TabletopPose(TableCoordinate.Zero, 0f, -2, -5);

            Vector3 position = converter.ToWorldPosition(pose);

            AssertVector3(position, 0f, 0.45f, 0f);
        }

        [Test]
        public void ToWorldPosition_WhenPosesAreEqual_ProducesEqualWorldPositions()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1.5f, 0.1f, 0.02f, 0.001f);
            TabletopPose first = new TabletopPose(new TableCoordinate(2.0, 3.0), 45f, 2, 7);
            TabletopPose second = new TabletopPose(new TableCoordinate(2.0, 3.0), 45f, 2, 7);

            Vector3 firstPosition = converter.ToWorldPosition(first);
            Vector3 secondPosition = converter.ToWorldPosition(second);

            Assert.That(firstPosition, Is.EqualTo(secondPosition));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ToWorldPosition_WithPoseWhenRotationIsNonFinite_ThrowsArgumentOutOfRangeException(float rotationDegrees)
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
            TabletopPose pose = new TabletopPose(TableCoordinate.Zero, rotationDegrees, 0, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => converter.ToWorldPosition(pose));
        }

        [Test]
        public void ToWorldRotation_WithZeroRotation_ProducesIdentityEquivalentYRotation()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);

            Quaternion rotation = converter.ToWorldRotation(TabletopPose.Default);

            Assert.That(Quaternion.Angle(Quaternion.identity, rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ToWorldRotation_WithPositiveRotation_MapsToUnityYRotation()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
            TabletopPose pose = new TabletopPose(TableCoordinate.Zero, 90f, 0, 0);

            Quaternion rotation = converter.ToWorldRotation(pose);

            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ToWorldRotation_WithNegativeRotation_PreservesRotationDirection()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
            TabletopPose pose = new TabletopPose(TableCoordinate.Zero, -45f, 0, 0);

            Quaternion rotation = converter.ToWorldRotation(pose);

            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, -45f, 0f), rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ToWorldRotation_WhenRotationIsNonFinite_ThrowsArgumentOutOfRangeException(float rotationDegrees)
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
            TabletopPose pose = new TabletopPose(TableCoordinate.Zero, rotationDegrees, 0, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => converter.ToWorldRotation(pose));
        }

        [Test]
        public void ToWorldRotation_DoesNotIntroduceXOrZRotation()
        {
            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
            TabletopPose pose = new TabletopPose(TableCoordinate.Zero, 135f, 0, 0);

            Quaternion rotation = converter.ToWorldRotation(pose);
            Vector3 rotatedUp = rotation * Vector3.up;

            AssertVector3(rotatedUp, Vector3.up.x, Vector3.up.y, Vector3.up.z);
        }

        private static void AssertVector3(Vector3 actual, float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expectedZ).Within(Tolerance));
        }
    }
}
