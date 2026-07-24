using System;
using System.Collections.Generic;
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
    public sealed class TabletopSelectionPresentationTests
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
        public void NewVisual_BeginsUnconfiguredAndUnselected()
        {
            TabletopSelectionVisual visual = CreateRoot("Visual").AddComponent<TabletopSelectionVisual>();

            Assert.That(visual.IsConfigured, Is.False);
            Assert.That(visual.ObjectView, Is.Null);
            Assert.That(visual.HighlightRoot, Is.Null);
            Assert.That(visual.IsSelected, Is.False);
        }

        [Test]
        public void Configure_WhenValid_StoresReferencesAndDisablesHighlight()
        {
            CardView view = CreateBoundCardView(1, out _);
            TabletopSelectionVisual visual = view.gameObject.AddComponent<TabletopSelectionVisual>();
            GameObject highlight = CreateChild("Highlight", view.transform);
            highlight.SetActive(true);

            visual.Configure(view, highlight);

            Assert.That(visual.IsConfigured, Is.True);
            Assert.That(visual.ObjectView, Is.SameAs(view));
            Assert.That(visual.HighlightRoot, Is.SameAs(highlight));
            Assert.That(highlight.activeSelf, Is.False);
            Assert.That(visual.IsSelected, Is.False);
        }

        [TestCase(InvalidVisualConfigurationCase.NullView)]
        [TestCase(InvalidVisualConfigurationCase.NullHighlight)]
        [TestCase(InvalidVisualConfigurationCase.ViewFromAnotherGameObject)]
        [TestCase(InvalidVisualConfigurationCase.RootGameObjectAsHighlight)]
        [TestCase(InvalidVisualConfigurationCase.NonDescendantHighlight)]
        [TestCase(InvalidVisualConfigurationCase.HighlightContainsAnotherView)]
        public void Configure_WhenInvalid_ThrowsExpectedException(InvalidVisualConfigurationCase configurationCase)
        {
            CardView view = CreateBoundCardView(10, out _);
            TabletopSelectionVisual visual = view.gameObject.AddComponent<TabletopSelectionVisual>();
            TabletopObjectView candidateView = view;
            GameObject candidateHighlight = CreateChild("Highlight", view.transform);

            switch (configurationCase)
            {
                case InvalidVisualConfigurationCase.NullView:
                    candidateView = null;
                    break;
                case InvalidVisualConfigurationCase.NullHighlight:
                    candidateHighlight = null;
                    break;
                case InvalidVisualConfigurationCase.ViewFromAnotherGameObject:
                    candidateView = CreateBoundPawnView(11, out _);
                    break;
                case InvalidVisualConfigurationCase.RootGameObjectAsHighlight:
                    candidateHighlight = view.gameObject;
                    break;
                case InvalidVisualConfigurationCase.NonDescendantHighlight:
                    candidateHighlight = CreateRoot("ExternalHighlight");
                    break;
                case InvalidVisualConfigurationCase.HighlightContainsAnotherView:
                    CreateChild("NestedView", candidateHighlight.transform).AddComponent<PawnView>();
                    break;
            }

            Exception exception = Assert.Catch(() => visual.Configure(candidateView, candidateHighlight));

            if (configurationCase == InvalidVisualConfigurationCase.NullView
                || configurationCase == InvalidVisualConfigurationCase.NullHighlight)
            {
                Assert.That(exception, Is.TypeOf<ArgumentNullException>());
            }
            else
            {
                Assert.That(exception, Is.TypeOf<ArgumentException>());
            }
        }

        [Test]
        public void Configure_WhenReconfigurationFails_PreservesPreviousReferencesAndActiveState()
        {
            CardView view = CreateBoundCardView(20, out CardInstanceState state);
            TabletopSelectionVisual visual = view.gameObject.AddComponent<TabletopSelectionVisual>();
            GameObject firstHighlight = CreateChild("FirstHighlight", view.transform);
            GameObject invalidHighlight = CreateRoot("InvalidHighlight");
            Vector3 originalPosition = view.transform.position;
            Quaternion originalRotation = view.transform.rotation;
            Vector3 originalScale = view.transform.localScale;
            TabletopPose originalPose = state.BaseState.Pose;
            visual.Configure(view, firstHighlight);
            firstHighlight.SetActive(true);

            Assert.Throws<ArgumentException>(() => visual.Configure(view, invalidHighlight));

            Assert.That(visual.ObjectView, Is.SameAs(view));
            Assert.That(visual.HighlightRoot, Is.SameAs(firstHighlight));
            Assert.That(firstHighlight.activeSelf, Is.True);
            Assert.That(visual.IsSelected, Is.True);
            Assert.That(view.IsBound, Is.True);
            Assert.That(view.BoundState, Is.SameAs(state.BaseState));
            AssertVector3(view.transform.position, originalPosition);
            Assert.That(Quaternion.Angle(originalRotation, view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            AssertVector3(view.transform.localScale, originalScale);
            Assert.That(state.BaseState.Pose, Is.EqualTo(originalPose));
        }

        [Test]
        public void Configure_DoesNotChangeViewTransformOrRuntimeState()
        {
            CardView view = CreateBoundCardView(30, out CardInstanceState state);
            TabletopSelectionVisual visual = view.gameObject.AddComponent<TabletopSelectionVisual>();
            GameObject highlight = CreateChild("Highlight", view.transform);
            Vector3 originalPosition = view.transform.position;
            Quaternion originalRotation = view.transform.rotation;
            Vector3 originalScale = view.transform.localScale;
            TabletopPose originalPose = state.BaseState.Pose;
            CardFace originalFace = state.Face;

            visual.Configure(view, highlight);

            AssertVector3(view.transform.position, originalPosition);
            Assert.That(Quaternion.Angle(originalRotation, view.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            AssertVector3(view.transform.localScale, originalScale);
            Assert.That(state.BaseState.Pose, Is.EqualTo(originalPose));
            Assert.That(state.Face, Is.EqualTo(originalFace));
        }

        [Test]
        public void SetSelected_WhenValidBoundView_ActivatesAndDeactivatesHighlightSafely()
        {
            SelectionFixture fixture = CreateFixture();

            fixture.CardVisual.SetSelected(true);
            Assert.That(fixture.CardHighlight.activeSelf, Is.True);
            Assert.That(fixture.CardVisual.IsSelected, Is.True);

            fixture.CardVisual.SetSelected(true);
            Assert.That(fixture.CardHighlight.activeSelf, Is.True);

            fixture.CardVisual.SetSelected(false);
            Assert.That(fixture.CardHighlight.activeSelf, Is.False);
            Assert.That(fixture.CardVisual.IsSelected, Is.False);

            fixture.CardVisual.SetSelected(false);
            Assert.That(fixture.CardHighlight.activeSelf, Is.False);
        }

        [TestCase(UnavailableViewCase.Unconfigured)]
        [TestCase(UnavailableViewCase.Unbound)]
        [TestCase(UnavailableViewCase.Disabled)]
        [TestCase(UnavailableViewCase.Inactive)]
        public void SetSelected_WhenSelectingUnavailableView_ThrowsInvalidOperationException(UnavailableViewCase unavailableCase)
        {
            CardView view = CreateBoundCardView(40, out _);
            TabletopSelectionVisual visual = view.gameObject.AddComponent<TabletopSelectionVisual>();
            GameObject highlight = CreateChild("Highlight", view.transform);

            if (unavailableCase != UnavailableViewCase.Unconfigured)
            {
                visual.Configure(view, highlight);
            }

            switch (unavailableCase)
            {
                case UnavailableViewCase.Unbound:
                    view.Unbind();
                    break;
                case UnavailableViewCase.Disabled:
                    view.enabled = false;
                    break;
                case UnavailableViewCase.Inactive:
                    view.gameObject.SetActive(false);
                    break;
            }

            Assert.Throws<InvalidOperationException>(() => visual.SetSelected(true));
        }

        [Test]
        public void SetSelected_WhenDeselectingUnavailableView_RemainsSafe()
        {
            CardView view = CreateBoundCardView(50, out _);
            TabletopSelectionVisual visual = view.gameObject.AddComponent<TabletopSelectionVisual>();
            GameObject highlight = CreateChild("Highlight", view.transform);
            visual.Configure(view, highlight);
            view.Unbind();

            visual.SetSelected(false);

            Assert.That(highlight.activeSelf, Is.False);
            Assert.That(visual.IsSelected, Is.False);
        }

        [Test]
        public void SetSelected_DoesNotMutateRuntimeStateOrSelectionState()
        {
            SelectionFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.PawnView);
            TabletopPose originalPose = fixture.CardState.BaseState.Pose;
            CardFace originalFace = fixture.CardState.Face;
            long originalRevision = fixture.Match.Revision;

            fixture.CardVisual.SetSelected(true);

            Assert.That(fixture.CardState.BaseState.Pose, Is.EqualTo(originalPose));
            Assert.That(fixture.CardState.Face, Is.EqualTo(originalFace));
            Assert.That(fixture.Match.Revision, Is.EqualTo(originalRevision));
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.PawnView));
        }

        [Test]
        public void Clear_WhenConfigured_DisablesHighlightAndClearsReferences()
        {
            SelectionFixture fixture = CreateFixture();
            fixture.CardVisual.SetSelected(true);

            fixture.CardVisual.Clear();
            fixture.CardVisual.Clear();

            Assert.That(fixture.CardHighlight.activeSelf, Is.False);
            Assert.That(fixture.CardVisual.IsConfigured, Is.False);
            Assert.That(fixture.CardVisual.ObjectView, Is.Null);
            Assert.That(fixture.CardVisual.HighlightRoot, Is.Null);
            Assert.That(fixture.CardVisual.IsSelected, Is.False);
        }

        [Test]
        public void PresenterConstructor_WhenValid_StoresDependencies()
        {
            SelectionFixture fixture = CreateFixture();

            Assert.That(fixture.Presenter.SelectionState, Is.SameAs(fixture.SelectionState));
            Assert.That(fixture.Presenter.CardSelectionVisual, Is.SameAs(fixture.CardVisual));
            Assert.That(fixture.Presenter.PawnSelectionVisual, Is.SameAs(fixture.PawnVisual));
            Assert.That(fixture.Presenter.TokenSelectionVisual, Is.SameAs(fixture.TokenVisual));
        }

        [TestCase(PresenterNullDependency.SelectionState)]
        [TestCase(PresenterNullDependency.CardVisual)]
        [TestCase(PresenterNullDependency.PawnVisual)]
        [TestCase(PresenterNullDependency.TokenVisual)]
        public void PresenterConstructor_WhenDependencyIsNull_ThrowsArgumentNullException(
            PresenterNullDependency dependency)
        {
            SelectionFixture fixture = CreateFixture();
            TabletopSelectionState selectionState = fixture.SelectionState;
            TabletopSelectionVisual cardVisual = fixture.CardVisual;
            TabletopSelectionVisual pawnVisual = fixture.PawnVisual;
            TabletopSelectionVisual tokenVisual = fixture.TokenVisual;

            switch (dependency)
            {
                case PresenterNullDependency.SelectionState:
                    selectionState = null;
                    break;
                case PresenterNullDependency.CardVisual:
                    cardVisual = null;
                    break;
                case PresenterNullDependency.PawnVisual:
                    pawnVisual = null;
                    break;
                case PresenterNullDependency.TokenVisual:
                    tokenVisual = null;
                    break;
            }

            Assert.Throws<ArgumentNullException>(
                () => new TabletopSelectionPresenter(selectionState, cardVisual, pawnVisual, tokenVisual));
        }

        [TestCase(InvalidPresenterConfigurationCase.UnconfiguredVisual)]
        [TestCase(InvalidPresenterConfigurationCase.DuplicateVisualComponent)]
        [TestCase(InvalidPresenterConfigurationCase.DuplicateViewTarget)]
        public void PresenterConstructor_WhenVisualConfigurationIsInvalid_ThrowsArgumentException(
            InvalidPresenterConfigurationCase configurationCase)
        {
            SelectionFixture fixture = CreateFixture();
            TabletopSelectionVisual cardVisual = fixture.CardVisual;
            TabletopSelectionVisual pawnVisual = fixture.PawnVisual;
            TabletopSelectionVisual tokenVisual = fixture.TokenVisual;

            switch (configurationCase)
            {
                case InvalidPresenterConfigurationCase.UnconfiguredVisual:
                    cardVisual = CreateRoot("UnconfiguredVisual").AddComponent<TabletopSelectionVisual>();
                    break;
                case InvalidPresenterConfigurationCase.DuplicateVisualComponent:
                    pawnVisual = cardVisual;
                    break;
                case InvalidPresenterConfigurationCase.DuplicateViewTarget:
                    pawnVisual = fixture.CardView.gameObject.AddComponent<TabletopSelectionVisual>();
                    pawnVisual.Configure(fixture.CardView, CreateChild("SecondCardHighlight", fixture.CardView.transform));
                    break;
            }

            Assert.Throws<ArgumentException>(
                () => new TabletopSelectionPresenter(fixture.SelectionState, cardVisual, pawnVisual, tokenVisual));
        }

        [TestCase(SelectionTarget.None)]
        [TestCase(SelectionTarget.Card)]
        [TestCase(SelectionTarget.Pawn)]
        [TestCase(SelectionTarget.Token)]
        [TestCase(SelectionTarget.External)]
        public void Refresh_ProjectsCurrentSelectionOntoConfiguredHighlights(SelectionTarget target)
        {
            SelectionFixture fixture = CreateFixture();
            SelectTarget(fixture, target);
            TabletopObjectView selectedBeforeRefresh = fixture.SelectionState.SelectedView;
            fixture.CardHighlight.SetActive(target != SelectionTarget.Card);
            fixture.PawnHighlight.SetActive(target != SelectionTarget.Pawn);
            fixture.TokenHighlight.SetActive(target != SelectionTarget.Token);

            fixture.Presenter.Refresh();

            Assert.That(fixture.CardHighlight.activeSelf, Is.EqualTo(target == SelectionTarget.Card));
            Assert.That(fixture.PawnHighlight.activeSelf, Is.EqualTo(target == SelectionTarget.Pawn));
            Assert.That(fixture.TokenHighlight.activeSelf, Is.EqualTo(target == SelectionTarget.Token));
            Assert.That(CountActiveHighlights(fixture), Is.LessThanOrEqualTo(1));
            if (target == SelectionTarget.None)
            {
                Assert.That(fixture.SelectionState.SelectedView, Is.Null);
            }
            else
            {
                Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(selectedBeforeRefresh));
            }
        }

        [Test]
        public void Refresh_WhenSelectionChanges_UpdatesHighlights()
        {
            SelectionFixture fixture = CreateFixture();

            fixture.SelectionState.Select(fixture.CardView);
            fixture.Presenter.Refresh();
            fixture.SelectionState.Select(fixture.TokenView);
            fixture.Presenter.Refresh();

            Assert.That(fixture.CardHighlight.activeSelf, Is.False);
            Assert.That(fixture.PawnHighlight.activeSelf, Is.False);
            Assert.That(fixture.TokenHighlight.activeSelf, Is.True);
            Assert.That(CountActiveHighlights(fixture), Is.EqualTo(1));
        }

        [Test]
        public void Refresh_WhenSelectionCleared_DeactivatesAllHighlights()
        {
            SelectionFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.CardView);
            fixture.Presenter.Refresh();

            fixture.SelectionState.ClearSelection();
            fixture.Presenter.Refresh();

            AssertAllHighlightsInactive(fixture);
            Assert.That(fixture.SelectionState.HasSelection, Is.False);
        }

        [TestCase(UnavailableViewCase.Destroyed)]
        [TestCase(UnavailableViewCase.Unbound)]
        [TestCase(UnavailableViewCase.Disabled)]
        [TestCase(UnavailableViewCase.Inactive)]
        public void Refresh_WhenSelectedViewIsUnavailable_ClearsSelectionAndHighlights(
            UnavailableViewCase unavailableCase)
        {
            SelectionFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.CardView);
            fixture.Presenter.Refresh();

            switch (unavailableCase)
            {
                case UnavailableViewCase.Destroyed:
                    UnityObject.DestroyImmediate(fixture.CardView.gameObject);
                    break;
                case UnavailableViewCase.Unbound:
                    fixture.CardView.Unbind();
                    break;
                case UnavailableViewCase.Disabled:
                    fixture.CardView.enabled = false;
                    break;
                case UnavailableViewCase.Inactive:
                    fixture.CardView.gameObject.SetActive(false);
                    break;
            }

            fixture.Presenter.Refresh();

            Assert.That(fixture.SelectionState.HasSelection, Is.False);
            Assert.That(fixture.CardVisual.IsSelected, Is.False);
            Assert.That(fixture.PawnVisual.IsSelected, Is.False);
            Assert.That(fixture.TokenVisual.IsSelected, Is.False);
        }

        [Test]
        public void Refresh_WhenManualHighlightStatesAreWrong_OverwritesThem()
        {
            SelectionFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.PawnView);
            fixture.CardHighlight.SetActive(true);
            fixture.PawnHighlight.SetActive(false);
            fixture.TokenHighlight.SetActive(true);

            fixture.Presenter.Refresh();

            Assert.That(fixture.CardHighlight.activeSelf, Is.False);
            Assert.That(fixture.PawnHighlight.activeSelf, Is.True);
            Assert.That(fixture.TokenHighlight.activeSelf, Is.False);
            Assert.That(CountActiveHighlights(fixture), Is.EqualTo(1));
        }

        [Test]
        public void Refresh_WhenRepeated_IsIdempotent()
        {
            SelectionFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.TokenView);

            fixture.Presenter.Refresh();
            fixture.Presenter.Refresh();

            Assert.That(fixture.CardHighlight.activeSelf, Is.False);
            Assert.That(fixture.PawnHighlight.activeSelf, Is.False);
            Assert.That(fixture.TokenHighlight.activeSelf, Is.True);
            Assert.That(CountActiveHighlights(fixture), Is.EqualTo(1));
        }

        [Test]
        public void Refresh_DoesNotMutateRuntimeStateRevisionFaceOrViewTransforms()
        {
            SelectionFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.CardView);
            Snapshot before = Snapshot.Capture(fixture);

            fixture.Presenter.Refresh();

            AssertSnapshotUnchanged(fixture, before);
        }

        [Test]
        public void PresenterClear_DisablesHighlightsAndPreservesSelection()
        {
            SelectionFixture fixture = CreateFixture();
            fixture.SelectionState.Select(fixture.CardView);
            fixture.Presenter.Refresh();

            fixture.Presenter.Clear();
            fixture.Presenter.Clear();

            AssertAllHighlightsInactive(fixture);
            Assert.That(fixture.SelectionState.SelectedView, Is.SameAs(fixture.CardView));
            Assert.That(fixture.SelectionState.HasSelection, Is.True);
        }

        [Test]
        public void Boundary_RequiresNoRendererMaterialCameraInputScenePrefabMatchFactoryOrCoreSelectionStorage()
        {
            SelectionFixture fixture = CreateFixture();

            Assert.That(fixture.CardView.GetComponent<Renderer>(), Is.Null);
            Assert.That(fixture.CardView.GetComponent<Camera>(), Is.Null);
            Assert.That(fixture.CardView.GetComponent<UnityEngine.InputSystem.PlayerInput>(), Is.Null);
            Assert.That(fixture.CardHighlight.GetComponent<Renderer>(), Is.Null);
            Assert.That(fixture.Match.ContainsObject(fixture.CardState.BaseState.Id), Is.True);
            Assert.That(fixture.CardState.BaseState.Pose, Is.EqualTo(Snapshot.Capture(fixture).CardPose));
            Assert.That(fixture.SelectionState.SelectedView, Is.Null);
        }

        private SelectionFixture CreateFixture()
        {
            TabletopCoordinateConverter converter = CreateConverter();
            CardInstanceState cardState = CreateCardState(100, new TabletopPose(new TableCoordinate(-2d, 0d), 15f, 0, 0));
            PawnState pawnState = CreatePawnState(101, new TabletopPose(new TableCoordinate(0d, 0d), 30f, 0, 0));
            TokenState tokenState = CreateTokenState(102, new TabletopPose(new TableCoordinate(2d, 0d), 45f, 0, 0));
            MatchState match = CreateMatch(cardState, pawnState, tokenState);

            CardView cardView = CreateView<CardView>("CardView");
            PawnView pawnView = CreateView<PawnView>("PawnView");
            TokenView tokenView = CreateView<TokenView>("TokenView");
            cardView.transform.localScale = new Vector3(1f, 2f, 3f);
            pawnView.transform.localScale = new Vector3(2f, 3f, 4f);
            tokenView.transform.localScale = new Vector3(3f, 4f, 5f);

            cardView.Bind(cardState, converter);
            pawnView.Bind(pawnState, converter);
            tokenView.Bind(tokenState, converter);

            TabletopSelectionVisual cardVisual = cardView.gameObject.AddComponent<TabletopSelectionVisual>();
            TabletopSelectionVisual pawnVisual = pawnView.gameObject.AddComponent<TabletopSelectionVisual>();
            TabletopSelectionVisual tokenVisual = tokenView.gameObject.AddComponent<TabletopSelectionVisual>();
            GameObject cardHighlight = CreateChild("CardHighlight", cardView.transform);
            GameObject pawnHighlight = CreateChild("PawnHighlight", pawnView.transform);
            GameObject tokenHighlight = CreateChild("TokenHighlight", tokenView.transform);
            cardVisual.Configure(cardView, cardHighlight);
            pawnVisual.Configure(pawnView, pawnHighlight);
            tokenVisual.Configure(tokenView, tokenHighlight);

            TabletopSelectionState selectionState = new TabletopSelectionState();
            TabletopSelectionPresenter presenter = new TabletopSelectionPresenter(
                selectionState,
                cardVisual,
                pawnVisual,
                tokenVisual);

            return new SelectionFixture(
                match,
                cardState,
                pawnState,
                tokenState,
                cardView,
                pawnView,
                tokenView,
                cardVisual,
                pawnVisual,
                tokenVisual,
                cardHighlight,
                pawnHighlight,
                tokenHighlight,
                selectionState,
                presenter);
        }

        private void SelectTarget(
            SelectionFixture fixture,
            SelectionTarget target)
        {
            switch (target)
            {
                case SelectionTarget.Card:
                    fixture.SelectionState.Select(fixture.CardView);
                    break;
                case SelectionTarget.Pawn:
                    fixture.SelectionState.Select(fixture.PawnView);
                    break;
                case SelectionTarget.Token:
                    fixture.SelectionState.Select(fixture.TokenView);
                    break;
                case SelectionTarget.External:
                    CardView externalView = CreateBoundCardView(500, out _);
                    fixture.SelectionState.Select(externalView);
                    break;
            }
        }

        private CardView CreateBoundCardView(int seed, out CardInstanceState state)
        {
            CardView view = CreateView<CardView>("CardView");
            state = CreateCardState(seed, new TabletopPose(new TableCoordinate(seed, seed + 1d), 10f, 0, 0));
            view.Bind(state, CreateConverter());
            return view;
        }

        private PawnView CreateBoundPawnView(int seed, out PawnState state)
        {
            PawnView view = CreateView<PawnView>("PawnView");
            state = CreatePawnState(seed, new TabletopPose(new TableCoordinate(seed, seed + 1d), 20f, 0, 0));
            view.Bind(state, CreateConverter());
            return view;
        }

        private T CreateView<T>(string name)
            where T : TabletopObjectView
        {
            return CreateRoot(name).AddComponent<T>();
        }

        private GameObject CreateRoot(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }

        private GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static MatchState CreateMatch(
            CardInstanceState cardState,
            PawnState pawnState,
            TokenState tokenState)
        {
            return new MatchState(
                MatchId.New(),
                GameTemplateId.Empty,
                0,
                new[] { cardState },
                new[] { pawnState },
                new[] { tokenState },
                Array.Empty<ContainerState>(),
                Array.Empty<SeatState>());
        }

        private static CardInstanceState CreateCardState(
            int seed,
            TabletopPose pose)
        {
            return new CardInstanceState(CreateBaseState(seed, TabletopObjectKind.Card, pose), CardFace.FaceUp);
        }

        private static PawnState CreatePawnState(
            int seed,
            TabletopPose pose)
        {
            return new PawnState(CreateBaseState(seed, TabletopObjectKind.Pawn, pose));
        }

        private static TokenState CreateTokenState(
            int seed,
            TabletopPose pose)
        {
            return new TokenState(CreateBaseState(seed, TabletopObjectKind.Token, pose));
        }

        private static TabletopObjectState CreateBaseState(
            int seed,
            TabletopObjectKind kind,
            TabletopPose pose)
        {
            return new TabletopObjectState(
                new TabletopObjectId(GuidFromSeed(seed)),
                new ObjectDefinitionId(GuidFromSeed(seed + 1000)),
                kind,
                pose,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }

        private static TabletopCoordinateConverter CreateConverter()
        {
            return new TabletopCoordinateConverter(1f, 0f, 0f, 0f);
        }

        private static int CountActiveHighlights(SelectionFixture fixture)
        {
            int count = 0;
            count += fixture.CardHighlight.activeSelf ? 1 : 0;
            count += fixture.PawnHighlight.activeSelf ? 1 : 0;
            count += fixture.TokenHighlight.activeSelf ? 1 : 0;
            return count;
        }

        private static void AssertAllHighlightsInactive(SelectionFixture fixture)
        {
            Assert.That(fixture.CardHighlight.activeSelf, Is.False);
            Assert.That(fixture.PawnHighlight.activeSelf, Is.False);
            Assert.That(fixture.TokenHighlight.activeSelf, Is.False);
        }

        private static void AssertSnapshotUnchanged(
            SelectionFixture fixture,
            Snapshot before)
        {
            Assert.That(fixture.Match.Revision, Is.EqualTo(before.Revision));
            Assert.That(fixture.CardState.BaseState.Pose, Is.EqualTo(before.CardPose));
            Assert.That(fixture.PawnState.BaseState.Pose, Is.EqualTo(before.PawnPose));
            Assert.That(fixture.TokenState.BaseState.Pose, Is.EqualTo(before.TokenPose));
            Assert.That(fixture.CardState.Face, Is.EqualTo(before.CardFace));
            AssertVector3(fixture.CardView.transform.position, before.CardPosition);
            AssertVector3(fixture.PawnView.transform.position, before.PawnPosition);
            AssertVector3(fixture.TokenView.transform.position, before.TokenPosition);
            Assert.That(Quaternion.Angle(before.CardRotation, fixture.CardView.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Quaternion.Angle(before.PawnRotation, fixture.PawnView.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Quaternion.Angle(before.TokenRotation, fixture.TokenView.transform.rotation), Is.EqualTo(0f).Within(Tolerance));
            AssertVector3(fixture.CardView.transform.localScale, before.CardScale);
            AssertVector3(fixture.PawnView.transform.localScale, before.PawnScale);
            AssertVector3(fixture.TokenView.transform.localScale, before.TokenScale);
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }

        private static Guid GuidFromSeed(int seed)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)(seed / 256), (byte)(seed % 256));
        }

        public enum InvalidVisualConfigurationCase
        {
            NullView,
            NullHighlight,
            ViewFromAnotherGameObject,
            RootGameObjectAsHighlight,
            NonDescendantHighlight,
            HighlightContainsAnotherView
        }

        public enum UnavailableViewCase
        {
            Unconfigured,
            Destroyed,
            Unbound,
            Disabled,
            Inactive
        }

        public enum PresenterNullDependency
        {
            SelectionState,
            CardVisual,
            PawnVisual,
            TokenVisual
        }

        public enum InvalidPresenterConfigurationCase
        {
            UnconfiguredVisual,
            DuplicateVisualComponent,
            DuplicateViewTarget
        }

        public enum SelectionTarget
        {
            None,
            Card,
            Pawn,
            Token,
            External
        }

        private sealed class SelectionFixture
        {
            public SelectionFixture(
                MatchState match,
                CardInstanceState cardState,
                PawnState pawnState,
                TokenState tokenState,
                CardView cardView,
                PawnView pawnView,
                TokenView tokenView,
                TabletopSelectionVisual cardVisual,
                TabletopSelectionVisual pawnVisual,
                TabletopSelectionVisual tokenVisual,
                GameObject cardHighlight,
                GameObject pawnHighlight,
                GameObject tokenHighlight,
                TabletopSelectionState selectionState,
                TabletopSelectionPresenter presenter)
            {
                Match = match;
                CardState = cardState;
                PawnState = pawnState;
                TokenState = tokenState;
                CardView = cardView;
                PawnView = pawnView;
                TokenView = tokenView;
                CardVisual = cardVisual;
                PawnVisual = pawnVisual;
                TokenVisual = tokenVisual;
                CardHighlight = cardHighlight;
                PawnHighlight = pawnHighlight;
                TokenHighlight = tokenHighlight;
                SelectionState = selectionState;
                Presenter = presenter;
            }

            public MatchState Match { get; }

            public CardInstanceState CardState { get; }

            public PawnState PawnState { get; }

            public TokenState TokenState { get; }

            public CardView CardView { get; }

            public PawnView PawnView { get; }

            public TokenView TokenView { get; }

            public TabletopSelectionVisual CardVisual { get; }

            public TabletopSelectionVisual PawnVisual { get; }

            public TabletopSelectionVisual TokenVisual { get; }

            public GameObject CardHighlight { get; }

            public GameObject PawnHighlight { get; }

            public GameObject TokenHighlight { get; }

            public TabletopSelectionState SelectionState { get; }

            public TabletopSelectionPresenter Presenter { get; }
        }

        private sealed class Snapshot
        {
            private Snapshot(
                long revision,
                TabletopPose cardPose,
                TabletopPose pawnPose,
                TabletopPose tokenPose,
                CardFace cardFace,
                Vector3 cardPosition,
                Vector3 pawnPosition,
                Vector3 tokenPosition,
                Quaternion cardRotation,
                Quaternion pawnRotation,
                Quaternion tokenRotation,
                Vector3 cardScale,
                Vector3 pawnScale,
                Vector3 tokenScale)
            {
                Revision = revision;
                CardPose = cardPose;
                PawnPose = pawnPose;
                TokenPose = tokenPose;
                CardFace = cardFace;
                CardPosition = cardPosition;
                PawnPosition = pawnPosition;
                TokenPosition = tokenPosition;
                CardRotation = cardRotation;
                PawnRotation = pawnRotation;
                TokenRotation = tokenRotation;
                CardScale = cardScale;
                PawnScale = pawnScale;
                TokenScale = tokenScale;
            }

            public long Revision { get; }

            public TabletopPose CardPose { get; }

            public TabletopPose PawnPose { get; }

            public TabletopPose TokenPose { get; }

            public CardFace CardFace { get; }

            public Vector3 CardPosition { get; }

            public Vector3 PawnPosition { get; }

            public Vector3 TokenPosition { get; }

            public Quaternion CardRotation { get; }

            public Quaternion PawnRotation { get; }

            public Quaternion TokenRotation { get; }

            public Vector3 CardScale { get; }

            public Vector3 PawnScale { get; }

            public Vector3 TokenScale { get; }

            public static Snapshot Capture(SelectionFixture fixture)
            {
                return new Snapshot(
                    fixture.Match.Revision,
                    fixture.CardState.BaseState.Pose,
                    fixture.PawnState.BaseState.Pose,
                    fixture.TokenState.BaseState.Pose,
                    fixture.CardState.Face,
                    fixture.CardView.transform.position,
                    fixture.PawnView.transform.position,
                    fixture.TokenView.transform.position,
                    fixture.CardView.transform.rotation,
                    fixture.PawnView.transform.rotation,
                    fixture.TokenView.transform.rotation,
                    fixture.CardView.transform.localScale,
                    fixture.PawnView.transform.localScale,
                    fixture.TokenView.transform.localScale);
            }
        }
    }
}
