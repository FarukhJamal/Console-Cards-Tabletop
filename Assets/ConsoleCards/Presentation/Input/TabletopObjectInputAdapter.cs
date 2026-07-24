using System;
using System.Collections.Generic;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Input
{
    /// <summary>
    /// Adapts local tabletop object input actions into move-interaction coordinator calls.
    /// </summary>
    public sealed class TabletopObjectInputAdapter : MonoBehaviour
    {
        [SerializeField] internal InputActionReference pointAction;
        [SerializeField] internal InputActionReference selectAction;
        [SerializeField] internal InputActionReference cancelAction;

        private readonly List<InputAction> actionsEnabledByAdapter = new List<InputAction>();
        private TabletopMoveInteractionCoordinator coordinator;
        private TabletopInputFrameCoordinator externalFrameDriver;

        public bool HasValidActionConfiguration { get; private set; }

        public bool IsInitialized { get; private set; }

        public TabletopMoveInteractionCoordinator Coordinator => coordinator;

        public InputActionReference PointAction => pointAction;

        public InputActionReference SelectAction => selectAction;

        public InputActionReference CancelAction => cancelAction;

        public MoveInteractionReleaseResult? LastReleaseResult { get; private set; }

        internal bool IsExternallyDriven => externalFrameDriver != null;

        internal bool IsExternallyDrivenBy(TabletopInputFrameCoordinator frameDriver)
        {
            return externalFrameDriver == frameDriver;
        }

        public void Initialize(TabletopMoveInteractionCoordinator coordinator)
        {
            if (coordinator == null)
            {
                throw new ArgumentNullException(nameof(coordinator));
            }

            if (!HasValidActionConfiguration)
            {
                throw new InvalidOperationException("TabletopObjectInputAdapter cannot initialize before valid action configuration is available.");
            }

            if (IsInitialized)
            {
                throw new InvalidOperationException("TabletopObjectInputAdapter is already initialized.");
            }

            if (coordinator.HasActiveInteraction)
            {
                throw new ArgumentException("Tabletop object input coordinator must not already have an active interaction.", nameof(coordinator));
            }

            this.coordinator = coordinator;
            IsInitialized = true;
            LastReleaseResult = null;

            if (isActiveAndEnabled)
            {
                EnableAssignedActions();
            }
        }

        public void Shutdown()
        {
            if (IsInitialized && coordinator != null && coordinator.HasActiveInteraction)
            {
                coordinator.Reset();
            }

            DisableActionsEnabledByAdapter();
            coordinator = null;
            IsInitialized = false;
            LastReleaseResult = null;
        }

        private void Awake()
        {
            if (!ValidateActionConfiguration())
            {
                HasValidActionConfiguration = false;
                IsInitialized = false;
                enabled = false;
                return;
            }

            HasValidActionConfiguration = true;
        }

        private void OnEnable()
        {
            if (!IsInitialized)
            {
                return;
            }

            EnableAssignedActions();
        }

        private void OnDisable()
        {
            DisableActionsEnabledByAdapter();
        }

        private void OnDestroy()
        {
            if (IsInitialized)
            {
                Shutdown();
            }
        }

        private void Update()
        {
            if (!IsInitialized || IsExternallyDriven)
            {
                return;
            }

            ApplyInputFrame(
                pointAction.action.ReadValue<Vector2>(),
                selectAction.action.WasPressedThisFrame(),
                selectAction.action.IsPressed(),
                selectAction.action.WasReleasedThisFrame(),
                cancelAction.action.WasPressedThisFrame());
        }

        internal void AttachExternalFrameDriver(TabletopInputFrameCoordinator frameDriver)
        {
            if (frameDriver == null)
            {
                throw new ArgumentNullException(nameof(frameDriver));
            }

            if (externalFrameDriver != null)
            {
                throw new InvalidOperationException("TabletopObjectInputAdapter already has an external frame driver.");
            }

            externalFrameDriver = frameDriver;
        }

        internal void DetachExternalFrameDriver(TabletopInputFrameCoordinator frameDriver)
        {
            if (frameDriver == null)
            {
                throw new ArgumentNullException(nameof(frameDriver));
            }

            if (externalFrameDriver != frameDriver)
            {
                throw new InvalidOperationException("TabletopObjectInputAdapter cannot detach a different external frame driver.");
            }

            externalFrameDriver = null;
        }

        internal void ReadObjectInputValues(
            out Vector2 screenPosition,
            out bool selectPressedThisFrame,
            out bool selectHeld,
            out bool selectReleasedThisFrame,
            out bool cancelPressedThisFrame)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("TabletopObjectInputAdapter must be initialized before input values are read.");
            }

            screenPosition = pointAction.action.ReadValue<Vector2>();
            selectPressedThisFrame = selectAction.action.WasPressedThisFrame();
            selectHeld = selectAction.action.IsPressed();
            selectReleasedThisFrame = selectAction.action.WasReleasedThisFrame();
            cancelPressedThisFrame = cancelAction.action.WasPressedThisFrame();
        }

        internal MoveInteractionReleaseResult? ApplyInputFrame(
            Vector2 screenPosition,
            bool selectPressedThisFrame,
            bool selectHeld,
            bool selectReleasedThisFrame,
            bool cancelPressedThisFrame)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("TabletopObjectInputAdapter must be initialized before input frames are applied.");
            }

            ValidateScreenPosition(screenPosition, nameof(screenPosition));

            if (cancelPressedThisFrame)
            {
                if (coordinator.HasActiveInteraction)
                {
                    coordinator.Cancel();
                }

                LastReleaseResult = null;
                return null;
            }

            if (selectPressedThisFrame && !coordinator.HasActiveInteraction)
            {
                coordinator.TryBeginPress(screenPosition);
            }

            if (selectReleasedThisFrame && coordinator.HasActiveInteraction)
            {
                MoveInteractionReleaseResult result = coordinator.ReleasePointer(screenPosition);
                LastReleaseResult = result;
                return result;
            }

            if (selectHeld && coordinator.HasActiveInteraction)
            {
                coordinator.UpdatePointer(screenPosition);
            }

            return null;
        }

        private bool ValidateActionConfiguration()
        {
            if (!ValidateActionReference(pointAction, "Point"))
            {
                return false;
            }

            if (!ValidateActionReference(selectAction, "Select"))
            {
                return false;
            }

            if (!ValidateActionReference(cancelAction, "Cancel"))
            {
                return false;
            }

            if (pointAction.action.expectedControlType != "Vector2")
            {
                LogConfigurationError("TabletopObjectInputAdapter requires the Point action expected control type to be Vector2.");
                return false;
            }

            if (selectAction.action.type != InputActionType.Button)
            {
                LogConfigurationError("TabletopObjectInputAdapter requires the Select action to be a Button.");
                return false;
            }

            if (cancelAction.action.type != InputActionType.Button)
            {
                LogConfigurationError("TabletopObjectInputAdapter requires the Cancel action to be a Button.");
                return false;
            }

            return true;
        }

        private bool ValidateActionReference(InputActionReference actionReference, string actionName)
        {
            if (actionReference == null)
            {
                LogConfigurationError($"TabletopObjectInputAdapter requires a {actionName} InputActionReference.");
                return false;
            }

            if (actionReference.action == null)
            {
                LogConfigurationError($"TabletopObjectInputAdapter requires the {actionName} InputActionReference to resolve to an InputAction.");
                return false;
            }

            return true;
        }

        private void EnableAssignedActions()
        {
            EnableActionIfNeeded(pointAction.action);
            EnableActionIfNeeded(selectAction.action);
            EnableActionIfNeeded(cancelAction.action);
        }

        private void EnableActionIfNeeded(InputAction action)
        {
            if (actionsEnabledByAdapter.Contains(action))
            {
                return;
            }

            if (action.enabled)
            {
                return;
            }

            action.Enable();
            actionsEnabledByAdapter.Add(action);
        }

        private void DisableActionsEnabledByAdapter()
        {
            for (int i = 0; i < actionsEnabledByAdapter.Count; i++)
            {
                InputAction action = actionsEnabledByAdapter[i];
                if (action != null && action.enabled)
                {
                    action.Disable();
                }
            }

            actionsEnabledByAdapter.Clear();
        }

        private void LogConfigurationError(string message)
        {
            Debug.LogError(message, this);
        }

        private static void ValidateScreenPosition(Vector2 screenPosition, string parameterName)
        {
            if (!IsFinite(screenPosition.x) || !IsFinite(screenPosition.y))
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
