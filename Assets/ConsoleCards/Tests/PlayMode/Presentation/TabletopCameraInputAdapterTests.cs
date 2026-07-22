using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityCamera = UnityEngine.Camera;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopCameraInputAdapterTests
    {
        private const float Tolerance = 0.0001f;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();
        private readonly List<InputActionAsset> createdInputAssets = new List<InputActionAsset>();
        private readonly List<InputActionReference> createdActionReferences = new List<InputActionReference>();

        public enum RequiredActionReference
        {
            KeyboardPan,
            DragPan,
            PointerDelta,
            Zoom
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdInputAssets.Count; i++)
            {
                if (createdInputAssets[i] != null)
                {
                    createdInputAssets[i].Disable();
                }
            }

            for (int i = 0; i < createdActionReferences.Count; i++)
            {
                if (createdActionReferences[i] != null)
                {
                    UnityObject.DestroyImmediate(createdActionReferences[i]);
                }
            }

            for (int i = 0; i < createdInputAssets.Count; i++)
            {
                if (createdInputAssets[i] != null)
                {
                    UnityObject.DestroyImmediate(createdInputAssets[i]);
                }
            }

            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                if (createdGameObjects[i] != null)
                {
                    UnityObject.DestroyImmediate(createdGameObjects[i]);
                }
            }

            createdActionReferences.Clear();
            createdInputAssets.Clear();
            createdGameObjects.Clear();
        }

        [Test]
        public void ValidReferencesAndConfiguration_InitializesSuccessfully()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            Assert.That(context.Adapter.enabled, Is.True);
            Assert.That(context.Adapter.IsInitialized, Is.True);
            Assert.That(context.Adapter.CameraController, Is.SameAs(context.Controller));
            Assert.That(context.Adapter.KeyboardPanSpeed, Is.EqualTo(5f));
            Assert.That(context.Adapter.DragPanUnitsPerPixel, Is.EqualTo(0.02f));
            Assert.That(context.Adapter.ZoomSensitivity, Is.EqualTo(0.01f));
        }

        [Test]
        public void Awake_WhenCameraControllerIsMissing_DisablesAdapter()
        {
            LogAssert.Expect(LogType.Error, "TabletopCameraInputAdapter requires a TabletopCameraController reference.");

            AdapterTestContext context = CreateInitializedAdapter(assignCameraController: false);

            Assert.That(context.Adapter.enabled, Is.False);
            Assert.That(context.Adapter.IsInitialized, Is.False);
        }

        [TestCase(RequiredActionReference.KeyboardPan, "TabletopCameraInputAdapter requires a KeyboardPan InputActionReference.")]
        [TestCase(RequiredActionReference.DragPan, "TabletopCameraInputAdapter requires a DragPan InputActionReference.")]
        [TestCase(RequiredActionReference.PointerDelta, "TabletopCameraInputAdapter requires a PointerDelta InputActionReference.")]
        [TestCase(RequiredActionReference.Zoom, "TabletopCameraInputAdapter requires a Zoom InputActionReference.")]
        public void Awake_WhenRequiredInputActionReferenceIsMissing_DisablesAdapter(
            RequiredActionReference missingReference,
            string expectedMessage)
        {
            LogAssert.Expect(LogType.Error, expectedMessage);

            AdapterTestContext context = CreateInitializedAdapter(missingReference: missingReference);

            Assert.That(context.Adapter.enabled, Is.False);
            Assert.That(context.Adapter.IsInitialized, Is.False);
        }

        [Test]
        public void Awake_WhenInputActionReferenceHasNoAction_DisablesAdapter()
        {
            InputActionReference referenceWithoutAction = CreateReferenceWithoutAction();
            LogAssert.Expect(LogType.Error, "TabletopCameraInputAdapter requires the KeyboardPan InputActionReference to resolve to an InputAction.");

            AdapterTestContext context = CreateInitializedAdapter(keyboardPanAction: referenceWithoutAction);

            Assert.That(context.Adapter.enabled, Is.False);
            Assert.That(context.Adapter.IsInitialized, Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Awake_WhenKeyboardPanSpeedIsInvalid_DisablesAdapter(float keyboardPanSpeed)
        {
            LogAssert.Expect(LogType.Error, "TabletopCameraInputAdapter requires finite keyboardPanSpeed greater than or equal to zero.");

            AdapterTestContext context = CreateInitializedAdapter(keyboardPanSpeed: keyboardPanSpeed);

            Assert.That(context.Adapter.enabled, Is.False);
            Assert.That(context.Adapter.IsInitialized, Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Awake_WhenDragPanUnitsPerPixelIsInvalid_DisablesAdapter(float dragPanUnitsPerPixel)
        {
            LogAssert.Expect(LogType.Error, "TabletopCameraInputAdapter requires finite dragPanUnitsPerPixel greater than or equal to zero.");

            AdapterTestContext context = CreateInitializedAdapter(dragPanUnitsPerPixel: dragPanUnitsPerPixel);

            Assert.That(context.Adapter.enabled, Is.False);
            Assert.That(context.Adapter.IsInitialized, Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Awake_WhenZoomSensitivityIsInvalid_DisablesAdapter(float zoomSensitivity)
        {
            LogAssert.Expect(LogType.Error, "TabletopCameraInputAdapter requires finite zoomSensitivity greater than or equal to zero.");

            AdapterTestContext context = CreateInitializedAdapter(zoomSensitivity: zoomSensitivity);

            Assert.That(context.Adapter.enabled, Is.False);
            Assert.That(context.Adapter.IsInitialized, Is.False);
        }

        [Test]
        public void OnEnable_WhenInitialized_EnablesFourActions()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            Assert.That(context.KeyboardPanAction.action.enabled, Is.True);
            Assert.That(context.DragPanAction.action.enabled, Is.True);
            Assert.That(context.PointerDeltaAction.action.enabled, Is.True);
            Assert.That(context.ZoomAction.action.enabled, Is.True);
        }

        [Test]
        public void OnDisable_WhenInitialized_DisablesActionsEnabledByAdapter()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.enabled = false;

            Assert.That(context.KeyboardPanAction.action.enabled, Is.False);
            Assert.That(context.DragPanAction.action.enabled, Is.False);
            Assert.That(context.PointerDeltaAction.action.enabled, Is.False);
            Assert.That(context.ZoomAction.action.enabled, Is.False);
        }

        [Test]
        public void OnDisable_WhenActionsWereAlreadyEnabled_DoesNotDisableExternallyEnabledActions()
        {
            AdapterTestContext context = CreateInitializedAdapter(enableActionsBeforeAdapter: true);

            context.Adapter.enabled = false;

            Assert.That(context.KeyboardPanAction.action.enabled, Is.True);
            Assert.That(context.DragPanAction.action.enabled, Is.True);
            Assert.That(context.PointerDeltaAction.action.enabled, Is.True);
            Assert.That(context.ZoomAction.action.enabled, Is.True);
        }

        [Test]
        public void Lifecycle_WhenInvalidAdapter_DoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, "TabletopCameraInputAdapter requires a TabletopCameraController reference.");
            AdapterTestContext context = CreateInitializedAdapter(assignCameraController: false);

            Assert.DoesNotThrow(() =>
            {
                context.Adapter.enabled = true;
                context.Adapter.enabled = false;
                context.Adapter.gameObject.SetActive(false);
                context.Adapter.gameObject.SetActive(true);
            });
        }

        [Test]
        public void ValidSetup_RequiresOnlyAdapterAndControllerComponents()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            Assert.That(context.Adapter.GetComponents<Component>(), Has.Length.EqualTo(2));
            Assert.That(context.Controller.GetComponents<Component>(), Has.Length.EqualTo(2));
        }

        [Test]
        public void ApplyInputFrame_WhenKeyboardRightIsPressed_PansPositiveLogicalX()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(new Vector2(1f, 0f), false, Vector2.zero, 0f, 1f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, 5.0, 0.0);
        }

        [Test]
        public void ApplyInputFrame_WhenKeyboardUpIsPressed_PansPositiveLogicalY()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(new Vector2(0f, 1f), false, Vector2.zero, 0f, 1f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, 0.0, 5.0);
        }

        [Test]
        public void ApplyInputFrame_WhenKeyboardPans_UsesUnscaledDeltaTime()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(new Vector2(1f, 0f), false, Vector2.zero, 0f, 0.25f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, 1.25, 0.0);
        }

        [Test]
        public void ApplyInputFrame_WhenKeyboardPanIsDiagonal_ClampsMagnitude()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(new Vector2(1f, 1f), false, Vector2.zero, 0f, 1f);

            float expected = Mathf.Sqrt(0.5f) * 5f;
            AssertCoordinate(context.Controller.State.FocusCoordinate, expected, expected);
        }

        [Test]
        public void ApplyInputFrame_WhenDeltaTimeIsZero_ProducesNoKeyboardMovement()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(new Vector2(1f, 0f), false, Vector2.zero, 0f, 0f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, 0.0, 0.0);
        }

        [Test]
        public void ApplyInputFrame_WhenDraggingRight_MovesFocusNegativeLogicalX()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(Vector2.zero, true, new Vector2(10f, 0f), 0f, 1f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, -0.2, 0.0);
        }

        [Test]
        public void ApplyInputFrame_WhenDraggingUp_MovesFocusNegativeLogicalY()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(Vector2.zero, true, new Vector2(0f, 10f), 0f, 1f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, 0.0, -0.2);
        }

        [Test]
        public void ApplyInputFrame_WhenDragIsNotHeld_IgnoresPointerDelta()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(Vector2.zero, false, new Vector2(10f, 10f), 0f, 1f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, 0.0, 0.0);
        }

        [Test]
        public void ApplyInputFrame_WhenDragging_DoesNotMultiplyDragByDeltaTime()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(Vector2.zero, true, new Vector2(10f, 0f), 0f, 100f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, -0.2, 0.0);
        }

        [Test]
        public void ApplyInputFrame_WhenKeyboardAndDragArePresent_CombinesPan()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(new Vector2(1f, 0f), true, new Vector2(100f, 0f), 0f, 1f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, 3.0, 0.0);
        }

        [Test]
        public void ApplyInputFrame_WhenScrollIsPositive_ZoomsIn()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(Vector2.zero, false, Vector2.zero, 100f, 1f);

            Assert.That(context.Controller.State.OrthographicSize, Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void ApplyInputFrame_WhenScrollIsNegative_ZoomsOut()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(Vector2.zero, false, Vector2.zero, -100f, 1f);

            Assert.That(context.Controller.State.OrthographicSize, Is.EqualTo(6f).Within(Tolerance));
        }

        [Test]
        public void ApplyInputFrame_WhenZoomExceedsLimits_RespectsCameraStateLimits()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(Vector2.zero, false, Vector2.zero, 1000f, 1f);
            Assert.That(context.Controller.State.OrthographicSize, Is.EqualTo(2f).Within(Tolerance));

            context.Adapter.ApplyInputFrame(Vector2.zero, false, Vector2.zero, -10000f, 1f);
            Assert.That(context.Controller.State.OrthographicSize, Is.EqualTo(20f).Within(Tolerance));
        }

        [Test]
        public void ApplyInputFrame_WhenInputIsZero_LeavesCameraStateUnchanged()
        {
            AdapterTestContext context = CreateInitializedAdapter();
            TableCoordinate originalFocus = context.Controller.State.FocusCoordinate;
            float originalSize = context.Controller.State.OrthographicSize;

            context.Adapter.ApplyInputFrame(Vector2.zero, false, Vector2.zero, 0f, 1f);

            Assert.That(context.Controller.State.FocusCoordinate, Is.EqualTo(originalFocus));
            Assert.That(context.Controller.State.OrthographicSize, Is.EqualTo(originalSize));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, float.NegativeInfinity)]
        public void ApplyInputFrame_WhenKeyboardPanIsInvalid_ThrowsWithoutMutation(float x, float y)
        {
            AdapterTestContext context = CreateInitializedAdapter();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Adapter.ApplyInputFrame(new Vector2(x, y), false, Vector2.zero, 0f, 1f));

            AssertUnchanged(context);
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, float.NegativeInfinity)]
        public void ApplyInputFrame_WhenPointerDeltaIsInvalid_ThrowsWithoutMutation(float x, float y)
        {
            AdapterTestContext context = CreateInitializedAdapter();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Adapter.ApplyInputFrame(Vector2.zero, true, new Vector2(x, y), 0f, 1f));

            AssertUnchanged(context);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ApplyInputFrame_WhenZoomDeltaIsInvalid_ThrowsWithoutMutation(float zoomDelta)
        {
            AdapterTestContext context = CreateInitializedAdapter();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Adapter.ApplyInputFrame(Vector2.zero, false, Vector2.zero, zoomDelta, 1f));

            AssertUnchanged(context);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ApplyInputFrame_WhenUnscaledDeltaTimeIsInvalid_ThrowsWithoutMutation(float unscaledDeltaTime)
        {
            AdapterTestContext context = CreateInitializedAdapter();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => context.Adapter.ApplyInputFrame(Vector2.zero, false, Vector2.zero, 0f, unscaledDeltaTime));

            AssertUnchanged(context);
        }

        [Test]
        public void ApplyInputFrame_UpdatesCameraThroughController()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(new Vector2(1f, 0f), false, Vector2.zero, 100f, 1f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, 5.0, 0.0);
            AssertVector3(context.Controller.CameraRig.position, 5f, 10f, 0f);
            Assert.That(context.Controller.TargetCamera.orthographicSize, Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void ValidSetup_DoesNotRequireMatchStateOrApplicationObjects()
        {
            AdapterTestContext context = CreateInitializedAdapter();

            context.Adapter.ApplyInputFrame(new Vector2(0f, 1f), false, Vector2.zero, 0f, 1f);

            AssertCoordinate(context.Controller.State.FocusCoordinate, 0.0, 5.0);
        }

        private AdapterTestContext CreateInitializedAdapter(
            bool assignCameraController = true,
            bool enableActionsBeforeAdapter = false,
            RequiredActionReference? missingReference = null,
            InputActionReference keyboardPanAction = null,
            float keyboardPanSpeed = 5f,
            float dragPanUnitsPerPixel = 0.02f,
            float zoomSensitivity = 0.01f)
        {
            TabletopCameraController controller = CreateInitializedController();
            InputActionMap actionMap = CreateActionMap();
            InputActionReference createdKeyboardPanAction = CreateActionReference(actionMap, "KeyboardPan", InputActionType.Value, "Vector2");
            InputActionReference createdDragPanAction = CreateActionReference(actionMap, "DragPan", InputActionType.Button, "Button");
            InputActionReference createdPointerDeltaAction = CreateActionReference(actionMap, "PointerDelta", InputActionType.PassThrough, "Vector2");
            InputActionReference createdZoomAction = CreateActionReference(actionMap, "Zoom", InputActionType.PassThrough, "Axis");

            InputActionReference assignedKeyboardPanAction = missingReference == RequiredActionReference.KeyboardPan
                ? null
                : keyboardPanAction ?? createdKeyboardPanAction;
            InputActionReference assignedDragPanAction = missingReference == RequiredActionReference.DragPan
                ? null
                : createdDragPanAction;
            InputActionReference assignedPointerDeltaAction = missingReference == RequiredActionReference.PointerDelta
                ? null
                : createdPointerDeltaAction;
            InputActionReference assignedZoomAction = missingReference == RequiredActionReference.Zoom
                ? null
                : createdZoomAction;

            if (enableActionsBeforeAdapter)
            {
                assignedKeyboardPanAction.action.Enable();
                assignedDragPanAction.action.Enable();
                assignedPointerDeltaAction.action.Enable();
                assignedZoomAction.action.Enable();
            }

            GameObject adapterObject = CreateGameObject("Tabletop Camera Input Adapter");
            adapterObject.SetActive(false);
            TabletopCameraInputAdapter adapter = adapterObject.AddComponent<TabletopCameraInputAdapter>();
            adapter.cameraController = assignCameraController ? controller : null;
            adapter.keyboardPanAction = assignedKeyboardPanAction;
            adapter.dragPanAction = assignedDragPanAction;
            adapter.pointerDeltaAction = assignedPointerDeltaAction;
            adapter.zoomAction = assignedZoomAction;
            adapter.keyboardPanSpeed = keyboardPanSpeed;
            adapter.dragPanUnitsPerPixel = dragPanUnitsPerPixel;
            adapter.zoomSensitivity = zoomSensitivity;

            adapterObject.SetActive(true);

            return new AdapterTestContext(
                controller,
                adapter,
                assignedKeyboardPanAction,
                assignedDragPanAction,
                assignedPointerDeltaAction,
                assignedZoomAction);
        }

        private TabletopCameraController CreateInitializedController()
        {
            UnityCamera targetCamera = CreateOrthographicCamera();
            Transform cameraRig = CreateGameObject("Camera Rig").transform;
            GameObject controllerObject = CreateGameObject("Tabletop Camera Controller");
            controllerObject.SetActive(false);

            TabletopCameraController controller = controllerObject.AddComponent<TabletopCameraController>();
            controller.targetCamera = targetCamera;
            controller.cameraRig = cameraRig;

            controllerObject.SetActive(true);

            return controller;
        }

        private UnityCamera CreateOrthographicCamera()
        {
            GameObject cameraObject = CreateGameObject("Target Camera");
            UnityCamera camera = cameraObject.AddComponent<UnityCamera>();
            camera.orthographic = true;
            return camera;
        }

        private InputActionMap CreateActionMap()
        {
            InputActionAsset inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            createdInputAssets.Add(inputActionAsset);

            return inputActionAsset.AddActionMap("TabletopCamera");
        }

        private InputActionReference CreateActionReference(
            InputActionMap actionMap,
            string actionName,
            InputActionType actionType,
            string expectedControlType)
        {
            InputAction action = actionMap.AddAction(actionName, actionType, expectedControlLayout: expectedControlType);
            InputActionReference actionReference = InputActionReference.Create(action);
            createdActionReferences.Add(actionReference);
            return actionReference;
        }

        private InputActionReference CreateReferenceWithoutAction()
        {
            InputActionReference actionReference = ScriptableObject.CreateInstance<InputActionReference>();
            createdActionReferences.Add(actionReference);
            return actionReference;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private static void AssertUnchanged(AdapterTestContext context)
        {
            AssertCoordinate(context.Controller.State.FocusCoordinate, 0.0, 0.0);
            Assert.That(context.Controller.State.OrthographicSize, Is.EqualTo(5f).Within(Tolerance));
            AssertVector3(context.Controller.CameraRig.position, 0f, 10f, 0f);
            Assert.That(context.Controller.TargetCamera.orthographicSize, Is.EqualTo(5f).Within(Tolerance));
        }

        private static void AssertCoordinate(TableCoordinate actual, double expectedX, double expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(Tolerance));
        }

        private static void AssertVector3(Vector3 actual, float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expectedZ).Within(Tolerance));
        }

        private sealed class AdapterTestContext
        {
            public AdapterTestContext(
                TabletopCameraController controller,
                TabletopCameraInputAdapter adapter,
                InputActionReference keyboardPanAction,
                InputActionReference dragPanAction,
                InputActionReference pointerDeltaAction,
                InputActionReference zoomAction)
            {
                Controller = controller;
                Adapter = adapter;
                KeyboardPanAction = keyboardPanAction;
                DragPanAction = dragPanAction;
                PointerDeltaAction = pointerDeltaAction;
                ZoomAction = zoomAction;
            }

            public TabletopCameraController Controller { get; }

            public TabletopCameraInputAdapter Adapter { get; }

            public InputActionReference KeyboardPanAction { get; }

            public InputActionReference DragPanAction { get; }

            public InputActionReference PointerDeltaAction { get; }

            public InputActionReference ZoomAction { get; }
        }
    }
}
