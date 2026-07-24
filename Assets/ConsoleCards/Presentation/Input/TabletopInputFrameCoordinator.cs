using System;
using UnityEngine;

namespace ConsoleCards.Presentation.Input
{
    public sealed class TabletopInputFrameCoordinator : MonoBehaviour
    {
        [SerializeField] internal TabletopCameraInputAdapter cameraInputAdapter;
        [SerializeField] internal TabletopObjectInputAdapter objectInputAdapter;

        private bool isInitialized;
        private bool isAttached;

        public bool IsInitialized => isInitialized;

        public TabletopCameraInputAdapter CameraInputAdapter => cameraInputAdapter;

        public TabletopObjectInputAdapter ObjectInputAdapter => objectInputAdapter;

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
                out bool cancelPressedThisFrame);

            ApplyInputFrame(new TabletopInputFrame(
                keyboardPan,
                dragHeld,
                pointerDelta,
                scrollDelta,
                screenPosition,
                selectPressedThisFrame,
                selectHeld,
                selectReleasedThisFrame,
                cancelPressedThisFrame));
        }

        internal void ApplyInputFrame(TabletopInputFrame frame)
        {
            ApplyInputFrame(frame, Time.unscaledDeltaTime);
        }

        internal void ApplyInputFrame(TabletopInputFrame frame, float unscaledDeltaTime)
        {
            if (!IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            bool suppressScrollForPointerTransition = frame.HasPointerTransition;

            objectInputAdapter.ApplyInputFrame(
                frame.ScreenPosition,
                frame.SelectPressedThisFrame,
                frame.SelectHeld,
                frame.SelectReleasedThisFrame,
                frame.CancelPressedThisFrame);

            float effectiveScroll = suppressScrollForPointerTransition ? 0f : frame.ScrollDelta;
            cameraInputAdapter.ApplyInputFrame(
                frame.KeyboardPan,
                frame.DragHeld,
                frame.PointerDelta,
                effectiveScroll,
                unscaledDeltaTime);
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
