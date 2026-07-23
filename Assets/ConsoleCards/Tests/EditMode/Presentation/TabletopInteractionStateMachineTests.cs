using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace ConsoleCards.Tests.EditMode.Presentation
{
    public sealed class TabletopInteractionStateMachineTests
    {
        public enum ActiveInteractionPhase
        {
            Pressed,
            DraggingObject,
            AwaitingAcceptance,
            Cancelling
        }

        public enum AnyInteractionPhase
        {
            Idle,
            Hovering,
            Pressed,
            DraggingObject,
            AwaitingAcceptance,
            Cancelling
        }

        public enum UpdatePointerInvalidPhase
        {
            Idle,
            Hovering,
            AwaitingAcceptance,
            Cancelling
        }

        public enum CancellationInvalidPhase
        {
            Idle,
            Hovering,
            Cancelling
        }

        [Test]
        public void Constructor_WhenThresholdIsValid_StoresThreshold()
        {
            TabletopInteractionStateMachine stateMachine = new TabletopInteractionStateMachine(7.5f);

            Assert.That(stateMachine.DragThresholdPixels, Is.EqualTo(7.5f));
        }

        [Test]
        public void Constructor_WhenThresholdIsZero_AcceptsThreshold()
        {
            TabletopInteractionStateMachine stateMachine = new TabletopInteractionStateMachine(0f);

            Assert.That(stateMachine.DragThresholdPixels, Is.EqualTo(0f));
        }

        [Test]
        public void Constructor_WhenThresholdIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopInteractionStateMachine(-0.01f));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Constructor_WhenThresholdIsNonFinite_ThrowsArgumentOutOfRangeException(float threshold)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TabletopInteractionStateMachine(threshold));
        }

        [Test]
        public void Constructor_InitialStateMatchesApprovedContract()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            AssertIdleState(stateMachine);
            Assert.That(stateMachine.DragThresholdPixels, Is.EqualTo(5f));
        }

        [Test]
        public void SetHoveredObject_FromIdle_TransitionsToHovering()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            TabletopObjectId objectId = ObjectId(1);

            stateMachine.SetHoveredObject(objectId);

            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Hovering));
            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(objectId));
            Assert.That(stateMachine.HasActiveObject, Is.True);
        }

        [Test]
        public void SetHoveredObject_WhenAlreadyHovering_ReplacesHoveredObject()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            TabletopObjectId replacementId = ObjectId(2);
            stateMachine.SetHoveredObject(ObjectId(1));

            stateMachine.SetHoveredObject(replacementId);

            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(replacementId));
            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Hovering));
        }

        [Test]
        public void SetHoveredObject_WhenObjectIdIsEmpty_ThrowsArgumentException()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            Assert.Throws<ArgumentException>(() => stateMachine.SetHoveredObject(TabletopObjectId.Empty));
        }

        [Test]
        public void ClearHoveredObject_FromHovering_TransitionsToIdle()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            stateMachine.SetHoveredObject(ObjectId(1));

            stateMachine.ClearHoveredObject();

            AssertIdleState(stateMachine);
        }

        [Test]
        public void ClearHoveredObject_FromIdle_IsIdempotent()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            stateMachine.ClearHoveredObject();
            stateMachine.ClearHoveredObject();

            AssertIdleState(stateMachine);
        }

        [Test]
        public void Hovering_NeverCapturesPointer()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            stateMachine.SetHoveredObject(ObjectId(1));

            Assert.That(stateMachine.HasPointerCapture, Is.False);
        }

        [TestCase(ActiveInteractionPhase.Pressed)]
        [TestCase(ActiveInteractionPhase.DraggingObject)]
        [TestCase(ActiveInteractionPhase.AwaitingAcceptance)]
        [TestCase(ActiveInteractionPhase.Cancelling)]
        public void HoverOperations_WhenInteractionIsActive_ThrowInvalidOperationException(ActiveInteractionPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);

            Assert.Throws<InvalidOperationException>(() => stateMachine.SetHoveredObject(ObjectId(9)));
            Assert.Throws<InvalidOperationException>(() => stateMachine.ClearHoveredObject());
        }

        [Test]
        public void HoverOperation_WhenTransitionIsInvalid_PreservesPreviousState()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(ActiveInteractionPhase.DraggingObject);
            StateSnapshot snapshot = StateSnapshot.Capture(stateMachine);

            Assert.Throws<InvalidOperationException>(() => stateMachine.SetHoveredObject(ObjectId(9)));

            snapshot.AssertMatches(stateMachine);
        }

        [Test]
        public void BeginPress_FromIdle_TransitionsToPressed()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            TabletopObjectId objectId = ObjectId(1);

            stateMachine.BeginPress(objectId, new Vector2(10f, 20f));

            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(objectId));
        }

        [Test]
        public void BeginPress_FromHovering_TransitionsToPressed()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            stateMachine.SetHoveredObject(ObjectId(1));

            stateMachine.BeginPress(ObjectId(2), new Vector2(10f, 20f));

            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
        }

        [Test]
        public void BeginPress_FromHovering_ReplacesHoveredObjectId()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            TabletopObjectId pressedObjectId = ObjectId(2);
            stateMachine.SetHoveredObject(ObjectId(1));

            stateMachine.BeginPress(pressedObjectId, new Vector2(10f, 20f));

            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(pressedObjectId));
        }

        [Test]
        public void BeginPress_StoresPressAndCurrentScreenPosition()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            Vector2 screenPosition = new Vector2(10f, 20f);

            stateMachine.BeginPress(ObjectId(1), screenPosition);

            Assert.That(stateMachine.PressScreenPosition, Is.EqualTo(screenPosition));
            Assert.That(stateMachine.CurrentScreenPosition, Is.EqualTo(screenPosition));
        }

        [Test]
        public void BeginPress_CapturesPointer()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            stateMachine.BeginPress(ObjectId(1), new Vector2(10f, 20f));

            Assert.That(stateMachine.HasPointerCapture, Is.True);
        }

        [Test]
        public void BeginPress_WhenObjectIdIsEmpty_ThrowsArgumentException()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            Assert.Throws<ArgumentException>(() => stateMachine.BeginPress(TabletopObjectId.Empty, new Vector2(10f, 20f)));
        }

        [TestCase(float.NaN, 20f)]
        [TestCase(float.PositiveInfinity, 20f)]
        [TestCase(10f, float.NegativeInfinity)]
        public void BeginPress_WhenScreenPositionIsNonFinite_ThrowsArgumentOutOfRangeException(
            float x,
            float y)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            Assert.Throws<ArgumentOutOfRangeException>(() => stateMachine.BeginPress(ObjectId(1), new Vector2(x, y)));
        }

        [TestCase(ActiveInteractionPhase.Pressed)]
        [TestCase(ActiveInteractionPhase.DraggingObject)]
        [TestCase(ActiveInteractionPhase.AwaitingAcceptance)]
        [TestCase(ActiveInteractionPhase.Cancelling)]
        public void BeginPress_WhenInteractionIsActive_ThrowsInvalidOperationException(ActiveInteractionPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);

            Assert.Throws<InvalidOperationException>(() => stateMachine.BeginPress(ObjectId(9), new Vector2(10f, 20f)));
        }

        [Test]
        public void BeginPress_WhenValidationFails_PreservesPreviousState()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            stateMachine.SetHoveredObject(ObjectId(1));
            StateSnapshot snapshot = StateSnapshot.Capture(stateMachine);

            Assert.Throws<ArgumentException>(() => stateMachine.BeginPress(TabletopObjectId.Empty, new Vector2(10f, 20f)));

            snapshot.AssertMatches(stateMachine);
        }

        [Test]
        public void UpdatePointer_WhenMovementIsBelowThreshold_RemainsPressed()
        {
            TabletopInteractionStateMachine stateMachine = CreatePressedStateMachine();

            bool startedDragging = stateMachine.UpdatePointer(new Vector2(3f, 0f));

            Assert.That(startedDragging, Is.False);
            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
        }

        [Test]
        public void UpdatePointer_WhenMovementEqualsThreshold_StartsDragging()
        {
            TabletopInteractionStateMachine stateMachine = CreatePressedStateMachine();

            bool startedDragging = stateMachine.UpdatePointer(new Vector2(5f, 0f));

            Assert.That(startedDragging, Is.True);
            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
        }

        [Test]
        public void UpdatePointer_WhenMovementExceedsThreshold_StartsDragging()
        {
            TabletopInteractionStateMachine stateMachine = CreatePressedStateMachine();

            bool startedDragging = stateMachine.UpdatePointer(new Vector2(6f, 0f));

            Assert.That(startedDragging, Is.True);
            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
        }

        [Test]
        public void UpdatePointer_DiagonalDistanceIsCalculatedCorrectly()
        {
            TabletopInteractionStateMachine stateMachine = CreatePressedStateMachine();

            bool startedDragging = stateMachine.UpdatePointer(new Vector2(3f, 4f));

            Assert.That(startedDragging, Is.True);
            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
        }

        [Test]
        public void UpdatePointer_ReturnsTrueOnlyWhenDragStarts()
        {
            TabletopInteractionStateMachine stateMachine = CreatePressedStateMachine();

            bool first = stateMachine.UpdatePointer(new Vector2(5f, 0f));
            bool second = stateMachine.UpdatePointer(new Vector2(8f, 0f));

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
        }

        [Test]
        public void UpdatePointer_WhenAlreadyDragging_ReturnsFalse()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();

            bool startedDragging = stateMachine.UpdatePointer(new Vector2(12f, 14f));

            Assert.That(startedDragging, Is.False);
            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
        }

        [Test]
        public void UpdatePointer_WhenDragging_UpdatesCurrentPointerPosition()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();
            Vector2 currentPosition = new Vector2(12f, 14f);

            stateMachine.UpdatePointer(currentPosition);

            Assert.That(stateMachine.CurrentScreenPosition, Is.EqualTo(currentPosition));
        }

        [Test]
        public void UpdatePointer_WhenThresholdIsZero_StartsDraggingOnFirstValidUpdate()
        {
            TabletopInteractionStateMachine stateMachine = new TabletopInteractionStateMachine(0f);
            stateMachine.BeginPress(ObjectId(1), new Vector2(10f, 20f));

            bool startedDragging = stateMachine.UpdatePointer(new Vector2(10f, 20f));

            Assert.That(startedDragging, Is.True);
            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        [TestCase(0f, float.NegativeInfinity)]
        public void UpdatePointer_WhenScreenPositionIsNonFinite_ThrowsWithoutMutation(float x, float y)
        {
            TabletopInteractionStateMachine stateMachine = CreatePressedStateMachine();
            StateSnapshot snapshot = StateSnapshot.Capture(stateMachine);

            Assert.Throws<ArgumentOutOfRangeException>(() => stateMachine.UpdatePointer(new Vector2(x, y)));

            snapshot.AssertMatches(stateMachine);
        }

        [Test]
        public void UpdatePointer_WhenDistanceCalculationOverflows_ThrowsWithoutMutation()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            stateMachine.BeginPress(ObjectId(1), new Vector2(-float.MaxValue, 0f));
            StateSnapshot snapshot = StateSnapshot.Capture(stateMachine);

            Assert.Throws<OverflowException>(() => stateMachine.UpdatePointer(new Vector2(float.MaxValue, 0f)));

            snapshot.AssertMatches(stateMachine);
        }

        [TestCase(UpdatePointerInvalidPhase.Idle)]
        [TestCase(UpdatePointerInvalidPhase.Hovering)]
        [TestCase(UpdatePointerInvalidPhase.AwaitingAcceptance)]
        [TestCase(UpdatePointerInvalidPhase.Cancelling)]
        public void UpdatePointer_WhenPhaseIsInvalid_ThrowsInvalidOperationException(UpdatePointerInvalidPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);

            Assert.Throws<InvalidOperationException>(() => stateMachine.UpdatePointer(new Vector2(1f, 1f)));
        }

        [Test]
        public void UpdatePointer_WhenPhaseIsInvalid_PreservesPreviousState()
        {
            TabletopInteractionStateMachine stateMachine = CreateAwaitingAcceptanceStateMachine();
            StateSnapshot snapshot = StateSnapshot.Capture(stateMachine);

            Assert.Throws<InvalidOperationException>(() => stateMachine.UpdatePointer(new Vector2(1f, 1f)));

            snapshot.AssertMatches(stateMachine);
        }

        [Test]
        public void ReleasePointer_FromPressed_ReturnsToIdle()
        {
            TabletopInteractionStateMachine stateMachine = CreatePressedStateMachine();

            TabletopInteractionPhase result = stateMachine.ReleasePointer();

            Assert.That(result, Is.EqualTo(TabletopInteractionPhase.Idle));
            AssertIdleState(stateMachine);
        }

        [Test]
        public void ReleasePointer_FromPressed_ClearsActiveState()
        {
            TabletopInteractionStateMachine stateMachine = CreatePressedStateMachine();

            stateMachine.ReleasePointer();

            Assert.That(stateMachine.HasActiveObject, Is.False);
            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(TabletopObjectId.Empty));
            Assert.That(stateMachine.PressScreenPosition, Is.EqualTo(Vector2.zero));
            Assert.That(stateMachine.CurrentScreenPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ReleasePointer_FromDragging_EntersAwaitingAcceptance()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();

            TabletopInteractionPhase result = stateMachine.ReleasePointer();

            Assert.That(result, Is.EqualTo(TabletopInteractionPhase.AwaitingAcceptance));
            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.AwaitingAcceptance));
        }

        [Test]
        public void ReleasePointer_FromDragging_PreservesActiveObjectAndPointerPositions()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();
            TabletopObjectId objectId = stateMachine.ActiveObjectId;
            Vector2 pressPosition = stateMachine.PressScreenPosition;
            Vector2 currentPosition = stateMachine.CurrentScreenPosition;

            stateMachine.ReleasePointer();

            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(objectId));
            Assert.That(stateMachine.PressScreenPosition, Is.EqualTo(pressPosition));
            Assert.That(stateMachine.CurrentScreenPosition, Is.EqualTo(currentPosition));
        }

        [Test]
        public void ReleasePointer_ClearsPointerCapture()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();

            stateMachine.ReleasePointer();

            Assert.That(stateMachine.HasPointerCapture, Is.False);
        }

        [TestCase(UpdatePointerInvalidPhase.Idle)]
        [TestCase(UpdatePointerInvalidPhase.Hovering)]
        [TestCase(UpdatePointerInvalidPhase.AwaitingAcceptance)]
        [TestCase(UpdatePointerInvalidPhase.Cancelling)]
        public void ReleasePointer_WhenPhaseIsInvalid_ThrowsWithoutMutation(UpdatePointerInvalidPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);
            StateSnapshot snapshot = StateSnapshot.Capture(stateMachine);

            Assert.Throws<InvalidOperationException>(() => stateMachine.ReleasePointer());

            snapshot.AssertMatches(stateMachine);
        }

        [Test]
        public void CompleteAcceptance_FromAwaitingAcceptance_ReturnsToIdle()
        {
            TabletopInteractionStateMachine stateMachine = CreateAwaitingAcceptanceStateMachine();

            stateMachine.CompleteAcceptance();

            AssertIdleState(stateMachine);
        }

        [Test]
        public void CompleteAcceptance_ClearsActiveObjectAndPointerPositions()
        {
            TabletopInteractionStateMachine stateMachine = CreateAwaitingAcceptanceStateMachine();

            stateMachine.CompleteAcceptance();

            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(TabletopObjectId.Empty));
            Assert.That(stateMachine.PressScreenPosition, Is.EqualTo(Vector2.zero));
            Assert.That(stateMachine.CurrentScreenPosition, Is.EqualTo(Vector2.zero));
        }

        [TestCase(AnyInteractionPhase.Idle)]
        [TestCase(AnyInteractionPhase.Hovering)]
        [TestCase(AnyInteractionPhase.Pressed)]
        [TestCase(AnyInteractionPhase.DraggingObject)]
        [TestCase(AnyInteractionPhase.Cancelling)]
        public void CompleteAcceptance_WhenPhaseIsInvalid_ThrowsWithoutMutation(AnyInteractionPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);
            StateSnapshot snapshot = StateSnapshot.Capture(stateMachine);

            Assert.Throws<InvalidOperationException>(() => stateMachine.CompleteAcceptance());

            snapshot.AssertMatches(stateMachine);
        }

        [TestCase(ActiveInteractionPhase.Pressed)]
        [TestCase(ActiveInteractionPhase.DraggingObject)]
        [TestCase(ActiveInteractionPhase.AwaitingAcceptance)]
        public void BeginCancellation_FromAllowedPhase_TransitionsToCancelling(ActiveInteractionPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);

            stateMachine.BeginCancellation();

            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Cancelling));
        }

        [Test]
        public void BeginCancellation_PreservesActiveObject()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();
            TabletopObjectId objectId = stateMachine.ActiveObjectId;

            stateMachine.BeginCancellation();

            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(objectId));
            Assert.That(stateMachine.HasActiveObject, Is.True);
        }

        [Test]
        public void BeginCancellation_PreservesPointerPositions()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();
            Vector2 pressPosition = stateMachine.PressScreenPosition;
            Vector2 currentPosition = stateMachine.CurrentScreenPosition;

            stateMachine.BeginCancellation();

            Assert.That(stateMachine.PressScreenPosition, Is.EqualTo(pressPosition));
            Assert.That(stateMachine.CurrentScreenPosition, Is.EqualTo(currentPosition));
        }

        [Test]
        public void BeginCancellation_ClearsPointerCapture()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();

            stateMachine.BeginCancellation();

            Assert.That(stateMachine.HasPointerCapture, Is.False);
        }

        [TestCase(CancellationInvalidPhase.Idle)]
        [TestCase(CancellationInvalidPhase.Hovering)]
        [TestCase(CancellationInvalidPhase.Cancelling)]
        public void BeginCancellation_WhenPhaseIsInvalid_ThrowsWithoutMutation(CancellationInvalidPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);
            StateSnapshot snapshot = StateSnapshot.Capture(stateMachine);

            Assert.Throws<InvalidOperationException>(() => stateMachine.BeginCancellation());

            snapshot.AssertMatches(stateMachine);
        }

        [Test]
        public void CompleteCancellation_FromCancelling_ReturnsToIdle()
        {
            TabletopInteractionStateMachine stateMachine = CreateCancellingStateMachine();

            stateMachine.CompleteCancellation();

            AssertIdleState(stateMachine);
        }

        [Test]
        public void CompleteCancellation_ClearsActiveState()
        {
            TabletopInteractionStateMachine stateMachine = CreateCancellingStateMachine();

            stateMachine.CompleteCancellation();

            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(TabletopObjectId.Empty));
            Assert.That(stateMachine.PressScreenPosition, Is.EqualTo(Vector2.zero));
            Assert.That(stateMachine.CurrentScreenPosition, Is.EqualTo(Vector2.zero));
        }

        [TestCase(AnyInteractionPhase.Idle)]
        [TestCase(AnyInteractionPhase.Hovering)]
        [TestCase(AnyInteractionPhase.Pressed)]
        [TestCase(AnyInteractionPhase.DraggingObject)]
        [TestCase(AnyInteractionPhase.AwaitingAcceptance)]
        public void CompleteCancellation_WhenPhaseIsInvalid_ThrowsWithoutMutation(AnyInteractionPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);
            StateSnapshot snapshot = StateSnapshot.Capture(stateMachine);

            Assert.Throws<InvalidOperationException>(() => stateMachine.CompleteCancellation());

            snapshot.AssertMatches(stateMachine);
        }

        [TestCase(AnyInteractionPhase.Idle)]
        [TestCase(AnyInteractionPhase.Hovering)]
        [TestCase(AnyInteractionPhase.Pressed)]
        [TestCase(AnyInteractionPhase.DraggingObject)]
        [TestCase(AnyInteractionPhase.AwaitingAcceptance)]
        [TestCase(AnyInteractionPhase.Cancelling)]
        public void Reset_FromEveryPhase_ReturnsToIdle(AnyInteractionPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);

            stateMachine.Reset();

            AssertIdleState(stateMachine);
        }

        [Test]
        public void Reset_ProducesApprovedIdleState()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();

            stateMachine.Reset();

            AssertIdleState(stateMachine);
        }

        [Test]
        public void Reset_IsIdempotent()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            stateMachine.Reset();
            stateMachine.Reset();

            AssertIdleState(stateMachine);
        }

        [TestCase(AnyInteractionPhase.Idle)]
        [TestCase(AnyInteractionPhase.Hovering)]
        [TestCase(AnyInteractionPhase.Pressed)]
        [TestCase(AnyInteractionPhase.DraggingObject)]
        [TestCase(AnyInteractionPhase.AwaitingAcceptance)]
        [TestCase(AnyInteractionPhase.Cancelling)]
        public void PointerCapture_IsTrueOnlyDuringPressedOrDragging(AnyInteractionPhase phase)
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachineInPhase(phase);

            bool expected = phase == AnyInteractionPhase.Pressed || phase == AnyInteractionPhase.DraggingObject;

            Assert.That(stateMachine.HasPointerCapture, Is.EqualTo(expected));
        }

        [Test]
        public void Idle_NeverRetainsActiveObject()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();

            stateMachine.Reset();

            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(stateMachine.HasActiveObject, Is.False);
            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(TabletopObjectId.Empty));
        }

        [Test]
        public void Hovering_NeverCapturesPointerInvariant()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            stateMachine.SetHoveredObject(ObjectId(1));

            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Hovering));
            Assert.That(stateMachine.HasPointerCapture, Is.False);
        }

        [Test]
        public void StateMachine_DoesNotRequireSelectionState()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            stateMachine.BeginPress(ObjectId(1), Vector2.zero);

            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
        }

        [Test]
        public void StateMachine_DoesNotRequireLockService()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            stateMachine.BeginPress(ObjectId(1), Vector2.zero);

            Assert.That(stateMachine.HasPointerCapture, Is.True);
        }

        [Test]
        public void StateMachine_DoesNotMutateTabletopObjectState()
        {
            TabletopObjectState objectState = CreateObjectState();
            TabletopPose originalPose = objectState.Pose;
            bool originalUserLocked = objectState.IsUserLocked;
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();

            stateMachine.BeginPress(objectState.Id, Vector2.zero);
            stateMachine.UpdatePointer(new Vector2(5f, 0f));
            stateMachine.ReleasePointer();
            stateMachine.CompleteAcceptance();

            Assert.That(objectState.Pose, Is.EqualTo(originalPose));
            Assert.That(objectState.IsUserLocked, Is.EqualTo(originalUserLocked));
        }

        private static TabletopInteractionStateMachine CreateStateMachine()
        {
            return new TabletopInteractionStateMachine(5f);
        }

        private static TabletopInteractionStateMachine CreatePressedStateMachine()
        {
            TabletopInteractionStateMachine stateMachine = CreateStateMachine();
            stateMachine.BeginPress(ObjectId(1), Vector2.zero);
            return stateMachine;
        }

        private static TabletopInteractionStateMachine CreateDraggingStateMachine()
        {
            TabletopInteractionStateMachine stateMachine = CreatePressedStateMachine();
            stateMachine.UpdatePointer(new Vector2(5f, 0f));
            return stateMachine;
        }

        private static TabletopInteractionStateMachine CreateAwaitingAcceptanceStateMachine()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();
            stateMachine.ReleasePointer();
            return stateMachine;
        }

        private static TabletopInteractionStateMachine CreateCancellingStateMachine()
        {
            TabletopInteractionStateMachine stateMachine = CreateDraggingStateMachine();
            stateMachine.BeginCancellation();
            return stateMachine;
        }

        private static TabletopInteractionStateMachine CreateStateMachineInPhase(ActiveInteractionPhase phase)
        {
            switch (phase)
            {
                case ActiveInteractionPhase.Pressed:
                    return CreatePressedStateMachine();
                case ActiveInteractionPhase.DraggingObject:
                    return CreateDraggingStateMachine();
                case ActiveInteractionPhase.AwaitingAcceptance:
                    return CreateAwaitingAcceptanceStateMachine();
                case ActiveInteractionPhase.Cancelling:
                    return CreateCancellingStateMachine();
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private static TabletopInteractionStateMachine CreateStateMachineInPhase(UpdatePointerInvalidPhase phase)
        {
            switch (phase)
            {
                case UpdatePointerInvalidPhase.Idle:
                    return CreateStateMachine();
                case UpdatePointerInvalidPhase.Hovering:
                    TabletopInteractionStateMachine hovering = CreateStateMachine();
                    hovering.SetHoveredObject(ObjectId(1));
                    return hovering;
                case UpdatePointerInvalidPhase.AwaitingAcceptance:
                    return CreateAwaitingAcceptanceStateMachine();
                case UpdatePointerInvalidPhase.Cancelling:
                    return CreateCancellingStateMachine();
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private static TabletopInteractionStateMachine CreateStateMachineInPhase(CancellationInvalidPhase phase)
        {
            switch (phase)
            {
                case CancellationInvalidPhase.Idle:
                    return CreateStateMachine();
                case CancellationInvalidPhase.Hovering:
                    TabletopInteractionStateMachine hovering = CreateStateMachine();
                    hovering.SetHoveredObject(ObjectId(1));
                    return hovering;
                case CancellationInvalidPhase.Cancelling:
                    return CreateCancellingStateMachine();
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private static TabletopInteractionStateMachine CreateStateMachineInPhase(AnyInteractionPhase phase)
        {
            switch (phase)
            {
                case AnyInteractionPhase.Idle:
                    return CreateStateMachine();
                case AnyInteractionPhase.Hovering:
                    TabletopInteractionStateMachine hovering = CreateStateMachine();
                    hovering.SetHoveredObject(ObjectId(1));
                    return hovering;
                case AnyInteractionPhase.Pressed:
                    return CreatePressedStateMachine();
                case AnyInteractionPhase.DraggingObject:
                    return CreateDraggingStateMachine();
                case AnyInteractionPhase.AwaitingAcceptance:
                    return CreateAwaitingAcceptanceStateMachine();
                case AnyInteractionPhase.Cancelling:
                    return CreateCancellingStateMachine();
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private static void AssertIdleState(TabletopInteractionStateMachine stateMachine)
        {
            Assert.That(stateMachine.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(TabletopObjectId.Empty));
            Assert.That(stateMachine.HasActiveObject, Is.False);
            Assert.That(stateMachine.HasPointerCapture, Is.False);
            Assert.That(stateMachine.PressScreenPosition, Is.EqualTo(Vector2.zero));
            Assert.That(stateMachine.CurrentScreenPosition, Is.EqualTo(Vector2.zero));
        }

        private static TabletopObjectState CreateObjectState()
        {
            return new TabletopObjectState(
                ObjectId(100),
                new ObjectDefinitionId(GuidFromSeed(200)),
                TabletopObjectKind.Card,
                new TabletopPose(new TableCoordinate(1.5, -2.5), 30f, 1, 2),
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                true);
        }

        private static TabletopObjectId ObjectId(int seed)
        {
            return new TabletopObjectId(GuidFromSeed(seed));
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private readonly struct StateSnapshot
        {
            private StateSnapshot(
                TabletopInteractionPhase phase,
                TabletopObjectId activeObjectId,
                bool hasActiveObject,
                bool hasPointerCapture,
                Vector2 pressScreenPosition,
                Vector2 currentScreenPosition)
            {
                Phase = phase;
                ActiveObjectId = activeObjectId;
                HasActiveObject = hasActiveObject;
                HasPointerCapture = hasPointerCapture;
                PressScreenPosition = pressScreenPosition;
                CurrentScreenPosition = currentScreenPosition;
            }

            private TabletopInteractionPhase Phase { get; }

            private TabletopObjectId ActiveObjectId { get; }

            private bool HasActiveObject { get; }

            private bool HasPointerCapture { get; }

            private Vector2 PressScreenPosition { get; }

            private Vector2 CurrentScreenPosition { get; }

            public static StateSnapshot Capture(TabletopInteractionStateMachine stateMachine)
            {
                return new StateSnapshot(
                    stateMachine.Phase,
                    stateMachine.ActiveObjectId,
                    stateMachine.HasActiveObject,
                    stateMachine.HasPointerCapture,
                    stateMachine.PressScreenPosition,
                    stateMachine.CurrentScreenPosition);
            }

            public void AssertMatches(TabletopInteractionStateMachine stateMachine)
            {
                Assert.That(stateMachine.Phase, Is.EqualTo(Phase));
                Assert.That(stateMachine.ActiveObjectId, Is.EqualTo(ActiveObjectId));
                Assert.That(stateMachine.HasActiveObject, Is.EqualTo(HasActiveObject));
                Assert.That(stateMachine.HasPointerCapture, Is.EqualTo(HasPointerCapture));
                Assert.That(stateMachine.PressScreenPosition, Is.EqualTo(PressScreenPosition));
                Assert.That(stateMachine.CurrentScreenPosition, Is.EqualTo(CurrentScreenPosition));
            }
        }
    }
}
