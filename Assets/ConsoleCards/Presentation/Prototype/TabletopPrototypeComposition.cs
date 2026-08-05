using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Core.Results;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;
using UnityObject = UnityEngine.Object;

namespace ConsoleCards.Presentation.Prototype
{
    public sealed class TabletopPrototypeComposition : MonoBehaviour, IContainedCardDragFeedback
    {
        private const int TotalCardCount = 16;
        private const int PrototypeConsoleSlotCount = 3;
        private const int ShuffleSeed = 123;
        private const string ButtonUpLabel = "\u2191";
        private const string ButtonDownLabel = "\u2193";
        private const string ButtonLeftLabel = "\u2190";
        private const string ButtonRightLabel = "\u2192";

        [SerializeField] internal UnityCamera targetCamera;
        [SerializeField] internal TabletopCameraInputAdapter cameraInputAdapter;
        [SerializeField] internal TabletopObjectInputAdapter objectInputAdapter;
        [SerializeField] internal TabletopInputFrameCoordinator inputFrameCoordinator;
        [SerializeField] internal PrototypeCardVisualReferences prototypeCardPrefab;
        [SerializeField] internal CardView cardView;
        [SerializeField] internal PawnView pawnView;
        [SerializeField] internal TokenView tokenView;
        [SerializeField] internal TabletopSelectionVisual cardSelectionVisual;
        [SerializeField] internal GameObject cardHighlightRoot;
        [SerializeField] internal TabletopSelectionVisual pawnSelectionVisual;
        [SerializeField] internal GameObject pawnHighlightRoot;
        [SerializeField] internal TabletopSelectionVisual tokenSelectionVisual;
        [SerializeField] internal GameObject tokenHighlightRoot;
        [SerializeField] internal PrototypeFixedContainerVisual sceneDeckVisual;
        [SerializeField] internal PrototypeFixedContainerVisual sceneStackAVisual;
        [SerializeField] internal PrototypeFixedContainerVisual sceneStackBVisual;
        [SerializeField] internal PrototypeFixedContainerVisual sceneDiscardPileVisual;
        [SerializeField] internal PrototypeFixedContainerVisual sceneHandVisual;
        [SerializeField] internal ConsoleView sceneConsoleView;
        [SerializeField] internal ConsoleSlotView[] sceneConsoleSlotViews = Array.Empty<ConsoleSlotView>();
        [SerializeField] internal PrototypeConsoleSlotVisual[] sceneConsoleSlotVisuals = Array.Empty<PrototypeConsoleSlotVisual>();

        [SerializeField] internal LayerMask interactionLayerMask;
        [SerializeField] internal float maximumHitDistance = 100f;
        [SerializeField] internal float dragThresholdPixels = 8f;
        [SerializeField] internal float worldUnitsPerTableUnit = 1f;
        [SerializeField] internal float tabletopHeight = 0f;

        private readonly List<RuntimeCardInstance> runtimeCardInstances = new List<RuntimeCardInstance>();
        private readonly List<GameObject> runtimeOwnedStackRoots = new List<GameObject>();
        private readonly List<GameObject> runtimeGeneratedHierarchyRoots = new List<GameObject>();
        private readonly List<CardView> cardViews = new List<CardView>();
        private readonly List<TabletopSelectionVisual> cardSelectionVisuals = new List<TabletopSelectionVisual>();
        private readonly List<IContainerLayoutView> layoutViews = new List<IContainerLayoutView>();
        private readonly Dictionary<TabletopObjectId, string> labelsByCardId = new Dictionary<TabletopObjectId, string>();
        private readonly Dictionary<ObjectDefinitionId, ButtonCardDefinition> buttonDefinitions =
            new Dictionary<ObjectDefinitionId, ButtonCardDefinition>();
        private readonly Dictionary<ContainerId, StackRuntimeView> stackViewsByContainerId =
            new Dictionary<ContainerId, StackRuntimeView>();
        private readonly Dictionary<ContainerId, ContainerFeedbackTarget> feedbackTargetsByContainerId =
            new Dictionary<ContainerId, ContainerFeedbackTarget>();

        private bool cameraRoutingConfiguredByComposition;
        private bool frameCoordinatorEnabledByComposition;
        private bool objectAdapterInitializedByComposition;
        private bool cardViewBoundByComposition;
        private bool pawnViewBoundByComposition;
        private bool tokenViewBoundByComposition;
        private bool disablesRepeatedStartAfterFailure;

        private MatchState matchState;
        private PlayerId localPlayerId;
        private InteractionOwnerId interactionOwnerId;
        private CardInstanceState cardState;
        private PawnState pawnState;
        private TokenState tokenState;
        private PrototypeCardVisualReferences looseCardVisualReferences;
        private bool sceneOwnedInitialPosesCaptured;
        private TabletopPose sceneLooseCardInitialPose;
        private TabletopPose scenePawnInitialPose;
        private TabletopPose sceneTokenInitialPose;
        private TabletopPose sceneDeckInitialPose;
        private TabletopPose sceneStackAInitialPose;
        private TabletopPose sceneStackBInitialPose;
        private TabletopPose sceneDiscardInitialPose;
        private TabletopCoordinateConverter coordinateConverter;
        private TabletopSelectionState selectionState;
        private TabletopObjectHitResolver hitResolver;
        private TabletopPointerProjector pointerProjector;
        private LocalInteractionLockService lockService;
        private TabletopInteractionStateMachine interactionStateMachine;
        private TabletopDragPreviewSession previewSession;
        private TabletopMoveInteractionCoordinator moveCoordinator;
        private TabletopRotationCoordinator rotationCoordinator;
        private TabletopCardFlipCoordinator flipCoordinator;
        private TabletopInteractionInputRoutingPolicy inputRoutingPolicy;
        private TabletopSelectionPresenter selectionPresenter;
        private CardDropTargetResolver dropTargetResolver;
        private CardTransferInteractionCoordinator transferCoordinator;
        private ContainedCardDragCoordinator containedCardDragCoordinator;
        private TabletopInteractionRouter interactionRouter;
        private ContainerLayoutViewLookup layoutViewLookup;

        private SeatId localSeatId;
        private ContainerId deckContainerId;
        private ContainerId handContainerId;
        private ContainerId discardContainerId;
        private ContainerId stackAContainerId;
        private ContainerId stackBContainerId;
        private ContainerId primaryStackContainerId;
        private ContainerId sourceFeedbackContainerId;
        private int dynamicStackSequence;
        private string operationMessage = "M3 prototype ready.";
        private float operationMessageUntil;

        private DeckView deckView;
        private HandView handView;
        private DiscardPileView discardPileView;
        private ConsoleView consoleView;
        private readonly List<ConsoleSlotView> consoleSlotViews = new List<ConsoleSlotView>();
        private ConsoleSlotView[] resolvedSceneConsoleSlotViews = Array.Empty<ConsoleSlotView>();
        private PrototypeConsoleSlotVisual[] resolvedSceneConsoleSlotVisuals = Array.Empty<PrototypeConsoleSlotVisual>();

        private Material cardFrontMaterial;
        private Material cardBackMaterial;
        private Material buttonCardMaterial;
        private Material stackMaterial;
        private Material validTargetMaterial;
        private Material sourceTargetMaterial;
        private Material invalidTargetMaterial;
        private Material selectionMaterial;

        public bool IsInitialized { get; private set; }

        public MatchState MatchState => matchState;

        public PlayerId LocalPlayerId => localPlayerId;

        public SeatId LocalSeatId => localSeatId;

        public CardInstanceState CardState => cardState;

        public PawnState PawnState => pawnState;

        public TokenState TokenState => tokenState;

        public TabletopCoordinateConverter CoordinateConverter => coordinateConverter;

        public TabletopSelectionState SelectionState => selectionState;

        public TabletopObjectHitResolver HitResolver => hitResolver;

        public TabletopPointerProjector PointerProjector => pointerProjector;

        public LocalInteractionLockService LockService => lockService;

        public TabletopInteractionStateMachine InteractionStateMachine => interactionStateMachine;

        public TabletopDragPreviewSession PreviewSession => previewSession;

        public TabletopMoveInteractionCoordinator MoveCoordinator => moveCoordinator;

        public TabletopRotationCoordinator RotationCoordinator => rotationCoordinator;

        public TabletopCardFlipCoordinator FlipCoordinator => flipCoordinator;

        public TabletopInteractionInputRoutingPolicy InputRoutingPolicy => inputRoutingPolicy;

        public TabletopSelectionPresenter SelectionPresenter => selectionPresenter;

        public CardDropTargetResolver DropTargetResolver => dropTargetResolver;

        public CardTransferInteractionCoordinator TransferCoordinator => transferCoordinator;

        public ContainedCardDragCoordinator ContainedCardDragCoordinator => containedCardDragCoordinator;

        public TabletopInteractionRouter InteractionRouter => interactionRouter;

        public ContainerLayoutViewLookup LayoutViewLookup => layoutViewLookup;

        public TabletopCameraInputAdapter CameraAdapter => cameraInputAdapter;

        public TabletopObjectInputAdapter ObjectAdapter => objectInputAdapter;

        public TabletopInputFrameCoordinator FrameCoordinator => inputFrameCoordinator;

        public IReadOnlyList<CardView> CardViews => cardViews.AsReadOnly();

        public DeckView DeckView => deckView;

        public HandView HandView => handView;

        public DiscardPileView DiscardPileView => discardPileView;

        public ConsoleView ConsoleView => consoleView;

        public IReadOnlyList<ConsoleSlotView> ConsoleSlotViews => consoleSlotViews.AsReadOnly();

        public IReadOnlyDictionary<ObjectDefinitionId, ButtonCardDefinition> ButtonDefinitions => buttonDefinitions;

        public ContainerId DeckContainerId => deckContainerId;

        public ContainerId HandContainerId => handContainerId;

        public ContainerId DiscardContainerId => discardContainerId;

        public ContainerId StackAContainerId => stackAContainerId;

        public ContainerId StackBContainerId => stackBContainerId;

        public void Initialize()
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition is already initialized.");
            }

