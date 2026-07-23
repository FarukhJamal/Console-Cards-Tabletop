using System;
using ConsoleCards.Core.Identifiers;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class TabletopInteractionStateMachine
    {
        public TabletopInteractionStateMachine(float dragThresholdPixels)
        {
            if (!IsFinite(dragThresholdPixels) || dragThresholdPixels < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(dragThresholdPixels));
            }

            DragThresholdPixels = dragThresholdPixels;
            Reset();
        }

        public TabletopInteractionPhase Phase { get; private set; }

        public float DragThresholdPixels { get; }

        public TabletopObjectId ActiveObjectId { get; private set; }

        public bool HasActiveObject => !ActiveObjectId.IsEmpty;

        public bool HasPointerCapture { get; private set; }

        public Vector2 PressScreenPosition { get; private set; }

        public Vector2 CurrentScreenPosition { get; private set; }

        public void SetHoveredObject(TabletopObjectId objectId)
        {
            ValidateObjectId(objectId, nameof(objectId));
            EnsurePhaseAllows(nameof(SetHoveredObject), TabletopInteractionPhase.Idle, TabletopInteractionPhase.Hovering);

            Phase = TabletopInteractionPhase.Hovering;
            ActiveObjectId = objectId;
            HasPointerCapture = false;
        }

        public void ClearHoveredObject()
        {
            EnsurePhaseAllows(nameof(ClearHoveredObject), TabletopInteractionPhase.Idle, TabletopInteractionPhase.Hovering);
            Reset();
        }

        public void BeginPress(TabletopObjectId objectId, Vector2 screenPosition)
        {
            ValidateObjectId(objectId, nameof(objectId));
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            EnsurePhaseAllows(nameof(BeginPress), TabletopInteractionPhase.Idle, TabletopInteractionPhase.Hovering);

            Phase = TabletopInteractionPhase.Pressed;
            ActiveObjectId = objectId;
            PressScreenPosition = screenPosition;
            CurrentScreenPosition = screenPosition;
            HasPointerCapture = true;
        }

        public bool UpdatePointer(Vector2 screenPosition)
        {
            ValidateScreenPosition(screenPosition, nameof(screenPosition));
            EnsurePhaseAllows(nameof(UpdatePointer), TabletopInteractionPhase.Pressed, TabletopInteractionPhase.DraggingObject);

            if (Phase == TabletopInteractionPhase.DraggingObject)
            {
                CurrentScreenPosition = screenPosition;
                return false;
            }

            float distance = Vector2.Distance(PressScreenPosition, screenPosition);
            if (!IsFinite(distance))
            {
                throw new OverflowException("Pointer movement distance became non-finite.");
            }

            bool startsDragging = distance >= DragThresholdPixels;

            CurrentScreenPosition = screenPosition;
            if (startsDragging)
            {
                Phase = TabletopInteractionPhase.DraggingObject;
                return true;
            }

            return false;
        }

        public TabletopInteractionPhase ReleasePointer()
        {
            EnsurePhaseAllows(nameof(ReleasePointer), TabletopInteractionPhase.Pressed, TabletopInteractionPhase.DraggingObject);

            if (Phase == TabletopInteractionPhase.Pressed)
            {
                Reset();
                return TabletopInteractionPhase.Idle;
            }

            Phase = TabletopInteractionPhase.AwaitingAcceptance;
            HasPointerCapture = false;
            return TabletopInteractionPhase.AwaitingAcceptance;
        }

        public void CompleteAcceptance()
        {
            EnsurePhaseAllows(nameof(CompleteAcceptance), TabletopInteractionPhase.AwaitingAcceptance);
            Reset();
        }

        public void BeginCancellation()
        {
            EnsurePhaseAllows(
                nameof(BeginCancellation),
                TabletopInteractionPhase.Pressed,
                TabletopInteractionPhase.DraggingObject,
                TabletopInteractionPhase.AwaitingAcceptance);

            Phase = TabletopInteractionPhase.Cancelling;
            HasPointerCapture = false;
        }

        public void CompleteCancellation()
        {
            EnsurePhaseAllows(nameof(CompleteCancellation), TabletopInteractionPhase.Cancelling);
            Reset();
        }

        public void Reset()
        {
            Phase = TabletopInteractionPhase.Idle;
            ActiveObjectId = TabletopObjectId.Empty;
            HasPointerCapture = false;
            PressScreenPosition = Vector2.zero;
            CurrentScreenPosition = Vector2.zero;
        }

        private static void ValidateObjectId(TabletopObjectId objectId, string parameterName)
        {
            if (objectId.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", parameterName);
            }
        }

        private static void ValidateScreenPosition(Vector2 screenPosition, string parameterName)
        {
            if (!IsFinite(screenPosition.x) || !IsFinite(screenPosition.y))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private void EnsurePhaseAllows(string operation, params TabletopInteractionPhase[] allowedPhases)
        {
            for (int i = 0; i < allowedPhases.Length; i++)
            {
                if (Phase == allowedPhases[i])
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"{operation} is not valid while the interaction phase is {Phase}.");
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
