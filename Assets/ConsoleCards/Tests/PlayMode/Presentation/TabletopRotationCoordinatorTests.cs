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
    public sealed class TabletopRotationCoordinatorTests
    {
        private const float Tolerance = 0.0001f;

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
            RotationFixture fixture = CreateRotationFixture();

            Assert.That(fixture.Coordinator.MatchState, Is.SameAs(fixture.Match));
            Assert.That(fixture.Coordinator.RequestedByPlayerId, Is.EqualTo(fixture.RequestedByPlayerId));
            Assert.That(fixture.Coordinator.InteractionOwnerId, Is.EqualTo(fixture.OwnerId));
            Assert.That(fixture.Coordinator.SelectionState, Is.SameAs(fixture.SelectionState));
            Assert.That(fixture.Coordinator.LockService, Is.SameAs(fixture.LockService));
            Assert.That(fixture.Coordinator.RotateUseCase, Is.SameAs(fixture.RotateUseCase));
        }

        [TestCase(RequiredDependency.MatchState)]
        [TestCase(RequiredDependency.SelectionState)]
        [TestCase(RequiredDependency.LockService)]
        [TestCase(RequiredDependency.RotateUseCase)]
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
        public void RotateSelected_WhenNoSelection_ReturnsNoSelection()
        {
            RotationFixture fixture = CreateRotationFixture(selectView: false);

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            AssertResult(result, RotationInteractionStatus.NoSelection, false, false, false);
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenDeltaIsZero_ReturnsNoRotationRequested()
        {
            RotationFixture fixture = CreateRotationFixture();
            TabletopPose previousPose = fixture.State.Pose;

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(0f);

            AssertResult(result, RotationInteractionStatus.NoRotationRequested, false, false, false);
            Assert.That(fixture.State.Pose, Is.EqualTo(previousPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenSelectedViewIsDestroyed_ReturnsSelectionUnavailable()
        {
            RotationFixture fixture = CreateRotationFixture();
            UnityObject.DestroyImmediate(fixture.View.gameObject);

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            AssertResult(result, RotationInteractionStatus.SelectionUnavailable, false, false, false);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenSelectedViewIsUnbound_ReturnsSelectionUnavailable()
        {
            RotationFixture fixture = CreateRotationFixture();
            fixture.View.Unbind();

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            AssertResult(result, RotationInteractionStatus.SelectionUnavailable, false, false, false);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenSelectedViewIsDisabled_ReturnsSelectionUnavailable()
        {
            RotationFixture fixture = CreateRotationFixture();
            fixture.View.enabled = false;

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            AssertResult(result, RotationInteractionStatus.SelectionUnavailable, false, false, false);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenSelectedViewIsInactive_ReturnsSelectionUnavailable()
        {
            RotationFixture fixture = CreateRotationFixture();
            fixture.View.gameObject.SetActive(false);

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            AssertResult(result, RotationInteractionStatus.SelectionUnavailable, false, false, false);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenSelectedViewIsPreviewing_ThrowsWithoutMutation()
        {
            RotationFixture fixture = CreateRotationFixture();
            TabletopPose previousPose = fixture.State.Pose;
            Vector3 previousPosition = fixture.View.transform.position;
            fixture.View.ApplyPreviewPose(new TabletopPose(TableCoordinate.Zero, 90f, 0, 0));
            Quaternion previewRotation = fixture.View.transform.rotation;

            Assert.Throws<InvalidOperationException>(() => fixture.Coordinator.RotateSelected(15f));

            Assert.That(fixture.State.Pose, Is.EqualTo(previousPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            AssertVector3(fixture.View.transform.position, previousPosition);
            Assert.That(Quaternion.Angle(previewRotation, fixture.View.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RotateSelected_WhenDeltaIsNonFinite_ThrowsWithoutMutation(float delta)
        {
            RotationFixture fixture = CreateRotationFixture(revision: 4);
            TabletopPose previousPose = fixture.State.Pose;
            Quaternion previousRotation = fixture.View.transform.rotation;

            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Coordinator.RotateSelected(delta));

            Assert.That(fixture.State.Pose, Is.EqualTo(previousPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(4));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(Quaternion.Angle(previousRotation, fixture.View.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void RotateSelected_WhenObjectIsUserLocked_ReturnsObjectUserLocked()
        {
            RotationFixture fixture = CreateRotationFixture(isUserLocked: true);

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            AssertResult(result, RotationInteractionStatus.ObjectUserLocked, false, false, false);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenUnlocked_AcquiresAndReleasesTemporaryLock()
        {
            RotationFixture fixture = CreateRotationFixture();

            fixture.Coordinator.RotateSelected(15f);

            Assert.That(fixture.LockService.IsLocked(fixture.View.ObjectId), Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenLockConflicts_ReturnsLocalLockConflict()
        {
            RotationFixture fixture = CreateRotationFixture();
            InteractionOwnerId otherOwner = InteractionOwnerId.New();
            fixture.LockService.Acquire(fixture.View.ObjectId, otherOwner);
            TabletopPose previousPose = fixture.State.Pose;

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            AssertResult(result, RotationInteractionStatus.LocalLockConflict, false, false, false);
            Assert.That(fixture.LockService.IsOwnedBy(fixture.View.ObjectId, otherOwner), Is.True);
            Assert.That(fixture.State.Pose, Is.EqualTo(previousPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenLockConflicts_PreservesExistingOwner()
        {
            RotationFixture fixture = CreateRotationFixture();
            InteractionOwnerId otherOwner = InteractionOwnerId.New();
            fixture.LockService.Acquire(fixture.View.ObjectId, otherOwner);

            fixture.Coordinator.RotateSelected(15f);

            Assert.That(fixture.LockService.TryGetOwner(fixture.View.ObjectId, out InteractionOwnerId owner), Is.True);
            Assert.That(owner, Is.EqualTo(otherOwner));
        }

        [Test]
        public void RotateSelected_WhenSameOwnerAlreadyOwnsLock_PermitsRotation()
        {
            RotationFixture fixture = CreateRotationFixture();
            fixture.LockService.Acquire(fixture.View.ObjectId, fixture.OwnerId);

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(30f);

            AssertResult(result, RotationInteractionStatus.RotationAccepted, true, true, true);
            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(30f).Within(Tolerance));
        }

        [Test]
        public void RotateSelected_WhenSameOwnerAlreadyOwnsLock_PreservesLockAfterRotation()
        {
            RotationFixture fixture = CreateRotationFixture();
            fixture.LockService.Acquire(fixture.View.ObjectId, fixture.OwnerId);

            fixture.Coordinator.RotateSelected(30f);

            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
            Assert.That(fixture.LockService.IsOwnedBy(fixture.View.ObjectId, fixture.OwnerId), Is.True);
        }

        [Test]
        public void RotateSelected_WhenExceptionOccursAfterNewLock_ReleasesOnlyNewlyAcquiredLock()
        {
            TabletopPose initialPose = new TabletopPose(TableCoordinate.Zero, float.MaxValue, 0, 0);
            RotationFixture fixture = CreateRotationFixture(initialPose: initialPose);
            TabletopObjectId otherObjectId = new TabletopObjectId(GuidFromSeed(900));
            fixture.LockService.Acquire(otherObjectId, fixture.OwnerId);

            Assert.Throws<OverflowException>(() => fixture.Coordinator.RotateSelected(float.MaxValue));

            Assert.That(fixture.LockService.IsLocked(fixture.View.ObjectId), Is.False);
            Assert.That(fixture.LockService.IsOwnedBy(otherObjectId, fixture.OwnerId), Is.True);
            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
        }

        [Test]
        public void RotateSelected_WhenExceptionOccursWithExistingSameOwnerLock_PreservesThatLock()
        {
            TabletopPose initialPose = new TabletopPose(TableCoordinate.Zero, float.MaxValue, 0, 0);
            RotationFixture fixture = CreateRotationFixture(initialPose: initialPose);
            fixture.LockService.Acquire(fixture.View.ObjectId, fixture.OwnerId);

            Assert.Throws<OverflowException>(() => fixture.Coordinator.RotateSelected(float.MaxValue));

            Assert.That(fixture.LockService.IsOwnedBy(fixture.View.ObjectId, fixture.OwnerId), Is.True);
            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
        }

        [TestCase(TabletopObjectKind.Card)]
        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void RotateSelected_WhenAccepted_RotatesObjectKind(TabletopObjectKind kind)
        {
            RotationFixture fixture = CreateRotationFixture(kind: kind);

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(45f);

            AssertResult(result, RotationInteractionStatus.RotationAccepted, true, true, true);
            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(45f).Within(Tolerance));
        }

        [Test]
        public void RotateSelected_AddsPositiveDeltaToAcceptedRotation()
        {
            RotationFixture fixture = CreateRotationFixture(
                initialPose: new TabletopPose(TableCoordinate.Zero, 10f, 0, 0));

            fixture.Coordinator.RotateSelected(25f);

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(35f).Within(Tolerance));
        }

        [Test]
        public void RotateSelected_AddsNegativeDeltaToAcceptedRotation()
        {
            RotationFixture fixture = CreateRotationFixture(
                initialPose: new TabletopPose(TableCoordinate.Zero, 10f, 0, 0));

            fixture.Coordinator.RotateSelected(-25f);

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(-15f).Within(Tolerance));
        }

        [Test]
        public void RotateSelected_PreservesLargeFiniteDeltaWithoutNormalization()
        {
            RotationFixture fixture = CreateRotationFixture(
                initialPose: new TabletopPose(TableCoordinate.Zero, 1000f, 0, 0));

            fixture.Coordinator.RotateSelected(5000f);

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(6000f).Within(Tolerance));
        }

        [Test]
        public void RotateSelected_WhenAccepted_AdvancesRevisionExactlyOnce()
        {
            RotationFixture fixture = CreateRotationFixture(revision: 12);

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            Assert.That(fixture.Match.Revision, Is.EqualTo(13));
            Assert.That(result.RotateResult.Value.Revision, Is.EqualTo(13));
        }

        [Test]
        public void RotateSelected_WhenAccepted_PerformsExactlyOneAcceptedOperation()
        {
            RotationFixture fixture = CreateRotationFixture(
                revision: 2,
                initialPose: new TabletopPose(TableCoordinate.Zero, 20f, 0, 0));

            fixture.Coordinator.RotateSelected(10f);

            Assert.That(fixture.State.Pose.RotationDegrees, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(3));
        }

        [Test]
        public void RotateSelected_WhenAccepted_ReconcilesViewToAcceptedRotation()
        {
            RotationFixture fixture = CreateRotationFixture(
                initialPose: new TabletopPose(new TableCoordinate(2.0, 3.0), 20f, 1, 2),
                converter: CreateConverter(baseHeight: 0.5f, layerHeight: 0.25f, localOrderHeight: 0.02f));

            fixture.Coordinator.RotateSelected(35f);

            AssertWorldPose(fixture.View, fixture.State.Pose, 0.5f, 0.25f, 0.02f);
        }

        [Test]
        public void RotateSelected_WhenAccepted_PreservesPoseFieldsScaleAndNonPoseState()
        {
            PlayerId ownerPlayerId = new PlayerId(GuidFromSeed(5000));
            TabletopPose initialPose = new TabletopPose(new TableCoordinate(3.0, 4.0), 25f, 2, 3);
            RotationFixture fixture = CreateRotationFixture(
                initialPose: initialPose,
                ownerPlayerId: ownerPlayerId,
                visibility: ObjectVisibility.OwnerOnly,
                converter: CreateConverter(baseHeight: 0.5f, layerHeight: 0.25f, localOrderHeight: 0.02f));
            Vector3 scale = new Vector3(2f, 3f, 4f);
            fixture.View.transform.localScale = scale;

            fixture.Coordinator.RotateSelected(15f);

            Assert.That(fixture.State.Pose.Position, Is.EqualTo(initialPose.Position));
            Assert.That(fixture.State.Pose.Layer, Is.EqualTo(initialPose.Layer));
            Assert.That(fixture.State.Pose.LocalOrder, Is.EqualTo(initialPose.LocalOrder));
            AssertVector3(fixture.View.transform.localScale, scale);
            Assert.That(fixture.State.ContainerId, Is.EqualTo(ContainerId.Empty));
            Assert.That(fixture.State.OwnerPlayerId, Is.EqualTo(ownerPlayerId));
            Assert.That(fixture.State.Visibility, Is.EqualTo(ObjectVisibility.OwnerOnly));
            Assert.That(fixture.State.IsUserLocked, Is.False);
            Assert.That(fixture.State.Id, Is.EqualTo(fixture.View.ObjectId));
            Assert.That(fixture.State.DefinitionId, Is.EqualTo(new ObjectDefinitionId(GuidFromSeed(1001))));
        }

        [Test]
        public void RotateSelected_WhenAccepted_PreservesSelection()
        {
            RotationFixture fixture = CreateRotationFixture();

            fixture.Coordinator.RotateSelected(15f);

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
            Assert.That(fixture.SelectionState.HasSelection, Is.True);
        }

        [Test]
        public void RotateSelected_WhenRejectedByRevisionOverflow_ReturnsRotationRejected()
        {
            RotationFixture fixture = CreateRotationFixture(revision: long.MaxValue);

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            AssertResult(result, RotationInteractionStatus.RotationRejected, true, false, true);
            Assert.That(result.RotateResult.Value.Status, Is.EqualTo(CommandResultStatus.Conflict));
            Assert.That(result.RotateResult.Value.Error, Is.EqualTo(RotateObjectError.RevisionOverflow));
        }

        [Test]
        public void RotateSelected_WhenRejectedByRevisionOverflow_PreservesRuntimeStateAndRevision()
        {
            RotationFixture fixture = CreateRotationFixture(revision: long.MaxValue);
            TabletopPose previousPose = fixture.State.Pose;

            fixture.Coordinator.RotateSelected(15f);

            Assert.That(fixture.State.Pose, Is.EqualTo(previousPose));
            Assert.That(fixture.Match.Revision, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void RotateSelected_WhenRejectedByRevisionOverflow_ReconcilesViewAndReleasesLock()
        {
            RotationFixture fixture = CreateRotationFixture(revision: long.MaxValue);
            fixture.View.transform.SetPositionAndRotation(new Vector3(9f, 9f, 9f), Quaternion.Euler(0f, 99f, 0f));

            fixture.Coordinator.RotateSelected(15f);

            AssertWorldPose(fixture.View, fixture.State.Pose);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void RotateSelected_WhenRejectedByRevisionOverflow_PreservesSelection()
        {
            RotationFixture fixture = CreateRotationFixture(revision: long.MaxValue);

            fixture.Coordinator.RotateSelected(15f);

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
            Assert.That(fixture.SelectionState.HasSelection, Is.True);
        }

        [Test]
        public void RotateSelected_DoesNotRequireInputActionOrPlayerInput()
        {
            RotationFixture fixture = CreateRotationFixture();

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            Assert.That(result.Status, Is.EqualTo(RotationInteractionStatus.RotationAccepted));
        }

        [Test]
        public void RotateSelected_DoesNotRequireCameraOrSurfaceProxy()
        {
            RotationFixture fixture = CreateRotationFixture();

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.View.GetComponent<Camera>(), Is.Null);
        }

        [Test]
        public void RotateSelected_DoesNotRequireSceneOrViewRegistry()
        {
            RotationFixture fixture = CreateRotationFixture();

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(15f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.View));
        }

        [Test]
        public void RotateSelected_DoesNotCreateRotationPreview()
        {
            RotationFixture fixture = CreateRotationFixture();

            fixture.Coordinator.RotateSelected(15f);

            Assert.That(fixture.View.IsPreviewing, Is.False);
            Assert.That(fixture.View.PreviewPose, Is.EqualTo(TabletopPose.Default));
        }

        [TestCase(NoCommandCase.ZeroDelta)]
        [TestCase(NoCommandCase.Conflict)]
        [TestCase(NoCommandCase.MissingSelection)]
        [TestCase(NoCommandCase.UnavailableSelection)]
        [TestCase(NoCommandCase.UserLock)]
        public void RotateSelected_NoCommandCases_DoNotAdvanceRevision(NoCommandCase noCommandCase)
        {
            RotationFixture fixture = CreateNoCommandFixture(noCommandCase);
            long revision = fixture.Match.Revision;

            RotationInteractionResult result = fixture.Coordinator.RotateSelected(
                noCommandCase == NoCommandCase.ZeroDelta ? 0f : 15f);

            Assert.That(result.RotationAttempted, Is.False);
            Assert.That(fixture.Match.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void RotationInteractionResult_FactoriesHaveApprovedValues()
        {
            AssertResult(RotationInteractionResult.NoSelection(), RotationInteractionStatus.NoSelection, false, false, false);
            AssertResult(RotationInteractionResult.SelectionUnavailable(), RotationInteractionStatus.SelectionUnavailable, false, false, false);
            AssertResult(RotationInteractionResult.NoRotationRequested(), RotationInteractionStatus.NoRotationRequested, false, false, false);
            AssertResult(RotationInteractionResult.ObjectUserLocked(), RotationInteractionStatus.ObjectUserLocked, false, false, false);
            AssertResult(RotationInteractionResult.LocalLockConflict(), RotationInteractionStatus.LocalLockConflict, false, false, false);
        }

        [Test]
        public void RotationInteractionResult_FromAcceptedRotate_MapsToRotationAccepted()
        {
            RotateObjectResult rotateResult = RotateObjectResult.Accepted(5);

            RotationInteractionResult result = RotationInteractionResult.FromRotateResult(rotateResult);

            AssertResult(result, RotationInteractionStatus.RotationAccepted, true, true, true);
            Assert.That(result.RotateResult.Value, Is.EqualTo(rotateResult));
        }

        [Test]
        public void RotationInteractionResult_FromFailedRotate_MapsToRotationRejected()
        {
            RotateObjectResult rotateResult = RotateObjectResult.Failure(
                CommandResultStatus.Conflict,
                RotateObjectError.RevisionOverflow);

            RotationInteractionResult result = RotationInteractionResult.FromRotateResult(rotateResult);

            AssertResult(result, RotationInteractionStatus.RotationRejected, true, false, true);
            Assert.That(result.RotateResult.Value, Is.EqualTo(rotateResult));
        }

        [Test]
        public void RotationInteractionResult_EqualityHashCodeOperatorsAndToStringBehaveCorrectly()
        {
            RotationInteractionResult first = RotationInteractionResult.FromRotateResult(RotateObjectResult.Accepted(7));
            RotationInteractionResult second = RotationInteractionResult.FromRotateResult(RotateObjectResult.Accepted(7));
            RotationInteractionResult different = RotationInteractionResult.NoSelection();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ToString(), Does.Contain(nameof(RotationInteractionStatus.RotationAccepted)));
        }

        private RotationFixture CreateNoCommandFixture(NoCommandCase noCommandCase)
        {
            switch (noCommandCase)
            {
                case NoCommandCase.ZeroDelta:
                    return CreateRotationFixture();
                case NoCommandCase.Conflict:
                {
                    RotationFixture fixture = CreateRotationFixture();
                    fixture.LockService.Acquire(fixture.View.ObjectId, InteractionOwnerId.New());
                    return fixture;
                }
                case NoCommandCase.MissingSelection:
                    return CreateRotationFixture(selectView: false);
                case NoCommandCase.UnavailableSelection:
                {
                    RotationFixture fixture = CreateRotationFixture();
                    fixture.View.Unbind();
                    return fixture;
                }
                case NoCommandCase.UserLock:
                    return CreateRotationFixture(isUserLocked: true);
                default:
                    throw new ArgumentOutOfRangeException(nameof(noCommandCase), noCommandCase, "Unsupported no-command case.");
            }
        }

        private RotationFixture CreateRotationFixture(
            TabletopObjectKind kind = TabletopObjectKind.Card,
            long revision = 0,
            bool isUserLocked = false,
            bool selectView = true,
            TabletopPose? initialPose = null,
            ContainerId? containerId = null,
            PlayerId? ownerPlayerId = null,
            ObjectVisibility visibility = ObjectVisibility.Public,
            TabletopCoordinateConverter converter = null)
        {
            TabletopCoordinateConverter actualConverter = converter ?? CreateConverter();
            TabletopObjectView view = CreateBoundView(
                kind,
                1,
                initialPose ?? TabletopPose.Default,
                isUserLocked,
                actualConverter,
                containerId,
                ownerPlayerId,
                visibility,
                out TabletopObjectState state);
            MatchState match = CreateMatch(kind, state, revision);
            TabletopSelectionState selectionState = new TabletopSelectionState();
            if (selectView)
            {
                selectionState.Select(view);
            }

            LocalInteractionLockService lockService = new LocalInteractionLockService();
            RotateObjectUseCase rotateUseCase = new RotateObjectUseCase();
            PlayerId requestedByPlayerId = PlayerId.New();
            InteractionOwnerId ownerId = InteractionOwnerId.New();
            TabletopRotationCoordinator coordinator = new TabletopRotationCoordinator(
                match,
                requestedByPlayerId,
                ownerId,
                selectionState,
                lockService,
                rotateUseCase);

            return new RotationFixture(
                coordinator,
                match,
                view,
                state,
                selectionState,
                lockService,
                rotateUseCase,
                requestedByPlayerId,
                ownerId);
        }

        private ConstructorDependencies CreateConstructorDependencies()
        {
            RotationFixture fixture = CreateRotationFixture();
            return new ConstructorDependencies
            {
                Match = fixture.Match,
                RequestedByPlayerId = fixture.RequestedByPlayerId,
                OwnerId = fixture.OwnerId,
                SelectionState = fixture.SelectionState,
                LockService = fixture.LockService,
                RotateUseCase = fixture.RotateUseCase
            };
        }

        private TabletopObjectView CreateBoundView(
            TabletopObjectKind kind,
            int seed,
            TabletopPose pose,
            bool isUserLocked,
            TabletopCoordinateConverter converter,
            ContainerId? containerId,
            PlayerId? ownerPlayerId,
            ObjectVisibility visibility,
            out TabletopObjectState state)
        {
            switch (kind)
            {
                case TabletopObjectKind.Card:
                {
                    CardView view = CreateView<CardView>();
                    state = CreateBaseState(kind, seed, pose, isUserLocked, containerId, ownerPlayerId, visibility);
                    view.Bind(new CardInstanceState(state, CardFace.FaceUp), converter);
                    return view;
                }

                case TabletopObjectKind.Pawn:
                {
                    PawnView view = CreateView<PawnView>();
                    state = CreateBaseState(kind, seed, pose, isUserLocked, containerId, ownerPlayerId, visibility);
                    view.Bind(new PawnState(state), converter);
                    return view;
                }

                case TabletopObjectKind.Token:
                {
                    TokenView view = CreateView<TokenView>();
                    state = CreateBaseState(kind, seed, pose, isUserLocked, containerId, ownerPlayerId, visibility);
                    view.Bind(new TokenState(state), converter);
                    return view;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported object kind.");
            }
        }

        private T CreateView<T>()
            where T : TabletopObjectView
        {
            GameObject gameObject = new GameObject(typeof(T).Name);
            createdGameObjects.Add(gameObject);
            return gameObject.AddComponent<T>();
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
            bool isUserLocked = false,
            ContainerId? containerId = null,
            PlayerId? ownerPlayerId = null,
            ObjectVisibility visibility = ObjectVisibility.Public)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                pose,
                containerId ?? ContainerId.Empty,
                ownerPlayerId ?? PlayerId.Empty,
                visibility,
                isUserLocked);
        }

        private static TabletopCoordinateConverter CreateConverter(
            float worldUnitsPerTableUnit = 1f,
            float baseHeight = 0f,
            float layerHeight = 0f,
            float localOrderHeight = 0f)
        {
            return new TabletopCoordinateConverter(
                worldUnitsPerTableUnit,
                baseHeight,
                layerHeight,
                localOrderHeight);
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static void AssertResult(
            RotationInteractionResult result,
            RotationInteractionStatus expectedStatus,
            bool expectedAttempted,
            bool expectedSucceeded,
            bool expectRotateResult)
        {
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.RotationAttempted, Is.EqualTo(expectedAttempted));
            Assert.That(result.Succeeded, Is.EqualTo(expectedSucceeded));
            Assert.That(result.RotateResult.HasValue, Is.EqualTo(expectRotateResult));
        }

        private static void AssertWorldPose(
            TabletopObjectView view,
            TabletopPose pose,
            float baseHeight = 0f,
            float layerHeight = 0f,
            float localOrderHeight = 0f)
        {
            float expectedY = baseHeight + (pose.Layer * layerHeight) + (pose.LocalOrder * localOrderHeight);
            AssertVector3(view.transform.position, (float)pose.Position.X, expectedY, (float)pose.Position.Y);
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

        public enum RequiredDependency
        {
            MatchState,
            SelectionState,
            LockService,
            RotateUseCase
        }

        public enum NoCommandCase
        {
            ZeroDelta,
            Conflict,
            MissingSelection,
            UnavailableSelection,
            UserLock
        }

        private sealed class ConstructorDependencies
        {
            public MatchState Match { get; set; }

            public PlayerId RequestedByPlayerId { get; set; }

            public InteractionOwnerId OwnerId { get; set; }

            public TabletopSelectionState SelectionState { get; set; }

            public LocalInteractionLockService LockService { get; set; }

            public RotateObjectUseCase RotateUseCase { get; set; }

            public TabletopRotationCoordinator CreateCoordinator()
            {
                return new TabletopRotationCoordinator(
                    Match,
                    RequestedByPlayerId,
                    OwnerId,
                    SelectionState,
                    LockService,
                    RotateUseCase);
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
                    case RequiredDependency.LockService:
                        LockService = null;
                        break;
                    case RequiredDependency.RotateUseCase:
                        RotateUseCase = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(dependency), dependency, "Unsupported dependency.");
                }
            }
        }

        private sealed class RotationFixture
        {
            public RotationFixture(
                TabletopRotationCoordinator coordinator,
                MatchState match,
                TabletopObjectView view,
                TabletopObjectState state,
                TabletopSelectionState selectionState,
                LocalInteractionLockService lockService,
                RotateObjectUseCase rotateUseCase,
                PlayerId requestedByPlayerId,
                InteractionOwnerId ownerId)
            {
                Coordinator = coordinator;
                Match = match;
                View = view;
                State = state;
                SelectionState = selectionState;
                LockService = lockService;
                RotateUseCase = rotateUseCase;
                RequestedByPlayerId = requestedByPlayerId;
                OwnerId = ownerId;
            }

            public TabletopRotationCoordinator Coordinator { get; }

            public MatchState Match { get; }

            public TabletopObjectView View { get; }

            public TabletopObjectState State { get; }

            public TabletopSelectionState SelectionState { get; }

            public LocalInteractionLockService LockService { get; }

            public RotateObjectUseCase RotateUseCase { get; }

            public PlayerId RequestedByPlayerId { get; }

            public InteractionOwnerId OwnerId { get; }
        }
    }
}