            if (disablesRepeatedStartAfterFailure)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition cannot retry after a failed initialization.");
            }

            try
            {
                ValidateConfiguration();
                ReactivateSceneOwnedObjectViews();
                BuildRuntimeMaterials();
                BuildRuntimeGraph();
                BindObjectViews();
                BuildContainerViews();
                BindContainerViews();
                ConfigureDropTargets();
                BuildInteractionGraph();
                inputFrameCoordinator.ConfigureSelectionPresenter(selectionPresenter);
                inputFrameCoordinator.enabled = true;
                frameCoordinatorEnabledByComposition = true;

                if (!cameraInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator)
                    || !objectInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator))
                {
                    throw new InvalidOperationException("TabletopInputFrameCoordinator failed to attach both input adapters.");
                }

                selectionPresenter.Refresh();
                ShowMessage("M3.17 prototype ready.");
                IsInitialized = true;
            }
            catch
            {
                disablesRepeatedStartAfterFailure = true;
                Shutdown();
                throw;
            }
        }

        public void Shutdown()
        {
            ClearFeedback();

            if (frameCoordinatorEnabledByComposition && inputFrameCoordinator != null)
            {
                inputFrameCoordinator.enabled = false;
            }

            frameCoordinatorEnabledByComposition = false;

            if (inputFrameCoordinator != null)
            {
                inputFrameCoordinator.ClearSelectionPresenter();
            }

            if (objectAdapterInitializedByComposition && objectInputAdapter != null)
            {
                objectInputAdapter.Shutdown();
            }

            objectAdapterInitializedByComposition = false;

            if (cameraRoutingConfiguredByComposition && cameraInputAdapter != null)
            {
                cameraInputAdapter.ClearScrollRoutingPolicy();
            }

            cameraRoutingConfiguredByComposition = false;

            inputRoutingPolicy?.ClearInteractionRouter();
            moveCoordinator?.Reset();
            containedCardDragCoordinator?.Reset();
            previewSession?.Reset();
            lockService?.Clear();

            selectionPresenter?.Clear();
            selectionPresenter = null;
            DeactivateSelectionVisual(cardSelectionVisual);
            DeactivateSelectionVisual(pawnSelectionVisual);
            DeactivateSelectionVisual(tokenSelectionVisual);

            for (int i = 0; i < consoleSlotViews.Count; i++)
            {
                if (i < resolvedSceneConsoleSlotVisuals.Length)
                {
                    PrototypeConsoleSlotVisual slotVisual = resolvedSceneConsoleSlotVisuals[i];
                    if (slotVisual != null)
                    {
                        slotVisual.DropTarget?.ClearConfiguration();
                        slotVisual.ClearFeedback();
                    }
                }

                if (consoleSlotViews[i] != null && consoleSlotViews[i].IsBound)
                {
                    consoleSlotViews[i].Unbind();
                }
            }

            consoleView?.Unbind();

            UnbindIfOwned(cardView, ref cardViewBoundByComposition);
            UnbindIfOwned(pawnView, ref pawnViewBoundByComposition);
            UnbindIfOwned(tokenView, ref tokenViewBoundByComposition);

            selectionState = null;
            hitResolver = null;
            pointerProjector = null;
            lockService = null;
            interactionStateMachine = null;
            previewSession = null;
            dropTargetResolver = null;
            layoutViewLookup = null;
            transferCoordinator = null;
            containedCardDragCoordinator = null;
            moveCoordinator = null;
            rotationCoordinator = null;
            flipCoordinator = null;
            inputRoutingPolicy = null;
            interactionRouter = null;
            layoutViews.Clear();

            ReleaseAllStackViews();
            ReleaseSceneOwnedFixedContainerViews();
            ReleaseRuntimeCardInstances();
            ReleaseRuntimeGeneratedHierarchyRoots();
            DestroyRuntimeMaterials();

            matchState = null;
            localPlayerId = PlayerId.Empty;
            interactionOwnerId = InteractionOwnerId.Empty;
            localSeatId = SeatId.Empty;
            deckContainerId = ContainerId.Empty;
            handContainerId = ContainerId.Empty;
            discardContainerId = ContainerId.Empty;
            stackAContainerId = ContainerId.Empty;
            stackBContainerId = ContainerId.Empty;
            primaryStackContainerId = ContainerId.Empty;
            sourceFeedbackContainerId = ContainerId.Empty;
            dynamicStackSequence = 0;
            cardState = null;
            pawnState = null;
            tokenState = null;
            looseCardVisualReferences = null;
            coordinateConverter = null;
            selectionState = null;
            hitResolver = null;
            pointerProjector = null;
            lockService = null;
            interactionStateMachine = null;
            previewSession = null;
            moveCoordinator = null;
            rotationCoordinator = null;
            flipCoordinator = null;
            inputRoutingPolicy = null;
            dropTargetResolver = null;
            transferCoordinator = null;
            containedCardDragCoordinator = null;
            interactionRouter = null;
            layoutViewLookup = null;
            deckView = null;
            handView = null;
            discardPileView = null;
            consoleView = null;
            cardViews.Clear();
            cardSelectionVisuals.Clear();
            runtimeCardInstances.Clear();
            runtimeOwnedStackRoots.Clear();
            runtimeGeneratedHierarchyRoots.Clear();
            consoleSlotViews.Clear();
            resolvedSceneConsoleSlotViews = Array.Empty<ConsoleSlotView>();
            resolvedSceneConsoleSlotVisuals = Array.Empty<PrototypeConsoleSlotVisual>();
            layoutViews.Clear();
            labelsByCardId.Clear();
            buttonDefinitions.Clear();
            stackViewsByContainerId.Clear();
            feedbackTargetsByContainerId.Clear();
            IsInitialized = false;
        }

        public ShuffleDeckResult ShuffleDeck()
        {
            EnsureInitialized();
            ShuffleDeckResult result = new ShuffleDeckUseCase().Execute(
                matchState,
                new ShuffleDeckCommand(CreateCommandContext(), deckContainerId, ShuffleSeed));
            if (result.Succeeded)
            {
                deckView.ApplyAcceptedLayout();
                ShowMessage("Deck shuffled.");
            }
            else
            {
                ShowMessage($"Shuffle rejected: {result.Error}.");
            }

            return result;
        }

        public DrawCardsResult DrawOne()
        {
            return DrawCards(1);
        }

        public DrawCardsResult DrawThree()
        {
            return DrawCards(3);
        }

        public DrawCardsResult DrawCards(int count)
        {
            EnsureInitialized();
            DrawCardsResult result = new DrawCardsUseCase().Execute(
                matchState,
                new DrawCardsCommand(CreateCommandContext(), deckContainerId, handContainerId, count));
            if (result.Succeeded)
            {
                deckView.ApplyAcceptedLayout();
                handView.ApplyAcceptedLayout();
                ShowMessage($"Drew {count} card{(count == 1 ? string.Empty : "s")} to Hand.");
            }
            else
            {
                deckView.ApplyAcceptedLayout();
                handView.ApplyAcceptedLayout();
                ShowMessage($"Draw rejected: {result.Error}.");
            }

            return result;
        }

        public ReorderContainerResult MoveSelectedHandCardLeft()
        {
            return MoveSelectedCardInContainer(handContainerId, -1);
        }

        public ReorderContainerResult MoveSelectedHandCardRight()
        {
            return MoveSelectedCardInContainer(handContainerId, 1);
        }

        public ReorderContainerResult MoveSelectedStackCardDown()
        {
            return MoveSelectedCardInSelectedStack(-1);
        }

        public ReorderContainerResult MoveSelectedStackCardUp()
        {
            return MoveSelectedCardInSelectedStack(1);
        }

        public MergeStacksResult MergeStackAOntoStackB()
        {
            return MergeStacks(stackAContainerId, stackBContainerId);
        }

        public MergeStacksResult MergeStackBOntoStackA()
        {
            return MergeStacks(stackBContainerId, stackAContainerId);
        }

        public SplitStackResult SplitSelectedOrPrimaryStack()
        {
            EnsureInitialized();
            if (!TryResolveSplitSource(out ContainerState source, out StackRuntimeView sourceView))
            {
                ShowMessage("Split unavailable.");
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.SourceStackTooSmall);
            }

            int firstMovedIndex = Math.Max(1, source.Count / 2);
            ContainerId newStackId = CreateDeterministicDynamicStackId(dynamicStackSequence++);
            TabletopPose sourcePose = sourceView.Placement.Pose;
            TabletopPose newPose = new TabletopPose(
                new TableCoordinate(sourcePose.Position.X + 1.4d, sourcePose.Position.Y + 0.9d),
                sourcePose.RotationDegrees,
                sourcePose.Layer,
                sourcePose.LocalOrder);

            SplitStackResult result = new SplitStackUseCase().Execute(
                matchState,
                new SplitStackCommand(
                    CreateCommandContext(),
                    source.Id,
                    newStackId,
                    new StackSplitSpecification(firstMovedIndex),
                    newPose));

            if (result.Succeeded)
            {
                StackRuntimeView newStackView = CreateStackRuntimeView(
                    $"Stack {stackViewsByContainerId.Count + 1}",
                    matchState.GetContainer(newStackId),
                    matchState.ContainerPlacements[newStackId]);
                stackViewsByContainerId.Add(newStackId, newStackView);
                primaryStackContainerId = newStackId;
                sourceView.View.ApplyAcceptedLayout();
                newStackView.View.ApplyAcceptedLayout();
                RebuildLayoutLookupAndRouter();
                ShowMessage("Stack split.");
            }
            else
            {
                ShowMessage($"Split rejected: {result.Error}.");
            }

            return result;
        }

        public void ResetPrototype()
        {
            Shutdown();
            disablesRepeatedStartAfterFailure = false;
            Initialize();
        }

        void IContainedCardDragFeedback.Begin(ContainerId sourceContainerId)
        {
            sourceFeedbackContainerId = sourceContainerId;
            ClearFeedback();
            if (feedbackTargetsByContainerId.TryGetValue(sourceContainerId, out ContainerFeedbackTarget target))
            {
                target.SetSource();
            }
        }

        void IContainedCardDragFeedback.Update(
            ContainerId sourceContainerId,
            CardDropTarget target,
            bool targetWouldAccept)
        {
            ClearFeedback();
            if (feedbackTargetsByContainerId.TryGetValue(sourceContainerId, out ContainerFeedbackTarget sourceTarget))
            {
                sourceTarget.SetSource();
            }

            if (target.Kind != CardDropTargetKind.Container)
            {
                return;
            }

            if (!feedbackTargetsByContainerId.TryGetValue(target.ContainerId, out ContainerFeedbackTarget feedbackTarget))
            {
                return;
            }

            if (target.ContainerId == sourceContainerId)
            {
                feedbackTarget.SetSource();
            }
            else if (targetWouldAccept)
            {
                feedbackTarget.SetValid();
            }
            else
            {
                feedbackTarget.SetInvalid();
            }
        }

        void IContainedCardDragFeedback.ShowRejected(ContainerId sourceContainerId, CardDropTarget target)
        {
            if (target.Kind == CardDropTargetKind.Container
                && feedbackTargetsByContainerId.TryGetValue(target.ContainerId, out ContainerFeedbackTarget feedbackTarget))
            {
                feedbackTarget.SetInvalid();
            }

            ShowMessage("Transfer rejected.");
        }

        void IContainedCardDragFeedback.Clear()
        {
            ClearFeedback();
        }

        private void Start()
        {
            if (IsInitialized || disablesRepeatedStartAfterFailure)
            {
                return;
            }

            try
            {
                Initialize();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"TabletopPrototypeComposition failed to initialize: {exception.Message}",
                    this);
                Shutdown();
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void OnGUI()
        {
            if (!IsInitialized)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 330f, 280f, 390f), GUI.skin.box);
            GUILayout.Label("M3 Prototype Controls");
            GUILayout.Space(4f);
            if (GUILayout.Button("Shuffle Deck"))
            {
                ShuffleDeck();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Draw 1"))
            {
                DrawOne();
            }

            if (GUILayout.Button("Draw 3"))
            {
                DrawThree();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label("Hand order");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Left"))
            {
                MoveSelectedHandCardLeft();
            }

            if (GUILayout.Button("Right"))
            {
                MoveSelectedHandCardRight();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Stack order");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Down"))
            {
                MoveSelectedStackCardDown();
            }

            if (GUILayout.Button("Up"))
            {
                MoveSelectedStackCardUp();
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            if (GUILayout.Button("Merge Stack A onto Stack B"))
            {
                MergeStackAOntoStackB();
            }

            if (GUILayout.Button("Merge Stack B onto Stack A"))
            {
                MergeStackBOntoStackA();
            }

            if (GUILayout.Button("Split Selected/Primary Stack"))
            {
                SplitSelectedOrPrimaryStack();
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Reset Prototype"))
            {
                ResetPrototype();
            }

            GUILayout.Space(6f);
            GUILayout.Label(CurrentStatusText());
            GUILayout.EndArea();
        }

        private void ValidateConfiguration()
        {
            RequireReference(targetCamera, nameof(targetCamera));
            RequireReference(cameraInputAdapter, nameof(cameraInputAdapter));
            RequireReference(objectInputAdapter, nameof(objectInputAdapter));
            RequireReference(inputFrameCoordinator, nameof(inputFrameCoordinator));
            RequireReference(prototypeCardPrefab, nameof(prototypeCardPrefab));
            RequireReference(cardView, nameof(cardView));
            RequireReference(pawnView, nameof(pawnView));
            RequireReference(tokenView, nameof(tokenView));
            RequireReference(cardSelectionVisual, nameof(cardSelectionVisual));
            RequireReference(cardHighlightRoot, nameof(cardHighlightRoot));
            RequireReference(pawnSelectionVisual, nameof(pawnSelectionVisual));
            RequireReference(pawnHighlightRoot, nameof(pawnHighlightRoot));
            RequireReference(tokenSelectionVisual, nameof(tokenSelectionVisual));
            RequireReference(tokenHighlightRoot, nameof(tokenHighlightRoot));
            ValidateFixedContainerReferences();
            ValidateSceneConsoleReferences();

            if (!targetCamera.orthographic)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires an orthographic Camera.");
            }

            ValidateFiniteGreaterThanZero(maximumHitDistance, nameof(maximumHitDistance));
            ValidateFiniteGreaterThanOrEqualToZero(dragThresholdPixels, nameof(dragThresholdPixels));
            ValidateFiniteGreaterThanZero(worldUnitsPerTableUnit, nameof(worldUnitsPerTableUnit));
            ValidateFinite(tabletopHeight, nameof(tabletopHeight));
            ValidateDistinctViews();
            ValidateSelectionPresentationReferences();
            ValidateCardPrefabReferences();
            ValidatePreInitializationState();
        }

        private void ValidateCardPrefabReferences()
        {
            if (prototypeCardPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires prototypeCardPrefab to reference a prefab asset.");
            }

            prototypeCardPrefab.ValidateReferences();
            looseCardVisualReferences = cardView.GetComponent<PrototypeCardVisualReferences>();
            RequireReference(looseCardVisualReferences, nameof(looseCardVisualReferences));
            looseCardVisualReferences.ValidateReferences();

            if (!ReferenceEquals(looseCardVisualReferences.CardView, cardView)
                || !ReferenceEquals(looseCardVisualReferences.SelectionVisual, cardSelectionVisual))
            {
                throw new InvalidOperationException(
                    "The scene-owned loose Card references must resolve from its PrototypeCard instance.");
            }
        }

        private void ReactivateSceneOwnedObjectViews()
        {
            cardView.gameObject.SetActive(true);
            pawnView.gameObject.SetActive(true);
            tokenView.gameObject.SetActive(true);
            sceneDeckVisual.Reactivate();
            sceneStackAVisual.Reactivate();
            sceneStackBVisual.Reactivate();
            sceneDiscardPileVisual.Reactivate();
            sceneHandVisual.Reactivate();
        }

        private void ValidateFixedContainerReferences()
        {
            RequireReference(sceneDeckVisual, nameof(sceneDeckVisual));
            RequireReference(sceneStackAVisual, nameof(sceneStackAVisual));
            RequireReference(sceneStackBVisual, nameof(sceneStackBVisual));
            RequireReference(sceneDiscardPileVisual, nameof(sceneDiscardPileVisual));
            RequireReference(sceneHandVisual, nameof(sceneHandVisual));

            sceneDeckVisual.ValidateReferences();
            sceneStackAVisual.ValidateReferences();
            sceneStackBVisual.ValidateReferences();
            sceneDiscardPileVisual.ValidateReferences();
            sceneHandVisual.ValidateReferences();

            DeckView resolvedDeckView = sceneDeckVisual.GetView<DeckView>();
            StackView resolvedStackAView = sceneStackAVisual.GetView<StackView>();
            StackView resolvedStackBView = sceneStackBVisual.GetView<StackView>();
            DiscardPileView resolvedDiscardView = sceneDiscardPileVisual.GetView<DiscardPileView>();
            HandView resolvedHandView = sceneHandVisual.GetView<HandView>();

            if (ReferenceEquals(resolvedStackAView, resolvedStackBView))
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires distinct scene-owned Stack A and Stack B Views.");
            }

            if (!ReferenceEquals(resolvedHandView.LayoutAnchor, sceneHandVisual.LayoutAnchor)
                || ReferenceEquals(resolvedHandView.transform, sceneHandVisual.LayoutAnchor))
            {
                throw new InvalidOperationException(
                    "The scene-owned Hand must use its distinct authored layout anchor.");
            }

            if (resolvedDeckView.gameObject.scene != gameObject.scene
                || resolvedStackAView.gameObject.scene != gameObject.scene
                || resolvedStackBView.gameObject.scene != gameObject.scene
                || resolvedDiscardView.gameObject.scene != gameObject.scene
                || resolvedHandView.gameObject.scene != gameObject.scene)
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires fixed container Views from its scene.");
            }
        }

        private void ValidatePreInitializationState()
        {
            if (!cameraInputAdapter.IsInitialized)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires an initialized Camera input adapter.");
            }

            if (!objectInputAdapter.HasValidActionConfiguration)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires a valid Object input adapter action configuration.");
            }

            if (cardView.IsBound)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the CardView to begin unbound.");
            }

            if (pawnView.IsBound)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the PawnView to begin unbound.");
            }

            if (tokenView.IsBound)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the TokenView to begin unbound.");
            }

            ValidateFixedContainerPreInitializationState(sceneDeckVisual);
            ValidateFixedContainerPreInitializationState(sceneStackAVisual);
            ValidateFixedContainerPreInitializationState(sceneStackBVisual);
            ValidateFixedContainerPreInitializationState(sceneDiscardPileVisual);
            ValidateFixedContainerPreInitializationState(sceneHandVisual);

            if (sceneConsoleView.IsBound)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the scene ConsoleView to begin unbound.");
            }

            for (int i = 0; i < resolvedSceneConsoleSlotViews.Length; i++)
            {
                if (resolvedSceneConsoleSlotViews[i].IsBound)
                {
                    throw new InvalidOperationException("TabletopPrototypeComposition requires scene ConsoleSlotViews to begin unbound.");
                }

                if (resolvedSceneConsoleSlotVisuals[i].DropTarget.IsConfigured)
                {
                    throw new InvalidOperationException("TabletopPrototypeComposition requires scene Console Slot drop targets to begin unconfigured.");
                }
            }

            if (objectInputAdapter.IsInitialized)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the Object input adapter to begin uninitialized.");
            }

            if (cameraInputAdapter.HasScrollRoutingPolicy)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the Camera input adapter to begin without a scroll routing policy.");
            }

            if (inputFrameCoordinator.enabled)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the input-frame coordinator to begin disabled.");
            }

            if (!inputFrameCoordinator.IsInitialized)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires an initialized input-frame coordinator.");
            }

            if (!ReferenceEquals(inputFrameCoordinator.CameraInputAdapter, cameraInputAdapter)
                || !ReferenceEquals(inputFrameCoordinator.ObjectInputAdapter, objectInputAdapter))
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the input-frame coordinator to reference the supplied adapters.");
            }

            if (cameraInputAdapter.IsExternallyDriven || objectInputAdapter.IsExternallyDriven)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires input adapters to begin without an external frame driver.");
            }

            if (inputFrameCoordinator.HasSelectionPresenter)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the input-frame coordinator to begin without a selection presenter.");
            }
        }

        private void BuildRuntimeGraph()
        {
            localPlayerId = PlayerId.New();
            interactionOwnerId = InteractionOwnerId.New();
            localSeatId = SeatId.New();
            coordinateConverter = new TabletopCoordinateConverter(worldUnitsPerTableUnit, tabletopHeight, 0f, 0f);
            CaptureSceneOwnedInitialPoses();

            deckContainerId = ContainerId.New();
            handContainerId = ContainerId.New();
            discardContainerId = ContainerId.New();
            stackAContainerId = ContainerId.New();
            stackBContainerId = ContainerId.New();
            primaryStackContainerId = stackAContainerId;
            ContainerId slotAId = ContainerId.New();
            ContainerId slotBId = ContainerId.New();
            ContainerId slotCId = ContainerId.New();

            ContainerState deck = new ContainerState(deckContainerId, ContainerKind.Deck, SeatId.Empty, ObjectVisibility.Public, 0);
            ContainerState hand = new ContainerState(handContainerId, ContainerKind.Hand, localSeatId, ObjectVisibility.OwnerOnly, 10);
            ContainerState stackA = new ContainerState(stackAContainerId, ContainerKind.Stack, SeatId.Empty, ObjectVisibility.Public, 0);
            ContainerState stackB = new ContainerState(stackBContainerId, ContainerKind.Stack, SeatId.Empty, ObjectVisibility.Public, 0);
            ContainerState discard = new ContainerState(discardContainerId, ContainerKind.DiscardPile, SeatId.Empty, ObjectVisibility.Public, 0);
            ContainerState slotA = new ContainerState(slotAId, ContainerKind.ConsoleSlot, localSeatId, ObjectVisibility.Public, 1);
            ContainerState slotB = new ContainerState(slotBId, ContainerKind.ConsoleSlot, localSeatId, ObjectVisibility.Public, 1);
            ContainerState slotC = new ContainerState(slotCId, ContainerKind.ConsoleSlot, localSeatId, ObjectVisibility.Public, 1);
            ConsoleState console = new ConsoleState(localSeatId, new[] { slotAId, slotBId, slotCId });
            SeatState seat = new SeatState(
                localSeatId,
                new TabletopPose(new TableCoordinate(0d, -4d), 0f, 0, 0),
                handContainerId,
                console,
                localPlayerId,
                SeatStatus.Occupied);

            List<CardInstanceState> cards = CreateCards();
            PawnState createdPawnState = CreatePawnState();
            TokenState createdTokenState = CreateTokenState();

            ContainerTransferService transferService = new ContainerTransferService();
            PlaceInitialCards(transferService, cards, deck, stackA, stackB);

            matchState = new MatchState(
                MatchId.New(),
                GameTemplateId.Empty,
                0,
                cards,
                new[] { createdPawnState },
                new[] { createdTokenState },
                new[] { deck, hand, stackA, stackB, discard, slotA, slotB, slotC },
                new[] { seat },
                new[]
                {
                    new ContainerPlacementState(deckContainerId, sceneDeckInitialPose),
                    new ContainerPlacementState(stackAContainerId, sceneStackAInitialPose),
                    new ContainerPlacementState(stackBContainerId, sceneStackBInitialPose),
                    new ContainerPlacementState(discardContainerId, sceneDiscardInitialPose),
                });

            cardState = cards[0];
            pawnState = createdPawnState;
            tokenState = createdTokenState;
        }

        private List<CardInstanceState> CreateCards()
        {
            List<CardInstanceState> cards = new List<CardInstanceState>(TotalCardCount);
            string[] labels =
            {
                "1",
                "2",
                "3",
                "4",
                "5",
                "6",
                "7",
                "8",
                "A",
                "B",
                "X",
                "Y",
                ButtonUpLabel,
                ButtonDownLabel,
                ButtonLeftLabel,
                ButtonRightLabel,
            };
            ButtonCardKind?[] buttonKinds =
            {
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                ButtonCardKind.A,
                ButtonCardKind.B,
                ButtonCardKind.X,
                ButtonCardKind.Y,
                ButtonCardKind.Up,
                ButtonCardKind.Down,
                ButtonCardKind.Left,
                ButtonCardKind.Right,
            };

            for (int i = 0; i < TotalCardCount; i++)
            {
                ObjectDefinitionId definitionId = ObjectDefinitionId.New();
                TabletopPose pose = i == 0
                    ? sceneLooseCardInitialPose
                    : new TabletopPose(new TableCoordinate(-3d + (i * 0.2d), -1.5d), 0f, 0, 0);
                CardInstanceState card = new CardInstanceState(
                    CreateBaseState(
                        TabletopObjectKind.Card,
                        definitionId,
                        pose),
                    CardFace.FaceUp);
                cards.Add(card);
                labelsByCardId.Add(card.BaseState.Id, labels[i]);
                if (buttonKinds[i].HasValue)
                {
                    buttonDefinitions.Add(definitionId, new ButtonCardDefinition(definitionId, buttonKinds[i].Value));
                }
            }

            return cards;
        }

        private static void PlaceInitialCards(
            ContainerTransferService transferService,
            IReadOnlyList<CardInstanceState> cards,
            ContainerState deck,
            ContainerState stackA,
            ContainerState stackB)
        {
            Place(transferService, cards[1], stackA);
            Place(transferService, cards[2], stackA);
            Place(transferService, cards[3], stackB);

            for (int i = 4; i < cards.Count; i++)
            {
                Place(transferService, cards[i], deck);
            }
        }

        private static void Place(
            ContainerTransferService transferService,
            CardInstanceState card,
            ContainerState destination)
        {
            ContainerTransferResult result = transferService.PlaceIntoContainer(card.BaseState, destination);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Initial card placement failed: {result.Error}.");
            }
        }

        private void BindObjectViews()
        {
            ConfigureCardVisuals(
                looseCardVisualReferences,
                labelsByCardId[cardState.BaseState.Id],
                IsButtonCard(cardState));
            cardSelectionVisual.SetSelected(false);
            cardView.Bind(cardState, coordinateConverter);
            cardViewBoundByComposition = true;
            cardSelectionVisuals.Add(cardSelectionVisual);
            cardViews.Add(cardView);

            foreach (CardInstanceState card in matchState.Cards.Values)
            {
                if (ReferenceEquals(card, cardState))
                {
                    continue;
                }

                CardView createdView = CreateCardView(
                    card,
                    labelsByCardId[card.BaseState.Id],
                    out TabletopSelectionVisual createdSelectionVisual);
                cardViews.Add(createdView);
                cardSelectionVisuals.Add(createdSelectionVisual);
            }

            pawnSelectionVisual.SetSelected(false);
            pawnView.Bind(pawnState, coordinateConverter);
            pawnViewBoundByComposition = true;
            tokenSelectionVisual.SetSelected(false);
            tokenView.Bind(tokenState, coordinateConverter);
            tokenViewBoundByComposition = true;
        }

        private void BuildContainerViews()
        {
            GameObject dynamicRoot = EnsureRuntimeRoot("DynamicContainers");
            EnsureRuntimeRoot("LooseCards");

            deckView = sceneDeckVisual.GetView<DeckView>();
            handView = sceneHandVisual.GetView<HandView>();
            StackRuntimeView stackA = CreateSceneOwnedStackView(
                sceneStackAVisual,
                matchState.GetContainer(stackAContainerId),
                matchState.ContainerPlacements[stackAContainerId]);
            StackRuntimeView stackB = CreateSceneOwnedStackView(
                sceneStackBVisual,
                matchState.GetContainer(stackBContainerId),
                matchState.ContainerPlacements[stackBContainerId]);
            stackViewsByContainerId.Add(stackAContainerId, stackA);
            stackViewsByContainerId.Add(stackBContainerId, stackB);
            discardPileView = sceneDiscardPileVisual.GetView<DiscardPileView>();
            consoleView = sceneConsoleView;
            dynamicRoot.SetActive(true);
        }

        private static StackRuntimeView CreateSceneOwnedStackView(
            PrototypeFixedContainerVisual visual,
            ContainerState container,
            ContainerPlacementState placement)
        {
            StackView view = visual.GetView<StackView>();
            return new StackRuntimeView(
                StackViewOwnership.SceneOwned,
                visual.gameObject,
                view,
                container,
                placement,
                visual.DropTarget);
        }

        private void BindContainerViews()
        {
            ContainerState deck = matchState.GetContainer(deckContainerId);
            ContainerState hand = matchState.GetContainer(handContainerId);
            ContainerState discard = matchState.GetContainer(discardContainerId);

            deckView.Bind(deck, matchState.ContainerPlacements[deckContainerId], coordinateConverter, cardViews);
            handView.Bind(hand, sceneHandVisual.LayoutAnchor, coordinateConverter, cardViews);
            foreach (StackRuntimeView stackRuntimeView in stackViewsByContainerId.Values)
            {
                stackRuntimeView.View.Bind(
                    stackRuntimeView.Container,
                    stackRuntimeView.Placement,
                    coordinateConverter,
                    cardViews);
            }

            discardPileView.Bind(discard, matchState.ContainerPlacements[discardContainerId], coordinateConverter, cardViews);

            consoleSlotViews.Clear();
            ConsoleState runtimeConsole = matchState.GetSeat(localSeatId).Console;
            for (int i = 0; i < runtimeConsole.SlotCount; i++)
            {
                ContainerState slot = matchState.GetContainer(runtimeConsole.SlotContainerIds[i]);
                ConsoleSlotView slotView = resolvedSceneConsoleSlotViews[i];
                slotView.Bind(slot, resolvedSceneConsoleSlotVisuals[i].LayoutAnchor, coordinateConverter, cardViews);
                consoleSlotViews.Add(slotView);
            }

            consoleView.Bind(runtimeConsole, consoleView.LayoutAnchor, consoleSlotViews);
            RebuildLayoutViewCollection();
        }

        private void ConfigureDropTargets()
        {
            ConfigureFixedContainer(sceneDeckVisual, deckView);
            ConfigureFixedContainer(sceneHandVisual, handView);
            foreach (StackRuntimeView stackRuntimeView in stackViewsByContainerId.Values)
            {
                ConfigureStackDropTarget(stackRuntimeView);
            }

            ConfigureFixedContainer(sceneDiscardPileVisual, discardPileView);
            for (int i = 0; i < consoleSlotViews.Count; i++)
            {
                ConfigureSceneConsoleSlot(i);
            }
        }

        private void BuildInteractionGraph()
        {
            selectionState = new TabletopSelectionState();
            hitResolver = new TabletopObjectHitResolver(targetCamera, interactionLayerMask, maximumHitDistance);
            pointerProjector = new TabletopPointerProjector(targetCamera, coordinateConverter, tabletopHeight);
            lockService = new LocalInteractionLockService();
            interactionStateMachine = new TabletopInteractionStateMachine(dragThresholdPixels);
            previewSession = new TabletopDragPreviewSession();
            dropTargetResolver = new CardDropTargetResolver(
                targetCamera,
                pointerProjector,
                interactionLayerMask,
                maximumHitDistance,
                QueryTriggerInteraction.Collide);
            RebuildInteractionDependencies();
            selectionPresenter = new TabletopSelectionPresenter(
                selectionState,
                cardSelectionVisuals,
                pawnSelectionVisual,
                tokenSelectionVisual);
        }

        private void RebuildInteractionDependencies()
        {
            RebuildLayoutViewCollection();
            layoutViewLookup = new ContainerLayoutViewLookup(layoutViews);
            transferCoordinator = new CardTransferInteractionCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                lockService,
                new TransferCardUseCase(),
                layoutViews);
            containedCardDragCoordinator = new ContainedCardDragCoordinator(
                interactionOwnerId,
                lockService,
                interactionStateMachine,
                previewSession,
                pointerProjector,
                dropTargetResolver,
                transferCoordinator,
                layoutViewLookup,
                this);
            moveCoordinator = new TabletopMoveInteractionCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                selectionState,
                hitResolver,
                pointerProjector,
                lockService,
                interactionStateMachine,
                previewSession,
                new MoveObjectUseCase(),
                dropTargetResolver,
                transferCoordinator);
            rotationCoordinator = new TabletopRotationCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                selectionState,
                lockService,
                new RotateObjectUseCase());
            flipCoordinator = new TabletopCardFlipCoordinator(
                matchState,
                localPlayerId,
                interactionOwnerId,
                selectionState,
                lockService,
                new FlipCardUseCase());
            inputRoutingPolicy = new TabletopInteractionInputRoutingPolicy(selectionState, moveCoordinator);
            interactionRouter = new TabletopInteractionRouter(
                hitResolver,
                moveCoordinator,
                containedCardDragCoordinator,
                selectionState);
            inputRoutingPolicy.ConfigureInteractionRouter(interactionRouter);
            cameraInputAdapter.ConfigureScrollRoutingPolicy(inputRoutingPolicy);
            cameraRoutingConfiguredByComposition = true;
            objectInputAdapter.Initialize(moveCoordinator, rotationCoordinator, flipCoordinator, inputRoutingPolicy);
            objectInputAdapter.ConfigureInteractionRouter(interactionRouter);
            objectAdapterInitializedByComposition = true;
        }

        private void RebuildLayoutLookupAndRouter()
        {
            SuspendInteractionDependenciesForRebuild();
            ResumeInteractionDependenciesAfterRebuild();
        }

        private void SuspendInteractionDependenciesForRebuild()
        {
            if (interactionRouter != null && interactionRouter.HasActiveInteraction)
            {
                throw new InvalidOperationException("Cannot rebuild M3 interaction graph during an active interaction.");
            }

            if (frameCoordinatorEnabledByComposition)
            {
                inputFrameCoordinator.enabled = false;
                frameCoordinatorEnabledByComposition = false;
            }

            if (objectAdapterInitializedByComposition)
            {
                objectInputAdapter.Shutdown();
                objectAdapterInitializedByComposition = false;
            }

            if (cameraRoutingConfiguredByComposition)
            {
                cameraInputAdapter.ClearScrollRoutingPolicy();
                cameraRoutingConfiguredByComposition = false;
            }

            inputRoutingPolicy?.ClearInteractionRouter();
            layoutViewLookup = null;
            transferCoordinator = null;
            containedCardDragCoordinator = null;
            moveCoordinator = null;
            rotationCoordinator = null;
            flipCoordinator = null;
            inputRoutingPolicy = null;
            interactionRouter = null;
        }

        private void ResumeInteractionDependenciesAfterRebuild()
        {
            RebuildInteractionDependencies();
            inputFrameCoordinator.enabled = true;
            frameCoordinatorEnabledByComposition = true;
            selectionPresenter.Refresh();
        }

        private void RebuildLayoutViewCollection()
        {
            layoutViews.Clear();
            if (deckView != null && deckView.IsBound)
            {
                layoutViews.Add(deckView);
            }

            if (handView != null && handView.IsBound)
            {
                layoutViews.Add(handView);
            }

            foreach (StackRuntimeView stackRuntimeView in stackViewsByContainerId.Values)
            {
                if (stackRuntimeView.View != null && stackRuntimeView.View.IsBound)
                {
                    layoutViews.Add(stackRuntimeView.View);
                }
            }

            if (discardPileView != null && discardPileView.IsBound)
            {
                layoutViews.Add(discardPileView);
            }

            for (int i = 0; i < consoleSlotViews.Count; i++)
            {
                if (consoleSlotViews[i] != null && consoleSlotViews[i].IsBound)
                {
                    layoutViews.Add(consoleSlotViews[i]);
                }
            }
        }

        private ReorderContainerResult MoveSelectedCardInSelectedStack(int delta)
        {
            EnsureInitialized();
            CardView selectedCard = SelectionState.SelectedView as CardView;
            if (selectedCard == null || selectedCard.CardState == null)
            {
                ShowMessage("Select a Stack card first.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing);
            }

            ContainerId containerId = selectedCard.CardState.BaseState.ContainerId;
            if (!stackViewsByContainerId.ContainsKey(containerId))
            {
                ShowMessage("Selected Card is not in a Stack.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ContainerMissing);
            }

            return MoveSelectedCardInContainer(containerId, delta);
        }

        private ReorderContainerResult MoveSelectedCardInContainer(ContainerId containerId, int delta)
        {
            EnsureInitialized();
            CardView selectedCard = SelectionState.SelectedView as CardView;
            if (selectedCard == null || selectedCard.CardState == null)
            {
                ShowMessage("Select a contained Card first.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing);
            }

            if (selectedCard.CardState.BaseState.ContainerId != containerId)
            {
                ShowMessage("Selected Card is not in that Container.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectContainerMismatch);
            }

            ContainerState container = matchState.GetContainer(containerId);
            int fromIndex = container.IndexOf(selectedCard.ObjectId);
            int toIndex = Mathf.Clamp(fromIndex + delta, 0, container.Count - 1);
            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                matchState,
                new ReorderContainerCommand(
                    CreateCommandContext(),
                    containerId,
                    selectedCard.ObjectId,
                    fromIndex,
                    toIndex));

            if (result.Succeeded)
            {
                ApplyLayout(containerId);
                ShowMessage("Card reordered.");
            }
            else
            {
                ApplyLayout(containerId);
                ShowMessage($"Reorder rejected: {result.Error}.");
            }

            return result;
        }

        private MergeStacksResult MergeStacks(ContainerId sourceId, ContainerId destinationId)
        {
            EnsureInitialized();
            if (!stackViewsByContainerId.ContainsKey(sourceId)
                || !stackViewsByContainerId.ContainsKey(destinationId)
                || !matchState.Containers.ContainsKey(sourceId)
                || !matchState.Containers.ContainsKey(destinationId))
            {
                ShowMessage("Merge unavailable.");
                return MergeStacksResult.Failure(CommandResultStatus.Rejected, MergeStacksError.SourceStackMissing);
            }

            MergeStacksResult result = new MergeStacksUseCase().Execute(
                matchState,
                new MergeStacksCommand(CreateCommandContext(), sourceId, destinationId));
            if (result.Succeeded)
            {
                StackRuntimeView destinationView = stackViewsByContainerId[destinationId];
                destinationView.View.ApplyAcceptedLayout();
                RemoveStackRuntimeView(sourceId);
                primaryStackContainerId = destinationId;
                ShowMessage("Stacks merged.");
            }
            else
            {
                ShowMessage($"Merge rejected: {result.Error}.");
            }

            return result;
        }

        private bool TryResolveSplitSource(out ContainerState source, out StackRuntimeView sourceView)
        {
            source = null;
            sourceView = null;
            CardView selectedCard = SelectionState.SelectedView as CardView;
            if (selectedCard != null
                && selectedCard.CardState != null
                && stackViewsByContainerId.TryGetValue(selectedCard.CardState.BaseState.ContainerId, out sourceView)
                && matchState.Containers.TryGetValue(selectedCard.CardState.BaseState.ContainerId, out source)
                && source.Count >= 2)
            {
                return true;
            }

            if (!primaryStackContainerId.IsEmpty
                && stackViewsByContainerId.TryGetValue(primaryStackContainerId, out sourceView)
                && matchState.Containers.TryGetValue(primaryStackContainerId, out source)
                && source.Count >= 2)
            {
                return true;
            }

            foreach (KeyValuePair<ContainerId, StackRuntimeView> pair in stackViewsByContainerId)
            {
                if (matchState.Containers.TryGetValue(pair.Key, out source) && source.Count >= 2)
                {
                    sourceView = pair.Value;
                    return true;
                }
            }

            return false;
        }

        private void RemoveStackRuntimeView(ContainerId containerId)
        {
            if (!stackViewsByContainerId.TryGetValue(containerId, out StackRuntimeView stackRuntimeView))
            {
                return;
            }

            SuspendInteractionDependenciesForRebuild();
            ReleaseStackView(containerId, stackRuntimeView);
            ResumeInteractionDependenciesAfterRebuild();
        }

        private void ApplyLayout(ContainerId containerId)
        {
            if (containerId == deckContainerId)
            {
                deckView.ApplyAcceptedLayout();
            }
            else if (containerId == handContainerId)
            {
                handView.ApplyAcceptedLayout();
            }
            else if (containerId == discardContainerId)
            {
                discardPileView.ApplyAcceptedLayout();
            }
            else if (stackViewsByContainerId.TryGetValue(containerId, out StackRuntimeView stackRuntimeView))
            {
                stackRuntimeView.View.ApplyAcceptedLayout();
            }
            else
            {
                for (int i = 0; i < consoleSlotViews.Count; i++)
                {
                    if (consoleSlotViews[i].ContainerId == containerId)
                    {
                        consoleSlotViews[i].ApplyAcceptedLayout();
                    }
                }
            }
        }

        private CommandContext CreateCommandContext()
        {
            return new CommandContext(CommandId.New(), matchState.Id, localPlayerId, matchState.Revision);
        }

        private CardView CreateCardView(
            CardInstanceState card,
            string label,
            out TabletopSelectionVisual selectionVisual)
        {
            PrototypeCardVisualReferences createdVisualReferences = Instantiate(prototypeCardPrefab);
            GameObject clone = createdVisualReferences.gameObject;
            clone.name = $"Card {label}";
            RuntimeCardInstance runtimeCardInstance = new RuntimeCardInstance(clone);
            runtimeCardInstances.Add(runtimeCardInstance);
            createdVisualReferences.ValidateReferences();
            clone.transform.SetParent(EnsureRuntimeRoot("LooseCards").transform, false);
            CardView createdView = createdVisualReferences.CardView;
            selectionVisual = createdVisualReferences.SelectionVisual;
            runtimeCardInstance.SetReferences(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            ConfigureCardVisuals(createdVisualReferences, label, IsButtonCard(card));
            createdView.Bind(card, coordinateConverter);
            return createdView;
        }

        private void ConfigureCardVisuals(
            PrototypeCardVisualReferences visualReferences,
            string label,
            bool isButtonCard)
        {
            Material frontMaterial = isButtonCard ? buttonCardMaterial : cardFrontMaterial;
            visualReferences.FaceUpRenderer.sharedMaterial = frontMaterial;
            visualReferences.FaceDownRenderer.sharedMaterial = cardBackMaterial;
            visualReferences.FrontLabel.text = label;
        }

        private StackRuntimeView CreateStackRuntimeView(
            string name,
            ContainerState container,
            ContainerPlacementState placement)
        {
            Transform parent = EnsureRuntimeRoot("DynamicContainers").transform;
            GameObject root = CreateContainerRoot(
                name,
                parent,
                placement.Pose.Position,
                stackMaterial,
                new Vector3(1.25f, 0.045f, 1.65f));
            StackView view = root.AddComponent<StackView>();
            AddContainerLabel(root.transform, name, Color.white);
            view.Bind(container, placement, coordinateConverter, cardViews);
            StackRuntimeView stackRuntimeView = new StackRuntimeView(
                StackViewOwnership.RuntimeOwned,
                root,
                view,
                container,
                placement,
                null);
            stackRuntimeView.DropTarget = ConfigureDropTarget(root, view, stackMaterial);
            return stackRuntimeView;
        }

        private GameObject CreateContainerRoot(
            string name,
            Transform parent,
            TableCoordinate coordinate,
            Material material,
            Vector3 boundaryScale)
        {
            GameObject root = new GameObject(name);
            runtimeOwnedStackRoots.Add(root);

            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            root.transform.position = coordinateConverter.ToWorldPosition(coordinate);
            root.transform.rotation = Quaternion.identity;
            SetLayerRecursively(root, LayerMask.NameToLayer("TabletopObject"));

            GameObject boundary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boundary.name = "Boundary";
            boundary.transform.SetParent(root.transform, false);
            boundary.transform.localPosition = Vector3.zero;
            boundary.transform.localRotation = Quaternion.identity;
            boundary.transform.localScale = boundaryScale;
            ApplyMaterialToRenderers(boundary, material);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(boundaryScale.x, 0.08f, boundaryScale.z);
            collider.center = Vector3.zero;
            Collider boundaryCollider = boundary.GetComponent<Collider>();
            if (boundaryCollider != null)
            {
                boundaryCollider.enabled = false;
                UnityObject.Destroy(boundaryCollider);
            }

            return root;
        }

        private TabletopContainerDropTarget ConfigureDropTarget(
            GameObject root,
            IContainerView view,
            Material baseMaterial)
        {
            TabletopContainerDropTarget target = root.GetComponent<TabletopContainerDropTarget>();
            if (target == null)
            {
                target = root.AddComponent<TabletopContainerDropTarget>();
            }

            Collider collider = root.GetComponent<Collider>();
            collider.enabled = true;
            target.enabled = true;
            target.Configure(view, collider);
            Renderer renderer = FindChild(root.transform, "Boundary").GetComponent<Renderer>();
            feedbackTargetsByContainerId[view.ContainerId] = new ContainerFeedbackTarget(
                renderer,
                baseMaterial,
                validTargetMaterial,
                sourceTargetMaterial,
                invalidTargetMaterial);
            return target;
        }

        private void ConfigureStackDropTarget(StackRuntimeView stackRuntimeView)
        {
            if (stackRuntimeView.Ownership == StackViewOwnership.SceneOwned)
            {
                PrototypeFixedContainerVisual visual = ReferenceEquals(stackRuntimeView.View, sceneStackAVisual.ContainerView)
                    ? sceneStackAVisual
                    : sceneStackBVisual;
                ConfigureFixedContainer(visual, stackRuntimeView.View);
                stackRuntimeView.DropTarget = visual.DropTarget;
                return;
            }

            TabletopContainerDropTarget dropTarget = ConfigureDropTarget(
                stackRuntimeView.Root,
                stackRuntimeView.View,
                stackMaterial);
            stackRuntimeView.DropTarget = dropTarget;
        }

        private void ConfigureSceneConsoleSlot(int index)
        {
            ConsoleSlotView slotView = consoleSlotViews[index];
            PrototypeConsoleSlotVisual slotVisual = resolvedSceneConsoleSlotVisuals[index];
            slotVisual.DropTarget.Configure(slotView, slotVisual.TargetCollider);
            slotVisual.ClearFeedback();
            feedbackTargetsByContainerId[slotView.ContainerId] = new ContainerFeedbackTarget(slotVisual);
        }

        private void AddContainerLabel(Transform root, string text, Color color)
        {
            Transform label = FindOrCreateChild(root, "Label");
            TextMesh mesh = label.GetComponent<TextMesh>();
            if (mesh == null)
            {
                mesh = label.gameObject.AddComponent<TextMesh>();
            }

            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = 0.18f;
            mesh.color = color;
            label.localPosition = new Vector3(0f, 0.08f, -0.95f);
            label.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void BuildRuntimeMaterials()
        {
            cardFrontMaterial = CreateMaterial("M3 Card Front", new Color(0.95f, 0.88f, 0.42f));
            cardBackMaterial = CreateMaterial("M3 Card Back", new Color(0.10f, 0.19f, 0.42f));
            buttonCardMaterial = CreateMaterial("M3 Button Card", new Color(0.58f, 0.88f, 0.82f));
            stackMaterial = CreateMaterial("M3 Stack Boundary", new Color(0.24f, 0.21f, 0.29f));
            validTargetMaterial = CreateMaterial("M3 Valid Target", new Color(0.18f, 0.62f, 0.38f));
            sourceTargetMaterial = CreateMaterial("M3 Source Target", new Color(0.34f, 0.37f, 0.42f));
            invalidTargetMaterial = CreateMaterial("M3 Invalid Target", new Color(0.72f, 0.18f, 0.16f));
            selectionMaterial = CreateMaterial("M3 Selection", new Color(0.90f, 0.30f, 0.80f));
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else
            {
                material.color = color;
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.12f);
            }

            return material;
        }

        private void DestroyRuntimeMaterials()
        {
            DestroyMaterial(cardFrontMaterial);
            DestroyMaterial(cardBackMaterial);
            DestroyMaterial(buttonCardMaterial);
            DestroyMaterial(stackMaterial);
            DestroyMaterial(validTargetMaterial);
            DestroyMaterial(sourceTargetMaterial);
            DestroyMaterial(invalidTargetMaterial);
            DestroyMaterial(selectionMaterial);
            cardFrontMaterial = null;
            cardBackMaterial = null;
            buttonCardMaterial = null;
            stackMaterial = null;
            validTargetMaterial = null;
            sourceTargetMaterial = null;
            invalidTargetMaterial = null;
            selectionMaterial = null;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private bool IsButtonCard(CardInstanceState card)
        {
            return buttonDefinitions.ContainsKey(card.BaseState.DefinitionId);
        }

        private PawnState CreatePawnState()
        {
            return new PawnState(
                CreateBaseState(
                    TabletopObjectKind.Pawn,
                    ObjectDefinitionId.New(),
                    scenePawnInitialPose));
        }

        private TokenState CreateTokenState()
        {
            return new TokenState(
                CreateBaseState(
                    TabletopObjectKind.Token,
                    ObjectDefinitionId.New(),
                    sceneTokenInitialPose));
        }

        private void CaptureSceneOwnedInitialPoses()
        {
            if (sceneOwnedInitialPosesCaptured)
            {
                return;
            }

            sceneLooseCardInitialPose = CreateSceneAuthoredPose(cardView);
            scenePawnInitialPose = CreateSceneAuthoredPose(pawnView);
            sceneTokenInitialPose = CreateSceneAuthoredPose(tokenView);
            sceneDeckInitialPose = CreateSceneAuthoredPose(sceneDeckVisual.transform);
            sceneStackAInitialPose = CreateSceneAuthoredPose(sceneStackAVisual.transform);
            sceneStackBInitialPose = CreateSceneAuthoredPose(sceneStackBVisual.transform);
            sceneDiscardInitialPose = CreateSceneAuthoredPose(sceneDiscardPileVisual.transform);
            sceneOwnedInitialPosesCaptured = true;
        }

        private TabletopPose CreateSceneAuthoredPose(TabletopObjectView view)
        {
            return CreateSceneAuthoredPose(view.transform);
        }

        private TabletopPose CreateSceneAuthoredPose(Transform authoredTransform)
        {
            return new TabletopPose(
                coordinateConverter.ToTableCoordinate(authoredTransform.position),
                authoredTransform.eulerAngles.y,
                0,
                0);
        }

        private static TabletopObjectState CreateBaseState(
            TabletopObjectKind kind,
            ObjectDefinitionId definitionId,
            TabletopPose pose)
        {
            return new TabletopObjectState(
                TabletopObjectId.New(),
                definitionId,
                kind,
                pose,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
        }

        private ContainerId CreateDeterministicDynamicStackId(int sequence)
        {
            byte[] bytes = localSeatId.Value.ToByteArray();
            bytes[0] = (byte)(bytes[0] ^ 0x5a);
            bytes[1] = (byte)(bytes[1] ^ sequence);
            bytes[2] = (byte)(bytes[2] ^ (sequence >> 8));
            return new ContainerId(new Guid(bytes));
        }

        private void ClearFeedback()
        {
            foreach (ContainerFeedbackTarget target in feedbackTargetsByContainerId.Values)
            {
                target.Clear();
            }
        }

        private string CurrentStatusText()
        {
            if (Time.unscaledTime <= operationMessageUntil)
            {
                return operationMessage;
            }

            return $"Deck {ContainerCount(deckContainerId)} | Hand {ContainerCount(handContainerId)} | Discard {ContainerCount(discardContainerId)}";
        }

        private int ContainerCount(ContainerId containerId)
        {
            return matchState != null && matchState.Containers.TryGetValue(containerId, out ContainerState container)
                ? container.Count
                : 0;
        }

        private void ShowMessage(string message)
        {
            operationMessage = message;
            operationMessageUntil = Time.unscaledTime + 2.5f;
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition is not initialized.");
            }
        }

        private GameObject EnsureRuntimeRoot(string name)
        {
            Scene scene = gameObject.scene;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            if (!runtimeGeneratedHierarchyRoots.Contains(root))
            {
                runtimeGeneratedHierarchyRoots.Add(root);
            }

            return root;
        }

        private void ReleaseAllStackViews()
        {
            List<KeyValuePair<ContainerId, StackRuntimeView>> activeStackViews =
                new List<KeyValuePair<ContainerId, StackRuntimeView>>(stackViewsByContainerId);
            for (int i = activeStackViews.Count - 1; i >= 0; i--)
            {
                ReleaseStackView(activeStackViews[i].Key, activeStackViews[i].Value);
            }

            while (runtimeOwnedStackRoots.Count > 0)
            {
                int lastIndex = runtimeOwnedStackRoots.Count - 1;
                GameObject root = runtimeOwnedStackRoots[lastIndex];
                runtimeOwnedStackRoots.RemoveAt(lastIndex);
                DisableRuntimeInteraction(root);
                DestroyRuntimeOwnedGameObject(root);
            }

            stackViewsByContainerId.Clear();
        }

        private void ReleaseStackView(ContainerId containerId, StackRuntimeView stackRuntimeView)
        {
            if (stackRuntimeView == null)
            {
                return;
            }

            StackViewOwnership ownership = stackRuntimeView.Ownership;
            GameObject root = stackRuntimeView.Root;
            StackView view = stackRuntimeView.View;
            TabletopContainerDropTarget dropTarget = stackRuntimeView.DropTarget;

            DisableRuntimeInteraction(root);
            if (dropTarget != null)
            {
                dropTarget.ClearConfiguration();
                dropTarget.enabled = false;
            }

            if (view != null && view.IsBound)
            {
                view.Unbind();
            }

            if (ownership == StackViewOwnership.SceneOwned)
            {
                PrototypeFixedContainerVisual sceneVisual = ReferenceEquals(view, sceneStackAVisual.ContainerView)
                    ? sceneStackAVisual
                    : sceneStackBVisual;
                sceneVisual.ClearFeedback();
            }

            feedbackTargetsByContainerId.Remove(containerId);
            stackViewsByContainerId.Remove(containerId);
            if (view != null)
            {
                layoutViews.Remove(view);
            }

            runtimeOwnedStackRoots.Remove(root);
            stackRuntimeView.ClearReferences();

            if (root == null)
            {
                return;
            }

            root.SetActive(false);
            if (ownership == StackViewOwnership.RuntimeOwned)
            {
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ConfigureFixedContainer(
            PrototypeFixedContainerVisual visual,
            IContainerView view)
        {
            visual.TargetCollider.enabled = true;
            visual.DropTarget.enabled = true;
            visual.DropTarget.Configure(view, visual.TargetCollider);
            visual.ClearFeedback();
            feedbackTargetsByContainerId[view.ContainerId] = new ContainerFeedbackTarget(visual);
        }

        private static void ValidateFixedContainerPreInitializationState(
            PrototypeFixedContainerVisual visual)
        {
            if (visual.ContainerView.IsBound)
            {
                throw new InvalidOperationException(
                    $"TabletopPrototypeComposition requires scene {visual.name} to begin unbound.");
            }

            if (visual.DropTarget.IsConfigured)
            {
                throw new InvalidOperationException(
                    $"TabletopPrototypeComposition requires scene {visual.name} drop target to begin unconfigured.");
            }
        }

        private void ReleaseSceneOwnedFixedContainerViews()
        {
            ReleaseSceneOwnedFixedStack(sceneStackAVisual, stackAContainerId);
            ReleaseSceneOwnedFixedStack(sceneStackBVisual, stackBContainerId);
            ReleaseSceneOwnedFixedContainer(sceneDeckVisual, deckView, deckContainerId);
            deckView = null;
            ReleaseSceneOwnedFixedContainer(sceneHandVisual, handView, handContainerId);
            handView = null;
            ReleaseSceneOwnedFixedContainer(sceneDiscardPileVisual, discardPileView, discardContainerId);
            discardPileView = null;
        }

        private void ReleaseSceneOwnedFixedStack(
            PrototypeFixedContainerVisual visual,
            ContainerId containerId)
        {
            if (visual == null)
            {
                return;
            }

            visual.DropTarget?.ClearConfiguration();
            if (visual.DropTarget != null)
            {
                visual.DropTarget.enabled = false;
            }

            if (visual.TargetCollider != null)
            {
                visual.TargetCollider.enabled = false;
            }

            visual.ClearFeedback();
            StackView view = visual.GetView<StackView>();
            if (view.IsBound)
            {
                view.Unbind();
            }

            feedbackTargetsByContainerId.Remove(containerId);
            layoutViews.Remove(view);
            visual.gameObject.SetActive(false);
        }

        private void ReleaseSceneOwnedFixedContainer(
            PrototypeFixedContainerVisual visual,
            IContainerLayoutView view,
            ContainerId containerId)
        {
            if (visual == null)
            {
                return;
            }

            visual.DropTarget?.ClearConfiguration();
            if (visual.DropTarget != null)
            {
                visual.DropTarget.enabled = false;
            }

            if (visual.TargetCollider != null)
            {
                visual.TargetCollider.enabled = false;
            }

            visual.ClearFeedback();
            if (view != null && view.IsBound)
            {
                UnbindFixedContainerView(view);
            }

            feedbackTargetsByContainerId.Remove(containerId);
            if (view != null)
            {
                layoutViews.Remove(view);
            }

            visual.gameObject.SetActive(false);
        }

        private static void UnbindFixedContainerView(IContainerLayoutView view)
        {
            if (view is DeckView deck)
            {
                deck.Unbind();
            }
            else if (view is HandView hand)
            {
                hand.Unbind();
            }
            else if (view is DiscardPileView discard)
            {
                discard.Unbind();
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported fixed container View type {view.GetType().Name}.");
            }
        }

        private void ReleaseRuntimeCardInstances()
        {
            while (runtimeCardInstances.Count > 0)
            {
                int lastIndex = runtimeCardInstances.Count - 1;
                RuntimeCardInstance runtimeCardInstance = runtimeCardInstances[lastIndex];
                GameObject root = runtimeCardInstance.Root;
                CardView view = runtimeCardInstance.View;
                TabletopSelectionVisual selectionVisual = runtimeCardInstance.SelectionVisual;

                DisableRuntimeInteraction(root);
                selectionVisual?.Clear();
                if (view != null && view.IsBound)
                {
                    view.Unbind();
                }

                if (view != null)
                {
                    cardViews.Remove(view);
                }

                if (selectionVisual != null)
                {
                    cardSelectionVisuals.Remove(selectionVisual);
                }

                runtimeCardInstances.RemoveAt(lastIndex);
                runtimeCardInstance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ReleaseRuntimeGeneratedHierarchyRoots()
        {
            while (runtimeGeneratedHierarchyRoots.Count > 0)
            {
                int lastIndex = runtimeGeneratedHierarchyRoots.Count - 1;
                GameObject root = runtimeGeneratedHierarchyRoots[lastIndex];
                runtimeGeneratedHierarchyRoots.RemoveAt(lastIndex);
                DisableRuntimeInteraction(root);
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private static void DisableRuntimeInteraction(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            TabletopContainerDropTarget dropTarget = root.GetComponent<TabletopContainerDropTarget>();
            if (dropTarget != null)
            {
                dropTarget.ClearConfiguration();
                dropTarget.enabled = false;
            }

            Collider[] colliders = root.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        private static void DestroyRuntimeOwnedGameObject(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(false);
            root.name = $"{root.name} (Pending Runtime Destruction)";
            Destroy(root);
        }

        private static void ApplyMaterialToRenderers(GameObject root, Material material)
        {
            if (root == null || material == null)
            {
                return;
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = material;
            }
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                throw new InvalidOperationException($"Missing child '{name}' under '{parent.name}'.");
            }

            return child;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        private void ValidateDistinctViews()
        {
            if (ReferenceEquals(cardView, pawnView)
                || ReferenceEquals(cardView, tokenView)
                || ReferenceEquals(pawnView, tokenView))
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires distinct Card, Pawn, and Token Views.");
            }

            if (ReferenceEquals(cardView.gameObject, pawnView.gameObject)
                || ReferenceEquals(cardView.gameObject, tokenView.gameObject)
                || ReferenceEquals(pawnView.gameObject, tokenView.gameObject))
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires Card, Pawn, and Token Views on distinct GameObjects.");
            }
        }

        private void ValidateSelectionPresentationReferences()
        {
            if (!ReferenceEquals(cardSelectionVisual.gameObject, cardView.gameObject))
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the Card selection visual on the CardView GameObject.");
            }

            if (!ReferenceEquals(pawnSelectionVisual.gameObject, pawnView.gameObject))
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the Pawn selection visual on the PawnView GameObject.");
            }

            if (!ReferenceEquals(tokenSelectionVisual.gameObject, tokenView.gameObject))
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires the Token selection visual on the TokenView GameObject.");
            }

            if (ReferenceEquals(cardSelectionVisual, pawnSelectionVisual)
                || ReferenceEquals(cardSelectionVisual, tokenSelectionVisual)
                || ReferenceEquals(pawnSelectionVisual, tokenSelectionVisual))
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires distinct selection visual components.");
            }

            if (ReferenceEquals(cardHighlightRoot, pawnHighlightRoot)
                || ReferenceEquals(cardHighlightRoot, tokenHighlightRoot)
                || ReferenceEquals(pawnHighlightRoot, tokenHighlightRoot))
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires distinct selection highlight roots.");
            }

            if (!cardSelectionVisual.IsConfigured
                || !pawnSelectionVisual.IsConfigured
                || !tokenSelectionVisual.IsConfigured)
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires prefab-authored selection visual references.");
            }

            if (!ReferenceEquals(cardSelectionVisual.ObjectView, cardView)
                || !ReferenceEquals(pawnSelectionVisual.ObjectView, pawnView)
                || !ReferenceEquals(tokenSelectionVisual.ObjectView, tokenView))
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires each selection visual to reference its scene-owned View.");
            }

            if (!ReferenceEquals(cardSelectionVisual.HighlightRoot, cardHighlightRoot)
                || !ReferenceEquals(pawnSelectionVisual.HighlightRoot, pawnHighlightRoot)
                || !ReferenceEquals(tokenSelectionVisual.HighlightRoot, tokenHighlightRoot))
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition selection roots must match the prefab-authored references.");
            }

            if (cardHighlightRoot.activeSelf
                || pawnHighlightRoot.activeSelf
                || tokenHighlightRoot.activeSelf)
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires selection highlight roots to begin inactive.");
            }
        }

        private void ValidateSceneConsoleReferences()
        {
            RequireReference(sceneConsoleView, nameof(sceneConsoleView));
            resolvedSceneConsoleSlotViews = ResolveSceneConsoleSlotViews();
            resolvedSceneConsoleSlotVisuals = new PrototypeConsoleSlotVisual[PrototypeConsoleSlotCount];

            HashSet<ConsoleSlotView> seenViews = new HashSet<ConsoleSlotView>();
            for (int i = 0; i < PrototypeConsoleSlotCount; i++)
            {
                ConsoleSlotView slotView = resolvedSceneConsoleSlotViews[i];
                if (!seenViews.Add(slotView))
                {
                    throw new InvalidOperationException("Duplicate ConsoleSlotView detected.");
                }

                if (!slotView.transform.IsChildOf(sceneConsoleView.transform))
                {
                    throw new InvalidOperationException("ConsoleSlotView must belong to sceneConsoleView hierarchy.");
                }

                Collider targetCollider = slotView.GetComponent<Collider>();
                if (targetCollider == null)
                {
                    throw new InvalidOperationException("ConsoleSlotView requires a Collider on its Slot root.");
                }

                TabletopContainerDropTarget dropTarget = slotView.GetComponent<TabletopContainerDropTarget>();
                if (dropTarget == null)
                {
                    throw new InvalidOperationException("ConsoleSlotView requires a TabletopContainerDropTarget on its Slot root.");
                }

                PrototypeConsoleSlotVisual slotVisual = ResolveSceneConsoleSlotVisual(slotView);
                RequireReference(slotVisual, $"PrototypeConsoleSlotVisual for Console Slot {i}");
                slotVisual.ValidateReferences();
                if (!ReferenceEquals(slotVisual.SlotView, slotView))
                {
                    throw new InvalidOperationException("TabletopPrototypeComposition Console Slot View order must match its authored visual order.");
                }

                if (!ReferenceEquals(slotVisual.TargetCollider, targetCollider)
                    || !ReferenceEquals(slotVisual.DropTarget, dropTarget))
                {
                    throw new InvalidOperationException("ConsoleSlotView Collider and drop target must match its authored visual references.");
                }

                resolvedSceneConsoleSlotVisuals[i] = slotVisual;
            }

            for (int i = 1; i < resolvedSceneConsoleSlotViews.Length; i++)
            {
                if (CompareConsoleHierarchyOrder(
                        resolvedSceneConsoleSlotViews[i - 1].transform,
                        resolvedSceneConsoleSlotViews[i].transform) >= 0)
                {
                    throw new InvalidOperationException("ConsoleSlotView sibling order must be stable.");
                }
            }
        }

        private ConsoleSlotView[] ResolveSceneConsoleSlotViews()
        {
            ConsoleSlotView[] resolvedViews;
            if (sceneConsoleSlotViews != null
                && sceneConsoleSlotViews.Length == PrototypeConsoleSlotCount
                && Array.TrueForAll(sceneConsoleSlotViews, view => view != null))
            {
                resolvedViews = (ConsoleSlotView[])sceneConsoleSlotViews.Clone();
            }
            else
            {
                resolvedViews = sceneConsoleView.GetComponentsInChildren<ConsoleSlotView>(true);
            }

            if (resolvedViews.Length != PrototypeConsoleSlotCount)
            {
                throw new InvalidOperationException(
                    $"Expected exactly {PrototypeConsoleSlotCount} ConsoleSlotView components under sceneConsoleView.");
            }

            for (int i = 0; i < resolvedViews.Length; i++)
            {
                if (resolvedViews[i] == null)
                {
                    throw new InvalidOperationException("ConsoleSlotView resolution returned a null entry.");
                }

                if (!resolvedViews[i].transform.IsChildOf(sceneConsoleView.transform))
                {
                    throw new InvalidOperationException("ConsoleSlotView must belong to sceneConsoleView hierarchy.");
                }
            }

            Array.Sort(
                resolvedViews,
                (left, right) => CompareConsoleHierarchyOrder(left.transform, right.transform));
            return resolvedViews;
        }

        private PrototypeConsoleSlotVisual ResolveSceneConsoleSlotVisual(ConsoleSlotView slotView)
        {
            if (sceneConsoleSlotVisuals != null)
            {
                for (int i = 0; i < sceneConsoleSlotVisuals.Length; i++)
                {
                    PrototypeConsoleSlotVisual explicitVisual = sceneConsoleSlotVisuals[i];
                    if (explicitVisual != null && ReferenceEquals(explicitVisual.SlotView, slotView))
                    {
                        return explicitVisual;
                    }
                }
            }

            return slotView.GetComponent<PrototypeConsoleSlotVisual>();
        }

        private int CompareConsoleHierarchyOrder(Transform left, Transform right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            List<int> leftOrder = GetConsoleHierarchyOrder(left);
            List<int> rightOrder = GetConsoleHierarchyOrder(right);
            int sharedDepth = Math.Min(leftOrder.Count, rightOrder.Count);
            for (int i = 0; i < sharedDepth; i++)
            {
                int comparison = leftOrder[i].CompareTo(rightOrder[i]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return leftOrder.Count.CompareTo(rightOrder.Count);
        }

        private List<int> GetConsoleHierarchyOrder(Transform slotTransform)
        {
            List<int> order = new List<int>();
            Transform current = slotTransform;
            while (!ReferenceEquals(current, sceneConsoleView.transform))
            {
                order.Add(current.GetSiblingIndex());
                current = current.parent;
                if (current == null)
                {
                    throw new InvalidOperationException("ConsoleSlotView must belong to sceneConsoleView hierarchy.");
                }
            }

            order.Reverse();
            return order;
        }

        private static void UnbindIfOwned(TabletopObjectView view, ref bool boundByComposition)
        {
            if (!boundByComposition)
            {
                return;
            }

            if (view != null && view.IsBound)
            {
                view.Unbind();
            }

            boundByComposition = false;
        }

        private static void DeactivateSelectionVisual(TabletopSelectionVisual visual)
        {
            if (visual != null && visual.IsConfigured)
            {
                visual.SetSelected(false);
            }
        }

        private static void RequireReference(UnityEngine.Object reference, string name)
        {
            if (reference == null)
            {
                throw new InvalidOperationException($"TabletopPrototypeComposition requires {name}.");
            }
        }

        private static void ValidateFiniteGreaterThanZero(float value, string name)
        {
            ValidateFinite(value, name);
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void ValidateFiniteGreaterThanOrEqualToZero(float value, string name)
        {
            ValidateFinite(value, name);
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void ValidateFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private enum StackViewOwnership
        {
            SceneOwned,
            RuntimeOwned,
        }

        private sealed class RuntimeCardInstance
        {
            public RuntimeCardInstance(GameObject root)
            {
                Root = root;
            }

            public GameObject Root { get; private set; }

            public CardView View { get; private set; }

            public TabletopSelectionVisual SelectionVisual { get; private set; }

            public void SetReferences(CardView view, TabletopSelectionVisual selectionVisual)
            {
                View = view;
                SelectionVisual = selectionVisual;
            }

            public void ClearReferences()
            {
                Root = null;
                View = null;
                SelectionVisual = null;
            }
        }

        private sealed class StackRuntimeView
        {
            public StackRuntimeView(
                StackViewOwnership ownership,
                GameObject root,
                StackView view,
                ContainerState container,
                ContainerPlacementState placement,
                TabletopContainerDropTarget dropTarget)
            {
                Ownership = ownership;
                Root = root;
                View = view;
                Container = container;
                Placement = placement;
                DropTarget = dropTarget;
            }

            public StackViewOwnership Ownership { get; }

            public GameObject Root { get; private set; }

            public StackView View { get; private set; }

            public ContainerState Container { get; private set; }

            public ContainerPlacementState Placement { get; private set; }

            public TabletopContainerDropTarget DropTarget { get; set; }

            public void ClearReferences()
            {
                Root = null;
                View = null;
                Container = default(ContainerState);
                Placement = default(ContainerPlacementState);
                DropTarget = null;
            }
        }

        private sealed class ContainerFeedbackTarget
        {
            private readonly Renderer renderer;
            private readonly Material baseMaterial;
            private readonly Material validMaterial;
            private readonly Material sourceMaterial;
            private readonly Material invalidMaterial;
            private readonly PrototypeConsoleSlotVisual authoredSlotVisual;
            private readonly PrototypeFixedContainerVisual authoredFixedContainerVisual;

            public ContainerFeedbackTarget(
                Renderer renderer,
                Material baseMaterial,
                Material validMaterial,
                Material sourceMaterial,
                Material invalidMaterial)
            {
                this.renderer = renderer;
                this.baseMaterial = baseMaterial;
                this.validMaterial = validMaterial;
                this.sourceMaterial = sourceMaterial;
                this.invalidMaterial = invalidMaterial;
                Clear();
            }

            public ContainerFeedbackTarget(PrototypeConsoleSlotVisual slotVisual)
            {
                authoredSlotVisual = slotVisual ?? throw new ArgumentNullException(nameof(slotVisual));
                Clear();
            }

            public ContainerFeedbackTarget(PrototypeFixedContainerVisual fixedContainerVisual)
            {
                authoredFixedContainerVisual = fixedContainerVisual
                    ?? throw new ArgumentNullException(nameof(fixedContainerVisual));
                Clear();
            }

            public void SetValid()
            {
                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ShowValidTarget();
                    return;
                }

                if (authoredFixedContainerVisual != null)
                {
                    authoredFixedContainerVisual.ShowValidTarget();
                    return;
                }

                Set(validMaterial);
            }

            public void SetSource()
            {
                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ShowSourceTarget();
                    return;
                }

                if (authoredFixedContainerVisual != null)
                {
                    authoredFixedContainerVisual.ShowSourceTarget();
                    return;
                }

                Set(sourceMaterial);
            }

            public void SetInvalid()
            {
                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ShowInvalidTarget();
                    return;
                }

                if (authoredFixedContainerVisual != null)
                {
                    authoredFixedContainerVisual.ShowInvalidTarget();
                    return;
                }

                Set(invalidMaterial);
            }

            public void Clear()
            {
                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ClearFeedback();
                    return;
                }

                if (authoredFixedContainerVisual != null)
                {
                    authoredFixedContainerVisual.ClearFeedback();
                    return;
                }

                Set(baseMaterial);
            }

            private void Set(Material material)
            {
                if (renderer != null && material != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }
    }
}
