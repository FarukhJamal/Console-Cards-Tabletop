using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.TableSurface;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class TabletopPointerProjectorTests
    {
        private const double Tolerance = 0.0001;

        private GameObject cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null)
            {
                Object.DestroyImmediate(cameraObject);
                cameraObject = null;
            }
        }

        [Test]
        public void Constructor_WithValidArguments_StoresSuppliedValues()
        {
            Camera camera = CreateCamera(true);
            TabletopCoordinateConverter converter = CreateConverter();

            TabletopPointerProjector projector = new TabletopPointerProjector(camera, converter, 1.5f);

            Assert.That(projector.TargetCamera, Is.SameAs(camera));
            Assert.That(projector.CoordinateConverter, Is.SameAs(converter));
            Assert.That(projector.TabletopHeight, Is.EqualTo(1.5f));
        }

        [Test]
        public void Constructor_WhenCameraIsNull_ThrowsArgumentNullException()
        {
            TabletopCoordinateConverter converter = CreateConverter();

            Assert.Throws<ArgumentNullException>(() => new TabletopPointerProjector(null, converter));
        }

        [Test]
        public void Constructor_WhenConverterIsNull_ThrowsArgumentNullException()
        {
            Camera camera = CreateCamera(true);

            Assert.Throws<ArgumentNullException>(() => new TabletopPointerProjector(camera, null));
        }

        [Test]
        public void Constructor_WhenCameraIsPerspective_ThrowsArgumentException()
        {
            Camera camera = CreateCamera(false);
            TabletopCoordinateConverter converter = CreateConverter();

            Assert.Throws<ArgumentException>(() => new TabletopPointerProjector(camera, converter));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenTabletopHeightIsNonFinite_ThrowsArgumentOutOfRangeException(float tabletopHeight)
        {
            Camera camera = CreateCamera(true);
            TabletopCoordinateConverter converter = CreateConverter();

            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopPointerProjector(camera, converter, tabletopHeight));
        }

        [Test]
        public void TryProjectRay_WithDownwardRay_IntersectsTabletopAtZeroHeight()
        {
            TabletopPointerProjector projector = CreateProjector();

            bool projected = projector.TryProjectRay(new Ray(new Vector3(2f, 10f, 3f), Vector3.down), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            AssertTableCoordinate(coordinate, 2.0, 3.0);
        }

        [Test]
        public void TryProjectRay_MapsWorldXZIntoLogicalXY()
        {
            TabletopPointerProjector projector = CreateProjector();

            bool projected = projector.TryProjectRay(new Ray(new Vector3(4.25f, 7f, -5.5f), Vector3.down), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            AssertTableCoordinate(coordinate, 4.25, -5.5);
        }

        [Test]
        public void TryProjectRay_RespectsNonZeroTabletopHeight()
        {
            TabletopPointerProjector projector = CreateProjector(tabletopHeight: 2f);

            bool projected = projector.TryProjectRay(new Ray(new Vector3(4f, 10f, 6f), Vector3.down), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            AssertTableCoordinate(coordinate, 4.0, 6.0);
        }

        [Test]
        public void TryProjectRay_RespectsNonUnitWorldScale()
        {
            TabletopPointerProjector projector = CreateProjector(worldUnitsPerTableUnit: 2f);

            bool projected = projector.TryProjectRay(new Ray(new Vector3(4f, 10f, 6f), Vector3.down), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            AssertTableCoordinate(coordinate, 2.0, 3.0);
        }

        [Test]
        public void TryProjectRay_PreservesNegativeWorldCoordinates()
        {
            TabletopPointerProjector projector = CreateProjector();

            bool projected = projector.TryProjectRay(new Ray(new Vector3(-4f, 10f, -6f), Vector3.down), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            AssertTableCoordinate(coordinate, -4.0, -6.0);
        }

        [Test]
        public void TryProjectRay_WhenRayIsParallelToPlane_ReturnsFalseAndOutputsZero()
        {
            TabletopPointerProjector projector = CreateProjector();

            bool projected = projector.TryProjectRay(new Ray(new Vector3(1f, 5f, 2f), Vector3.right), out TableCoordinate coordinate);

            Assert.That(projected, Is.False);
            Assert.That(coordinate, Is.EqualTo(TableCoordinate.Zero));
        }

        [Test]
        public void TryProjectRay_WhenRayPointsAwayFromPlane_ReturnsFalseAndOutputsZero()
        {
            TabletopPointerProjector projector = CreateProjector();

            bool projected = projector.TryProjectRay(new Ray(new Vector3(1f, 5f, 2f), Vector3.up), out TableCoordinate coordinate);

            Assert.That(projected, Is.False);
            Assert.That(coordinate, Is.EqualTo(TableCoordinate.Zero));
        }

        [Test]
        public void TryProjectRay_WhenRayBeginsOnPlane_SucceedsAtDistanceZero()
        {
            TabletopPointerProjector projector = CreateProjector();

            bool projected = projector.TryProjectRay(new Ray(new Vector3(3f, 0f, 4f), Vector3.down), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            AssertTableCoordinate(coordinate, 3.0, 4.0);
        }

        [TestCase(float.NaN, 0f, 0f)]
        [TestCase(float.PositiveInfinity, 0f, 0f)]
        [TestCase(float.NegativeInfinity, 0f, 0f)]
        [TestCase(0f, float.NaN, 0f)]
        [TestCase(0f, float.PositiveInfinity, 0f)]
        [TestCase(0f, float.NegativeInfinity, 0f)]
        [TestCase(0f, 0f, float.NaN)]
        [TestCase(0f, 0f, float.PositiveInfinity)]
        [TestCase(0f, 0f, float.NegativeInfinity)]
        public void TryProjectRay_WhenRayOriginComponentIsNonFinite_ThrowsArgumentOutOfRangeException(float x, float y, float z)
        {
            TabletopPointerProjector projector = CreateProjector();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => projector.TryProjectRay(new Ray(new Vector3(x, y, z), Vector3.down), out _));
        }

        [TestCase(float.NaN, -1f, 0f)]
        [TestCase(float.PositiveInfinity, -1f, 0f)]
        [TestCase(float.NegativeInfinity, -1f, 0f)]
        [TestCase(0f, float.NaN, 0f)]
        [TestCase(0f, float.PositiveInfinity, 0f)]
        [TestCase(0f, float.NegativeInfinity, 0f)]
        [TestCase(0f, -1f, float.NaN)]
        [TestCase(0f, -1f, float.PositiveInfinity)]
        [TestCase(0f, -1f, float.NegativeInfinity)]
        public void TryProjectRay_WhenRayDirectionComponentIsNonFinite_ThrowsArgumentOutOfRangeException(float x, float y, float z)
        {
            TabletopPointerProjector projector = CreateProjector();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => projector.TryProjectRay(new Ray(new Vector3(0f, 10f, 0f), new Vector3(x, y, z)), out _));
        }

        [Test]
        public void TryProjectRay_WhenDirectionIsZero_ThrowsArgumentOutOfRangeException()
        {
            TabletopPointerProjector projector = CreateProjector();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => projector.TryProjectRay(new Ray(new Vector3(0f, 10f, 0f), Vector3.zero), out _));
        }

        [Test]
        public void TryProjectRay_DoesNotMoveOrRotateCamera()
        {
            Camera camera = CreateCamera(true);
            Vector3 originalPosition = new Vector3(1f, 10f, 2f);
            Quaternion originalRotation = Quaternion.Euler(90f, 15f, 0f);
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            TabletopPointerProjector projector = new TabletopPointerProjector(camera, CreateConverter());

            projector.TryProjectRay(new Ray(new Vector3(2f, 10f, 3f), Vector3.down), out _);

            Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(Quaternion.Angle(originalRotation, camera.transform.rotation), Is.EqualTo(0f).Within((float)Tolerance));
        }

        [Test]
        public void TryProjectRay_RequiresNoColliderOrSurfaceProxy()
        {
            Camera camera = CreateCamera(true);
            TabletopPointerProjector projector = new TabletopPointerProjector(camera, CreateConverter());

            bool projected = projector.TryProjectRay(new Ray(new Vector3(2f, 10f, 3f), Vector3.down), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            Assert.That(camera.GetComponent<Collider>(), Is.Null);
            Assert.That(camera.GetComponent<TabletopSurfaceProxy>(), Is.Null);
            AssertTableCoordinate(coordinate, 2.0, 3.0);
        }

        private TabletopPointerProjector CreateProjector(float worldUnitsPerTableUnit = 1f, float tabletopHeight = 0f)
        {
            return new TabletopPointerProjector(
                CreateCamera(true),
                CreateConverter(worldUnitsPerTableUnit),
                tabletopHeight);
        }

        private Camera CreateCamera(bool orthographic)
        {
            cameraObject = new GameObject("Test Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = orthographic;
            return camera;
        }

        private static TabletopCoordinateConverter CreateConverter(float worldUnitsPerTableUnit = 1f)
        {
            return new TabletopCoordinateConverter(worldUnitsPerTableUnit, 0f, 0f, 0f);
        }

        private static void AssertTableCoordinate(TableCoordinate actual, double expectedX, double expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(Tolerance));
        }
    }
}
