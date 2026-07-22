using System;
using ConsoleCards.Presentation.Camera;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Input
{
    /// <summary>
    /// Adapts local tabletop Camera input actions into Camera controller state changes.
    /// </summary>
    public sealed class TabletopCameraInputAdapter : MonoBehaviour
    {
        [SerializeField] internal TabletopCameraController cameraController;
        [SerializeField] internal InputActionReference keyboardPanAction;
        [SerializeField] internal InputActionReference dragPanAction;
        [SerializeField] internal InputActionReference pointerDeltaAction;
        [SerializeField] internal InputActionReference zoomAction;

        [SerializeField] internal float keyboardPanSpeed = 5f;
        [SerializeField] internal float dragPanUnitsPerPixel = 0.02f;
        [SerializeField] internal float zoomSensitivity = 0.01f;

        private bool keyboardPanEnabledByAdapter;
        private bool dragPanEnabledByAdapter;
        private bool pointerDeltaEnabledByAdapter;
        private bool zoomEnabledByAdapter;

        public bool IsInitialized { get; private set; }

        public TabletopCameraController CameraController => cameraController;

        public float KeyboardPanSpeed => keyboardPanSpeed;

        public float DragPanUnitsPerPixel => dragPanUnitsPerPixel;

        public float ZoomSensitivity => zoomSensitivity;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            IsInitialized = true;
        }

        private void OnEnable()
        {
            if (!IsInitialized)
            {
                return;
            }

            EnableActionIfNeeded(keyboardPanAction.action, ref keyboardPanEnabledByAdapter);
            EnableActionIfNeeded(dragPanAction.action, ref dragPanEnabledByAdapter);
            EnableActionIfNeeded(pointerDeltaAction.action, ref pointerDeltaEnabledByAdapter);
            EnableActionIfNeeded(zoomAction.action, ref zoomEnabledByAdapter);
        }

        private void OnDisable()
        {
            DisableActionIfOwned(keyboardPanAction, ref keyboardPanEnabledByAdapter);
            DisableActionIfOwned(dragPanAction, ref dragPanEnabledByAdapter);
            DisableActionIfOwned(pointerDeltaAction, ref pointerDeltaEnabledByAdapter);
            DisableActionIfOwned(zoomAction, ref zoomEnabledByAdapter);
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            ApplyInputFrame(
                keyboardPanAction.action.ReadValue<Vector2>(),
                dragPanAction.action.IsPressed(),
                pointerDeltaAction.action.ReadValue<Vector2>(),
                zoomAction.action.ReadValue<float>(),
                Time.unscaledDeltaTime);
        }

        internal void ApplyInputFrame(
            Vector2 keyboardPan,
            bool dragPanHeld,
            Vector2 pointerDelta,
            float zoomDelta,
            float unscaledDeltaTime)
        {
            ValidateFinite(keyboardPan.x, nameof(keyboardPan));
            ValidateFinite(keyboardPan.y, nameof(keyboardPan));
            ValidateFinite(pointerDelta.x, nameof(pointerDelta));
            ValidateFinite(pointerDelta.y, nameof(pointerDelta));
            ValidateFinite(zoomDelta, nameof(zoomDelta));

            if (!IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            Vector2 clampedKeyboardPan = Vector2.ClampMagnitude(keyboardPan, 1f);
            Vector2 keyboardDelta = clampedKeyboardPan * keyboardPanSpeed * unscaledDeltaTime;
            Vector2 dragDelta = dragPanHeld ? -pointerDelta * dragPanUnitsPerPixel : Vector2.zero;
            Vector2 combinedPan = keyboardDelta + dragDelta;

            if (combinedPan != Vector2.zero)
            {
                cameraController.Pan(combinedPan.x, combinedPan.y);
            }

            if (zoomDelta != 0f)
            {
                cameraController.Zoom(-zoomDelta * zoomSensitivity);
            }
        }

        private bool ValidateConfiguration()
        {
            if (cameraController == null)
            {
                LogConfigurationError("TabletopCameraInputAdapter requires a TabletopCameraController reference.");
                return false;
            }

            if (!ValidateActionReference(keyboardPanAction, "KeyboardPan"))
            {
                return false;
            }

            if (!ValidateActionReference(dragPanAction, "DragPan"))
            {
                return false;
            }

            if (!ValidateActionReference(pointerDeltaAction, "PointerDelta"))
            {
                return false;
            }

            if (!ValidateActionReference(zoomAction, "Zoom"))
            {
                return false;
            }

            if (!IsFinite(keyboardPanSpeed) || keyboardPanSpeed < 0f)
            {
                LogConfigurationError("TabletopCameraInputAdapter requires finite keyboardPanSpeed greater than or equal to zero.");
                return false;
            }

            if (!IsFinite(dragPanUnitsPerPixel) || dragPanUnitsPerPixel < 0f)
            {
                LogConfigurationError("TabletopCameraInputAdapter requires finite dragPanUnitsPerPixel greater than or equal to zero.");
                return false;
            }

            if (!IsFinite(zoomSensitivity) || zoomSensitivity < 0f)
            {
                LogConfigurationError("TabletopCameraInputAdapter requires finite zoomSensitivity greater than or equal to zero.");
                return false;
            }

            return true;
        }

        private bool ValidateActionReference(InputActionReference actionReference, string actionName)
        {
            if (actionReference == null)
            {
                LogConfigurationError($"TabletopCameraInputAdapter requires a {actionName} InputActionReference.");
                return false;
            }

            if (actionReference.action == null)
            {
                LogConfigurationError($"TabletopCameraInputAdapter requires the {actionName} InputActionReference to resolve to an InputAction.");
                return false;
            }

            return true;
        }

        private static void EnableActionIfNeeded(InputAction action, ref bool enabledByAdapter)
        {
            if (!action.enabled)
            {
                action.Enable();
                enabledByAdapter = true;
            }
        }

        private static void DisableActionIfOwned(InputActionReference actionReference, ref bool enabledByAdapter)
        {
            if (!enabledByAdapter)
            {
                return;
            }

            if (actionReference != null && actionReference.action != null)
            {
                actionReference.action.Disable();
            }

            enabledByAdapter = false;
        }

        private void LogConfigurationError(string message)
        {
            Debug.LogError(message, this);
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
