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
    public sealed class TabletopCardFlipCoordinatorTests
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
            FlipCoordinatorFixture fixture = CreateCardFixture();

            Assert.That(fixture.Coordinator.MatchState, Is.SameAs(fixture.Match));
            Assert.That(fixture.Coordinator.RequestedByPlayerId, Is.EqualTo(fixture.RequestedByPlayerId));
            Assert.That(fixture.Coordinator.InteractionOwnerId, Is.EqualTo(fixture.OwnerId));
            Assert.That(fixture.Coordinator.SelectionState, Is.SameAs(fixture.SelectionState));
            Assert.That(fixture.Coordinator.LockService, Is.SameAs(fixture.LockService));
            Assert.That(fixture.Coordinator.FlipUseCase, Is.SameAs(fixture.FlipUseCase));
        }

        [TestCase(RequiredDependency.MatchState)]
        [TestCase(RequiredDependency.SelectionState)]
        [TestCase(RequiredDependency.LockService)]
        [TestCase(RequiredDependency.FlipUseCase)]
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
        public void FlipSelected_WhenNoSelection_ReturnsNoSelection()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(selectView: false);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.NoSelection, false, false, false);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenSelectedViewIsDestroyed_ReturnsSelectionUnavailable()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            UnityObject.DestroyImmediate(fixture.SelectedView.gameObject);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.SelectionUnavailable, false, false, false);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenSelectedViewIsUnbound_ReturnsSelectionUnavailable()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            fixture.SelectedView.Unbind();

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.SelectionUnavailable, false, false, false);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenSelectedViewIsDisabled_ReturnsSelectionUnavailable()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            fixture.SelectedView.enabled = false;

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.SelectionUnavailable, false, false, false);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenSelectedViewIsInactive_ReturnsSelectionUnavailable()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            fixture.SelectedView.gameObject.SetActive(false);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.SelectionUnavailable, false, false, false);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenSelectedViewIsPawn_ReturnsSelectionNotCard()
        {
            FlipCoordinatorFixture fixture = CreateObjectFixture(TabletopObjectKind.Pawn);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.SelectionNotCard, false, false, false);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.SelectedView));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenSelectedViewIsToken_ReturnsSelectionNotCard()
        {
            FlipCoordinatorFixture fixture = CreateObjectFixture(TabletopObjectKind.Token);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.SelectionNotCard, false, false, false);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.SelectedView));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void FlipSelected_WhenSelectedViewIsNotCard_PreservesSelection(TabletopObjectKind kind)
        {
            FlipCoordinatorFixture fixture = CreateObjectFixture(kind);

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.SelectedView));
            Assert.That(fixture.SelectionState.HasSelection, Is.True);
        }

        [Test]
        public void FlipSelected_WhenSelectedCardIsPreviewing_ThrowsWithoutMutation()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            CardFace faceBefore = fixture.Card.Face;
            fixture.CardView.ApplyPreviewPose(new TabletopPose(new TableCoordinate(8.0, 9.0), 90f, 1, 2));
            Vector3 previewPosition = fixture.CardView.transform.position;
            Quaternion previewRotation = fixture.CardView.transform.rotation;

            Assert.Throws<InvalidOperationException>(() => fixture.Coordinator.FlipSelected());

            Assert.That(fixture.Card.Face, Is.EqualTo(faceBefore));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.CardView));
            AssertVector3(fixture.CardView.transform.position, previewPosition);
            Assert.That(Quaternion.Angle(previewRotation, fixture.CardView.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void FlipSelected_WhenCardIsUserLocked_ReturnsObjectUserLocked()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(isUserLocked: true);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.ObjectUserLocked, false, false, false);
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenCardIsUserLocked_DoesNotAcquireTemporaryLock()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(isUserLocked: true);

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenUnlocked_AcquiresAndReleasesTemporaryLock()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.LockService.IsLocked(fixture.CardView.ObjectId), Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenLockConflicts_ReturnsLocalLockConflict()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            InteractionOwnerId otherOwner = InteractionOwnerId.New();
            fixture.LockService.Acquire(fixture.CardView.ObjectId, otherOwner);
            CardFace faceBefore = fixture.Card.Face;

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.LocalLockConflict, false, false, false);
            Assert.That(fixture.Card.Face, Is.EqualTo(faceBefore));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenLockConflicts_PreservesCurrentOwner()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            InteractionOwnerId otherOwner = InteractionOwnerId.New();
            fixture.LockService.Acquire(fixture.CardView.ObjectId, otherOwner);

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.LockService.TryGetOwner(fixture.CardView.ObjectId, out InteractionOwnerId owner), Is.True);
            Assert.That(owner, Is.EqualTo(otherOwner));
        }

        [Test]
        public void FlipSelected_WhenSameOwnerAlreadyOwnsLock_PermitsFlipping()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(initialFace: CardFace.FaceUp);
            fixture.LockService.Acquire(fixture.CardView.ObjectId, fixture.OwnerId);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.FlipAccepted, true, true, true);
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void FlipSelected_WhenSameOwnerAlreadyOwnsLock_PreservesLockAfterFlipping()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            fixture.LockService.Acquire(fixture.CardView.ObjectId, fixture.OwnerId);

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
            Assert.That(fixture.LockService.IsOwnedBy(fixture.CardView.ObjectId, fixture.OwnerId), Is.True);
        }

        [Test]
        public void FlipSelected_WhenExceptionOccursAfterNewLock_ReleasesOnlyNewlyAcquiredLock()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(initialFace: (CardFace)99);
            TabletopObjectId otherObjectId = new TabletopObjectId(GuidFromSeed(900));
            fixture.LockService.Acquire(otherObjectId, fixture.OwnerId);

            Assert.Throws<InvalidOperationException>(() => fixture.Coordinator.FlipSelected());

            Assert.That(fixture.LockService.IsLocked(fixture.CardView.ObjectId), Is.False);
            Assert.That(fixture.LockService.IsOwnedBy(otherObjectId, fixture.OwnerId), Is.True);
            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.CardView));
        }

        [Test]
        public void FlipSelected_WhenExceptionOccursWithExistingSameOwnerLock_PreservesThatLock()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(initialFace: (CardFace)99);
            fixture.LockService.Acquire(fixture.CardView.ObjectId, fixture.OwnerId);

            Assert.Throws<InvalidOperationException>(() => fixture.Coordinator.FlipSelected());

            Assert.That(fixture.LockService.IsOwnedBy(fixture.CardView.ObjectId, fixture.OwnerId), Is.True);
            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
        }

        [Test]
        public void FlipSelected_WhenCardFaceIsUnsupported_ThrowsWithoutMutation()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(initialFace: (CardFace)99);
            CardFace faceBefore = fixture.Card.Face;
            TabletopPose poseBefore = fixture.Card.BaseState.Pose;

            Assert.Throws<InvalidOperationException>(() => fixture.Coordinator.FlipSelected());

            Assert.That(fixture.Card.Face, Is.EqualTo(faceBefore));
            Assert.That(fixture.Card.BaseState.Pose, Is.EqualTo(poseBefore));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenFaceUp_FlipsToFaceDown()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(initialFace: CardFace.FaceUp);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.FlipAccepted, true, true, true);
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void FlipSelected_WhenFaceDown_FlipsToFaceUp()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(initialFace: CardFace.FaceDown);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.FlipAccepted, true, true, true);
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceUp));
        }

        [Test]
        public void FlipSelected_WhenAccepted_AdvancesRevisionExactlyOnce()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(revision: 12);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            Assert.That(fixture.Match.Revision, Is.EqualTo(13));
            Assert.That(result.FlipResult.Value.Revision, Is.EqualTo(13));
        }

        [Test]
        public void FlipSelected_WhenAccepted_PerformsExactlyOneFlipOperation()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(revision: 2, initialFace: CardFace.FaceUp);

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.Match.Revision, Is.EqualTo(3));
        }

        [Test]
        public void FlipSelected_WhenAccepted_CardViewExposesUpdatedAuthoritativeFace()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(initialFace: CardFace.FaceDown);

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.CardView.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.CardView.CardState, Is.SameAs(fixture.Card));
        }

        [Test]
        public void FlipSelected_WhenAccepted_PoseAndViewTransformRemainUnchanged()
        {
            TabletopPose pose = new TabletopPose(new TableCoordinate(3.0, -4.0), 25f, 2, 3);
            TabletopCoordinateConverter converter = CreateConverter(baseHeight: 0.5f, layerHeight: 0.25f, localOrderHeight: 0.02f);
            FlipCoordinatorFixture fixture = CreateCardFixture(initialPose: pose, converter: converter);
            Vector3 positionBefore = fixture.CardView.transform.position;
            Quaternion rotationBefore = fixture.CardView.transform.rotation;

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.Card.BaseState.Pose, Is.EqualTo(pose));
            AssertVector3(fixture.CardView.transform.position, positionBefore);
            Assert.That(Quaternion.Angle(rotationBefore, fixture.CardView.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void FlipSelected_WhenAccepted_TransformScaleRemainsUnchanged()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            Vector3 scale = new Vector3(2f, 3f, 4f);
            fixture.CardView.transform.localScale = scale;

            fixture.Coordinator.FlipSelected();

            AssertVector3(fixture.CardView.transform.localScale, scale);
        }

        [Test]
        public void FlipSelected_WhenAccepted_PreservesNonFaceStateAndOtherObjects()
        {
            PlayerId ownerPlayerId = new PlayerId(GuidFromSeed(5000));
            FlipCoordinatorFixture fixture = CreateCardFixture(
                includeOtherObjects: true,
                ownerPlayerId: ownerPlayerId,
                visibility: ObjectVisibility.OwnerOnly);
            ObjectSnapshot targetBefore = ObjectSnapshot.Capture(fixture.Card.BaseState);
            CardFace otherCardFaceBefore = fixture.OtherCard.Face;
            ObjectSnapshot otherCardBefore = ObjectSnapshot.Capture(fixture.OtherCard.BaseState);
            ObjectSnapshot pawnBefore = ObjectSnapshot.Capture(fixture.Pawn.BaseState);
            ObjectSnapshot tokenBefore = ObjectSnapshot.Capture(fixture.Token.BaseState);

            fixture.Coordinator.FlipSelected();

            targetBefore.AssertMatches(fixture.Card.BaseState);
            Assert.That(fixture.OtherCard.Face, Is.EqualTo(otherCardFaceBefore));
            otherCardBefore.AssertMatches(fixture.OtherCard.BaseState);
            pawnBefore.AssertMatches(fixture.Pawn.BaseState);
            tokenBefore.AssertMatches(fixture.Token.BaseState);
        }

        [Test]
        public void FlipSelected_WhenAccepted_PreservesSelection()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.CardView));
            Assert.That(fixture.SelectionState.HasSelection, Is.True);
        }

        [Test]
        public void FlipSelected_WhenRejectedByRevisionOverflow_ReturnsFlipRejected()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(revision: long.MaxValue);

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            AssertResult(result, FlipInteractionStatus.FlipRejected, true, false, true);
            Assert.That(result.FlipResult.Value.Status, Is.EqualTo(CommandResultStatus.Conflict));
            Assert.That(result.FlipResult.Value.Error, Is.EqualTo(FlipCardError.RevisionOverflow));
        }

        [Test]
        public void FlipSelected_WhenRejectedByRevisionOverflow_PreservesCardFaceAndRevision()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(revision: long.MaxValue);
            CardFace faceBefore = fixture.Card.Face;

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.Card.Face, Is.EqualTo(faceBefore));
            Assert.That(fixture.Match.Revision, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void FlipSelected_WhenRejectedByRevisionOverflow_ReconcilesViewAndReleasesNewLock()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(revision: long.MaxValue);
            fixture.CardView.transform.SetPositionAndRotation(new Vector3(9f, 9f, 9f), Quaternion.Euler(0f, 99f, 0f));

            fixture.Coordinator.FlipSelected();

            AssertWorldPose(fixture.CardView, fixture.Card.BaseState.Pose);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenRejectedWithExistingSameOwnerLock_PreservesThatLock()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(revision: long.MaxValue);
            fixture.LockService.Acquire(fixture.CardView.ObjectId, fixture.OwnerId);

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.LockService.Count, Is.EqualTo(1));
            Assert.That(fixture.LockService.IsOwnedBy(fixture.CardView.ObjectId, fixture.OwnerId), Is.True);
        }

        [Test]
        public void FlipSelected_WhenRejectedByRevisionOverflow_PreservesSelection()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(revision: long.MaxValue);

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.CardView));
            Assert.That(fixture.SelectionState.HasSelection, Is.True);
        }

        [Test]
        public void FlipSelected_DoesNotRequireInputActionOrPlayerInput()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            Assert.That(result.Status, Is.EqualTo(FlipInteractionStatus.FlipAccepted));
        }

        [Test]
        public void FlipSelected_DoesNotRequireCameraOrSurfaceProxy()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.CardView.GetComponent<Camera>(), Is.Null);
        }

        [Test]
        public void FlipSelected_DoesNotRequireRendererMaterialOrFaceAnimation()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.CardView.GetComponent<Renderer>(), Is.Null);
            Assert.That(fixture.CardView.GetComponent<Animator>(), Is.Null);
        }

        [Test]
        public void FlipSelected_DoesNotRequireSceneOrViewRegistry()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.CardView));
        }

        [Test]
        public void FlipSelected_DoesNotTreatRendererStateAsAuthoritativeFaceState()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture(initialFace: CardFace.FaceUp);

            fixture.Coordinator.FlipSelected();

            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.CardView.GetComponent<Renderer>(), Is.Null);
        }

        [TestCase(NoCommandCase.MissingSelection)]
        [TestCase(NoCommandCase.UnavailableSelection)]
        [TestCase(NoCommandCase.NonCardSelection)]
        [TestCase(NoCommandCase.UserLock)]
        [TestCase(NoCommandCase.Conflict)]
        public void FlipSelected_NoCommandCases_DoNotAdvanceRevision(NoCommandCase noCommandCase)
        {
            FlipCoordinatorFixture fixture = CreateNoCommandFixture(noCommandCase);
            long revision = fixture.Match.Revision;

            FlipInteractionResult result = fixture.Coordinator.FlipSelected();

            Assert.That(result.FlipAttempted, Is.False);
            Assert.That(fixture.Match.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void FlipInteractionResult_FactoriesHaveApprovedValues()
        {
            AssertResult(FlipInteractionResult.NoSelection(), FlipInteractionStatus.NoSelection, false, false, false);
            AssertResult(FlipInteractionResult.SelectionUnavailable(), FlipInteractionStatus.SelectionUnavailable, false, false, false);
            AssertResult(FlipInteractionResult.SelectionNotCard(), FlipInteractionStatus.SelectionNotCard, false, false, false);
            AssertResult(FlipInteractionResult.ObjectUserLocked(), FlipInteractionStatus.ObjectUserLocked, false, false, false);
            AssertResult(FlipInteractionResult.LocalLockConflict(), FlipInteractionStatus.LocalLockConflict, false, false, false);
        }

        [Test]
        public void FlipInteractionResult_FromAcceptedFlip_MapsToFlipAccepted()
        {
            FlipCardResult flipResult = FlipCardResult.Accepted(5);

            FlipInteractionResult result = FlipInteractionResult.FromFlipResult(flipResult);

            AssertResult(result, FlipInteractionStatus.FlipAccepted, true, true, true);
            Assert.That(result.FlipResult.Value, Is.EqualTo(flipResult));
        }

        [Test]
        public void FlipInteractionResult_FromFailedFlip_MapsToFlipRejected()
        {
            FlipCardResult flipResult = FlipCardResult.Failure(
                CommandResultStatus.Conflict,
                FlipCardError.RevisionOverflow);

            FlipInteractionResult result = FlipInteractionResult.FromFlipResult(flipResult);

            AssertResult(result, FlipInteractionStatus.FlipRejected, true, false, true);
            Assert.That(result.FlipResult.Value, Is.EqualTo(flipResult));
        }

        [Test]
        public void FlipInteractionResult_EqualityHashCodeOperatorsAndToStringBehaveCorrectly()
        {
            FlipInteractionResult first = FlipInteractionResult.FromFlipResult(FlipCardResult.Accepted(7));
            FlipInteractionResult second = FlipInteractionResult.FromFlipResult(FlipCardResult.Accepted(7));
            FlipInteractionResult different = FlipInteractionResult.NoSelection();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != different, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ToString(), Does.Contain(nameof(FlipInteractionStatus.FlipAccepted)));
        }

        private FlipCoordinatorFixture CreateNoCommandFixture(NoCommandCase noCommandCase)
        {
            switch (noCommandCase)
            {
                case NoCommandCase.MissingSelection:
                    return CreateCardFixture(selectView: false);
                case NoCommandCase.UnavailableSelection:
                {
                    FlipCoordinatorFixture fixture = CreateCardFixture();
                    fixture.CardView.Unbind();
                    return fixture;
                }
                case NoCommandCase.NonCardSelection:
                    return CreateObjectFixture(TabletopObjectKind.Pawn);
                case NoCommandCase.UserLock:
                    return CreateCardFixture(isUserLocked: true);
                case NoCommandCase.Conflict:
                {
                    FlipCoordinatorFixture fixture = CreateCardFixture();
                    fixture.LockService.Acquire(fixture.CardView.ObjectId, InteractionOwnerId.New());
                    return fixture;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(noCommandCase), noCommandCase, "Unsupported no-command case.");
            }
        }

        private FlipCoordinatorFixture CreateCardFixture(
            long revision = 0,
            bool isUserLocked = false,
            bool selectView = true,
            CardFace initialFace = CardFace.FaceUp,
            TabletopPose? initialPose = null,
            PlayerId? ownerPlayerId = null,
            ObjectVisibility visibility = ObjectVisibility.Public,
            bool includeOtherObjects = false,
            TabletopCoordinateConverter converter = null)
        {
            TabletopCoordinateConverter actualConverter = converter ?? CreateConverter();
            CardView cardView = CreateView<CardView>();
            TabletopObjectState cardState = CreateObjectState(
                TabletopObjectKind.Card,
                1,
                initialPose ?? CreatePose(rotationDegrees: 30f),
                isUserLocked: isUserLocked,
                ownerPlayerId: ownerPlayerId,
                visibility: visibility);
            CardInstanceState card = new CardInstanceState(cardState, initialFace);
            cardView.Bind(card, actualConverter);

            CardInstanceState[] cards = includeOtherObjects
                ? new[] { card, CreateCard(2, CardFace.FaceDown) }
                : new[] { card };
            PawnState pawn = includeOtherObjects ? CreatePawn(3) : null;
            TokenState token = includeOtherObjects ? CreateToken(4) : null;
            MatchState match = CreateMatch(
                revision,
                cards,
                includeOtherObjects ? new[] { pawn } : null,
                includeOtherObjects ? new[] { token } : null);
            TabletopSelectionState selectionState = new TabletopSelectionState();
            if (selectView)
            {
                selectionState.Select(cardView);
            }

            LocalInteractionLockService lockService = new LocalInteractionLockService();
            FlipCardUseCase flipUseCase = new FlipCardUseCase();
            PlayerId requestedByPlayerId = PlayerId.New();
            InteractionOwnerId ownerId = InteractionOwnerId.New();
            TabletopCardFlipCoordinator coordinator = new TabletopCardFlipCoordinator(
                match,
                requestedByPlayerId,
                ownerId,
                selectionState,
                lockService,
                flipUseCase);

            return new FlipCoordinatorFixture(
                coordinator,
                match,
                cardView,
                card,
                card.BaseState,
                selectionState,
                lockService,
                flipUseCase,
                requestedByPlayerId,
                ownerId,
                cardView,
                includeOtherObjects ? cards[1] : null,
                pawn,
                token);
        }

        private FlipCoordinatorFixture CreateObjectFixture(TabletopObjectKind selectedKind)
        {
            TabletopCoordinateConverter converter = CreateConverter();
            TabletopObjectView selectedView;
            TabletopObjectState selectedState;
            CardInstanceState card = CreateCard(1, CardFace.FaceUp);
            PawnState pawn = selectedKind == TabletopObjectKind.Pawn
                ? CreatePawn(2)
                : null;
            TokenState token = selectedKind == TabletopObjectKind.Token
                ? CreateToken(3)
                : null;

            switch (selectedKind)
            {
                case TabletopObjectKind.Pawn:
                {
                    PawnView view = CreateView<PawnView>();
                    view.Bind(pawn, converter);
                    selectedView = view;
                    selectedState = pawn.BaseState;
                    break;
                }

                case TabletopObjectKind.Token:
                {
                    TokenView view = CreateView<TokenView>();
                    view.Bind(token, converter);
                    selectedView = view;
                    selectedState = token.BaseState;
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(selectedKind), selectedKind, "Unsupported selected object kind.");
            }

            MatchState match = CreateMatch(
                0,
                new[] { card },
                pawn == null ? null : new[] { pawn },
                token == null ? null : new[] { token });
            TabletopSelectionState selectionState = new TabletopSelectionState();
            selectionState.Select(selectedView);
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            FlipCardUseCase flipUseCase = new FlipCardUseCase();
            PlayerId requestedByPlayerId = PlayerId.New();
            InteractionOwnerId ownerId = InteractionOwnerId.New();
            TabletopCardFlipCoordinator coordinator = new TabletopCardFlipCoordinator(
                match,
                requestedByPlayerId,
                ownerId,
                selectionState,
                lockService,
                flipUseCase);

            return new FlipCoordinatorFixture(
                coordinator,
                match,
                null,
                card,
                selectedState,
                selectionState,
                lockService,
                flipUseCase,
                requestedByPlayerId,
                ownerId,
                selectedView);
        }

        private ConstructorDependencies CreateConstructorDependencies()
        {
            FlipCoordinatorFixture fixture = CreateCardFixture();
            return new ConstructorDependencies
            {
                Match = fixture.Match,
                RequestedByPlayerId = fixture.RequestedByPlayerId,
                OwnerId = fixture.OwnerId,
                SelectionState = fixture.SelectionState,
                LockService = fixture.LockService,
                FlipUseCase = fixture.FlipUseCase
            };
        }

        private T CreateView<T>()
            where T : TabletopObjectView
        {
            GameObject gameObject = new GameObject(typeof(T).Name);
            createdGameObjects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private static MatchState CreateMatch(
            long revision,
            CardInstanceState[] cards,
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

        private static CardInstanceState CreateCard(int seed, CardFace face)
        {
            return new CardInstanceState(
                CreateObjectState(TabletopObjectKind.Card, seed, CreatePose(seed, -seed, seed * 10f)),
                face);
        }

        private static PawnState CreatePawn(int seed)
        {
            return new PawnState(
                CreateObjectState(TabletopObjectKind.Pawn, seed, CreatePose(seed, seed + 0.5, seed * 15f)));
        }

        private static TokenState CreateToken(int seed)
        {
            return new TokenState(
                CreateObjectState(TabletopObjectKind.Token, seed, CreatePose(-seed, seed + 0.25, seed * -10f)));
        }

        private static TabletopObjectState CreateObjectState(
            TabletopObjectKind kind,
            int seed,
            TabletopPose pose,
            bool isUserLocked = false,
            PlayerId? ownerPlayerId = null,
            ObjectVisibility visibility = ObjectVisibility.Public)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                pose,
                ContainerId.Empty,
                ownerPlayerId ?? PlayerId.Empty,
                visibility,
                isUserLocked);
        }

        private static TabletopPose CreatePose(
            double x = 1.0,
            double y = 2.0,
            float rotationDegrees = 30f,
            int layer = 0,
            int localOrder = 0)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, layer, localOrder);
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
            FlipInteractionResult result,
            FlipInteractionStatus expectedStatus,
            bool expectedAttempted,
            bool expectedSucceeded,
            bool expectFlipResult)
        {
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.FlipAttempted, Is.EqualTo(expectedAttempted));
            Assert.That(result.Succeeded, Is.EqualTo(expectedSucceeded));
            Assert.That(result.FlipResult.HasValue, Is.EqualTo(expectFlipResult));
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
            FlipUseCase
        }

        public enum NoCommandCase
        {
            MissingSelection,
            UnavailableSelection,
            NonCardSelection,
            UserLock,
            Conflict
        }

        private sealed class ConstructorDependencies
        {
            public MatchState Match { get; set; }

            public PlayerId RequestedByPlayerId { get; set; }

            public InteractionOwnerId OwnerId { get; set; }

            public TabletopSelectionState SelectionState { get; set; }

            public LocalInteractionLockService LockService { get; set; }

            public FlipCardUseCase FlipUseCase { get; set; }

            public TabletopCardFlipCoordinator CreateCoordinator()
            {
                return new TabletopCardFlipCoordinator(
                    Match,
                    RequestedByPlayerId,
                    OwnerId,
                    SelectionState,
                    LockService,
                    FlipUseCase);
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
                    case RequiredDependency.FlipUseCase:
                        FlipUseCase = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(dependency), dependency, "Unsupported dependency.");
                }
            }
        }

        private sealed class FlipCoordinatorFixture
        {
            public FlipCoordinatorFixture(
                TabletopCardFlipCoordinator coordinator,
                MatchState match,
                CardView cardView,
                CardInstanceState card,
                TabletopObjectState selectedState,
                TabletopSelectionState selectionState,
                LocalInteractionLockService lockService,
                FlipCardUseCase flipUseCase,
                PlayerId requestedByPlayerId,
                InteractionOwnerId ownerId,
                TabletopObjectView selectedView,
                CardInstanceState otherCard = null,
                PawnState pawn = null,
                TokenState token = null)
            {
                Coordinator = coordinator;
                Match = match;
                CardView = cardView;
                Card = card;
                SelectedState = selectedState;
                SelectionState = selectionState;
                LockService = lockService;
                FlipUseCase = flipUseCase;
                RequestedByPlayerId = requestedByPlayerId;
                OwnerId = ownerId;
                SelectedView = selectedView;
                OtherCard = otherCard;
                Pawn = pawn;
                Token = token;
            }

            public TabletopCardFlipCoordinator Coordinator { get; }

            public MatchState Match { get; }

            public CardView CardView { get; }

            public CardInstanceState Card { get; }

            public TabletopObjectState SelectedState { get; }

            public TabletopSelectionState SelectionState { get; }

            public LocalInteractionLockService LockService { get; }

            public FlipCardUseCase FlipUseCase { get; }

            public PlayerId RequestedByPlayerId { get; }

            public InteractionOwnerId OwnerId { get; }

            public TabletopObjectView SelectedView { get; }

            public CardInstanceState OtherCard { get; }

            public PawnState Pawn { get; }

            public TokenState Token { get; }
        }

        private sealed class ObjectSnapshot
        {
            private ObjectSnapshot(
                TabletopObjectId id,
                ObjectDefinitionId definitionId,
                TabletopPose pose,
                ContainerId containerId,
                PlayerId ownerPlayerId,
                ObjectVisibility visibility,
                bool isUserLocked)
            {
                Id = id;
                DefinitionId = definitionId;
                Pose = pose;
                ContainerId = containerId;
                OwnerPlayerId = ownerPlayerId;
                Visibility = visibility;
                IsUserLocked = isUserLocked;
            }

            public TabletopObjectId Id { get; }

            public ObjectDefinitionId DefinitionId { get; }

            public TabletopPose Pose { get; }

            public ContainerId ContainerId { get; }

            public PlayerId OwnerPlayerId { get; }

            public ObjectVisibility Visibility { get; }

            public bool IsUserLocked { get; }

            public static ObjectSnapshot Capture(TabletopObjectState state)
            {
                return new ObjectSnapshot(
                    state.Id,
                    state.DefinitionId,
                    state.Pose,
                    state.ContainerId,
                    state.OwnerPlayerId,
                    state.Visibility,
                    state.IsUserLocked);
            }

            public void AssertMatches(TabletopObjectState state)
            {
                Assert.That(state.Id, Is.EqualTo(Id));
                Assert.That(state.DefinitionId, Is.EqualTo(DefinitionId));
                Assert.That(state.Pose, Is.EqualTo(Pose));
                Assert.That(state.ContainerId, Is.EqualTo(ContainerId));
                Assert.That(state.OwnerPlayerId, Is.EqualTo(OwnerPlayerId));
                Assert.That(state.Visibility, Is.EqualTo(Visibility));
                Assert.That(state.IsUserLocked, Is.EqualTo(IsUserLocked));
            }
        }
    }
}
