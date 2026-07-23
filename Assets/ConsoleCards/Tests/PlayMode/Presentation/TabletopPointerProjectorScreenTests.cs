using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.TableSurface;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopPointerProjectorScreenTests
    {
        private const double Tolerance = 0.0001;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                if (createdGameObjects[i] != null)
                {
                    Camera camera = createdGameObjects[i].GetComponent<Camera>();
                    if (camera != null)
                    {
                        camera.targetTexture = null;
                    }

                    Object.DestroyImmediate(createdGameObjects[i]);
                }
            }

            createdGameObjects.Clear();
        }

        [Test]
        public void TryProjectScreenPoint_WithScreenCenter_ProjectsToCameraFocusCoordinate()
        {
            CameraTestContext context = CreateCameraContext(cameraPosition: new Vector3(3f, 10f, -4f));

            bool projected = context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            AssertTableCoordinate(coordinate, 3.0, -4.0);
        }

        [Test]
        public void TryProjectScreenPoint_WhenCameraMovesXZ_ChangesProjectedCenterCoordinate()
        {
            CameraTestContext context = CreateCameraContext(cameraPosition: Vector3.up * 10f);
            Vector2 center = GetScreenPoint(context.Camera, 0.5f, 0.5f);
            context.Projector.TryProjectScreenPoint(center, out TableCoordinate originalCoordinate);

            context.Camera.transform.position = new Vector3(5f, 10f, -6f);
            context.Projector.TryProjectScreenPoint(center, out TableCoordinate movedCoordinate);

            Assert.That(movedCoordinate, Is.Not.EqualTo(originalCoordinate));
            AssertTableCoordinate(movedCoordinate, 5.0, -6.0);
        }

        [Test]
        public void TryProjectScreenPoint_WithScreenRight_ProjectsToGreaterLogicalXThanCenter()
        {
            CameraTestContext context = CreateCameraContext();

            context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out TableCoordinate center);
            context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.75f, 0.5f), out TableCoordinate right);

            Assert.That(right.X, Is.GreaterThan(center.X));
        }

        [Test]
        public void TryProjectScreenPoint_WithScreenLeft_ProjectsToLowerLogicalXThanCenter()
        {
            CameraTestContext context = CreateCameraContext();

            context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out TableCoordinate center);
            context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.25f, 0.5f), out TableCoordinate left);

            Assert.That(left.X, Is.LessThan(center.X));
        }

        [Test]
        public void TryProjectScreenPoint_WithScreenTop_ProjectsToGreaterLogicalYThanCenter()
        {
            CameraTestContext context = CreateCameraContext();

            context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out TableCoordinate center);
            context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.75f), out TableCoordinate top);

            Assert.That(top.Y, Is.GreaterThan(center.Y));
        }

        [Test]
        public void TryProjectScreenPoint_WithScreenBottom_ProjectsToLowerLogicalYThanCenter()
        {
            CameraTestContext context = CreateCameraContext();

            context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out TableCoordinate center);
            context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.25f), out TableCoordinate bottom);

            Assert.That(bottom.Y, Is.LessThan(center.Y));
        }

        [Test]
        public void TryProjectScreenPoint_WhenOrthographicSizeChanges_ChangesLogicalSpanButNotCenter()
        {
            CameraTestContext context = CreateCameraContext(orthographicSize: 5f);
            Vector2 center = GetScreenPoint(context.Camera, 0.5f, 0.5f);
            Vector2 top = GetScreenPoint(context.Camera, 0.5f, 0.75f);
            context.Projector.TryProjectScreenPoint(center, out TableCoordinate centerAtSizeFive);
            context.Projector.TryProjectScreenPoint(top, out TableCoordinate topAtSizeFive);

            context.Camera.orthographicSize = 10f;
            context.Projector.TryProjectScreenPoint(center, out TableCoordinate centerAtSizeTen);
            context.Projector.TryProjectScreenPoint(top, out TableCoordinate topAtSizeTen);

            AssertTableCoordinate(centerAtSizeTen, centerAtSizeFive.X, centerAtSizeFive.Y);
            Assert.That(topAtSizeTen.Y - centerAtSizeTen.Y, Is.GreaterThan(topAtSizeFive.Y - centerAtSizeFive.Y));
        }

        [Test]
        public void TryProjectScreenPoint_AppliesNonUnitWorldScale()
        {
            CameraTestContext context = CreateCameraContext(
                cameraPosition: new Vector3(4f, 10f, 6f),
                worldUnitsPerTableUnit: 2f);

            bool projected = context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            AssertTableCoordinate(coordinate, 2.0, 3.0);
        }

        [Test]
        public void TryProjectScreenPoint_SupportsNonZeroTabletopHeight()
        {
            CameraTestContext context = CreateCameraContext(
                cameraPosition: new Vector3(2f, 10f, 3f),
                tabletopHeight: 2f);

            bool projected = context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out TableCoordinate coordinate);

            Assert.That(projected, Is.True);
            AssertTableCoordinate(coordinate, 2.0, 3.0);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TryProjectScreenPoint_WhenScreenXIsNonFinite_ThrowsArgumentOutOfRangeException(float screenX)
        {
            CameraTestContext context = CreateCameraContext();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Projector.TryProjectScreenPoint(new Vector2(screenX, GetScreenPoint(context.Camera, 0.5f, 0.5f).y), out _));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TryProjectScreenPoint_WhenScreenYIsNonFinite_ThrowsArgumentOutOfRangeException(float screenY)
        {
            CameraTestContext context = CreateCameraContext();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Projector.TryProjectScreenPoint(new Vector2(GetScreenPoint(context.Camera, 0.5f, 0.5f).x, screenY), out _));
        }

        [Test]
        public void TestSetup_RequiresNoCollider()
        {
            CameraTestContext context = CreateCameraContext();

            bool projected = context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out _);

            Assert.That(projected, Is.True);
            Assert.That(context.Camera.GetComponent<Collider>(), Is.Null);
        }

        [Test]
        public void TestSetup_RequiresNoTabletopSurfaceProxy()
        {
            CameraTestContext context = CreateCameraContext();

            bool projected = context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out _);

            Assert.That(projected, Is.True);
            Assert.That(context.Camera.GetComponent<TabletopSurfaceProxy>(), Is.Null);
        }

        [Test]
        public void TryProjectScreenPoint_DoesNotMutateCamera()
        {
            Vector3 originalPosition = new Vector3(3f, 10f, -4f);
            Quaternion originalRotation = Quaternion.Euler(90f, 0f, 0f);
            float originalOrthographicSize = 7f;
            CameraTestContext context = CreateCameraContext(
                cameraPosition: originalPosition,
                orthographicSize: originalOrthographicSize);

            context.Projector.TryProjectScreenPoint(GetScreenPoint(context.Camera, 0.5f, 0.5f), out _);

            Assert.That(context.Camera.transform.position, Is.EqualTo(originalPosition));
            Assert.That(Quaternion.Angle(originalRotation, context.Camera.transform.rotation), Is.EqualTo(0f).Within((float)Tolerance));
            Assert.That(context.Camera.orthographicSize, Is.EqualTo(originalOrthographicSize).Within((float)Tolerance));
        }

        private CameraTestContext CreateCameraContext(
            Vector3? cameraPosition = null,
            float orthographicSize = 5f,
            float worldUnitsPerTableUnit = 1f,
            float tabletopHeight = 0f)
        {
            GameObject cameraObject = new GameObject("Pointer Projector Camera");
            createdGameObjects.Add(cameraObject);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.aspect = 800f / 600f;
            camera.transform.SetPositionAndRotation(cameraPosition ?? (Vector3.up * 10f), Quaternion.Euler(90f, 0f, 0f));

            TabletopCoordinateConverter converter = new TabletopCoordinateConverter(worldUnitsPerTableUnit, 0f, 0f, 0f);
            TabletopPointerProjector projector = new TabletopPointerProjector(camera, converter, tabletopHeight);

            return new CameraTestContext(camera, projector);
        }

        private static void AssertTableCoordinate(TableCoordinate actual, double expectedX, double expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(Tolerance));
        }

        private static Vector2 GetScreenPoint(Camera camera, float normalizedX, float normalizedY)
        {
            Assert.That(camera.targetTexture, Is.Null);
            Assert.That(camera.pixelWidth, Is.GreaterThan(0));
            Assert.That(camera.pixelHeight, Is.GreaterThan(0));

            Vector3 point = camera.ViewportToScreenPoint(new Vector3(normalizedX, normalizedY, 0f));

            Assert.That(float.IsFinite(point.x), Is.True);
            Assert.That(float.IsFinite(point.y), Is.True);

            return new Vector2(point.x, point.y);
        }

        private sealed class CameraTestContext
        {
            public CameraTestContext(Camera camera, TabletopPointerProjector projector)
            {
                Camera = camera;
                Projector = projector;
            }

            public Camera Camera { get; }

            public TabletopPointerProjector Projector { get; }
        }
    }
}
