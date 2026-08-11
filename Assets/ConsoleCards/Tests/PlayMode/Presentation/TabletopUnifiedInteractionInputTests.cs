using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
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
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopUnifiedInteractionInputTests
    {
        private const int ObjectLayer = 8;
        private const int DropTargetLayer = 9;
        private const float ScrollDelta = 100f;
        private const float DeltaTime = 1f;
        private const float FloatTolerance = 0.0001f;
        private const double CoordinateTolerance = 0.00001d;

        private readonly List<GameObject> createdGameObjects = new List<GameObject>();
        private readonly List<InputActionAsset> createdInputAssets = new List<InputActionAsset>();
        private readonly List<InputActionReference> createdActionReferences = new List<InputActionReference>();

        [TearDown]
        public void TearDown()
        {
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
        public void AdapterRouterConfiguration_StartsWithoutRouterAndConfiguresExplicitly()
        {
            UnifiedFixture fixture = CreateFixture();

            Assert.That(fixture.ObjectAdapter.HasInteractionRouter, Is.True);
            Assert.That(fixture.ObjectAdapter.InteractionRouter, Is.SameAs(fixture.Router));
            fixture.ObjectAdapter.ClearInteractionRouter();
            Assert.That(fixture.ObjectAdapter.HasInteractionRouter, Is.False);
            fixture.ObjectAdapter.ClearInteractionRouter();
        }

        [Test]
        public void AdapterRouterConfiguration_RejectsNullSecondAndActiveClear()
        {
            UnifiedFixture fixture = CreateFixture(configureRouterOnAdapter: false);

            Assert.Throws<ArgumentNullException>(() => fixture.ObjectAdapter.ConfigureInteractionRouter(null));
            fixture.ObjectAdapter.ConfigureInteractionRouter(fixture.Router);
            Assert.Throws<InvalidOperationException>(() => fixture.ObjectAdapter.ConfigureInteractionRouter(fixture.Router));

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.ContainedScreenPoint,
                selectPressedThisFrame: true));

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
            Assert.Throws<InvalidOperationException>(() => fixture.ObjectAdapter.ClearInteractionRouter());
        }

        [Test]
        public void Shutdown_WhenRoutedInteractionIsActive_ResetsRouterAndClearsRoutedRelease()
        {
            UnifiedFixture fixture = CreateFixture();
            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.ContainedScreenPoint,
                selectPressedThisFrame: true));

            fixture.ObjectAdapter.Shutdown();

            Assert.That(fixture.Router.HasActiveInteraction, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.HasInteractionRouter, Is.False);
            Assert.That(fixture.ObjectAdapter.LastInteractionReleaseResult.HasValue, Is.False);
        }

        [Test]
        public void RoutingPolicy_WhenRouterIsActive_SuppressesScroll()
        {
            UnifiedFixture fixture = CreateFixture();
            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.ContainedScreenPoint,
                selectPressedThisFrame: true));

            Assert.That(fixture.RoutingPolicy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.Suppressed));
        }

        [Test]
        public void RoutingPolicy_WhenSelectedCardIsContained_SuppressesScroll()
        {
            UnifiedFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.ContainedCardView);

            Assert.That(fixture.RoutingPolicy.ResolveScrollRoute(), Is.EqualTo(TabletopScrollInputRoute.Suppressed));
        }

        [TestCase(ContainerKind.Deck)]
        [TestCase(ContainerKind.Stack)]
        [TestCase(ContainerKind.Hand)]
        [TestCase(ContainerKind.DiscardPile)]
        [TestCase(ContainerKind.ConsoleSlot)]
        public void SharedFrame_SelectPressOnContainedCard_StartsContainedRoute(ContainerKind sourceKind)
        {
            UnifiedFixture fixture = CreateFixture(sourceKind: sourceKind);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.ContainedScreenPoint,
                selectPressedThisFrame: true,
                rotateDelta: ScrollDelta,
                scrollDelta: ScrollDelta,
                flipPressedThisFrame: true));

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
            Assert.That(fixture.ContainedCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed));
            Assert.That(fixture.MoveCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.ContainedCardView));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [Test]
        public void SharedFrame_ContainedDragReleaseToContainer_AcceptsOneTransferAndStoresRoutedResult()
        {
            UnifiedFixture fixture = CreateFixture(sourceKind: ContainerKind.Deck, destinationKind: ContainerKind.Hand);

            BeginContainedDrag(fixture);
            MoveInteractionReleaseResult? legacyResult = fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.DestinationScreenPoint,
                selectReleasedThisFrame: true,
                scrollDelta: ScrollDelta,
                rotateDelta: ScrollDelta));

            Assert.That(legacyResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastInteractionReleaseResult.HasValue, Is.True);
            Assert.That(fixture.ObjectAdapter.LastInteractionReleaseResult.Value.Route, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
            Assert.That(fixture.ObjectAdapter.LastInteractionReleaseResult.Value.ContainedCardResult.Value.Status, Is.EqualTo(ContainedCardDragReleaseStatus.TransferAccepted));
            Assert.That(fixture.ObjectAdapter.LastReleaseResult.HasValue, Is.False);
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.SourceContainer.Contains(fixture.ContainedCard.BaseState.Id), Is.False);
            Assert.That(fixture.DestinationContainer.GetObjectAt(fixture.DestinationContainer.Count - 1), Is.EqualTo(fixture.ContainedCard.BaseState.Id));
            Assert.That(fixture.ContainedCard.BaseState.ContainerId, Is.EqualTo(fixture.DestinationContainer.Id));
            Assert.That(fixture.SourceLayout.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.DestinationLayout.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.Router.HasActiveInteraction, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
        }

        [Test]
        public void SharedFrame_ContainedDragReleaseToTabletop_AcceptsTransferAndPreservesPoseFields()
        {
            UnifiedFixture fixture = CreateFixture(sourceKind: ContainerKind.Stack, destinationKind: ContainerKind.Hand);
            TabletopPose acceptedPose = fixture.ContainedCard.BaseState.Pose;

            BeginContainedDrag(fixture);
            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.TabletopReleaseScreenPoint,
                selectReleasedThisFrame: true));

            Assert.That(fixture.ObjectAdapter.LastInteractionReleaseResult.Value.ContainedCardResult.Value.Status, Is.EqualTo(ContainedCardDragReleaseStatus.TransferAccepted));
            Assert.That(fixture.ContainedCard.BaseState.ContainerId, Is.EqualTo(ContainerId.Empty));
            AssertCoordinate(fixture.ContainedCard.BaseState.Pose.Position, 6.0, -5.0);
            Assert.That(fixture.ContainedCard.BaseState.Pose.RotationDegrees, Is.EqualTo(acceptedPose.RotationDegrees));
            Assert.That(fixture.ContainedCard.BaseState.Pose.Layer, Is.EqualTo(acceptedPose.Layer));
            Assert.That(fixture.ContainedCard.BaseState.Pose.LocalOrder, Is.EqualTo(acceptedPose.LocalOrder));
            Assert.That(fixture.ContainedCardView.IsContainerLayoutApplied, Is.False);
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
        }

        [Test]
        public void SharedFrame_ContainedDragReleaseToSameSource_CancelsWithoutTransfer()
        {
            UnifiedFixture fixture = CreateFixture(sourceKind: ContainerKind.Deck, destinationKind: ContainerKind.Hand);

            BeginContainedDrag(fixture);
            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.SourceScreenPoint,
                selectReleasedThisFrame: true));

            Assert.That(fixture.ObjectAdapter.LastInteractionReleaseResult.Value.ContainedCardResult.Value.Status, Is.EqualTo(ContainedCardDragReleaseStatus.SameSource));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.SourceContainer.Contains(fixture.ContainedCard.BaseState.Id), Is.True);
            Assert.That(fixture.ContainedCard.BaseState.ContainerId, Is.EqualTo(fixture.SourceContainer.Id));
            Assert.That(fixture.SourceLayout.ApplyCount, Is.EqualTo(1));
            Assert.That(fixture.DestinationLayout.ApplyCount, Is.EqualTo(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SharedFrame_ContainedCancel_CleansLifecycleWithoutDiscreteActions(bool dragging)
        {
            UnifiedFixture fixture = CreateFixture();
            if (dragging)
            {
                BeginContainedDrag(fixture);
            }
            else
            {
                fixture.ApplySharedFrame(fixture.CreateFrame(
                    screenPosition: fixture.ContainedScreenPoint,
                    selectPressedThisFrame: true));
            }

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: dragging ? fixture.DragScreenPoint : fixture.ContainedScreenPoint,
                selectHeld: true,
                cancelPressedThisFrame: true,
                rotateDelta: ScrollDelta,
                scrollDelta: ScrollDelta,
                flipPressedThisFrame: true));

            Assert.That(fixture.Router.HasActiveInteraction, Is.False);
            Assert.That(fixture.ContainedCoordinator.HasActiveInteraction, Is.False);
            Assert.That(fixture.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
        }

        [Test]
        public void SharedFrame_SelectedContainedCardSuppressesCameraRotateAndFlip()
        {
            UnifiedFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.ContainedCardView);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.ContainedScreenPoint,
                scrollDelta: ScrollDelta,
                rotateDelta: ScrollDelta,
                flipPressedThisFrame: true));

            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ContainedCard.BaseState.Pose.RotationDegrees, Is.EqualTo(20f).Within(FloatTolerance));
            Assert.That(fixture.CardState.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
        }

        [Test]
        public void SharedFrame_ActiveContainedDragSuppressesCameraRotateAndFlip()
        {
            UnifiedFixture fixture = CreateFixture();
            BeginContainedDrag(fixture);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.DragScreenPoint,
                selectHeld: true,
                scrollDelta: ScrollDelta,
                rotateDelta: ScrollDelta,
                flipPressedThisFrame: true));

            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False);
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
        }

        [Test]
        public void SharedFrame_TabletopCardUsesMoveRouteAndStoresLegacyAndRoutedMoveResults()
        {
            UnifiedFixture fixture = CreateFixture();

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.TabletopCardScreenPoint,
                selectPressedThisFrame: true));
            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.TabletopDragScreenPoint,
                selectHeld: true));
            MoveInteractionReleaseResult? legacyResult = fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.TabletopReleaseScreenPoint,
                selectReleasedThisFrame: true));

            Assert.That(legacyResult.HasValue, Is.True);
            Assert.That(legacyResult.Value.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveAccepted));
            Assert.That(fixture.ObjectAdapter.LastReleaseResult, Is.EqualTo(legacyResult));
            Assert.That(fixture.ObjectAdapter.LastInteractionReleaseResult.Value.Route, Is.EqualTo(TabletopInteractionRoute.TabletopMove));
            Assert.That(fixture.ObjectAdapter.LastInteractionReleaseResult.Value.MoveResult.Value, Is.EqualTo(legacyResult.Value));
            Assert.That(fixture.TabletopCard.BaseState.ContainerId, Is.EqualTo(ContainerId.Empty));
            AssertCoordinate(fixture.TabletopCard.BaseState.Pose.Position, 6.0, -5.0);
        }

        [Test]
        public void SharedFrame_NoSelectionScrollZoomsCamera()
        {
            UnifiedFixture fixture = CreateFixture();
            fixture.SelectionState.ClearSelection();
            float expectedSize = fixture.CameraController.State.OrthographicSize
                - (ScrollDelta * fixture.CameraAdapter.ZoomSensitivity);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.EmptyScreenPoint,
                scrollDelta: ScrollDelta,
                rotateDelta: ScrollDelta));

            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(expectedSize).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
        }

        [Test]
        public void SharedFrame_SelectedTabletopCardScrollRotatesOneStepAndSuppressesCamera()
        {
            UnifiedFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.TabletopCardView);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.TabletopCardScreenPoint,
                scrollDelta: ScrollDelta,
                rotateDelta: ScrollDelta));

            Assert.That(fixture.TabletopCard.BaseState.Pose.RotationDegrees, Is.EqualTo(30f).Within(FloatTolerance));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.Value.Status, Is.EqualTo(RotationInteractionStatus.RotationAccepted));
        }

        [Test]
        public void SharedFrame_TabletopCardFlipKeepsPriorityOverRotate()
        {
            UnifiedFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.TabletopCardView);

            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.TabletopCardScreenPoint,
                scrollDelta: ScrollDelta,
                rotateDelta: ScrollDelta,
                flipPressedThisFrame: true));

            Assert.That(fixture.TabletopCardState.Face, Is.EqualTo(CardFace.FaceDown));
            Assert.That(fixture.TabletopCard.BaseState.Pose.RotationDegrees, Is.EqualTo(15f).Within(FloatTolerance));
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(fixture.ObjectAdapter.LastFlipResult.Value.Status, Is.EqualTo(FlipInteractionStatus.FlipAccepted));
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False);
        }

        [TestCase(FixtureSetupOrder.CameraAdapterFirst)]
        [TestCase(FixtureSetupOrder.ObjectAdapterFirst)]
        public void SetupOrder_WhenContainedPressIsApplied_RouterReceivesPress(FixtureSetupOrder setupOrder)
        {
            UnifiedFixture fixture = CreateFixture(setupOrder: setupOrder);
            TabletopInputFrame frame = fixture.CreateFrame(
                screenPosition: fixture.ContainedScreenPoint,
                selectPressedThisFrame: true,
                scrollDelta: ScrollDelta,
                rotateDelta: ScrollDelta);

            Assert.That(fixture.FrameCoordinator.ObjectInputAdapter, Is.SameAs(fixture.ObjectAdapter));
            Assert.That(fixture.ObjectAdapter.InteractionRouter, Is.SameAs(fixture.Router));
            Assert.That(fixture.Router.HasActiveInteraction, Is.False);
            Assert.That(frame.SelectPressedThisFrame, Is.True);
            Assert.That(fixture.HitResolver.TryResolve(frame.ScreenPosition, out TabletopObjectView hitView), Is.True);
            Assert.That(hitView, Is.SameAs(fixture.ContainedCardView));

            fixture.ApplySharedFrame(frame);

            string outcome = DescribeSetupOrderOutcome(fixture);
            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag), outcome);
            Assert.That(fixture.ContainedCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed), outcome);
            Assert.That(fixture.MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle), outcome);
            Assert.That(fixture.SelectionState.SelectedObjectId, Is.EqualTo(fixture.ContainedCard.BaseState.Id), outcome);
            Assert.That(fixture.CameraController.State.OrthographicSize, Is.EqualTo(5f).Within(FloatTolerance), outcome);
            Assert.That(fixture.LockService.Count, Is.EqualTo(1), outcome);
            Assert.That(fixture.Match.Revision, Is.EqualTo(0), outcome);
            Assert.That(fixture.ObjectAdapter.LastRotationResult.HasValue, Is.False, outcome);
            Assert.That(fixture.ObjectAdapter.LastFlipResult.HasValue, Is.False, outcome);
            Assert.That(fixture.ContainedCardView.IsPreviewing, Is.False, outcome);
        }

        [Test]
        public void SetupOrder_WhenContainedPressAndScrollOccur_ProducesSameOutcome()
        {
            UnifiedFixture cameraFirst = CreateFixture(
                setupOrder: FixtureSetupOrder.CameraAdapterFirst,
                deterministicIds: true,
                tableOffset: Vector2.zero);
            UnifiedFixture objectFirst = CreateFixture(
                setupOrder: FixtureSetupOrder.ObjectAdapterFirst,
                deterministicIds: true,
                tableOffset: new Vector2(20f, 0f));
            TabletopInputFrame cameraFirstFrame = cameraFirst.CreateFrame(
                screenPosition: cameraFirst.ContainedScreenPoint,
                selectPressedThisFrame: true,
                scrollDelta: ScrollDelta,
                rotateDelta: ScrollDelta);
            TabletopInputFrame objectFirstFrame = objectFirst.CreateFrame(
                screenPosition: objectFirst.ContainedScreenPoint,
                selectPressedThisFrame: true,
                scrollDelta: ScrollDelta,
                rotateDelta: ScrollDelta);

            Assert.That(cameraFirst.FrameCoordinator.ObjectInputAdapter, Is.SameAs(cameraFirst.ObjectAdapter));
            Assert.That(objectFirst.FrameCoordinator.ObjectInputAdapter, Is.SameAs(objectFirst.ObjectAdapter));
            Assert.That(cameraFirst.ObjectAdapter.HasInteractionRouter, Is.True);
            Assert.That(objectFirst.ObjectAdapter.HasInteractionRouter, Is.True);
            Assert.That(cameraFirst.ObjectAdapter.InteractionRouter, Is.SameAs(cameraFirst.Router));
            Assert.That(objectFirst.ObjectAdapter.InteractionRouter, Is.SameAs(objectFirst.Router));
            Assert.That(cameraFirst.ObjectAdapter.IsInitialized, Is.True);
            Assert.That(objectFirst.ObjectAdapter.IsInitialized, Is.True);
            Assert.That(cameraFirst.ObjectAdapter.IsExternallyDriven, Is.True);
            Assert.That(objectFirst.ObjectAdapter.IsExternallyDriven, Is.True);
            Assert.That(cameraFirst.ObjectAdapter.IsExternallyDrivenBy(cameraFirst.FrameCoordinator), Is.True);
            Assert.That(objectFirst.ObjectAdapter.IsExternallyDrivenBy(objectFirst.FrameCoordinator), Is.True);
            Assert.That(cameraFirst.Router.HasActiveInteraction, Is.False);
            Assert.That(objectFirst.Router.HasActiveInteraction, Is.False);
            Assert.That(cameraFirstFrame.SelectPressedThisFrame, Is.True);
            Assert.That(objectFirstFrame.SelectPressedThisFrame, Is.True);
            Assert.That(cameraFirst.ContainedCard.BaseState.ContainerId.IsEmpty, Is.False);
            Assert.That(objectFirst.ContainedCard.BaseState.ContainerId.IsEmpty, Is.False);
            Assert.That(cameraFirst.HitResolver.TryResolve(cameraFirstFrame.ScreenPosition, out TabletopObjectView cameraFirstHit), Is.True);
            Assert.That(objectFirst.HitResolver.TryResolve(objectFirstFrame.ScreenPosition, out TabletopObjectView objectFirstHit), Is.True);
            Assert.That(cameraFirstHit, Is.SameAs(cameraFirst.ContainedCardView));
            Assert.That(objectFirstHit, Is.SameAs(objectFirst.ContainedCardView));

            cameraFirst.ApplySharedFrame(cameraFirstFrame);
            objectFirst.ApplySharedFrame(objectFirstFrame);

            string cameraFirstOutcome = DescribeSetupOrderOutcome(cameraFirst);
            string objectFirstOutcome = DescribeSetupOrderOutcome(objectFirst);

            Assert.That(cameraFirst.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag), cameraFirstOutcome);
            Assert.That(objectFirst.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag), objectFirstOutcome);
            Assert.That(cameraFirst.ContainedCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed), cameraFirstOutcome);
            Assert.That(objectFirst.ContainedCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Pressed), objectFirstOutcome);
            Assert.That(cameraFirst.MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle), cameraFirstOutcome);
            Assert.That(objectFirst.MoveCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.Idle), objectFirstOutcome);
            Assert.That(cameraFirst.SelectionState.SelectedObjectId, Is.EqualTo(cameraFirst.ContainedCard.BaseState.Id), cameraFirstOutcome);
            Assert.That(objectFirst.SelectionState.SelectedObjectId, Is.EqualTo(objectFirst.ContainedCard.BaseState.Id), objectFirstOutcome);
            Assert.That(cameraFirst.ObjectAdapter.LastRotationResult.HasValue, Is.False, cameraFirstOutcome);
            Assert.That(objectFirst.ObjectAdapter.LastRotationResult.HasValue, Is.False, objectFirstOutcome);
            Assert.That(cameraFirst.ObjectAdapter.LastFlipResult.HasValue, Is.False, cameraFirstOutcome);
            Assert.That(objectFirst.ObjectAdapter.LastFlipResult.HasValue, Is.False, objectFirstOutcome);

            Assert.That(cameraFirst.CameraController.State.OrthographicSize, Is.EqualTo(objectFirst.CameraController.State.OrthographicSize).Within(FloatTolerance));
            Assert.That(cameraFirst.Router.ActiveRoute, Is.EqualTo(objectFirst.Router.ActiveRoute));
            Assert.That(cameraFirst.ContainedCoordinator.Phase, Is.EqualTo(objectFirst.ContainedCoordinator.Phase));
            Assert.That(cameraFirst.MoveCoordinator.Phase, Is.EqualTo(objectFirst.MoveCoordinator.Phase));
            Assert.That(cameraFirst.LockService.Count, Is.EqualTo(objectFirst.LockService.Count));
            Assert.That(cameraFirst.Match.Revision, Is.EqualTo(objectFirst.Match.Revision));
            Assert.That(cameraFirst.ContainedCard.BaseState.ContainerId, Is.EqualTo(objectFirst.ContainedCard.BaseState.ContainerId));
            Assert.That(cameraFirst.SourceContainer.ObjectIds, Is.EqualTo(objectFirst.SourceContainer.ObjectIds));
            Assert.That(cameraFirst.DestinationContainer.ObjectIds, Is.EqualTo(objectFirst.DestinationContainer.ObjectIds));
            Assert.That(cameraFirst.SelectionState.SelectedObjectId, Is.EqualTo(objectFirst.SelectionState.SelectedObjectId));
            Assert.That(cameraFirst.ObjectAdapter.LastRotationResult.HasValue, Is.EqualTo(objectFirst.ObjectAdapter.LastRotationResult.HasValue));
            Assert.That(cameraFirst.ObjectAdapter.LastFlipResult.HasValue, Is.EqualTo(objectFirst.ObjectAdapter.LastFlipResult.HasValue));
        }

        [Test]
        public void StaticBoundaries_DoNotIntroduceForbiddenInputOrScenePatterns()
        {
            string[] files =
            {
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Input", "TabletopObjectInputAdapter.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Input", "TabletopInputFrameCoordinator.cs"),
                Path.Combine("Assets", "ConsoleCards", "Presentation", "Input", "TabletopInteractionInputRoutingPolicy.cs")
            };

            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                Assert.That(text, Does.Not.Contain("FindObjectOfType"));
                Assert.That(text, Does.Not.Contain("FindObjectsByType"));
                Assert.That(text, Does.Not.Contain("Camera.main"));
                Assert.That(text, Does.Not.Contain("PlayerInput"));
                Assert.That(text, Does.Not.Contain("new TransferCardCommand"));
                Assert.That(text, Does.Not.Contain("new MoveObjectCommand"));
            }
        }

        private static void BeginContainedDrag(UnifiedFixture fixture)
        {
            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.ContainedScreenPoint,
                selectPressedThisFrame: true));
            fixture.ApplySharedFrame(fixture.CreateFrame(
                screenPosition: fixture.DragScreenPoint,
                selectHeld: true));

            Assert.That(fixture.Router.ActiveRoute, Is.EqualTo(TabletopInteractionRoute.ContainedCardDrag));
            Assert.That(fixture.ContainedCoordinator.Phase, Is.EqualTo(TabletopInteractionPhase.DraggingObject));
            Assert.That(fixture.ContainedCardView.IsPreviewing, Is.True);
        }

        private UnifiedFixture CreateFixture(
            ContainerKind sourceKind = ContainerKind.Deck,
            ContainerKind destinationKind = ContainerKind.Hand,
            bool configureRouterOnAdapter = true,
            FixtureSetupOrder setupOrder = FixtureSetupOrder.CameraAdapterFirst,
            bool deterministicIds = false,
            Vector2? tableOffset = null)
        {
            Vector2 offset = tableOffset ?? Vector2.zero;
            TabletopCoordinateConverter converter = CreateConverter();
            UnityEngine.Camera pointerCamera = CreateTopDownCamera();

            ContainerState sourceContainer = CreateContainer(sourceKind, deterministicIds ? 10 : (int?)null);
            ContainerState destinationContainer = CreateContainer(destinationKind, deterministicIds ? 11 : (int?)null);
            CardInstanceState containedCardState = CreateCard(1, CreatePose(offset.x, offset.y, 20f));
            CardInstanceState tabletopCardState = CreateCard(2, CreatePose(offset.x + 3.0, offset.y, 15f));
            ContainerTransferService transferService = new ContainerTransferService();
            Assert.That(transferService.PlaceIntoContainer(containedCardState.BaseState, sourceContainer).Succeeded, Is.True);

            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                0,
                new[] { containedCardState, tabletopCardState },
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                new[] { sourceContainer, destinationContainer },
                Array.Empty<SeatState>());

            CardView containedCardView = CreateCardView("Contained Card", containedCardState, converter);
            CardView tabletopCardView = CreateCardView("Tabletop Card", tabletopCardState, converter);
            AddObjectCollider(containedCardView.gameObject);
            AddObjectCollider(tabletopCardView.gameObject);

            Component sourceViewComponent = CreateLayoutView(
                sourceContainer,
                converter,
                new[] { containedCardView },
                new Vector3(offset.x, 0f, offset.y));
            Component destinationViewComponent = CreateLayoutView(
                destinationContainer,
                converter,
                new[] { containedCardView },
                new Vector3(offset.x - 3f, 0f, offset.y - 3f));
            CountingLayoutView sourceLayout = new CountingLayoutView((IContainerLayoutView)sourceViewComponent);
            CountingLayoutView destinationLayout = new CountingLayoutView((IContainerLayoutView)destinationViewComponent);
            AddDropTarget(sourceViewComponent);
            AddDropTarget(destinationViewComponent);

            TabletopSelectionState selectionState = new TabletopSelectionState();
            TabletopObjectHitResolver hitResolver = new TabletopObjectHitResolver(pointerCamera, LayerMaskFor(ObjectLayer), 100f);
            TabletopPointerProjector pointerProjector = new TabletopPointerProjector(pointerCamera, converter, 0f);
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            PlayerId playerId = PlayerId.New();
            InteractionOwnerId moveOwnerId = InteractionOwnerId.New();
            InteractionOwnerId containedOwnerId = InteractionOwnerId.New();

            TabletopMoveInteractionCoordinator moveCoordinator = new TabletopMoveInteractionCoordinator(
                match,
                playerId,
                moveOwnerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                new TabletopInteractionStateMachine(5f),
                new TabletopDragPreviewSession(),
                new MoveObjectUseCase());
            TabletopRotationCoordinator rotationCoordinator = new TabletopRotationCoordinator(
                match,
                playerId,
                moveOwnerId,
                selectionState,
                lockService,
                new RotateObjectUseCase());
            TabletopCardFlipCoordinator flipCoordinator = new TabletopCardFlipCoordinator(
                match,
                playerId,
                moveOwnerId,
                selectionState,
                lockService,
                new FlipCardUseCase());

            IContainerLayoutView[] layoutViews = { sourceLayout, destinationLayout };
            CardTransferInteractionCoordinator transferCoordinator = new CardTransferInteractionCoordinator(
                match,
                playerId,
                containedOwnerId,
                lockService,
                new TransferCardUseCase(),
                layoutViews);
            ContainedCardDragCoordinator containedCoordinator = new ContainedCardDragCoordinator(
                containedOwnerId,
                lockService,
                new TabletopInteractionStateMachine(5f),
                new TabletopDragPreviewSession(),
                pointerProjector,
                new CardDropTargetResolver(
                    pointerCamera,
                    pointerProjector,
                    LayerMaskFor(DropTargetLayer),
                    100f,
                    QueryTriggerInteraction.Collide),
                transferCoordinator,
                new ContainerLayoutViewLookup(layoutViews));
            TabletopInteractionRouter router = new TabletopInteractionRouter(
                hitResolver,
                moveCoordinator,
                containedCoordinator,
                selectionState);

            TabletopInteractionInputRoutingPolicy routingPolicy = new TabletopInteractionInputRoutingPolicy(
                selectionState,
                moveCoordinator);
            routingPolicy.ConfigureInteractionRouter(router);
            Assert.That(routingPolicy.HasInteractionRouter, Is.True, "After routing-policy ConfigureInteractionRouter: policy must have a router.");
            Assert.That(routingPolicy.InteractionRouter, Is.SameAs(router), "After routing-policy ConfigureInteractionRouter: policy must preserve the exact router instance.");

            TabletopCameraController cameraController = CreateInitializedCameraController(pointerCamera);
            TabletopCameraInputAdapter cameraAdapter;
            TabletopObjectInputAdapter objectAdapter;
            if (setupOrder == FixtureSetupOrder.CameraAdapterFirst)
            {
                cameraAdapter = CreateInitializedCameraAdapter(cameraController, routingPolicy);
                objectAdapter = CreateObjectAdapter();
            }
            else
            {
                objectAdapter = CreateObjectAdapter();
                cameraAdapter = CreateInitializedCameraAdapter(cameraController, routingPolicy);
            }

            if (configureRouterOnAdapter)
            {
                objectAdapter.ConfigureInteractionRouter(router);
                AssertRouterConfiguredAfterConfigure(objectAdapter, router);
            }

            objectAdapter.Initialize(
                moveCoordinator,
                rotationCoordinator,
                flipCoordinator,
                routingPolicy);
            if (configureRouterOnAdapter)
            {
                AssertRouterPreservedAfterInitialize(objectAdapter, router);
            }

            TabletopInputFrameCoordinator frameCoordinator = CreateFrameCoordinator(cameraAdapter, objectAdapter);
            if (configureRouterOnAdapter)
            {
                AssertRouterPreservedAfterExternalFrameDriverAttachment(objectAdapter, router, frameCoordinator);
            }

            return new UnifiedFixture(
                match,
                sourceContainer,
                destinationContainer,
                containedCardState,
                tabletopCardState,
                containedCardView,
                tabletopCardView,
                sourceLayout,
                destinationLayout,
                sourceViewComponent,
                destinationViewComponent,
                selectionState,
                lockService,
                moveCoordinator,
                containedCoordinator,
                router,
                routingPolicy,
                hitResolver,
                cameraController,
                cameraAdapter,
                objectAdapter,
                frameCoordinator,
                pointerCamera,
                offset);
        }

        private Component CreateLayoutView(
            ContainerState container,
            TabletopCoordinateConverter converter,
            IReadOnlyList<CardView> cardViews,
            Vector3 anchorPosition)
        {
            switch (container.Kind)
            {
                case ContainerKind.Deck:
                {
                    DeckView view = CreateGameObject("DeckView").AddComponent<DeckView>();
                    view.Bind(container, new ContainerPlacementState(container.Id, CreatePose(anchorPosition.x, anchorPosition.z, 0f)), converter, cardViews);
                    return view;
                }
                case ContainerKind.Stack:
                {
                    StackView view = CreateGameObject("StackView").AddComponent<StackView>();
                    view.Bind(container, new ContainerPlacementState(container.Id, CreatePose(anchorPosition.x, anchorPosition.z, 0f)), converter, cardViews);
                    return view;
                }
                case ContainerKind.DiscardPile:
                {
                    DiscardPileView view = CreateGameObject("DiscardPileView").AddComponent<DiscardPileView>();
                    view.Bind(container, new ContainerPlacementState(container.Id, CreatePose(anchorPosition.x, anchorPosition.z, 0f)), converter, cardViews);
                    return view;
                }
                case ContainerKind.Hand:
                {
                    HandView view = CreateGameObject("HandView").AddComponent<HandView>();
                    Transform anchor = CreateGameObject("HandAnchor").transform;
                    anchor.position = anchorPosition;
                    view.Bind(container, anchor, converter, cardViews);
                    return view;
                }
                case ContainerKind.ConsoleSlot:
                {
                    ConsoleSlotView view = CreateGameObject("ConsoleSlotView").AddComponent<ConsoleSlotView>();
                    Transform anchor = CreateGameObject("ConsoleSlotAnchor").transform;
                    anchor.position = anchorPosition;
                    view.Bind(container, anchor, converter, cardViews);
                    return view;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(container), container.Kind, "Unsupported container kind.");
            }
        }

        private void AddDropTarget(Component viewComponent)
        {
            viewComponent.gameObject.layer = DropTargetLayer;
            BoxCollider collider = viewComponent.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 0.2f, 2f);
            TabletopContainerDropTarget target = viewComponent.gameObject.AddComponent<TabletopContainerDropTarget>();
            target.Configure((IContainerView)viewComponent, collider);
        }

        private CardView CreateCardView(
            string name,
            CardInstanceState cardState,
            TabletopCoordinateConverter converter)
        {
            CardView view = CreateGameObject(name).AddComponent<CardView>();
            view.Bind(cardState, converter);
            return view;
        }

        private TabletopInputFrameCoordinator CreateFrameCoordinator(
            TabletopCameraInputAdapter cameraAdapter,
            TabletopObjectInputAdapter objectAdapter)
        {
            GameObject gameObject = CreateGameObject("Unified Input Frame Coordinator");
            gameObject.SetActive(false);
            TabletopInputFrameCoordinator coordinator = gameObject.AddComponent<TabletopInputFrameCoordinator>();
            coordinator.enabled = false;
            coordinator.cameraInputAdapter = cameraAdapter;
            coordinator.objectInputAdapter = objectAdapter;
            gameObject.SetActive(true);
            coordinator.enabled = true;
            return coordinator;
        }

        private TabletopCameraController CreateInitializedCameraController(UnityEngine.Camera targetCamera)
        {
            Transform cameraRig = CreateGameObject("Unified Camera Rig").transform;
            GameObject gameObject = CreateGameObject("Unified Camera Controller");
            gameObject.SetActive(false);
            TabletopCameraController controller = gameObject.AddComponent<TabletopCameraController>();
            controller.targetCamera = targetCamera;
            controller.cameraRig = cameraRig;
            gameObject.SetActive(true);
            return controller;
        }

        private TabletopCameraInputAdapter CreateInitializedCameraAdapter(
            TabletopCameraController cameraController,
            TabletopInteractionInputRoutingPolicy routingPolicy)
        {
            InputActionMap actionMap = CreateActionMap("UnifiedCamera");
            GameObject gameObject = CreateGameObject("Unified Camera Adapter");
            gameObject.SetActive(false);
            TabletopCameraInputAdapter adapter = gameObject.AddComponent<TabletopCameraInputAdapter>();
            adapter.cameraController = cameraController;
            adapter.keyboardPanAction = CreateActionReference(actionMap, "KeyboardPan", InputActionType.Value, "Vector2");
            adapter.dragPanAction = CreateActionReference(actionMap, "DragPan", InputActionType.Button, "Button");
            adapter.pointerDeltaAction = CreateActionReference(actionMap, "PointerDelta", InputActionType.PassThrough, "Vector2");
            adapter.zoomAction = CreateActionReference(actionMap, "Zoom", InputActionType.PassThrough, "Axis");
            adapter.keyboardPanSpeed = 5f;
            adapter.dragPanUnitsPerPixel = 0.02f;
            adapter.zoomSensitivity = 0.01f;
            gameObject.SetActive(true);
            adapter.ConfigureScrollRoutingPolicy(routingPolicy);
            return adapter;
        }

        private TabletopObjectInputAdapter CreateObjectAdapter()
        {
            InputActionMap actionMap = CreateActionMap("UnifiedObject");
            GameObject gameObject = CreateGameObject("Unified Object Adapter");
            gameObject.SetActive(false);
            TabletopObjectInputAdapter adapter = gameObject.AddComponent<TabletopObjectInputAdapter>();
            adapter.pointAction = CreateActionReference(actionMap, "Point", InputActionType.PassThrough, "Vector2");
            adapter.selectAction = CreateActionReference(actionMap, "Select", InputActionType.Button, "Button");
            adapter.cancelAction = CreateActionReference(actionMap, "Cancel", InputActionType.Button, "Button");
            adapter.rotateAction = CreateActionReference(actionMap, "Rotate", InputActionType.PassThrough, "Axis");
            adapter.flipAction = CreateActionReference(actionMap, "Flip", InputActionType.Button, "Button");
            adapter.rotationStepDegrees = 15f;
            gameObject.SetActive(true);
            return adapter;
        }

        private InputActionMap CreateActionMap(string name)
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            createdInputAssets.Add(asset);
            return asset.AddActionMap(name);
        }

        private InputActionReference CreateActionReference(
            InputActionMap actionMap,
            string actionName,
            InputActionType actionType,
            string expectedControlType)
        {
            InputAction action = actionMap.AddAction(actionName, actionType, expectedControlLayout: expectedControlType);
            InputActionReference reference = InputActionReference.Create(action);
            createdActionReferences.Add(reference);
            return reference;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private UnityEngine.Camera CreateTopDownCamera()
        {
            GameObject cameraObject = CreateGameObject("Unified Input Camera");
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.targetTexture = null;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f));
            return camera;
        }

        private static void AddObjectCollider(GameObject gameObject)
        {
            gameObject.layer = ObjectLayer;
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 0.2f, 1f);
        }

        private static ContainerState CreateContainer(ContainerKind kind, int? seed = null)
        {
            ContainerId containerId = seed.HasValue
                ? new ContainerId(GuidFromSeed(seed.Value))
                : ContainerId.New();

            return new ContainerState(containerId, kind, SeatId.Empty, ObjectVisibility.Public, 0);
        }

        private static CardInstanceState CreateCard(int seed, TabletopPose pose)
        {
            return new CardInstanceState(
                new TabletopObjectState(
                    new TabletopObjectId(GuidFromSeed(seed)),
                    new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                    TabletopObjectKind.Card,
                    pose,
                    ContainerId.Empty,
                    PlayerId.Empty,
                    ObjectVisibility.Public,
                    false),
                CardFace.FaceUp);
        }

        private static TabletopPose CreatePose(double x, double y, float rotationDegrees)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, 0, 0);
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

        private static void AssertCoordinate(TableCoordinate coordinate, double x, double y)
        {
            Assert.That(coordinate.X, Is.EqualTo(x).Within(CoordinateTolerance));
            Assert.That(coordinate.Y, Is.EqualTo(y).Within(CoordinateTolerance));
        }

        private static string DescribeSetupOrderOutcome(UnifiedFixture fixture)
        {
            return string.Format(
                "ObjectAdapterId={0}; RouterId={1}; FrameCoordinatorId={2}; FrameCoordinatorObjectAdapterId={3}; ObjectAdapterHasRouter={4}; ObjectAdapterRouterId={5}; ObjectAdapterInitialized={6}; ObjectAdapterExternallyDriven={7}; ObjectAdapterDrivenByFrameCoordinator={8}; ActiveRoute={9}; ContainedPhase={10}; MovePhase={11}; Selection={12}; CameraSize={13}; LockCount={14}; Revision={15}; RotationResult={16}; FlipResult={17}",
                RuntimeHelpers.GetHashCode(fixture.ObjectAdapter),
                RuntimeHelpers.GetHashCode(fixture.Router),
                RuntimeHelpers.GetHashCode(fixture.FrameCoordinator),
                fixture.FrameCoordinator.ObjectInputAdapter != null ? RuntimeHelpers.GetHashCode(fixture.FrameCoordinator.ObjectInputAdapter) : 0,
                fixture.ObjectAdapter.HasInteractionRouter,
                fixture.ObjectAdapter.InteractionRouter != null ? RuntimeHelpers.GetHashCode(fixture.ObjectAdapter.InteractionRouter) : 0,
                fixture.ObjectAdapter.IsInitialized,
                fixture.ObjectAdapter.IsExternallyDriven,
                fixture.ObjectAdapter.IsExternallyDrivenBy(fixture.FrameCoordinator),
                fixture.Router.ActiveRoute,
                fixture.ContainedCoordinator.Phase,
                fixture.MoveCoordinator.Phase,
                fixture.SelectionState.SelectedObjectId,
                fixture.CameraController.State.OrthographicSize,
                fixture.LockService.Count,
                fixture.Match.Revision,
                fixture.ObjectAdapter.LastRotationResult.HasValue ? fixture.ObjectAdapter.LastRotationResult.Value.Status.ToString() : "None",
                fixture.ObjectAdapter.LastFlipResult.HasValue ? fixture.ObjectAdapter.LastFlipResult.Value.Status.ToString() : "None");
        }

        private static void AssertRouterConfiguredAfterConfigure(
            TabletopObjectInputAdapter objectAdapter,
            TabletopInteractionRouter router)
        {
            Assert.That(objectAdapter.HasInteractionRouter, Is.True, "After ConfigureInteractionRouter: adapter must have a router.");
            Assert.That(objectAdapter.InteractionRouter, Is.SameAs(router), "After ConfigureInteractionRouter: adapter must preserve the exact router instance.");
        }

        private static void AssertRouterPreservedAfterInitialize(
            TabletopObjectInputAdapter objectAdapter,
            TabletopInteractionRouter router)
        {
            Assert.That(objectAdapter.IsInitialized, Is.True, "After Initialize: adapter must be initialized.");
            Assert.That(objectAdapter.HasInteractionRouter, Is.True, "After Initialize: adapter must still have a router.");
            Assert.That(objectAdapter.InteractionRouter, Is.SameAs(router), "After Initialize: adapter must preserve the exact router instance.");
        }

        private static void AssertRouterPreservedAfterExternalFrameDriverAttachment(
            TabletopObjectInputAdapter objectAdapter,
            TabletopInteractionRouter router,
            TabletopInputFrameCoordinator frameCoordinator)
        {
            Assert.That(objectAdapter.HasInteractionRouter, Is.True, "After external frame-driver attachment: adapter must still have a router.");
            Assert.That(objectAdapter.InteractionRouter, Is.SameAs(router), "After external frame-driver attachment: adapter must preserve the exact router instance.");
            Assert.That(objectAdapter.IsExternallyDrivenBy(frameCoordinator), Is.True, "After external frame-driver attachment: adapter must be driven by the expected coordinator.");
        }

        public enum FixtureSetupOrder
        {
            CameraAdapterFirst,
            ObjectAdapterFirst
        }

        private sealed class UnifiedFixture
        {
            public UnifiedFixture(
                MatchState match,
                ContainerState sourceContainer,
                ContainerState destinationContainer,
                CardInstanceState containedCardState,
                CardInstanceState tabletopCardState,
                CardView containedCardView,
                CardView tabletopCardView,
                CountingLayoutView sourceLayout,
                CountingLayoutView destinationLayout,
                Component sourceViewComponent,
                Component destinationViewComponent,
                TabletopSelectionState selectionState,
                LocalInteractionLockService lockService,
                TabletopMoveInteractionCoordinator moveCoordinator,
                ContainedCardDragCoordinator containedCoordinator,
                TabletopInteractionRouter router,
                TabletopInteractionInputRoutingPolicy routingPolicy,
                TabletopObjectHitResolver hitResolver,
                TabletopCameraController cameraController,
                TabletopCameraInputAdapter cameraAdapter,
                TabletopObjectInputAdapter objectAdapter,
                TabletopInputFrameCoordinator frameCoordinator,
                UnityEngine.Camera pointerCamera,
                Vector2 tableOffset)
            {
                Match = match;
                SourceContainer = sourceContainer;
                DestinationContainer = destinationContainer;
                ContainedCard = containedCardState;
                TabletopCardState = tabletopCardState;
                CardState = containedCardState;
                TabletopCard = tabletopCardState;
                ContainedCardView = containedCardView;
                TabletopCardView = tabletopCardView;
                SourceLayout = sourceLayout;
                DestinationLayout = destinationLayout;
                SourceViewComponent = sourceViewComponent;
                DestinationViewComponent = destinationViewComponent;
                SelectionState = selectionState;
                LockService = lockService;
                MoveCoordinator = moveCoordinator;
                ContainedCoordinator = containedCoordinator;
                Router = router;
                RoutingPolicy = routingPolicy;
                HitResolver = hitResolver;
                CameraController = cameraController;
                CameraAdapter = cameraAdapter;
                ObjectAdapter = objectAdapter;
                FrameCoordinator = frameCoordinator;
                PointerCamera = pointerCamera;
                ContainedScreenPoint = ScreenPointFor(containedCardView);
                SourceScreenPoint = ScreenPointFor(sourceViewComponent);
                DestinationScreenPoint = ScreenPointFor(destinationViewComponent);
                DragScreenPoint = ScreenPointForWorld(tableOffset.x + 2f, tableOffset.y);
                TabletopCardScreenPoint = ScreenPointFor(tabletopCardView);
                TabletopDragScreenPoint = ScreenPointForWorld(tableOffset.x + 4f, tableOffset.y - 2f);
                TabletopReleaseScreenPoint = ScreenPointForWorld(tableOffset.x + 6f, tableOffset.y - 5f);
                EmptyScreenPoint = ScreenPointForWorld(tableOffset.x + 8f, tableOffset.y + 8f);
            }

            public MatchState Match { get; }

            public ContainerState SourceContainer { get; }

            public ContainerState DestinationContainer { get; }

            public CardInstanceState ContainedCard { get; }

            public CardInstanceState CardState { get; }

            public CardInstanceState TabletopCard { get; }

            public CardInstanceState TabletopCardState { get; }

            public CardView ContainedCardView { get; }

            public CardView TabletopCardView { get; }

            public CountingLayoutView SourceLayout { get; }

            public CountingLayoutView DestinationLayout { get; }

            public Component SourceViewComponent { get; }

            public Component DestinationViewComponent { get; }

            public TabletopSelectionState SelectionState { get; }

            public LocalInteractionLockService LockService { get; }

            public TabletopMoveInteractionCoordinator MoveCoordinator { get; }

            public ContainedCardDragCoordinator ContainedCoordinator { get; }

            public TabletopInteractionRouter Router { get; }

            public TabletopInteractionInputRoutingPolicy RoutingPolicy { get; }

            public TabletopObjectHitResolver HitResolver { get; }

            public TabletopCameraController CameraController { get; }

            public TabletopCameraInputAdapter CameraAdapter { get; }

            public TabletopObjectInputAdapter ObjectAdapter { get; }

            public TabletopInputFrameCoordinator FrameCoordinator { get; }

            public UnityEngine.Camera PointerCamera { get; }

            public Vector2 ContainedScreenPoint { get; }

            public Vector2 SourceScreenPoint { get; }

            public Vector2 DestinationScreenPoint { get; }

            public Vector2 DragScreenPoint { get; }

            public Vector2 TabletopCardScreenPoint { get; }

            public Vector2 TabletopDragScreenPoint { get; }

            public Vector2 TabletopReleaseScreenPoint { get; }

            public Vector2 EmptyScreenPoint { get; }

            public MoveInteractionReleaseResult? ApplySharedFrame(TabletopInputFrame frame)
            {
                return FrameCoordinator.ApplyInputFrame(frame, DeltaTime);
            }

            public TabletopInputFrame CreateFrame(
                Vector2? screenPosition = null,
                bool selectPressedThisFrame = false,
                bool selectHeld = false,
                bool selectReleasedThisFrame = false,
                bool cancelPressedThisFrame = false,
                float rotateDelta = 0f,
                float scrollDelta = 0f,
                bool flipPressedThisFrame = false)
            {
                return new TabletopInputFrame(
                    Vector2.zero,
                    false,
                    Vector2.zero,
                    scrollDelta,
                    screenPosition ?? EmptyScreenPoint,
                    selectPressedThisFrame,
                    selectHeld,
                    selectReleasedThisFrame,
                    cancelPressedThisFrame,
                    rotateDelta,
                    flipPressedThisFrame);
            }

            private Vector2 ScreenPointFor(Component component)
            {
                return ScreenPointForWorld(component.transform.position.x, component.transform.position.z);
            }

            private Vector2 ScreenPointForWorld(float x, float z)
            {
                Physics.SyncTransforms();
                Vector3 screenPoint = PointerCamera.WorldToScreenPoint(new Vector3(x, 0f, z));
                Assert.That(float.IsFinite(screenPoint.x), Is.True);
                Assert.That(float.IsFinite(screenPoint.y), Is.True);
                return new Vector2(screenPoint.x, screenPoint.y);
            }
        }

        private sealed class CountingLayoutView : IContainerLayoutView
        {
            private readonly IContainerLayoutView inner;

            public CountingLayoutView(IContainerLayoutView inner)
            {
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public bool IsBound => inner.IsBound;

            public ContainerId ContainerId => inner.ContainerId;

            public ContainerState ContainerState => inner.ContainerState;

            public int ApplyCount { get; private set; }

            public void SetCardViews(IReadOnlyList<CardView> cardViews)
            {
                inner.SetCardViews(cardViews);
            }

            public void ApplyAcceptedLayout()
            {
                inner.ApplyAcceptedLayout();
                ApplyCount++;
            }
        }
    }
}
