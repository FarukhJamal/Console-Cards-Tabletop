using System;
using System.Collections.Generic;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopMoveInteractionCoordinatorTests
    {
        private const int InteractionLayer = 8;
        private const float Tolerance = 0.0001f;
        private const double CoordinateTolerance = 0.00001d;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                if (createdGameObjects[i] != null)
                {
                    UnityObject.DestroyImmediate(createdGameObjects[i]);
                }
            }

            createdGameObjects.Clear();
        }

        [Test]
        public void Constructor_WithValidDependencies_ConstructsSuccessfully()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();

            Assert.That(fixture.Coordinator.MatchState, Is.SameAs(fixture.Match));
            Assert.That(fixture.Coordinator.RequestedByPlayerId, Is.EqualTo(fixture.RequestedByPlayerId));
            Assert.That(fixture.Coordinator.InteractionOwnerId, Is.EqualTo(fixture.OwnerId));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [TestCase(RequiredDependency.MatchState)]
        [TestCase(RequiredDependency.SelectionState)]
        [TestCase(RequiredDependency.HitResolver)]
        [TestCase(RequiredDependency.PointerProjector)]
        [TestCase(RequiredDependency.LockService)]
        [TestCase(RequiredDependency.StateMachine)]
        [TestCase(RequiredDependency.PreviewSession)]
        [TestCase(RequiredDependency.MoveUseCase)]
        public void Constructor_WhenRequiredDependencyIsNull_ThrowsArgumentNullException(RequiredDependency dependency)
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.Clear(dependency);

            Assert.Throws<ArgumentNullException>(() => dependencies.CreateCoordinator());
        }

        [Test]
        public void Constructor_WhenPlayerIdIsEmpty_ThrowsArgumentException()
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.RequestedByPlayerId = PlayerId.Empty;

            Assert.Throws<ArgumentException>(() => dependencies.CreateCoordinator());
        }

        [Test]
        public void Constructor_WhenInteractionOwnerIdIsEmpty_ThrowsArgumentException()
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.OwnerId = InteractionOwnerId.Empty;

            Assert.Throws<ArgumentException>(() => dependencies.CreateCoordinator());
        }

        [Test]
        public void Constructor_WhenStateMachineIsNotIdle_ThrowsArgumentException()
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.StateMachine.SetHoveredObject(dependencies.View.ObjectId);

            Assert.Throws<ArgumentException>(() => dependencies.CreateCoordinator());
        }

        [Test]
        public void Constructor_WhenPreviewSessionIsActive_ThrowsArgumentException()
        {
            ConstructorDependencies dependencies = CreateConstructorDependencies();
            dependencies.PreviewSession.Begin(dependencies.View);

            Assert.Throws<ArgumentException>(() => dependencies.CreateCoordinator());
        }

        [TestCase(TabletopObjectKind.Card)]
        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void TryBeginPress_WhenBoundObjectIsHit_BeginsPress(TabletopObjectKind kind)
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture(kind: kind);

            bool began = fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View));

            Assert.That(began, Is.True);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
        }

        [Test]
        public void TryBeginPress_WhenSuccessful_SelectsView()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();

            fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View));

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
        }

        [Test]
        public void TryBeginPress_WhenSuccessful_AcquiresLocalLock()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();

            fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View));

            Assert.That(fixture.LockService.IsOwnedBy(fixture.View.ObjectId, fixture.OwnerId), Is.True);
        }

        [Test]
        public void TryBeginPress_WhenSuccessful_CapturesCorrectObjectId()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();

            fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View));

            Assert.That(fixture.Coordinator.ActiveView, Is.SameAs(fixture.View));
            Assert.That(fixture.StateMachine.ActiveObjectId, Is.EqualTo(fixture.View.ObjectId));
        }

        [Test]
        public void TryBeginPress_WhenEmptySpaceIsPressed_ClearsSelectionAndReturnsFalse()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.SelectionState.Select(fixture.View);
            fixture.SelectionState.SetHovered(fixture.View);

            bool began = fixture.Coordinator.TryBeginPress(fixture.ScreenPointForWorld(6f, 6f));

            Assert.That(began, Is.False);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.SelectionState.HasHoveredObject, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
        }

        [Test]
        public void TryBeginPress_WhenObjectIsUserLocked_ReturnsFalse()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture(isUserLocked: true);

            bool began = fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View));

            Assert.That(began, Is.False);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryBeginPress_WhenLocalLockConflicts_ReturnsFalseWithoutStealing()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            InteractionOwnerId otherOwner = InteractionOwnerId.New();
            fixture.LockService.Acquire(fixture.View.ObjectId, otherOwner);

            bool began = fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View));

            Assert.That(began, Is.False);
            Assert.That(fixture.LockService.IsOwnedBy(fixture.View.ObjectId, otherOwner), Is.True);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
        }

        [Test]
        public void TryBeginPress_WhenSameOwnerAlreadyOwnsLock_BeginsIdempotently()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.LockService.Acquire(fixture.View.ObjectId, fixture.OwnerId);

            bool began = fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View));

            Assert.That(began, Is.True);
            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
            Assert.That(fixture.LockService.IsOwnedBy(fixture.View.ObjectId, fixture.OwnerId), Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TryBeginPress_WhenScreenXIsInvalid_ThrowsWithoutAcquiringLock(float screenX)
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => fixture.Coordinator.TryBeginPress(new Vector2(screenX, fixture.ScreenPointFor(fixture.View).y)));

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TryBeginPress_WhenScreenYIsInvalid_ThrowsWithoutAcquiringLock(float screenY)
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => fixture.Coordinator.TryBeginPress(new Vector2(fixture.ScreenPointFor(fixture.View).x, screenY)));

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryBeginPress_WhenCoordinatorAlreadyActive_ThrowsWithoutChangingLock()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.BeginPress();

            Assert.Throws<InvalidOperationException>(
                () => fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View)));

            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
            Assert.That(fixture.StateMachine.ActiveObjectId, Is.EqualTo(fixture.View.ObjectId));
        }

        [Test]
        public void TryBeginPress_WhenStateMachineIsExternallyActive_ThrowsWithoutAcquiringLock()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.StateMachine.BeginPress(fixture.View.ObjectId, fixture.ScreenPointFor(fixture.View));

            Assert.Throws<InvalidOperationException>(
                () => fixture.Coordinator.TryBeginPress(fixture.ScreenPointFor(fixture.View)));

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
        }

        [Test]
        public void UpdatePointer_WhenBelowThreshold_RemainsPressedAndCreatesNoPreview()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.BeginPress();

            bool updated = fixture.Coordinator.UpdatePointer(fixture.PressScreenPoint + new Vector2(1f, 0f));

            Assert.That(updated, Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
            Assert.That(fixture.PreviewSession.IsActive, Is.False);
        }

        [Test]
        public void UpdatePointer_WhenThresholdIsCrossed_BeginsPreviewOnce()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.BeginPress();

            bool updated = fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(2f, 0f));

            Assert.That(updated, Is.True);
            Assert.That(fixture.PreviewSession.IsActive, Is.True);
            Assert.That(fixture.PreviewSession.ActiveView, Is.SameAs(fixture.View));
        }

        [Test]
        public void UpdatePointer_WhenDragging_AppliesProjectedPreviewCoordinate()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();

            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(3f, -4f));

            Assert.That(fixture.View.IsPreviewing, Is.True);
            AssertCoordinate(fixture.View.PreviewPose.Position, 3.0, -4.0);
            AssertVector3(fixture.View.transform.position, 3f, 0f, -4f);
        }

        [Test]
        public void UpdatePointer_WhenRepeated_ReplacesPreview()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();

            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(1f, 2f));
            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(-3f, 4f));

            AssertCoordinate(fixture.View.PreviewPose.Position, -3.0, 4.0);
            AssertVector3(fixture.View.transform.position, -3f, 0f, 4f);
        }

        [Test]
        public void UpdatePointer_WhenProjectionFails_PreservesPreviousPreview()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(1f, 2f));
            TabletopPose previousPreview = fixture.View.PreviewPose;
            Vector3 previousPosition = fixture.View.transform.position;
            fixture.Camera.transform.rotation = Quaternion.identity;

            bool updated = fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(5f, 5f));

            Assert.That(updated, Is.False);
            Assert.That(fixture.View.PreviewPose, Is.EqualTo(previousPreview));
            AssertVector3(fixture.View.transform.position, previousPosition);
        }

        [Test]
        public void UpdatePointer_DoesNotMutateRuntimeStateDuringPreview()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            TabletopPose acceptedPose = fixture.State.Pose;

            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(4f, 5f));

            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
        }

        [Test]
        public void UpdatePointer_DoesNotChangeMatchRevisionDuringPreview()
        {
            CoordinatorFixture fixture = CreateDraggingFixture(revision: 7);

            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(4f, 5f));
            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(6f, 7f));

            Assert.That(fixture.Match.Revision, Is.EqualTo(7));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void UpdatePointer_WhenScreenXIsInvalid_PreservesActiveInteraction(float screenX)
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            TabletopPose previousPreview = fixture.View.PreviewPose;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => fixture.Coordinator.UpdatePointer(new Vector2(screenX, fixture.PressScreenPoint.y)));

            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.True);
            Assert.That(fixture.LockService.IsOwnedBy(fixture.View.ObjectId, fixture.OwnerId), Is.True);
            Assert.That(fixture.View.PreviewPose, Is.EqualTo(previousPreview));
        }

        [Test]
        public void UpdatePointer_WithoutActiveInteraction_Throws()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();

            Assert.Throws<InvalidOperationException>(
                () => fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(1f, 1f)));
        }

        [Test]
        public void ReleasePointer_FromPressed_ReturnsClickCompleted()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.BeginPress();

            MoveInteractionReleaseResult result = fixture.Coordinator.ReleasePointer(fixture.PressScreenPoint);

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.ClickCompleted));
            Assert.That(result.MovementAttempted, Is.False);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.MoveResult.HasValue, Is.False);
        }

        [Test]
        public void ReleasePointer_FromPressed_DoesNotMoveOrAdvanceRevision()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture(revision: 3);
            TabletopPose acceptedPose = fixture.State.Pose;
            fixture.BeginPress();

            fixture.Coordinator.ReleasePointer(fixture.PressScreenPoint);

            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(3));
        }

        [Test]
        public void ReleasePointer_FromPressed_ReleasesLockAndBecomesInactive()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.BeginPress();

            fixture.Coordinator.ReleasePointer(fixture.PressScreenPoint);

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [Test]
        public void ReleasePointer_FromPressed_PreservesSelection()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.BeginPress();

            fixture.Coordinator.ReleasePointer(fixture.PressScreenPoint);

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
        }

        [TestCase(TabletopObjectKind.Card)]
        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void ReleasePointer_WhenDragAccepted_UpdatesAuthoritativePose(TabletopObjectKind kind)
        {
            CoordinatorFixture fixture = CreateDraggingFixture(kind: kind);

            MoveInteractionReleaseResult result = fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(4f, -2f));

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveAccepted));
            AssertCoordinate(fixture.State.Pose.Position, 4.0, -2.0);
        }

        [Test]
        public void ReleasePointer_WhenDragAccepted_AdvancesRevisionOnceAndReportsMoveResult()
        {
            CoordinatorFixture fixture = CreateDraggingFixture(revision: 9);

            MoveInteractionReleaseResult result = fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(4f, -2f));

            Assert.That(fixture.Match.Revision, Is.EqualTo(10));
            Assert.That(result.MovementAttempted, Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.MoveResult.HasValue, Is.True);
            Assert.That(result.MoveResult.Value.Revision, Is.EqualTo(10));
        }

        [Test]
        public void ReleasePointer_WhenDragAccepted_ReconcilesViewAndClearsPreview()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();

            fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(4f, -2f));

            Assert.That(fixture.View.IsPreviewing, Is.False);
            AssertWorldPose(fixture.View, fixture.State.Pose);
        }

        [Test]
        public void ReleasePointer_WhenDragAccepted_CleansUpLifecycle()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();

            fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(4f, -2f));

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.PreviewSession.IsActive, Is.False);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
        }

        [Test]
        public void ReleasePointer_WhenDragAccepted_PreservesRotationLayerAndLocalOrder()
        {
            TabletopPose initialPose = new TabletopPose(new TableCoordinate(0.0, 0.0), 45f, 2, 3);
            CoordinatorFixture fixture = CreateDraggingFixture(initialPose: initialPose);

            fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(4f, -2f));

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(45f).Within(Tolerance));
            Assert.That(fixture.State.Pose.Layer, Is.EqualTo(2));
            Assert.That(fixture.State.Pose.LocalOrder, Is.EqualTo(3));
        }

        [Test]
        public void ReleasePointer_WhenObjectBecomesUserLocked_ReturnsMoveRejectedAndRollsBack()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            TabletopPose acceptedPose = fixture.State.Pose;
            fixture.State.SetUserLocked(true);

            MoveInteractionReleaseResult result = fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(4f, -2f));

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveRejected));
            Assert.That(result.MoveResult.Value.Error, Is.EqualTo(MoveObjectError.ObjectUserLocked));
            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            AssertWorldPose(fixture.View, acceptedPose);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [Test]
        public void ReleasePointer_WhenObjectIsMissingFromMatch_ReturnsMoveRejectedAndRollsBack()
        {
            CoordinatorFixture fixture = CreateDraggingFixture(includeViewObjectInMatch: false);
            TabletopPose acceptedPose = fixture.State.Pose;

            MoveInteractionReleaseResult result = fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(4f, -2f));

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveRejected));
            Assert.That(result.MoveResult.Value.Error, Is.EqualTo(MoveObjectError.ObjectNotFound));
            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            AssertWorldPose(fixture.View, acceptedPose);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
        }

        [Test]
        public void ReleasePointer_WhenProjectionFails_ReturnsProjectionFailedWithoutMutation()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            TabletopPose acceptedPose = fixture.State.Pose;
            fixture.Camera.transform.rotation = Quaternion.identity;

            MoveInteractionReleaseResult result = fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(4f, -2f));

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.ProjectionFailed));
            Assert.That(result.MovementAttempted, Is.False);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.MoveResult.HasValue, Is.False);
            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            AssertWorldPose(fixture.View, acceptedPose);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [Test]
        public void Cancel_FromPressed_CleansUpAndPreservesSelection()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.BeginPress();

            fixture.Coordinator.Cancel();

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
        }

        [Test]
        public void Cancel_FromDragging_RestoresAcceptedPoseAndPreservesRuntimeState()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            TabletopPose acceptedPose = fixture.State.Pose;
            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(4f, 5f));

            fixture.Coordinator.Cancel();

            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
            AssertWorldPose(fixture.View, acceptedPose);
            Assert.That(fixture.View.IsPreviewing, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
        }

        [Test]
        public void Cancel_WhenInactive_ThrowsWithoutPartialMutation()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();

            Assert.Throws<InvalidOperationException>(() => fixture.Coordinator.Cancel());

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [Test]
        public void Reset_WhenInactive_IsSafe()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();

            Assert.DoesNotThrow(() => fixture.Coordinator.Reset());

            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
        }

        [Test]
        public void Reset_FromPressed_ReleasesLockAndPreservesSelection()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            fixture.BeginPress();

            fixture.Coordinator.Reset();

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Coordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
        }

        [Test]
        public void Reset_FromDragging_RestoresAcceptedState()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            TabletopPose acceptedPose = fixture.State.Pose;
            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(4f, 5f));

            fixture.Coordinator.Reset();

            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
            AssertWorldPose(fixture.View, acceptedPose);
            Assert.That(fixture.View.IsPreviewing, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void Reset_WhenViewWasUnbound_ClearsSafely()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            fixture.View.Unbind();

            Assert.DoesNotThrow(() => fixture.Coordinator.Reset());

            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void Reset_WhenViewWasDestroyed_ClearsSafely()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            UnityObject.DestroyImmediate(fixture.View.gameObject);

            Assert.DoesNotThrow(() => fixture.Coordinator.Reset());

            Assert.That(fixture.Coordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void Reset_RemovesAllLocksForOwner()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture();
            TabletopObjectId extraObjectId = TabletopObjectId.New();
            fixture.LockService.Acquire(extraObjectId, fixture.OwnerId);
            fixture.BeginPress();

            fixture.Coordinator.Reset();

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void Reset_PreservesRuntimeStateAndSelection()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            TabletopPose acceptedPose = fixture.State.Pose;

            fixture.Coordinator.Reset();

            Assert.That(fixture.State.Pose, Is.EqualTo(acceptedPose));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
        }

        [Test]
        public void Coordinator_DoesNotRequireInputActionOrPlayerInput()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();

            MoveInteractionReleaseResult result = fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(1f, 1f));

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveAccepted));
        }

        [Test]
        public void Coordinator_DoesNotRequireSurfaceProxyOrSurfaceCollider()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();

            MoveInteractionReleaseResult result = fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(1f, 1f));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.View.GetComponent<Collider>(), Is.Not.Null);
        }

        [Test]
        public void Coordinator_DoesNotRequireGlobalViewRegistry()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();

            MoveInteractionReleaseResult result = fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(1f, 1f));

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveAccepted));
        }

        [Test]
        public void Coordinator_CreatesNoMoveCommandDuringUpdatesAndOneAcceptedMoveOnRelease()
        {
            CoordinatorFixture fixture = CreateDraggingFixture(revision: 12);

            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(2f, 3f));
            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(4f, 5f));
            Assert.That(fixture.Match.Revision, Is.EqualTo(12));

            fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(6f, 7f));

            Assert.That(fixture.Match.Revision, Is.EqualTo(13));
        }

        [Test]
        public void Coordinator_DoesNotMutateCameraOrOtherViews()
        {
            CoordinatorFixture fixture = CreateDraggingFixture();
            PawnView otherView = CreateBoundPawnView(900, new TabletopPose(new TableCoordinate(8.0, 9.0), 15f, 0, 0), out _);
            Vector3 cameraPosition = fixture.Camera.transform.position;
            Quaternion cameraRotation = fixture.Camera.transform.rotation;
            float orthographicSize = fixture.Camera.orthographicSize;
            Vector3 otherPosition = otherView.transform.position;
            Quaternion otherRotation = otherView.transform.rotation;

            fixture.Coordinator.ReleasePointer(fixture.ScreenPointForWorld(6f, 7f));

            AssertVector3(fixture.Camera.transform.position, cameraPosition);
            Assert.That(Quaternion.Angle(cameraRotation, fixture.Camera.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(fixture.Camera.orthographicSize, Is.EqualTo(orthographicSize).Within(Tolerance));
            AssertVector3(otherView.transform.position, otherPosition);
            Assert.That(Quaternion.Angle(otherRotation, otherView.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void MoveInteractionReleaseResult_ClickCompleted_HasApprovedValues()
        {
            MoveInteractionReleaseResult result = MoveInteractionReleaseResult.ClickCompleted();

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.ClickCompleted));
            Assert.That(result.MovementAttempted, Is.False);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.MoveResult.HasValue, Is.False);
        }

        [Test]
        public void MoveInteractionReleaseResult_ProjectionFailed_HasApprovedValues()
        {
            MoveInteractionReleaseResult result = MoveInteractionReleaseResult.ProjectionFailed();

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.ProjectionFailed));
            Assert.That(result.MovementAttempted, Is.False);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.MoveResult.HasValue, Is.False);
        }

        [Test]
        public void MoveInteractionReleaseResult_FromAcceptedMove_MapsToMoveAccepted()
        {
            MoveObjectResult moveResult = MoveObjectResult.Accepted(4);

            MoveInteractionReleaseResult result = MoveInteractionReleaseResult.FromMoveResult(moveResult);

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveAccepted));
            Assert.That(result.MovementAttempted, Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.MoveResult.Value, Is.EqualTo(moveResult));
        }

        [Test]
        public void MoveInteractionReleaseResult_FromFailedMove_MapsToMoveRejected()
        {
            MoveObjectResult moveResult = MoveObjectResult.Failure(CommandResultStatus.Rejected, MoveObjectError.ObjectNotFound);

            MoveInteractionReleaseResult result = MoveInteractionReleaseResult.FromMoveResult(moveResult);

            Assert.That(result.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveRejected));
            Assert.That(result.MovementAttempted, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.MoveResult.Value, Is.EqualTo(moveResult));
        }

        [Test]
        public void MoveInteractionReleaseResult_EqualityAndToStringBehaveCorrectly()
        {
            MoveInteractionReleaseResult first = MoveInteractionReleaseResult.FromMoveResult(MoveObjectResult.Accepted(5));
            MoveInteractionReleaseResult second = MoveInteractionReleaseResult.FromMoveResult(MoveObjectResult.Accepted(5));
            MoveInteractionReleaseResult different = MoveInteractionReleaseResult.ProjectionFailed();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ToString(), Does.Contain(nameof(MoveInteractionReleaseStatus.MoveAccepted)));
        }

        private CoordinatorFixture CreateDraggingFixture(
            TabletopObjectKind kind = TabletopObjectKind.Card,
            long revision = 0,
            TabletopPose? initialPose = null,
            bool includeViewObjectInMatch = true)
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture(
                kind: kind,
                revision: revision,
                initialPose: initialPose,
                includeViewObjectInMatch: includeViewObjectInMatch);
            fixture.BeginPress();
            fixture.Coordinator.UpdatePointer(fixture.ScreenPointForWorld(2f, 0f));
            return fixture;
        }

        private CoordinatorFixture CreateCoordinatorFixture(
            TabletopObjectKind kind = TabletopObjectKind.Card,
            long revision = 0,
            bool isUserLocked = false,
            TabletopPose? initialPose = null,
            bool includeViewObjectInMatch = true)
        {
            Camera camera = CreateCamera();
            TabletopCoordinateConverter converter = CreateConverter();
            TabletopObjectView view = CreateBoundView(kind, 1, initialPose ?? TabletopPose.Default, isUserLocked, out TabletopObjectState state);
            AddBoxCollider(view.gameObject, InteractionLayer);

            MatchState match = includeViewObjectInMatch
                ? CreateMatch(kind, state, revision)
                : CreateMatch(kind, CreateBaseState(kind, 200, TabletopPose.Default), revision);
            TabletopSelectionState selectionState = new TabletopSelectionState();
            TabletopObjectHitResolver hitResolver = new TabletopObjectHitResolver(camera, LayerMaskFor(InteractionLayer), 25f);
            TabletopPointerProjector pointerProjector = new TabletopPointerProjector(camera, converter, 0f);
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            TabletopInteractionStateMachine stateMachine = new TabletopInteractionStateMachine(5f);
            TabletopDragPreviewSession previewSession = new TabletopDragPreviewSession();
            MoveObjectUseCase moveUseCase = new MoveObjectUseCase();
            PlayerId requestedByPlayerId = PlayerId.New();
            InteractionOwnerId ownerId = InteractionOwnerId.New();
            TabletopMoveInteractionCoordinator coordinator = new TabletopMoveInteractionCoordinator(
                match,
                requestedByPlayerId,
                ownerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                stateMachine,
                previewSession,
                moveUseCase);

            return new CoordinatorFixture(
                coordinator,
                camera,
                match,
                view,
                state,
                selectionState,
                lockService,
                stateMachine,
                previewSession,
                requestedByPlayerId,
                ownerId);
        }

        private ConstructorDependencies CreateConstructorDependencies()
        {
            Camera camera = CreateCamera();
            TabletopCoordinateConverter converter = CreateConverter();
            TabletopObjectView view = CreateBoundView(TabletopObjectKind.Card, 50, TabletopPose.Default, false, out TabletopObjectState state);
            AddBoxCollider(view.gameObject, InteractionLayer);

            return new ConstructorDependencies
            {
                Match = CreateMatch(TabletopObjectKind.Card, state, 0),
                RequestedByPlayerId = PlayerId.New(),
                OwnerId = InteractionOwnerId.New(),
                SelectionState = new TabletopSelectionState(),
                HitResolver = new TabletopObjectHitResolver(camera, LayerMaskFor(InteractionLayer), 25f),
                PointerProjector = new TabletopPointerProjector(camera, converter, 0f),
                LockService = new LocalInteractionLockService(),
                StateMachine = new TabletopInteractionStateMachine(5f),
                PreviewSession = new TabletopDragPreviewSession(),
                MoveUseCase = new MoveObjectUseCase(),
                View = view
            };
        }

        private Camera CreateCamera()
        {
            GameObject cameraObject = CreateGameObject("Move Interaction Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f));
            return camera;
        }

        private TabletopObjectView CreateBoundView(
            TabletopObjectKind kind,
            int seed,
            TabletopPose pose,
            bool isUserLocked,
            out TabletopObjectState state)
        {
            switch (kind)
            {
                case TabletopObjectKind.Card:
                {
                    CardView view = CreateView<CardView>();
                    state = CreateBaseState(kind, seed, pose, isUserLocked);
                    view.Bind(new CardInstanceState(state, CardFace.FaceUp), CreateConverter());
                    return view;
                }

                case TabletopObjectKind.Pawn:
                {
                    PawnView view = CreateView<PawnView>();
                    state = CreateBaseState(kind, seed, pose, isUserLocked);
                    view.Bind(new PawnState(state), CreateConverter());
                    return view;
                }

                case TabletopObjectKind.Token:
                {
                    TokenView view = CreateView<TokenView>();
                    state = CreateBaseState(kind, seed, pose, isUserLocked);
                    view.Bind(new TokenState(state), CreateConverter());
                    return view;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported object kind.");
            }
        }

        private PawnView CreateBoundPawnView(int seed, TabletopPose pose, out PawnState state)
        {
            PawnView view = CreateView<PawnView>();
            state = new PawnState(CreateBaseState(TabletopObjectKind.Pawn, seed, pose, false));
            view.Bind(state, CreateConverter());
            return view;
        }

        private T CreateView<T>()
            where T : TabletopObjectView
        {
            GameObject gameObject = CreateGameObject(typeof(T).Name);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private static BoxCollider AddBoxCollider(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.2f, 1f);
            return collider;
        }

        private static MatchState CreateMatch(TabletopObjectKind kind, TabletopObjectState state, long revision)
        {
            switch (kind)
            {
                case TabletopObjectKind.Card:
                    return CreateMatch(revision, cards: new[] { new CardInstanceState(state, CardFace.FaceUp) });
                case TabletopObjectKind.Pawn:
                    return CreateMatch(revision, pawns: new[] { new PawnState(state) });
                case TabletopObjectKind.Token:
                    return CreateMatch(revision, tokens: new[] { new TokenState(state) });
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported object kind.");
            }
        }

        private static MatchState CreateMatch(
            long revision,
            CardInstanceState[] cards = null,
            PawnState[] pawns = null,
            TokenState[] tokens = null)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards ?? Array.Empty<CardInstanceState>(),
                pawns ?? Array.Empty<PawnState>(),
                tokens ?? Array.Empty<TokenState>(),
                Array.Empty<ContainerState>(),
                Array.Empty<SeatState>());
        }

        private static TabletopObjectState CreateBaseState(
            TabletopObjectKind kind,
            int seed,
            TabletopPose pose,
            bool isUserLocked = false)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                pose,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                isUserLocked);
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static LayerMask LayerMaskFor(int layer)
        {
            return 1 << layer;
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static void AssertWorldPose(TabletopObjectView view, TabletopPose pose)
        {
            AssertVector3(view.transform.position, (float)pose.Position.X, 0f, (float)pose.Position.Y);
            Assert.That(Quaternion.Angle(Quaternion.Euler(0f, pose.RotationDegrees, 0f), view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            AssertVector3(actual, expected.x, expected.y, expected.z);
        }

        private static void AssertVector3(Vector3 actual, float expectedX, float expectedY, float expectedZ)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expectedZ).Within(Tolerance));
        }

        private static void AssertCoordinate(TableCoordinate actual, double expectedX, double expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(CoordinateTolerance));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(CoordinateTolerance));
        }

        public enum RequiredDependency
        {
            MatchState,
            SelectionState,
            HitResolver,
            PointerProjector,
            LockService,
            StateMachine,
            PreviewSession,
            MoveUseCase
        }

        private sealed class ConstructorDependencies
        {
            public MatchState Match { get; set; }

            public PlayerId RequestedByPlayerId { get; set; }

            public InteractionOwnerId OwnerId { get; set; }

            public TabletopSelectionState SelectionState { get; set; }

            public TabletopObjectHitResolver HitResolver { get; set; }

            public TabletopPointerProjector PointerProjector { get; set; }

            public LocalInteractionLockService LockService { get; set; }

            public TabletopInteractionStateMachine StateMachine { get; set; }

            public TabletopDragPreviewSession PreviewSession { get; set; }

            public MoveObjectUseCase MoveUseCase { get; set; }

            public TabletopObjectView View { get; set; }

            public TabletopMoveInteractionCoordinator CreateCoordinator()
            {
                return new TabletopMoveInteractionCoordinator(
                    Match,
                    RequestedByPlayerId,
                    OwnerId,
                    SelectionState,
                    HitResolver,
                    PointerProjector,
                    LockService,
                    StateMachine,
                    PreviewSession,
                    MoveUseCase);
            }

            public void Clear(RequiredDependency dependency)
            {
                switch (dependency)
                {
                    case RequiredDependency.MatchState:
                        Match = null;
                        break;
                    case RequiredDependency.SelectionState:
                        SelectionState = null;
                        break;
                    case RequiredDependency.HitResolver:
                        HitResolver = null;
                        break;
                    case RequiredDependency.PointerProjector:
                        PointerProjector = null;
                        break;
                    case RequiredDependency.LockService:
                        LockService = null;
                        break;
                    case RequiredDependency.StateMachine:
                        StateMachine = null;
                        break;
                    case RequiredDependency.PreviewSession:
                        PreviewSession = null;
                        break;
                    case RequiredDependency.MoveUseCase:
                        MoveUseCase = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(dependency), dependency, "Unsupported dependency.");
                }
            }
        }

        private sealed class CoordinatorFixture
        {
            public CoordinatorFixture(
                TabletopMoveInteractionCoordinator coordinator,
                Camera camera,
                MatchState match,
                TabletopObjectView view,
                TabletopObjectState state,
                TabletopSelectionState selectionState,
                LocalInteractionLockService lockService,
                TabletopInteractionStateMachine stateMachine,
                TabletopDragPreviewSession previewSession,
                PlayerId requestedByPlayerId,
                InteractionOwnerId ownerId)
            {
                Coordinator = coordinator;
                Camera = camera;
                Match = match;
                View = view;
                State = state;
                SelectionState = selectionState;
                LockService = lockService;
                StateMachine = stateMachine;
                PreviewSession = previewSession;
                RequestedByPlayerId = requestedByPlayerId;
                OwnerId = ownerId;
                PressScreenPoint = ScreenPointFor(view);
            }

            public TabletopMoveInteractionCoordinator Coordinator { get; }

            public Camera Camera { get; }

            public MatchState Match { get; }

            public TabletopObjectView View { get; }

            public TabletopObjectState State { get; }

            public TabletopSelectionState SelectionState { get; }

            public LocalInteractionLockService LockService { get; }

            public TabletopInteractionStateMachine StateMachine { get; }

            public TabletopDragPreviewSession PreviewSession { get; }

            public PlayerId RequestedByPlayerId { get; }

            public InteractionOwnerId OwnerId { get; }

            public Vector2 PressScreenPoint { get; }

            public void BeginPress()
            {
                Assert.That(Coordinator.TryBeginPress(PressScreenPoint), Is.True);
            }

            public Vector2 ScreenPointFor(TabletopObjectView view)
            {
                return ScreenPointForWorld(view.transform.position.x, view.transform.position.z);
            }

            public Vector2 ScreenPointForWorld(float x, float z)
            {
                Physics.SyncTransforms();
                Vector3 screenPoint = Camera.WorldToScreenPoint(new Vector3(x, 0f, z));
                Assert.That(float.IsFinite(screenPoint.x), Is.True);
                Assert.That(float.IsFinite(screenPoint.y), Is.True);
                return new Vector2(screenPoint.x, screenPoint.y);
            }
        }
    }
}
