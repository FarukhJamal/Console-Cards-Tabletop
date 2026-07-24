using System;
using System.Collections.Generic;
using ConsoleCards.Presentation.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Input
{
    /// <summary>
    /// Adapts local tabletop object input actions into object-interaction coordinator calls.
    /// </summary>
    public sealed class TabletopObjectInputAdapter : MonoBehaviour
    {
        [SerializeField] internal InputActionReference pointAction;
        [SerializeField] internal InputActionReference selectAction;
        [SerializeField] internal InputActionReference cancelAction;
        [SerializeField] internal InputActionReference rotateAction;
        [SerializeField] internal InputActionReference flipAction;

        [SerializeField] internal float rotationStepDegrees = 15f;

        private readonly List<InputAction> actionsEnabledByAdapter = new List<InputAction>();
        private TabletopMoveInteractionCoordinator moveCoordinator;
        private TabletopRotationCoordinator rotationCoordinator;
        private TabletopCardFlipCoordinator flipCoordinator;
        private TabletopInteractionInputRoutingPolicy routingPolicy;
        private TabletopInputFrameCoordinator externalFrameDriver;

        public bool HasValidActionConfiguration { get; private set; }

        public bool IsInitialized { get; private set; }

        public TabletopMoveInteractionCoordinator Coordinator => moveCoordinator;

        public TabletopMoveInteractionCoordinator MoveCoordinator => moveCoordinator;

        public TabletopRotationCoordinator RotationCoordinator => rotationCoordinator;

        public TabletopCardFlipCoordinator FlipCoordinator => flipCoordinator;

        public TabletopInteractionInputRoutingPolicy RoutingPolicy => routingPolicy;

        public InputActionReference PointAction => pointAction;

        public InputActionReference SelectAction => selectAction;

        public InputActionReference CancelAction => cancelAction;

        public InputActionReference RotateAction => rotateAction;

        public InputActionReference FlipAction => flipAction;

        public float RotationStepDegrees => rotationStepDegrees;

        public MoveInteractionReleaseResult? LastReleaseResult { get; private set; }

        public RotationInteractionResult? LastRotationResult { get; private set; }

        public FlipInteractionResult? LastFlipResult { get; private set; }

        internal bool IsExternallyDriven => externalFrameDriver != null;

        internal bool IsExternallyDrivenBy(TabletopInputFrameCoordinator frameDriver)
        {
            return externalFrameDriver == frameDriver;
        }

        public void Initialize(
            TabletopMoveInteractionCoordinator moveCoordinator,
            TabletopRotationCoordinator rotationCoordinator,
            TabletopCardFlipCoordinator flipCoordinator,
            TabletopInteractionInputRoutingPolicy routingPolicy)
        {
            if (moveCoordinator == null)
            {
                throw new ArgumentNullException(nameof(moveCoordinator));
            }

            if (rotationCoordinator == null)
            {
                throw new ArgumentNullException(nameof(rotationCoordinator));
            }

            if (flipCoordinator == null)
            {
                throw new ArgumentNullException(nameof(flipCoordinator));
            }

            if (routingPolicy == null)
            {
                throw new ArgumentNullException(nameof(routingPolicy));
            }

            if (!HasValidActionConfiguration)
            {
                throw new InvalidOperationException("TabletopObjectInputAdapter cannot initialize before valid action configuration is available.");
            }

            if (IsInitialized)
            {
                throw new InvalidOperationException("TabletopObjectInputAdapter is already initialized.");
            }

            if (moveCoordinator.HasActiveInteraction)
            {
                throw new ArgumentException("Tabletop object input coordinator must not already have an active interaction.", nameof(moveCoordinator));
            }

            this.moveCoordinator = moveCoordinator;
            this.rotationCoordinator = rotationCoordinator;
            this.flipCoordinator = flipCoordinator;
            this.routingPolicy = routingPolicy;
            IsInitialized = true;
            LastReleaseResult = null;
            LastRotationResult = null;
            LastFlipResult = null;

            if (isActiveAndEnabled)
            {
                EnableAssignedActions();
            }
        }

        public void Shutdown()
        {
            if (IsInitialized && moveCoordinator != null && moveCoordinator.HasActiveInteraction)
            {
                moveCoordinator.Reset();
            }

            DisableActionsEnabledByAdapter();
            moveCoordinator = null;
            rotationCoordinator = null;
            flipCoordinator = null;
            routingPolicy = null;
            IsInitialized = false;
            LastReleaseResult = null;
            LastRotationResult = null;
            LastFlipResult = null;
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
                cancelAction.action.WasPressedThisFrame(),
                0f,
                false);
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
            out bool cancelPressedThisFrame,
            out float rotateDelta,
            out bool flipPressedThisFrame)
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
            rotateDelta = rotateAction.action.ReadValue<float>();
            flipPressedThisFrame = flipAction.action.WasPressedThisFrame();
        }

        internal MoveInteractionReleaseResult? ApplyInputFrame(
            Vector2 screenPosition,
            bool selectPressedThisFrame,
            bool selectHeld,
            bool selectReleasedThisFrame,
            bool cancelPressedThisFrame)
        {
            return ApplyInputFrame(
                screenPosition,
                selectPressedThisFrame,
                selectHeld,
                selectReleasedThisFrame,
                cancelPressedThisFrame,
                0f,
                false);
        }

        internal MoveInteractionReleaseResult? ApplyInputFrame(
            Vector2 screenPosition,
            bool selectPressedThisFrame,
            bool selectHeld,
            bool selectReleasedThisFrame,
            bool cancelPressedThisFrame,
            float rotateDelta,
            bool flipPressedThisFrame)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("TabletopObjectInputAdapter must be initialized before input frames are applied.");
            }

            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            ValidateFinite(rotateDelta, nameof(rotateDelta));

            if (cancelPressedThisFrame)
            {
                if (moveCoordinator.HasActiveInteraction)
                {
                    moveCoordinator.Cancel();
                }

                return null;
            }

            MoveInteractionReleaseResult? releaseResult = null;
            if (selectPressedThisFrame && !moveCoordinator.HasActiveInteraction)
            {
                moveCoordinator.TryBeginPress(screenPosition);
            }

            if (selectReleasedThisFrame && moveCoordinator.HasActiveInteraction)
            {
                MoveInteractionReleaseResult result = moveCoordinator.ReleasePointer(screenPosition);
                LastReleaseResult = result;
                releaseResult = result;
            }
            else if (selectHeld && moveCoordinator.HasActiveInteraction)
            {
                moveCoordinator.UpdatePointer(screenPosition);
            }

            if (selectPressedThisFrame || selectReleasedThisFrame)
            {
                return releaseResult;
            }

            if (moveCoordinator.HasActiveInteraction)
            {
                return releaseResult;
            }

            TabletopScrollInputRoute route = routingPolicy.ResolveScrollRoute();
            if (route == TabletopScrollInputRoute.Suppressed)
            {
                return releaseResult;
            }

            if (flipPressedThisFrame)
            {
                LastFlipResult = flipCoordinator.FlipSelected();
                return releaseResult;
            }

            if (rotateDelta != 0f)
            {
                switch (route)
                {
                    case TabletopScrollInputRoute.ObjectRotation:
                        LastRotationResult = rotationCoordinator.RotateSelected(
                            Math.Sign(rotateDelta) * rotationStepDegrees);
                        break;
                    case TabletopScrollInputRoute.CameraZoom:
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported scroll input route.");
                }
            }

            return releaseResult;
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

            if (!ValidateActionReference(rotateAction, "Rotate"))
            {
                return false;
            }

            if (!ValidateActionReference(flipAction, "Flip"))
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

            if (rotateAction.action.expectedControlType != "Axis")
            {
                LogConfigurationError("TabletopObjectInputAdapter requires the Rotate action expected control type to be Axis.");
                return false;
            }

            if (flipAction.action.type != InputActionType.Button)
            {
                LogConfigurationError("TabletopObjectInputAdapter requires the Flip action to be a Button.");
                return false;
            }

            if (!IsFinite(rotationStepDegrees) || rotationStepDegrees <= 0f)
            {
                LogConfigurationError("TabletopObjectInputAdapter requires finite rotationStepDegrees greater than zero.");
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
            EnableActionIfNeeded(rotateAction.action);
            EnableActionIfNeeded(flipAction.action);
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
            ValidateFinite(screenPosition.x, parameterName);
            ValidateFinite(screenPosition.y, parameterName);
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
