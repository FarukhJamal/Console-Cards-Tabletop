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

        private bool isInitialized;
        private bool isAttached;
        private bool hasObjectInputBlockingGuiRect;
        private bool suppressObjectPointerUntilRelease;
        private Rect objectInputBlockingGuiRect;
        private readonly List<RaycastResult> screenUiRaycastResults = new List<RaycastResult>();
        private EventSystem screenUiEventSystem;
        private PointerEventData screenUiPointerEventData;
        private Action<Vector2> secondaryPointerPressed;
        private TabletopSelectionPresenter selectionPresenter;
        private TabletopComponentPlacementController componentPlacementController;

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

        internal void ConfigurePrototypeUiInput(Action<Vector2> secondaryPointerPressedHandler)
        {
            if (secondaryPointerPressed != null)
            {
                throw new InvalidOperationException(
                    "TabletopInputFrameCoordinator already has prototype UI input handlers.");
            }

            secondaryPointerPressed = secondaryPointerPressedHandler
                ?? throw new ArgumentNullException(nameof(secondaryPointerPressedHandler));
        }

        internal void ClearPrototypeUiInput()
        {
            suppressObjectPointerUntilRelease = false;
            secondaryPointerPressed = null;
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
            DetachAdaptersIfNeeded();
        }

        private void OnDestroy()
        {
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
            bool placementCancelledBySecondary = secondaryPressedThisFrame
                && componentPlacementController != null
                && componentPlacementController.IsActive
                && !IsInsideObjectInputBlockingGuiRect(screenPosition);
            if (placementCancelledBySecondary)
            {
                componentPlacementController.Cancel();
                dragHeld = false;
                pointerDelta = Vector2.zero;
                scrollDelta = 0f;
            }
            else if (secondaryPressedThisFrame
                && secondaryPointerPressed != null
                && !HasActiveObjectInteraction()
                && !IsInsideObjectInputBlockingGuiRect(screenPosition))
            {
                secondaryPointerPressed(screenPosition);
            }

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
                flipPressedThisFrame));
        }

        internal MoveInteractionReleaseResult? ApplyInputFrame(TabletopInputFrame frame)
        {
            return ApplyInputFrame(frame, Time.unscaledDeltaTime);
        }

        internal MoveInteractionReleaseResult? ApplyInputFrame(TabletopInputFrame frame, float unscaledDeltaTime)
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
                    frame.CancelPressedThisFrame))
            {
                if (selectionPresenter != null)
                {
                    selectionPresenter.Refresh();
                }

                cameraInputAdapter.ApplyInputFrame(
                    frame.KeyboardPan,
                    false,
                    Vector2.zero,
                    0f,
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
                frame.KeyboardPan,
                frame.DragHeld && !pointerInsideBlockedUi,
                pointerInsideBlockedUi ? Vector2.zero : frame.PointerDelta,
                effectiveScroll,
                unscaledDeltaTime);

            return releaseResult;
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
