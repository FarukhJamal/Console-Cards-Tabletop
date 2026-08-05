using System.Collections;
using System.Linq;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Prototype;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConsoleCards.Tests.PlayMode.Presentation
{
    public sealed class TabletopM3PrototypeSceneTests
    {
        private const string ScenePath = "Assets/ConsoleCards/Presentation/Scenes/TabletopPrototype.unity";
        private const float Tolerance = 0.0001f;
        private const float DeltaTime = 1f;
        private SceneFixture fixture;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (fixture != null)
            {
                yield return fixture.Dispose();
                fixture = null;
            }

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SceneInitializes_WithRequiredM3RuntimeGraphAndViews()
        {
            yield return LoadFixture();

            Assert.That(fixture.Composition.MatchState.Cards.Count, Is.EqualTo(16));
            Assert.That(fixture.Composition.ButtonDefinitions.Count, Is.EqualTo(8));
            Assert.That(fixture.Composition.MatchState.Pawns.Count, Is.EqualTo(1));
            Assert.That(fixture.Composition.MatchState.Tokens.Count, Is.EqualTo(1));
            Assert.That(fixture.Composition.MatchState.Seats.Count, Is.EqualTo(1));
            Assert.That(fixture.Composition.MatchState.Containers.Values.Count(container => container.Kind == ContainerKind.Deck), Is.EqualTo(1));
            Assert.That(fixture.Composition.MatchState.Containers.Values.Count(container => container.Kind == ContainerKind.Hand), Is.EqualTo(1));
            Assert.That(fixture.Composition.MatchState.Containers.Values.Count(container => container.Kind == ContainerKind.Stack), Is.GreaterThanOrEqualTo(2));
            Assert.That(fixture.Composition.MatchState.Containers.Values.Count(container => container.Kind == ContainerKind.DiscardPile), Is.EqualTo(1));
            Assert.That(fixture.Composition.ConsoleSlotViews, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(fixture.Composition.CardViews, Has.Count.EqualTo(16));
            Assert.That(fixture.Composition.DeckView.VisibleCardCount, Is.EqualTo(12));
            Assert.That(fixture.Composition.HandView.VisibleCardCount, Is.EqualTo(0));
            Assert.That(fixture.Composition.DiscardPileView.VisibleCardCount, Is.EqualTo(0));
            Assert.That(fixture.Composition.LayoutViewLookup, Is.Not.Null);
            Assert.That(fixture.Composition.InteractionRouter, Is.Not.Null);
            Assert.That(fixture.Composition.ObjectAdapter.HasInteractionRouter, Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeAndViews_BindExactCardInstances()
        {
            yield return LoadFixture();

            foreach (CardView view in fixture.Composition.CardViews)
            {
                Assert.That(view.IsBound, Is.True);
                Assert.That(fixture.Composition.MatchState.Cards[view.ObjectId], Is.SameAs(view.CardState));
                Assert.That(view.BoundState, Is.SameAs(view.CardState.BaseState));
            }
        }

        [UnityTest]
        public IEnumerator InitialLayouts_DoNotMutateRuntimePose()
        {
            yield return LoadFixture();
            CardView deckCard = fixture.Composition.CardViews.First(view =>
                view.CardState.BaseState.ContainerId == fixture.Composition.DeckContainerId);
            TabletopPose acceptedPose = deckCard.CardState.BaseState.Pose;

            fixture.Composition.DeckView.ApplyAcceptedLayout();

            Assert.That(deckCard.CardState.BaseState.Pose, Is.EqualTo(acceptedPose));
            Assert.That(deckCard.IsContainerLayoutApplied, Is.True);
        }

        [UnityTest]
        public IEnumerator ShuffleAndDrawControls_UpdateDeckAndHandLayouts()
        {
            yield return LoadFixture();
            ContainerState deck = fixture.Composition.MatchState.GetContainer(fixture.Composition.DeckContainerId);
            var originalOrder = deck.ObjectIds.ToArray();

            Assert.That(fixture.Composition.ShuffleDeck().Succeeded, Is.True);
            Assert.That(deck.ObjectIds, Is.Not.EqualTo(originalOrder));
            Assert.That(fixture.Composition.DeckView.VisibleCardCount, Is.EqualTo(deck.Count));

            Assert.That(fixture.Composition.DrawOne().Succeeded, Is.True);
            Assert.That(fixture.Composition.DeckView.VisibleCardCount, Is.EqualTo(11));
            Assert.That(fixture.Composition.HandView.VisibleCardCount, Is.EqualTo(1));

            Assert.That(fixture.Composition.DrawThree().Succeeded, Is.True);
            Assert.That(fixture.Composition.DeckView.VisibleCardCount, Is.EqualTo(8));
            Assert.That(fixture.Composition.HandView.VisibleCardCount, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator ContainedDrag_TransfersHandCardToConsoleSlot()
        {
            yield return LoadFixture();
            fixture.Composition.DrawOne();
            CardView card = fixture.FirstCardIn(fixture.Composition.HandContainerId);
            ConsoleSlotView slot = fixture.Composition.ConsoleSlotViews[0];

            ContainedCardDragReleaseResult result = fixture.DragContainedCard(card, slot.transform.position);

            Assert.That(result.Status, Is.EqualTo(ContainedCardDragReleaseStatus.TransferAccepted));
            Assert.That(card.CardState.BaseState.ContainerId, Is.EqualTo(slot.ContainerId));
            Assert.That(slot.VisibleCardCount, Is.EqualTo(1));
            Assert.That(fixture.Composition.LockService.Count, Is.EqualTo(0));
            Assert.That(fixture.Composition.PreviewSession.IsActive, Is.False);
        }

        [UnityTest]
        public IEnumerator FullConsoleSlotRejection_RestoresSourceLayout()
        {
            yield return LoadFixture();
            fixture.Composition.DrawThree();
            ConsoleSlotView slot = fixture.Composition.ConsoleSlotViews[0];
            CardView first = fixture.FirstCardIn(fixture.Composition.HandContainerId);
            Assert.That(fixture.DragContainedCard(first, slot.transform.position).Status, Is.EqualTo(ContainedCardDragReleaseStatus.TransferAccepted));
            CardView second = fixture.FirstCardIn(fixture.Composition.HandContainerId);
            long revision = fixture.Composition.MatchState.Revision;

            ContainedCardDragReleaseResult rejected = fixture.DragContainedCard(second, slot.transform.position);

            Assert.That(rejected.Status, Is.EqualTo(ContainedCardDragReleaseStatus.TransferRejected));
            Assert.That(second.CardState.BaseState.ContainerId, Is.EqualTo(fixture.Composition.HandContainerId));
            Assert.That(fixture.Composition.MatchState.Revision, Is.EqualTo(revision));
            Assert.That(second.IsPreviewing, Is.False);
            Assert.That(fixture.Composition.LockService.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ContainerToTabletop_AndTabletopToStackTransfer_Work()
        {
            yield return LoadFixture();
            fixture.Composition.DrawOne();
            CardView card = fixture.FirstCardIn(fixture.Composition.HandContainerId);
            Vector3 tabletopPosition = fixture.WorldForTable(0d, 0.5d);

            ContainedCardDragReleaseResult toTable = fixture.DragContainedCard(card, tabletopPosition);
            Assert.That(toTable.Status, Is.EqualTo(ContainedCardDragReleaseStatus.TransferAccepted));
            Assert.That(card.CardState.BaseState.ContainerId.IsEmpty, Is.True);

            MoveInteractionReleaseResult? toStack = fixture.DragTabletopObject(card, fixture.StackA.transform.position);
            Assert.That(toStack.HasValue, Is.True);
            Assert.That(toStack.Value.Status, Is.EqualTo(MoveInteractionReleaseStatus.CardTransferAccepted));
            Assert.That(card.CardState.BaseState.ContainerId, Is.EqualTo(fixture.StackA.ContainerId));
            Assert.That(fixture.StackA.VisibleCardCount, Is.EqualTo(fixture.StackA.ContainerState.Count));
        }

        [UnityTest]
        public IEnumerator HandAndStackReorderControls_UseSelectedCard()
        {
            yield return LoadFixture();
            fixture.Composition.DrawThree();
            ContainerState hand = fixture.Composition.MatchState.GetContainer(fixture.Composition.HandContainerId);
            CardView card = fixture.Composition.CardViews.Single(view => view.ObjectId == hand.GetObjectAt(0));
            fixture.Select(card);

            Assert.That(fixture.Composition.MoveSelectedHandCardRight().Succeeded, Is.True);
            Assert.That(hand.IndexOf(card.ObjectId), Is.EqualTo(1));

            ContainerState stack = fixture.StackA.ContainerState;
            CardView stackCard = fixture.Composition.CardViews.Single(view => view.ObjectId == stack.GetObjectAt(0));
            fixture.Select(stackCard);
            Assert.That(fixture.Composition.MoveSelectedStackCardUp().Succeeded, Is.True);
            Assert.That(stack.IndexOf(stackCard.ObjectId), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Merge_RemovesSourceRuntimeContainerViewAndTarget()
        {
            yield return LoadFixture();
            ContainerId sourceId = fixture.Composition.StackAContainerId;

            Assert.That(fixture.Composition.MergeStackAOntoStackB().Succeeded, Is.True);

            Assert.That(fixture.Composition.MatchState.Containers.ContainsKey(sourceId), Is.False);
            Assert.That(fixture.GetStackViews().Any(view => view.ContainerId == sourceId), Is.False);
            Assert.That(fixture.Composition.LayoutViewLookup.TryGet(sourceId, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator Split_CreatesRuntimeStackViewTargetAndRebuiltRouter()
        {
            yield return LoadFixture();
            int beforeStackViews = fixture.GetStackViews().Length;

            Assert.That(fixture.Composition.SplitSelectedOrPrimaryStack().Succeeded, Is.True);

            StackView[] stackViews = fixture.GetStackViews();
            Assert.That(stackViews.Length, Is.EqualTo(beforeStackViews + 1));
            StackView created = stackViews.First(view => view.ContainerId != fixture.Composition.StackAContainerId
                && view.ContainerId != fixture.Composition.StackBContainerId);
            Assert.That(created.VisibleCardCount, Is.GreaterThan(0));
            Assert.That(fixture.Composition.LayoutViewLookup.TryGet(created.ContainerId, out _), Is.True);
            Assert.That(fixture.Composition.ObjectAdapter.HasInteractionRouter, Is.True);
        }

        [UnityTest]
        public IEnumerator Cancel_CleansPreviewLockAndKeepsMembership()
        {
            yield return LoadFixture();
            fixture.Composition.DrawOne();
            CardView card = fixture.FirstCardIn(fixture.Composition.HandContainerId);
            ContainerId source = card.CardState.BaseState.ContainerId;

            Assert.That(fixture.Composition.ContainedCardDragCoordinator.TryBegin(card, fixture.ScreenPoint(card.transform.position)), Is.True);
            fixture.Composition.ContainedCardDragCoordinator.UpdatePointer(fixture.ScreenPoint(fixture.WorldForTable(1d, 1d)));
            fixture.Composition.ContainedCardDragCoordinator.Cancel();

            Assert.That(card.CardState.BaseState.ContainerId, Is.EqualTo(source));
            Assert.That(fixture.Composition.PreviewSession.IsActive, Is.False);
            Assert.That(fixture.Composition.LockService.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Reset_RestoresUsablePrototypeState()
        {
            yield return LoadFixture();
            fixture.Composition.DrawThree();
            fixture.Composition.SplitSelectedOrPrimaryStack();

            fixture.Composition.ResetPrototype();
            yield return null;

            Assert.That(fixture.Composition.IsInitialized, Is.True);
            Assert.That(fixture.Composition.MatchState.Cards.Count, Is.EqualTo(16));
            Assert.That(fixture.Composition.DeckView.VisibleCardCount, Is.EqualTo(12));
            Assert.That(fixture.Composition.HandView.VisibleCardCount, Is.EqualTo(0));
            Assert.That(fixture.Composition.ObjectAdapter.IsInitialized, Is.True);
            Assert.That(fixture.Composition.LockService.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ExistingM2MovementRotationFlipAndCamera_StillWork()
        {
            yield return LoadFixture();
            CardView card = fixture.Composition.cardView;
            TabletopPose originalPose = card.CardState.BaseState.Pose;

            MoveInteractionReleaseResult? move = fixture.DragTabletopObject(card, fixture.WorldForTable(0d, 0d));
            Assert.That(move.HasValue, Is.True);
            Assert.That(move.Value.Status, Is.EqualTo(MoveInteractionReleaseStatus.MoveAccepted));
            Assert.That(card.CardState.BaseState.Pose.Position.X, Is.EqualTo(0d).Within(Tolerance));
            Assert.That(card.CardState.BaseState.Pose, Is.Not.EqualTo(originalPose));

            fixture.Select(card);
            fixture.ApplyFrame(fixture.ScreenPoint(card.transform.position), rotateDelta: 120f, scrollDelta: 120f);
            Assert.That(card.CardState.BaseState.Pose.RotationDegrees, Is.EqualTo(15f).Within(Tolerance));
            fixture.ApplyFrame(fixture.ScreenPoint(card.transform.position), flipPressedThisFrame: true);
            Assert.That(card.CardState.Face, Is.EqualTo(CardFace.FaceDown));

            float cameraSize = fixture.Camera.orthographicSize;
            fixture.Composition.SelectionState.ClearSelection();
            fixture.ApplyFrame(fixture.ScreenPoint(fixture.WorldForTable(0d, 4d)), scrollDelta: 120f, rotateDelta: 120f);
            Assert.That(fixture.Camera.orthographicSize, Is.Not.EqualTo(cameraSize));
        }

        [UnityTest]
        public IEnumerator Shutdown_CleansDynamicStacksTargetsLocksPreviewsAndFeedback()
        {
            yield return LoadFixture();
            fixture.Composition.SplitSelectedOrPrimaryStack();
            int stackViewsBeforeShutdown = fixture.GetStackViews().Length;
            Assert.That(stackViewsBeforeShutdown, Is.GreaterThan(2));

            fixture.Composition.Shutdown();

            Assert.That(fixture.Composition.IsInitialized, Is.False);
            Assert.That(fixture.Composition.MatchState, Is.Null);
            Assert.That(fixture.Composition.cardView.IsBound, Is.False);
            Assert.That(fixture.Composition.pawnView.IsBound, Is.False);
            Assert.That(fixture.Composition.tokenView.IsBound, Is.False);
            Assert.That(fixture.Composition.ObjectAdapter.IsInitialized, Is.False);
            Assert.That(fixture.Composition.CameraAdapter.HasScrollRoutingPolicy, Is.False);
        }

        private IEnumerator LoadFixture()
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(ScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0));
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Additive);
            while (!loadOperation.isDone)
            {
                yield return null;
            }

            yield return null;
            Scene scene = SceneManager.GetSceneByBuildIndex(buildIndex);
            fixture = new SceneFixture(scene);
            fixture.AssertInitialized();
        }

        private sealed class SceneFixture
        {
            public SceneFixture(Scene scene)
            {
                Scene = scene;
                Composition = FindPath(scene, "Interaction/PrototypeComposition").GetComponent<TabletopPrototypeComposition>();
                Camera = FindPath(scene, "CameraRig/Main Camera").GetComponent<Camera>();
                ObjectAdapter = FindPath(scene, "Interaction/TabletopInput").GetComponent<TabletopObjectInputAdapter>();
                StackA = Composition.LayoutViewLookup.TryGet(Composition.StackAContainerId, out IContainerLayoutView stackA)
                    ? (StackView)stackA
                    : null;
            }

            public Scene Scene { get; }

            public TabletopPrototypeComposition Composition { get; }

            public Camera Camera { get; }

            public TabletopObjectInputAdapter ObjectAdapter { get; }

            public StackView StackA { get; private set; }

            public IEnumerator Dispose()
            {
                if (Composition != null && Composition.IsInitialized)
                {
                    Composition.Shutdown();
                }

                if (Scene.IsValid() && Scene.isLoaded)
                {
                    AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(Scene);
                    while (unloadOperation != null && !unloadOperation.isDone)
                    {
                        yield return null;
                    }
                }
            }

            public void AssertInitialized()
            {
                Assert.That(Composition.IsInitialized, Is.True);
                Assert.That(Composition.MatchState, Is.Not.Null);
                Assert.That(Composition.FrameCoordinator.enabled, Is.True);
                Assert.That(Composition.ObjectAdapter.IsInitialized, Is.True);
                Assert.That(Composition.CameraAdapter.HasScrollRoutingPolicy, Is.True);
                Assert.That(Composition.LockService.Count, Is.EqualTo(0));
                Assert.That(Composition.PreviewSession.IsActive, Is.False);
            }

            public CardView FirstCardIn(ContainerId containerId)
            {
                return Composition.CardViews.First(view => view.CardState.BaseState.ContainerId == containerId);
            }

            public void Select(TabletopObjectView view)
            {
                Composition.SelectionState.Select(view);
                Composition.SelectionPresenter.Refresh();
            }

            public ContainedCardDragReleaseResult DragContainedCard(CardView card, Vector3 targetWorldPosition)
            {
                Vector2 start = ScreenPoint(card.transform.position);
                Vector2 target = ScreenPoint(targetWorldPosition);
                Assert.That(Composition.ContainedCardDragCoordinator.TryBegin(card, start), Is.True);
                Composition.ContainedCardDragCoordinator.UpdatePointer(target);
                ContainedCardDragReleaseResult result = Composition.ContainedCardDragCoordinator.Release(target);
                Physics.SyncTransforms();
                RefreshStackA();
                return result;
            }

            public MoveInteractionReleaseResult? DragTabletopObject(TabletopObjectView view, Vector3 targetWorldPosition)
            {
                Vector2 start = ScreenPoint(view.transform.position);
                Vector2 target = ScreenPoint(targetWorldPosition);
                ApplyFrame(start, selectPressedThisFrame: true);
                ApplyFrame(target, selectHeld: true);
                MoveInteractionReleaseResult? result = ApplyFrame(target, selectReleasedThisFrame: true);
                RefreshStackA();
                return result;
            }

            public MoveInteractionReleaseResult? ApplyFrame(
                Vector2 screenPosition,
                bool selectPressedThisFrame = false,
                bool selectHeld = false,
                bool selectReleasedThisFrame = false,
                bool cancelPressedThisFrame = false,
                float rotateDelta = 0f,
                bool flipPressedThisFrame = false,
                float scrollDelta = 0f)
            {
                MoveInteractionReleaseResult? result = Composition.FrameCoordinator.ApplyInputFrame(
                    new TabletopInputFrame(
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
                        flipPressedThisFrame),
                    DeltaTime);
                Physics.SyncTransforms();
                return result;
            }

            public Vector3 WorldForTable(double x, double y)
            {
                return Composition.CoordinateConverter.ToWorldPosition(new TableCoordinate(x, y));
            }

            public Vector2 ScreenPoint(Vector3 worldPosition)
            {
                Vector3 point = Camera.WorldToScreenPoint(worldPosition);
                return new Vector2(point.x, point.y);
            }

            public StackView[] GetStackViews()
            {
                return Scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<StackView>(true))
                    .Where(view => view.IsBound)
                    .ToArray();
            }

            private void RefreshStackA()
            {
                Composition.LayoutViewLookup.TryGet(Composition.StackAContainerId, out IContainerLayoutView stackA);
                StackA = stackA as StackView;
            }

            private static GameObject FindPath(Scene scene, string path)
            {
                string[] parts = path.Split('/');
                GameObject current = scene.GetRootGameObjects().Single(root => root.name == parts[0]);
                for (int i = 1; i < parts.Length; i++)
                {
                    current = Enumerable.Range(0, current.transform.childCount)
                        .Select(index => current.transform.GetChild(index).gameObject)
                        .Single(child => child.name == parts[i]);
                }

                return current;
            }
        }
    }
}
