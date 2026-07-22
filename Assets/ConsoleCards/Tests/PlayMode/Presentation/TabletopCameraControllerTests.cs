using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Camera;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopCameraControllerTests
    {
        private const float Tolerance = 0.0001f;

        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdObjects.Count; i++)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void ValidSetup_InitializesState()
        {
            TabletopCameraController controller = CreateInitializedController();

            Assert.That(controller.enabled, Is.True);
            Assert.That(controller.State, Is.Not.Null);
            Assert.That(controller.State.FocusCoordinate, Is.EqualTo(TableCoordinate.Zero));
        }

        [Test]
        public void ValidSetup_CameraIsOrthographic()
        {
            TabletopCameraController controller = CreateInitializedController();

            Assert.That(controller.TargetCamera.orthographic, Is.True);
        }

        [Test]
        public void ValidSetup_AppliesInitialCameraSize()
        {
            TabletopCameraController controller = CreateInitializedController(initialOrthographicSize: 7f);

            Assert.That(controller.TargetCamera.orthographicSize, Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void ValidSetup_CameraRigPositionUsesLogicalOriginAndCameraHeight()
        {
            TabletopCameraController controller = CreateInitializedController(cameraHeight: 12f);

            AssertVector3(controller.CameraRig.position, 0f, 12f, 0f);
        }

        [Test]
        public void Pan_MovesCameraRigXAndZ()
        {
            TabletopCameraController controller = CreateInitializedController(worldUnitsPerTableUnit: 2f, cameraHeight: 10f);

            controller.Pan(3.0, -4.0);

            AssertVector3(controller.CameraRig.position, 6f, 10f, -8f);
        }

        [Test]
        public void Zoom_ChangesAndClampsOrthographicSize()
        {
            TabletopCameraController controller = CreateInitializedController(
                minimumOrthographicSize: 2f,
                maximumOrthographicSize: 8f,
                initialOrthographicSize: 5f);

            controller.Zoom(2f);
            Assert.That(controller.TargetCamera.orthographicSize, Is.EqualTo(7f).Within(Tolerance));

            controller.Zoom(20f);
            Assert.That(controller.TargetCamera.orthographicSize, Is.EqualTo(8f).Within(Tolerance));

            controller.Zoom(-20f);
            Assert.That(controller.TargetCamera.orthographicSize, Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void Focus_MovesCameraRigToExpectedConvertedCoordinate()
        {
            TabletopCameraController controller = CreateInitializedController(worldUnitsPerTableUnit: 1.5f, cameraHeight: 9f);

            controller.Focus(new TableCoordinate(4.0, -2.0));

            AssertVector3(controller.CameraRig.position, 6f, 9f, -3f);
        }

        [Test]
        public void FocusWithSize_UpdatesPositionAndZoom()
        {
            TabletopCameraController controller = CreateInitializedController(worldUnitsPerTableUnit: 2f);

            controller.Focus(new TableCoordinate(-3.0, 4.0), 12f);

            AssertVector3(controller.CameraRig.position, -6f, 10f, 8f);
            Assert.That(controller.TargetCamera.orthographicSize, Is.EqualTo(12f).Within(Tolerance));
        }

        [Test]
        public void CaptureBookmark_CapturesCurrentLogicalFocus()
        {
            TabletopCameraController controller = CreateInitializedController();
            TableCoordinate focus = new TableCoordinate(3.0, -4.0);
            controller.Focus(focus);

            TabletopCameraBookmark bookmark = controller.CaptureBookmark("Current");

            Assert.That(bookmark.FocusCoordinate, Is.EqualTo(focus));
        }

        [Test]
        public void CaptureBookmark_CapturesCurrentOrthographicSize()
        {
            TabletopCameraController controller = CreateInitializedController();
            controller.Zoom(2f);

            TabletopCameraBookmark bookmark = controller.CaptureBookmark("Current");

            Assert.That(bookmark.OrthographicSize, Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void CaptureBookmark_PreservesSuppliedName()
        {
            TabletopCameraController controller = CreateInitializedController();

            TabletopCameraBookmark bookmark = controller.CaptureBookmark("Primary Play Area");

            Assert.That(bookmark.Name, Is.EqualTo("Primary Play Area"));
        }

        [Test]
        public void CaptureBookmark_WhenControllerIsNotInitialized_ThrowsInvalidOperationException()
        {
            GameObject controllerObject = CreateGameObject("Controller");
            controllerObject.SetActive(false);
            TabletopCameraController controller = controllerObject.AddComponent<TabletopCameraController>();

            Assert.Throws<System.InvalidOperationException>(() => controller.CaptureBookmark("Current"));
        }

        [Test]
        public void FocusBookmark_AppliesCoordinate()
        {
            TabletopCameraController controller = CreateInitializedController(worldUnitsPerTableUnit: 2f, cameraHeight: 11f);
            TabletopCameraBookmark bookmark = new TabletopCameraBookmark(
                "Saved",
                new TableCoordinate(-2.0, 3.0),
                6f);

            controller.Focus(bookmark);

            Assert.That(controller.State.FocusCoordinate, Is.EqualTo(bookmark.FocusCoordinate));
            AssertVector3(controller.CameraRig.position, -4f, 11f, 6f);
        }

        [Test]
        public void FocusBookmark_AppliesZoom()
        {
            TabletopCameraController controller = CreateInitializedController();
            TabletopCameraBookmark bookmark = new TabletopCameraBookmark("Saved", TableCoordinate.Zero, 8f);

            controller.Focus(bookmark);

            Assert.That(controller.State.OrthographicSize, Is.EqualTo(8f).Within(Tolerance));
            Assert.That(controller.TargetCamera.orthographicSize, Is.EqualTo(8f).Within(Tolerance));
        }

        [Test]
        public void FocusBookmark_ClampsZoomToControllerLimits()
        {
            TabletopCameraController controller = CreateInitializedController(
                minimumOrthographicSize: 2f,
                maximumOrthographicSize: 6f);
            TabletopCameraBookmark bookmark = new TabletopCameraBookmark("Saved", TableCoordinate.Zero, 10f);

            controller.Focus(bookmark);

            Assert.That(controller.State.OrthographicSize, Is.EqualTo(6f).Within(Tolerance));
            Assert.That(controller.TargetCamera.orthographicSize, Is.EqualTo(6f).Within(Tolerance));
        }

        [Test]
        public void FocusBookmark_LeavesCameraRotationUnchanged()
        {
            TabletopCameraController controller = CreateInitializedController();
            Quaternion originalRotation = Quaternion.Euler(90f, 25f, 5f);
            controller.TargetCamera.transform.rotation = originalRotation;
            TabletopCameraBookmark bookmark = new TabletopCameraBookmark(
                "Saved",
                new TableCoordinate(2.0, 3.0),
                6f);

            controller.Focus(bookmark);

            Assert.That(Quaternion.Angle(originalRotation, controller.TargetCamera.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void FocusBookmark_WhenControllerIsNotInitialized_ThrowsInvalidOperationException()
        {
            GameObject controllerObject = CreateGameObject("Controller");
            controllerObject.SetActive(false);
            TabletopCameraController controller = controllerObject.AddComponent<TabletopCameraController>();
            TabletopCameraBookmark bookmark = new TabletopCameraBookmark("Saved", TableCoordinate.Zero, 5f);

            Assert.Throws<System.InvalidOperationException>(() => controller.Focus(bookmark));
        }

        [Test]
        public void CaptureBookmark_DoesNotMutateCameraState()
        {
            TabletopCameraController controller = CreateInitializedController();
            controller.Focus(new TableCoordinate(3.0, 4.0), 7f);
            TableCoordinate originalFocus = controller.State.FocusCoordinate;
            float originalSize = controller.State.OrthographicSize;
            Vector3 originalRigPosition = controller.CameraRig.position;
            float originalCameraSize = controller.TargetCamera.orthographicSize;

            controller.CaptureBookmark("Current");

            Assert.That(controller.State.FocusCoordinate, Is.EqualTo(originalFocus));
            Assert.That(controller.State.OrthographicSize, Is.EqualTo(originalSize).Within(Tolerance));
            Assert.That(controller.CameraRig.position, Is.EqualTo(originalRigPosition));
            Assert.That(controller.TargetCamera.orthographicSize, Is.EqualTo(originalCameraSize).Within(Tolerance));
        }

        [Test]
        public void FocusBookmark_WhenApplyingOneBookmarkAfterAnother_RestoresEachState()
        {
            TabletopCameraController controller = CreateInitializedController(worldUnitsPerTableUnit: 1.5f);
            TabletopCameraBookmark first = new TabletopCameraBookmark(
                "First",
                new TableCoordinate(2.0, -4.0),
                6f);
            TabletopCameraBookmark second = new TabletopCameraBookmark(
                "Second",
                new TableCoordinate(-3.0, 5.0),
                9f);

            controller.Focus(first);
            Assert.That(controller.State.FocusCoordinate, Is.EqualTo(first.FocusCoordinate));
            Assert.That(controller.State.OrthographicSize, Is.EqualTo(first.OrthographicSize).Within(Tolerance));
            AssertVector3(controller.CameraRig.position, 3f, 10f, -6f);

            controller.Focus(second);
            Assert.That(controller.State.FocusCoordinate, Is.EqualTo(second.FocusCoordinate));
            Assert.That(controller.State.OrthographicSize, Is.EqualTo(second.OrthographicSize).Within(Tolerance));
            AssertVector3(controller.CameraRig.position, -4.5f, 10f, 7.5f);

            controller.Focus(first);
            Assert.That(controller.State.FocusCoordinate, Is.EqualTo(first.FocusCoordinate));
            Assert.That(controller.State.OrthographicSize, Is.EqualTo(first.OrthographicSize).Within(Tolerance));
            AssertVector3(controller.CameraRig.position, 3f, 10f, -6f);
        }

        [Test]
        public void ApplyState_DoesNotModifyCameraRotation()
        {
            TabletopCameraController controller = CreateInitializedController();
            Quaternion originalRotation = Quaternion.Euler(90f, 15f, 0f);
            controller.TargetCamera.transform.rotation = originalRotation;

            controller.Pan(1.0, 1.0);
            controller.Zoom(1f);
            controller.Focus(new TableCoordinate(2.0, 2.0));

            Assert.That(Quaternion.Angle(originalRotation, controller.TargetCamera.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ValidSetup_UsesOnlyLocalPresentationObjects()
        {
            TabletopCameraController controller = CreateInitializedController();

            controller.Pan(1.0, 1.0);

            Assert.That(controller.State.FocusCoordinate, Is.EqualTo(new TableCoordinate(1.0, 1.0)));
        }

        [Test]
        public void Awake_WhenCameraReferenceIsMissing_DisablesComponent()
        {
            Transform cameraRig = CreateGameObject("Camera Rig").transform;
            GameObject controllerObject = CreateGameObject("Controller");
            controllerObject.SetActive(false);
            TabletopCameraController controller = controllerObject.AddComponent<TabletopCameraController>();
            controller.cameraRig = cameraRig;

            LogAssert.Expect(LogType.Error, "TabletopCameraController requires a target Camera reference.");
            controllerObject.SetActive(true);

            Assert.That(controller.enabled, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => controller.Pan(1.0, 0.0));
        }

        [Test]
        public void Awake_WhenCameraRigReferenceIsMissing_DisablesComponent()
        {
            Camera targetCamera = CreateOrthographicCamera();
            GameObject controllerObject = CreateGameObject("Controller");
            controllerObject.SetActive(false);
            TabletopCameraController controller = controllerObject.AddComponent<TabletopCameraController>();
            controller.targetCamera = targetCamera;

            LogAssert.Expect(LogType.Error, "TabletopCameraController requires a CameraRig Transform reference.");
            controllerObject.SetActive(true);

            Assert.That(controller.enabled, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => controller.Zoom(1f));
        }

        [Test]
        public void Awake_WhenCameraIsPerspective_DisablesComponent()
        {
            Camera targetCamera = CreateOrthographicCamera();
            targetCamera.orthographic = false;

            LogAssert.Expect(LogType.Error, "TabletopCameraController requires an orthographic target Camera.");
            TabletopCameraController controller = CreateInitializedController(targetCamera: targetCamera);

            Assert.That(controller.enabled, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => controller.Focus(TableCoordinate.Zero));
        }

        [TestCase(0f, 10f, 2f, 20f, 5f, "TabletopCameraController requires finite worldUnitsPerTableUnit greater than zero.")]
        [TestCase(float.NaN, 10f, 2f, 20f, 5f, "TabletopCameraController requires finite worldUnitsPerTableUnit greater than zero.")]
        [TestCase(1f, float.NaN, 2f, 20f, 5f, "TabletopCameraController requires finite cameraHeight.")]
        [TestCase(1f, 10f, 0f, 20f, 5f, "TabletopCameraController requires finite minimumOrthographicSize greater than zero.")]
        [TestCase(1f, 10f, 5f, 4f, 5f, "TabletopCameraController requires finite maximumOrthographicSize greater than or equal to minimumOrthographicSize.")]
        [TestCase(1f, 10f, 2f, 20f, float.PositiveInfinity, "TabletopCameraController requires finite initialOrthographicSize.")]
        public void Awake_WhenConfigurationIsInvalid_DisablesComponent(
            float worldUnitsPerTableUnit,
            float cameraHeight,
            float minimumOrthographicSize,
            float maximumOrthographicSize,
            float initialOrthographicSize,
            string expectedMessage)
        {
            LogAssert.Expect(LogType.Error, expectedMessage);

            TabletopCameraController controller = CreateInitializedController(
                worldUnitsPerTableUnit: worldUnitsPerTableUnit,
                cameraHeight: cameraHeight,
                minimumOrthographicSize: minimumOrthographicSize,
                maximumOrthographicSize: maximumOrthographicSize,
                initialOrthographicSize: initialOrthographicSize);

            Assert.That(controller.enabled, Is.False);
            Assert.Throws<System.InvalidOperationException>(() => controller.ApplyState());
        }

        private TabletopCameraController CreateInitializedController(
            Camera targetCamera = null,
            float worldUnitsPerTableUnit = 1f,
            float cameraHeight = 10f,
            float minimumOrthographicSize = 2f,
            float maximumOrthographicSize = 20f,
            float initialOrthographicSize = 5f)
        {
            Camera assignedCamera = targetCamera != null ? targetCamera : CreateOrthographicCamera();
            Transform cameraRig = CreateGameObject("Camera Rig").transform;
            GameObject controllerObject = CreateGameObject("Controller");
            controllerObject.SetActive(false);

            TabletopCameraController controller = controllerObject.AddComponent<TabletopCameraController>();
            controller.targetCamera = assignedCamera;
            controller.cameraRig = cameraRig;
            controller.worldUnitsPerTableUnit = worldUnitsPerTableUnit;
            controller.cameraHeight = cameraHeight;
            controller.minimumOrthographicSize = minimumOrthographicSize;
            controller.maximumOrthographicSize = maximumOrthographicSize;
            controller.initialOrthographicSize = initialOrthographicSize;

            controllerObject.SetActive(true);

            return controller;
        }

        private Camera CreateOrthographicCamera()
        {
            GameObject cameraObject = CreateGameObject("Target Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            return camera;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void AssertVector3(Vector3 actual, float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expectedZ).Within(Tolerance));
        }
    }
}
