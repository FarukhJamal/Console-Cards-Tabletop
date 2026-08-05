using System;
using System.Collections.Generic;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Prototype;
using ConsoleCards.Presentation.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopPrototypeCompositionTests
    {
        private const int InteractionLayer = 8;
        private const float FloatTolerance = 0.0001f;
        private const double CoordinateTolerance = 0.00001d;
        private const float DeltaTime = 1f;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();
        private readonly List<InputActionAsset> createdInputAssets = new List<InputActionAsset>();
        private readonly List<InputActionReference> createdActionReferences = new List<InputActionReference>();

        [TearDown]
        public void TearDown()
        {
            ShutdownRuntimeComponents();

            for (int i = 0; i < createdInputAssets.Count; i++)
            {
                if (createdInputAssets[i] != null)
                {
                    createdInputAssets[i].Disable();
                }
            }

            for (int i = 0; i < createdActionReferences.Count; i++)
            {
                if (createdActionReferences[i] != null)
                {
                    UnityObject.DestroyImmediate(createdActionReferences[i]);
                }
            }

            for (int i = 0; i < createdInputAssets.Count; i++)
            {
                if (createdInputAssets[i] != null)
                {
                    UnityObject.DestroyImmediate(createdInputAssets[i]);
                }
            }

            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                if (createdGameObjects[i] != null)
                {
                    UnityObject.DestroyImmediate(createdGameObjects[i]);
                }
            }

            createdActionReferences.Clear();
            createdInputAssets.Clear();
            createdGameObjects.Clear();
        }

        [Test]
        public void NewComposition_BeforeInitialize_StartsUninitialized()
        {
            PrototypeFixture fixture = CreateFixture();

            Assert.That(fixture.Composition.IsInitialized, Is.False);
            Assert.That(fixture.Composition.MatchState, Is.Null);
            Assert.That(fixture.Composition.LocalPlayerId, Is.EqualTo(PlayerId.Empty));
        }

        [Test]
        public void Initialize_WithValidConfiguration_ComposesRuntimeGraph()
        {
            PrototypeFixture fixture = CreateInitializedFixture();

            Assert.That(fixture.Composition.IsInitialized, Is.True);
            Assert.That(fixture.Composition.MatchState, Is.Not.Null);
            Assert.That(fixture.Composition.LocalPlayerId.IsEmpty, Is.False);
            Assert.That(fixture.Composition.CardState, Is.Not.Null);
            Assert.That(fixture.Composition.PawnState, Is.Not.Null);
            Assert.That(fixture.Composition.TokenState, Is.Not.Null);
            Assert.That(fixture.Composition.CoordinateConverter, Is.Not.Null);
            Assert.That(fixture.Composition.SelectionState, Is.Not.Null);
            Assert.That(fixture.Composition.HitResolver, Is.Not.Null);
            Assert.That(fixture.Composition.PointerProjector, Is.Not.Null);
            Assert.That(fixture.Composition.LockService, Is.Not.Null);
            Assert.That(fixture.Composition.InteractionStateMachine, Is.Not.Null);
            Assert.That(fixture.Composition.PreviewSession, Is.Not.Null);
            Assert.That(fixture.Composition.MoveCoordinator, Is.Not.Null);
            Assert.That(fixture.Composition.RotationCoordinator, Is.Not.Null);
            Assert.That(fixture.Composition.FlipCoordinator, Is.Not.Null);
            Assert.That(fixture.Composition.InputRoutingPolicy, Is.Not.Null);
            Assert.That(fixture.Composition.SelectionPresenter, Is.Not.Null);
        }

        [TestCase(MissingReference.TargetCamera)]
        [TestCase(MissingReference.CameraInputAdapter)]
        [TestCase(MissingReference.ObjectInputAdapter)]
        [TestCase(MissingReference.InputFrameCoordinator)]
        [TestCase(MissingReference.CardView)]
        [TestCase(MissingReference.PawnView)]
        [TestCase(MissingReference.TokenView)]
        [TestCase(MissingReference.CardSelectionVisual)]
        [TestCase(MissingReference.CardHighlightRoot)]
        [TestCase(MissingReference.PawnSelectionVisual)]
        [TestCase(MissingReference.PawnHighlightRoot)]
        [TestCase(MissingReference.TokenSelectionVisual)]
        [TestCase(MissingReference.TokenHighlightRoot)]
        public void Initialize_WhenRequiredReferenceIsMissing_RejectsConfiguration(MissingReference missingReference)
        {
            PrototypeFixture fixture = CreateFixture();
            fixture.RemoveReference(missingReference);

            Assert.Throws<InvalidOperationException>(() => fixture.Composition.Initialize());
            fixture.AssertFailedInitializationCleanup();
            fixture.AssertCompositionAssignedValidHighlightsInactive();
        }

        [TestCase(InvalidConfiguration.PerspectiveCamera)]
        [TestCase(InvalidConfiguration.ZeroMaximumHitDistance)]
        [TestCase(InvalidConfiguration.NaNMaximumHitDistance)]
        [TestCase(InvalidConfiguration.NegativeDragThreshold)]
        [TestCase(InvalidConfiguration.NaNDragThreshold)]
        [TestCase(InvalidConfiguration.ZeroWorldScale)]
        [TestCase(InvalidConfiguration.NaNWorldScale)]
        [TestCase(InvalidConfiguration.NaNTabletopHeight)]
        [TestCase(InvalidConfiguration.DuplicateCardPawnViews)]
        [TestCase(InvalidConfiguration.DuplicateCardTokenViews)]
        [TestCase(InvalidConfiguration.DuplicatePawnTokenViews)]
        [TestCase(InvalidConfiguration.PreBoundCardView)]
        [TestCase(InvalidConfiguration.PreBoundPawnView)]
        [TestCase(InvalidConfiguration.PreBoundTokenView)]
        [TestCase(InvalidConfiguration.PreInitializedObjectAdapter)]
        [TestCase(InvalidConfiguration.ExistingCameraScrollPolicy)]
        [TestCase(InvalidConfiguration.EnabledFrameCoordinator)]
        [TestCase(InvalidConfiguration.ExistingExternalFrameDriver)]
        [TestCase(InvalidConfiguration.CoordinatorReferencesDifferentAdapters)]
        [TestCase(InvalidConfiguration.CardVisualOnWrongView)]
        [TestCase(InvalidConfiguration.PawnVisualOnWrongView)]
        [TestCase(InvalidConfiguration.TokenVisualOnWrongView)]
        [TestCase(InvalidConfiguration.InvalidCardHighlightRoot)]
        [TestCase(InvalidConfiguration.InvalidPawnHighlightRoot)]
        [TestCase(InvalidConfiguration.InvalidTokenHighlightRoot)]
        [TestCase(InvalidConfiguration.DuplicateSelectionVisual)]
        [TestCase(InvalidConfiguration.DuplicateHighlightRoot)]
        [TestCase(InvalidConfiguration.ExistingFrameSelectionPresenter)]
        public void Initialize_WhenPreconditionsAreInvalid_RejectsConfiguration(InvalidConfiguration invalidConfiguration)
        {
            PrototypeFixture fixture = CreateFixture();
            ApplyInvalidConfiguration(fixture, invalidConfiguration);
            ExternalLifecycleState externalLifecycleBeforeInitialize = fixture.CaptureExternalLifecycleState();

            Assert.That(
                () => fixture.Composition.Initialize(),
                Throws.TypeOf(ExpectedExceptionTypeFor(invalidConfiguration)));
            fixture.AssertFailedInitializationCleanup(invalidConfiguration, externalLifecycleBeforeInitialize);
            fixture.AssertCompositionAssignedValidHighlightsInactive();
        }

        [TestCase(InvalidConfiguration.InvalidPawnHighlightRoot)]
        [TestCase(InvalidConfiguration.InvalidTokenHighlightRoot)]
        public void Initialize_WhenFailureOccursAfterSelectionVisualConfiguration_ClearsConfiguredHighlights(
            InvalidConfiguration invalidConfiguration)
        {
            PrototypeFixture fixture = CreateFixture();
            fixture.SetAllHighlightsActive();
            ApplyInvalidConfiguration(fixture, invalidConfiguration);
            ExternalLifecycleState externalLifecycleBeforeInitialize = fixture.CaptureExternalLifecycleState();

            Assert.That(
                () => fixture.Composition.Initialize(),
                Throws.TypeOf(ExpectedExceptionTypeFor(invalidConfiguration)));

            fixture.AssertFailedInitializationCleanup(invalidConfiguration, externalLifecycleBeforeInitialize);
            fixture.AssertConfiguredBeforeFailureHighlightsInactive(invalidConfiguration);
        }

        [Test]
        public void Initialize_CreatesApprovedPrototypeStateGraphAndBindsExactInstances()
        {
            PrototypeFixture fixture = CreateInitializedFixture();
            MatchState match = fixture.Composition.MatchState;

            Assert.That(match.Revision, Is.EqualTo(0));
            Assert.That(match.ObjectCount, Is.EqualTo(18));
            Assert.That(match.Cards.Count, Is.EqualTo(16));
            Assert.That(match.Pawns.Count, Is.EqualTo(1));
            Assert.That(match.Tokens.Count, Is.EqualTo(1));
            Assert.That(match.Containers.Count, Is.EqualTo(8));
            Assert.That(match.Seats.Count, Is.EqualTo(1));
            Assert.That(fixture.Composition.ButtonDefinitions.Count, Is.EqualTo(8));
            Assert.That(fixture.Composition.CardViews.Count, Is.EqualTo(16));
            Assert.That(fixture.Composition.DeckView.VisibleCardCount, Is.EqualTo(12));
            Assert.That(fixture.Composition.HandView.VisibleCardCount, Is.EqualTo(0));
            Assert.That(fixture.Composition.ConsoleSlotViews.Count, Is.EqualTo(3));
            Assert.That(fixture.Composition.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            AssertPose(fixture.Composition.CardState.BaseState.Pose, -2d, 0d, 0f);
            AssertPose(fixture.Composition.PawnState.BaseState.Pose, -3.5d, -0.5d, 0f);
            AssertPose(fixture.Composition.TokenState.BaseState.Pose, 3.5d, -0.5d, 0f);
            Assert.That(fixture.Composition.CardState.BaseState.Id.IsEmpty, Is.False);
            Assert.That(fixture.Composition.PawnState.BaseState.Id.IsEmpty, Is.False);
            Assert.That(fixture.Composition.TokenState.BaseState.Id.IsEmpty, Is.False);
            Assert.That(fixture.Composition.CardState.BaseState.DefinitionId.IsEmpty, Is.False);
            Assert.That(fixture.Composition.PawnState.BaseState.DefinitionId.IsEmpty, Is.False);
            Assert.That(fixture.Composition.TokenState.BaseState.DefinitionId.IsEmpty, Is.False);
            Assert.That(fixture.Composition.CardState.BaseState.Id, Is.Not.EqualTo(fixture.Composition.PawnState.BaseState.Id));
            Assert.That(fixture.Composition.CardState.BaseState.Id, Is.Not.EqualTo(fixture.Composition.TokenState.BaseState.Id));
            Assert.That(fixture.Composition.PawnState.BaseState.Id, Is.Not.EqualTo(fixture.Composition.TokenState.BaseState.Id));
            Assert.That(fixture.Composition.CardState.BaseState.Visibility, Is.EqualTo(ObjectVisibility.Public));
            Assert.That(fixture.Composition.PawnState.BaseState.Visibility, Is.EqualTo(ObjectVisibility.Public));
            Assert.That(fixture.Composition.TokenState.BaseState.Visibility, Is.EqualTo(ObjectVisibility.Public));

            Assert.That(match.Cards[fixture.Composition.CardState.BaseState.Id], Is.SameAs(fixture.Composition.CardState));
            Assert.That(match.Pawns[fixture.Composition.PawnState.BaseState.Id], Is.SameAs(fixture.Composition.PawnState));
            Assert.That(match.Tokens[fixture.Composition.TokenState.BaseState.Id], Is.SameAs(fixture.Composition.TokenState));
            Assert.That(match.GetObject(fixture.Composition.CardState.BaseState.Id), Is.SameAs(fixture.Composition.CardState.BaseState));
            Assert.That(match.GetObject(fixture.Composition.PawnState.BaseState.Id), Is.SameAs(fixture.Composition.PawnState.BaseState));
            Assert.That(match.GetObject(fixture.Composition.TokenState.BaseState.Id), Is.SameAs(fixture.Composition.TokenState.BaseState));
            Assert.That(fixture.CardView.CardState, Is.SameAs(fixture.Composition.CardState));
            Assert.That(fixture.PawnView.PawnState, Is.SameAs(fixture.Composition.PawnState));
            Assert.That(fixture.TokenView.TokenState, Is.SameAs(fixture.Composition.TokenState));
            Assert.That(fixture.CardView.BoundState, Is.SameAs(fixture.Composition.CardState.BaseState));
            Assert.That(fixture.PawnView.BoundState, Is.SameAs(fixture.Composition.PawnState.BaseState));
            Assert.That(fixture.TokenView.BoundState, Is.SameAs(fixture.Composition.TokenState.BaseState));
            AssertWorldPose(fixture.CardView, fixture.Composition.CardState.BaseState.Pose);
            AssertWorldPose(fixture.PawnView, fixture.Composition.PawnState.BaseState.Pose);
            AssertWorldPose(fixture.TokenView, fixture.Composition.TokenState.BaseState.Pose);
        }

        [Test]
        public void Initialize_SharesOneDependencyGraphAcrossCoordinatorsAndAdapters()
        {
            PrototypeFixture fixture = CreateInitializedFixture();

            Assert.That(fixture.Composition.MoveCoordinator.MatchState, Is.SameAs(fixture.Composition.MatchState));
            Assert.That(fixture.Composition.RotationCoordinator.MatchState, Is.SameAs(fixture.Composition.MatchState));
            Assert.That(fixture.Composition.FlipCoordinator.MatchState, Is.SameAs(fixture.Composition.MatchState));
            Assert.That(fixture.Composition.MoveCoordinator.RequestedByPlayerId, Is.EqualTo(fixture.Composition.LocalPlayerId));
            Assert.That(fixture.Composition.RotationCoordinator.RequestedByPlayerId, Is.EqualTo(fixture.Composition.LocalPlayerId));
            Assert.That(fixture.Composition.FlipCoordinator.RequestedByPlayerId, Is.EqualTo(fixture.Composition.LocalPlayerId));
            Assert.That(fixture.Composition.MoveCoordinator.InteractionOwnerId, Is.EqualTo(fixture.Composition.RotationCoordinator.InteractionOwnerId));
            Assert.That(fixture.Composition.MoveCoordinator.InteractionOwnerId, Is.EqualTo(fixture.Composition.FlipCoordinator.InteractionOwnerId));
            Assert.That(fixture.Composition.RotationCoordinator.SelectionState, Is.SameAs(fixture.Composition.SelectionState));
            Assert.That(fixture.Composition.FlipCoordinator.SelectionState, Is.SameAs(fixture.Composition.SelectionState));
            Assert.That(fixture.Composition.RotationCoordinator.LockService, Is.SameAs(fixture.Composition.LockService));
            Assert.That(fixture.Composition.FlipCoordinator.LockService, Is.SameAs(fixture.Composition.LockService));
            Assert.That(fixture.Composition.InputRoutingPolicy.SelectionState, Is.SameAs(fixture.Composition.SelectionState));
            Assert.That(fixture.Composition.InputRoutingPolicy.MoveCoordinator, Is.SameAs(fixture.Composition.MoveCoordinator));
            Assert.That(fixture.Composition.HitResolver.TargetCamera, Is.SameAs(fixture.TargetCamera));
            Assert.That(fixture.Composition.HitResolver.InteractionLayerMask.value, Is.EqualTo(fixture.Composition.interactionLayerMask.value));
            Assert.That(fixture.Composition.PointerProjector.TargetCamera, Is.SameAs(fixture.TargetCamera));
            Assert.That(fixture.Composition.PointerProjector.CoordinateConverter, Is.SameAs(fixture.Composition.CoordinateConverter));
        }

        [Test]
        public void Initialize_WiresAdaptersToSingleFrameCoordinator()
        {
            PrototypeFixture fixture = CreateInitializedFixture();

            Assert.That(fixture.CameraAdapter.ScrollRoutingPolicy, Is.SameAs(fixture.Composition.InputRoutingPolicy));
            Assert.That(fixture.ObjectAdapter.MoveCoordinator, Is.SameAs(fixture.Composition.MoveCoordinator));
            Assert.That(fixture.ObjectAdapter.RotationCoordinator, Is.SameAs(fixture.Composition.RotationCoordinator));
            Assert.That(fixture.ObjectAdapter.FlipCoordinator, Is.SameAs(fixture.Composition.FlipCoordinator));
            Assert.That(fixture.ObjectAdapter.RoutingPolicy, Is.SameAs(fixture.Composition.InputRoutingPolicy));
            Assert.That(fixture.FrameCoordinator.enabled, Is.True);
            Assert.That(fixture.FrameCoordinator.CameraInputAdapter, Is.SameAs(fixture.CameraAdapter));
            Assert.That(fixture.FrameCoordinator.ObjectInputAdapter, Is.SameAs(fixture.ObjectAdapter));
            Assert.That(fixture.CameraAdapter.IsExternallyDrivenBy(fixture.FrameCoordinator), Is.True);
            Assert.That(fixture.ObjectAdapter.IsExternallyDrivenBy(fixture.FrameCoordinator), Is.True);
            Assert.That(fixture.FrameCoordinator.HasSelectionPresenter, Is.True);
            Assert.That(fixture.FrameCoordinator.SelectionPresenter, Is.SameAs(fixture.Composition.SelectionPresenter));
            Assert.That(fixture.Composition.SelectionPresenter.SelectionState, Is.SameAs(fixture.Composition.SelectionState));
            Assert.That(fixture.Composition.SelectionPresenter.CardSelectionVisual, Is.SameAs(fixture.CardSelectionVisual));
            Assert.That(fixture.Composition.SelectionPresenter.PawnSelectionVisual, Is.SameAs(fixture.PawnSelectionVisual));
            Assert.That(fixture.Composition.SelectionPresenter.TokenSelectionVisual, Is.SameAs(fixture.TokenSelectionVisual));
            Assert.That(fixture.CardSelectionVisual.ObjectView, Is.SameAs(fixture.CardView));
            Assert.That(fixture.PawnSelectionVisual.ObjectView, Is.SameAs(fixture.PawnView));
            Assert.That(fixture.TokenSelectionVisual.ObjectView, Is.SameAs(fixture.TokenView));
            fixture.AssertAllHighlightsInactive();
        }

        [Test]
        public void RuntimeInput_SelectingColliderBackedCard_UsesCompositionSelectionState()
        {
            PrototypeFixture fixture = CreateInitializedFixture();

            bool beganPress = fixture.Composition.MoveCoordinator.TryBeginPress(fixture.ScreenPointFor(fixture.CardView));

            Assert.That(beganPress, Is.True);
            Assert.That(fixture.Composition.SelectionState.SelectedView, Is.SameAs(fixture.CardView));
            Assert.That(fixture.Composition.MoveCoordinator.HasActiveInteraction, Is.True);
            Assert.That(fixture.Composition.LockService.Count, Is.EqualTo(1));
        }

        [TestCase(TabletopObjectKind.Card)]
        [TestCase(TabletopObjectKind.Pawn)]
        [TestCase(TabletopObjectKind.Token)]
        public void RuntimeInput_SelectingObjectThroughSharedFrame_HighlightsOnlySelectedObject(TabletopObjectKind kind)
        {
            PrototypeFixture fixture = CreateInitializedFixture();
            TabletopObjectView view = fixture.ViewFor(kind);

            fixture.ApplySharedFrame(fixture.CreatePressFrame(view));

            Assert.That(fixture.Composition.SelectionState.SelectedView, Is.SameAs(view));
            fixture.AssertOnlyHighlightActive(kind);
            Assert.That(fixture.Composition.MatchState.Revision, Is.EqualTo(0));
        }

        [Test]
        public void RuntimeInput_EmptyClickThroughSharedFrame_ClearsSelectionAndHighlights()
        {
            PrototypeFixture fixture = CreateInitializedFixture();
            fixture.Composition.SelectionState.Select(fixture.CardView);
            fixture.Composition.SelectionPresenter.Refresh();
            Assert.That(fixture.CardHighlightRoot.activeSelf, Is.True);

            fixture.ApplySharedFrame(fixture.CreateEmptyPressFrame());

            Assert.That(fixture.Composition.SelectionState.HasSelection, Is.False);
            fixture.AssertAllHighlightsInactive();
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Composition.MatchState.Revision, Is.EqualTo(0));
        }

        [TestCase(TabletopObjectKind.Card, 0d, 0d)]
        [TestCase(TabletopObjectKind.Pawn, 1d, 1d)]
        [TestCase(TabletopObjectKind.Token, 3d, 1d)]
        public void RuntimeInput_AcceptedMovement_MutatesCompositionMatchStateAndReconcilesSameView(
            TabletopObjectKind kind,
            double targetX,
            double targetY)
        {
            PrototypeFixture fixture = CreateInitializedFixture();
            TabletopObjectView view = fixture.ViewFor(kind);
            TabletopObjectState state = fixture.StateFor(kind);

            fixture.Composition.MoveCoordinator.TryBeginPress(fixture.ScreenPointFor(view));
            fixture.Composition.MoveCoordinator.UpdatePointer(fixture.ScreenPointForWorld((float)targetX, (float)targetY));
            Assert.That(fixture.Composition.PreviewSession.IsActive, Is.True);
            Assert.That(fixture.Composition.MatchState.Revision, Is.EqualTo(0));

            MoveInteractionReleaseResult releaseResult =
                fixture.Composition.MoveCoordinator.ReleasePointer(fixture.ScreenPointForWorld((float)targetX, (float)targetY));

            Assert.That(releaseResult.Succeeded, Is.True);
            Assert.That(fixture.Composition.MatchState.Revision, Is.EqualTo(1));
            Assert.That(state.Pose.Position.X, Is.EqualTo(targetX).Within(CoordinateTolerance));
            Assert.That(state.Pose.Position.Y, Is.EqualTo(targetY).Within(CoordinateTolerance));
            Assert.That(view.BoundState, Is.SameAs(state));
            Assert.That(view.IsPreviewing, Is.False);
            AssertWorldPose(view, state.Pose);
            Assert.That(fixture.Composition.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void RuntimeInput_SelectedRotation_UsesCompositionRuntimeStateAndView()
        {
            PrototypeFixture fixture = CreateInitializedFixture();
            fixture.Composition.SelectionState.Select(fixture.PawnView);

            fixture.ApplySharedFrame(new TabletopInputFrame(
                Vector2.zero,
                false,
                Vector2.zero,
                120f,
                fixture.ScreenPointFor(fixture.PawnView),
                false,
                false,
                false,
                false,
                120f,
                false));

            Assert.That(fixture.Composition.MatchState.Revision, Is.EqualTo(1));
            Assert.That(fixture.Composition.PawnState.BaseState.Pose.RotationDegrees, Is.EqualTo(15f).Within(FloatTolerance));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.True);
            Assert.That(fixture.ObjectAdapter.LastRotationResult.Value.Succeeded, Is.True);
            AssertWorldPose(fixture.PawnView, fixture.Composition.PawnState.BaseState.Pose);
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            fixture.AssertOnlyHighlightActive(TabletopObjectKind.Pawn);
        }

        [Test]
        public void RuntimeInput_SelectedCardFlip_UpdatesAuthoritativeFaceAndConfiguredRoots()
        {
            PrototypeFixture fixture = CreateInitializedFixture();
            fixture.Composition.SelectionState.Select(fixture.CardView);

            fixture.ApplySharedFrame(new TabletopInputFrame(
                Vector2.zero,
                false,
                Vector2.zero,
                0f,
                fixture.ScreenPointFor(fixture.CardView),
                false,
                false,
                false,
                false,
                0f,
                true));

            Assert.That(fixture.Composition.MatchState.Revision, Is.EqualTo(1));
            Assert.That(fixture.Composition.CardState.Face, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.CardView.DisplayedFace, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.FaceUpRoot.activeSelf, Is.False);
            Assert.That(fixture.FaceDownRoot.activeSelf, Is.True);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.True);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.Value.Succeeded, Is.True);
            fixture.AssertOnlyHighlightActive(TabletopObjectKind.Card);
        }

        [Test]
        public void RuntimeInput_DragPreviewUpdates_DoNotCreateCommandsOrAdvanceRevision()
        {
            PrototypeFixture fixture = CreateInitializedFixture();

            fixture.Composition.MoveCoordinator.TryBeginPress(fixture.ScreenPointFor(fixture.CardView));
            fixture.Composition.MoveCoordinator.UpdatePointer(fixture.ScreenPointForWorld(-1f, 1f));
            fixture.Composition.MoveCoordinator.UpdatePointer(fixture.ScreenPointForWorld(-0.5f, 1.5f));

            Assert.That(fixture.Composition.PreviewSession.IsActive, Is.True);
            Assert.That(fixture.Composition.MatchState.Revision, Is.EqualTo(0));
            Assert.That(fixture.CardView.IsPreviewing, Is.True);
            Assert.That(fixture.CardView.BoundState.Pose.Position.X, Is.EqualTo(-2d).Within(CoordinateTolerance));
            Assert.That(fixture.CardView.BoundState.Pose.Position.Y, Is.EqualTo(0d).Within(CoordinateTolerance));
        }

        [Test]
        public void Shutdown_CleansCompositionGraphWithoutDestroyingViewsOrScale()
        {
            PrototypeFixture fixture = CreateInitializedFixture();
            Vector3 cardScale = fixture.CardView.transform.localScale;
            fixture.Composition.MoveCoordinator.TryBeginPress(fixture.ScreenPointFor(fixture.CardView));
            fixture.Composition.MoveCoordinator.UpdatePointer(fixture.ScreenPointForWorld(-1f, 1f));

            fixture.Composition.Shutdown();
            fixture.Composition.Shutdown();

            Assert.That(fixture.Composition.IsInitialized, Is.False);
            Assert.That(fixture.FrameCoordinator.enabled, Is.False);
            Assert.That(fixture.CameraAdapter.IsExternallyDriven, Is.False);
            Assert.That(fixture.ObjectAdapter.IsExternallyDriven, Is.False);
            Assert.That(fixture.ObjectAdapter.IsInitialized, Is.False);
            Assert.That(fixture.CameraAdapter.HasScrollRoutingPolicy, Is.False);
            Assert.That(fixture.FrameCoordinator.HasSelectionPresenter, Is.False);
            Assert.That(fixture.Composition.SelectionPresenter, Is.Null);
            fixture.AssertSelectionVisualsUnconfigured();
            fixture.AssertAllHighlightsInactive();
            Assert.That(fixture.CardView.IsBound, Is.False);
            Assert.That(fixture.PawnView.IsBound, Is.False);
            Assert.That(fixture.TokenView.IsBound, Is.False);
            Assert.That(fixture.CardView.gameObject, Is.Not.Null);
            Assert.That(fixture.PawnView.gameObject, Is.Not.Null);
            Assert.That(fixture.TokenView.gameObject, Is.Not.Null);
            Assert.That(fixture.CardView.transform.localScale, Is.EqualTo(cardScale));
            Assert.That(fixture.Composition.MatchState, Is.Null);
            Assert.That(fixture.Composition.SelectionState, Is.Null);
            Assert.That(fixture.Composition.MoveCoordinator, Is.Null);
            Assert.That(fixture.Composition.LocalPlayerId, Is.EqualTo(PlayerId.Empty));
        }

        [Test]
        public void Initialize_WhenFailureOccurs_LeavesNoPartialAdapterConfigurationOrBindings()
        {
            PrototypeFixture fixture = CreateFixture();
            fixture.FrameCoordinator.objectInputAdapter = null;

            Assert.Throws<InvalidOperationException>(() => fixture.Composition.Initialize());

            Assert.That(fixture.Composition.IsInitialized, Is.False);
            Assert.That(fixture.CardView.IsBound, Is.False);
            Assert.That(fixture.PawnView.IsBound, Is.False);
            Assert.That(fixture.TokenView.IsBound, Is.False);
            Assert.That(fixture.CameraAdapter.HasScrollRoutingPolicy, Is.False);
            Assert.That(fixture.ObjectAdapter.IsInitialized, Is.False);
            Assert.That(fixture.CameraAdapter.IsExternallyDriven, Is.False);
            Assert.That(fixture.ObjectAdapter.IsExternallyDriven, Is.False);
            Assert.That(fixture.FrameCoordinator.HasSelectionPresenter, Is.False);
            fixture.AssertSelectionVisualsUnconfigured();
            fixture.AssertAllHighlightsInactive();
        }

        [Test]
        public void Boundary_CompositionRequiresNoScenePrefabMaterialLayerTemplatePersistenceOrNetworking()
        {
            PrototypeFixture fixture = CreateInitializedFixture();

            Assert.That(fixture.Composition.GetComponent<PlayerInput>(), Is.Null);
            Assert.That(fixture.CardView.GetComponent<Renderer>(), Is.Null);
            Assert.That(fixture.PawnView.GetComponent<Renderer>(), Is.Null);
            Assert.That(fixture.TokenView.GetComponent<Renderer>(), Is.Null);
            Assert.That(fixture.Composition.MatchState.Containers.Count, Is.EqualTo(8));
            Assert.That(fixture.Composition.MatchState.Seats.Count, Is.EqualTo(1));
            Assert.That(fixture.Composition.MatchState.GameTemplateId, Is.EqualTo(GameTemplateId.Empty));
        }

        private PrototypeFixture CreateInitializedFixture()
        {
            PrototypeFixture fixture = CreateFixture();
            fixture.Composition.Initialize();
            return fixture;
        }

        private PrototypeFixture CreateFixture()
        {
            InputActionAsset inputActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            createdInputAssets.Add(inputActionAsset);
            InputActionMap cameraMap = inputActionAsset.AddActionMap("TabletopCamera");
            InputActionMap objectMap = inputActionAsset.AddActionMap("TabletopObject");

            CameraActions cameraActions = new CameraActions(
                CreateActionReference(cameraMap, "KeyboardPan", InputActionType.Value, "Vector2"),
                CreateActionReference(cameraMap, "DragPan", InputActionType.Button, "Button"),
                CreateActionReference(cameraMap, "PointerDelta", InputActionType.PassThrough, "Vector2"),
                CreateActionReference(cameraMap, "Zoom", InputActionType.PassThrough, "Axis"));
            ObjectActions objectActions = new ObjectActions(
                CreateActionReference(objectMap, "Point", InputActionType.PassThrough, "Vector2"),
                CreateActionReference(objectMap, "Select", InputActionType.Button, "Button"),
                CreateActionReference(objectMap, "Cancel", InputActionType.Button, "Button"),
                CreateActionReference(objectMap, "Rotate", InputActionType.PassThrough, "Axis"),
                CreateActionReference(objectMap, "Flip", InputActionType.Button, "Button"));
            AssertInputGraphDisabled(inputActionAsset);

            GameObject cameraRigObject = CreateGameObject("Prototype Camera Rig");
            cameraRigObject.SetActive(false);
            GameObject cameraObject = CreateGameObject("Prototype Camera");
            cameraObject.transform.SetParent(cameraRigObject.transform, false);
            Camera targetCamera = cameraObject.AddComponent<Camera>();
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = 5f;
            targetCamera.nearClipPlane = 0.1f;
            targetCamera.farClipPlane = 100f;
            targetCamera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            TabletopCameraController cameraController = cameraRigObject.AddComponent<TabletopCameraController>();
            cameraController.targetCamera = targetCamera;
            cameraController.cameraRig = cameraRigObject.transform;
            cameraController.worldUnitsPerTableUnit = 1f;
            cameraController.cameraHeight = 10f;
            cameraController.minimumOrthographicSize = 2f;
            cameraController.maximumOrthographicSize = 20f;
            cameraController.initialOrthographicSize = 5f;
            cameraRigObject.SetActive(true);

            GameObject cameraAdapterObject = CreateGameObject("Prototype Camera Input Adapter");
            cameraAdapterObject.SetActive(false);
            TabletopCameraInputAdapter cameraInputAdapter =
                cameraAdapterObject.AddComponent<TabletopCameraInputAdapter>();
            cameraInputAdapter.cameraController = cameraController;
            cameraInputAdapter.keyboardPanAction = cameraActions.KeyboardPan;
            cameraInputAdapter.dragPanAction = cameraActions.DragPan;
            cameraInputAdapter.pointerDeltaAction = cameraActions.PointerDelta;
            cameraInputAdapter.zoomAction = cameraActions.Zoom;
            cameraAdapterObject.SetActive(true);

            GameObject objectAdapterObject = CreateGameObject("Prototype Object Input Adapter");
            objectAdapterObject.SetActive(false);
            TabletopObjectInputAdapter objectInputAdapter =
                objectAdapterObject.AddComponent<TabletopObjectInputAdapter>();
            objectInputAdapter.pointAction = objectActions.Point;
            objectInputAdapter.selectAction = objectActions.Select;
            objectInputAdapter.cancelAction = objectActions.Cancel;
            objectInputAdapter.rotateAction = objectActions.Rotate;
            objectInputAdapter.flipAction = objectActions.Flip;
            objectInputAdapter.rotationStepDegrees = 15f;
            objectAdapterObject.SetActive(true);

            CardView cardView = CreateView<CardView>("Prototype Card");
            GameObject faceUpRoot = CreateChild(cardView.gameObject, "FaceUp Root");
            GameObject faceDownRoot = CreateChild(cardView.gameObject, "FaceDown Root");
            cardView.ConfigureFacePresentation(faceUpRoot, faceDownRoot);
            PawnView pawnView = CreateView<PawnView>("Prototype Pawn");
            TokenView tokenView = CreateView<TokenView>("Prototype Token");
            AddBoxCollider(cardView.gameObject, InteractionLayer);
            AddBoxCollider(pawnView.gameObject, InteractionLayer);
            AddBoxCollider(tokenView.gameObject, InteractionLayer);
            TabletopSelectionVisual cardSelectionVisual =
                cardView.gameObject.AddComponent<TabletopSelectionVisual>();
            TabletopSelectionVisual pawnSelectionVisual =
                pawnView.gameObject.AddComponent<TabletopSelectionVisual>();
            TabletopSelectionVisual tokenSelectionVisual =
                tokenView.gameObject.AddComponent<TabletopSelectionVisual>();
            GameObject cardHighlightRoot = CreateChild(cardView.gameObject, "Card Selection Highlight");
            GameObject pawnHighlightRoot = CreateChild(pawnView.gameObject, "Pawn Selection Highlight");
            GameObject tokenHighlightRoot = CreateChild(tokenView.gameObject, "Token Selection Highlight");
            cardHighlightRoot.SetActive(false);
            pawnHighlightRoot.SetActive(false);
            tokenHighlightRoot.SetActive(false);

            GameObject frameCoordinatorObject = CreateGameObject("Prototype Input Frame Coordinator");
            frameCoordinatorObject.SetActive(false);
            TabletopInputFrameCoordinator frameCoordinator =
                frameCoordinatorObject.AddComponent<TabletopInputFrameCoordinator>();
            frameCoordinator.cameraInputAdapter = cameraInputAdapter;
            frameCoordinator.objectInputAdapter = objectInputAdapter;
            frameCoordinator.enabled = false;
            frameCoordinatorObject.SetActive(true);

            GameObject compositionObject = CreateGameObject("Prototype Composition");
            compositionObject.SetActive(false);
            TabletopPrototypeComposition composition =
                compositionObject.AddComponent<TabletopPrototypeComposition>();
            composition.targetCamera = targetCamera;
            composition.cameraInputAdapter = cameraInputAdapter;
            composition.objectInputAdapter = objectInputAdapter;
            composition.inputFrameCoordinator = frameCoordinator;
            composition.cardView = cardView;
            composition.pawnView = pawnView;
            composition.tokenView = tokenView;
            composition.cardSelectionVisual = cardSelectionVisual;
            composition.cardHighlightRoot = cardHighlightRoot;
            composition.pawnSelectionVisual = pawnSelectionVisual;
            composition.pawnHighlightRoot = pawnHighlightRoot;
            composition.tokenSelectionVisual = tokenSelectionVisual;
            composition.tokenHighlightRoot = tokenHighlightRoot;
            composition.interactionLayerMask = LayerMaskFor(InteractionLayer);
            composition.maximumHitDistance = 100f;
            composition.dragThresholdPixels = 8f;
            composition.worldUnitsPerTableUnit = 1f;
            composition.tabletopHeight = 0f;
            compositionObject.SetActive(true);

            return new PrototypeFixture(
                composition,
                targetCamera,
                cameraController,
                cameraInputAdapter,
                objectInputAdapter,
                frameCoordinator,
                cardView,
                pawnView,
                tokenView,
                cardSelectionVisual,
                pawnSelectionVisual,
                tokenSelectionVisual,
                cardHighlightRoot,
                pawnHighlightRoot,
                tokenHighlightRoot,
                faceUpRoot,
                faceDownRoot);
        }

        private T CreateView<T>(string name)
            where T : TabletopObjectView
        {
            GameObject gameObject = CreateGameObject(name);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateChild(GameObject parent, string name)
        {
            GameObject child = CreateGameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private InputActionReference CreateActionReference(
            InputActionMap actionMap,
            string actionName,
            InputActionType actionType,
            string expectedControlType)
        {
            InputAction action = actionMap.AddAction(actionName, actionType, expectedControlLayout: expectedControlType);
            AddRequiredBinding(action, actionName);
            InputActionReference actionReference = InputActionReference.Create(action);
            createdActionReferences.Add(actionReference);
            return actionReference;
        }

        private static void AddRequiredBinding(InputAction action, string actionName)
        {
            switch (actionName)
            {
                case "KeyboardPan":
                    action.AddCompositeBinding("2DVector")
                        .With("Up", "<Keyboard>/w")
                        .With("Down", "<Keyboard>/s")
                        .With("Left", "<Keyboard>/a")
                        .With("Right", "<Keyboard>/d");
                    break;
                case "DragPan":
                    action.AddBinding("<Mouse>/middleButton");
                    break;
                case "PointerDelta":
                    action.AddBinding("<Pointer>/delta");
                    break;
                case "Zoom":
                case "Rotate":
                    action.AddBinding("<Mouse>/scroll/y");
                    break;
                case "Point":
                    action.AddBinding("<Pointer>/position");
                    break;
                case "Select":
                    action.AddBinding("<Mouse>/leftButton");
                    break;
                case "Cancel":
                    action.AddBinding("<Keyboard>/escape");
                    break;
                case "Flip":
                    action.AddBinding("<Keyboard>/f");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(actionName), actionName, "Unsupported input action.");
            }
        }

        private static void AssertInputGraphDisabled(InputActionAsset inputActionAsset)
        {
            foreach (InputActionMap actionMap in inputActionAsset.actionMaps)
            {
                Assert.That(actionMap.enabled, Is.False);

                foreach (InputAction action in actionMap.actions)
                {
                    Assert.That(action.enabled, Is.False);
                }
            }
        }

        private void ShutdownRuntimeComponents()
        {
            for (int i = 0; i < createdGameObjects.Count; i++)
            {
                GameObject gameObject = createdGameObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                TabletopPrototypeComposition composition = gameObject.GetComponent<TabletopPrototypeComposition>();
                if (composition != null)
                {
                    composition.Shutdown();
                }

                TabletopInputFrameCoordinator frameCoordinator = gameObject.GetComponent<TabletopInputFrameCoordinator>();
                if (frameCoordinator != null)
                {
                    frameCoordinator.enabled = false;
                }

                TabletopObjectInputAdapter objectInputAdapter = gameObject.GetComponent<TabletopObjectInputAdapter>();
                if (objectInputAdapter != null)
                {
                    objectInputAdapter.Shutdown();
                }

                TabletopCameraInputAdapter cameraInputAdapter = gameObject.GetComponent<TabletopCameraInputAdapter>();
                if (cameraInputAdapter != null)
                {
                    cameraInputAdapter.enabled = false;
                }
            }
        }

        private TabletopInteractionInputRoutingPolicy CreateManualRoutingPolicy(PrototypeFixture fixture)
        {
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.Empty,
                0,
                Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                Array.Empty<ContainerState>(),
                Array.Empty<SeatState>());
            TabletopSelectionState selectionState = new TabletopSelectionState();
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            TabletopMoveInteractionCoordinator moveCoordinator = new TabletopMoveInteractionCoordinator(
                match,
                PlayerId.New(),
                InteractionOwnerId.New(),
                selectionState,
                new TabletopObjectHitResolver(fixture.TargetCamera, LayerMaskFor(InteractionLayer), 100f),
                new TabletopPointerProjector(fixture.TargetCamera, new TabletopCoordinateConverter(1f, 0f, 0f, 0f), 0f),
                lockService,
                new TabletopInteractionStateMachine(8f),
                new TabletopDragPreviewSession(),
                new MoveObjectUseCase());
            return new TabletopInteractionInputRoutingPolicy(selectionState, moveCoordinator);
        }

        private void InitializeObjectAdapterWithManualGraph(PrototypeFixture fixture)
        {
            TabletopInteractionInputRoutingPolicy routingPolicy = CreateManualRoutingPolicy(fixture);
            TabletopSelectionState selectionState = routingPolicy.SelectionState;
            TabletopMoveInteractionCoordinator moveCoordinator = routingPolicy.MoveCoordinator;
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            fixture.ObjectAdapter.Initialize(
                moveCoordinator,
                new TabletopRotationCoordinator(
                    moveCoordinator.MatchState,
                    moveCoordinator.RequestedByPlayerId,
                    moveCoordinator.InteractionOwnerId,
                    selectionState,
                    lockService,
                    new RotateObjectUseCase()),
                new TabletopCardFlipCoordinator(
                    moveCoordinator.MatchState,
                    moveCoordinator.RequestedByPlayerId,
                    moveCoordinator.InteractionOwnerId,
                    selectionState,
                    lockService,
                    new FlipCardUseCase()),
                routingPolicy);
        }

        private TabletopInputFrameCoordinator CreateDetachedFrameDriver(
            TabletopCameraInputAdapter cameraAdapter,
            TabletopObjectInputAdapter objectAdapter)
        {
            GameObject gameObject = CreateGameObject("Detached Input Frame Driver");
            gameObject.SetActive(false);
            TabletopInputFrameCoordinator frameDriver = gameObject.AddComponent<TabletopInputFrameCoordinator>();
            frameDriver.cameraInputAdapter = cameraAdapter;
            frameDriver.objectInputAdapter = objectAdapter;
            frameDriver.enabled = false;
            gameObject.SetActive(true);
            return frameDriver;
        }

        private static BoxCollider AddBoxCollider(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.2f, 1f);
            return collider;
        }

        private static LayerMask LayerMaskFor(int layer)
        {
            return 1 << layer;
        }

        private static void AssertPose(TabletopPose pose, double x, double y, float rotationDegrees)
        {
            Assert.That(pose.Position.X, Is.EqualTo(x).Within(CoordinateTolerance));
            Assert.That(pose.Position.Y, Is.EqualTo(y).Within(CoordinateTolerance));
            Assert.That(pose.RotationDegrees, Is.EqualTo(rotationDegrees).Within(FloatTolerance));
            Assert.That(pose.Layer, Is.EqualTo(0));
            Assert.That(pose.LocalOrder, Is.EqualTo(0));
        }

        private static void AssertWorldPose(TabletopObjectView view, TabletopPose pose)
        {
            Assert.That(view.transform.position.x, Is.EqualTo((float)pose.Position.X).Within(FloatTolerance));
            Assert.That(view.transform.position.y, Is.EqualTo(0f).Within(FloatTolerance));
            Assert.That(view.transform.position.z, Is.EqualTo((float)pose.Position.Y).Within(FloatTolerance));
            Assert.That(
                Quaternion.Angle(Quaternion.Euler(0f, pose.RotationDegrees, 0f), view.transform.rotation),
                Is.EqualTo(0f).Within(FloatTolerance));
        }

        private static TabletopObjectState CreateBaseState(TabletopObjectKind kind, int seed)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                TabletopPose.Default,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static Type ExpectedExceptionTypeFor(InvalidConfiguration invalidConfiguration)
        {
            switch (invalidConfiguration)
            {
                case InvalidConfiguration.ZeroMaximumHitDistance:
                case InvalidConfiguration.NaNMaximumHitDistance:
                case InvalidConfiguration.NegativeDragThreshold:
                case InvalidConfiguration.NaNDragThreshold:
                case InvalidConfiguration.ZeroWorldScale:
                case InvalidConfiguration.NaNWorldScale:
                case InvalidConfiguration.NaNTabletopHeight:
                    return typeof(ArgumentOutOfRangeException);
                case InvalidConfiguration.InvalidCardHighlightRoot:
                case InvalidConfiguration.InvalidPawnHighlightRoot:
                case InvalidConfiguration.InvalidTokenHighlightRoot:
                    return typeof(ArgumentException);
                default:
                    return typeof(InvalidOperationException);
            }
        }

        public enum MissingReference
        {
            TargetCamera,
            CameraInputAdapter,
            ObjectInputAdapter,
            InputFrameCoordinator,
            CardView,
            PawnView,
            TokenView,
            CardSelectionVisual,
            CardHighlightRoot,
            PawnSelectionVisual,
            PawnHighlightRoot,
            TokenSelectionVisual,
            TokenHighlightRoot
        }

        public enum InvalidConfiguration
        {
            PerspectiveCamera,
            ZeroMaximumHitDistance,
            NaNMaximumHitDistance,
            NegativeDragThreshold,
            NaNDragThreshold,
            ZeroWorldScale,
            NaNWorldScale,
            NaNTabletopHeight,
            DuplicateCardPawnViews,
            DuplicateCardTokenViews,
            DuplicatePawnTokenViews,
            PreBoundCardView,
            PreBoundPawnView,
            PreBoundTokenView,
            PreInitializedObjectAdapter,
            ExistingCameraScrollPolicy,
            EnabledFrameCoordinator,
            ExistingExternalFrameDriver,
            CoordinatorReferencesDifferentAdapters,
            CardVisualOnWrongView,
            PawnVisualOnWrongView,
            TokenVisualOnWrongView,
            InvalidCardHighlightRoot,
            InvalidPawnHighlightRoot,
            InvalidTokenHighlightRoot,
            DuplicateSelectionVisual,
            DuplicateHighlightRoot,
            ExistingFrameSelectionPresenter
        }

        private readonly struct CameraActions
        {
            public CameraActions(
                InputActionReference keyboardPan,
                InputActionReference dragPan,
                InputActionReference pointerDelta,
                InputActionReference zoom)
            {
                KeyboardPan = keyboardPan;
                DragPan = dragPan;
                PointerDelta = pointerDelta;
                Zoom = zoom;
            }

            public InputActionReference KeyboardPan { get; }

            public InputActionReference DragPan { get; }

            public InputActionReference PointerDelta { get; }

            public InputActionReference Zoom { get; }
        }

        private readonly struct ObjectActions
        {
            public ObjectActions(
                InputActionReference point,
                InputActionReference select,
                InputActionReference cancel,
                InputActionReference rotate,
                InputActionReference flip)
            {
                Point = point;
                Select = select;
                Cancel = cancel;
                Rotate = rotate;
                Flip = flip;
            }

            public InputActionReference Point { get; }

            public InputActionReference Select { get; }

            public InputActionReference Cancel { get; }

            public InputActionReference Rotate { get; }

            public InputActionReference Flip { get; }
        }

        private readonly struct ExternalLifecycleState
        {
            public ExternalLifecycleState(
                bool frameCoordinatorEnabled,
                bool cameraAdapterExternallyDriven,
                bool objectAdapterExternallyDriven,
                bool cameraAdapterExternallyDrivenByFrameCoordinator,
                bool objectAdapterExternallyDrivenByFrameCoordinator)
            {
                FrameCoordinatorEnabled = frameCoordinatorEnabled;
                CameraAdapterExternallyDriven = cameraAdapterExternallyDriven;
                ObjectAdapterExternallyDriven = objectAdapterExternallyDriven;
                CameraAdapterExternallyDrivenByFrameCoordinator = cameraAdapterExternallyDrivenByFrameCoordinator;
                ObjectAdapterExternallyDrivenByFrameCoordinator = objectAdapterExternallyDrivenByFrameCoordinator;
            }

            public bool FrameCoordinatorEnabled { get; }

            public bool CameraAdapterExternallyDriven { get; }

            public bool ObjectAdapterExternallyDriven { get; }

            public bool CameraAdapterExternallyDrivenByFrameCoordinator { get; }

            public bool ObjectAdapterExternallyDrivenByFrameCoordinator { get; }
        }

        private sealed class PrototypeFixture
        {
            public PrototypeFixture(
                TabletopPrototypeComposition composition,
                Camera targetCamera,
                TabletopCameraController cameraController,
                TabletopCameraInputAdapter cameraAdapter,
                TabletopObjectInputAdapter objectAdapter,
                TabletopInputFrameCoordinator frameCoordinator,
                CardView cardView,
                PawnView pawnView,
                TokenView tokenView,
                TabletopSelectionVisual cardSelectionVisual,
                TabletopSelectionVisual pawnSelectionVisual,
                TabletopSelectionVisual tokenSelectionVisual,
                GameObject cardHighlightRoot,
                GameObject pawnHighlightRoot,
                GameObject tokenHighlightRoot,
                GameObject faceUpRoot,
                GameObject faceDownRoot)
            {
                Composition = composition;
                TargetCamera = targetCamera;
                CameraController = cameraController;
                CameraAdapter = cameraAdapter;
                ObjectAdapter = objectAdapter;
                FrameCoordinator = frameCoordinator;
                CardView = cardView;
                PawnView = pawnView;
                TokenView = tokenView;
                CardSelectionVisual = cardSelectionVisual;
                PawnSelectionVisual = pawnSelectionVisual;
                TokenSelectionVisual = tokenSelectionVisual;
                CardHighlightRoot = cardHighlightRoot;
                PawnHighlightRoot = pawnHighlightRoot;
                TokenHighlightRoot = tokenHighlightRoot;
                FaceUpRoot = faceUpRoot;
                FaceDownRoot = faceDownRoot;
            }

            public TabletopPrototypeComposition Composition { get; }

            public Camera TargetCamera { get; }

            public TabletopCameraController CameraController { get; }

            public TabletopCameraInputAdapter CameraAdapter { get; }

            public TabletopObjectInputAdapter ObjectAdapter { get; }

            public TabletopInputFrameCoordinator FrameCoordinator { get; }

            public CardView CardView { get; }

            public PawnView PawnView { get; }

            public TokenView TokenView { get; }

            public TabletopSelectionVisual CardSelectionVisual { get; }

            public TabletopSelectionVisual PawnSelectionVisual { get; }

            public TabletopSelectionVisual TokenSelectionVisual { get; }

            public GameObject CardHighlightRoot { get; }

            public GameObject PawnHighlightRoot { get; }

            public GameObject TokenHighlightRoot { get; }

            public GameObject FaceUpRoot { get; }

            public GameObject FaceDownRoot { get; }

            public void ApplySharedFrame(TabletopInputFrame frame)
            {
                FrameCoordinator.ApplyInputFrame(frame, DeltaTime);
            }

            public TabletopInputFrame CreatePressFrame(TabletopObjectView view)
            {
                return CreateFrame(ScreenPointFor(view), selectPressedThisFrame: true);
            }

            public TabletopInputFrame CreateEmptyPressFrame()
            {
                return CreateFrame(ScreenPointForWorld(7f, 7f), selectPressedThisFrame: true);
            }

            public TabletopInputFrame CreateStableScrollFrame(TabletopObjectView view)
            {
                return CreateFrame(ScreenPointFor(view), scrollDelta: 100f, rotateDelta: 100f);
            }

            public TabletopInputFrame CreateFlipFrame(TabletopObjectView view)
            {
                return CreateFrame(ScreenPointFor(view), flipPressedThisFrame: true);
            }

            public Vector2 ScreenPointFor(TabletopObjectView view)
            {
                return ScreenPointForWorld(view.transform.position.x, view.transform.position.z);
            }

            public Vector2 ScreenPointForWorld(float x, float z)
            {
                Physics.SyncTransforms();
                Vector3 screenPoint = TargetCamera.WorldToScreenPoint(new Vector3(x, 0f, z));
                Assert.That(IsFinite(screenPoint.x), Is.True);
                Assert.That(IsFinite(screenPoint.y), Is.True);
                return new Vector2(screenPoint.x, screenPoint.y);
            }

            public TabletopObjectView ViewFor(TabletopObjectKind kind)
            {
                switch (kind)
                {
                    case TabletopObjectKind.Card:
                        return CardView;
                    case TabletopObjectKind.Pawn:
                        return PawnView;
                    case TabletopObjectKind.Token:
                        return TokenView;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported object kind.");
                }
            }

            public TabletopObjectState StateFor(TabletopObjectKind kind)
            {
                switch (kind)
                {
                    case TabletopObjectKind.Card:
                        return Composition.CardState.BaseState;
                    case TabletopObjectKind.Pawn:
                        return Composition.PawnState.BaseState;
                    case TabletopObjectKind.Token:
                        return Composition.TokenState.BaseState;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported object kind.");
                }
            }

            public void RemoveReference(MissingReference missingReference)
            {
                switch (missingReference)
                {
                    case MissingReference.TargetCamera:
                        Composition.targetCamera = null;
                        break;
                    case MissingReference.CameraInputAdapter:
                        Composition.cameraInputAdapter = null;
                        break;
                    case MissingReference.ObjectInputAdapter:
                        Composition.objectInputAdapter = null;
                        break;
                    case MissingReference.InputFrameCoordinator:
                        Composition.inputFrameCoordinator = null;
                        break;
                    case MissingReference.CardView:
                        Composition.cardView = null;
                        break;
                    case MissingReference.PawnView:
                        Composition.pawnView = null;
                        break;
                    case MissingReference.TokenView:
                        Composition.tokenView = null;
                        break;
                    case MissingReference.CardSelectionVisual:
                        Composition.cardSelectionVisual = null;
                        break;
                    case MissingReference.CardHighlightRoot:
                        Composition.cardHighlightRoot = null;
                        break;
                    case MissingReference.PawnSelectionVisual:
                        Composition.pawnSelectionVisual = null;
                        break;
                    case MissingReference.PawnHighlightRoot:
                        Composition.pawnHighlightRoot = null;
                        break;
                    case MissingReference.TokenSelectionVisual:
                        Composition.tokenSelectionVisual = null;
                        break;
                    case MissingReference.TokenHighlightRoot:
                        Composition.tokenHighlightRoot = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(missingReference), missingReference, "Unsupported missing reference.");
                }
            }

            public void AssertSelectionVisualsUnconfigured()
            {
                Assert.That(CardSelectionVisual.IsConfigured, Is.False);
                Assert.That(PawnSelectionVisual.IsConfigured, Is.False);
                Assert.That(TokenSelectionVisual.IsConfigured, Is.False);
            }

            public void AssertAllHighlightsInactive()
            {
                Assert.That(CardHighlightRoot.activeSelf, Is.False);
                Assert.That(PawnHighlightRoot.activeSelf, Is.False);
                Assert.That(TokenHighlightRoot.activeSelf, Is.False);
            }

            public void AssertCompositionAssignedValidHighlightsInactive()
            {
                AssertAssignedValidHighlightInactive(Composition.cardHighlightRoot, CardView);
                AssertAssignedValidHighlightInactive(Composition.pawnHighlightRoot, PawnView);
                AssertAssignedValidHighlightInactive(Composition.tokenHighlightRoot, TokenView);
            }

            public void AssertConfiguredBeforeFailureHighlightsInactive(InvalidConfiguration invalidConfiguration)
            {
                switch (invalidConfiguration)
                {
                    case InvalidConfiguration.InvalidPawnHighlightRoot:
                        Assert.That(CardHighlightRoot.activeSelf, Is.False);
                        break;
                    case InvalidConfiguration.InvalidTokenHighlightRoot:
                        Assert.That(CardHighlightRoot.activeSelf, Is.False);
                        Assert.That(PawnHighlightRoot.activeSelf, Is.False);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(invalidConfiguration),
                            invalidConfiguration,
                            "Unsupported partial selection-visual configuration failure.");
                }
            }

            public void AssertOnlyHighlightActive(TabletopObjectKind kind)
            {
                Assert.That(CardHighlightRoot.activeSelf, Is.EqualTo(kind == TabletopObjectKind.Card));
                Assert.That(PawnHighlightRoot.activeSelf, Is.EqualTo(kind == TabletopObjectKind.Pawn));
                Assert.That(TokenHighlightRoot.activeSelf, Is.EqualTo(kind == TabletopObjectKind.Token));
            }

            public void SetAllHighlightsActive()
            {
                CardHighlightRoot.SetActive(true);
                PawnHighlightRoot.SetActive(true);
                TokenHighlightRoot.SetActive(true);
            }

            public void AssertNoRuntimeGraphPublished()
            {
                Assert.That(Composition.IsInitialized, Is.False);
                Assert.That(Composition.MatchState, Is.Null);
                Assert.That(Composition.SelectionState, Is.Null);
                Assert.That(Composition.MoveCoordinator, Is.Null);
                Assert.That(Composition.InputRoutingPolicy, Is.Null);
                Assert.That(Composition.SelectionPresenter, Is.Null);
                Assert.That(Composition.LocalPlayerId, Is.EqualTo(PlayerId.Empty));
            }

            public void AssertFailedInitializationCleanup()
            {
                AssertCommonFailedInitializationCleanup();
                Assert.That(CardView.IsBound, Is.False);
                Assert.That(PawnView.IsBound, Is.False);
                Assert.That(TokenView.IsBound, Is.False);
                Assert.That(ObjectAdapter.IsInitialized, Is.False);
                Assert.That(CameraAdapter.HasScrollRoutingPolicy, Is.False);
                Assert.That(CameraAdapter.IsExternallyDriven, Is.False);
                Assert.That(ObjectAdapter.IsExternallyDriven, Is.False);
            }

            public ExternalLifecycleState CaptureExternalLifecycleState()
            {
                return new ExternalLifecycleState(
                    FrameCoordinator.enabled,
                    CameraAdapter.IsExternallyDriven,
                    ObjectAdapter.IsExternallyDriven,
                    CameraAdapter.IsExternallyDrivenBy(FrameCoordinator),
                    ObjectAdapter.IsExternallyDrivenBy(FrameCoordinator));
            }

            public void AssertFailedInitializationCleanup(
                InvalidConfiguration invalidConfiguration,
                ExternalLifecycleState externalLifecycleBeforeInitialize)
            {
                AssertCommonFailedInitializationCleanup();
                AssertExternalLifecycleState(externalLifecycleBeforeInitialize);

                if (invalidConfiguration != InvalidConfiguration.PreBoundCardView)
                {
                    Assert.That(CardView.IsBound, Is.False);
                }

                if (invalidConfiguration != InvalidConfiguration.PreBoundPawnView)
                {
                    Assert.That(PawnView.IsBound, Is.False);
                }

                if (invalidConfiguration != InvalidConfiguration.PreBoundTokenView)
                {
                    Assert.That(TokenView.IsBound, Is.False);
                }

                if (invalidConfiguration != InvalidConfiguration.PreInitializedObjectAdapter)
                {
                    Assert.That(ObjectAdapter.IsInitialized, Is.False);
                }

                if (invalidConfiguration != InvalidConfiguration.ExistingCameraScrollPolicy)
                {
                    Assert.That(CameraAdapter.HasScrollRoutingPolicy, Is.False);
                }

            }

            private static TabletopInputFrame CreateFrame(
                Vector2 screenPosition,
                bool selectPressedThisFrame = false,
                bool selectHeld = false,
                bool selectReleasedThisFrame = false,
                bool cancelPressedThisFrame = false,
                float scrollDelta = 0f,
                float rotateDelta = 0f,
                bool flipPressedThisFrame = false)
            {
                return new TabletopInputFrame(
                    Vector2.zero,
                    false,
                    Vector2.zero,
                    scrollDelta,
                    screenPosition,
                    selectPressedThisFrame,
                    selectHeld,
                    selectReleasedThisFrame,
                    cancelPressedThisFrame,
                    rotateDelta,
                    flipPressedThisFrame);
            }

            private static bool IsFinite(float value)
            {
                return !float.IsNaN(value) && !float.IsInfinity(value);
            }

            private void AssertCommonFailedInitializationCleanup()
            {
                Assert.That(Composition.IsInitialized, Is.False);
                Assert.That(Composition.SelectionPresenter, Is.Null);
                Assert.That(FrameCoordinator.HasSelectionPresenter, Is.False);
                AssertSelectionVisualsUnconfigured();
                AssertNoRuntimeGraphPublished();
                Assert.That(Composition.LockService, Is.Null);
                Assert.That(Composition.PreviewSession, Is.Null);
            }

            private void AssertExternalLifecycleState(ExternalLifecycleState expected)
            {
                Assert.That(FrameCoordinator.enabled, Is.EqualTo(expected.FrameCoordinatorEnabled));
                Assert.That(CameraAdapter.IsExternallyDriven, Is.EqualTo(expected.CameraAdapterExternallyDriven));
                Assert.That(ObjectAdapter.IsExternallyDriven, Is.EqualTo(expected.ObjectAdapterExternallyDriven));
                Assert.That(
                    CameraAdapter.IsExternallyDrivenBy(FrameCoordinator),
                    Is.EqualTo(expected.CameraAdapterExternallyDrivenByFrameCoordinator));
                Assert.That(
                    ObjectAdapter.IsExternallyDrivenBy(FrameCoordinator),
                    Is.EqualTo(expected.ObjectAdapterExternallyDrivenByFrameCoordinator));
            }

            private static void AssertAssignedValidHighlightInactive(
                GameObject assignedHighlightRoot,
                TabletopObjectView ownerView)
            {
                if (!IsValidAssignedHighlightRoot(assignedHighlightRoot, ownerView))
                {
                    return;
                }

                Assert.That(assignedHighlightRoot.activeSelf, Is.False);
            }

            private static bool IsValidAssignedHighlightRoot(
                GameObject assignedHighlightRoot,
                TabletopObjectView ownerView)
            {
                if (assignedHighlightRoot == null
                    || ownerView == null
                    || ReferenceEquals(assignedHighlightRoot, ownerView.gameObject)
                    || !assignedHighlightRoot.transform.IsChildOf(ownerView.transform))
                {
                    return false;
                }

                return !ContainsTabletopObjectView(assignedHighlightRoot.transform);
            }

            private static bool ContainsTabletopObjectView(Transform transform)
            {
                if (transform.GetComponent<TabletopObjectView>() != null)
                {
                    return true;
                }

                for (int i = 0; i < transform.childCount; i++)
                {
                    if (ContainsTabletopObjectView(transform.GetChild(i)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void ApplyInvalidConfiguration(PrototypeFixture fixture, InvalidConfiguration invalidConfiguration)
        {
            switch (invalidConfiguration)
            {
                case InvalidConfiguration.PerspectiveCamera:
                    fixture.TargetCamera.orthographic = false;
                    break;
                case InvalidConfiguration.ZeroMaximumHitDistance:
                    fixture.Composition.maximumHitDistance = 0f;
                    break;
                case InvalidConfiguration.NaNMaximumHitDistance:
                    fixture.Composition.maximumHitDistance = float.NaN;
                    break;
                case InvalidConfiguration.NegativeDragThreshold:
                    fixture.Composition.dragThresholdPixels = -1f;
                    break;
                case InvalidConfiguration.NaNDragThreshold:
                    fixture.Composition.dragThresholdPixels = float.NaN;
                    break;
                case InvalidConfiguration.ZeroWorldScale:
                    fixture.Composition.worldUnitsPerTableUnit = 0f;
                    break;
                case InvalidConfiguration.NaNWorldScale:
                    fixture.Composition.worldUnitsPerTableUnit = float.NaN;
                    break;
                case InvalidConfiguration.NaNTabletopHeight:
                    fixture.Composition.tabletopHeight = float.NaN;
                    break;
                case InvalidConfiguration.DuplicateCardPawnViews:
                    fixture.Composition.pawnView = fixture.CardView.gameObject.AddComponent<PawnView>();
                    break;
                case InvalidConfiguration.DuplicateCardTokenViews:
                    fixture.Composition.tokenView = fixture.CardView.gameObject.AddComponent<TokenView>();
                    break;
                case InvalidConfiguration.DuplicatePawnTokenViews:
                    fixture.Composition.tokenView = fixture.PawnView.gameObject.AddComponent<TokenView>();
                    break;
                case InvalidConfiguration.PreBoundCardView:
                    fixture.CardView.Bind(
                        new CardInstanceState(CreateBaseState(TabletopObjectKind.Card, 501), CardFace.FaceUp),
                        new TabletopCoordinateConverter(1f, 0f, 0f, 0f));
                    break;
                case InvalidConfiguration.PreBoundPawnView:
                    fixture.PawnView.Bind(
                        new PawnState(CreateBaseState(TabletopObjectKind.Pawn, 502)),
                        new TabletopCoordinateConverter(1f, 0f, 0f, 0f));
                    break;
                case InvalidConfiguration.PreBoundTokenView:
                    fixture.TokenView.Bind(
                        new TokenState(CreateBaseState(TabletopObjectKind.Token, 503)),
                        new TabletopCoordinateConverter(1f, 0f, 0f, 0f));
                    break;
                case InvalidConfiguration.PreInitializedObjectAdapter:
                    InitializeObjectAdapterWithManualGraph(fixture);
                    break;
                case InvalidConfiguration.ExistingCameraScrollPolicy:
                    fixture.CameraAdapter.ConfigureScrollRoutingPolicy(CreateManualRoutingPolicy(fixture));
                    break;
                case InvalidConfiguration.EnabledFrameCoordinator:
                    fixture.FrameCoordinator.enabled = true;
                    break;
                case InvalidConfiguration.ExistingExternalFrameDriver:
                    fixture.CameraAdapter.AttachExternalFrameDriver(
                        CreateDetachedFrameDriver(fixture.CameraAdapter, fixture.ObjectAdapter));
                    break;
                case InvalidConfiguration.CoordinatorReferencesDifferentAdapters:
                    fixture.FrameCoordinator.objectInputAdapter = null;
                    break;
                case InvalidConfiguration.CardVisualOnWrongView:
                    fixture.Composition.cardSelectionVisual = fixture.PawnSelectionVisual;
                    break;
                case InvalidConfiguration.PawnVisualOnWrongView:
                    fixture.Composition.pawnSelectionVisual = fixture.TokenSelectionVisual;
                    break;
                case InvalidConfiguration.TokenVisualOnWrongView:
                    fixture.Composition.tokenSelectionVisual = fixture.CardSelectionVisual;
                    break;
                case InvalidConfiguration.InvalidCardHighlightRoot:
                    fixture.Composition.cardHighlightRoot = CreateGameObject("Invalid Card Highlight");
                    break;
                case InvalidConfiguration.InvalidPawnHighlightRoot:
                    fixture.Composition.pawnHighlightRoot = CreateGameObject("Invalid Pawn Highlight");
                    break;
                case InvalidConfiguration.InvalidTokenHighlightRoot:
                    fixture.Composition.tokenHighlightRoot = CreateGameObject("Invalid Token Highlight");
                    break;
                case InvalidConfiguration.DuplicateSelectionVisual:
                    fixture.Composition.pawnSelectionVisual = fixture.CardSelectionVisual;
                    break;
                case InvalidConfiguration.DuplicateHighlightRoot:
                    fixture.Composition.pawnHighlightRoot = fixture.CardHighlightRoot;
                    break;
                case InvalidConfiguration.ExistingFrameSelectionPresenter:
                    CardView externalCardView = CreateView<CardView>("External Presenter Card");
                    PawnView externalPawnView = CreateView<PawnView>("External Presenter Pawn");
                    TokenView externalTokenView = CreateView<TokenView>("External Presenter Token");
                    TabletopSelectionVisual externalCardVisual =
                        externalCardView.gameObject.AddComponent<TabletopSelectionVisual>();
                    TabletopSelectionVisual externalPawnVisual =
                        externalPawnView.gameObject.AddComponent<TabletopSelectionVisual>();
                    TabletopSelectionVisual externalTokenVisual =
                        externalTokenView.gameObject.AddComponent<TabletopSelectionVisual>();
                    externalCardVisual.Configure(externalCardView, CreateChild(externalCardView.gameObject, "External Card Highlight"));
                    externalPawnVisual.Configure(externalPawnView, CreateChild(externalPawnView.gameObject, "External Pawn Highlight"));
                    externalTokenVisual.Configure(externalTokenView, CreateChild(externalTokenView.gameObject, "External Token Highlight"));
                    fixture.FrameCoordinator.ConfigureSelectionPresenter(new TabletopSelectionPresenter(
                        new TabletopSelectionState(),
                        externalCardVisual,
                        externalPawnVisual,
                        externalTokenVisual));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidConfiguration), invalidConfiguration, "Unsupported invalid configuration.");
            }
        }
    }
}
