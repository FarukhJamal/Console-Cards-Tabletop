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
    public sealed class CardFacePresentationTests
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
        public void NewCardView_HasNoFacePresentationConfiguration()
        {
            CardView view = CreateView<CardView>();

            Assert.That(view.IsFacePresentationConfigured, Is.False);
            Assert.That(view.FaceUpVisualRoot, Is.Null);
            Assert.That(view.FaceDownVisualRoot, Is.Null);
        }

        [Test]
        public void DisplayedFace_WhenUnconfigured_IsNull()
        {
            CardView view = CreateView<CardView>();

            Assert.That(view.DisplayedFace, Is.Null);
        }

        [Test]
        public void ConfigureFacePresentation_WithValidChildRoots_StoresReferences()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();

            fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.FaceDownRoot);

            Assert.That(fixture.View.IsFacePresentationConfigured, Is.True);
            Assert.That(fixture.View.FaceUpVisualRoot, Is.SameAs(fixture.FaceUpRoot));
            Assert.That(fixture.View.FaceDownVisualRoot, Is.SameAs(fixture.FaceDownRoot));
        }

        [Test]
        public void DisplayedFace_WhenBothRootsAreActive_IsNull()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();
            fixture.FaceUpRoot.SetActive(true);
            fixture.FaceDownRoot.SetActive(true);
            fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.FaceDownRoot);

            Assert.That(fixture.View.DisplayedFace, Is.Null);
        }

        [Test]
        public void DisplayedFace_WhenNeitherRootIsActive_IsNull()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();
            fixture.FaceUpRoot.SetActive(false);
            fixture.FaceDownRoot.SetActive(false);
            fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.FaceDownRoot);

            Assert.That(fixture.View.DisplayedFace, Is.Null);
        }

        [Test]
        public void ConfigureFacePresentation_WhenFaceUpRootIsNull_ThrowsArgumentNullException()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();

            Assert.Throws<ArgumentNullException>(
                () => fixture.View.ConfigureFacePresentation(null, fixture.FaceDownRoot));
        }

        [Test]
        public void ConfigureFacePresentation_WhenFaceDownRootIsNull_ThrowsArgumentNullException()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();

            Assert.Throws<ArgumentNullException>(
                () => fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, null));
        }

        [Test]
        public void ConfigureFacePresentation_WhenRootsAreSameObject_ThrowsArgumentException()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();

            Assert.Throws<ArgumentException>(
                () => fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.FaceUpRoot));
        }

        [Test]
        public void ConfigureFacePresentation_WhenCardViewRootIsFaceRoot_ThrowsArgumentException()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();

            Assert.Throws<ArgumentException>(
                () => fixture.View.ConfigureFacePresentation(fixture.View.gameObject, fixture.FaceDownRoot));
            Assert.Throws<ArgumentException>(
                () => fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.View.gameObject));
        }

        [Test]
        public void ConfigureFacePresentation_WhenFaceUpRootIsNotChild_ThrowsArgumentException()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();
            GameObject nonChild = CreateGameObject("NonChildFaceUp");

            Assert.Throws<ArgumentException>(
                () => fixture.View.ConfigureFacePresentation(nonChild, fixture.FaceDownRoot));
        }

        [Test]
        public void ConfigureFacePresentation_WhenFaceDownRootIsNotChild_ThrowsArgumentException()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();
            GameObject nonChild = CreateGameObject("NonChildFaceDown");

            Assert.Throws<ArgumentException>(
                () => fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, nonChild));
        }

        [Test]
        public void FailedConfiguration_PreservesPreviousValidReferencesAndActiveStates()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceDown);
            GameObject originalFaceUp = fixture.View.FaceUpVisualRoot;
            GameObject originalFaceDown = fixture.View.FaceDownVisualRoot;
            bool originalFaceUpActive = originalFaceUp.activeSelf;
            bool originalFaceDownActive = originalFaceDown.activeSelf;
            TabletopPose originalPose = fixture.Card.BaseState.Pose;
            CardFace originalFace = fixture.Card.Face;
            Vector3 originalPosition = fixture.View.transform.position;
            Quaternion originalRotation = fixture.View.transform.rotation;
            GameObject nonChild = CreateGameObject("RejectedFaceRoot");

            Assert.Throws<ArgumentException>(
                () => fixture.View.ConfigureFacePresentation(nonChild, fixture.FaceDownRoot));

            Assert.That(fixture.View.FaceUpVisualRoot, Is.SameAs(originalFaceUp));
            Assert.That(fixture.View.FaceDownVisualRoot, Is.SameAs(originalFaceDown));
            Assert.That(originalFaceUp.activeSelf, Is.EqualTo(originalFaceUpActive));
            Assert.That(originalFaceDown.activeSelf, Is.EqualTo(originalFaceDownActive));
            Assert.That(fixture.View.CardState, Is.SameAs(fixture.Card));
            Assert.That(fixture.Card.BaseState.Pose, Is.EqualTo(originalPose));
            Assert.That(fixture.Card.Face, Is.EqualTo(originalFace));
            AssertVector3(fixture.View.transform.position, originalPosition);
            Assert.That(Quaternion.Angle(originalRotation, fixture.View.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void ConfigureFacePresentation_WhenUnbound_DoesNotInventDisplayedFace()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();
            fixture.FaceUpRoot.SetActive(false);
            fixture.FaceDownRoot.SetActive(false);

            fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.FaceDownRoot);

            Assert.That(fixture.View.DisplayedFace, Is.Null);
            Assert.That(fixture.FaceUpRoot.activeSelf, Is.False);
            Assert.That(fixture.FaceDownRoot.activeSelf, Is.False);
        }

        [Test]
        public void ConfigureFacePresentation_WhenBound_ImmediatelyProjectsAuthoritativeFace()
        {
            CardFaceFixture fixture = CreateUnconfiguredBoundFixture(CardFace.FaceDown);

            fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.FaceDownRoot);

            AssertFacePresentation(fixture.View, CardFace.FaceDown);
        }

        [Test]
        public void ApplyAcceptedFacePresentation_WhenFaceUp_ProjectsFaceUpWithoutMutatingPoseScaleOrState()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);
            Vector3 position = fixture.View.transform.position;
            Quaternion rotation = fixture.View.transform.rotation;
            Vector3 scale = new Vector3(2f, 3f, 4f);
            fixture.View.transform.localScale = scale;

            fixture.View.ApplyAcceptedFacePresentation();

            AssertFacePresentation(fixture.View, CardFace.FaceUp);
            AssertVector3(fixture.View.transform.position, position);
            Assert.That(Quaternion.Angle(rotation, fixture.View.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            AssertVector3(fixture.View.transform.localScale, scale);
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceUp));
        }

        [Test]
        public void ApplyAcceptedFacePresentation_WhenFaceDown_ProjectsFaceDownWithoutMutatingPoseScaleOrState()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceDown);
            Vector3 position = fixture.View.transform.position;
            Quaternion rotation = fixture.View.transform.rotation;
            Vector3 scale = new Vector3(0.5f, 1.5f, 2.5f);
            fixture.View.transform.localScale = scale;

            fixture.View.ApplyAcceptedFacePresentation();

            AssertFacePresentation(fixture.View, CardFace.FaceDown);
            AssertVector3(fixture.View.transform.position, position);
            Assert.That(Quaternion.Angle(rotation, fixture.View.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            AssertVector3(fixture.View.transform.localScale, scale);
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void ApplyAcceptedFacePresentation_WhenReappliedToSameFace_IsSafe()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);

            fixture.View.ApplyAcceptedFacePresentation();
            fixture.View.ApplyAcceptedFacePresentation();

            AssertFacePresentation(fixture.View, CardFace.FaceUp);
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceUp));
        }

        [Test]
        public void ApplyAcceptedFacePresentation_WhenUnbound_ThrowsInvalidOperationException()
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();
            fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.FaceDownRoot);

            Assert.Throws<InvalidOperationException>(() => fixture.View.ApplyAcceptedFacePresentation());
        }

        [Test]
        public void ApplyAcceptedFacePresentation_WhenUnconfigured_ThrowsInvalidOperationException()
        {
            CardFaceFixture fixture = CreateUnconfiguredBoundFixture(CardFace.FaceUp);

            Assert.Throws<InvalidOperationException>(() => fixture.View.ApplyAcceptedFacePresentation());
        }

        [Test]
        public void ApplyAcceptedFacePresentation_WhenCardFaceIsUnsupported_ThrowsInvalidOperationException()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);
            fixture.Card.SetFace((CardFace)99);

            Assert.Throws<InvalidOperationException>(() => fixture.View.ApplyAcceptedFacePresentation());
        }

        [Test]
        public void ManualVisualRootActivation_DoesNotMutateAuthoritativeFace()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);

            fixture.FaceUpRoot.SetActive(false);
            fixture.FaceDownRoot.SetActive(true);

            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceUp));
            Assert.That(fixture.View.DisplayedFace, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void ApplyAcceptedFacePresentation_AfterManualInversion_RestoresRootsFromAuthoritativeFace()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);
            fixture.FaceUpRoot.SetActive(false);
            fixture.FaceDownRoot.SetActive(true);

            fixture.View.ApplyAcceptedFacePresentation();

            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceUp));
            AssertFacePresentation(fixture.View, CardFace.FaceUp);
        }

        [Test]
        public void ApplyAcceptedState_AfterCardStateSetFace_UpdatesVisuals()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);

            fixture.Card.SetFace(CardFace.FaceDown);
            fixture.View.ApplyAcceptedState();

            AssertFacePresentation(fixture.View, CardFace.FaceDown);
        }

        [Test]
        public void ReconcileAcceptedState_AfterCardStateSetFace_UpdatesVisuals()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceDown);

            fixture.Card.SetFace(CardFace.FaceUp);
            fixture.View.ReconcileAcceptedState();

            AssertFacePresentation(fixture.View, CardFace.FaceUp);
        }

        [Test]
        public void Rebind_FromFaceUpCardToFaceDownCard_UpdatesVisuals()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);
            CardInstanceState second = CreateCard(200, CardFace.FaceDown);

            fixture.View.Bind(second, CreateConverter());

            Assert.That(fixture.View.CardState, Is.SameAs(second));
            AssertFacePresentation(fixture.View, CardFace.FaceDown);
        }

        [Test]
        public void Rebind_FromFaceDownCardToFaceUpCard_UpdatesVisuals()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceDown);
            CardInstanceState second = CreateCard(201, CardFace.FaceUp);

            fixture.View.Bind(second, CreateConverter());

            Assert.That(fixture.View.CardState, Is.SameAs(second));
            AssertFacePresentation(fixture.View, CardFace.FaceUp);
        }

        [Test]
        public void Rebind_WhenConfiguredAndNewFaceIsUnsupported_PreservesPreviousBindingAndVisuals()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceDown);
            CardInstanceState previousCard = fixture.Card;
            Vector3 previousPosition = fixture.View.transform.position;
            Quaternion previousRotation = fixture.View.transform.rotation;
            CardInstanceState unsupportedCard = CreateCard(202, (CardFace)99);

            Assert.Throws<InvalidOperationException>(() => fixture.View.Bind(unsupportedCard, CreateConverter()));

            Assert.That(fixture.View.CardState, Is.SameAs(previousCard));
            Assert.That(fixture.View.BoundState, Is.SameAs(previousCard.BaseState));
            AssertVector3(fixture.View.transform.position, previousPosition);
            Assert.That(Quaternion.Angle(previousRotation, fixture.View.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            AssertFacePresentation(fixture.View, CardFace.FaceDown);
        }

        [Test]
        public void ApplyPreviewPose_DoesNotChangeVisibleFace()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceDown);

            fixture.View.ApplyPreviewPose(new TabletopPose(new TableCoordinate(8.0, 9.0), 90f, 1, 2));

            AssertFacePresentation(fixture.View, CardFace.FaceDown);
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceDown));
        }

        [Test]
        public void ReconcileAcceptedState_AfterDragPreview_PreservesAuthoritativeFaceProjection()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceDown);
            fixture.View.ApplyPreviewPose(new TabletopPose(new TableCoordinate(8.0, 9.0), 90f, 1, 2));
            fixture.FaceUpRoot.SetActive(true);
            fixture.FaceDownRoot.SetActive(false);

            fixture.View.ReconcileAcceptedState();

            Assert.That(fixture.View.IsPreviewing, Is.False);
            AssertFacePresentation(fixture.View, CardFace.FaceDown);
        }

        [Test]
        public void Unbind_PreservesRootActiveStates()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);
            bool faceUpActive = fixture.FaceUpRoot.activeSelf;
            bool faceDownActive = fixture.FaceDownRoot.activeSelf;

            fixture.View.Unbind();

            Assert.That(fixture.FaceUpRoot.activeSelf, Is.EqualTo(faceUpActive));
            Assert.That(fixture.FaceDownRoot.activeSelf, Is.EqualTo(faceDownActive));
            Assert.That(fixture.View.CardState, Is.Null);
        }

        [Test]
        public void FlipSelected_WhenFaceUpAccepted_UpdatesAuthoritativeFaceAndVisualRoots()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);

            FlipInteractionResult result = fixture.FlipCoordinator.FlipSelected();

            Assert.That(result.Status, Is.EqualTo(FlipInteractionStatus.FlipAccepted));
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceDown));
            AssertFacePresentation(fixture.View, CardFace.FaceDown);
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
        }

        [Test]
        public void FlipSelected_WhenFaceDownAccepted_UpdatesAuthoritativeFaceAndVisualRoots()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceDown);

            FlipInteractionResult result = fixture.FlipCoordinator.FlipSelected();

            Assert.That(result.Status, Is.EqualTo(FlipInteractionStatus.FlipAccepted));
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceUp));
            AssertFacePresentation(fixture.View, CardFace.FaceUp);
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
        }

        [Test]
        public void FlipSelected_WhenRevisionOverflowRejected_PreservesAuthoritativeFaceAndVisualRoots()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp, revision: long.MaxValue);

            FlipInteractionResult result = fixture.FlipCoordinator.FlipSelected();

            Assert.That(result.Status, Is.EqualTo(FlipInteractionStatus.FlipRejected));
            Assert.That(result.FlipResult.Value.Status, Is.EqualTo(CommandResultStatus.Conflict));
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceUp));
            AssertFacePresentation(fixture.View, CardFace.FaceUp);
            Assert.That(fixture.Match.Revision, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void FlipSelected_WhenUserLockRejected_PreservesAuthoritativeFaceAndVisualRoots()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceDown, isUserLocked: true);

            FlipInteractionResult result = fixture.FlipCoordinator.FlipSelected();

            Assert.That(result.Status, Is.EqualTo(FlipInteractionStatus.ObjectUserLocked));
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceDown));
            AssertFacePresentation(fixture.View, CardFace.FaceDown);
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenLocalLockConflictRejected_PreservesAuthoritativeFaceAndVisualRoots()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);
            fixture.LockService.Acquire(fixture.View.ObjectId, InteractionOwnerId.New());

            FlipInteractionResult result = fixture.FlipCoordinator.FlipSelected();

            Assert.That(result.Status, Is.EqualTo(FlipInteractionStatus.LocalLockConflict));
            Assert.That(fixture.Card.Face, Is.EqualTo(CardFace.FaceUp));
            AssertFacePresentation(fixture.View, CardFace.FaceUp);
            Assert.That(fixture.Match.Revision, Is.EqualTo(0));
        }

        [Test]
        public void FlipSelected_WhenAccepted_DoesNotChangeTransform()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);
            Vector3 position = fixture.View.transform.position;
            Quaternion rotation = fixture.View.transform.rotation;
            Vector3 scale = new Vector3(2f, 1f, 3f);
            fixture.View.transform.localScale = scale;

            fixture.FlipCoordinator.FlipSelected();

            AssertVector3(fixture.View.transform.position, position);
            Assert.That(Quaternion.Angle(rotation, fixture.View.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            AssertVector3(fixture.View.transform.localScale, scale);
        }

        [Test]
        public void FlipSelected_WithoutRendererMaterialCameraSurfaceSceneOrInputSystem_StillProjectsFace()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);

            FlipInteractionResult result = fixture.FlipCoordinator.FlipSelected();

            Assert.That(result.Status, Is.EqualTo(FlipInteractionStatus.FlipAccepted));
            Assert.That(fixture.View.GetComponent<Renderer>(), Is.Null);
            Assert.That(fixture.FaceUpRoot.GetComponent<Renderer>(), Is.Null);
            Assert.That(fixture.FaceDownRoot.GetComponent<Renderer>(), Is.Null);
            AssertFacePresentation(fixture.View, CardFace.FaceDown);
        }

        [Test]
        public void PawnView_AcceptedStateHookDoesNotChangePawnBindingOrTransform()
        {
            PawnView view = CreateView<PawnView>();
            PawnState pawn = new PawnState(CreateObjectState(TabletopObjectKind.Pawn, 300, CreatePose(1.0, 2.0, 15f)));
            view.Bind(pawn, CreateConverter());
            pawn.BaseState.SetPose(CreatePose(3.0, 4.0, 30f));

            view.ApplyAcceptedState();

            Assert.That(view.PawnState, Is.SameAs(pawn));
            AssertWorldPose(view, pawn.BaseState.Pose);
        }

        [Test]
        public void TokenView_AcceptedStateHookDoesNotChangeTokenBindingOrTransform()
        {
            TokenView view = CreateView<TokenView>();
            TokenState token = new TokenState(CreateObjectState(TabletopObjectKind.Token, 301, CreatePose(-1.0, -2.0, 45f)));
            view.Bind(token, CreateConverter());
            token.BaseState.SetPose(CreatePose(-3.0, -4.0, 60f));

            view.ReconcileAcceptedState();

            Assert.That(view.TokenState, Is.SameAs(token));
            AssertWorldPose(view, token.BaseState.Pose);
        }

        [Test]
        public void ExistingViewBindingAndPreviewBehavior_RemainsIntact()
        {
            CardFaceFixture fixture = CreateBoundFaceFixture(CardFace.FaceUp);
            Vector3 scale = new Vector3(1.5f, 2f, 2.5f);
            fixture.View.transform.localScale = scale;
            TabletopPose previewPose = new TabletopPose(new TableCoordinate(8.0, 9.0), 90f, 1, 2);

            fixture.View.ApplyPreviewPose(previewPose);
            fixture.View.ReconcileAcceptedState();

            Assert.That(fixture.View.IsPreviewing, Is.False);
            Assert.That(fixture.View.PreviewPose, Is.EqualTo(TabletopPose.Default));
            AssertWorldPose(fixture.View, fixture.Card.BaseState.Pose);
            AssertVector3(fixture.View.transform.localScale, scale);
            AssertFacePresentation(fixture.View, CardFace.FaceUp);
        }

        private CardFaceFixture CreateBoundFaceFixture(
            CardFace face,
            long revision = 0,
            bool isUserLocked = false,
            bool configureWhileUnbound = false)
        {
            CardFaceFixture fixture = CreateUnconfiguredBoundFixture(face, revision, isUserLocked, bindBeforeConfigure: !configureWhileUnbound);
            if (configureWhileUnbound)
            {
                fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.FaceDownRoot);
                fixture.View.Bind(fixture.Card, CreateConverter());
            }
            else
            {
                fixture.View.ConfigureFacePresentation(fixture.FaceUpRoot, fixture.FaceDownRoot);
            }

            return fixture;
        }

        private CardFaceFixture CreateUnconfiguredBoundFixture(
            CardFace face,
            long revision = 0,
            bool isUserLocked = false,
            bool bindBeforeConfigure = true)
        {
            CardFaceFixture fixture = CreateUnboundFaceFixture();
            CardInstanceState card = new CardInstanceState(
                CreateObjectState(TabletopObjectKind.Card, 1, CreatePose(2.0, 3.0, 25f), isUserLocked),
                face);
            MatchState match = CreateMatch(revision, new[] { card });
            TabletopSelectionState selectionState = new TabletopSelectionState();
            LocalInteractionLockService lockService = new LocalInteractionLockService();
            PlayerId playerId = PlayerId.New();
            InteractionOwnerId ownerId = InteractionOwnerId.New();
            TabletopCardFlipCoordinator flipCoordinator = new TabletopCardFlipCoordinator(
                match,
                playerId,
                ownerId,
                selectionState,
                lockService,
                new FlipCardUseCase());

            fixture.Card = card;
            fixture.Match = match;
            fixture.SelectionState = selectionState;
            fixture.LockService = lockService;
            fixture.FlipCoordinator = flipCoordinator;

            if (bindBeforeConfigure)
            {
                fixture.View.Bind(card, CreateConverter());
                selectionState.Select(fixture.View);
            }

            return fixture;
        }

        private CardFaceFixture CreateUnboundFaceFixture()
        {
            CardView view = CreateView<CardView>();
            GameObject faceUpRoot = CreateChild(view.gameObject, "FaceUpVisualRoot");
            GameObject faceDownRoot = CreateChild(view.gameObject, "FaceDownVisualRoot");

            return new CardFaceFixture(view, faceUpRoot, faceDownRoot);
        }

        private T CreateView<T>()
            where T : TabletopObjectView
        {
            GameObject gameObject = CreateGameObject(typeof(T).Name);
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

        private static CardInstanceState CreateCard(int seed, CardFace face)
        {
            return new CardInstanceState(
                CreateObjectState(TabletopObjectKind.Card, seed, CreatePose(seed, seed + 1.0, seed * 5f)),
                face);
        }

        private static TabletopObjectState CreateObjectState(
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

        private static TabletopPose CreatePose(double x, double y, float rotationDegrees)
        {
            return new TabletopPose(new TableCoordinate(x, y), rotationDegrees, 0, 0);
        }

        private static MatchState CreateMatch(long revision, CardInstanceState[] cards)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.New(),
                revision,
                cards,
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                Array.Empty<ContainerState>(),
                Array.Empty<SeatState>());
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        private static void AssertFacePresentation(CardView view, CardFace expectedFace)
        {
            Assert.That(view.DisplayedFace, Is.EqualTo(expectedFace));
            Assert.That(view.FaceUpVisualRoot.activeSelf, Is.EqualTo(expectedFace == CardFace.FaceUp));
            Assert.That(view.FaceDownVisualRoot.activeSelf, Is.EqualTo(expectedFace == CardFace.FaceDown));
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

        private sealed class CardFaceFixture
        {
            public CardFaceFixture(CardView view, GameObject faceUpRoot, GameObject faceDownRoot)
            {
                View = view;
                FaceUpRoot = faceUpRoot;
                FaceDownRoot = faceDownRoot;
            }

            public CardView View { get; }

            public GameObject FaceUpRoot { get; }

            public GameObject FaceDownRoot { get; }

            public CardInstanceState Card { get; set; }

            public MatchState Match { get; set; }

            public TabletopSelectionState SelectionState { get; set; }

            public LocalInteractionLockService LockService { get; set; }

            public TabletopCardFlipCoordinator FlipCoordinator { get; set; }
        }
    }
}
