using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Prototype
{
    public sealed class TabletopPrototypeComposition : MonoBehaviour, IContainedCardDragFeedback
    {
        private const int PrototypeConsoleSlotCount = TrapFloorTemplateFactory.ConsoleSlotCountPerPlayer;
        private const int PrototypeFloorfallRoundNumber = 1;
        private const int ShuffleSeed = 123;
        private const float ContextMenuWidth = 240f;
        private const float FloorfallStatusWidth = 280f;
        private const float FloorfallStatusHeight = 78f;
        private const float TrapFloorCoinVisualScale = 0.34f;
        private const float TrapFloorFloorLabelCharacterSize = 0.16f;
        private const float TrapFloorCardLabelCharacterSize = 0.18f;
        private const float TrapFloorCardBackLabelCharacterSize = 0.14f;
        private const float TrapFloorContainerLabelCharacterSize = 0.1f;
        private const int TrapFloorCardLabelFontSize = 64;
        private const int TrapFloorContainerLabelFontSize = 48;
        private const int ToolboxPhysicalOrderStride = 40;
        private const float SessionEntryWidth = 420f;
        private const float SessionEntryHeight = 270f;
        private static readonly Rect PrototypeControlsPanelRect = new Rect(16f, 330f, 280f, 460f);

        [SerializeField] internal UnityCamera targetCamera;
        [SerializeField] internal TabletopCameraInputAdapter cameraInputAdapter;
        [SerializeField] internal TabletopObjectInputAdapter objectInputAdapter;
        [SerializeField] internal TabletopInputFrameCoordinator inputFrameCoordinator;
        [SerializeField] internal PrototypeCardVisualReferences prototypeCardPrefab;
        [SerializeField] internal PawnView prototypePawnPrefab;
        [SerializeField] internal TokenView prototypeTokenPrefab;
        [SerializeField] internal DieView prototypeDiePrefab;
        [SerializeField] internal PrototypeFixedContainerVisual prototypeDeckPrefab;
        [SerializeField] internal ConsoleView prototypeConsolePrefab;
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
        [SerializeField] internal PrototypeFixedContainerVisual prototypeStackPrefab;
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
        [SerializeField] internal float tabletopLayerHeight = 0.02f;
        [SerializeField] internal float tabletopLocalOrderHeight = 0.0005f;
        [SerializeField] internal float pickupLift = 0.08f;
        [SerializeField] internal float dragLift = 0.16f;
        [SerializeField] internal float pickupResponseDuration = 0.06f;
        [SerializeField] internal float dragFollowSmoothing = 0.045f;
        [SerializeField] internal float settleDuration = 0.12f;
        [SerializeField] internal float returnDuration = 0.1f;
        [SerializeField] internal float handReflowDuration = 0.14f;
        [SerializeField] internal float magneticDistance = 0.8f;
        [SerializeField] internal float feedbackDuration = 0.18f;
        [SerializeField] internal float shuffleCompression = 0.06f;
        [SerializeField] internal float floorCardVisualScale = 0.62f;
        [SerializeField] internal bool showDeveloperControls;

        private readonly List<RuntimeCardInstance> runtimeCardInstances = new List<RuntimeCardInstance>();
        private readonly List<RuntimeObjectInstance> runtimePawnInstances = new List<RuntimeObjectInstance>();
        private readonly List<RuntimeObjectInstance> runtimeTokenInstances = new List<RuntimeObjectInstance>();
        private readonly List<RuntimeObjectInstance> runtimeDieInstances = new List<RuntimeObjectInstance>();
        private readonly List<RuntimeDeckInstance> runtimeDeckInstances = new List<RuntimeDeckInstance>();
        private readonly List<RuntimeConsoleInstance> runtimeConsoleInstances = new List<RuntimeConsoleInstance>();
        private readonly List<GameObject> runtimeOwnedStackRoots = new List<GameObject>();
        private readonly List<CardView> cardViews = new List<CardView>();
        private readonly List<PrototypeCardVisualReferences> cardVisualReferences =
            new List<PrototypeCardVisualReferences>();
        private readonly List<TabletopSelectionVisual> cardSelectionVisuals = new List<TabletopSelectionVisual>();
        private readonly List<TabletopSelectionVisual> pawnSelectionVisuals = new List<TabletopSelectionVisual>();
        private readonly List<TabletopSelectionVisual> tokenSelectionVisuals = new List<TabletopSelectionVisual>();
        private readonly List<TabletopSelectionVisual> dieSelectionVisuals = new List<TabletopSelectionVisual>();
        private readonly List<PawnView> pawnViews = new List<PawnView>();
        private readonly List<TokenView> tokenViews = new List<TokenView>();
        private readonly List<DieView> dieViews = new List<DieView>();
        private readonly List<DeckView> controllerDeckViews = new List<DeckView>();
        private readonly List<ConsoleView> playerConsoleViews = new List<ConsoleView>();
        private readonly List<IContainerLayoutView> layoutViews = new List<IContainerLayoutView>();
        private readonly Dictionary<TabletopObjectId, string> labelsByCardId = new Dictionary<TabletopObjectId, string>();
        private readonly Dictionary<ObjectDefinitionId, ButtonCardDefinition> buttonDefinitions =
            new Dictionary<ObjectDefinitionId, ButtonCardDefinition>();
        private readonly Dictionary<ContainerId, StackRuntimeView> stackViewsByContainerId =
            new Dictionary<ContainerId, StackRuntimeView>();
        private readonly Dictionary<ContainerId, ContainerFeedbackTarget> feedbackTargetsByContainerId =
            new Dictionary<ContainerId, ContainerFeedbackTarget>();
        private readonly Dictionary<ContainerId, PrototypeConsoleSlotVisual> consoleSlotVisualsByContainerId =
            new Dictionary<ContainerId, PrototypeConsoleSlotVisual>();

        private bool cameraRoutingConfiguredByComposition;
        private bool frameCoordinatorEnabledByComposition;
        private bool controlsPanelInputBlockConfiguredByComposition;
        private bool prototypeUiInputConfiguredByComposition;
        private bool componentPlacementInputConfiguredByComposition;
        private bool objectAdapterInitializedByComposition;
        private bool cardViewBoundByComposition;
        private bool pawnViewBoundByComposition;
        private bool tokenViewBoundByComposition;
        private bool sessionEntryVisible;
        private bool cameraInputSuspendedForSessionEntry;

        private MatchState matchState;
        private TabletopSession activeSession;
        private TabletopSessionBootstrapService sessionBootstrapService;
        private GameTemplateCatalog sessionTemplateCatalog;
        private TrapFloorTemplateDefinition availableTrapFloorTemplate;
        private PlayerId sessionEntryActorId;
        private string sessionEntryError;
        private PrototypeTemplateContext prototypeTemplateContext;
        private TrapFloorTemplateDefinition trapFloorTemplate;
        private TrapFloorFloorfallState floorfallState;
        private TrapFloorFloorfallService floorfallService;
        private TrapFloorFloorfallTargetPresenter floorfallTargetPresenter;
        private SystemRandomValueSource authoritativeRandomValueSource;
        private CreateTabletopComponentUseCase componentCreationUseCase;
        private RollDieUseCase rollDieUseCase;
        private TabletopComponentPlacementController componentPlacementController;
        private PlayerLayoutDefinition playerLayout;
        private PlayerSeatLayoutEntry localSeatLayout;
        private PlayAreaId centralPlayAreaId;
        private PlayerId localPlayerId;
        private int localPlayerLayoutSeatIndex = -1;
        private InteractionOwnerId interactionOwnerId;
        private CardInstanceState cardState;
        private PawnState pawnState;
        private TokenState tokenState;
        private PrototypeCardVisualReferences looseCardVisualReferences;
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
        private TabletopPresentationTransitionController presentationTransitions;

        private SeatId localSeatId;
        private ContainerId deckContainerId;
        private ContainerId handContainerId;
        private ContainerId discardContainerId;
        private ContainerId stackAContainerId;
        private ContainerId stackBContainerId;
        private ContainerId primaryStackContainerId;
        private ContainerId sourceFeedbackContainerId;
        private int dynamicStackSequence;
        private string operationMessage = "Tabletop ready.";
        private float operationMessageUntil;
        private float feedbackHoldUntil;
        private PrototypeContextMenuMode contextMenuMode;
        private Rect contextMenuRect;
        private Vector2 contextMenuAnchorGuiPosition;
        private Vector2 contextMenuScrollPosition;
        private CardView contextMenuCardView;
        private ContainerId contextMenuContainerId;
        private int selectedDrawCount = 1;
        private int toolboxSpawnSequence;
        private bool toolboxVisible;
        private bool toolboxDieChoicesVisible;
        private DieView contextMenuDieView;

        private DeckView deckView;
        private HandView handView;
        private DiscardPileView discardPileView;
        private ConsoleView consoleView;
        private readonly List<ConsoleSlotView> consoleSlotViews = new List<ConsoleSlotView>();
        private ConsoleSlotView[] resolvedSceneConsoleSlotViews = Array.Empty<ConsoleSlotView>();
        private PrototypeConsoleSlotVisual[] resolvedSceneConsoleSlotVisuals = Array.Empty<PrototypeConsoleSlotVisual>();

        public bool IsInitialized { get; private set; }

        public bool IsSessionEntryVisible => sessionEntryVisible;

        public TabletopSession ActiveSession => activeSession;

        public MatchState MatchState => matchState;

        public TrapFloorFloorfallState FloorfallState => floorfallState;

        public PlayerLayoutDefinition PlayerLayout => playerLayout;

        public PlayerSeatLayoutEntry LocalSeatLayout => localSeatLayout;

        public PlayAreaState CentralPlayArea => matchState != null && !centralPlayAreaId.IsEmpty
            ? matchState.GetPlayArea(centralPlayAreaId)
            : null;

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

        public Rect ControlsPanelScreenRect => PrototypeControlsPanelRect;

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
            InitializeActiveSession(false);
        }

        private void InitializeActiveSession(bool restoreInitialBaseline)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition is already initialized.");
            }

            if (activeSession == null)
            {
                throw new InvalidOperationException("Session Entry must construct an authoritative session before tabletop initialization.");
            }

            ResumeCameraInputForSession();
            if (activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                InitializeEmptyTableSession(restoreInitialBaseline);
                return;
            }

            InitializeTrapFloorSession(restoreInitialBaseline);
        }

        private void InitializeTrapFloorSession(bool restoreInitialBaseline)
        {
            if (prototypeTemplateContext == null)
            {
                throw new InvalidOperationException("The selected Game Template has no Trap Floor prototype wiring.");
            }

            try
            {
                ValidateTrapFloorConfiguration();
                presentationTransitions = new TabletopPresentationTransitionController();
                ReactivateSceneOwnedObjectViews();
                BuildRuntimeGraph(restoreInitialBaseline);
                BuildToolboxRuntime();
                BuildFloorfallRuntime();
                ProjectTrapFloorCameraBookmark();
                BindObjectViews();
                BuildContainerViews();
                BindContainerViews();
                RefreshCardContentVisibility();
                ConfigureDropTargets();
                BuildInteractionGraph();
                inputFrameCoordinator.ConfigurePrototypeUiInput(
                    HandleSecondaryPointerPressed,
                    CloseContextMenu);
                prototypeUiInputConfiguredByComposition = true;
                inputFrameCoordinator.ConfigureObjectInputBlockingGuiRect(ControlsPanelScreenRect);
                controlsPanelInputBlockConfiguredByComposition = true;

                inputFrameCoordinator.ConfigureSelectionPresenter(selectionPresenter);
                inputFrameCoordinator.enabled = true;
                frameCoordinatorEnabledByComposition = true;

                if (!cameraInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator)
                    || !objectInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator))
                {
                    throw new InvalidOperationException("TabletopInputFrameCoordinator failed to attach both input adapters.");
                }

                selectionPresenter.Refresh();
                ShowMessage("Trap Floor tabletop foundation ready.");
                IsInitialized = true;
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        private void InitializeEmptyTableSession(bool restoreInitialBaseline)
        {
            try
            {
                ValidateCommonConfiguration();
                ValidateInputPreInitializationState();
                presentationTransitions = new TabletopPresentationTransitionController();
                interactionOwnerId = InteractionOwnerId.New();
                coordinateConverter = new TabletopCoordinateConverter(
                    worldUnitsPerTableUnit,
                    tabletopHeight,
                    tabletopLayerHeight,
                    tabletopLocalOrderHeight);
                matchState = restoreInitialBaseline
                    ? activeSession.Reset()
                    : activeSession.CurrentMatch;
                localPlayerId = activeSession.Request.RequestingPlayerId;
                BuildToolboxRuntime();

                BuildInteractionGraph();
                inputFrameCoordinator.ConfigurePrototypeUiInput(
                    HandleSecondaryPointerPressed,
                    CloseContextMenu);
                prototypeUiInputConfiguredByComposition = true;
                inputFrameCoordinator.ConfigureObjectInputBlockingGuiRect(ControlsPanelScreenRect);
                controlsPanelInputBlockConfiguredByComposition = true;
                inputFrameCoordinator.ConfigureSelectionPresenter(selectionPresenter);
                inputFrameCoordinator.enabled = true;
                frameCoordinatorEnabledByComposition = true;

                if (!cameraInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator)
                    || !objectInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator))
                {
                    throw new InvalidOperationException("TabletopInputFrameCoordinator failed to attach both input adapters.");
                }

                selectionPresenter.Refresh();
                ShowMessage("Empty Table ready.");
                IsInitialized = true;
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        public void Shutdown()
        {
            Shutdown(false);
        }

        private void Shutdown(bool preserveTemplateContext)
        {
            ClearFeedback();
            floorfallTargetPresenter?.Clear();
            floorfallState?.Clear();

            if (frameCoordinatorEnabledByComposition && inputFrameCoordinator != null)
            {
                inputFrameCoordinator.enabled = false;
            }

            frameCoordinatorEnabledByComposition = false;

            CloseContextMenu();

            if (componentPlacementInputConfiguredByComposition && inputFrameCoordinator != null)
            {
                inputFrameCoordinator.ClearComponentPlacement();
            }

            componentPlacementInputConfiguredByComposition = false;
            componentPlacementController = null;

            if (prototypeUiInputConfiguredByComposition && inputFrameCoordinator != null)
            {
                inputFrameCoordinator.ClearPrototypeUiInput();
            }

            prototypeUiInputConfiguredByComposition = false;

            if (controlsPanelInputBlockConfiguredByComposition && inputFrameCoordinator != null)
            {
                inputFrameCoordinator.ClearObjectInputBlockingGuiRect();
            }

            controlsPanelInputBlockConfiguredByComposition = false;

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
            interactionRouter?.Reset();
            previewSession?.Reset();
            presentationTransitions?.CompleteAll();
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
            presentationTransitions = null;
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
            ReleaseRuntimeDeckInstances();
            ReleaseRuntimeConsoleInstances();
            ReleaseRuntimeObjectInstances(runtimePawnInstances, pawnViews, pawnSelectionVisuals);
            ReleaseRuntimeObjectInstances(runtimeTokenInstances, tokenViews, tokenSelectionVisuals);
            ReleaseRuntimeObjectInstances(runtimeDieInstances, dieViews, dieSelectionVisuals);
            ReleaseRuntimeCardInstances();

            matchState = null;
            trapFloorTemplate = null;
            floorfallState = null;
            floorfallService = null;
            floorfallTargetPresenter = null;
            authoritativeRandomValueSource = null;
            componentCreationUseCase = null;
            rollDieUseCase = null;
            playerLayout = null;
            localSeatLayout = null;
            centralPlayAreaId = PlayAreaId.Empty;
            localPlayerId = PlayerId.Empty;
            localPlayerLayoutSeatIndex = -1;
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
            cardVisualReferences.Clear();
            cardSelectionVisuals.Clear();
            runtimeCardInstances.Clear();
            runtimePawnInstances.Clear();
            runtimeTokenInstances.Clear();
            runtimeDieInstances.Clear();
            runtimeDeckInstances.Clear();
            runtimeConsoleInstances.Clear();
            runtimeOwnedStackRoots.Clear();
            pawnViews.Clear();
            tokenViews.Clear();
            dieViews.Clear();
            pawnSelectionVisuals.Clear();
            tokenSelectionVisuals.Clear();
            dieSelectionVisuals.Clear();
            controllerDeckViews.Clear();
            playerConsoleViews.Clear();
            consoleSlotViews.Clear();
            resolvedSceneConsoleSlotViews = Array.Empty<ConsoleSlotView>();
            resolvedSceneConsoleSlotVisuals = Array.Empty<PrototypeConsoleSlotVisual>();
            layoutViews.Clear();
            labelsByCardId.Clear();
            buttonDefinitions.Clear();
            stackViewsByContainerId.Clear();
            feedbackTargetsByContainerId.Clear();
            consoleSlotVisualsByContainerId.Clear();
            feedbackHoldUntil = 0f;
            contextMenuMode = PrototypeContextMenuMode.None;
            contextMenuCardView = null;
            contextMenuDieView = null;
            contextMenuContainerId = ContainerId.Empty;
            selectedDrawCount = 1;
            toolboxSpawnSequence = 0;
            toolboxVisible = false;
            toolboxDieChoicesVisible = false;
            if (!preserveTemplateContext)
            {
                prototypeTemplateContext = null;
            }

            IsInitialized = false;
        }

        public ShuffleDeckResult ShuffleDeck()
        {
            EnsureInitialized();
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(deckContainerId);
            ShuffleDeckResult result = new ShuffleDeckUseCase().Execute(
                matchState,
                new ShuffleDeckCommand(CreateCommandContext(), deckContainerId, ShuffleSeed));
            if (result.Succeeded)
            {
                deckView.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    settleDuration);
                presentationTransitions.Pulse(
                    sceneDeckVisual.transform,
                    shuffleCompression,
                    feedbackDuration);
                ShowMessage("Deck shuffled.");
            }
            else
            {
                deckView.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Shuffle rejected: {result.Error}.");
            }

            RefreshCardContentVisibility();
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
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(deckContainerId, handContainerId);
            DrawCardsResult result = new DrawCardsUseCase().Execute(
                matchState,
                new DrawCardsCommand(CreateCommandContext(), deckContainerId, handContainerId, count));
            if (result.Succeeded)
            {
                deckView.ApplyAcceptedLayout();
                handView.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    handReflowDuration,
                    0.035f);
                ShowMessage($"Drew {count} card{(count == 1 ? string.Empty : "s")} to Hand.");
            }
            else
            {
                deckView.ApplyAcceptedLayout();
                handView.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Draw rejected: {result.Error}.");
            }

            RefreshCardContentVisibility();
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

            return SplitStack(source, sourceView);
        }

        private SplitStackResult SplitStack(ContainerState source, StackRuntimeView sourceView)
        {
            EnsureInitialized();
            if (source == null
                || sourceView == null
                || source.Kind != ContainerKind.Stack
                || source.Count < 2
                || !matchState.Containers.TryGetValue(source.Id, out ContainerState authoritativeSource)
                || !ReferenceEquals(authoritativeSource, source)
                || !stackViewsByContainerId.TryGetValue(source.Id, out StackRuntimeView authoritativeView)
                || !ReferenceEquals(authoritativeView, sourceView))
            {
                ShowMessage("Split unavailable.");
                return SplitStackResult.Failure(CommandResultStatus.Rejected, SplitStackError.SourceStackTooSmall);
            }

            int firstMovedIndex = Math.Max(1, source.Count / 2);
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(source.Id);
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
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    settleDuration,
                    0.035f);
                presentationTransitions.Appear(newStackView.Root.transform, settleDuration);
                RebuildLayoutLookupAndRouter();
                ShowMessage("Stack split.");
            }
            else
            {
                sourceView.View.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Split rejected: {result.Error}.");
            }

            RefreshCardContentVisibility();
            return result;
        }

        public void ResetPrototype()
        {
            EnsureInitialized();
            Shutdown(true);
            InitializeActiveSession(true);
        }

        public TrapFloorFloorfallTarget TriggerFloorfall()
        {
            EnsureInitialized();
            if (floorfallService == null || floorfallTargetPresenter == null)
            {
                throw new InvalidOperationException("Floorfall is available only in the active Trap Floor session.");
            }

            TrapFloorFloorfallTarget target = floorfallService.RollAndResolve(
                new TrapFloorFloorfallContext(PrototypeFloorfallRoundNumber));
            AnimateAcceptedDieResult(trapFloorTemplate.FloorfallXAxisDieId);
            AnimateAcceptedDieResult(trapFloorTemplate.FloorfallYAxisDieId);
            floorfallTargetPresenter.Show(target.FloorCardId);
            ShowMessage(
                $"Floorfall: X {target.XAxisRoll.Value}, Y {target.YAxisRoll.Value} -> {target.Coordinate}.");
            return target;
        }

        private void AnimateAcceptedDieResult(TabletopObjectId dieId)
        {
            for (int i = 0; i < dieViews.Count; i++)
            {
                DieView view = dieViews[i];
                if (view == null || !view.IsBound || view.ObjectId != dieId)
                {
                    continue;
                }

                view.ApplyAcceptedState();
                TabletopTransformSnapshot destination = presentationTransitions.Capture(view.transform);
                presentationTransitions.AnimateFromCurrentResult(
                    view.transform,
                    new TabletopTransformSnapshot(
                        destination.Position + (Vector3.up * 0.18f),
                        destination.Rotation * Quaternion.Euler(30f, 210f, 20f),
                        destination.LocalScale),
                    settleDuration,
                    0.12f);
                return;
            }
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
            ClearFeedback();
            if (target.Kind == CardDropTargetKind.Container
                && feedbackTargetsByContainerId.TryGetValue(target.ContainerId, out ContainerFeedbackTarget feedbackTarget))
            {
                feedbackTarget.SetInvalid();
                feedbackHoldUntil = Time.unscaledTime + feedbackDuration;
            }

            ShowMessage("Transfer rejected.");
        }

        void IContainedCardDragFeedback.Clear()
        {
            ClearFeedback();
        }

        private void Start()
        {
            if (IsInitialized || sessionEntryVisible)
            {
                return;
            }

            try
            {
                PrepareSessionEntry();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"TabletopPrototypeComposition failed to prepare Session Entry: {exception.Message}",
                    this);
                sessionEntryError = exception.Message;
                sessionEntryVisible = true;
                HideSessionPresentation();
                SuspendCameraInputForSessionEntry();
            }
        }

        private void OnDestroy()
        {
            Shutdown();
            activeSession = null;
            sessionTemplateCatalog = null;
            availableTrapFloorTemplate = null;
        }

        private void Update()
        {
            presentationTransitions?.Tick(Time.unscaledDeltaTime);
            RefreshCardContentVisibility();
            if (feedbackHoldUntil > 0f && Time.unscaledTime >= feedbackHoldUntil)
            {
                ClearFeedback();
            }
        }

        private void OnGUI()
        {
            if (sessionEntryVisible)
            {
                DrawSessionEntry();
                return;
            }

            if (!IsInitialized)
            {
                return;
            }

            DrawFloorfallStatus();
            DrawPlayerContextMenu();
            DrawActiveSessionPanel();
        }

        private void DrawActiveSessionPanel()
        {
            GUILayout.BeginArea(ControlsPanelScreenRect, GUI.skin.box);
            GUILayout.Label(activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom
                ? "EMPTY / CUSTOM TABLE"
                : activeSession.Template.DisplayName.ToUpperInvariant());
            if (GUILayout.Button("Return to Session Entry"))
            {
                ReturnToSessionEntry();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Reset Current Session"))
            {
                ResetPrototype();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(toolboxVisible ? "Close Component Toolbox" : "Add Component"))
            {
                toolboxVisible = !toolboxVisible;
                toolboxDieChoicesVisible = false;
            }

            if (toolboxVisible)
            {
                DrawComponentToolbox();
            }

            GUILayout.Label(CurrentStatusText());

            if (!showDeveloperControls
                || activeSession.Selection.Kind != TabletopSessionKind.GameTemplate)
            {
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(4f);
            GUILayout.Label("Developer Controls");
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

            GUILayout.Space(6f);
            GUILayout.EndArea();
        }

        private void DrawComponentToolbox()
        {
            GUILayout.Space(4f);
            GUILayout.Label("COMPONENT TOOLBOX");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Card"))
            {
                BeginToolboxPlacement(TabletopComponentKind.Card);
            }

            if (GUILayout.Button("Deck"))
            {
                BeginToolboxPlacement(TabletopComponentKind.Deck);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stack"))
            {
                BeginToolboxPlacement(TabletopComponentKind.Stack);
            }

            if (GUILayout.Button("Pawn"))
            {
                BeginToolboxPlacement(TabletopComponentKind.Pawn);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Token"))
            {
                BeginToolboxPlacement(TabletopComponentKind.Token);
            }

            if (GUILayout.Button("Die"))
            {
                toolboxDieChoicesVisible = !toolboxDieChoicesVisible;
            }
            GUILayout.EndHorizontal();

            if (!toolboxDieChoicesVisible)
            {
                return;
            }

            GUILayout.Label("Choose Die");
            GUILayout.BeginHorizontal();
            DrawDieToolboxButton(4);
            DrawDieToolboxButton(6);
            DrawDieToolboxButton(8);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawDieToolboxButton(10);
            DrawDieToolboxButton(12);
            DrawDieToolboxButton(20);
            GUILayout.EndHorizontal();
        }

        private void DrawDieToolboxButton(int sideCount)
        {
            if (GUILayout.Button($"d{sideCount}"))
            {
                BeginToolboxPlacement(TabletopComponentKind.Die, sideCount);
            }
        }

        private void BeginToolboxPlacement(
            TabletopComponentKind componentKind,
            int dieSideCount = 0)
        {
            EnsureInitialized();
            if (componentPlacementController == null)
            {
                throw new InvalidOperationException("Component placement is not configured.");
            }

            CloseContextMenu();
            GameObject previewRoot = CreateToolboxPlacementPreview(componentKind, dieSideCount);
            float rotation = localSeatLayout != null
                ? localSeatLayout.PlayerZonePose.RotationDegrees
                : 0f;
            componentPlacementController.Begin(
                componentKind,
                dieSideCount,
                previewRoot,
                rotation,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride);
            toolboxVisible = false;
            toolboxDieChoicesVisible = false;
            ShowMessage(
                $"Place {(componentKind == TabletopComponentKind.Die ? $"d{dieSideCount}" : componentKind.ToString())}: left-click to confirm, right-click or Escape to cancel.");
        }

        public CreateTabletopComponentResult AddToolboxComponent(
            TabletopComponentKind componentKind,
            int dieSideCount = 0)
        {
            EnsureInitialized();
            return AddToolboxComponentAtPose(
                componentKind,
                dieSideCount,
                CreateNextToolboxSpawnPose());
        }

        private CreateTabletopComponentResult AddToolboxComponentAtPose(
            TabletopComponentKind componentKind,
            int dieSideCount,
            TabletopPose requestedPose)
        {
            CreateTabletopComponentResult result = componentCreationUseCase.Execute(
                matchState,
                activeSession.Request.ActivePlayerIds,
                new CreateTabletopComponentRequest(
                    CreateCommandContext(),
                    componentKind,
                    requestedPose,
                    dieSideCount));
            if (!result.Succeeded)
            {
                ShowMessage($"Add {componentKind} rejected: {result.Error}.");
                return result;
            }

            toolboxSpawnSequence++;
            ProjectCreatedToolboxComponent(result);
            ShowMessage(componentKind == TabletopComponentKind.Die
                ? $"Added d{dieSideCount}."
                : $"Added {componentKind}.");
            return result;
        }

        private bool CommitToolboxPlacement(
            TabletopComponentKind componentKind,
            int dieSideCount,
            TabletopPose requestedPose)
        {
            return AddToolboxComponentAtPose(componentKind, dieSideCount, requestedPose).Succeeded;
        }

        private TabletopPose CreateNextToolboxSpawnPose()
        {
            double baseX = -2.4d;
            double baseY = -2.4d;
            double columnX = 0.62d;
            double columnY = 0d;
            double rowX = 0d;
            double rowY = 0.72d;
            float rotation = 0f;
            if (localSeatLayout != null)
            {
                TabletopPose playerZonePose = localSeatLayout.PlayerZonePose;
                double x = playerZonePose.Position.X;
                double y = playerZonePose.Position.Y;
                double magnitude = Math.Sqrt((x * x) + (y * y));
                if (magnitude > 0.0001d)
                {
                    double radialX = x / magnitude;
                    double radialY = y / magnitude;
                    baseX = x - (radialX * 0.25d);
                    baseY = y - (radialY * 0.25d);
                    columnX = -radialY * 0.62d;
                    columnY = radialX * 0.62d;
                    rowX = radialX * 0.72d;
                    rowY = radialY * 0.72d;
                }
                else
                {
                    baseX = x;
                    baseY = y;
                }

                rotation = playerZonePose.RotationDegrees;
            }

            int column = toolboxSpawnSequence % 4;
            int row = (toolboxSpawnSequence / 4) % 3;
            return new TabletopPose(
                new TableCoordinate(
                    baseX + (column * columnX) + (row * rowX),
                    baseY + (column * columnY) + (row * rowY)),
                rotation,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride);
        }

        private GameObject CreateToolboxPlacementPreview(
            TabletopComponentKind componentKind,
            int dieSideCount)
        {
            GameObject previewRoot;
            switch (componentKind)
            {
                case TabletopComponentKind.Card:
                {
                    PrototypeCardVisualReferences preview = Instantiate(prototypeCardPrefab);
                    preview.ValidateReferences();
                    ConfigurePrototypeLabel(
                        preview.FrontLabel,
                        "CARD",
                        TrapFloorCardLabelCharacterSize,
                        TrapFloorCardLabelFontSize);
                    ConfigurePrototypeLabel(
                        preview.BackLabel,
                        preview.BackLabel.text,
                        TrapFloorCardBackLabelCharacterSize,
                        TrapFloorCardLabelFontSize);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Deck:
                {
                    PrototypeFixedContainerVisual preview = Instantiate(prototypeDeckPrefab);
                    preview.ValidateReferences();
                    ConfigureContainerLabel(preview.Label, "DECK");
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Stack:
                {
                    PrototypeFixedContainerVisual preview = Instantiate(prototypeStackPrefab);
                    preview.ValidateReferences();
                    ConfigureContainerLabel(preview.Label, "STACK");
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Pawn:
                    previewRoot = Instantiate(prototypePawnPrefab).gameObject;
                    break;
                case TabletopComponentKind.Token:
                    previewRoot = Instantiate(prototypeTokenPrefab).gameObject;
                    break;
                case TabletopComponentKind.Die:
                {
                    if (!ToolboxComponentDefinitions.IsSupportedDieSideCount(dieSideCount))
                    {
                        throw new ArgumentOutOfRangeException(nameof(dieSideCount));
                    }

                    DieView preview = Instantiate(prototypeDiePrefab);
                    ConfigurePrototypeLabel(preview.ResultLabel, $"d{dieSideCount}\n1", 0.18f, 64);
                    previewRoot = preview.gameObject;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(componentKind));
            }

            PrepareRuntimeRoot(previewRoot, $"{componentKind} Placement Preview");
            DisablePlacementPreviewInteraction(previewRoot);
            TintPlacementPreview(previewRoot);
            return previewRoot;
        }

        private static void DisablePlacementPreviewInteraction(GameObject previewRoot)
        {
            Collider[] colliders = previewRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            TabletopContainerDropTarget[] dropTargets =
                previewRoot.GetComponentsInChildren<TabletopContainerDropTarget>(true);
            for (int i = 0; i < dropTargets.Length; i++)
            {
                dropTargets[i].enabled = false;
            }

            TabletopObjectView[] objectViews = previewRoot.GetComponentsInChildren<TabletopObjectView>(true);
            for (int i = 0; i < objectViews.Length; i++)
            {
                objectViews[i].enabled = false;
            }

            TabletopSelectionVisual[] selectionVisuals =
                previewRoot.GetComponentsInChildren<TabletopSelectionVisual>(true);
            for (int i = 0; i < selectionVisuals.Length; i++)
            {
                if (selectionVisuals[i].IsConfigured)
                {
                    selectionVisuals[i].SetSelected(false);
                }

                selectionVisuals[i].enabled = false;
            }
        }

        private static void TintPlacementPreview(GameObject previewRoot)
        {
            Renderer[] renderers = previewRoot.GetComponentsInChildren<Renderer>(true);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            Color previewColor = new Color(0.42f, 0.82f, 1f, 0.72f);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer.GetComponent<TextMesh>() != null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", previewColor);
                properties.SetColor("_Color", previewColor);
                renderer.SetPropertyBlock(properties);
                properties.Clear();
            }
        }

        private void ProjectCreatedToolboxComponent(CreateTabletopComponentResult result)
        {
            Transform appearedTransform;
            bool layoutCollectionChanged = false;
            switch (result.ComponentKind)
            {
                case TabletopComponentKind.Card:
                {
                    CardInstanceState card = matchState.Cards[result.ObjectId];
                    CardView view = CreateCardView(card, "CARD", out TabletopSelectionVisual selectionVisual);
                    cardViews.Add(view);
                    cardSelectionVisuals.Add(selectionVisual);
                    RefreshContainerCardViewSources();
                    appearedTransform = view.transform;
                    break;
                }
                case TabletopComponentKind.Deck:
                {
                    RuntimeDeckInstance instance = CreateRuntimeDeckInstance(
                        "Toolbox Deck",
                        "DECK",
                        result.ContainerId);
                    runtimeDeckInstances.Add(instance);
                    controllerDeckViews.Add(instance.View);
                    instance.View.Bind(
                        matchState.GetContainer(result.ContainerId),
                        matchState.ContainerPlacements[result.ContainerId],
                        coordinateConverter,
                        cardViews);
                    ConfigureFixedContainer(instance.Visual, instance.View);
                    appearedTransform = instance.Root.transform;
                    layoutCollectionChanged = true;
                    break;
                }
                case TabletopComponentKind.Stack:
                {
                    ContainerState container = matchState.GetContainer(result.ContainerId);
                    ContainerPlacementState placement = matchState.ContainerPlacements[result.ContainerId];
                    StackRuntimeView stack = CreateStackRuntimeView("STACK", container, placement);
                    stackViewsByContainerId.Add(result.ContainerId, stack);
                    appearedTransform = stack.Root.transform;
                    layoutCollectionChanged = true;
                    break;
                }
                case TabletopComponentKind.Pawn:
                {
                    PawnView view = CreatePawnView(
                        matchState.Pawns[result.ObjectId],
                        out TabletopSelectionVisual selectionVisual);
                    pawnViews.Add(view);
                    pawnSelectionVisuals.Add(selectionVisual);
                    appearedTransform = view.transform;
                    break;
                }
                case TabletopComponentKind.Token:
                {
                    TokenView view = CreateTokenView(
                        matchState.Tokens[result.ObjectId],
                        out TabletopSelectionVisual selectionVisual,
                        1f);
                    tokenViews.Add(view);
                    tokenSelectionVisuals.Add(selectionVisual);
                    appearedTransform = view.transform;
                    break;
                }
                case TabletopComponentKind.Die:
                {
                    DieState die = matchState.Dice[result.ObjectId];
                    DieView view = CreateDieView(
                        die,
                        $"d{die.SideCount}",
                        out TabletopSelectionVisual selectionVisual);
                    dieViews.Add(view);
                    dieSelectionVisuals.Add(selectionVisual);
                    appearedTransform = view.transform;
                    break;
                }
                default:
                    throw new InvalidOperationException("Accepted toolbox component kind is unsupported by Presentation.");
            }

            if (layoutCollectionChanged)
            {
                RebuildLayoutLookupAndRouter();
            }

            RefreshSelectionPresenterAfterRuntimeProjection();
            Physics.SyncTransforms();
            presentationTransitions.Appear(appearedTransform, settleDuration);
            Physics.SyncTransforms();
        }

        private void RefreshContainerCardViewSources()
        {
            for (int i = 0; i < layoutViews.Count; i++)
            {
                IContainerLayoutView layoutView = layoutViews[i];
                if (layoutView != null && layoutView.IsBound)
                {
                    layoutView.SetCardViews(cardViews);
                }
            }
        }

        private void RefreshSelectionPresenterAfterRuntimeProjection()
        {
            inputFrameCoordinator.ClearSelectionPresenter();
            selectionPresenter = new TabletopSelectionPresenter(
                selectionState,
                cardSelectionVisuals,
                pawnSelectionVisuals,
                tokenSelectionVisuals,
                dieSelectionVisuals);
            inputFrameCoordinator.ConfigureSelectionPresenter(selectionPresenter);
            selectionPresenter.Refresh();
        }

        private void DrawSessionEntry()
        {
            Rect entryRect = new Rect(
                Mathf.Max(0f, (Screen.width - SessionEntryWidth) * 0.5f),
                Mathf.Max(0f, (Screen.height - SessionEntryHeight) * 0.5f),
                SessionEntryWidth,
                SessionEntryHeight);
            GUILayout.BeginArea(entryRect, GUI.skin.window);
            GUILayout.Label("CONSOLE CARDS");
            GUILayout.Space(8f);
            GUILayout.Label("Choose a tabletop session");
            GUILayout.Space(8f);

            if (GUILayout.Button("Empty Table", GUILayout.Height(42f)))
            {
                SelectEmptyTableSession();
                GUIUtility.ExitGUI();
            }

            if (sessionTemplateCatalog != null)
            {
                foreach (GameTemplateRegistration registration in sessionTemplateCatalog.Registrations.Values)
                {
                    if (GUILayout.Button(registration.Template.DisplayName, GUILayout.Height(42f)))
                    {
                        SelectGameTemplateSession(registration.Template.Id);
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(sessionEntryError))
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Session could not start: {sessionEntryError}");
            }

            GUILayout.EndArea();
        }

        public void SelectEmptyTableSession()
        {
            TryEnterSession(TabletopSessionSelection.EmptyCustom);
        }

        public void SelectGameTemplateSession(GameTemplateId gameTemplateId)
        {
            TryEnterSession(TabletopSessionSelection.FromGameTemplate(gameTemplateId));
        }

        public void ReturnToSessionEntry()
        {
            if (sessionEntryVisible)
            {
                return;
            }

            Shutdown();
            activeSession = null;
            prototypeTemplateContext = null;
            trapFloorTemplate = null;
            sessionEntryError = null;
            HideSessionPresentation();
            SuspendCameraInputForSessionEntry();
            sessionEntryVisible = true;
        }

        private void PrepareSessionEntry()
        {
            sessionBootstrapService = new TabletopSessionBootstrapService();
            sessionEntryActorId = PlayerId.New();
            sessionEntryError = null;
            try
            {
                availableTrapFloorTemplate = TrapFloorTemplateFactory.CreateStandardFourPlayer();
                sessionTemplateCatalog = new GameTemplateCatalog(
                    new[]
                    {
                        new GameTemplateRegistration(
                            availableTrapFloorTemplate.Template,
                            availableTrapFloorTemplate.ContentCatalog),
                    });
            }
            catch (Exception exception)
            {
                availableTrapFloorTemplate = null;
                sessionTemplateCatalog = new GameTemplateCatalog(Array.Empty<GameTemplateRegistration>());
                sessionEntryError = $"Trap Floor is unavailable: {exception.Message}";
            }

            activeSession = null;
            prototypeTemplateContext = null;
            trapFloorTemplate = null;
            HideSessionPresentation();
            SuspendCameraInputForSessionEntry();
            sessionEntryVisible = true;
        }

        private void TryEnterSession(TabletopSessionSelection selection)
        {
            if (!sessionEntryVisible)
            {
                return;
            }

            if (sessionBootstrapService == null || sessionTemplateCatalog == null)
            {
                sessionEntryError = "Session Entry is not configured.";
                return;
            }

            List<PlayerId> activePlayerIds = CreatePrototypeActivePlayers(selection);
            TabletopSessionBootstrapRequest request = new TabletopSessionBootstrapRequest(
                sessionEntryActorId,
                selection,
                activePlayerIds,
                MatchId.New());
            TabletopSessionBootstrapResult result = sessionBootstrapService.TryCreate(
                request,
                sessionTemplateCatalog);
            if (!result.Succeeded)
            {
                sessionEntryError = FormatSessionBuildFailure(result.Issues);
                return;
            }

            try
            {
                activeSession = result.Session;
                if (selection.Kind == TabletopSessionKind.GameTemplate)
                {
                    if (availableTrapFloorTemplate == null
                        || selection.GameTemplateId != availableTrapFloorTemplate.Template.Id)
                    {
                        throw new InvalidOperationException(
                            "The selected Game Template has no registered prototype Presentation wiring.");
                    }

                    trapFloorTemplate = availableTrapFloorTemplate;
                    prototypeTemplateContext = CreateTrapFloorPrototypeContext(
                        activeSession,
                        trapFloorTemplate,
                        sessionEntryActorId);
                }
                else
                {
                    trapFloorTemplate = null;
                    prototypeTemplateContext = null;
                }

                sessionEntryError = null;
                sessionEntryVisible = false;
                InitializeActiveSession(false);
            }
            catch (Exception exception)
            {
                Shutdown();
                activeSession = null;
                prototypeTemplateContext = null;
                trapFloorTemplate = null;
                HideSessionPresentation();
                SuspendCameraInputForSessionEntry();
                sessionEntryError = exception.Message;
                sessionEntryVisible = true;
                Debug.LogError($"Session construction failed: {exception.Message}", this);
            }
        }

        private List<PlayerId> CreatePrototypeActivePlayers(TabletopSessionSelection selection)
        {
            int playerCount = 1;
            if (selection.Kind == TabletopSessionKind.GameTemplate)
            {
                if (!sessionTemplateCatalog.TryGet(selection.GameTemplateId, out GameTemplateRegistration registration))
                {
                    return new List<PlayerId> { sessionEntryActorId };
                }

                playerCount = registration.Template.RequiredPlayerCount;
            }

            List<PlayerId> players = new List<PlayerId>(playerCount)
            {
                sessionEntryActorId,
            };
            for (int i = 1; i < playerCount; i++)
            {
                players.Add(PlayerId.New());
            }

            return players;
        }

        private static string FormatSessionBuildFailure(
            IReadOnlyList<TabletopSessionBootstrapIssue> issues)
        {
            string message = "Authoritative session construction failed.";
            for (int i = 0; i < issues.Count; i++)
            {
                message += $" {issues[i]}";
            }

            return message;
        }

        private void HandleSecondaryPointerPressed(Vector2 screenPosition)
        {
            if (!IsInitialized || (interactionRouter != null && interactionRouter.HasActiveInteraction))
            {
                return;
            }

            CloseContextMenu();
            if (hitResolver.TryResolve(screenPosition, out TabletopObjectView resolvedObjectView)
                && resolvedObjectView is DieView hitDie
                && hitDie.IsBound
                && hitDie.DieState != null)
            {
                selectionState.Select(hitDie);
                selectionPresenter.Refresh();
                contextMenuDieView = hitDie;
                OpenContextMenu(
                    PrototypeContextMenuMode.Die,
                    screenPosition,
                    null,
                    ContainerId.Empty);
                return;
            }

            if (hitResolver.TryResolve(screenPosition, out TabletopObjectView objectView)
                && objectView is CardView hitCard
                && TryOpenCardContextMenu(hitCard, screenPosition))
            {
                return;
            }

            if (!dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container
                || !matchState.Containers.TryGetValue(target.ContainerId, out ContainerState container))
            {
                return;
            }

            if (container.Kind == ContainerKind.Deck && container.Id == deckContainerId)
            {
                OpenContextMenu(PrototypeContextMenuMode.Deck, screenPosition, null, container.Id);
            }
            else if (container.Kind == ContainerKind.Stack
                && stackViewsByContainerId.ContainsKey(container.Id))
            {
                OpenContextMenu(PrototypeContextMenuMode.Stack, screenPosition, null, container.Id);
            }
        }

        private bool TryOpenCardContextMenu(CardView hitCard, Vector2 screenPosition)
        {
            if (hitCard == null || !hitCard.IsBound || hitCard.CardState == null)
            {
                return false;
            }

            if (trapFloorTemplate != null && trapFloorTemplate.IsFloorCard(hitCard.ObjectId))
            {
                OpenContextMenu(
                    PrototypeContextMenuMode.FloorCard,
                    screenPosition,
                    hitCard,
                    ContainerId.Empty);
                return true;
            }

            ContainerId containerId = hitCard.CardState.BaseState.ContainerId;
            if (containerId.IsEmpty)
            {
                selectionState.Select(hitCard);
                selectionPresenter.Refresh();
                OpenContextMenu(PrototypeContextMenuMode.TabletopCard, screenPosition, hitCard, ContainerId.Empty);
                return true;
            }

            if (!matchState.Containers.TryGetValue(containerId, out ContainerState container))
            {
                return false;
            }

            if (container.Kind == ContainerKind.Deck && container.Id == deckContainerId)
            {
                OpenContextMenu(PrototypeContextMenuMode.Deck, screenPosition, null, containerId);
                return true;
            }

            if (container.Kind == ContainerKind.Stack
                && stackViewsByContainerId.ContainsKey(containerId))
            {
                selectionState.Select(hitCard);
                selectionPresenter.Refresh();
                OpenContextMenu(PrototypeContextMenuMode.StackCard, screenPosition, hitCard, containerId);
                return true;
            }

            return false;
        }

        private void OpenContextMenu(
            PrototypeContextMenuMode mode,
            Vector2 screenPosition,
            CardView card,
            ContainerId containerId)
        {
            contextMenuAnchorGuiPosition = new Vector2(
                screenPosition.x,
                Screen.height - screenPosition.y);
            contextMenuCardView = card;
            contextMenuContainerId = containerId;
            contextMenuScrollPosition = Vector2.zero;
            if (mode == PrototypeContextMenuMode.Deck)
            {
                selectedDrawCount = Mathf.Clamp(selectedDrawCount, 1, Math.Max(1, AvailableDrawableCount()));
            }

            SetContextMenuMode(mode);
        }

        private void SetContextMenuMode(PrototypeContextMenuMode mode)
        {
            contextMenuMode = mode;
            if (mode == PrototypeContextMenuMode.None)
            {
                inputFrameCoordinator?.ClearTransientObjectInputBlockingGuiRect();
                return;
            }

            float height = ContextMenuHeight(mode);
            float x = Mathf.Clamp(
                contextMenuAnchorGuiPosition.x + 8f,
                0f,
                Mathf.Max(0f, Screen.width - ContextMenuWidth));
            float y = Mathf.Clamp(
                contextMenuAnchorGuiPosition.y + 8f,
                0f,
                Mathf.Max(0f, Screen.height - height));
            contextMenuRect = new Rect(x, y, ContextMenuWidth, height);
            inputFrameCoordinator?.SetTransientObjectInputBlockingGuiRect(contextMenuRect);
        }

        private void CloseContextMenu()
        {
            contextMenuMode = PrototypeContextMenuMode.None;
            contextMenuCardView = null;
            contextMenuDieView = null;
            contextMenuContainerId = ContainerId.Empty;
            contextMenuScrollPosition = Vector2.zero;
            inputFrameCoordinator?.ClearTransientObjectInputBlockingGuiRect();
        }

        private void DrawPlayerContextMenu()
        {
            PrototypeContextMenuMode mode = contextMenuMode;
            if (mode == PrototypeContextMenuMode.None)
            {
                return;
            }

            GUILayout.BeginArea(contextMenuRect, GUI.skin.box);
            switch (mode)
            {
                case PrototypeContextMenuMode.Deck:
                    DrawDeckContextMenu();
                    break;
                case PrototypeContextMenuMode.DrawCards:
                    DrawCardCountSelector();
                    break;
                case PrototypeContextMenuMode.TabletopCard:
                    DrawTabletopCardContextMenu();
                    break;
                case PrototypeContextMenuMode.FloorCard:
                    DrawFloorCardContextMenu();
                    break;
                case PrototypeContextMenuMode.StackCard:
                    DrawStackCardContextMenu();
                    break;
                case PrototypeContextMenuMode.Stack:
                    DrawStackContextMenu();
                    break;
                case PrototypeContextMenuMode.MergeDestination:
                    DrawMergeDestinationMenu();
                    break;
                case PrototypeContextMenuMode.Die:
                    DrawDieContextMenu();
                    break;
                default:
                    CloseContextMenu();
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawDeckContextMenu()
        {
            GUILayout.Label("DECK");
            int availableCount = AvailableDrawableCount();
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && availableCount > 0;
            if (GUILayout.Button("Draw 1"))
            {
                DrawCardsResult result = DrawCards(1);
                if (result.Succeeded)
                {
                    CloseContextMenu();
                }
            }

            if (GUILayout.Button("Draw Cards..."))
            {
                selectedDrawCount = Mathf.Clamp(selectedDrawCount, 1, availableCount);
                SetContextMenuMode(PrototypeContextMenuMode.DrawCards);
            }

            GUI.enabled = previousEnabled;
            if (GUILayout.Button("Shuffle"))
            {
                ShuffleDeckResult result = ShuffleDeck();
                if (result.Succeeded)
                {
                    CloseContextMenu();
                }
            }
        }

        private void DrawCardCountSelector()
        {
            GUILayout.Label("DRAW CARDS");
            int availableCount = AvailableDrawableCount();
            if (availableCount <= 0)
            {
                GUILayout.Label("Deck is empty.");
                if (GUILayout.Button("Cancel"))
                {
                    CloseContextMenu();
                }

                return;
            }

            selectedDrawCount = Mathf.Clamp(selectedDrawCount, 1, availableCount);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(44f)))
            {
                selectedDrawCount = Mathf.Max(1, selectedDrawCount - 1);
            }

            GUILayout.Label(selectedDrawCount.ToString(), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("+", GUILayout.Width(44f)))
            {
                selectedDrawCount = Mathf.Min(availableCount, selectedDrawCount + 1);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"Available: {availableCount}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Draw"))
            {
                int count = Mathf.Clamp(selectedDrawCount, 1, availableCount);
                DrawCardsResult result = DrawCards(count);
                if (result.Succeeded)
                {
                    CloseContextMenu();
                }
            }

            if (GUILayout.Button("Cancel"))
            {
                CloseContextMenu();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawTabletopCardContextMenu()
        {
            GUILayout.Label("CARD");
            if (!IsCurrentContextCardInContainer(ContainerId.Empty))
            {
                GUILayout.Label("Card unavailable.");
                return;
            }

            if (GUILayout.Button("Flip"))
            {
                FlipInteractionResult result = flipCoordinator.Flip(contextMenuCardView);
                ShowMessage(result.Succeeded ? "Card flipped." : $"Flip rejected: {result.Status}.");
                if (result.Succeeded)
                {
                    CloseContextMenu();
                }
            }
        }

        private void DrawFloorCardContextMenu()
        {
            if (contextMenuCardView == null
                || !contextMenuCardView.IsBound
                || !trapFloorTemplate.TryGetFloorCoordinate(
                    contextMenuCardView.ObjectId,
                    out TrapFloorCoordinate coordinate))
            {
                GUILayout.Label("FLOOR CARD");
                GUILayout.Label("Floor Card unavailable.");
                return;
            }

            GUILayout.Label($"FLOOR {coordinate}");
            if (GUILayout.Button("Roll Floorfall"))
            {
                TriggerFloorfall();
                CloseContextMenu();
            }
        }

        private void DrawStackCardContextMenu()
        {
            GUILayout.Label("CARD");
            if (!IsCurrentContextCardInContainer(contextMenuContainerId)
                || !matchState.Containers.TryGetValue(contextMenuContainerId, out ContainerState container)
                || container.Kind != ContainerKind.Stack)
            {
                GUILayout.Label("Card unavailable.");
                return;
            }

            int index = container.IndexOf(contextMenuCardView.ObjectId);
            if (index < container.Count - 1 && GUILayout.Button("Move Up"))
            {
                ReorderContainerResult result = MoveCardInContainer(contextMenuCardView, container.Id, 1);
                if (result.Succeeded)
                {
                    CloseContextMenu();
                }
            }

            if (index > 0 && GUILayout.Button("Move Down"))
            {
                ReorderContainerResult result = MoveCardInContainer(contextMenuCardView, container.Id, -1);
                if (result.Succeeded)
                {
                    CloseContextMenu();
                }
            }
        }

        private void DrawStackContextMenu()
        {
            GUILayout.Label("STACK");
            if (!TryGetContextStack(out ContainerState stack, out StackRuntimeView stackView))
            {
                GUILayout.Label("Stack unavailable.");
                return;
            }

            if (stack.Count >= 2 && GUILayout.Button("Split Stack"))
            {
                SplitStackResult result = SplitStack(stack, stackView);
                if (result.Succeeded)
                {
                    CloseContextMenu();
                }
            }

            if (HasValidMergeDestination(stack.Id) && GUILayout.Button("Merge Into..."))
            {
                SetContextMenuMode(PrototypeContextMenuMode.MergeDestination);
            }
        }

        private void DrawDieContextMenu()
        {
            GUILayout.Label("DIE");
            if (contextMenuDieView == null
                || !contextMenuDieView.IsBound
                || contextMenuDieView.DieState == null
                || !matchState.Dice.ContainsKey(contextMenuDieView.ObjectId))
            {
                GUILayout.Label("Die unavailable.");
                return;
            }

            GUILayout.Label($"d{contextMenuDieView.DieState.SideCount}: {contextMenuDieView.DieState.CurrentValue}");
            if (!GUILayout.Button("Roll"))
            {
                return;
            }

            RollDieResult result = rollDieUseCase.Execute(
                matchState,
                activeSession.Request.ActivePlayerIds,
                new RollDieRequest(CreateCommandContext(), contextMenuDieView.ObjectId));
            if (!result.Succeeded)
            {
                ShowMessage($"Die Roll rejected: {result.Error}.");
                return;
            }

            contextMenuDieView.ApplyAcceptedState();
            TabletopTransformSnapshot destination = presentationTransitions.Capture(contextMenuDieView.transform);
            TabletopTransformSnapshot tumbleStart = new TabletopTransformSnapshot(
                destination.Position + (Vector3.up * 0.18f),
                destination.Rotation * Quaternion.Euler(30f, 210f, 20f),
                destination.LocalScale);
            presentationTransitions.AnimateFromCurrentResult(
                contextMenuDieView.transform,
                tumbleStart,
                settleDuration,
                0.12f);
            ShowMessage($"Rolled d{result.Roll.SideCount}: {result.Roll.Value}.");
            CloseContextMenu();
        }

        private void DrawMergeDestinationMenu()
        {
            GUILayout.Label("MERGE INTO");
            if (!TryGetContextStack(out ContainerState source, out _))
            {
                GUILayout.Label("Stack unavailable.");
                return;
            }

            contextMenuScrollPosition = GUILayout.BeginScrollView(contextMenuScrollPosition);
            foreach (KeyValuePair<ContainerId, StackRuntimeView> pair in stackViewsByContainerId)
            {
                if (!IsValidMergeDestination(source.Id, pair.Key, pair.Value))
                {
                    continue;
                }

                string label = pair.Value.Visual != null && pair.Value.Visual.Label != null
                    ? pair.Value.Visual.Label.text
                    : "Stack";
                if (GUILayout.Button(label))
                {
                    MergeStacksResult result = MergeStacks(source.Id, pair.Key);
                    if (result.Succeeded)
                    {
                        CloseContextMenu();
                    }

                    break;
                }
            }

            GUILayout.EndScrollView();
            if (GUILayout.Button("Back"))
            {
                SetContextMenuMode(PrototypeContextMenuMode.Stack);
            }
        }

        private bool IsCurrentContextCardInContainer(ContainerId containerId)
        {
            return contextMenuCardView != null
                && contextMenuCardView.IsBound
                && contextMenuCardView.CardState != null
                && contextMenuCardView.CardState.BaseState.ContainerId == containerId;
        }

        private bool TryGetContextStack(out ContainerState stack, out StackRuntimeView stackView)
        {
            stack = null;
            stackView = null;
            return !contextMenuContainerId.IsEmpty
                && matchState.Containers.TryGetValue(contextMenuContainerId, out stack)
                && stack.Kind == ContainerKind.Stack
                && stackViewsByContainerId.TryGetValue(contextMenuContainerId, out stackView)
                && stackView.View != null
                && stackView.View.IsBound;
        }

        private bool HasValidMergeDestination(ContainerId sourceId)
        {
            foreach (KeyValuePair<ContainerId, StackRuntimeView> pair in stackViewsByContainerId)
            {
                if (IsValidMergeDestination(sourceId, pair.Key, pair.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsValidMergeDestination(
            ContainerId sourceId,
            ContainerId destinationId,
            StackRuntimeView destinationView)
        {
            return destinationId != sourceId
                && destinationView != null
                && destinationView.View != null
                && destinationView.View.IsBound
                && matchState.Containers.TryGetValue(destinationId, out ContainerState destination)
                && destination.Kind == ContainerKind.Stack;
        }

        private float ContextMenuHeight(PrototypeContextMenuMode mode)
        {
            switch (mode)
            {
                case PrototypeContextMenuMode.Deck:
                    return 145f;
                case PrototypeContextMenuMode.DrawCards:
                    return 155f;
                case PrototypeContextMenuMode.TabletopCard:
                    return 90f;
                case PrototypeContextMenuMode.FloorCard:
                    return 90f;
                case PrototypeContextMenuMode.StackCard:
                    return 120f;
                case PrototypeContextMenuMode.Stack:
                    return 120f;
                case PrototypeContextMenuMode.MergeDestination:
                    return Mathf.Min(320f, 82f + (stackViewsByContainerId.Count * 28f));
                case PrototypeContextMenuMode.Die:
                    return 110f;
                default:
                    return 90f;
            }
        }

        private void ValidateTrapFloorConfiguration()
        {
            ValidateCommonConfiguration();
            RequireReference(prototypeCardPrefab, nameof(prototypeCardPrefab));
            RequireReference(prototypePawnPrefab, nameof(prototypePawnPrefab));
            RequireReference(prototypeTokenPrefab, nameof(prototypeTokenPrefab));
            RequireReference(prototypeDeckPrefab, nameof(prototypeDeckPrefab));
            RequireReference(prototypeConsolePrefab, nameof(prototypeConsolePrefab));
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
            ValidateFiniteGreaterThanZero(floorCardVisualScale, nameof(floorCardVisualScale));
            ValidateDistinctViews();
            ValidateSelectionPresentationReferences();
            ValidateCardPrefabReferences();
            ValidateTrapFloorPrefabReferences();
            ValidatePreInitializationState();
        }

        private void ValidateCommonConfiguration()
        {
            RequireReference(targetCamera, nameof(targetCamera));
            RequireReference(cameraInputAdapter, nameof(cameraInputAdapter));
            RequireReference(objectInputAdapter, nameof(objectInputAdapter));
            RequireReference(inputFrameCoordinator, nameof(inputFrameCoordinator));

            if (!targetCamera.orthographic)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires an orthographic Camera.");
            }

            ValidateFiniteGreaterThanZero(maximumHitDistance, nameof(maximumHitDistance));
            ValidateFiniteGreaterThanOrEqualToZero(dragThresholdPixels, nameof(dragThresholdPixels));
            ValidateFiniteGreaterThanZero(worldUnitsPerTableUnit, nameof(worldUnitsPerTableUnit));
            ValidateFinite(tabletopHeight, nameof(tabletopHeight));
            ValidateFiniteGreaterThanOrEqualToZero(tabletopLayerHeight, nameof(tabletopLayerHeight));
            ValidateFiniteGreaterThanOrEqualToZero(
                tabletopLocalOrderHeight,
                nameof(tabletopLocalOrderHeight));
            ValidateFiniteGreaterThanOrEqualToZero(pickupLift, nameof(pickupLift));
            ValidateFiniteGreaterThanOrEqualToZero(dragLift, nameof(dragLift));
            ValidateFiniteGreaterThanOrEqualToZero(pickupResponseDuration, nameof(pickupResponseDuration));
            ValidateFiniteGreaterThanOrEqualToZero(dragFollowSmoothing, nameof(dragFollowSmoothing));
            ValidateFiniteGreaterThanOrEqualToZero(settleDuration, nameof(settleDuration));
            ValidateFiniteGreaterThanOrEqualToZero(returnDuration, nameof(returnDuration));
            ValidateFiniteGreaterThanOrEqualToZero(handReflowDuration, nameof(handReflowDuration));
            ValidateFiniteGreaterThanOrEqualToZero(magneticDistance, nameof(magneticDistance));
            ValidateFiniteGreaterThanOrEqualToZero(feedbackDuration, nameof(feedbackDuration));
            ValidateFiniteGreaterThanOrEqualToZero(shuffleCompression, nameof(shuffleCompression));
            ValidateToolboxPrefabReferences();
        }

        private void ValidateToolboxPrefabReferences()
        {
            RequireReference(prototypeCardPrefab, nameof(prototypeCardPrefab));
            RequireReference(prototypePawnPrefab, nameof(prototypePawnPrefab));
            RequireReference(prototypeTokenPrefab, nameof(prototypeTokenPrefab));
            RequireReference(prototypeDiePrefab, nameof(prototypeDiePrefab));
            RequireReference(prototypeDeckPrefab, nameof(prototypeDeckPrefab));
            RequireReference(prototypeStackPrefab, nameof(prototypeStackPrefab));

            if (prototypeCardPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException("prototypeCardPrefab must reference a prefab asset.");
            }

            prototypeCardPrefab.ValidateReferences();
            ValidateObjectPrefab(prototypePawnPrefab, nameof(prototypePawnPrefab));
            ValidateObjectPrefab(prototypeTokenPrefab, nameof(prototypeTokenPrefab));
            ValidateObjectPrefab(prototypeDiePrefab, nameof(prototypeDiePrefab));

            if (prototypeDeckPrefab.gameObject.scene.IsValid()
                || prototypeStackPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException("Toolbox container prefabs must reference prefab assets.");
            }

            prototypeDeckPrefab.ValidateReferences();
            prototypeDeckPrefab.GetView<DeckView>();
            prototypeStackPrefab.ValidateReferences();
            ValidateStackLayoutAnchor(prototypeStackPrefab);
        }

        private void ValidateInputPreInitializationState()
        {
            if (!cameraInputAdapter.IsInitialized)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires an initialized Camera input adapter.");
            }

            if (!objectInputAdapter.HasValidActionConfiguration)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires a valid Object input adapter action configuration.");
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
            sceneConsoleView.gameObject.SetActive(true);
        }

        private void HideSessionPresentation()
        {
            SetActiveIfPresent(cardView, false);
            SetActiveIfPresent(pawnView, false);
            SetActiveIfPresent(tokenView, false);
            SetActiveIfPresent(sceneDeckVisual, false);
            SetActiveIfPresent(sceneStackAVisual, false);
            SetActiveIfPresent(sceneStackBVisual, false);
            SetActiveIfPresent(sceneDiscardPileVisual, false);
            SetActiveIfPresent(sceneHandVisual, false);
            SetActiveIfPresent(sceneConsoleView, false);
        }

        private void SuspendCameraInputForSessionEntry()
        {
            if (cameraInputAdapter != null && cameraInputAdapter.enabled)
            {
                cameraInputAdapter.enabled = false;
                cameraInputSuspendedForSessionEntry = true;
            }
        }

        private void ResumeCameraInputForSession()
        {
            if (!cameraInputSuspendedForSessionEntry)
            {
                return;
            }

            cameraInputSuspendedForSessionEntry = false;
            if (cameraInputAdapter != null)
            {
                cameraInputAdapter.enabled = true;
            }
        }

        private static void SetActiveIfPresent(Component component, bool active)
        {
            if (component != null)
            {
                component.gameObject.SetActive(active);
            }
        }

        private void ValidateFixedContainerReferences()
        {
            RequireReference(sceneDeckVisual, nameof(sceneDeckVisual));
            RequireReference(sceneStackAVisual, nameof(sceneStackAVisual));
            RequireReference(sceneStackBVisual, nameof(sceneStackBVisual));
            RequireReference(prototypeStackPrefab, nameof(prototypeStackPrefab));
            RequireReference(sceneDiscardPileVisual, nameof(sceneDiscardPileVisual));
            RequireReference(sceneHandVisual, nameof(sceneHandVisual));

            sceneDeckVisual.ValidateReferences();
            sceneStackAVisual.ValidateReferences();
            sceneStackBVisual.ValidateReferences();
            sceneDiscardPileVisual.ValidateReferences();
            sceneHandVisual.ValidateReferences();

            if (prototypeStackPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires prototypeStackPrefab to reference a prefab asset.");
            }

            prototypeStackPrefab.ValidateReferences();
            ValidateStackLayoutAnchor(prototypeStackPrefab);

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

            ValidateStackLayoutAnchor(sceneStackAVisual);
            ValidateStackLayoutAnchor(sceneStackBVisual);

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

        private void BuildRuntimeGraph(bool restoreInitialBaseline)
        {
            interactionOwnerId = InteractionOwnerId.New();
            coordinateConverter = new TabletopCoordinateConverter(
                worldUnitsPerTableUnit,
                tabletopHeight,
                tabletopLayerHeight,
                tabletopLocalOrderHeight);

            if (prototypeTemplateContext == null)
            {
                throw new InvalidOperationException("Trap Floor Presentation requires a selected Template session context.");
            }

            RestorePrototypeTemplateContext(restoreInitialBaseline);
            ProjectPrototypePlayerLayout(localSeatLayout);
        }

        private void BuildFloorfallRuntime()
        {
            floorfallState = new TrapFloorFloorfallState();
            floorfallService = new TrapFloorFloorfallService(
                trapFloorTemplate,
                matchState,
                authoritativeRandomValueSource,
                floorfallState);
            floorfallTargetPresenter = new TrapFloorFloorfallTargetPresenter();
        }

        private void BuildToolboxRuntime()
        {
            authoritativeRandomValueSource = new SystemRandomValueSource();
            componentCreationUseCase = new CreateTabletopComponentUseCase(
                new GuidTabletopComponentIdentitySource());
            rollDieUseCase = new RollDieUseCase(authoritativeRandomValueSource);
            toolboxSpawnSequence = 0;
            toolboxVisible = false;
            toolboxDieChoicesVisible = false;
        }

        private static PrototypeTemplateContext CreateTrapFloorPrototypeContext(
            TabletopSession session,
            TrapFloorTemplateDefinition templateDefinition,
            PlayerId requestingPlayerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (templateDefinition == null)
            {
                throw new ArgumentNullException(nameof(templateDefinition));
            }

            TrapFloorPlayerSetupDefinition localPlayer = null;
            for (int i = 0; i < templateDefinition.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition candidate = templateDefinition.Players[i];
                if (session.CurrentMatch.GetSeat(candidate.SeatId).OccupantPlayerId == requestingPlayerId)
                {
                    localPlayer = candidate;
                    break;
                }
            }

            if (localPlayer == null)
            {
                throw new InvalidOperationException(
                    "The requesting Player is not assigned to a Trap Floor Seat.");
            }

            return new PrototypeTemplateContext(
                session,
                templateDefinition,
                requestingPlayerId,
                localPlayer.SeatId,
                localPlayer.LayoutSeatIndex,
                templateDefinition.FloormasterDeckId,
                localPlayer.HandContainerId,
                templateDefinition.FloormasterDiscardId,
                ContainerId.Empty,
                ContainerId.Empty,
                templateDefinition.BoardPlayAreaId,
                localPlayer.AvatarCardId,
                localPlayer.PawnId,
                templateDefinition.CoinTokenIds[0],
                templateDefinition.CardLabels,
                new Dictionary<ObjectDefinitionId, ButtonCardDefinition>());
        }

        private void ProjectPrototypePlayerLayout(PlayerSeatLayoutEntry seatLayout)
        {
            ApplyAuthoredPose(
                sceneHandVisual.transform,
                TrapFloorTemplateFactory.GetHandPose(seatLayout));
            ApplyAuthoredPose(
                sceneConsoleView.transform,
                TrapFloorTemplateFactory.GetConsolePose(seatLayout));
        }

        private void ProjectTrapFloorCameraBookmark()
        {
            IReadOnlyList<GameTemplateCameraBookmarkDefinition> bookmarks =
                prototypeTemplateContext.Session.CameraBookmarks;
            if (bookmarks.Count == 0)
            {
                return;
            }

            GameTemplateCameraBookmarkDefinition bookmark = bookmarks[0];
            cameraInputAdapter.CameraController.Focus(
                bookmark.FocusCoordinate,
                bookmark.OrthographicSize);
        }

        private void ApplyAuthoredPose(Transform target, TabletopPose pose)
        {
            target.SetPositionAndRotation(
                coordinateConverter.ToWorldPosition(pose),
                coordinateConverter.ToWorldRotation(pose));
        }

        private void RestorePrototypeTemplateContext(bool restoreInitialBaseline)
        {
            PrototypeTemplateContext context = prototypeTemplateContext;
            matchState = restoreInitialBaseline
                ? context.Session.Reset()
                : context.Session.CurrentMatch;
            playerLayout = context.Session.PlayerLayout;
            trapFloorTemplate = context.TrapFloorTemplate;
            localPlayerLayoutSeatIndex = context.LocalPlayerLayoutSeatIndex;
            if (!playerLayout.TryGetSeat(localPlayerLayoutSeatIndex, out localSeatLayout))
            {
                throw new InvalidOperationException("The prototype Template Player Layout is missing its local Seat entry.");
            }

            localPlayerId = context.LocalPlayerId;
            localSeatId = context.LocalSeatId;
            deckContainerId = context.DeckContainerId;
            handContainerId = context.HandContainerId;
            discardContainerId = context.DiscardContainerId;
            stackAContainerId = context.StackAContainerId;
            stackBContainerId = context.StackBContainerId;
            primaryStackContainerId = stackAContainerId;
            centralPlayAreaId = context.CentralPlayAreaId;
            cardState = matchState.Cards[context.LooseCardId];
            pawnState = matchState.Pawns[context.PawnId];
            tokenState = matchState.Tokens[context.TokenId];

            foreach (KeyValuePair<TabletopObjectId, string> label in context.LabelsByCardId)
            {
                labelsByCardId.Add(label.Key, label.Value);
            }

            foreach (KeyValuePair<ObjectDefinitionId, ButtonCardDefinition> definition in context.ButtonDefinitions)
            {
                buttonDefinitions.Add(definition.Key, definition.Value);
            }
        }

        private void ValidateTrapFloorPrefabReferences()
        {
            ValidateObjectPrefab(prototypePawnPrefab, nameof(prototypePawnPrefab));
            ValidateObjectPrefab(prototypeTokenPrefab, nameof(prototypeTokenPrefab));

            if (prototypeDeckPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires prototypeDeckPrefab to reference a prefab asset.");
            }

            prototypeDeckPrefab.ValidateReferences();
            prototypeDeckPrefab.GetView<DeckView>();

            if (prototypeConsolePrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires prototypeConsolePrefab to reference a prefab asset.");
            }

            ConsoleSlotView[] slotViews = prototypeConsolePrefab.GetComponentsInChildren<ConsoleSlotView>(true);
            if (slotViews.Length != PrototypeConsoleSlotCount)
            {
                throw new InvalidOperationException(
                    $"The prototype Console prefab requires exactly {PrototypeConsoleSlotCount} Console Slots.");
            }

            for (int i = 0; i < slotViews.Length; i++)
            {
                PrototypeConsoleSlotVisual slotVisual = slotViews[i].GetComponent<PrototypeConsoleSlotVisual>();
                RequireReference(slotVisual, $"PrototypeConsoleSlotVisual on prefab Slot {i}");
                slotVisual.ValidateReferences();
            }
        }

        private static void ValidateObjectPrefab(TabletopObjectView objectView, string fieldName)
        {
            if (objectView.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    $"TabletopPrototypeComposition requires {fieldName} to reference a prefab asset.");
            }

            TabletopSelectionVisual selectionVisual = objectView.GetComponent<TabletopSelectionVisual>();
            if (selectionVisual == null
                || !selectionVisual.IsConfigured
                || !ReferenceEquals(selectionVisual.ObjectView, objectView))
            {
                throw new InvalidOperationException(
                    $"TabletopPrototypeComposition requires {fieldName} to own an explicit selection visual.");
            }
        }

        private void BindObjectViews()
        {
            ConfigureCardVisuals(
                looseCardVisualReferences,
                cardState,
                labelsByCardId[cardState.BaseState.Id]);
            cardSelectionVisual.SetSelected(false);
            cardView.Bind(cardState, coordinateConverter);
            cardViewBoundByComposition = true;
            cardVisualReferences.Add(looseCardVisualReferences);
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
            pawnViews.Add(pawnView);
            pawnSelectionVisuals.Add(pawnSelectionVisual);

            foreach (PawnState pawn in matchState.Pawns.Values)
            {
                if (ReferenceEquals(pawn, pawnState))
                {
                    continue;
                }

                PawnView createdPawnView = CreatePawnView(pawn, out TabletopSelectionVisual createdSelectionVisual);
                pawnViews.Add(createdPawnView);
                pawnSelectionVisuals.Add(createdSelectionVisual);
            }

            tokenSelectionVisual.SetSelected(false);
            tokenView.Bind(tokenState, coordinateConverter);
            tokenView.transform.localScale = Vector3.one * TrapFloorCoinVisualScale;
            tokenViewBoundByComposition = true;
            tokenViews.Add(tokenView);
            tokenSelectionVisuals.Add(tokenSelectionVisual);

            foreach (TokenState token in matchState.Tokens.Values)
            {
                if (ReferenceEquals(token, tokenState))
                {
                    continue;
                }

                TokenView createdTokenView = CreateTokenView(
                    token,
                    out TabletopSelectionVisual createdSelectionVisual,
                    TrapFloorCoinVisualScale);
                tokenViews.Add(createdTokenView);
                tokenSelectionVisuals.Add(createdSelectionVisual);
            }

            foreach (DieState die in matchState.Dice.Values)
            {
                DieView createdDieView = CreateDieView(
                    die,
                    $"d{die.SideCount}",
                    out TabletopSelectionVisual createdSelectionVisual);
                dieViews.Add(createdDieView);
                dieSelectionVisuals.Add(createdSelectionVisual);
            }
        }

        private void BuildContainerViews()
        {
            deckView = sceneDeckVisual.GetView<DeckView>();
            handView = sceneHandVisual.GetView<HandView>();
            DeactivateUnusedSceneStack(sceneStackAVisual);
            DeactivateUnusedSceneStack(sceneStackBVisual);
            discardPileView = sceneDiscardPileVisual.GetView<DiscardPileView>();
            consoleView = sceneConsoleView;
            playerConsoleViews.Add(consoleView);

            for (int playerIndex = 0; playerIndex < trapFloorTemplate.Players.Count; playerIndex++)
            {
                TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[playerIndex];
                RuntimeDeckInstance controllerDeck = CreateRuntimeDeckInstance(
                    $"Player {playerIndex + 1} Controller Deck",
                    $"P{playerIndex + 1} CTRL",
                    player.ControllerDeckId);
                runtimeDeckInstances.Add(controllerDeck);
                controllerDeckViews.Add(controllerDeck.View);

                if (player.LayoutSeatIndex == localPlayerLayoutSeatIndex)
                {
                    continue;
                }

                RuntimeConsoleInstance playerConsole = CreateRuntimeConsoleInstance(
                    $"Player {playerIndex + 1} Console",
                    player.LayoutSeatIndex);
                runtimeConsoleInstances.Add(playerConsole);
                playerConsoleViews.Add(playerConsole.View);
            }
        }

        private static void DeactivateUnusedSceneStack(PrototypeFixedContainerVisual visual)
        {
            visual.DropTarget.ClearConfiguration();
            visual.DropTarget.enabled = false;
            visual.TargetCollider.enabled = false;
            visual.ClearFeedback();
            visual.gameObject.SetActive(false);
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
                visual,
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
                    stackRuntimeView.Visual.LayoutAnchor,
                    coordinateConverter,
                    cardViews);
            }

            discardPileView.Bind(discard, matchState.ContainerPlacements[discardContainerId], coordinateConverter, cardViews);

            consoleSlotViews.Clear();
            BindConsole(
                consoleView,
                matchState.GetSeat(localSeatId).Console,
                resolvedSceneConsoleSlotViews,
                resolvedSceneConsoleSlotVisuals);

            for (int i = 0; i < runtimeConsoleInstances.Count; i++)
            {
                RuntimeConsoleInstance instance = runtimeConsoleInstances[i];
                TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[instance.LayoutSeatIndex];
                BindConsole(
                    instance.View,
                    matchState.GetSeat(player.SeatId).Console,
                    instance.SlotViews,
                    instance.SlotVisuals);
            }

            for (int i = 0; i < runtimeDeckInstances.Count; i++)
            {
                RuntimeDeckInstance instance = runtimeDeckInstances[i];
                ContainerState controllerDeck = matchState.GetContainer(instance.ContainerId);
                instance.View.Bind(
                    controllerDeck,
                    matchState.ContainerPlacements[instance.ContainerId],
                    coordinateConverter,
                    cardViews);
            }

            ConfigureContainerLabel(sceneDeckVisual.Label, "FM DECK");
            ConfigureContainerLabel(sceneDiscardPileVisual.Label, "FM DISC");
            ConfigureContainerLabel(sceneHandVisual.Label, "HAND");
            RebuildLayoutViewCollection();
        }

        private void BindConsole(
            ConsoleView targetConsole,
            ConsoleState runtimeConsole,
            IReadOnlyList<ConsoleSlotView> slotViews,
            IReadOnlyList<PrototypeConsoleSlotVisual> slotVisuals)
        {
            if (slotViews.Count != runtimeConsole.SlotCount || slotVisuals.Count != runtimeConsole.SlotCount)
            {
                throw new InvalidOperationException("Trap Floor Console presentation does not match its Runtime Slot count.");
            }

            List<ConsoleSlotView> orderedSlots = new List<ConsoleSlotView>(runtimeConsole.SlotCount);
            for (int i = 0; i < runtimeConsole.SlotCount; i++)
            {
                ContainerState slot = matchState.GetContainer(runtimeConsole.SlotContainerIds[i]);
                ConsoleSlotView slotView = slotViews[i];
                PrototypeConsoleSlotVisual slotVisual = slotVisuals[i];
                slotView.Bind(slot, slotVisual.LayoutAnchor, coordinateConverter, cardViews);
                consoleSlotViews.Add(slotView);
                consoleSlotVisualsByContainerId.Add(slot.Id, slotVisual);
                orderedSlots.Add(slotView);
            }

            targetConsole.Bind(runtimeConsole, targetConsole.LayoutAnchor, orderedSlots);
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
                ConfigureConsoleSlot(consoleSlotViews[i]);
            }
        }

        private void BuildInteractionGraph()
        {
            selectionState = new TabletopSelectionState();
            hitResolver = new TabletopObjectHitResolver(targetCamera, interactionLayerMask, maximumHitDistance);
            pointerProjector = new TabletopPointerProjector(targetCamera, coordinateConverter, tabletopHeight);
            lockService = new LocalInteractionLockService();
            interactionStateMachine = new TabletopInteractionStateMachine(dragThresholdPixels);
            previewSession = new TabletopDragPreviewSession(
                presentationTransitions,
                pickupLift,
                dragLift,
                pickupResponseDuration,
                dragFollowSmoothing,
                settleDuration,
                returnDuration);
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
                pawnSelectionVisuals,
                tokenSelectionVisuals,
                dieSelectionVisuals);
            componentPlacementController = new TabletopComponentPlacementController(
                pointerProjector,
                coordinateConverter,
                CommitToolboxPlacement);
            inputFrameCoordinator.ConfigureComponentPlacement(componentPlacementController);
            componentPlacementInputConfiguredByComposition = true;
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
                layoutViews,
                cardViews,
                presentationTransitions,
                settleDuration,
                returnDuration,
                handReflowDuration);
            containedCardDragCoordinator = handView != null
                ? new ContainedCardDragCoordinator(
                    interactionOwnerId,
                    lockService,
                    interactionStateMachine,
                    previewSession,
                    pointerProjector,
                    dropTargetResolver,
                    transferCoordinator,
                    layoutViewLookup,
                    this,
                    magneticDistance,
                    handView,
                    ReorderHandCardFromDrag)
                : new ContainedCardDragCoordinator(
                    interactionOwnerId,
                    lockService,
                    interactionStateMachine,
                    previewSession,
                    pointerProjector,
                    dropTargetResolver,
                    transferCoordinator,
                    layoutViewLookup,
                    this,
                    magneticDistance);
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
                transferCoordinator,
                layoutViewLookup,
                this,
                magneticDistance);
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

            for (int i = 0; i < controllerDeckViews.Count; i++)
            {
                if (controllerDeckViews[i] != null && controllerDeckViews[i].IsBound)
                {
                    layoutViews.Add(controllerDeckViews[i]);
                }
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

            return MoveCardInContainer(selectedCard, containerId, delta);
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

            return MoveCardInContainer(selectedCard, containerId, delta);
        }

        private ReorderContainerResult MoveCardInContainer(
            CardView card,
            ContainerId containerId,
            int delta)
        {
            EnsureInitialized();
            if (card == null || card.CardState == null)
            {
                ShowMessage("Card is unavailable.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing);
            }

            if (card.CardState.BaseState.ContainerId != containerId)
            {
                ShowMessage("Card is not in that Container.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectContainerMismatch);
            }

            ContainerState container = matchState.GetContainer(containerId);
            int fromIndex = container.IndexOf(card.ObjectId);
            int toIndex = Mathf.Clamp(fromIndex + delta, 0, container.Count - 1);
            return ReorderCardInContainer(card, container, fromIndex, toIndex);
        }

        private ReorderContainerResult ReorderHandCardFromDrag(CardView card, int targetIndex)
        {
            EnsureInitialized();
            if (card == null
                || card.CardState == null
                || card.CardState.BaseState.ContainerId != handContainerId)
            {
                ShowMessage("Hand Card is unavailable.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectContainerMismatch);
            }

            ContainerState hand = matchState.GetContainer(handContainerId);
            int fromIndex = hand.IndexOf(card.ObjectId);
            int toIndex = Mathf.Clamp(targetIndex, 0, hand.Count - 1);
            return ReorderCardInContainer(card, hand, fromIndex, toIndex);
        }

        private ReorderContainerResult ReorderCardInContainer(
            CardView card,
            ContainerState container,
            int fromIndex,
            int toIndex)
        {
            ContainerId containerId = container.Id;
            if (fromIndex < 0)
            {
                ShowMessage("Card is not in that Container.");
                return ReorderContainerResult.Failure(CommandResultStatus.Rejected, ReorderContainerError.ObjectMissing);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(containerId);
            ReorderContainerResult result = new ReorderContainerUseCase().Execute(
                matchState,
                new ReorderContainerCommand(
                    CreateCommandContext(),
                    containerId,
                    card.ObjectId,
                    fromIndex,
                    toIndex));

            if (result.Succeeded)
            {
                ApplyLayout(containerId);
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    containerId == handContainerId ? handReflowDuration : settleDuration);
                ShowMessage("Card reordered.");
            }
            else
            {
                ApplyLayout(containerId);
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
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

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(sourceId, destinationId);
            MergeStacksResult result = new MergeStacksUseCase().Execute(
                matchState,
                new MergeStacksCommand(CreateCommandContext(), sourceId, destinationId));
            if (result.Succeeded)
            {
                StackRuntimeView destinationView = stackViewsByContainerId[destinationId];
                RemoveStackRuntimeView(sourceId);
                destinationView.View.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    settleDuration,
                    0.04f);
                presentationTransitions.Pulse(destinationView.Root.transform, shuffleCompression, feedbackDuration);
                primaryStackContainerId = destinationId;
                ShowMessage("Stacks merged.");
            }
            else
            {
                stackViewsByContainerId[sourceId].View.ApplyAcceptedLayout();
                stackViewsByContainerId[destinationId].View.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Merge rejected: {result.Error}.");
            }

            RefreshCardContentVisibility();
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
                for (int i = 0; i < controllerDeckViews.Count; i++)
                {
                    if (controllerDeckViews[i].ContainerId == containerId)
                    {
                        controllerDeckViews[i].ApplyAcceptedLayout();
                    }
                }

                for (int i = 0; i < consoleSlotViews.Count; i++)
                {
                    if (consoleSlotViews[i].ContainerId == containerId)
                    {
                        consoleSlotViews[i].ApplyAcceptedLayout();
                    }
                }
            }

            RefreshCardContentVisibility();
        }

        private void RefreshCardContentVisibility()
        {
            if (matchState == null)
            {
                return;
            }

            for (int i = 0; i < cardVisualReferences.Count; i++)
            {
                PrototypeCardVisualReferences visualReferences = cardVisualReferences[i];
                if (visualReferences == null)
                {
                    continue;
                }

                CardView boundCardView = visualReferences.CardView;
                if (boundCardView == null
                    || !boundCardView.IsBound
                    || boundCardView.CardState == null)
                {
                    continue;
                }

                visualReferences.SetCardContentVisible(
                    ShouldShowCardContent(boundCardView.CardState));
            }
        }

        private bool ShouldShowCardContent(CardInstanceState card)
        {
            ContainerId containerId = card.BaseState.ContainerId;
            if (containerId.IsEmpty
                || !matchState.Containers.TryGetValue(containerId, out ContainerState container)
                || !ShowsOnlyTopCardContent(container.Kind))
            {
                return true;
            }

            return container.Count > 0
                && container.ObjectIds[container.Count - 1] == card.BaseState.Id;
        }

        private static bool ShowsOnlyTopCardContent(ContainerKind kind)
        {
            return kind == ContainerKind.Deck
                || kind == ContainerKind.Stack
                || kind == ContainerKind.DiscardPile;
        }

        private IReadOnlyDictionary<Transform, TabletopTransformSnapshot> CaptureContainerCardTransforms(
            params ContainerId[] containerIds)
        {
            Dictionary<Transform, TabletopTransformSnapshot> starts =
                new Dictionary<Transform, TabletopTransformSnapshot>();
            for (int cardIndex = 0; cardIndex < cardViews.Count; cardIndex++)
            {
                CardView candidate = cardViews[cardIndex];
                if (candidate == null || candidate.CardState == null)
                {
                    continue;
                }

                ContainerId candidateContainerId = candidate.CardState.BaseState.ContainerId;
                for (int containerIndex = 0; containerIndex < containerIds.Length; containerIndex++)
                {
                    if (candidateContainerId != containerIds[containerIndex])
                    {
                        continue;
                    }

                    starts[candidate.transform] =
                        presentationTransitions.StopAndCapture(candidate.transform);
                    break;
                }
            }

            return starts;
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
            if (clone.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(clone, gameObject.scene);
            }

            clone.name = $"Card {label}";
            RuntimeCardInstance runtimeCardInstance = new RuntimeCardInstance(clone);
            runtimeCardInstances.Add(runtimeCardInstance);
            createdVisualReferences.ValidateReferences();
            CardView createdView = createdVisualReferences.CardView;
            selectionVisual = createdVisualReferences.SelectionVisual;
            runtimeCardInstance.SetReferences(createdView, selectionVisual, createdVisualReferences);
            selectionVisual.SetSelected(false);
            ConfigureCardVisuals(createdVisualReferences, card, label);
            createdView.Bind(card, coordinateConverter);
            if (trapFloorTemplate != null && trapFloorTemplate.IsFloorCard(card.BaseState.Id))
            {
                clone.transform.localScale = Vector3.one * floorCardVisualScale;
                floorfallTargetPresenter.Register(
                    card.BaseState.Id,
                    createdVisualReferences.FaceUpRenderer);
            }

            cardVisualReferences.Add(createdVisualReferences);
            return createdView;
        }

        private PawnView CreatePawnView(
            PawnState pawn,
            out TabletopSelectionVisual selectionVisual)
        {
            PawnView createdView = Instantiate(prototypePawnPrefab);
            GameObject root = PrepareRuntimeRoot(createdView.gameObject, "Trap Floor Pawn");
            selectionVisual = createdView.GetComponent<TabletopSelectionVisual>();
            ValidateRuntimeSelectionVisual(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            createdView.Bind(pawn, coordinateConverter);
            runtimePawnInstances.Add(new RuntimeObjectInstance(root, createdView, selectionVisual));
            return createdView;
        }

        private TokenView CreateTokenView(
            TokenState token,
            out TabletopSelectionVisual selectionVisual,
            float visualScale)
        {
            TokenView createdView = Instantiate(prototypeTokenPrefab);
            GameObject root = PrepareRuntimeRoot(createdView.gameObject, "Token");
            selectionVisual = createdView.GetComponent<TabletopSelectionVisual>();
            ValidateRuntimeSelectionVisual(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            createdView.Bind(token, coordinateConverter);
            createdView.transform.localScale = Vector3.one * visualScale;
            runtimeTokenInstances.Add(new RuntimeObjectInstance(root, createdView, selectionVisual));
            return createdView;
        }

        private DieView CreateDieView(
            DieState die,
            string name,
            out TabletopSelectionVisual selectionVisual)
        {
            DieView createdView = Instantiate(prototypeDiePrefab);
            GameObject root = PrepareRuntimeRoot(createdView.gameObject, name);
            ConfigurePrototypeLabel(createdView.ResultLabel, createdView.ResultLabel.text, 0.18f, 64);
            selectionVisual = createdView.GetComponent<TabletopSelectionVisual>();
            ValidateRuntimeSelectionVisual(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            createdView.Bind(die, coordinateConverter);
            runtimeDieInstances.Add(new RuntimeObjectInstance(root, createdView, selectionVisual));
            return createdView;
        }

        private RuntimeDeckInstance CreateRuntimeDeckInstance(
            string name,
            string displayLabel,
            ContainerId containerId)
        {
            PrototypeFixedContainerVisual visual = Instantiate(prototypeDeckPrefab);
            GameObject root = PrepareRuntimeRoot(visual.gameObject, name);
            visual.ValidateReferences();
            DeckView view = visual.GetView<DeckView>();
            ConfigureContainerLabel(visual.Label, displayLabel);
            visual.DropTarget.ClearConfiguration();
            visual.DropTarget.enabled = false;
            visual.TargetCollider.enabled = false;
            visual.ClearFeedback();
            return new RuntimeDeckInstance(root, visual, view, containerId);
        }

        private RuntimeConsoleInstance CreateRuntimeConsoleInstance(string name, int layoutSeatIndex)
        {
            ConsoleView view = Instantiate(prototypeConsolePrefab);
            GameObject root = PrepareRuntimeRoot(view.gameObject, name);
            if (!playerLayout.TryGetSeat(layoutSeatIndex, out PlayerSeatLayoutEntry seatLayout))
            {
                throw new InvalidOperationException("Trap Floor Console references a missing Player Layout Seat.");
            }

            ApplyAuthoredPose(
                root.transform,
                TrapFloorTemplateFactory.GetConsolePose(seatLayout));
            ConsoleSlotView[] slotViews = view.GetComponentsInChildren<ConsoleSlotView>(true);
            if (slotViews.Length != PrototypeConsoleSlotCount)
            {
                throw new InvalidOperationException(
                    $"Runtime Trap Floor Consoles require exactly {PrototypeConsoleSlotCount} Slots.");
            }

            Array.Sort(slotViews, (left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
            PrototypeConsoleSlotVisual[] slotVisuals = new PrototypeConsoleSlotVisual[slotViews.Length];
            for (int i = 0; i < slotViews.Length; i++)
            {
                slotVisuals[i] = slotViews[i].GetComponent<PrototypeConsoleSlotVisual>();
                RequireReference(slotVisuals[i], $"Runtime Console Slot visual {i}");
                slotVisuals[i].ValidateReferences();
            }

            return new RuntimeConsoleInstance(root, view, layoutSeatIndex, slotViews, slotVisuals);
        }

        private GameObject PrepareRuntimeRoot(GameObject root, string name)
        {
            if (root.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            }

            root.name = name;
            return root;
        }

        private static void ValidateRuntimeSelectionVisual(
            TabletopObjectView view,
            TabletopSelectionVisual selectionVisual)
        {
            if (selectionVisual == null
                || !selectionVisual.IsConfigured
                || !ReferenceEquals(selectionVisual.ObjectView, view))
            {
                throw new InvalidOperationException("A runtime Tabletop Object prefab has invalid selection references.");
            }
        }

        private void ConfigureCardVisuals(
            PrototypeCardVisualReferences visualReferences,
            CardInstanceState card,
            string label)
        {
            bool isButtonCard = IsButtonCard(card);
            ApplyCardColor(
                visualReferences.FaceUpRenderer,
                isButtonCard
                    ? new Color(0.58f, 0.88f, 0.82f)
                    : new Color(0.95f, 0.88f, 0.42f));
            ApplyCardColor(visualReferences.FaceDownRenderer, new Color(0.10f, 0.19f, 0.42f));
            ConfigurePrototypeLabel(
                visualReferences.FrontLabel,
                label,
                trapFloorTemplate != null && trapFloorTemplate.IsFloorCard(card.BaseState.Id)
                    ? TrapFloorFloorLabelCharacterSize
                    : TrapFloorCardLabelCharacterSize,
                TrapFloorCardLabelFontSize);
            ConfigurePrototypeLabel(
                visualReferences.BackLabel,
                visualReferences.BackLabel.text,
                TrapFloorCardBackLabelCharacterSize,
                TrapFloorCardLabelFontSize);
        }

        private static void ConfigureContainerLabel(TextMesh label, string text)
        {
            ConfigurePrototypeLabel(
                label,
                text,
                TrapFloorContainerLabelCharacterSize,
                TrapFloorContainerLabelFontSize);
        }

        private static void ConfigurePrototypeLabel(
            TextMesh label,
            string text,
            float characterSize,
            int fontSize)
        {
            label.text = text;
            label.characterSize = characterSize;
            label.fontSize = fontSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.lineSpacing = 0.8f;
            PrototypeWorldTextDepth.Apply(label);
        }

        private static void ApplyCardColor(Renderer renderer, Color color)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetFloat("_Metallic", 0f);
            properties.SetFloat("_Smoothness", 0.12f);
            renderer.SetPropertyBlock(properties);
        }

        private StackRuntimeView CreateStackRuntimeView(
            string name,
            ContainerState container,
            ContainerPlacementState placement)
        {
            PrototypeFixedContainerVisual visual = Instantiate(prototypeStackPrefab);
            GameObject root = visual.gameObject;
            if (root.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            }

            root.name = name;
            runtimeOwnedStackRoots.Add(root);

            visual.ValidateReferences();
            StackView view = visual.GetView<StackView>();
            ConfigureContainerLabel(visual.Label, name);
            view.Bind(container, placement, visual.LayoutAnchor, coordinateConverter, cardViews);
            StackRuntimeView stackRuntimeView = new StackRuntimeView(
                StackViewOwnership.RuntimeOwned,
                root,
                visual,
                view,
                container,
                placement,
                visual.DropTarget);
            ConfigureFixedContainer(visual, view);
            return stackRuntimeView;
        }

        private void ConfigureStackDropTarget(StackRuntimeView stackRuntimeView)
        {
            ConfigureFixedContainer(stackRuntimeView.Visual, stackRuntimeView.View);
            stackRuntimeView.DropTarget = stackRuntimeView.Visual.DropTarget;
        }

        private void ConfigureConsoleSlot(ConsoleSlotView slotView)
        {
            if (!consoleSlotVisualsByContainerId.TryGetValue(
                    slotView.ContainerId,
                    out PrototypeConsoleSlotVisual slotVisual))
            {
                throw new InvalidOperationException("A bound Console Slot is missing its authored visual references.");
            }

            slotVisual.DropTarget.Configure(slotView, slotVisual.TargetCollider);
            slotVisual.DropTarget.enabled = true;
            slotVisual.TargetCollider.enabled = true;
            slotVisual.ClearFeedback();
            feedbackTargetsByContainerId[slotView.ContainerId] = new ContainerFeedbackTarget(slotVisual);
        }

        private bool IsButtonCard(CardInstanceState card)
        {
            return buttonDefinitions.ContainsKey(card.BaseState.DefinitionId);
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
            feedbackHoldUntil = 0f;
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

            if (activeSession != null
                && activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                return $"Empty Table | Objects {matchState?.ObjectCount ?? 0}";
            }

            return $"Deck {ContainerCount(deckContainerId)} | Hand {ContainerCount(handContainerId)} | Discard {ContainerCount(discardContainerId)}";
        }

        private void DrawFloorfallStatus()
        {
            if (floorfallState == null || !floorfallState.CurrentTarget.HasValue)
            {
                return;
            }

            TrapFloorFloorfallTarget target = floorfallState.CurrentTarget.Value;
            Rect statusRect = new Rect(
                Mathf.Max(0f, Screen.width - FloorfallStatusWidth - 16f),
                16f,
                FloorfallStatusWidth,
                FloorfallStatusHeight);
            GUILayout.BeginArea(statusRect, GUI.skin.box);
            GUILayout.Label("FLOORFALL TARGET");
            GUILayout.Label(
                $"Die 1 / X: {target.XAxisRoll.Value}   Die 2 / Y: {target.YAxisRoll.Value}");
            GUILayout.Label($"Coordinate: {target.Coordinate}");
            GUILayout.EndArea();
        }

        private int ContainerCount(ContainerId containerId)
        {
            return matchState != null && matchState.Containers.TryGetValue(containerId, out ContainerState container)
                ? container.Count
                : 0;
        }

        private int AvailableDrawableCount()
        {
            if (matchState == null
                || !matchState.Containers.TryGetValue(deckContainerId, out ContainerState deck)
                || !matchState.Containers.TryGetValue(handContainerId, out ContainerState hand))
            {
                return 0;
            }

            int handSpace = hand.Capacity == 0
                ? deck.Count
                : Math.Max(0, hand.Capacity - hand.Count);
            return Math.Min(deck.Count, handSpace);
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
            PrototypeFixedContainerVisual visual = stackRuntimeView.Visual;
            StackView view = stackRuntimeView.View;
            TabletopContainerDropTarget dropTarget = stackRuntimeView.DropTarget;

            DisableRuntimeInteraction(root);
            if (dropTarget != null)
            {
                dropTarget.ClearConfiguration();
                dropTarget.enabled = false;
            }

            visual?.ClearFeedback();
            if (view != null && view.IsBound)
            {
                view.Unbind();
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

            presentationTransitions?.Forget(root.transform);
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

        private static void ValidateStackLayoutAnchor(PrototypeFixedContainerVisual visual)
        {
            visual.GetView<StackView>();
            if (ReferenceEquals(visual.LayoutAnchor, visual.transform))
            {
                throw new InvalidOperationException(
                    $"Stack {visual.name} requires a distinct authored Card layout anchor.");
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

        private void ReleaseRuntimeDeckInstances()
        {
            while (runtimeDeckInstances.Count > 0)
            {
                int lastIndex = runtimeDeckInstances.Count - 1;
                RuntimeDeckInstance instance = runtimeDeckInstances[lastIndex];
                GameObject root = instance.Root;
                PrototypeFixedContainerVisual visual = instance.Visual;
                DeckView view = instance.View;
                DisableRuntimeInteraction(root);
                if (visual != null)
                {
                    visual.DropTarget.ClearConfiguration();
                    visual.ClearFeedback();
                }

                if (view != null && view.IsBound)
                {
                    view.Unbind();
                }

                controllerDeckViews.Remove(view);
                layoutViews.Remove(view);
                runtimeDeckInstances.RemoveAt(lastIndex);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ReleaseRuntimeConsoleInstances()
        {
            while (runtimeConsoleInstances.Count > 0)
            {
                int lastIndex = runtimeConsoleInstances.Count - 1;
                RuntimeConsoleInstance instance = runtimeConsoleInstances[lastIndex];
                GameObject root = instance.Root;
                for (int i = 0; i < instance.SlotViews.Length; i++)
                {
                    ConsoleSlotView slotView = instance.SlotViews[i];
                    PrototypeConsoleSlotVisual slotVisual = instance.SlotVisuals[i];
                    if (slotVisual != null)
                    {
                        slotVisual.DropTarget.ClearConfiguration();
                        slotVisual.DropTarget.enabled = false;
                        slotVisual.TargetCollider.enabled = false;
                        slotVisual.ClearFeedback();
                    }

                    if (slotView != null && slotView.IsBound)
                    {
                        slotView.Unbind();
                    }

                    if (slotView != null)
                    {
                        consoleSlotViews.Remove(slotView);
                        layoutViews.Remove(slotView);
                    }
                }

                if (instance.View != null && instance.View.IsBound)
                {
                    instance.View.Unbind();
                }

                playerConsoleViews.Remove(instance.View);
                runtimeConsoleInstances.RemoveAt(lastIndex);
                instance.ClearReferences();
                DisableRuntimeInteraction(root);
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ReleaseRuntimeObjectInstances<TView>(
            List<RuntimeObjectInstance> instances,
            List<TView> views,
            List<TabletopSelectionVisual> selectionVisuals)
            where TView : TabletopObjectView
        {
            while (instances.Count > 0)
            {
                int lastIndex = instances.Count - 1;
                RuntimeObjectInstance instance = instances[lastIndex];
                GameObject root = instance.Root;
                TabletopObjectView view = instance.View;
                TabletopSelectionVisual selectionVisual = instance.SelectionVisual;
                DisableRuntimeInteraction(root);
                selectionVisual?.SetSelected(false);
                if (view != null && view.IsBound)
                {
                    view.Unbind();
                }

                if (view is TView typedView)
                {
                    views.Remove(typedView);
                }

                selectionVisuals.Remove(selectionVisual);
                if (view != null)
                {
                    presentationTransitions?.Forget(view.transform);
                }

                instances.RemoveAt(lastIndex);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
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
                PrototypeCardVisualReferences visualReferences = runtimeCardInstance.VisualReferences;

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

                if (visualReferences != null)
                {
                    cardVisualReferences.Remove(visualReferences);
                }

                if (view != null)
                {
                    presentationTransitions?.Forget(view.transform);
                }

                runtimeCardInstances.RemoveAt(lastIndex);
                runtimeCardInstance.ClearReferences();
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

        private enum PrototypeContextMenuMode
        {
            None,
            Deck,
            DrawCards,
            TabletopCard,
            FloorCard,
            StackCard,
            Stack,
            MergeDestination,
            Die,
        }

        private enum StackViewOwnership
        {
            SceneOwned,
            RuntimeOwned,
        }

        private sealed class PrototypeTemplateContext
        {
            private readonly Dictionary<TabletopObjectId, string> labelsByCardId;
            private readonly Dictionary<ObjectDefinitionId, ButtonCardDefinition> buttonDefinitions;

            public PrototypeTemplateContext(
                TabletopSession session,
                TrapFloorTemplateDefinition trapFloorTemplate,
                PlayerId localPlayerId,
                SeatId localSeatId,
                int localPlayerLayoutSeatIndex,
                ContainerId deckContainerId,
                ContainerId handContainerId,
                ContainerId discardContainerId,
                ContainerId stackAContainerId,
                ContainerId stackBContainerId,
                PlayAreaId centralPlayAreaId,
                TabletopObjectId looseCardId,
                TabletopObjectId pawnId,
                TabletopObjectId tokenId,
                IReadOnlyDictionary<TabletopObjectId, string> labelsByCardId,
                IReadOnlyDictionary<ObjectDefinitionId, ButtonCardDefinition> buttonDefinitions)
            {
                Session = session ?? throw new ArgumentNullException(nameof(session));
                TrapFloorTemplate = trapFloorTemplate ?? throw new ArgumentNullException(nameof(trapFloorTemplate));
                LocalPlayerId = localPlayerId;
                LocalSeatId = localSeatId;
                LocalPlayerLayoutSeatIndex = localPlayerLayoutSeatIndex;
                DeckContainerId = deckContainerId;
                HandContainerId = handContainerId;
                DiscardContainerId = discardContainerId;
                StackAContainerId = stackAContainerId;
                StackBContainerId = stackBContainerId;
                CentralPlayAreaId = centralPlayAreaId;
                LooseCardId = looseCardId;
                PawnId = pawnId;
                TokenId = tokenId;
                this.labelsByCardId = new Dictionary<TabletopObjectId, string>();
                foreach (KeyValuePair<TabletopObjectId, string> label in labelsByCardId)
                {
                    this.labelsByCardId.Add(label.Key, label.Value);
                }

                this.buttonDefinitions = new Dictionary<ObjectDefinitionId, ButtonCardDefinition>();
                foreach (KeyValuePair<ObjectDefinitionId, ButtonCardDefinition> definition in buttonDefinitions)
                {
                    this.buttonDefinitions.Add(definition.Key, definition.Value);
                }
            }

            public TabletopSession Session { get; }
            public TrapFloorTemplateDefinition TrapFloorTemplate { get; }
            public PlayerId LocalPlayerId { get; }
            public SeatId LocalSeatId { get; }
            public int LocalPlayerLayoutSeatIndex { get; }
            public ContainerId DeckContainerId { get; }
            public ContainerId HandContainerId { get; }
            public ContainerId DiscardContainerId { get; }
            public ContainerId StackAContainerId { get; }
            public ContainerId StackBContainerId { get; }
            public PlayAreaId CentralPlayAreaId { get; }
            public TabletopObjectId LooseCardId { get; }
            public TabletopObjectId PawnId { get; }
            public TabletopObjectId TokenId { get; }
            public IReadOnlyDictionary<TabletopObjectId, string> LabelsByCardId => labelsByCardId;
            public IReadOnlyDictionary<ObjectDefinitionId, ButtonCardDefinition> ButtonDefinitions => buttonDefinitions;
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

            public PrototypeCardVisualReferences VisualReferences { get; private set; }

            public void SetReferences(
                CardView view,
                TabletopSelectionVisual selectionVisual,
                PrototypeCardVisualReferences visualReferences)
            {
                View = view;
                SelectionVisual = selectionVisual;
                VisualReferences = visualReferences;
            }

            public void ClearReferences()
            {
                Root = null;
                View = null;
                SelectionVisual = null;
                VisualReferences = null;
            }
        }

        private sealed class RuntimeObjectInstance
        {
            public RuntimeObjectInstance(
                GameObject root,
                TabletopObjectView view,
                TabletopSelectionVisual selectionVisual)
            {
                Root = root;
                View = view;
                SelectionVisual = selectionVisual;
            }

            public GameObject Root { get; private set; }
            public TabletopObjectView View { get; private set; }
            public TabletopSelectionVisual SelectionVisual { get; private set; }

            public void ClearReferences()
            {
                Root = null;
                View = null;
                SelectionVisual = null;
            }
        }

        private sealed class RuntimeDeckInstance
        {
            public RuntimeDeckInstance(
                GameObject root,
                PrototypeFixedContainerVisual visual,
                DeckView view,
                ContainerId containerId)
            {
                Root = root;
                Visual = visual;
                View = view;
                ContainerId = containerId;
            }

            public GameObject Root { get; private set; }
            public PrototypeFixedContainerVisual Visual { get; private set; }
            public DeckView View { get; private set; }
            public ContainerId ContainerId { get; }

            public void ClearReferences()
            {
                Root = null;
                Visual = null;
                View = null;
            }
        }

        private sealed class RuntimeConsoleInstance
        {
            public RuntimeConsoleInstance(
                GameObject root,
                ConsoleView view,
                int layoutSeatIndex,
                ConsoleSlotView[] slotViews,
                PrototypeConsoleSlotVisual[] slotVisuals)
            {
                Root = root;
                View = view;
                LayoutSeatIndex = layoutSeatIndex;
                SlotViews = slotViews;
                SlotVisuals = slotVisuals;
            }

            public GameObject Root { get; private set; }
            public ConsoleView View { get; private set; }
            public int LayoutSeatIndex { get; }
            public ConsoleSlotView[] SlotViews { get; private set; }
            public PrototypeConsoleSlotVisual[] SlotVisuals { get; private set; }

            public void ClearReferences()
            {
                Root = null;
                View = null;
                SlotViews = Array.Empty<ConsoleSlotView>();
                SlotVisuals = Array.Empty<PrototypeConsoleSlotVisual>();
            }
        }

        private sealed class StackRuntimeView
        {
            public StackRuntimeView(
                StackViewOwnership ownership,
                GameObject root,
                PrototypeFixedContainerVisual visual,
                StackView view,
                ContainerState container,
                ContainerPlacementState placement,
                TabletopContainerDropTarget dropTarget)
            {
                Ownership = ownership;
                Root = root;
                Visual = visual;
                View = view;
                Container = container;
                Placement = placement;
                DropTarget = dropTarget;
            }

            public StackViewOwnership Ownership { get; }

            public GameObject Root { get; private set; }

            public PrototypeFixedContainerVisual Visual { get; private set; }

            public StackView View { get; private set; }

            public ContainerState Container { get; private set; }

            public ContainerPlacementState Placement { get; private set; }

            public TabletopContainerDropTarget DropTarget { get; set; }

            public void ClearReferences()
            {
                Root = null;
                Visual = null;
                View = null;
                Container = default(ContainerState);
                Placement = default(ContainerPlacementState);
                DropTarget = null;
            }
        }

        private sealed class ContainerFeedbackTarget
        {
            private readonly PrototypeConsoleSlotVisual authoredSlotVisual;
            private readonly PrototypeFixedContainerVisual authoredFixedContainerVisual;

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

                authoredFixedContainerVisual.ShowValidTarget();
            }

            public void SetSource()
            {
                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ShowSourceTarget();
                    return;
                }

                authoredFixedContainerVisual.ShowSourceTarget();
            }

            public void SetInvalid()
            {
                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ShowInvalidTarget();
                    return;
                }

                authoredFixedContainerVisual.ShowInvalidTarget();
            }

            public void Clear()
            {
                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ClearFeedback();
                    return;
                }

                authoredFixedContainerVisual.ClearFeedback();
            }
        }
    }
}
