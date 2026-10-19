using System;
using System.Collections.Generic;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Input
{
    public sealed class TabletopInputFrameCoordinator : MonoBehaviour
    {
        [SerializeField] internal TabletopCameraInputAdapter cameraInputAdapter;
        [SerializeField] internal TabletopObjectInputAdapter objectInputAdapter;
        [SerializeField] internal float cameraOrbitDragThresholdPixels = 8f;

        private bool isInitialized;
        private bool isAttached;
        private bool hasObjectInputBlockingGuiRect;
        private bool suppressObjectPointerUntilRelease;
        private Rect objectInputBlockingGuiRect;
        private readonly List<RaycastResult> screenUiRaycastResults = new List<RaycastResult>();
        private EventSystem screenUiEventSystem;
        private PointerEventData screenUiPointerEventData;
        private Action<Vector2> secondaryPointerPressed;
        private Func<Vector2, bool> cameraOrbitEligibility;
        private bool secondaryPointerPending;
        private bool secondaryOrbitEligible;
        private bool secondaryOrbitDragging;
        private Vector2 secondaryPressScreenPosition;
        private TabletopSelectionPresenter selectionPresenter;
        private TabletopComponentPlacementController componentPlacementController;

        // UX-1, the table ping: Alt + left click, or a quick middle-button tap, anywhere on the table.
        private float middleTapSeconds = 0.3f;
        private TableKeys tableKeys;
        private Func<Vector2, bool> tablePingRequested;
        private bool middleTapPending;
        private Vector2 middleTapPressPosition;
        private float middleTapPressedAt;

        public bool IsInitialized => isInitialized;

        public TabletopCameraInputAdapter CameraInputAdapter => cameraInputAdapter;

        public TabletopObjectInputAdapter ObjectInputAdapter => objectInputAdapter;

        public bool HasSelectionPresenter => selectionPresenter != null;

        public TabletopSelectionPresenter SelectionPresenter => selectionPresenter;

        internal void ConfigureObjectInputBlockingGuiRect(Rect guiRect)
        {
            if (hasObjectInputBlockingGuiRect)
            {
                throw new InvalidOperationException(
                    "TabletopInputFrameCoordinator already has an object-input blocking GUI Rect.");
            }

            objectInputBlockingGuiRect = guiRect;
            hasObjectInputBlockingGuiRect = true;
            suppressObjectPointerUntilRelease = false;
        }

        internal void ClearObjectInputBlockingGuiRect()
        {
            objectInputBlockingGuiRect = default;
            hasObjectInputBlockingGuiRect = false;
            suppressObjectPointerUntilRelease = false;
        }

        internal void ConfigureUiInput(Action<Vector2> secondaryPointerPressedHandler)
        {
            ConfigureUiInput(secondaryPointerPressedHandler, null);
        }

        internal void ConfigureUiInput(
            Action<Vector2> secondaryPointerPressedHandler,
            Func<Vector2, bool> cameraOrbitEligibilityHandler)
        {
            if (secondaryPointerPressed != null)
            {
                throw new InvalidOperationException(
                    "TabletopInputFrameCoordinator already has prototype UI input handlers.");
            }

            secondaryPointerPressed = secondaryPointerPressedHandler
                ?? throw new ArgumentNullException(nameof(secondaryPointerPressedHandler));
            cameraOrbitEligibility = cameraOrbitEligibilityHandler;
        }

        /// <summary>
        /// UX-1: Alt + left click or a quick middle-button tap on the table asks the handler for a ping at that
        /// screen point. When the handler pings (returns true) after an Alt-click, the click does nothing else.
        /// </summary>
        internal void ConfigureTablePing(Func<Vector2, bool> pingHandler, TableKeys keys, float tapSeconds)
        {
            tablePingRequested = pingHandler;
            tableKeys = keys;
            middleTapSeconds = Mathf.Max(0.05f, tapSeconds);
            middleTapPending = false;
        }

        /// <summary>
        /// POL-0 diagnostics: when true, a click on the table that a menu or panel took is written to the Console
        /// (and Editor.log) with the name of what took it, so clicks that need a second try can be traced.
        /// </summary>
        internal bool LogMissedPresses { get; set; }

        internal void ClearUiInput()
        {
            tablePingRequested = null;
            middleTapPending = false;
            suppressObjectPointerUntilRelease = false;
            secondaryPointerPressed = null;
            cameraOrbitEligibility = null;
            ClearSecondaryPointerGesture();
        }

        internal void ConfigureComponentPlacement(
            TabletopComponentPlacementController placementController)
        {
            if (placementController == null)
            {
                throw new ArgumentNullException(nameof(placementController));
            }

            if (componentPlacementController != null)
            {
                throw new InvalidOperationException(
                    "TabletopInputFrameCoordinator already has a component placement controller.");
            }

            componentPlacementController = placementController;
        }

        internal void ClearComponentPlacement()
        {
            componentPlacementController?.Cancel();
            componentPlacementController = null;
        }

        public void ConfigureSelectionPresenter(TabletopSelectionPresenter presenter)
        {
            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }

            if (selectionPresenter != null)
            {
                throw new InvalidOperationException("TabletopInputFrameCoordinator already has a selection presenter.");
            }

            selectionPresenter = presenter;
        }

        public void ClearSelectionPresenter()
        {
            if (selectionPresenter != null)
            {
                selectionPresenter.Clear();
                selectionPresenter = null;
            }
        }

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            isInitialized = true;
        }

        private void OnEnable()
        {
            if (!isInitialized)
            {
                return;
            }

            AttachAdapters();
        }

        private void OnDisable()
        {
            ClearSecondaryPointerGesture();
            DetachAdaptersIfNeeded();
        }

        private void OnDestroy()
        {
            ClearSecondaryPointerGesture();
            DetachAdaptersIfNeeded();
        }

        private void Update()
        {
            if (!isInitialized || !isAttached)
            {
                return;
            }

            cameraInputAdapter.ReadCameraInputValues(
                out Vector2 keyboardPan,
                out bool dragHeld,
                out Vector2 pointerDelta,
                out float scrollDelta);
            objectInputAdapter.ReadObjectInputValues(
                out Vector2 screenPosition,
                out bool selectPressedThisFrame,
                out bool selectHeld,
                out bool selectReleasedThisFrame,
                out bool cancelPressedThisFrame,
                out float rotateDelta,
                out bool flipPressedThisFrame);

            bool secondaryPressedThisFrame = Mouse.current != null
                && Mouse.current.rightButton.wasPressedThisFrame;
            bool secondaryHeld = Mouse.current != null
                && Mouse.current.rightButton.isPressed;
            bool secondaryReleasedThisFrame = Mouse.current != null
                && Mouse.current.rightButton.wasReleasedThisFrame;
            bool pointerInsideBlockedUi = IsInsideObjectInputBlockingGuiRect(screenPosition);
            bool placementCancelledBySecondary = secondaryPressedThisFrame
                && componentPlacementController != null
                && componentPlacementController.IsActive
                && !pointerInsideBlockedUi;
            if (placementCancelledBySecondary)
            {
                componentPlacementController.Cancel();
                ClearSecondaryPointerGesture();
                dragHeld = false;
                pointerDelta = Vector2.zero;
                scrollDelta = 0f;
            }
            UpdateTablePing(screenPosition, selectPressedThisFrame, pointerInsideBlockedUi);
            bool orbitHeld = !placementCancelledBySecondary
                && UpdateSecondaryPointerGesture(
                    screenPosition,
                    secondaryPressedThisFrame,
                    secondaryHeld,
                    secondaryReleasedThisFrame,
                    pointerInsideBlockedUi);

            ApplyInputFrame(new TabletopInputFrame(
                keyboardPan,
                dragHeld,
                pointerDelta,
                scrollDelta,
                screenPosition,
                selectPressedThisFrame,
                selectHeld,
                selectReleasedThisFrame,
                cancelPressedThisFrame,
                rotateDelta,
                flipPressedThisFrame),
                Time.unscaledDeltaTime,
                orbitHeld);
        }

        internal MoveInteractionReleaseResult? ApplyInputFrame(TabletopInputFrame frame)
        {
            return ApplyInputFrame(frame, Time.unscaledDeltaTime);
        }

        internal MoveInteractionReleaseResult? ApplyInputFrame(TabletopInputFrame frame, float unscaledDeltaTime)
        {
            return ApplyInputFrame(frame, unscaledDeltaTime, false);
        }

        private MoveInteractionReleaseResult? ApplyInputFrame(
            TabletopInputFrame frame,
            float unscaledDeltaTime,
            bool orbitHeld)
        {
            if (!IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            bool pointerInsideBlockedUi = IsInsideObjectInputBlockingGuiRect(frame.ScreenPosition);
            if (componentPlacementController != null
                && componentPlacementController.HandlePointerFrame(
                    frame.ScreenPosition,
                    pointerInsideBlockedUi,
                    frame.SelectPressedThisFrame,
                    frame.CancelPressedThisFrame,
                    frame.RotateDelta,
                    objectInputAdapter.RotationStepDegrees))
            {
                objectInputAdapter.UpdatePointerHover(frame.ScreenPosition, false);
                if (selectionPresenter != null)
                {
                    selectionPresenter.Refresh();
                }

                cameraInputAdapter.ApplyInputFrame(
                    pointerInsideBlockedUi ? Vector2.zero : frame.KeyboardPan,
                    false,
                    false,
                    Vector2.zero,
                    0f,
                    frame.ScreenPosition,
                    unscaledDeltaTime);
                return null;
            }

            bool suppressScrollForPointerTransition = frame.HasPointerTransition;
            float effectiveRotateDelta = frame.HasPointerTransition || pointerInsideBlockedUi
                ? 0f
                : frame.RotateDelta;
            bool effectiveFlipPressedThisFrame = frame.HasPointerTransition || pointerInsideBlockedUi
                ? false
                : frame.FlipPressedThisFrame;

            if (frame.SelectPressedThisFrame
                && !HasActiveObjectInteraction()
                && pointerInsideBlockedUi)
            {
                suppressObjectPointerUntilRelease = true;
                LogPressTakenByUi(frame.ScreenPosition);
            }

            bool suppressObjectPointer = suppressObjectPointerUntilRelease;
            bool effectiveSelectPressedThisFrame = suppressObjectPointer
                ? false
                : frame.SelectPressedThisFrame;
            bool effectiveSelectHeld = suppressObjectPointer
                ? false
                : frame.SelectHeld;
            bool effectiveSelectReleasedThisFrame = suppressObjectPointer
                ? false
                : frame.SelectReleasedThisFrame;

            if (suppressObjectPointer && frame.SelectReleasedThisFrame)
            {
                suppressObjectPointerUntilRelease = false;
            }

            objectInputAdapter.UpdatePointerHover(
                frame.ScreenPosition,
                !pointerInsideBlockedUi && !suppressObjectPointer);

            MoveInteractionReleaseResult? releaseResult = objectInputAdapter.ApplyInputFrame(
                frame.ScreenPosition,
                effectiveSelectPressedThisFrame,
                effectiveSelectHeld,
                effectiveSelectReleasedThisFrame,
                frame.CancelPressedThisFrame,
                effectiveRotateDelta,
                effectiveFlipPressedThisFrame);

            if (selectionPresenter != null)
            {
                selectionPresenter.Refresh();
            }

            float effectiveScroll = suppressScrollForPointerTransition || pointerInsideBlockedUi
                ? 0f
                : frame.ScrollDelta;
            cameraInputAdapter.ApplyInputFrame(
                pointerInsideBlockedUi ? Vector2.zero : frame.KeyboardPan,
                frame.DragHeld && !pointerInsideBlockedUi && !orbitHeld,
                orbitHeld && !pointerInsideBlockedUi,
                pointerInsideBlockedUi ? Vector2.zero : frame.PointerDelta,
                effectiveScroll,
                frame.ScreenPosition,
                unscaledDeltaTime);

            return releaseResult;
        }

        private bool UpdateSecondaryPointerGesture(
            Vector2 screenPosition,
            bool pressedThisFrame,
            bool held,
            bool releasedThisFrame,
            bool pointerInsideBlockedUi)
        {
            if (pressedThisFrame)
            {
                ClearSecondaryPointerGesture();
                if (!pointerInsideBlockedUi && !HasActiveObjectInteraction())
                {
                    secondaryPointerPending = true;
                    secondaryPressScreenPosition = screenPosition;
                    secondaryOrbitEligible = cameraOrbitEligibility != null
                        && cameraOrbitEligibility(screenPosition);
                }
            }

            if (secondaryPointerPending
                && held
                && secondaryOrbitEligible
                && !secondaryOrbitDragging
                && Vector2.Distance(secondaryPressScreenPosition, screenPosition)
                    >= cameraOrbitDragThresholdPixels)
            {
                secondaryOrbitDragging = true;
            }

            bool orbitActive = secondaryPointerPending
                && secondaryOrbitDragging
                && held
                && !pointerInsideBlockedUi;

            if (releasedThisFrame)
            {
                if (secondaryPointerPending
                    && !secondaryOrbitDragging
                    && !pointerInsideBlockedUi)
                {
                    secondaryPointerPressed?.Invoke(secondaryPressScreenPosition);
                }

                ClearSecondaryPointerGesture();
            }

            return orbitActive;
        }

        private void UpdateTablePing(Vector2 screenPosition, bool selectPressedThisFrame, bool pointerInsideBlockedUi)
        {
            if (tablePingRequested == null)
            {
                return;
            }

            bool free = !pointerInsideBlockedUi
                && !HasActiveObjectInteraction()
                && (componentPlacementController == null || !componentPlacementController.IsActive);
            bool pingModifierHeld = tableKeys != null
                ? tableKeys.PingModifierHeld
                : Keyboard.current != null && Keyboard.current.altKey.isPressed;
            if (selectPressedThisFrame
                && free
                && pingModifierHeld
                && tablePingRequested(screenPosition))
            {
                suppressObjectPointerUntilRelease = true; // the click was the ping, not a pick-up
            }

            bool tapPressed = tableKeys != null
                ? tableKeys.PingTapPressedThisFrame
                : Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
            bool tapReleased = tableKeys != null
                ? tableKeys.PingTapReleasedThisFrame
                : Mouse.current != null && Mouse.current.middleButton.wasReleasedThisFrame;
            if (tapPressed)
            {
                middleTapPending = free;
                middleTapPressPosition = screenPosition;
                middleTapPressedAt = Time.unscaledTime;
            }

            if (middleTapPending && tapReleased)
            {
                middleTapPending = false;
                if (Time.unscaledTime - middleTapPressedAt <= middleTapSeconds
                    && Vector2.Distance(middleTapPressPosition, screenPosition) <= cameraOrbitDragThresholdPixels
                    && !pointerInsideBlockedUi)
                {
                    tablePingRequested(screenPosition);
                }
            }
        }

        private void ClearSecondaryPointerGesture()
        {
            secondaryPointerPending = false;
            secondaryOrbitEligible = false;
            secondaryOrbitDragging = false;
            secondaryPressScreenPosition = Vector2.zero;
        }

        private bool HasActiveObjectInteraction()
        {
            if (objectInputAdapter.HasInteractionRouter)
            {
                return objectInputAdapter.InteractionRouter.HasActiveInteraction;
            }

            return objectInputAdapter.MoveCoordinator != null
                && objectInputAdapter.MoveCoordinator.HasActiveInteraction;
        }

        private bool IsInsideObjectInputBlockingGuiRect(Vector2 screenPosition)
        {
            Vector2 guiPosition = ToGuiPosition(screenPosition);
            return IsPointerOverScreenUi(screenPosition)
                || (hasObjectInputBlockingGuiRect && objectInputBlockingGuiRect.Contains(guiPosition));
        }

        private bool IsPointerOverScreenUi(Vector2 screenPosition)
        {
            EventSystem currentEventSystem = EventSystem.current;
            if (currentEventSystem == null || !currentEventSystem.isActiveAndEnabled)
            {
                return false;
            }

            if (!ReferenceEquals(screenUiEventSystem, currentEventSystem)
                || screenUiPointerEventData == null)
            {
                screenUiEventSystem = currentEventSystem;
                screenUiPointerEventData = new PointerEventData(currentEventSystem);
            }

            screenUiPointerEventData.Reset();
            screenUiPointerEventData.position = screenPosition;
            screenUiRaycastResults.Clear();
            currentEventSystem.RaycastAll(screenUiPointerEventData, screenUiRaycastResults);
            return screenUiRaycastResults.Count > 0;
        }

        // POL-0 diagnostics: names what took a click meant for the table (only when a click is taken, never per frame).
        private void LogPressTakenByUi(Vector2 screenPosition)
        {
            if (!LogMissedPresses)
            {
                return;
            }

            string taker = "the developer controls area";
            if (IsPointerOverScreenUi(screenPosition) && screenUiRaycastResults.Count > 0)
            {
                GameObject top = screenUiRaycastResults[0].gameObject;
                Transform parent = top != null ? top.transform.parent : null;
                taker = top == null
                    ? "a menu"
                    : parent != null ? parent.name + "/" + top.name : top.name;
            }

            Debug.Log($"[ConsoleCards Input] A click at {screenPosition} went to the UI ({taker}), not the table.");
        }

        private static Vector2 ToGuiPosition(Vector2 screenPosition)
        {
            return new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        }

        private void AttachAdapters()
        {
            try
            {
                cameraInputAdapter.AttachExternalFrameDriver(this);
                objectInputAdapter.AttachExternalFrameDriver(this);
                isAttached = true;
            }
            catch (Exception exception)
            {
                DetachAdapterIfAttached(cameraInputAdapter);
                DetachAdapterIfAttached(objectInputAdapter);
                isAttached = false;
                LogConfigurationError(exception.Message);
                enabled = false;
            }
        }

        private void DetachAdaptersIfNeeded()
        {
            if (!isAttached)
            {
                return;
            }

            if (cameraInputAdapter != null)
            {
                cameraInputAdapter.DetachExternalFrameDriver(this);
            }

            if (objectInputAdapter != null)
            {
                objectInputAdapter.DetachExternalFrameDriver(this);
            }

            isAttached = false;
        }

        private void DetachAdapterIfAttached(TabletopCameraInputAdapter adapter)
        {
            if (adapter != null && adapter.IsExternallyDrivenBy(this))
            {
                adapter.DetachExternalFrameDriver(this);
            }
        }

        private void DetachAdapterIfAttached(TabletopObjectInputAdapter adapter)
        {
            if (adapter != null && adapter.IsExternallyDrivenBy(this))
            {
                adapter.DetachExternalFrameDriver(this);
            }
        }

        private bool ValidateConfiguration()
        {
            if (cameraInputAdapter == null)
            {
                LogConfigurationError("TabletopInputFrameCoordinator requires a TabletopCameraInputAdapter reference.");
                return false;
            }

            if (objectInputAdapter == null)
            {
                LogConfigurationError("TabletopInputFrameCoordinator requires a TabletopObjectInputAdapter reference.");
                return false;
            }

            if (ReferenceEquals((Component)cameraInputAdapter, (Component)objectInputAdapter))
            {
                LogConfigurationError("TabletopInputFrameCoordinator requires different adapter components.");
                return false;
            }

            if (!IsFinite(cameraOrbitDragThresholdPixels)
                || cameraOrbitDragThresholdPixels < 0f)
            {
                LogConfigurationError(
                    "TabletopInputFrameCoordinator requires a finite non-negative Camera orbit drag threshold.");
                return false;
            }

            return true;
        }

        private void LogConfigurationError(string message)
        {
            Debug.LogError(message, this);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
