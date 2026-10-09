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
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>Prototype tabletop visuals hidden at bind time without deleting their code paths.</summary>
    [Flags]
    public enum PrototypeVisualHide
    {
        None = 0,
        CardPlaceholderLabels = 1,
        ContainerLabels = 2,
        ContainerPlates = 4,
        EmptySlotPlate = 8,
        SelectionHighlight = 16,
        DieResultLabel = 32,
        PawnOwnerLabel = 64,
        CardFaceLabels = 128
    }

    public sealed class TabletopPrototypeComposition : MonoBehaviour, IContainedCardDragFeedback
    {
        // Console Slot anchors must sit at their layout positions (console-local x/z) within this tolerance.
        private const float ConsoleLayoutAnchorTolerance = 0.0005f;
        private const float TrapFloorCoinVisualScale = 0.34f;
        private const float TrapFloorCoinAreaLabelCharacterSize = 0.12f;
        private const int TrapFloorCoinAreaLabelFontSize = 56;
        private const float TrapFloorFloorLabelCharacterSize = 0.16f;
        private const float TrapFloorCardLabelCharacterSize = 0.18f;
        private const float TrapFloorCardBackLabelCharacterSize = 0.14f;
        private const float TrapFloorContainerLabelCharacterSize = 0.1f;
        private const int TrapFloorCardLabelFontSize = 64;
        private const int TrapFloorContainerLabelFontSize = 48;
        private const int ToolboxPhysicalOrderStride = 40;
        private static readonly Vector3 LocalHandRestPosition = new Vector3(0f, 0f, -1.86f);
        private static readonly Rect PrototypeControlsPanelRect = new Rect(16f, 330f, 280f, 320f);

        [SerializeField] internal UnityCamera targetCamera;
        [SerializeField] internal TabletopCameraInputAdapter cameraInputAdapter;
        [SerializeField] internal TabletopObjectInputAdapter objectInputAdapter;
        [SerializeField] internal TabletopInputFrameCoordinator inputFrameCoordinator;
        // Session-owned Trap Floor Board. Placement discovery still uses PhysicalTabletopSurface registration.
        [Tooltip("Trap Floor Board collider/surface. The Board is shown only while the Trap Floor Template is active.")]
        [SerializeField] private Collider gameBoardPhysicalSurface;
        [Tooltip("Imported Game Board model root used only as visual geometry under the authored Board collider.")]
        [SerializeField] private Transform gameBoardVisualRoot;
        [Tooltip("Only imported renderers using this Board material remain visible; showcase Table/lighting geometry stays disabled.")]
        [SerializeField] private Material gameBoardVisualMaterial;
        [SerializeField] private PhysicalInteractionConfig physicalInteraction = new PhysicalInteractionConfig();
        private PhysicalTabletopSurfaces physicalSurfaceQuery;
        private LocalPhysicalObjectAuthority physicalAuthority;
        private bool physicalFloorfallPending;
        private PlayerId physicalFloorfallActor;
        private bool physicalFloorCollapsePending;
        private PlayerId physicalFloorCollapseActor;
        private bool physicalBlindDirectionPending;
        private PlayerId physicalBlindDirectionActor;
        private TabletopObjectId physicalBlindDirectionDieId;
        // Component library (doc 22): Base box, Controller box, Game boxes and environment, each a catalog of
        // components (stable ID, prefab, linked definitions). Every spawn prefab comes from it (C1 piles, C3a
        // cards, pawns, tokens, dice and Consoles).
        [SerializeField] internal ComponentLibrary componentLibrary;
        [SerializeField] internal PrototypeRuntimeUiController runtimeUi;
        [Tooltip("Optional authored local Controller Mapping area included by Camera presets that request it.")]
        [SerializeField] private Transform sceneControllerMappingArea;
        [Tooltip("Designer-authored Trap Floor identity, content, Grid, Mode, Console, and presentation data.")]
        [SerializeField] private GameDefinition trapFloorGameDefinition;

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
        [Tooltip("Prototype visuals hidden at bind time. After changing in Play mode, press Reset or restart the session.")]
        [SerializeField] internal PrototypeVisualHide prototypeVisualHide =
            PrototypeVisualHide.CardPlaceholderLabels
            | PrototypeVisualHide.ContainerLabels
            | PrototypeVisualHide.SelectionHighlight
            | PrototypeVisualHide.DieResultLabel
            | PrototypeVisualHide.PawnOwnerLabel
            | PrototypeVisualHide.CardFaceLabels;

        private readonly List<RuntimeCardInstance> runtimeCardInstances = new List<RuntimeCardInstance>();
        private readonly List<RuntimeObjectInstance> runtimePawnInstances = new List<RuntimeObjectInstance>();
        private readonly List<RuntimeObjectInstance> runtimeTokenInstances = new List<RuntimeObjectInstance>();
        private readonly List<RuntimeObjectInstance> runtimeDieInstances = new List<RuntimeObjectInstance>();
        private readonly List<RuntimeDeckInstance> runtimeDeckInstances = new List<RuntimeDeckInstance>();
        private readonly List<RuntimeDiscardPileInstance> runtimeDiscardPileInstances =
            new List<RuntimeDiscardPileInstance>();
        private readonly List<RuntimeConsoleInstance> runtimeConsoleInstances = new List<RuntimeConsoleInstance>();
        private readonly List<RuntimeTokenContainerInstance> runtimeTokenContainerInstances =
            new List<RuntimeTokenContainerInstance>();
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
        private readonly List<TokenContainerView> tokenContainerViews = new List<TokenContainerView>();
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
        private readonly List<GameObject> officialPawnLabels = new List<GameObject>();
        private readonly List<Renderer> officialPawnRenderers = new List<Renderer>();

        private bool cameraRoutingConfiguredByComposition;
        private bool frameCoordinatorEnabledByComposition;
        private bool controlsPanelInputBlockConfiguredByComposition;
        private bool prototypeUiInputConfiguredByComposition;
        private bool componentPlacementInputConfiguredByComposition;
        private bool objectAdapterInitializedByComposition;
        private bool gameTemplatesPanelVisible;
        private readonly ActiveSessionUndoHistory<PrototypeSessionUndoSnapshot> undoHistory =
            new ActiveSessionUndoHistory<PrototypeSessionUndoSnapshot>();
        private MatchState undoTrackedMatch;
        private bool undoTransactionInProgress;
        private bool rebuildingFromUndo;
        // Empty Table hand (doc 22, H-E): the session's Hand container and whether the Hand is on. The switch is a
        // table setting, not a move: it survives Undo and Redo; Reset and loading a table turn it back on.
        private ContainerId emptyTableHandContainerId;
        private bool emptyTableHandEnabled = true;
        // Viewer settings from the Table menu (doc 23, R1); not table state, so Undo and Reset never change them.
        private bool rulesCardEnabled = true;
        private bool hintsEnabled = true;
        private TrapFloorSessionState pendingRestoredTrapFloorState;
        private PendingControllerPurchaseState pendingRestoredControllerPurchaseState;

        private MatchState matchState;
        private TabletopSession activeSession;
        private TabletopSessionBootstrapService sessionBootstrapService;
        private GameTemplateCatalog sessionTemplateCatalog;
        private readonly Dictionary<GameTemplateId, TrapFloorTemplateDefinition> availableTrapFloorTemplates =
            new Dictionary<GameTemplateId, TrapFloorTemplateDefinition>();
        private PlayerId tableActionActorId;
        private string templateCatalogError;
        private string gameTemplatesPanelError;
        private PrototypeTemplateContext prototypeTemplateContext;
        private TrapFloorTemplateDefinition trapFloorTemplate;
        private TrapFloorActivityFeedState trapFloorActivityFeed;
        private TrapFloorRevealFloorUseCase trapFloorRevealFloorUseCase;
        private TrapFloorObjectiveState trapFloorObjectiveState;
        private TrapFloorObjectiveUseCase trapFloorObjectiveUseCase;
        private TrapFloorCollapseState trapFloorCollapseState;
        private TrapFloorCollapseUseCase trapFloorCollapseUseCase;
        private TrapFloorTurnState trapFloorTurnState;
        private TrapFloorTurnService trapFloorTurnService;
        private TrapFloorAbilityResolutionState trapFloorAbilityResolutionState;
        private TrapFloorAbilityResolutionService trapFloorAbilityResolutionService;
        private TrapFloorPendingSearchState trapFloorPendingSearchState;
        private PendingControllerPurchaseState pendingControllerPurchaseState;
        private readonly ControllerInputHandService controllerInputHandService =
            new ControllerInputHandService();
        private TrapFloorActivityEntry activeFloorRevealActivity;
        private TrapFloorFloorfallState floorfallState;
        private TrapFloorFloorfallService floorfallService;
        private TrapFloorFloorfallTargetPresenter floorfallTargetPresenter;
        private TrapFloorAbilityTargetPresenter abilityTargetPresenter;
        private TrapFloorFloormasterLifecycleState floormasterLifecycleState;
        private TrapFloorFloormasterLifecycleService floormasterLifecycleService;
        private TrapFloorRoundState trapFloorRoundState;
        private TrapFloorRoundOrchestrationService trapFloorRoundOrchestrationService;
        private SystemRandomValueSource authoritativeRandomValueSource;
        private ITabletopComponentIdentitySource componentIdentitySource;
        private CreateTabletopComponentUseCase componentCreationUseCase;
        private CreateGenericCardBatchUseCase cardBatchCreationUseCase;
        private PopulateDeckUseCase populateDeckUseCase;
        private DeleteTabletopComponentUseCase componentDeletionUseCase;
        private DuplicateTabletopComponentUseCase componentDuplicationUseCase;
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
        private TokenDropTargetResolver tokenDropTargetResolver;
        private CardTransferInteractionCoordinator transferCoordinator;
        private ContainedCardDragCoordinator containedCardDragCoordinator;
        private TabletopInteractionRouter interactionRouter;
        private ContainerLayoutViewLookup layoutViewLookup;
        private TabletopPresentationTransitionController presentationTransitions;

        private SeatId localSeatId;
        private ContainerId handContainerId;
        // Local comfort setting for this player's own hand; never game state, Undo or authority.
        private readonly PlayerHandComfortSettings handComfortSettings = new PlayerHandComfortSettings();
        private ContainerId primaryStackContainerId;
        private ContainerId sourceFeedbackContainerId;
        private int dynamicStackSequence;
        private string operationMessage = "Tabletop ready.";
        private float operationMessageUntil;
        private float feedbackHoldUntil;
        private PrototypeContextMenuMode contextMenuMode;
        private Vector2 contextMenuAnchorScreenPosition;
        private TabletopObjectId contextMenuCardId;
        private ContainerId contextMenuContainerId;
        private int selectedDrawCount = 1;
        private int selectedQuantity = 1;
        private int toolboxSpawnSequence;
        private bool toolboxPlacementHintActive;
        private string toolboxPlacementSubject;
        // Icon of the catalog entry being placed from the Toolbox (shown on the Placing card); null otherwise.
        private Sprite toolboxPlacementIcon;
        private TabletopObjectId contextMenuDieId;
        private TabletopObjectId contextMenuPawnId;
        private TabletopObjectId contextMenuTokenId;
        private ConsoleId contextMenuConsoleId;
        private long contextMenuRenderedRevision = -1;
        private TabletopObjectId inspectedCardId;
        private long inspectedCardRenderedRevision = -1;
        private TrapFloorSearchKind pendingSearchKind;
        private string pendingPurchaseDefinitionStableId = string.Empty;
        private readonly List<TabletopObjectId> assistedHandCardIds =
            new List<TabletopObjectId>();

        private HandView handView;
        // Camera hand tray: created once, reused across session rebuilds (keeps its collapsed state).
        private HandTrayRig handTrayRig;
        private Action toggleHandTrayAction;
        // Other seats' hands as face-down piles on their hand zones; rebuilt with each session.
        private readonly List<HiddenHandView> hiddenHandViews = new List<HiddenHandView>();
        // Pile bays (doc 21): each pile's bay and the seat whose Console its mouth faces.
        private readonly List<PileBayView> pileBays = new List<PileBayView>();
        private readonly List<SeatId> pileBayOwnerSeats = new List<SeatId>();
        // Pile prefabs resolved from the box catalogs at initialisation (doc 22).
        private PrototypeFixedContainerVisual catalogDeckPrefab;
        private PrototypeFixedContainerVisual catalogStackPrefab;
        private PrototypeFixedContainerVisual catalogDiscardPilePrefab;
        // Object and Console prefabs resolved from the box catalogs (doc 22, C3a); no prefab field in the scene.
        private PrototypeCardVisualReferences catalogCardPrefab;
        private PawnView catalogPawnPrefab;
        private TokenView catalogTokenPrefab;
        private DieView catalogDiePrefab;
        private ConsoleView catalogConsolePrefab;
        private PrototypeFixedContainerVisual catalogHandPrefab;
        // The local Hand, built from the catalog Hand entry for each session (C3b-1).
        private PrototypeFixedContainerVisual localHandVisual;
        private ConsoleView consoleView;
        private ConsoleLayoutData consoleLayout;
        private readonly List<ConsoleSlotView> consoleSlotViews = new List<ConsoleSlotView>();

        public bool IsInitialized { get; private set; }

        public bool IsGameTemplatesPanelVisible => gameTemplatesPanelVisible;

        public TabletopSession ActiveSession => activeSession;

        public MatchState MatchState => matchState;

        public TrapFloorFloorfallState FloorfallState => floorfallState;

        public TrapFloorFloormasterLifecycleState FloormasterLifecycleState => floormasterLifecycleState;

        public TrapFloorRoundState TrapFloorRoundState => trapFloorRoundState;

        public TrapFloorActivityFeedState TrapFloorActivityFeed => trapFloorActivityFeed;

        public event Action<IConsoleCardInteraction, ConsoleCardBehavior> ConsoleCardInteractionAccepted;

        public TrapFloorObjectiveState TrapFloorObjectiveState => trapFloorObjectiveState;

        public TrapFloorCollapseState TrapFloorCollapseState => trapFloorCollapseState;

        public TrapFloorTurnState TrapFloorTurnState => trapFloorTurnState;

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

        public HandView HandView => handView;

        public ConsoleView ConsoleView => consoleView;

        public IReadOnlyList<ConsoleSlotView> ConsoleSlotViews => consoleSlotViews.AsReadOnly();

        public IReadOnlyDictionary<ObjectDefinitionId, ButtonCardDefinition> ButtonDefinitions => buttonDefinitions;

        public ContainerId HandContainerId => handContainerId;

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
                throw new InvalidOperationException("An authoritative table session must be constructed before tabletop initialization.");
            }

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
                // Restore authoritative state and its authored Template/Mode metadata before any
                // Presentation validation asks the active definition for Console or Board data.
                RestorePrototypeTemplateContext(restoreInitialBaseline);
                ValidateTrapFloorConfiguration();
                presentationTransitions = new TabletopPresentationTransitionController();
                BuildRuntimeGraph();
                BuildToolboxRuntime();
                BuildFloorfallRuntime();
                BindObjectViews();
                BuildContainerViews();
                BindContainerViews();
                ProjectUnprojectedPlacedComponents();
                ConfigureTabletopCameraFraming();
                if (!rebuildingFromUndo) ProjectTrapFloorCameraBookmark();
                RefreshCardContentVisibility();
                ConfigureDropTargets();
                BuildInteractionGraph();
                inputFrameCoordinator.ConfigurePrototypeUiInput(
                    HandleSecondaryPointerPressed,
                    CanBeginCameraOrbit);
                prototypeUiInputConfiguredByComposition = true;
                ConfigureDeveloperControlsInputBlockIfNeeded();

                inputFrameCoordinator.ConfigureSelectionPresenter(selectionPresenter);
                inputFrameCoordinator.enabled = true;
                frameCoordinatorEnabledByComposition = true;

                if (!cameraInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator)
                    || !objectInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator))
                {
                    throw new InvalidOperationException("TabletopInputFrameCoordinator failed to attach both input adapters.");
                }

                selectionPresenter.Refresh();
                RefreshNeutralizedTrapPresentation();
                RefreshMovementAssistancePresentation();
                ShowMessage("Trap Floor tabletop foundation ready.");
                IsInitialized = true;
                BeginUndoTrackingForCurrentMatch();
            }
            catch
            {
                // Keep the selected session context available so the caller can reconstruct the
                // previous authoritative state if this Presentation build did not complete.
                Shutdown(true);
                throw;
            }
        }

        // Empty Table keeps its simple X/Z mapping but rests on the real Table top when one is active.
        // Falls back to the serialized tabletopHeight only when no Template Layout Origin surface is active.
        private float ResolveEmptyTableSurfaceHeight()
        {
            PhysicsScene physicsScene = targetCamera.gameObject.scene.GetPhysicsScene();
            foreach (PhysicalTabletopSurface surface in PhysicalTabletopSurface.Registered)
            {
                if (surface != null && surface.ParticipatesIn(physicsScene) && surface.IsTemplateLayoutOrigin)
                {
                    return PhysicalTabletopSurfaces.CreateTemplateLayoutConverter(
                            targetCamera,
                            worldUnitsPerTableUnit,
                            tabletopLayerHeight,
                            tabletopLocalOrderHeight)
                        .ToWorldPosition(new TableCoordinate(0d, 0d)).y;
                }
            }

            return tabletopHeight;
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
                    ResolveEmptyTableSurfaceHeight(),
                    tabletopLayerHeight,
                    tabletopLocalOrderHeight);
                matchState = restoreInitialBaseline
                    ? activeSession.Reset()
                    : activeSession.CurrentMatch;
                localPlayerId = activeSession.Request.RequestingPlayerId;
                cameraInputAdapter.CameraController.ClearFramingTargets();
                if (!rebuildingFromUndo) cameraInputAdapter.CameraController.ShowDefaultView();
                BuildToolboxRuntime();
                RebuildEmptyTableLooseObjectPresentation();
                BuildEmptyTableHand();
                ProjectUnprojectedPlacedComponents();

                BuildInteractionGraph();
                inputFrameCoordinator.ConfigurePrototypeUiInput(
                    HandleSecondaryPointerPressed,
                    CanBeginCameraOrbit);
                prototypeUiInputConfiguredByComposition = true;
                ConfigureDeveloperControlsInputBlockIfNeeded();
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
                BeginUndoTrackingForCurrentMatch();
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
            StopUndoTrackingCurrentMatch();
            physicalFloorfallPending = false;
            physicalFloorfallActor = PlayerId.Empty;
            physicalFloorCollapsePending = false;
            physicalFloorCollapseActor = PlayerId.Empty;
            physicalBlindDirectionPending = false;
            physicalBlindDirectionActor = PlayerId.Empty;
            physicalBlindDirectionDieId = TabletopObjectId.Empty;
            physicalAuthority?.Shutdown();
            physicalAuthority = null;
            if (cameraInputAdapter != null && cameraInputAdapter.CameraController != null)
            {
                cameraInputAdapter.CameraController.ClearFramingTargets();
            }
            SetGameBoardActive(false);
            ClearFeedback();
            floorfallTargetPresenter?.Clear();
            abilityTargetPresenter?.Clear();
            floorfallState?.Clear();

            if (frameCoordinatorEnabledByComposition && inputFrameCoordinator != null)
            {
                inputFrameCoordinator.enabled = false;
            }

            frameCoordinatorEnabledByComposition = false;

            CloseContextMenu();
            CloseCardInspect();

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

            // ConsoleView depends on its bound Slot Views while applying layout, so release the
            // parent binding before releasing the Slot bindings. Rebuild performs the inverse.
            consoleView?.Unbind();
            for (int i = 0; i < consoleSlotViews.Count; i++)
            {
                if (consoleSlotViews[i] != null && consoleSlotViews[i].IsBound)
                {
                    consoleSlotViews[i].Unbind();
                }
            }

            selectionState = null;
            hitResolver = null;
            pointerProjector = null;
            lockService = null;
            interactionStateMachine = null;
            previewSession = null;
            dropTargetResolver = null;
            tokenDropTargetResolver = null;
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
            ReleaseRuntimeDiscardPileInstances();
            ReleaseRuntimeConsoleInstances();
            ReleaseRuntimeTokenContainerInstances();
            ClearOfficialPawnPresentation();
            ReleaseRuntimeObjectInstances(runtimePawnInstances, pawnViews, pawnSelectionVisuals);
            ReleaseRuntimeObjectInstances(runtimeTokenInstances, tokenViews, tokenSelectionVisuals);
            ReleaseRuntimeObjectInstances(runtimeDieInstances, dieViews, dieSelectionVisuals);
            ReleaseRuntimeCardInstances();

            matchState = null;
            trapFloorTemplate = null;
            trapFloorActivityFeed?.Clear();
            trapFloorActivityFeed = null;
            trapFloorRevealFloorUseCase = null;
            trapFloorObjectiveState?.Clear();
            trapFloorObjectiveState = null;
            trapFloorObjectiveUseCase = null;
            trapFloorCollapseState?.Clear();
            trapFloorCollapseState = null;
            trapFloorCollapseUseCase = null;
            trapFloorTurnState = null;
            trapFloorTurnService = null;
            ConsoleCardInteractionAccepted -= HandleTrapFloorConsoleCardInteractionAccepted;
            trapFloorAbilityResolutionState = null;
            trapFloorAbilityResolutionService = null;
            trapFloorPendingSearchState?.Clear();
            trapFloorPendingSearchState = null;
            pendingControllerPurchaseState?.Clear();
            pendingControllerPurchaseState = null;
            pendingPurchaseDefinitionStableId = string.Empty;
            assistedHandCardIds.Clear();
            activeFloorRevealActivity = null;
            floorfallState = null;
            floorfallService = null;
            floorfallTargetPresenter = null;
            abilityTargetPresenter = null;
            floormasterLifecycleState = null;
            floormasterLifecycleService = null;
            trapFloorRoundState = null;
            trapFloorRoundOrchestrationService = null;
            authoritativeRandomValueSource = null;
            componentIdentitySource = null;
            componentCreationUseCase = null;
            cardBatchCreationUseCase = null;
            populateDeckUseCase = null;
            componentDeletionUseCase = null;
            componentDuplicationUseCase = null;
            playerLayout = null;
            localSeatLayout = null;
            centralPlayAreaId = PlayAreaId.Empty;
            localPlayerId = PlayerId.Empty;
            localPlayerLayoutSeatIndex = -1;
            interactionOwnerId = InteractionOwnerId.Empty;
            localSeatId = SeatId.Empty;
            handContainerId = ContainerId.Empty;
            primaryStackContainerId = ContainerId.Empty;
            sourceFeedbackContainerId = ContainerId.Empty;
            dynamicStackSequence = 0;
            cardState = null;
            pawnState = null;
            tokenState = null;
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
            handView = null;
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
            runtimeTokenContainerInstances.Clear();
            runtimeOwnedStackRoots.Clear();
            pawnViews.Clear();
            tokenViews.Clear();
            tokenContainerViews.Clear();
            dieViews.Clear();
            pawnSelectionVisuals.Clear();
            tokenSelectionVisuals.Clear();
            dieSelectionVisuals.Clear();
            controllerDeckViews.Clear();
            pileBays.Clear();
            pileBayOwnerSeats.Clear();
            playerConsoleViews.Clear();
            consoleSlotViews.Clear();
            layoutViews.Clear();
            labelsByCardId.Clear();
            buttonDefinitions.Clear();
            stackViewsByContainerId.Clear();
            feedbackTargetsByContainerId.Clear();
            consoleSlotVisualsByContainerId.Clear();
            feedbackHoldUntil = 0f;
            contextMenuMode = PrototypeContextMenuMode.None;
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuContainerId = ContainerId.Empty;
            contextMenuConsoleId = ConsoleId.Empty;
            contextMenuRenderedRevision = -1;
            inspectedCardId = TabletopObjectId.Empty;
            inspectedCardRenderedRevision = -1;
            selectedDrawCount = 1;
            selectedQuantity = 1;
            toolboxSpawnSequence = 0;
            toolboxPlacementHintActive = false;
            toolboxPlacementSubject = null;
            runtimeUi?.ClearActiveSessionTransientUi();
            gameTemplatesPanelVisible = false;
            runtimeUi?.HideGameTemplatesPanel();
            if (!preserveTemplateContext)
            {
                prototypeTemplateContext = null;
            }

            IsInitialized = false;
        }

        public ShuffleDeckResult ShuffleDeck(ContainerId targetDeckContainerId)
        {
            EnsureInitialized();
            if (!TryGetDeckPresentation(
                    targetDeckContainerId,
                    out DeckView targetDeckView,
                    out PrototypeFixedContainerVisual targetDeckVisual))
            {
                ShowMessage("Shuffle rejected: Deck Presentation unavailable.");
                return ShuffleDeckResult.Failure(
                    CommandResultStatus.Rejected,
                    ShuffleDeckError.ContainerMissing);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(targetDeckContainerId);
            ShuffleDeckResult result = new ShuffleDeckUseCase(authoritativeRandomValueSource).Execute(
                matchState,
                new ShuffleDeckCommand(CreateCommandContext(), targetDeckContainerId));
            if (result.Succeeded)
            {
                targetDeckView.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    settleDuration);
                presentationTransitions.Pulse(
                    targetDeckVisual.transform,
                    shuffleCompression,
                    feedbackDuration);
                ShowMessage("Deck shuffled.");
            }
            else
            {
                targetDeckView.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    returnDuration);
                ShowMessage($"Shuffle rejected: {result.Error}.");
            }

            RefreshCardContentVisibility();
            return result;
        }

        public ControllerInputHandDrawResult DrawUpToConfiguredHandLimit()
        {
            EnsureInitialized();
            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState capHand))
            {
                // Draw To Hand Limit fills the hand up to the player's hand cap; the game's own limit is
                // shown as advisory text in the DRAW popup.
                return DrawControllerCardsUpToComfortCap(player, capHand);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(player.ControllerDeckId, player.HandContainerId);
            ControllerInputHandDrawResult result = trapFloorTurnService.DrawForCurrentPlayer(
                matchState,
                CreateCommandContext(trapFloorTurnState.ActivePlayerId));
            ApplyLayout(player.ControllerDeckId);
            ApplyLayout(player.HandContainerId);
            presentationTransitions.AnimateCardsFromCurrentResults(
                transitionStarts,
                result.Succeeded ? handReflowDuration : returnDuration,
                0.035f);
            ShowMessage(result.Succeeded
                ? result.Changed
                    ? $"Drew {result.DrawnCount} Controller Card{(result.DrawnCount == 1 ? string.Empty : "s")}."
                    : "Controller Hand is already at its configured limit or the Deck is empty."
                : $"Controller draw rejected: {result.Error}.");
            return result;
        }

        public ActionAbilityPurchaseResult PurchaseActionOrAbility(string cardDefinitionStableId)
        {
            EnsureInitialized();
            if (pendingControllerPurchaseState == null
                || !pendingControllerPurchaseState.IsActive
                || !pendingControllerPurchaseState.IsPaymentComplete
                || pendingControllerPurchaseState.PurchasedCardDefinitionStableId
                    != cardDefinitionStableId)
            {
                ShowMessage("Purchase rejected: physical Console payment is incomplete.");
                return ActionAbilityPurchaseResult.Failure(
                    ActionAbilityPurchaseError.PhysicalPaymentMissing);
            }

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(player.ActionAbilityAreaContainerId);
            ActionAbilityPurchaseResult result =
                new ActionAbilityPurchaseService().ConfirmPhysicalPaymentPurchase(
                matchState,
                trapFloorTemplate.GameDefinition,
                new ConfirmPhysicalActionOrAbilityPurchaseCommand(
                    CreateCommandContext(pendingControllerPurchaseState.PlayerId),
                    player.SeatId,
                    player.ActionAbilityAreaContainerId,
                    cardDefinitionStableId,
                    TabletopObjectId.New(),
                    pendingControllerPurchaseState.SelectedCardIds,
                    player.SideSlotContainerIds));
            if (!result.Succeeded)
            {
                ApplyLayout(player.ActionAbilityAreaContainerId);
                presentationTransitions.AnimateCardsFromCurrentResults(transitionStarts, returnDuration);
                ShowMessage($"Purchase rejected: {result.Error}.");
                return result;
            }

            pendingControllerPurchaseState.Clear();
            ReplaceCurrentUndoStateForPurchaseAssistance();

            if (!trapFloorTemplate.GameDefinition.TryGetCard(
                    cardDefinitionStableId,
                    out CardDefinitionData purchasedDefinition))
            {
                throw new InvalidOperationException("Accepted purchase lost its authored Card Definition.");
            }

            labelsByCardId[result.GrantedCardId] = purchasedDefinition.DisplayName;
            CardView grantedView = CreateCardView(
                matchState.Cards[result.GrantedCardId],
                purchasedDefinition.DisplayName,
                out TabletopSelectionVisual selectionVisual);
            cardViews.Add(grantedView);
            cardSelectionVisuals.Add(selectionVisual);
            physicalAuthority?.Register(grantedView);
            RefreshContainerCardViewSources();
            ApplyLayout(player.ActionAbilityAreaContainerId);
            RefreshSelectionPresenterAfterRuntimeProjection();
            presentationTransitions.Appear(grantedView.transform, settleDuration);
            ShowMessage($"Purchased {purchasedDefinition.DisplayName}.");
            return result;
        }

        private DrawCardsResult DrawCards(ContainerId sourceDeckContainerId, int count)
        {
            EnsureInitialized();
            int requestedCount = count;
            count = ClampToHandComfortCap(handContainerId, count);
            if (count <= 0)
            {
                ShowMessage(handComfortSettings.ReachedMessage + ".");
                return DrawCardsResult.Failure(
                    CommandResultStatus.Rejected,
                    DrawCardsError.DestinationCapacityExceeded);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(sourceDeckContainerId, handContainerId);
            DrawCardsResult result = new DrawCardsUseCase().Execute(
                matchState,
                new DrawCardsCommand(CreateCommandContext(), sourceDeckContainerId, handContainerId, count));
            TryGetDeckPresentation(sourceDeckContainerId, out DeckView sourceDeckView, out _);
            if (result.Succeeded)
            {
                sourceDeckView?.ApplyAcceptedLayout();
                handView.ApplyAcceptedLayout();
                presentationTransitions.AnimateCardsFromCurrentResults(
                    transitionStarts,
                    handReflowDuration,
                    0.035f);
                ShowMessage(count < requestedCount
                    ? $"{handComfortSettings.ReachedMessage}: drew {count} of {requestedCount}."
                    : $"Drew {count} card{(count == 1 ? string.Empty : "s")} to Hand.");
            }
            else
            {
                sourceDeckView?.ApplyAcceptedLayout();
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
                    matchState.ContainerPlacements[newStackId],
                    true);
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
            MatchState previousMatch = activeSession.CurrentMatch;
            PrototypeSessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                // Reset returns the table to its start, Empty Table Hand switched on included (H-E).
                emptyTableHandEnabled = true;
                Shutdown(true);
                InitializeActiveSession(true);
                ShowActiveSessionUi();
            }
            catch (Exception exception)
            {
                RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Reset");
                Debug.LogError($"Reset Presentation rebuild failed: {exception.Message}", this);
                throw;
            }

            RefreshTrapFloorStatusUi();
        }

        public bool UndoLatestAction()
        {
            return UndoLatestAction(localPlayerId);
        }

        public bool UndoLatestAction(PlayerId requestingPlayerId)
        {
            if (!IsActiveSessionPlayer(requestingPlayerId)
                || !CanUndoCurrentAction()
                || !undoHistory.TryPeekUndo(
                    out PrototypeSessionUndoSnapshot snapshot,
                    out ActiveSessionUndoTransaction transaction))
            {
                return false;
            }

            Debug.Log(
                $"[Undo] Selecting state index {undoHistory.CurrentStateIndex - 1} "
                + $"from {undoHistory.TransactionCount} transactions: {transaction.Description}; "
                + $"objects={snapshot.Match.ObjectCount}; ids={FormatUndoObjectIds(snapshot.Match)}.",
                this);

            long undoRevision;
            try
            {
                undoRevision = checked(matchState.Revision + 1L);
            }
            catch (OverflowException)
            {
                ShowMessage("Undo rejected: Match revision cannot advance.");
                return false;
            }

            MatchState replacement;
            TrapFloorSessionState restoredTrapFloor = null;
            PendingControllerPurchaseState restoredPurchase = null;
            try
            {
                replacement = snapshot.Match.Restore(undoRevision);
                restoredTrapFloor = snapshot.TrapFloor?.Restore();
                restoredPurchase = snapshot.PendingControllerPurchase?.Restore();
            }
            catch (Exception exception)
            {
                ShowMessage($"Undo rejected: {exception.Message}");
                return false;
            }

            rebuildingFromUndo = true;
            MatchState previousMatch = activeSession.CurrentMatch;
            PrototypeSessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                Shutdown(true);
                activeSession.ReplaceCurrentMatch(replacement);
                pendingRestoredTrapFloorState = restoredTrapFloor;
                pendingRestoredControllerPurchaseState = restoredPurchase;
                InitializeActiveSession(false);
                undoHistory.CommitUndo();
                undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                Debug.Log(
                    $"[Undo] Restored state index {undoHistory.CurrentStateIndex}; "
                    + $"history states={undoHistory.CurrentStateIndex}/{undoHistory.TransactionCount}; "
                    + $"objects={matchState.ObjectCount}.",
                    this);
                ShowActiveSessionUi();
                ShowMessage($"Undid {transaction.Description}.");
                return true;
            }
            catch (Exception exception)
            {
                RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Undo");
                Debug.LogError($"Undo Presentation rebuild failed: {exception.Message}", this);
                throw;
            }
            finally
            {
                pendingRestoredTrapFloorState = null;
                pendingRestoredControllerPurchaseState = null;
                rebuildingFromUndo = false;
                RefreshUndoUi();
            }
        }

        public bool RedoLatestAction()
        {
            return RedoLatestAction(localPlayerId);
        }

        public bool RedoLatestAction(PlayerId requestingPlayerId)
        {
            if (!IsActiveSessionPlayer(requestingPlayerId)
                || !CanRedoCurrentAction()
                || !undoHistory.TryPeekRedo(
                    out PrototypeSessionUndoSnapshot snapshot,
                    out ActiveSessionUndoTransaction transaction))
            {
                return false;
            }

            long redoRevision;
            try
            {
                redoRevision = checked(matchState.Revision + 1L);
            }
            catch (OverflowException)
            {
                ShowMessage("Redo rejected: Match revision cannot advance.");
                return false;
            }

            MatchState replacement;
            TrapFloorSessionState restoredTrapFloor = null;
            PendingControllerPurchaseState restoredPurchase = null;
            try
            {
                replacement = snapshot.Match.Restore(redoRevision);
                restoredTrapFloor = snapshot.TrapFloor?.Restore();
                restoredPurchase = snapshot.PendingControllerPurchase?.Restore();
            }
            catch (Exception exception)
            {
                ShowMessage($"Redo rejected: {exception.Message}");
                return false;
            }

            rebuildingFromUndo = true;
            MatchState previousMatch = activeSession.CurrentMatch;
            PrototypeSessionUndoSnapshot previousSnapshot = CaptureUndoSnapshot();
            try
            {
                Shutdown(true);
                activeSession.ReplaceCurrentMatch(replacement);
                pendingRestoredTrapFloorState = restoredTrapFloor;
                pendingRestoredControllerPurchaseState = restoredPurchase;
                InitializeActiveSession(false);
                undoHistory.CommitRedo();
                undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                ShowActiveSessionUi();
                ShowMessage($"Redid {transaction.Description}.");
                return true;
            }
            catch (Exception exception)
            {
                RestorePresentationAfterFailedRebuild(previousMatch, previousSnapshot, "Redo");
                Debug.LogError($"Redo Presentation rebuild failed: {exception.Message}", this);
                throw;
            }
            finally
            {
                pendingRestoredTrapFloorState = null;
                pendingRestoredControllerPurchaseState = null;
                rebuildingFromUndo = false;
                RefreshUndoUi();
            }
        }

        private bool IsActiveSessionPlayer(PlayerId playerId)
        {
            if (playerId.IsEmpty || activeSession == null) return false;
            IReadOnlyList<PlayerId> players = activeSession.Request.ActivePlayerIds;
            for (int i = 0; i < players.Count; i++)
                if (players[i] == playerId) return true;
            return false;
        }

        private bool HandleUndoShortcut()
        {
            Keyboard keyboard = Keyboard.current;
            if (!IsInitialized
                || keyboard == null
                || !keyboard.zKey.wasPressedThisFrame
                || !(keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
                || keyboard.leftShiftKey.isPressed
                || keyboard.rightShiftKey.isPressed)
            {
                return false;
            }

            UndoLatestAction();
            return true;
        }

        private bool HandleRedoShortcut()
        {
            Keyboard keyboard = Keyboard.current;
            if (!IsInitialized || keyboard == null)
            {
                return false;
            }

            bool controlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool shiftPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            bool redoPressed = keyboard.yKey.wasPressedThisFrame
                || (shiftPressed && keyboard.zKey.wasPressedThisFrame);
            if (!controlPressed || !redoPressed)
            {
                return false;
            }

            RedoLatestAction();
            return true;
        }

        private bool CanUndoCurrentAction()
        {
            return IsInitialized
                && !rebuildingFromUndo
                && !undoTransactionInProgress
                && undoHistory.CanUndo;
        }

        private bool CanRedoCurrentAction()
        {
            return IsInitialized
                && !rebuildingFromUndo
                && !undoTransactionInProgress
                && undoHistory.CanRedo;
        }

        private void BeginUndoTrackingForCurrentMatch()
        {
            StopUndoTrackingCurrentMatch();
            undoTrackedMatch = matchState;
            undoTrackedMatch.AuthoritativeActionAccepted += HandleAuthoritativeActionAccepted;
            undoTransactionInProgress = false;
            if (!rebuildingFromUndo)
            {
                undoHistory.EstablishBaseline(CaptureUndoSnapshot());
            }
            RefreshUndoUi();
        }

        private void StopUndoTrackingCurrentMatch()
        {
            if (undoTrackedMatch != null)
            {
                undoTrackedMatch.AuthoritativeActionAccepted -= HandleAuthoritativeActionAccepted;
                undoTrackedMatch = null;
            }
            undoTransactionInProgress = false;
        }

        private void HandleAuthoritativeActionAccepted(AuthoritativeActionAcceptance acceptance)
        {
            if (acceptance.Kind == AuthoritativeActionKind.TransferCard)
            {
                RefreshAcceptedCardFaces();
            }

            if ((acceptance.Kind == AuthoritativeActionKind.MoveObject
                    || acceptance.Kind == AuthoritativeActionKind.PhysicalObjectSettled)
                && trapFloorAbilityResolutionService != null
                && trapFloorAbilityResolutionService.ClearMovementAssistanceIfPawnMoved(matchState))
            {
                abilityTargetPresenter?.ClearCurrent();
            }

            switch (acceptance.RecordMode)
            {
                case AuthoritativeActionRecordMode.Intermediate:
                    undoTransactionInProgress = true;
                    RefreshUndoUi();
                    break;
                case AuthoritativeActionRecordMode.Transaction:
                    PrototypeSessionUndoSnapshot beforeState = undoHistory.CurrentState;
                    PrototypeSessionUndoSnapshot afterState = CaptureUndoSnapshot();
                    undoHistory.RecordAccepted(
                        beforeState,
                        afterState,
                        acceptance,
                        DescribeUndoAction(acceptance));
                    Debug.Log(
                        $"[Undo] Recorded state index {undoHistory.CurrentStateIndex}; "
                        + $"transactions={undoHistory.TransactionCount}; "
                        + $"action={DescribeUndoAction(acceptance)}; "
                        + $"beforeObjects={beforeState.Match.ObjectCount}; "
                        + $"afterObjects={afterState.Match.ObjectCount}; "
                        + $"afterIds={FormatUndoObjectIds(afterState.Match)}.",
                        this);
                    undoTransactionInProgress = false;
                    RefreshUndoUi();
                    break;
                case AuthoritativeActionRecordMode.CancelTransaction:
                    undoTransactionInProgress = false;
                    undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                    RefreshUndoUi();
                    break;
                default:
                    if (!undoTransactionInProgress)
                    {
                        undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                        RefreshUndoUi();
                    }
                    break;
            }
        }

        private PrototypeSessionUndoSnapshot CaptureUndoSnapshot()
        {
            return PrototypeSessionUndoSnapshot.Capture(
                matchState,
                trapFloorActivityFeed,
                trapFloorObjectiveState,
                trapFloorCollapseState,
                trapFloorTurnState,
                trapFloorAbilityResolutionState,
                trapFloorPendingSearchState,
                pendingControllerPurchaseState);
        }

        private static string FormatUndoObjectIds(GameTemplateInitialSnapshot snapshot)
        {
            IReadOnlyList<TabletopObjectId> ids = snapshot.CopyObjectIds();
            if (ids.Count == 0) return "<none>";
            string result = ids[0].ToString();
            for (int i = 1; i < ids.Count; i++) result += $",{ids[i]}";
            return result;
        }

        private string DescribeUndoAction(AuthoritativeActionAcceptance acceptance)
        {
            string actor = FormatUndoActor(acceptance.ActorPlayerId);
            switch (acceptance.Kind)
            {
                case AuthoritativeActionKind.MoveObject: return $"{actor} moved an object";
                case AuthoritativeActionKind.MoveContainer: return $"{actor} moved a Container";
                case AuthoritativeActionKind.RotateObject: return $"{actor} rotated an object";
                case AuthoritativeActionKind.FlipCard: return $"{actor} flipped a Card";
                case AuthoritativeActionKind.TransferCard: return $"{actor} transferred a Card";
                case AuthoritativeActionKind.TransferToken: return $"{actor} transferred a Token";
                case AuthoritativeActionKind.ShuffleDeck: return $"{actor} shuffled a Deck";
                case AuthoritativeActionKind.DrawCards: return $"{actor} drew Cards";
                case AuthoritativeActionKind.ReorderContainer: return $"{actor} reordered a Container";
                case AuthoritativeActionKind.MergeStacks: return $"{actor} merged Stacks";
                case AuthoritativeActionKind.SplitStack: return $"{actor} split a Stack";
                case AuthoritativeActionKind.CreateComponent: return $"{actor} created a Component";
                case AuthoritativeActionKind.DuplicateComponent: return $"{actor} duplicated a Component";
                case AuthoritativeActionKind.DeleteComponent: return $"{actor} deleted a Component";
                case AuthoritativeActionKind.PopulateDeck: return $"{actor} populated a Deck";
                case AuthoritativeActionKind.PhysicalObjectSettled: return $"{actor} moved a physical object";
                case AuthoritativeActionKind.TrapFloorReveal: return $"{actor} searched a Floor";
                case AuthoritativeActionKind.TrapFloorClaimKey: return $"{actor} claimed a Key";
                case AuthoritativeActionKind.TrapFloorAttemptEscape: return $"{actor} attempted Escape";
                case AuthoritativeActionKind.TrapFloorCollapse: return $"{actor} collapsed a Floor";
                case AuthoritativeActionKind.PurchaseActionOrAbility: return $"{actor} purchased an Action/Ability Card";
                default:
                    return trapFloorTurnState == null
                        ? $"{actor} performed a table action"
                        : $"{actor} advanced the Trap Floor turn";
            }
        }

        private string FormatUndoActor(PlayerId playerId)
        {
            if (activeSession != null)
            {
                IReadOnlyList<PlayerId> players = activeSession.Request.ActivePlayerIds;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] == playerId) return $"P{i + 1}";
                }
            }
            return "Player";
        }

        private void RefreshUndoUi()
        {
            ActiveSessionUndoTransaction nextUndo = undoHistory.NextUndo;
            runtimeUi?.SetUndoState(
                CanUndoCurrentAction(),
                nextUndo == null ? "Undo" : $"Undo: {nextUndo.Description}");
            ActiveSessionUndoTransaction nextRedo = undoHistory.NextRedo;
            runtimeUi?.SetRedoState(
                CanRedoCurrentAction(),
                nextRedo == null ? "Redo" : $"Redo: {nextRedo.Description}");
        }

        public TrapFloorRoundActionResult CompleteTrapFloorStart()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null)
            {
                throw new InvalidOperationException("Round progression is available only in the active Trap Floor session.");
            }

            TrapFloorRoundActionResult result = trapFloorRoundOrchestrationService.CompleteStart(
                new TrapFloorRoundActionRequest(CreateCommandContext()));
            ShowMessage(result.Succeeded
                ? trapFloorRoundState.CurrentRoundNumber == 1
                    ? "Round 1 Start complete. Each participating Player may now Search once."
                    : $"Round {trapFloorRoundState.CurrentRoundNumber} started. Move your pawns by hand if the round asks for it."
                : $"Start progression rejected: {result.Error}.");
            return result;
        }

        public TrapFloorRoundFloorfallResult TriggerFloorfall()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null || floorfallTargetPresenter == null)
            {
                throw new InvalidOperationException("Floorfall is available only in the active Trap Floor session.");
            }

            TrapFloorRoundFloorfallResult result = trapFloorRoundOrchestrationService.RollFloorfall(
                new TrapFloorRoundActionRequest(CreateCommandContext(
                    physicalFloorfallActor.IsEmpty ? localPlayerId : physicalFloorfallActor)));
            if (!result.Succeeded)
            {
                ShowMessage($"Official Floorfall rejected: {result.Error}.");
                return result;
            }

            TrapFloorFloorfallTarget target = result.Target.Value;
            AnimateAcceptedDieResult(trapFloorTemplate.FloorfallXAxisDieId);
            AnimateAcceptedDieResult(trapFloorTemplate.FloorfallYAxisDieId);
            floorfallTargetPresenter.Show(target.FloorCardId);
            ShowMessage(
                $"Floorfall {trapFloorRoundState.AcceptedFloorfallCount}: X {target.XAxisRoll.Value}, Y {target.YAxisRoll.Value} -> {target.Coordinate}.");
            return result;
        }

        private bool BeginPhysicalFloorfall()
        {
            if (physicalFloorfallPending || trapFloorRoundState == null
                || trapFloorRoundState.Phase != TrapFloorRoundPhase.Floorfall
                || !TryGetDieView(trapFloorTemplate.FloorfallXAxisDieId, out DieView x)
                || !TryGetDieView(trapFloorTemplate.FloorfallYAxisDieId, out DieView y)
                || x.PhysicalObject == null || y.PhysicalObject == null
                || x.PhysicalObject.IsHeld || y.PhysicalObject.IsHeld) return false;
            if (physicalFloorfallActor.IsEmpty) physicalFloorfallActor = localPlayerId;
            if (!x.PhysicalObject.Roll(physicalFloorfallActor) || !y.PhysicalObject.Roll(physicalFloorfallActor))
            { physicalFloorfallActor = PlayerId.Empty; return false; }
            physicalFloorfallPending = true;
            ShowMessage("Floorfall Dice rolling physically; waiting for both to settle.");
            return true;
        }

        private void CompletePhysicalFloorfallIfSettled()
        {
            if (!physicalFloorfallPending) return;
            if (trapFloorRoundState == null || trapFloorRoundState.Phase != TrapFloorRoundPhase.Floorfall)
            { physicalFloorfallPending = false; physicalFloorfallActor = PlayerId.Empty; return; }
            DieState x = matchState.Dice[trapFloorTemplate.FloorfallXAxisDieId];
            DieState y = matchState.Dice[trapFloorTemplate.FloorfallYAxisDieId];
            if (x.BaseState.PhysicalState?.Mode != PhysicalObjectMode.Sleeping
                || y.BaseState.PhysicalState?.Mode != PhysicalObjectMode.Sleeping) return;
            physicalFloorfallPending = false;
            if (floorfallService.IsProtectedPhysicalResult(new TrapFloorFloorfallContext(trapFloorRoundState.CurrentRoundNumber)))
            { BeginPhysicalFloorfall(); return; }
            TriggerFloorfall();
            physicalFloorfallActor = PlayerId.Empty;
        }

        private bool BeginPhysicalFloorCollapse()
        {
            if (physicalFloorCollapsePending
                || trapFloorCollapseState == null
                || trapFloorCollapseUseCase == null
                || trapFloorTurnState == null
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.FloorTurn)
            {
                return false;
            }

            if (trapFloorCollapseState.IsBoardExhausted)
            {
                ShowMessage("Collapse Floor rejected: the Board has no usable Floors remaining.");
                return false;
            }

            if (!TryGetOfficialFloorfallDieViews(out DieView xAxisDieView, out DieView yAxisDieView))
            {
                ShowMessage("Collapse Floor rejected: the two official physical d6 are unavailable.");
                return false;
            }

            PlayerId actor = trapFloorTurnState.FloorOperatorPlayerId;
            if (!LaunchPhysicalFloorCollapseDice(xAxisDieView, yAxisDieView, actor))
            {
                ShowMessage("Collapse Floor rejected: both official d6 must be loose and available.");
                return false;
            }

            TrapFloorCollapseResult begin = trapFloorCollapseUseCase.Begin(
                matchState,
                new TrapFloorBeginCollapseCommand(CreateCommandContext(actor)));
            if (!begin.Succeeded)
            {
                ShowMessage($"Collapse Floor rejected: {begin.Error}.");
                return false;
            }

            physicalFloorCollapsePending = true;
            physicalFloorCollapseActor = actor;
            RefreshTrapFloorStatusUi();
            ShowMessage(
                $"{FormatPlayerName(actor)} triggered Floorfall; rolling the two official d6 physically.");
            return true;
        }

        private void CompletePhysicalFloorCollapseIfSettled()
        {
            if (!physicalFloorCollapsePending)
            {
                return;
            }

            if (trapFloorCollapseState == null
                || trapFloorCollapseUseCase == null
                || !trapFloorCollapseState.IsCollapsePending
                || !TryGetOfficialFloorfallDieViews(out DieView xAxisDieView, out DieView yAxisDieView))
            {
                physicalFloorCollapsePending = false;
                physicalFloorCollapseActor = PlayerId.Empty;
                return;
            }

            DieState xAxisDie = matchState.Dice[trapFloorTemplate.FloorfallXAxisDieId];
            DieState yAxisDie = matchState.Dice[trapFloorTemplate.FloorfallYAxisDieId];
            if (xAxisDie.BaseState.PhysicalState?.Mode != PhysicalObjectMode.Sleeping
                || yAxisDie.BaseState.PhysicalState?.Mode != PhysicalObjectMode.Sleeping
                || (ReferenceEquals(
                        xAxisDie.BaseState.PhysicalState,
                        trapFloorCollapseState.LastResolvedXAxisPhysicalState)
                    && ReferenceEquals(
                        yAxisDie.BaseState.PhysicalState,
                        trapFloorCollapseState.LastResolvedYAxisPhysicalState)))
            {
                return;
            }

            TrapFloorCollapseResult result = trapFloorCollapseUseCase.ResolveSettled(
                matchState,
                new TrapFloorResolveCollapseCommand(
                    CreateCommandContext(physicalFloorCollapseActor)));
            if (!result.Succeeded)
            {
                physicalFloorCollapsePending = false;
                physicalFloorCollapseActor = PlayerId.Empty;
                ShowMessage($"Collapse Floor rejected: {result.Error}.");
                return;
            }

            if (result.RerollRequired)
            {
                ShowMessage(
                    $"Floor {result.Roll.Coordinate} is already collapsed; rerolling both official d6.");
                if (!LaunchPhysicalFloorCollapseDice(
                        xAxisDieView,
                        yAxisDieView,
                        physicalFloorCollapseActor))
                {
                    physicalFloorCollapsePending = false;
                    physicalFloorCollapseActor = PlayerId.Empty;
                    ShowMessage("Automatic Floorfall reroll could not launch both official d6.");
                }

                RefreshTrapFloorStatusUi();
                return;
            }

            physicalFloorCollapsePending = false;
            physicalFloorCollapseActor = PlayerId.Empty;
            ApplyCollapsedFloorPresentation(result.CollapsedFloor.FloorCardId);
            selectionState?.ClearAll();
            selectionPresenter?.Refresh();
            RefreshTrapFloorStatusUi();
            ShowMessage(
                $"Floorfall rolled {result.Roll.XAxisResult}/{result.Roll.YAxisResult}; "
                + $"Floor {result.Roll.Coordinate} collapsed permanently."
                + (result.BoardExhausted ? " The Board is exhausted." : string.Empty));
        }

        private bool BeginPhysicalBlindDirection()
        {
            if (physicalBlindDirectionPending
                || trapFloorAbilityResolutionService == null
                || !TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                || blindStatus.IsDirectionResolved)
            {
                ShowMessage("Roll Blind Direction is unavailable.");
                return false;
            }

            if (!TryGetAvailableBlindD4(out TabletopObjectId dieId, out DieView dieView))
            {
                ShowMessage("Roll Blind Direction requires an available loose physical d4.");
                return false;
            }

            PlayerId actor = trapFloorTurnState.ActivePlayerId;
            if (!dieView.PhysicalObject.Roll(actor, true))
            {
                ShowMessage("Roll Blind Direction rejected: the d4 is unavailable or controlled.");
                return false;
            }

            physicalBlindDirectionPending = true;
            physicalBlindDirectionActor = actor;
            physicalBlindDirectionDieId = dieId;
            ShowMessage("BLIND — rolling physical d4; waiting for it to settle.");
            return true;
        }

        private void CompletePhysicalBlindDirectionIfSettled()
        {
            if (!physicalBlindDirectionPending) return;
            if (matchState == null
                || trapFloorAbilityResolutionService == null
                || !TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                || blindStatus.IsDirectionResolved
                || physicalBlindDirectionActor != trapFloorTurnState.ActivePlayerId
                || !matchState.Dice.TryGetValue(physicalBlindDirectionDieId, out DieState die))
            {
                CancelPhysicalBlindDirection("Blind direction roll cancelled because its active status changed.");
                return;
            }

            PhysicalObjectState physicalState = die.BaseState.PhysicalState;
            if (physicalState == null
                || (physicalState.Mode != PhysicalObjectMode.Sleeping
                    && physicalState.Mode != PhysicalObjectMode.SleepingUnresolved))
            {
                return;
            }

            if (physicalState.Mode == PhysicalObjectMode.SleepingUnresolved)
            {
                if (TryGetDieView(physicalBlindDirectionDieId, out DieView unresolvedDieView)
                    && unresolvedDieView.PhysicalObject != null
                    && unresolvedDieView.PhysicalObject.Roll(physicalBlindDirectionActor, true))
                {
                    ShowMessage("Blind d4 is cocked; rolling it again.");
                    return;
                }
                CancelPhysicalBlindDirection("Blind direction roll could not resolve the cocked d4.");
                return;
            }

            TrapFloorBlindDirectionRollResult result =
                trapFloorAbilityResolutionService.ResolveBlindDirection(
                    matchState,
                    CreateCommandContext(physicalBlindDirectionActor),
                    physicalBlindDirectionDieId);
            if (!result.Succeeded)
            {
                CancelPhysicalBlindDirection($"Blind direction rejected: {result.Error}.");
                return;
            }

            physicalBlindDirectionPending = false;
            physicalBlindDirectionActor = PlayerId.Empty;
            physicalBlindDirectionDieId = TabletopObjectId.Empty;
            RefreshTrapFloorStatusUi();
            ShowMessage($"BLIND DIRECTION: {result.Direction.ToString().ToUpperInvariant()}");
        }

        private void CancelPhysicalBlindDirection(string message)
        {
            PlayerId actor = physicalBlindDirectionActor;
            physicalBlindDirectionPending = false;
            physicalBlindDirectionActor = PlayerId.Empty;
            physicalBlindDirectionDieId = TabletopObjectId.Empty;
            if (undoTransactionInProgress && matchState != null && !actor.IsEmpty)
            {
                matchState.AdvanceRevision(
                    CommandId.New(),
                    actor,
                    AuthoritativeActionKind.Unspecified,
                    AuthoritativeActionRecordMode.CancelTransaction);
            }
            ShowMessage(message);
        }

        private bool TryGetAvailableBlindD4(
            out TabletopObjectId dieId,
            out DieView dieView)
        {
            if (matchState != null)
            {
                foreach (KeyValuePair<TabletopObjectId, DieState> pair in matchState.Dice)
                {
                    if (pair.Value.SideCount == 4
                        && TryGetDieView(pair.Key, out DieView candidate)
                        && candidate.PhysicalObject != null
                        && candidate.PhysicalObject.OwnsLooseTransform
                        && !candidate.PhysicalObject.IsHeld)
                    {
                        dieId = pair.Key;
                        dieView = candidate;
                        return true;
                    }
                }
            }

            dieId = TabletopObjectId.Empty;
            dieView = null;
            return false;
        }

        private bool TryGetOfficialFloorfallDieViews(
            out DieView xAxisDieView,
            out DieView yAxisDieView)
        {
            xAxisDieView = null;
            yAxisDieView = null;
            return trapFloorTemplate != null
                && TryGetDieView(trapFloorTemplate.FloorfallXAxisDieId, out xAxisDieView)
                && TryGetDieView(trapFloorTemplate.FloorfallYAxisDieId, out yAxisDieView)
                && xAxisDieView.PhysicalObject != null
                && yAxisDieView.PhysicalObject != null
                && !xAxisDieView.PhysicalObject.IsHeld
                && !yAxisDieView.PhysicalObject.IsHeld
                && xAxisDieView.PhysicalObject.OwnsLooseTransform
                && yAxisDieView.PhysicalObject.OwnsLooseTransform
                && !xAxisDieView.BoundState.IsUserLocked
                && !yAxisDieView.BoundState.IsUserLocked;
        }

        private static bool LaunchPhysicalFloorCollapseDice(
            DieView xAxisDieView,
            DieView yAxisDieView,
            PlayerId actor)
        {
            return xAxisDieView.PhysicalObject.Roll(actor, true)
                && yAxisDieView.PhysicalObject.Roll(actor, true);
        }

        public TrapFloorRoundSearchResult SearchFloormasterDeck()
        {
            return SearchFloormasterDeck(localPlayerId);
        }

        public TrapFloorRoundSearchResult SearchFloormasterDeck(PlayerId searchingPlayerId)
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null || floormasterLifecycleState == null)
            {
                throw new InvalidOperationException("Floormaster Search is available only in the active Trap Floor session.");
            }

            TrapFloorRoundSearchResult result = trapFloorRoundOrchestrationService.Search(
                new TrapFloorFloormasterSearchRequest(CreateCommandContext(searchingPlayerId)));
            if (!result.Succeeded)
            {
                string detail = result.Error == TrapFloorRoundOrchestrationError.FloormasterLifecycleRejected
                    ? result.LifecycleError.ToString()
                    : result.Error.ToString();
                ShowMessage($"Floormaster Search rejected: {detail}.");
                return result;
            }

            ApplyLayout(trapFloorTemplate.FloormasterDeckId);
            if (result.ReshuffledDiscard)
            {
                ApplyLayout(trapFloorTemplate.FloormasterDiscardId);
            }

            CardView pendingCardView = FindCardView(result.PendingCard.CardId);
            pendingCardView.ApplyAcceptedState();
            RefreshCardContentVisibility();
            string reshuffleStatus = result.ReshuffledDiscard ? " Discard reshuffled first." : string.Empty;
            ShowMessage(
                $"{FormatPlayerName(result.PendingCard.SearchingPlayerId)} searched: {result.PendingCard.Category} pending.{reshuffleStatus}");
            return result;
        }

        public TrapFloorRoundTriggerResult CompletePendingFloormasterTriggerPrototype()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null || floormasterLifecycleState == null)
            {
                throw new InvalidOperationException("Floormaster Trigger completion is available only in the active Trap Floor session.");
            }

            TrapFloorPendingFloormasterCard pendingCard = floormasterLifecycleState.PendingCard;
            if (pendingCard == null)
            {
                ShowMessage("There is no searched card waiting to be resolved.");
                return TrapFloorRoundTriggerResult.Failure(
                    CommandResultStatus.Rejected,
                    TrapFloorRoundOrchestrationError.PendingCardStateMismatch);
            }

            TrapFloorRoundTriggerResult result = trapFloorRoundOrchestrationService.CompleteTrigger(
                new CompletePendingFloormasterCardRequest(CreateCommandContext(), pendingCard.CardId));
            if (!result.Succeeded)
            {
                string detail = result.Error == TrapFloorRoundOrchestrationError.FloormasterLifecycleRejected
                    ? result.LifecycleError.ToString()
                    : result.Error.ToString();
                ShowMessage($"The searched card could not be marked done: {detail}.");
                return result;
            }

            ApplyLayout(trapFloorTemplate.FloormasterDiscardId);
            RefreshCardContentVisibility();
            ShowMessage("Searched card discarded. Its effect is carried out by the players at the table.");
            return result;
        }

        public TrapFloorRoundActionResult CompleteFloorfallPhasePrototype()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null)
            {
                throw new InvalidOperationException("Floorfall phase progression is available only in the active Trap Floor session.");
            }

            TrapFloorRoundActionResult result = trapFloorRoundOrchestrationService.CompleteFloorfallPhase(
                new TrapFloorRoundActionRequest(CreateCommandContext()));
            ShowMessage(result.Succeeded
                ? "Floorfall done. Check together that your mode's Floorfalls were all rolled."
                : $"Floorfall phase completion rejected: {result.Error}.");
            return result;
        }

        public TrapFloorRoundActionResult CompleteEndPrototype()
        {
            EnsureInitialized();
            if (trapFloorRoundOrchestrationService == null)
            {
                throw new InvalidOperationException("End phase progression is available only in the active Trap Floor session.");
            }

            int completedRound = trapFloorRoundState.CurrentRoundNumber;
            TrapFloorRoundActionResult result = trapFloorRoundOrchestrationService.CompleteEnd(
                new TrapFloorRoundActionRequest(CreateCommandContext()));
            if (!result.Succeeded)
            {
                ShowMessage($"End completion rejected: {result.Error}.");
            }
            else if (trapFloorRoundState.IsScheduleCompleted)
            {
                ShowMessage("Round 10 schedule complete. Win/loss remains unresolved.");
            }
            else
            {
                ShowMessage($"Round {completedRound} End acknowledged. Round {trapFloorRoundState.CurrentRoundNumber} begins at Start.");
            }

            return result;
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
            if (IsInitialized)
            {
                return;
            }

            try
            {
                InitializeRuntimeUi();
                PrepareTemplateCatalog();
                if (!TryReplaceTable(TabletopSessionSelection.EmptyCustom))
                {
                    throw new InvalidOperationException(
                        gameTemplatesPanelError ?? "Empty Table startup failed.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"TabletopPrototypeComposition failed to start an Empty Table: {exception.Message}",
                    this);
                gameTemplatesPanelError = exception.Message;
            }
        }

        private void OnDestroy()
        {
            Shutdown();
            if (handTrayRig != null)
            {
                Destroy(handTrayRig.gameObject);
                handTrayRig = null;
            }

            if (runtimeUi != null)
            {
                runtimeUi.ReleaseBindings();
            }

            activeSession = null;
            sessionTemplateCatalog = null;
            availableTrapFloorTemplates.Clear();
        }

        private void Update()
        {
            if (HandleRedoShortcut()) return;
            if (HandleUndoShortcut()) return;
            HandleHandTrayShortcut();
            physicalAuthority?.Tick();
            CompletePhysicalFloorfallIfSettled();
            CompletePhysicalFloorCollapseIfSettled();
            CompletePhysicalBlindDirectionIfSettled();
            presentationTransitions?.Tick(Time.unscaledDeltaTime);
            RefreshHandInteractionPresentation();
            RefreshHandTrayDropTarget();
            RefreshCardContentVisibility();
            if (feedbackHoldUntil > 0f && Time.unscaledTime >= feedbackHoldUntil)
            {
                ClearFeedback();
            }

            RefreshRuntimeStatusUi();
            RefreshTrapFloorStatusUi();
            RefreshToolboxPlacementUi();
            RefreshOpenTabletopPopup();
            RefreshCardInspectPopup();
        }

        // The tray band is a drop target only while a card drag is in progress.
        private void RefreshHandTrayDropTarget()
        {
            if (handTrayRig == null || handView == null || !handView.IsTrayMode)
            {
                return;
            }

            bool cardDragActive = IsCardDragActive();
            handTrayRig.SetDropTargetActive(cardDragActive);
        }

        private bool IsCardDragActive()
        {
            return interactionStateMachine != null
                && interactionStateMachine.Phase == TabletopInteractionPhase.DraggingObject
                && ((containedCardDragCoordinator != null && containedCardDragCoordinator.ActiveCardView != null)
                    || (moveCoordinator != null && moveCoordinator.ActiveView is CardView));
        }

        // The pile style the active template declares for a Deck or Stack (null: not declared, or no style).
        private GameTemplatePileStyle ResolveTemplatePileStyle(ContainerId containerId)
        {
            if (trapFloorTemplate == null)
            {
                return null;
            }

            IReadOnlyList<GameTemplateContainerDefinition> definitions = trapFloorTemplate.Template.Containers;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].Id == containerId)
                {
                    return definitions[i].PileStyle;
                }
            }

            return null;
        }

        // A pile in its bay (doc 21 principle 17), the same whether a template or the Toolbox placed it: no
        // plate, labels kept, and the root drop box is a trigger so the pile never blocks loose pieces; drops
        // and right-clicks still resolve through it. The mouth faces the owner seat's Console once the
        // Consoles are bound.
        private void ConfigurePileBay(
            PrototypeFixedContainerVisual visual,
            GameTemplatePileStyle pileStyle,
            SeatId ownerSeatId)
        {
            visual.TargetCollider.isTrigger = true;
            ConfigureBayFootprint(visual, pileStyle.BayMark);
            pileBays.Add(visual.Bay);
            pileBayOwnerSeats.Add(ownerSeatId);
        }

        // The bay's outer size is the pile footprint the placement uses (ConsoleAdjacentPlacementSettings).
        private static void ConfigureBayFootprint(PrototypeFixedContainerVisual visual, GameTemplateBayMark mark)
        {
            if (visual.Bay == null)
            {
                throw new InvalidOperationException($"{visual.name} requires a Bay: pile prefabs come from the catalog.");
            }

            ConsoleAdjacentPlacementSettings footprint = ConsoleAdjacentPlacementSettings.Standard;
            visual.Bay.Configure((float)footprint.BayWidth, (float)footprint.BayDepth, mark);
        }

        // Points each owned pile's bay mouth at its owner seat's Console (call after the Consoles are bound).
        private void AssignPileBayMouthTargets()
        {
            for (int i = 0; i < pileBays.Count; i++)
            {
                if (pileBays[i] == null || pileBayOwnerSeats[i].IsEmpty)
                {
                    continue;
                }

                for (int c = 0; c < playerConsoleViews.Count; c++)
                {
                    ConsoleView console = playerConsoleViews[c];
                    if (console != null
                        && console.IsBound
                        && console.ConsoleState.OwnerSeatId == pileBayOwnerSeats[i])
                    {
                        pileBays[i].SetMouthTarget(console.transform);
                        break;
                    }
                }
            }
        }

        private void HandleHandTrayShortcut()
        {
            Keyboard keyboard = Keyboard.current;
            if (!IsInitialized
                || keyboard == null
                || !keyboard.hKey.wasPressedThisFrame
                || keyboard.leftCtrlKey.isPressed
                || keyboard.rightCtrlKey.isPressed)
            {
                return;
            }

            ToggleHandTray();
        }

        private void ToggleHandTray()
        {
            if (handTrayRig == null || handView == null || !handView.IsTrayMode)
            {
                return;
            }

            handTrayRig.SetCollapsed(!handTrayRig.IsCollapsed);
            ShowMessage(handTrayRig.IsCollapsed ? "Hand collapsed (H to show)." : "Hand shown.");
        }

        private List<PrototypePopupActionOption> AddHandTrayToggleAction(
            List<PrototypePopupActionOption> actions)
        {
            if (handTrayRig != null && handView != null && handView.IsTrayMode)
            {
                if (toggleHandTrayAction == null)
                {
                    toggleHandTrayAction = ToggleHandTray;
                }

                actions.Add(new PrototypePopupActionOption(
                    handTrayRig.IsCollapsed ? "Show Hand (H)" : "Hide Hand (H)",
                    true,
                    toggleHandTrayAction));
            }

            return actions;
        }

        private void RefreshHandInteractionPresentation()
        {
            if (handView == null || !handView.IsBound) return;
            TabletopObjectId hoveredCardId = selectionState != null
                ? selectionState.HoveredObjectId
                : TabletopObjectId.Empty;
            TabletopObjectId selectedCardId = selectionState != null
                ? selectionState.SelectedObjectId
                : TabletopObjectId.Empty;
            CardView draggedCard = containedCardDragCoordinator?.ActiveCardView;
            assistedHandCardIds.Clear();
            if (trapFloorPendingSearchState != null && trapFloorPendingSearchState.IsActive)
            {
                AddUniqueCardIds(assistedHandCardIds, trapFloorPendingSearchState.SelectedCardIds);
            }
            if (pendingControllerPurchaseState != null
                && pendingControllerPurchaseState.IsActive
                && !pendingControllerPurchaseState.IsPaymentComplete)
            {
                AddUniqueCardIds(
                    assistedHandCardIds,
                    pendingControllerPurchaseState.SelectedCardIds);
            }
            handView.SetInteractionState(
                hoveredCardId,
                selectedCardId,
                draggedCard != null ? draggedCard.ObjectId : TabletopObjectId.Empty,
                assistedHandCardIds.Count > 0 ? assistedHandCardIds : null);
        }

        private static void AddUniqueCardIds(
            List<TabletopObjectId> destination,
            IReadOnlyList<TabletopObjectId> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (!destination.Contains(source[i])) destination.Add(source[i]);
            }
        }

        private void OnGUI()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (HasDeveloperControls())
            {
                DrawDeveloperControls();
            }
        }

        private bool HasDeveloperControls()
        {
            return showDeveloperControls
                && activeSession != null
                && activeSession.Selection.Kind == TabletopSessionKind.GameTemplate;
        }

        private void ConfigureDeveloperControlsInputBlockIfNeeded()
        {
            if (!HasDeveloperControls())
            {
                return;
            }

            inputFrameCoordinator.ConfigureObjectInputBlockingGuiRect(ControlsPanelScreenRect);
            controlsPanelInputBlockConfiguredByComposition = true;
        }

        private void DrawDeveloperControls()
        {
            GUILayout.BeginArea(ControlsPanelScreenRect, GUI.skin.box);
            GUILayout.Label("Developer Controls");
            GUILayout.BeginHorizontal();
            GUILayout.Label(handComfortSettings.CapLabel);
            if (GUILayout.Button("-"))
            {
                handComfortSettings.MaxHandCards -= 1;
            }

            if (GUILayout.Button("+"))
            {
                handComfortSettings.MaxHandCards += 1;
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
            if (GUILayout.Button("Split Selected/Primary Stack"))
            {
                SplitSelectedOrPrimaryStack();
            }

            GUILayout.Space(6f);
            GUILayout.EndArea();
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
            ShowPlacementHint(
                componentKind == TabletopComponentKind.Die
                    ? $"d{dieSideCount}"
                    : ToolboxComponentLabel(componentKind));
        }

        private static string ToolboxComponentLabel(TabletopComponentKind componentKind)
        {
            return componentKind == TabletopComponentKind.DiscardPile ? "Discard Pile" : componentKind.ToString();
        }

        // A Toolbox pile takes its arrival face from the system pile style (doc 22, C2b): a Discard Pile turns
        // arriving Cards face down; a Deck or Stack keeps their face.
        private static ContainerArrivalFace ToolboxArrivalFace(TabletopComponentKind componentKind)
        {
            switch (componentKind)
            {
                case TabletopComponentKind.Deck:
                    return GameTemplatePileStyle.DefaultFor(ContainerKind.Deck).ArrivalFace;
                case TabletopComponentKind.Stack:
                    return GameTemplatePileStyle.DefaultFor(ContainerKind.Stack).ArrivalFace;
                case TabletopComponentKind.DiscardPile:
                    return GameTemplatePileStyle.DefaultFor(ContainerKind.DiscardPile).ArrivalFace;
                default:
                    return ContainerArrivalFace.Unchanged;
            }
        }

        // A Toolbox tile was picked (doc 22, C2a). Cards: 1 places a loose card, 2 or more place one Deck of that
        // many face-down cards. Dice: the value is the chosen size. Everything else places its component.
        private void PlaceCatalogEntry(ComponentCatalogEntry entry, int value)
        {
            EnsureInitialized();
            CloseContextMenu();
            toolboxPlacementIcon = entry.Icon;
            switch (entry.Kind)
            {
                case ComponentCatalogKind.Card:
                    if (value <= 1)
                    {
                        BeginCardBatchPlacement(1);
                    }
                    else
                    {
                        BeginDeckOfCardsPlacement(Mathf.Min(value, CreateTabletopComponentUseCase.MaximumDeckCardCount));
                    }

                    break;
                case ComponentCatalogKind.Deck:
                    BeginToolboxPlacement(TabletopComponentKind.Deck);
                    break;
                case ComponentCatalogKind.Stack:
                    BeginToolboxPlacement(TabletopComponentKind.Stack);
                    break;
                case ComponentCatalogKind.DiscardPile:
                    BeginToolboxPlacement(TabletopComponentKind.DiscardPile);
                    break;
                case ComponentCatalogKind.Pawn:
                    BeginToolboxPlacement(TabletopComponentKind.Pawn);
                    break;
                case ComponentCatalogKind.Token:
                    BeginToolboxPlacement(TabletopComponentKind.Token);
                    break;
                case ComponentCatalogKind.Console:
                    BeginToolboxPlacement(TabletopComponentKind.Console);
                    break;
                case ComponentCatalogKind.Die:
                    BeginToolboxPlacement(TabletopComponentKind.Die, value);
                    break;
                default:
                    toolboxPlacementIcon = null;
                    ShowMessage($"{entry.DisplayName} cannot be placed from the Toolbox yet.");
                    break;
            }
        }

        private void BeginDeckOfCardsPlacement(int quantity)
        {
            GameObject previewRoot = CreateDeckOfCardsPlacementPreview(quantity);
            float rotation = localSeatLayout != null
                ? localSeatLayout.PlayerZonePose.RotationDegrees
                : 0f;
            componentPlacementController.BeginCustomComponentPlacement(
                TabletopComponentKind.Deck,
                previewRoot,
                rotation,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose => CommitDeckOfCardsPlacement(quantity, pose));
            ShowPlacementHint($"Deck of {quantity} cards");
        }

        // One command: the Deck and its face-down cards, so one Undo removes both.
        private bool CommitDeckOfCardsPlacement(int quantity, TabletopPose requestedPose)
        {
            CreateTabletopComponentResult result = componentCreationUseCase.Execute(
                matchState,
                activeSession.Request.ActivePlayerIds,
                new CreateTabletopComponentRequest(
                    CreateCommandContext(),
                    TabletopComponentKind.Deck,
                    requestedPose,
                    0,
                    CardFace.FaceUp,
                    AuthoritativeActionKind.CreateComponent,
                    quantity));
            if (!result.Succeeded)
            {
                ShowMessage($"Add Deck rejected: {result.Error}.");
                return false;
            }

            toolboxSpawnSequence++;
            // The new cards need their Views before the Deck View binds (it resolves every member card).
            for (int i = 0; i < result.CardIds.Count; i++)
            {
                CardView view = CreateCardView(
                    matchState.Cards[result.CardIds[i]],
                    "CARD",
                    out TabletopSelectionVisual selectionVisual);
                cardViews.Add(view);
                cardSelectionVisuals.Add(selectionVisual);
            }

            RefreshContainerCardViewSources();
            ProjectCreatedToolboxComponent(result);
            RefreshContainerCardViewSources();
            RefreshSelectionPresenterAfterRuntimeProjection();
            RefreshCardContentVisibility();
            Physics.SyncTransforms();
            ShowMessage($"Added a deck of {quantity} cards.");
            return true;
        }

        // The Deck ghost (its bay with the Draw mark) with up to 12 card ghosts stacked inside it.
        private GameObject CreateDeckOfCardsPlacementPreview(int quantity)
        {
            GameObject previewRoot = CreateToolboxPlacementPreview(TabletopComponentKind.Deck, 0);
            int shown = Mathf.Min(quantity, 12);
            for (int i = 0; i < shown; i++)
            {
                PrototypeCardVisualReferences preview = Instantiate(catalogCardPrefab, previewRoot.transform, false);
                preview.ValidateReferences();
                preview.FrontLabel.gameObject.SetActive(false);
                preview.BackLabel.gameObject.SetActive(false);
                preview.transform.localPosition = new Vector3(0f, 0.03f + (i * 0.02f), 0f);
                preview.transform.localRotation = Quaternion.identity;
            }

            DisablePlacementPreviewInteraction(previewRoot);
            TintPlacementPreview(previewRoot);
            return previewRoot;
        }

        private void OpenCardQuantityPopup()
        {
            EnsureInitialized();
            CloseContextMenu();
            selectedQuantity = Mathf.Clamp(
                selectedQuantity,
                1,
                GenericCardBatchLayout.MaximumLooseBatchQuantity);
            runtimeUi.ShowQuantityPopup(
                "CREATE CARDS",
                "Choose how many generic Cards to place as one visible batch.",
                "Place Cards",
                selectedQuantity,
                1,
                GenericCardBatchLayout.MaximumLooseBatchQuantity,
                () => ChangeSelectedQuantity(-1, GenericCardBatchLayout.MaximumLooseBatchQuantity),
                () => ChangeSelectedQuantity(1, GenericCardBatchLayout.MaximumLooseBatchQuantity),
                ConfirmCardQuantity,
                CloseContextMenu);
        }

        private void ChangeSelectedQuantity(int delta, int maximum)
        {
            selectedQuantity = Mathf.Clamp(selectedQuantity + delta, 1, maximum);
            runtimeUi?.SetQuantityPopupValue(selectedQuantity, 1, maximum);
        }

        private void ConfirmCardQuantity()
        {
            int quantity = Mathf.Clamp(
                selectedQuantity,
                1,
                GenericCardBatchLayout.MaximumLooseBatchQuantity);
            runtimeUi?.CloseTabletopPopup();
            BeginCardBatchPlacement(quantity);
        }

        private void BeginCardBatchPlacement(int quantity)
        {
            GameObject previewRoot = CreateCardBatchPlacementPreview(quantity);
            float rotation = localSeatLayout != null
                ? localSeatLayout.PlayerZonePose.RotationDegrees
                : 0f;
            componentPlacementController.BeginCustomComponentPlacement(
                TabletopComponentKind.Card,
                previewRoot,
                rotation,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose => CommitCardBatchPlacement(quantity, pose));
            componentPlacementController.PhysicalQuantity = quantity;
            ShowPlacementHint(quantity == 1 ? "Card" : $"{quantity} Cards");
        }

        private bool CommitCardBatchPlacement(int quantity, TabletopPose requestedPose)
        {
            CreateGenericCardBatchResult result = cardBatchCreationUseCase.Execute(
                matchState,
                activeSession.Request.ActivePlayerIds,
                new CreateGenericCardBatchRequest(
                    CreateCommandContext(),
                    quantity,
                    requestedPose));
            if (!result.Succeeded)
            {
                ShowMessage($"Add Cards rejected: {result.Error}.");
                return false;
            }

            toolboxSpawnSequence++;
            ProjectCreatedCardBatch(result.CardIds);
            ShowMessage(quantity == 1 ? "Added Card." : $"Added {quantity} Cards.");
            return true;
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
                    dieSideCount,
                    containerArrivalFace: ToolboxArrivalFace(componentKind)));
            if (!result.Succeeded)
            {
                ShowMessage($"Add {ToolboxComponentLabel(componentKind)} rejected: {result.Error}.");
                return result;
            }

            toolboxSpawnSequence++;
            ProjectCreatedToolboxComponent(result);
            ShowMessage(componentKind == TabletopComponentKind.Die
                ? $"Added d{dieSideCount}."
                : $"Added {ToolboxComponentLabel(componentKind)}.");
            return result;
        }

        private bool CommitToolboxPlacement(
            TabletopComponentKind componentKind,
            int dieSideCount,
            TabletopPose requestedPose)
        {
            return AddToolboxComponentAtPose(componentKind, dieSideCount, requestedPose).Succeeded;
        }

        private void BeginDuplicatePlacement(TabletopObjectId sourceObjectId)
        {
            EnsureInitialized();
            if (componentPlacementController == null)
            {
                throw new InvalidOperationException("Component placement is not configured.");
            }

            if (sourceObjectId.IsEmpty || !matchState.ContainsObject(sourceObjectId))
            {
                ShowMessage("Duplicate rejected: SourceMissing.");
                CloseContextMenu();
                return;
            }

            TabletopObjectState sourceState = matchState.GetObject(sourceObjectId);
            if (!sourceState.ContainerId.IsEmpty)
            {
                ShowMessage("Duplicate rejected: SourceMustBeLoose.");
                CloseContextMenu();
                return;
            }

            if (!TryResolveDuplicatedComponent(
                    sourceObjectId,
                    sourceState,
                    out TabletopComponentKind componentKind,
                    out int dieSideCount))
            {
                ShowMessage("Duplicate rejected: SourceKindUnsupported.");
                CloseContextMenu();
                return;
            }

            GameObject previewRoot = CreateToolboxPlacementPreview(componentKind, dieSideCount);
            CloseContextMenu();
            componentPlacementController.BeginCustomComponentPlacement(
                componentKind,
                previewRoot,
                sourceState.Pose.RotationDegrees,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose => CommitDuplicatePlacement(sourceObjectId, pose));
            string subject = componentKind == TabletopComponentKind.Die
                ? $"Duplicate d{dieSideCount}"
                : $"Duplicate {componentKind}";
            ShowPlacementHint(subject);
        }

        private bool CommitDuplicatePlacement(
            TabletopObjectId sourceObjectId,
            TabletopPose requestedPose)
        {
            DuplicateTabletopComponentResult result = componentDuplicationUseCase.Execute(
                matchState,
                activeSession.Request.ActivePlayerIds,
                new DuplicateTabletopComponentRequest(
                    CreateCommandContext(),
                    sourceObjectId,
                    requestedPose));
            if (!result.Succeeded)
            {
                ShowMessage($"Duplicate rejected: {result.Error}.");
                return false;
            }

            toolboxSpawnSequence++;
            ProjectCreatedToolboxComponent(result.CreationResult);
            ShowMessage($"Duplicated {result.CreationResult.ComponentKind} as a generic Component.");
            return true;
        }

        private bool TryResolveDuplicatedComponent(
            TabletopObjectId sourceObjectId,
            TabletopObjectState sourceState,
            out TabletopComponentKind componentKind,
            out int dieSideCount)
        {
            dieSideCount = 0;
            switch (sourceState.Kind)
            {
                case TabletopObjectKind.Card:
                    componentKind = TabletopComponentKind.Card;
                    return true;
                case TabletopObjectKind.Pawn:
                    componentKind = TabletopComponentKind.Pawn;
                    return true;
                case TabletopObjectKind.Token:
                    componentKind = TabletopComponentKind.Token;
                    return true;
                case TabletopObjectKind.Die:
                    componentKind = TabletopComponentKind.Die;
                    dieSideCount = matchState.Dice[sourceObjectId].SideCount;
                    return true;
                default:
                    componentKind = default;
                    return false;
            }
        }

        private void BeginContainerMove(ContainerId containerId)
        {
            EnsureInitialized();
            if (componentPlacementController == null)
            {
                throw new InvalidOperationException("Component placement is not configured.");
            }

            if (!matchState.Containers.TryGetValue(containerId, out ContainerState container)
                || !matchState.TryGetContainerPlacement(containerId, out ContainerPlacementState placement)
                || (container.Kind != ContainerKind.Deck
                    && container.Kind != ContainerKind.Stack
                    && container.Kind != ContainerKind.DiscardPile))
            {
                ShowMessage("Container move rejected: Container unavailable.");
                return;
            }

            if (MoveContainerUseCase.IsMoveBlockedByOwner(matchState, container, localPlayerId))
            {
                ShowMessage($"Container move rejected: {MoveContainerError.NotContainerOwner}.");
                return;
            }

            TabletopComponentKind previewKind = container.Kind == ContainerKind.Deck
                ? TabletopComponentKind.Deck
                : container.Kind == ContainerKind.DiscardPile
                    ? TabletopComponentKind.DiscardPile
                    : TabletopComponentKind.Stack;
            if (!TryResolveContainerMoveRoot(containerId, container.Kind, out GameObject sourceRoot))
            {
                ShowMessage("Container move rejected: bound View unavailable.");
                return;
            }

            componentPlacementController.Cancel();
            GameObject previewRoot = CreateToolboxPlacementPreview(previewKind, 0);
            List<GameObjectActivationSnapshot> sourceVisibility = HideContainerMoveSource(
                sourceRoot,
                new[] { containerId });
            CloseContextMenu();
            try
            {
                componentPlacementController.BeginContainerMove(
                    previewRoot,
                    placement.Pose,
                    pose => CommitContainerMove(containerId, pose),
                    committed => CompleteContainerMovePresentation(
                        containerId,
                        false,
                        committed,
                        sourceVisibility));
            }
            catch
            {
                RestoreMoveSourceVisibility(sourceVisibility);
                previewRoot.SetActive(false);
                Destroy(previewRoot);
                throw;
            }

            ShowPlacementHint($"Move {ToolboxComponentLabel(previewKind)}");
            ShowMessage($"Move {ToolboxComponentLabel(previewKind)}: left-click to confirm, right-click or Escape to cancel.");
        }

        private bool CommitContainerMove(ContainerId containerId, TabletopPose requestedPose)
        {
            MoveContainerResult result = new MoveContainerUseCase(physicalSurfaceQuery.ResolveContainerSurfaceHeight).Execute(
                matchState,
                new MoveContainerCommand(CreateCommandContext(), containerId, requestedPose));
            if (!result.Succeeded)
            {
                ShowMessage($"Container move rejected: {result.Error}.");
                return false;
            }

            if (matchState.Containers.TryGetValue(containerId, out ContainerState container)
                && container.Kind == ContainerKind.ConsoleSlot)
            {
                ApplyConsolePlacement(containerId);
            }
            else
            {
                ApplyLayout(containerId);
            }

            ShowMessage("Container moved.");
            return true;
        }

        private void BeginConsoleMove(ContainerId slotContainerId)
        {
            EnsureInitialized();
            if (componentPlacementController == null)
            {
                throw new InvalidOperationException("Component placement is not configured.");
            }

            if (!TryResolveConsolePlacement(
                    slotContainerId,
                    out TabletopPose pose,
                    out _,
                    out ConsoleView sourceView))
            {
                ShowMessage("Console move rejected: Console unavailable.");
                return;
            }

            componentPlacementController.Cancel();
            GameObject previewRoot = CreateToolboxPlacementPreview(TabletopComponentKind.Console, 0);
            List<GameObjectActivationSnapshot> sourceVisibility = HideContainerMoveSource(
                sourceView.gameObject,
                sourceView.ConsoleState.SlotContainerIds);
            CloseContextMenu();
            try
            {
                componentPlacementController.BeginContainerMove(
                    previewRoot,
                    pose,
                    requestedPose => CommitContainerMove(slotContainerId, requestedPose),
                    committed => CompleteContainerMovePresentation(
                        slotContainerId,
                        true,
                        committed,
                        sourceVisibility));
            }
            catch
            {
                RestoreMoveSourceVisibility(sourceVisibility);
                previewRoot.SetActive(false);
                Destroy(previewRoot);
                throw;
            }

            ShowPlacementHint("Move Console");
            ShowMessage("Move Console: left-click to confirm, right-click or Escape to cancel.");
        }

        private bool TryResolveContainerMoveRoot(
            ContainerId containerId,
            ContainerKind containerKind,
            out GameObject sourceRoot)
        {
            if (containerKind == ContainerKind.Deck
                && TryGetDeckPresentation(containerId, out _, out PrototypeFixedContainerVisual deckVisual))
            {
                sourceRoot = deckVisual.gameObject;
                return true;
            }

            if (containerKind == ContainerKind.Stack
                && stackViewsByContainerId.TryGetValue(containerId, out StackRuntimeView stackView)
                && stackView.Root != null)
            {
                sourceRoot = stackView.Root;
                return true;
            }

            if (containerKind == ContainerKind.DiscardPile
                && TryGetRuntimeDiscardPile(containerId, out RuntimeDiscardPileInstance discardPile)
                && discardPile.Root != null)
            {
                sourceRoot = discardPile.Root;
                return true;
            }

            sourceRoot = null;
            return false;
        }

        private List<GameObjectActivationSnapshot> HideContainerMoveSource(
            GameObject sourceRoot,
            IReadOnlyList<ContainerId> memberContainerIds)
        {
            List<GameObjectActivationSnapshot> snapshots = new List<GameObjectActivationSnapshot>();
            HashSet<GameObject> capturedObjects = new HashSet<GameObject>();
            CaptureActivation(sourceRoot, snapshots, capturedObjects);
            for (int containerIndex = 0; containerIndex < memberContainerIds.Count; containerIndex++)
            {
                if (!matchState.Containers.TryGetValue(
                        memberContainerIds[containerIndex],
                        out ContainerState memberContainer))
                {
                    continue;
                }

                for (int objectIndex = 0; objectIndex < memberContainer.ObjectIds.Count; objectIndex++)
                {
                    if (TryGetCardView(memberContainer.ObjectIds[objectIndex], out CardView card))
                    {
                        CaptureActivation(card.gameObject, snapshots, capturedObjects);
                    }
                }
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                snapshots[i].Target.SetActive(false);
            }

            return snapshots;
        }

        private void CompleteContainerMovePresentation(
            ContainerId authoritativeId,
            bool isConsole,
            bool committed,
            IReadOnlyList<GameObjectActivationSnapshot> sourceVisibility)
        {
            try
            {
                if (!committed && matchState != null)
                {
                    if (isConsole)
                    {
                        ApplyConsolePlacement(authoritativeId);
                    }
                    else
                    {
                        ApplyLayout(authoritativeId);
                    }
                }
            }
            finally
            {
                RestoreMoveSourceVisibility(sourceVisibility);
                Physics.SyncTransforms();
            }
        }

        private static void CaptureActivation(
            GameObject target,
            ICollection<GameObjectActivationSnapshot> snapshots,
            ISet<GameObject> capturedObjects)
        {
            if (target != null && capturedObjects.Add(target))
            {
                snapshots.Add(new GameObjectActivationSnapshot(target, target.activeSelf));
            }
        }

        private static void RestoreMoveSourceVisibility(
            IReadOnlyList<GameObjectActivationSnapshot> snapshots)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                GameObjectActivationSnapshot snapshot = snapshots[i];
                if (snapshot.Target != null)
                {
                    snapshot.Target.SetActive(snapshot.WasActive);
                }
            }
        }

        private TabletopPose CreateNextToolboxSpawnPose()
        {
            return CreateToolboxSpawnPose(toolboxSpawnSequence);
        }

        // The first quick-spawn grid cell with no placed Deck, Stack, Discard Pile or Console in it (H-E): a pile
        // made outside the Toolbox (the hand turned off) never lands on another, even after a rebuild.
        private TabletopPose CreateFreeToolboxSpawnPose()
        {
            ConsoleAdjacentPlacementSettings footprint = ConsoleAdjacentPlacementSettings.Standard;
            double cellWidth = footprint.BayWidth + footprint.MinimumClearance;
            double cellDepth = footprint.BayDepth + footprint.MinimumClearance;
            for (int sequence = 0; sequence < 12; sequence++)
            {
                TabletopPose candidate = CreateToolboxSpawnPose(sequence);
                bool occupied = false;
                foreach (ContainerPlacementState placement in matchState.ContainerPlacements.Values)
                {
                    if (!placement.HasExtent
                        && Math.Abs(placement.Pose.Position.X - candidate.Position.X) < cellWidth
                        && Math.Abs(placement.Pose.Position.Y - candidate.Position.Y) < cellDepth)
                    {
                        occupied = true;
                        break;
                    }
                }

                foreach (PlacedConsoleState console in matchState.PlacedConsoles.Values)
                {
                    if (Math.Abs(console.Pose.Position.X - candidate.Position.X) < cellWidth * 2d
                        && Math.Abs(console.Pose.Position.Y - candidate.Position.Y) < cellDepth * 1.5d)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied)
                {
                    return candidate;
                }
            }

            return CreateToolboxSpawnPose(toolboxSpawnSequence);
        }

        private TabletopPose CreateToolboxSpawnPose(int sequence)
        {
            // Grid cells are the largest catalog footprint, a pile's bay, plus the placement clearance, so
            // quick-spawned piles never overlap (doc 22, C2b).
            ConsoleAdjacentPlacementSettings footprint = ConsoleAdjacentPlacementSettings.Standard;
            double cellWidth = footprint.BayWidth + footprint.MinimumClearance;
            double cellDepth = footprint.BayDepth + footprint.MinimumClearance;
            double baseX = -2.4d;
            double baseY = -2.4d;
            double columnX = cellWidth;
            double columnY = 0d;
            double rowX = 0d;
            double rowY = cellDepth;
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
                    columnX = -radialY * cellWidth;
                    columnY = radialX * cellWidth;
                    // Rows step toward the table centre so the larger cells stay on the table.
                    rowX = -radialX * cellDepth;
                    rowY = -radialY * cellDepth;
                }
                else
                {
                    baseX = x;
                    baseY = y;
                }

                rotation = playerZonePose.RotationDegrees;
            }

            int column = sequence % 4;
            int row = (sequence / 4) % 3;
            return new TabletopPose(
                new TableCoordinate(
                    baseX + (column * columnX) + (row * rowX),
                    baseY + (column * columnY) + (row * rowY)),
                rotation,
                0,
                sequence * ToolboxPhysicalOrderStride);
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
                    PrototypeCardVisualReferences preview = Instantiate(catalogCardPrefab);
                    preview.ValidateReferences();
                    preview.AlignFaceLabelsToSurface(tabletopLocalOrderHeight);
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
                    preview.SetBackLabelHidden(HidesPrototypeVisual(PrototypeVisualHide.CardPlaceholderLabels));
                    ApplyLabelRendererHide(preview.FrontLabel, PrototypeVisualHide.CardFaceLabels);
                    ApplyLabelRendererHide(preview.BackLabel, PrototypeVisualHide.CardFaceLabels);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Deck:
                {
                    PrototypeFixedContainerVisual preview = Instantiate(catalogDeckPrefab);
                    preview.ValidateReferences();
                    ConfigureContainerLabel(preview.Label, "DECK");
                    preview.Label.gameObject.SetActive(!HidesPrototypeVisual(PrototypeVisualHide.ContainerLabels));
                    ConfigureBayFootprint(preview, GameTemplatePileStyle.DefaultFor(ContainerKind.Deck).BayMark);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Stack:
                {
                    PrototypeFixedContainerVisual preview = Instantiate(catalogStackPrefab);
                    preview.ValidateReferences();
                    ConfigureContainerLabel(preview.Label, "STACK");
                    preview.Label.gameObject.SetActive(!HidesPrototypeVisual(PrototypeVisualHide.ContainerLabels));
                    ConfigureBayFootprint(preview, GameTemplatePileStyle.DefaultFor(ContainerKind.Stack).BayMark);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.DiscardPile:
                {
                    PrototypeFixedContainerVisual preview = Instantiate(catalogDiscardPilePrefab);
                    preview.ValidateReferences();
                    ConfigureContainerLabel(preview.Label, "DISCARD");
                    preview.Label.gameObject.SetActive(!HidesPrototypeVisual(PrototypeVisualHide.ContainerLabels));
                    ConfigureBayFootprint(preview, GameTemplatePileStyle.DefaultFor(ContainerKind.DiscardPile).BayMark);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Pawn:
                    previewRoot = Instantiate(catalogPawnPrefab).gameObject;
                    break;
                case TabletopComponentKind.Token:
                    previewRoot = Instantiate(catalogTokenPrefab).gameObject;
                    break;
                case TabletopComponentKind.Die:
                {
                    if (!ToolboxComponentDefinitions.IsSupportedDieSideCount(dieSideCount))
                    {
                        throw new ArgumentOutOfRangeException(nameof(dieSideCount));
                    }

                    DieView preview = Instantiate(catalogDiePrefab);
                    preview.ConfigurePhysicalShape(dieSideCount);
                    ConfigurePrototypeLabel(preview.ResultLabel, $"d{dieSideCount}\n1", 0.18f, 64);
                    ApplyLabelRendererHide(preview.ResultLabel, PrototypeVisualHide.DieResultLabel);
                    previewRoot = preview.gameObject;
                    break;
                }
                case TabletopComponentKind.Console:
                {
                    ConsoleView preview = Instantiate(catalogConsolePrefab);
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

        private GameObject CreateCardBatchPlacementPreview(int quantity)
        {
            GameObject previewRoot = new GameObject("Card Batch Placement Preview");
            PrepareRuntimeRoot(previewRoot, previewRoot.name);
            TabletopPose origin = TabletopPose.Default;
            for (int i = 0; i < quantity; i++)
            {
                PrototypeCardVisualReferences preview = Instantiate(catalogCardPrefab, previewRoot.transform, false);
                preview.ValidateReferences();
                preview.AlignFaceLabelsToSurface(tabletopLocalOrderHeight);
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
                preview.SetBackLabelHidden(HidesPrototypeVisual(PrototypeVisualHide.CardPlaceholderLabels));
                ApplyLabelRendererHide(preview.FrontLabel, PrototypeVisualHide.CardFaceLabels);
                ApplyLabelRendererHide(preview.BackLabel, PrototypeVisualHide.CardFaceLabels);
                TabletopPose offsetPose = GenericCardBatchLayout.ResolvePose(origin, i, quantity, i);
                preview.transform.localPosition = new Vector3(
                    (float)(offsetPose.Position.X * worldUnitsPerTableUnit),
                    i * tabletopLocalOrderHeight,
                    (float)(offsetPose.Position.Y * worldUnitsPerTableUnit));
                preview.transform.localRotation = Quaternion.identity;
            }

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
                    appearedTransform = ProjectToolboxDeck(result.ContainerId);
                    layoutCollectionChanged = true;
                    break;
                case TabletopComponentKind.Stack:
                    appearedTransform = ProjectToolboxStack(result.ContainerId, "STACK");
                    layoutCollectionChanged = true;
                    break;
                case TabletopComponentKind.DiscardPile:
                    appearedTransform = ProjectDiscardPile(result.ContainerId);
                    layoutCollectionChanged = true;
                    break;
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
                case TabletopComponentKind.Console:
                    appearedTransform = ProjectToolboxConsole(matchState.PlacedConsoles[result.ConsoleId]);
                    layoutCollectionChanged = true;
                    break;
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

        // The Presentation of one placed Deck, Stack, Discard Pile or Console the Toolbox (or a split) made. Used
        // when it is placed and again when Undo or Redo rebuilds the table (doc 22, C2b). No interaction
        // rebuild or appear animation here; the callers do that.
        private Transform ProjectToolboxDeck(ContainerId containerId)
        {
            RuntimeDeckInstance instance = CreateRuntimeDeckInstance(
                "Toolbox Deck",
                "DECK",
                containerId,
                true);
            runtimeDeckInstances.Add(instance);
            controllerDeckViews.Add(instance.View);
            GameTemplatePileStyle toolboxDeckStyle = GameTemplatePileStyle.DefaultFor(ContainerKind.Deck);
            instance.View.ConfigureTableRest(true, toolboxDeckStyle.MaximumPileHeight);
            ConfigurePileBay(instance.Visual, toolboxDeckStyle, SeatId.Empty);
            instance.View.Bind(
                matchState.GetContainer(containerId),
                matchState.ContainerPlacements[containerId],
                coordinateConverter,
                cardViews);
            ConfigureFixedContainer(instance.Visual, instance.View);
            return instance.Root.transform;
        }

        private Transform ProjectToolboxStack(ContainerId containerId, string name)
        {
            ContainerState container = matchState.GetContainer(containerId);
            ContainerPlacementState placement = matchState.ContainerPlacements[containerId];
            StackRuntimeView stack = CreateStackRuntimeView(name, container, placement, true);
            stackViewsByContainerId.Add(containerId, stack);
            return stack.Root.transform;
        }

        private Transform ProjectToolboxConsole(PlacedConsoleState placedConsole)
        {
            RuntimeConsoleInstance instance = CreateRuntimeConsoleInstance(
                "Toolbox Console",
                placedConsole);
            runtimeConsoleInstances.Add(instance);
            playerConsoleViews.Add(instance.View);
            BindConsole(
                instance.View,
                placedConsole.Console,
                instance.SlotViews,
                instance.SlotVisuals);
            for (int i = 0; i < instance.SlotViews.Length; i++)
            {
                ConfigureConsoleSlot(instance.SlotViews[i]);
            }

            return instance.Root.transform;
        }

        // Every Discard Pile, from the Toolbox or declared by a template, is the catalog prefab resting on the
        // table in its bay with the Discard mark; a template pile uses its declared style (doc 22, C2b).
        private Transform ProjectDiscardPile(ContainerId containerId)
        {
            ContainerState container = matchState.GetContainer(containerId);
            GameTemplatePileStyle style = ResolveTemplatePileStyle(containerId)
                ?? GameTemplatePileStyle.DefaultFor(ContainerKind.DiscardPile);
            PrototypeFixedContainerVisual visual = Instantiate(catalogDiscardPilePrefab);
            GameObject root = PrepareRuntimeRoot(
                visual.gameObject,
                matchState.IsTemplateContainer(containerId) ? "Template Discard Pile" : "Toolbox Discard Pile");
            visual.ValidateReferences();
            DiscardPileView view = visual.GetView<DiscardPileView>();
            ConfigureContainerLabel(visual.Label, "DISCARD");
            visual.ClearFeedback();
            ApplyFixedContainerVisualHide(visual, true);
            view.ConfigureTableRest(true, style.MaximumPileHeight);
            ConfigurePileBay(visual, style, container.OwnerSeatId);
            view.Bind(
                container,
                matchState.ContainerPlacements[containerId],
                coordinateConverter,
                cardViews);
            ConfigureFixedContainer(visual, view);
            runtimeDiscardPileInstances.Add(new RuntimeDiscardPileInstance(root, visual, view, containerId));
            return root.transform;
        }

        // Undo and Redo rebuild the table from the restored Match. Template pieces are rebuilt by the session
        // build; this recreates every other placed Deck, Stack and Console (from the Toolbox or a split) and every
        // Discard Pile, template or not, so nothing the Match still holds disappears from the table.
        private void ProjectUnprojectedPlacedComponents()
        {
            List<ContainerId> containerIds = new List<ContainerId>(matchState.ContainerPlacements.Keys);
            for (int i = 0; i < containerIds.Count; i++)
            {
                ContainerId containerId = containerIds[i];
                if (!matchState.Containers.TryGetValue(containerId, out ContainerState container)
                    || containerId == handContainerId)
                {
                    continue;
                }

                switch (container.Kind)
                {
                    case ContainerKind.Deck:
                        if (!matchState.IsTemplateContainer(containerId)
                            && !TryGetDeckPresentation(containerId, out _, out _))
                        {
                            ProjectToolboxDeck(containerId);
                        }

                        break;
                    case ContainerKind.Stack:
                        if (!matchState.IsTemplateContainer(containerId)
                            && !stackViewsByContainerId.ContainsKey(containerId))
                        {
                            ProjectToolboxStack(containerId, "STACK");
                        }

                        break;
                    case ContainerKind.DiscardPile:
                        if (!TryGetRuntimeDiscardPile(containerId, out _))
                        {
                            ProjectDiscardPile(containerId);
                        }

                        break;
                }
            }

            foreach (PlacedConsoleState placedConsole in matchState.PlacedConsoles.Values)
            {
                bool projected = false;
                for (int i = 0; i < runtimeConsoleInstances.Count; i++)
                {
                    if (runtimeConsoleInstances[i].ConsoleId == placedConsole.Id)
                    {
                        projected = true;
                        break;
                    }
                }

                if (!projected)
                {
                    ProjectToolboxConsole(placedConsole);
                }
            }

            AssignPileBayMouthTargets();
        }

        private bool TryGetRuntimeDiscardPile(ContainerId containerId, out RuntimeDiscardPileInstance instance)
        {
            for (int i = 0; i < runtimeDiscardPileInstances.Count; i++)
            {
                if (runtimeDiscardPileInstances[i].ContainerId == containerId
                    && runtimeDiscardPileInstances[i].View != null)
                {
                    instance = runtimeDiscardPileInstances[i];
                    return true;
                }
            }

            instance = null;
            return false;
        }

        private void ReleaseRuntimeDiscardPileInstance(int index)
        {
            RuntimeDiscardPileInstance instance = runtimeDiscardPileInstances[index];
            GameObject root = instance.Root;
            PrototypeFixedContainerVisual visual = instance.Visual;
            DiscardPileView view = instance.View;
            DisableRuntimeInteraction(root);
            if (visual != null)
            {
                visual.DropTarget.ClearConfiguration();
                visual.DropTarget.enabled = false;
                visual.TargetCollider.enabled = false;
                visual.ClearFeedback();
            }

            if (view != null && view.IsBound)
            {
                view.Unbind();
            }

            layoutViews.Remove(view);
            feedbackTargetsByContainerId.Remove(instance.ContainerId);
            runtimeDiscardPileInstances.RemoveAt(index);
            instance.ClearReferences();
            DestroyRuntimeOwnedGameObject(root);
        }

        private void ReleaseRuntimeDiscardPileInstance(ContainerId containerId)
        {
            for (int i = runtimeDiscardPileInstances.Count - 1; i >= 0; i--)
            {
                if (runtimeDiscardPileInstances[i].ContainerId == containerId)
                {
                    ReleaseRuntimeDiscardPileInstance(i);
                    return;
                }
            }
        }

        private void ReleaseRuntimeDiscardPileInstances()
        {
            while (runtimeDiscardPileInstances.Count > 0)
            {
                ReleaseRuntimeDiscardPileInstance(runtimeDiscardPileInstances.Count - 1);
            }
        }

        // ---------- Empty Table hand (doc 22, H-E) ----------

        private bool HasLocalHand()
        {
            return handView != null && handView.IsBound && !handContainerId.IsEmpty;
        }

        // The Empty Table's Hand from the catalog Hand entry with the camera tray, when the Hand is on. An Undo
        // that puts cards back into a Hand that is off turns it back on, so cards are never hidden.
        private void BuildEmptyTableHand()
        {
            emptyTableHandContainerId = ContainerId.Empty;
            foreach (ContainerState container in matchState.Containers.Values)
            {
                if (container.Kind == ContainerKind.Hand)
                {
                    emptyTableHandContainerId = container.Id;
                    break;
                }
            }

            if (emptyTableHandContainerId.IsEmpty)
            {
                return;
            }

            ContainerState hand = matchState.GetContainer(emptyTableHandContainerId);
            if (!emptyTableHandEnabled && hand.Count > 0)
            {
                emptyTableHandEnabled = true;
            }

            if (!emptyTableHandEnabled)
            {
                return;
            }

            handContainerId = emptyTableHandContainerId;
            localHandVisual = CreateLocalHandVisual();
            handView = localHandVisual.GetView<HandView>();
            handView.ConfigurePresentation(presentationTransitions, localHandVisual.FeedbackRenderer);
            if (handTrayRig == null)
            {
                handTrayRig = HandTrayRig.Create(
                    targetCamera,
                    localHandVisual.TargetCollider.gameObject.layer,
                    localHandVisual.FeedbackRenderer.sharedMaterial);
            }

            handTrayRig.Activate();
            handView.ConfigureTray(handTrayRig);
            handView.Bind(hand, localHandVisual.LayoutAnchor, coordinateConverter, cardViews);
            ConfigureContainerLabel(localHandVisual.Label, "HAND");
            ConfigureHandTrayDropTarget();
            RefreshCardContentVisibility();
        }

        // The Toolbox footer's Hand switch. Off: the Hand's cards go to the table as one face-down Deck (one Undo
        // step); the table is then rebuilt without the Hand. On: the table is rebuilt with it.
        private void SetEmptyTableHandEnabled(bool enabled)
        {
            if (!IsInitialized
                || activeSession == null
                || activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom
                || emptyTableHandContainerId.IsEmpty
                || enabled == emptyTableHandEnabled)
            {
                return;
            }

            if (interactionRouter != null && interactionRouter.HasActiveInteraction)
            {
                ShowMessage("Finish the current move first.");
                ShowActiveSessionUi();
                return;
            }

            CloseContextMenu();
            componentPlacementController?.Cancel();
            string message = enabled ? "Hand on." : "Hand off.";
            ContainerState hand = matchState.GetContainer(emptyTableHandContainerId);
            if (!enabled && hand.Count > 0)
            {
                int count = hand.Count;
                CollectHandIntoDeckResult result = new CollectHandIntoDeckUseCase(
                        componentIdentitySource,
                        physicalSurfaceQuery.ResolveContainerSurfaceHeight)
                    .Execute(
                        matchState,
                        new CollectHandIntoDeckRequest(
                            CreateCommandContext(),
                            emptyTableHandContainerId,
                            CreateFreeToolboxSpawnPose()));
                if (!result.Succeeded)
                {
                    ShowMessage($"Hand off rejected: {result.Error}.");
                    ShowActiveSessionUi();
                    return;
                }

                message = count == 1
                    ? "Hand off: your card went back to the table as a face-down deck. Undo brings it back."
                    : $"Hand off: your {count} cards went back to the table as a face-down deck. Undo brings them back.";
            }

            emptyTableHandEnabled = enabled;
            RebuildActiveTablePresentation();
            ShowMessage(message);
            runtimeUi?.OpenComponentToolbox();
        }

        // Rebuilds the table from the current Match, as Undo does, keeping the camera and the Undo history.
        private void RebuildActiveTablePresentation()
        {
            rebuildingFromUndo = true;
            try
            {
                Shutdown(true);
                InitializeActiveSession(false);
                ShowActiveSessionUi();
            }
            finally
            {
                rebuildingFromUndo = false;
                RefreshUndoUi();
            }
        }

        // Deck right-click "Draw to Hand": a count, then the draw goes from the top of the deck into the local Hand.
        private void OpenDrawToHandPopup(ContainerId deckId)
        {
            CloseContextMenu();
            if (!HasLocalHand() || !matchState.Containers.TryGetValue(deckId, out ContainerState deck) || deck.Count == 0)
            {
                ShowMessage("Draw to Hand is unavailable.");
                return;
            }

            int maximum = Math.Max(1, deck.Count);
            selectedQuantity = Mathf.Clamp(selectedQuantity, 1, maximum);
            runtimeUi.ShowQuantityPopup(
                "DRAW TO HAND",
                $"Draw from the top of this deck into your hand. {deck.Count} card{(deck.Count == 1 ? string.Empty : "s")} left.",
                "Draw",
                selectedQuantity,
                1,
                maximum,
                () => ChangeSelectedQuantity(-1, maximum),
                () => ChangeSelectedQuantity(1, maximum),
                () =>
                {
                    int count = Mathf.Clamp(selectedQuantity, 1, maximum);
                    runtimeUi?.CloseTabletopPopup();
                    DrawCards(deckId, count);
                },
                CloseContextMenu);
        }

        private void ProjectCreatedCardBatch(IReadOnlyList<TabletopObjectId> cardIds)
        {
            List<Transform> appearedTransforms = new List<Transform>(cardIds.Count);
            for (int i = 0; i < cardIds.Count; i++)
            {
                CardInstanceState card = matchState.Cards[cardIds[i]];
                CardView view = CreateCardView(card, "CARD", out TabletopSelectionVisual selectionVisual);
                cardViews.Add(view);
                cardSelectionVisuals.Add(selectionVisual);
                appearedTransforms.Add(view.transform);
            }

            RefreshContainerCardViewSources();
            RefreshSelectionPresenterAfterRuntimeProjection();
            Physics.SyncTransforms();
            for (int i = 0; i < appearedTransforms.Count; i++)
            {
                presentationTransitions.Appear(appearedTransforms[i], settleDuration);
            }

            Physics.SyncTransforms();
        }

        private void ProjectPopulatedDeckCards(
            ContainerId deckContainerId,
            IReadOnlyList<TabletopObjectId> cardIds)
        {
            for (int i = 0; i < cardIds.Count; i++)
            {
                CardInstanceState card = matchState.Cards[cardIds[i]];
                CardView view = CreateCardView(card, "CARD", out TabletopSelectionVisual selectionVisual);
                cardViews.Add(view);
                cardSelectionVisuals.Add(selectionVisual);
            }

            RefreshContainerCardViewSources();
            if (!TryGetDeckPresentation(deckContainerId, out DeckView deck, out _))
            {
                throw new InvalidOperationException("Populated Deck has no Presentation binding.");
            }

            deck.ApplyAcceptedLayout();
            RefreshSelectionPresenterAfterRuntimeProjection();
            RefreshCardContentVisibility();
            Physics.SyncTransforms();
        }

        private void ProjectDeletedComponent(DeleteTabletopComponentResult result)
        {
            if (result.ComponentKind == TabletopComponentKind.Card
                && inspectedCardId == result.Target.ObjectId)
            {
                CloseCardInspect();
            }

            selectionState.ClearAll();
            switch (result.ComponentKind)
            {
                case TabletopComponentKind.Card:
                    ReleaseRuntimeCardInstance(result.Target.ObjectId);
                    RefreshContainerCardViewSources();
                    if (!result.PreviousContainerId.IsEmpty
                        && matchState.Containers.ContainsKey(result.PreviousContainerId))
                    {
                        ApplyLayout(result.PreviousContainerId);
                    }
                    break;
                case TabletopComponentKind.Pawn:
                    ReleaseRuntimeObjectInstance(
                        result.Target.ObjectId,
                        runtimePawnInstances,
                        pawnViews,
                        pawnSelectionVisuals);
                    break;
                case TabletopComponentKind.Token:
                    ReleaseRuntimeObjectInstance(
                        result.Target.ObjectId,
                        runtimeTokenInstances,
                        tokenViews,
                        tokenSelectionVisuals);
                    if (!result.PreviousContainerId.IsEmpty)
                    {
                        ApplyTokenContainerLayout(result.PreviousContainerId);
                    }
                    break;
                case TabletopComponentKind.Die:
                    ReleaseRuntimeObjectInstance(
                        result.Target.ObjectId,
                        runtimeDieInstances,
                        dieViews,
                        dieSelectionVisuals);
                    break;
                case TabletopComponentKind.Deck:
                    SuspendInteractionDependenciesForRebuild();
                    ReleaseRuntimeDeckInstance(result.Target.ContainerId);
                    ResumeInteractionDependenciesAfterRebuild();
                    break;
                case TabletopComponentKind.DiscardPile:
                    SuspendInteractionDependenciesForRebuild();
                    ReleaseRuntimeDiscardPileInstance(result.Target.ContainerId);
                    ResumeInteractionDependenciesAfterRebuild();
                    break;
                case TabletopComponentKind.Stack:
                    RemoveStackRuntimeView(result.Target.ContainerId);
                    if (primaryStackContainerId == result.Target.ContainerId)
                    {
                        primaryStackContainerId = ContainerId.Empty;
                    }

                    if (sourceFeedbackContainerId == result.Target.ContainerId)
                    {
                        sourceFeedbackContainerId = ContainerId.Empty;
                    }
                    break;
                case TabletopComponentKind.Console:
                    SuspendInteractionDependenciesForRebuild();
                    ReleaseRuntimeConsoleInstance(result.Target.ConsoleId);
                    ResumeInteractionDependenciesAfterRebuild();
                    break;
                default:
                    throw new InvalidOperationException("Deleted Component kind has no Presentation removal path.");
            }

            RefreshSelectionPresenterAfterRuntimeProjection();
            RefreshCardContentVisibility();
            Physics.SyncTransforms();
        }

        private void ApplyTokenContainerLayout(ContainerId containerId)
        {
            for (int i = 0; i < tokenContainerViews.Count; i++)
            {
                TokenContainerView view = tokenContainerViews[i];
                if (view != null && view.IsBound && view.ContainerId == containerId)
                {
                    view.ApplyAcceptedLayout();
                    return;
                }
            }
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
            RegisterPhysicalViews();
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

        public void ClearTable()
        {
            RequestTableReplacement(TabletopSessionSelection.EmptyCustom);
        }

        public void LoadGameTemplate(GameTemplateId gameTemplateId)
        {
            RequestTableReplacement(TabletopSessionSelection.FromGameTemplate(gameTemplateId));
        }

        public void ToggleGameTemplatesPanel()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (gameTemplatesPanelVisible)
            {
                CloseGameTemplatesPanel();
                return;
            }

            gameTemplatesPanelVisible = true;
            RefreshGameTemplatesPanelUi();
        }

        // Table menu > New table… (UI-1): opens the game selection panel (the start flow replaces it later).
        private void OpenGameTemplatesPanel()
        {
            if (!IsInitialized)
            {
                return;
            }

            gameTemplatesPanelVisible = true;
            RefreshGameTemplatesPanelUi();
        }

        private void CloseGameTemplatesPanel()
        {
            gameTemplatesPanelVisible = false;
            runtimeUi?.HideGameTemplatesPanel();
        }

        private void PrepareTemplateCatalog()
        {
            sessionBootstrapService = new TabletopSessionBootstrapService();
            tableActionActorId = PlayerId.New();
            authoritativeRandomValueSource = new SystemRandomValueSource();
            templateCatalogError = null;
            gameTemplatesPanelError = null;
            try
            {
                RegisterFreshTrapFloorTemplates();
            }
            catch (Exception exception)
            {
                availableTrapFloorTemplates.Clear();
                sessionTemplateCatalog = new GameTemplateCatalog(Array.Empty<GameTemplateRegistration>());
                templateCatalogError = $"Trap Floor is unavailable: {exception.Message}";
            }
        }

        private void RegisterFreshTrapFloorTemplates()
        {
            RequireReference(trapFloorGameDefinition, nameof(trapFloorGameDefinition));
            if (authoritativeRandomValueSource == null)
            {
                authoritativeRandomValueSource = new SystemRandomValueSource();
            }

            var gameDefinition = trapFloorGameDefinition.ToData();
            if (gameDefinition.Modes.Count == 0)
                throw new InvalidOperationException("Trap Floor requires at least one authored Mode.");

            availableTrapFloorTemplates.Clear();
            List<GameTemplateRegistration> registrations = new List<GameTemplateRegistration>(gameDefinition.Modes.Count);
            for (int i = 0; i < gameDefinition.Modes.Count; i++)
            {
                TrapFloorTemplateDefinition template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                    authoritativeRandomValueSource,
                    gameDefinition,
                    gameDefinition.Modes[i].StableId,
                    GetConsoleLayout());
                availableTrapFloorTemplates.Add(template.Template.Id, template);
                registrations.Add(new GameTemplateRegistration(template.Template, template.ContentCatalog));
            }

            sessionTemplateCatalog = new GameTemplateCatalog(registrations);
        }

        private void InitializeRuntimeUi()
        {
            RequireReference(runtimeUi, nameof(runtimeUi));
            if (!runtimeUi.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires runtimeUi to reference the scene-authored UI controller.");
            }

            runtimeUi.ValidateReferences();
        }

        private void RefreshGameTemplatesPanelUi()
        {
            if (runtimeUi == null || !gameTemplatesPanelVisible)
            {
                return;
            }

            List<PrototypeGameTemplateOption> options = new List<PrototypeGameTemplateOption>();
            if (sessionTemplateCatalog != null)
            {
                foreach (GameTemplateRegistration registration in sessionTemplateCatalog.Registrations.Values)
                {
                    GameTemplateId templateId = registration.Template.Id;
                    options.Add(new PrototypeGameTemplateOption(
                        registration.Template.DisplayName,
                        () => RequestTableReplacement(
                            TabletopSessionSelection.FromGameTemplate(templateId))));
                }
            }

            runtimeUi.ShowGameTemplatesPanel(
                () => RequestTableReplacement(TabletopSessionSelection.EmptyCustom),
                options,
                gameTemplatesPanelError ?? templateCatalogError);
        }

        private void ShowActiveSessionUi()
        {
            if (runtimeUi == null || activeSession == null)
            {
                return;
            }

            // Session bar chip (UI-1): a game shows its name and mode ("Trap Floor — Easy"); Empty Table its name.
            bool isGameSession = activeSession.Selection.Kind != TabletopSessionKind.EmptyCustom;
            string sessionTitle = "EMPTY TABLE";
            string sessionSubtitle = string.Empty;
            if (isGameSession)
            {
                string displayName = activeSession.Template.DisplayName;
                int separator = displayName.IndexOf(" — ", StringComparison.Ordinal);
                sessionTitle = (separator < 0 ? displayName : displayName.Substring(0, separator)).ToUpperInvariant();
                sessionSubtitle = separator < 0 ? string.Empty : displayName.Substring(separator + 3);
            }

            runtimeUi.ShowActiveSession(
                sessionTitle,
                sessionSubtitle,
                isGameSession,
                HandleUndoButtonPressed,
                HandleRedoButtonPressed,
                OpenGameTemplatesPanel,
                ResetPrototype,
                ClearTable,
                CurrentStatusText(),
                new ComponentToolboxBindings(
                    componentLibrary,
                    activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom ? null : trapFloorGameDefinition,
                    PlaceCatalogEntry,
                    activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom && !emptyTableHandContainerId.IsEmpty,
                    emptyTableHandEnabled,
                    SetEmptyTableHandEnabled));
            runtimeUi.ShowTableSettings(
                BuildRulesCardModel(),
                rulesCardEnabled,
                SetRulesCardEnabled,
                hintsEnabled,
                SetHintsEnabled);
            RefreshUndoUi();
            RefreshTrapFloorStatusUi();
        }

        // The rules card for this table (doc 23, R1): the game's default rule set, filtered to the active mode.
        // Empty Table has no rules. Shown only; nothing is enforced.
        private RulesCardModel BuildRulesCardModel()
        {
            if (activeSession == null
                || activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom
                || trapFloorGameDefinition == null)
            {
                return null;
            }

            string modeStableId = trapFloorTemplate != null ? trapFloorTemplate.ActiveMode.StableId : string.Empty;
            return RulesCardModel.FromRuleSet(trapFloorGameDefinition.DefaultRuleSet, modeStableId);
        }

        private void SetRulesCardEnabled(bool enabled)
        {
            rulesCardEnabled = enabled;
            runtimeUi?.ShowRulesCard(BuildRulesCardModel(), enabled);
        }

        // Hints switch: guidance text only (controls strip, Toolbox placing bar, status card help line).
        private void SetHintsEnabled(bool enabled)
        {
            hintsEnabled = enabled;
            runtimeUi?.SetHintsVisible(enabled);
            RefreshTrapFloorStatusUi();
        }

        private string HintText(string text) => hintsEnabled ? text : string.Empty;

        private void HandleUndoButtonPressed()
        {
            UndoLatestAction();
        }

        private void HandleRedoButtonPressed()
        {
            RedoLatestAction();
        }

        private void RefreshRuntimeStatusUi()
        {
            if (runtimeUi != null && IsInitialized)
            {
                runtimeUi.SetStatusMessage(CurrentStatusText());
            }
        }

        private void RefreshTrapFloorStatusUi()
        {
            if (runtimeUi == null || !IsInitialized)
            {
                return;
            }

            if (trapFloorObjectiveState != null && trapFloorTurnState != null)
            {
                string phase = trapFloorTurnState.IsCurrentFloorFailed
                    ? "ALL PLAYERS ELIMINATED"
                    : trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                        ? $"{FormatPlayerName(trapFloorTurnState.ActivePlayerId).ToUpperInvariant()} TURN"
                        : "FLOOR TURN";
                string turnDetail = CurrentTrapFloorCollapseStatusText();
                if (!trapFloorTurnState.IsCurrentFloorFailed
                    && trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
                {
                    turnDetail += $"\nOperator: {FormatPlayerName(trapFloorTurnState.FloorOperatorPlayerId)}";
                }

                PrototypeTrapFloorStatusModel turnStatus = new PrototypeTrapFloorStatusModel(
                    $"ROUND {trapFloorTurnState.CurrentRound}",
                    phase,
                    $"{CurrentTrapFloorObjectiveProgressText()}\n{CurrentTrapFloorPlayerStatesText()}",
                    turnDetail,
                    trapFloorTurnState.IsCurrentFloorFailed
                        ? "ALL PLAYERS ELIMINATED"
                        : trapFloorObjectiveState.IsWon ? "VICTORY" : string.Empty,
                    HintText(TrapFloorTurnGuidanceText()));
                runtimeUi.ShowTrapFloorStatus(
                    turnStatus,
                    BuildFloorfallStatusModel(),
                    AddHandTrayToggleAction(BuildTrapFloorTurnActions()));
                return;
            }

            if (trapFloorRoundState == null)
            {
                runtimeUi.HideTrapFloorStatus();
                return;
            }

            TrapFloorPendingFloormasterCard pendingCard = floormasterLifecycleState?.PendingCard;
            string detail = string.Empty;
            if (pendingCard != null)
            {
                detail =
                    $"Pending Trigger: {FormatPlayerName(pendingCard.SearchingPlayerId)} / {pendingCard.Category}\n"
                    + "Resolve the card's effect at the table, then continue.";
            }
            else if (trapFloorRoundState.Phase == TrapFloorRoundPhase.Floorfall)
            {
                detail =
                    $"Floorfalls performed this phase: {trapFloorRoundState.AcceptedFloorfallCount}\n"
                    + "Roll as many Floorfalls as your mode asks for.";
            }
            else if (trapFloorRoundState.IsScheduleCompleted)
            {
                detail = "All 10 rounds are played. Decide the result together.";
            }

            PrototypeTrapFloorStatusModel status = new PrototypeTrapFloorStatusModel(
                $"Round: {trapFloorRoundState.CurrentRoundNumber} / {TrapFloorRoundState.FinalRoundNumber}",
                $"Phase: {trapFloorRoundState.Phase}",
                $"Search + Trigger: {trapFloorRoundState.CompletedSearchTriggerCount} / "
                    + $"{trapFloorRoundState.ParticipatingPlayerIds.Count} Players complete",
                detail,
                $"Hand: {ContainerCount(handContainerId)}",
                HintText(TrapFloorActionHelpText()));

            PrototypeFloorfallStatusModel floorfall = BuildFloorfallStatusModel();
            runtimeUi.ShowTrapFloorStatus(status, floorfall, AddHandTrayToggleAction(BuildTrapFloorAssistedActions()));
        }

        private PrototypeFloorfallStatusModel BuildFloorfallStatusModel()
        {
            if (floorfallState == null || !floorfallState.CurrentTarget.HasValue)
            {
                return new PrototypeFloorfallStatusModel(false, string.Empty, string.Empty, string.Empty);
            }

            TrapFloorFloorfallTarget target = floorfallState.CurrentTarget.Value;
            return new PrototypeFloorfallStatusModel(
                true,
                $"Die 1 / X: {target.XAxisRoll.Value}   Die 2 / Y: {target.YAxisRoll.Value}",
                $"Coordinate: {target.Coordinate}",
                $"Current target: Floor Card {target.Coordinate}");
        }

        private List<PrototypePopupActionOption> BuildTrapFloorAssistedActions()
        {
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            switch (trapFloorRoundState.Phase)
            {
                case TrapFloorRoundPhase.Start:
                    actions.Add(new PrototypePopupActionOption(
                        trapFloorRoundState.CurrentRoundNumber == 1
                            ? "Begin Round / Continue to Search"
                            : "Movement done / Continue",
                        true,
                        () => CompleteTrapFloorStart()));
                    break;
                case TrapFloorRoundPhase.Search:
                    for (int i = 0; i < trapFloorRoundState.ParticipatingPlayerIds.Count; i++)
                    {
                        PlayerId playerId = trapFloorRoundState.ParticipatingPlayerIds[i];
                        if (trapFloorRoundState.HasCompletedSearchTrigger(playerId))
                        {
                            continue;
                        }

                        PlayerId searchingPlayerId = playerId;
                        actions.Add(new PrototypePopupActionOption(
                            $"Search - {FormatPlayerName(searchingPlayerId)}",
                            true,
                            () => SearchFloormasterDeck(searchingPlayerId)));
                    }

                    break;
                case TrapFloorRoundPhase.Trigger:
                    actions.Add(new PrototypePopupActionOption(
                        "Card effect done",
                        true,
                        () => CompletePendingFloormasterTriggerPrototype()));
                    break;
                case TrapFloorRoundPhase.Floorfall:
                    actions.Add(new PrototypePopupActionOption(
                        "Roll Floorfall",
                        true,
                        () => BeginPhysicalFloorfall()));
                    actions.Add(new PrototypePopupActionOption(
                        "Floorfall done",
                        trapFloorRoundState.AcceptedFloorfallCount > 0,
                        () => CompleteFloorfallPhasePrototype()));
                    break;
                case TrapFloorRoundPhase.End:
                    actions.Add(new PrototypePopupActionOption(
                        "End round",
                        true,
                        () => CompleteEndPrototype()));
                    break;
            }

            return actions;
        }

        private List<PrototypePopupActionOption> BuildTrapFloorTurnActions()
        {
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            if (trapFloorTurnState == null)
            {
                return actions;
            }

            if (trapFloorTurnState.IsCurrentFloorFailed)
            {
                actions.Add(new PrototypePopupActionOption(
                    "End Floor Turn",
                    true,
                    AdvanceTrapFloorTurn));
                return actions;
            }

            if (trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn)
            {
                bool searchPending = trapFloorPendingSearchState != null
                    && trapFloorPendingSearchState.IsActive;
                if (TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus)
                    && !blindStatus.IsDirectionResolved)
                {
                    actions.Add(new PrototypePopupActionOption(
                        "Roll Blind Direction",
                        !physicalBlindDirectionPending,
                        () => BeginPhysicalBlindDirection()));
                }
                actions.Add(new PrototypePopupActionOption(
                    "Search",
                    !searchPending,
                    SearchSelectedTrapFloorCard));
                actions.Add(new PrototypePopupActionOption(
                    "Careful Search",
                    !searchPending,
                    CarefulSearchSelectedTrapFloorCard));
                if (searchPending)
                {
                    actions.Add(new PrototypePopupActionOption(
                        "Cancel Search",
                        true,
                        CancelFocusedTrapFloorSearch));
                }
                bool purchasePending = pendingControllerPurchaseState != null
                    && pendingControllerPurchaseState.IsActive
                    && pendingControllerPurchaseState.PlayerId == trapFloorTurnState.ActivePlayerId;
                if (purchasePending)
                {
                    actions.Add(new PrototypePopupActionOption(
                        "Confirm Purchase",
                        pendingControllerPurchaseState.IsPaymentComplete,
                        ConfirmPendingActionAbilityPurchase));
                    actions.Add(new PrototypePopupActionOption(
                        "Cancel Purchase",
                        true,
                        CancelPendingActionAbilityPurchase));
                }
                else
                {
                    actions.Add(new PrototypePopupActionOption(
                        "Buy Ability",
                        true,
                        OpenActionAbilityPurchase));
                }
                actions.Add(new PrototypePopupActionOption(
                    "Skip Turn",
                    true,
                    SkipTrapFloorTurn));
                actions.Add(new PrototypePopupActionOption(
                    "End Turn",
                    true,
                    AdvanceTrapFloorTurn));
                return actions;
            }

            if (trapFloorCollapseState != null)
            {
                actions.Add(new PrototypePopupActionOption(
                    "Collapse Floor",
                    !trapFloorCollapseState.IsCollapsePending
                        && !trapFloorCollapseState.IsBoardExhausted,
                    () => BeginPhysicalFloorCollapse()));
            }

            actions.Add(new PrototypePopupActionOption(
                "End Floor Turn",
                true,
                AdvanceTrapFloorTurn));
            return actions;
        }

        private string TrapFloorTurnGuidanceText()
        {
            if (trapFloorTurnState.IsCurrentFloorFailed)
            {
                return "This round failed. You can still move pieces by hand; end the Floor Turn to continue.";
            }

            if (trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
            {
                return "Roll 2d6 to resolve Collapse\nEnd Floor Turn when complete";
            }

            if (trapFloorPendingSearchState != null
                && trapFloorPendingSearchState.IsActive
                && trapFloorPendingSearchState.PlayerId == trapFloorTurnState.ActivePlayerId)
            {
                string prefix = trapFloorPendingSearchState.SearchKind == TrapFloorSearchKind.Careful
                    ? "CAREFUL SEARCH"
                    : "SEARCH";
                if (trapFloorPendingSearchState.IsPaymentComplete)
                    return $"{prefix} — Choose a Floor";
                return trapFloorPendingSearchState.SearchKind == TrapFloorSearchKind.Careful
                    ? "CAREFUL SEARCH — Place A + B + X + Y in your Console"
                    : "SEARCH — Place the selected Card in your Console";
            }

            if (pendingControllerPurchaseState != null
                && pendingControllerPurchaseState.IsActive
                && pendingControllerPurchaseState.PlayerId == trapFloorTurnState.ActivePlayerId)
            {
                if (pendingControllerPurchaseState.IsPaymentComplete)
                    return "PAYMENT READY — Confirm Purchase";
                string abilityName = FindPurchaseDisplayName(
                    pendingControllerPurchaseState.PurchasedCardDefinitionStableId)
                    .ToUpperInvariant();
                return $"BUY {abilityName} — Place the selected Cards in your Console";
            }

            int handLimit = trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
            string standardGuidance = $"Draw up to {handLimit} if needed\nMove / Search / Buy / Skip";
            bool isBlind = TryGetActiveBlindStatus(out TrapFloorBlindStatusState blindStatus);
            bool isSlow = TryGetActiveSlowStatus(out TrapFloorSlowStatusState slowStatus);
            bool isSticky = TryGetActiveStickyStatus(out _);
            string slowGuidance = isSlow
                ? $"SLOW — Movement {FormatSignedModifier(slowStatus.MovementModifier)} this round"
                : string.Empty;
            string movementGuidance = isSticky
                ? "STICKY — Stay in place this round"
                : string.Empty;
            if (isSlow)
                movementGuidance = string.IsNullOrEmpty(movementGuidance)
                    ? slowGuidance
                    : $"{movementGuidance}\n{slowGuidance}";
            if (!isBlind)
            {
                string availableActions = isSticky
                    ? $"Draw up to {handLimit} if needed\nSearch / Buy / Skip"
                    : standardGuidance;
                return string.IsNullOrEmpty(movementGuidance)
                    ? availableActions
                    : $"{movementGuidance}\n{availableActions}";
            }
            string directionGuidance = blindStatus.IsDirectionResolved
                ? $"BLIND DIRECTION: {blindStatus.MovementDirection.Value.ToString().ToUpperInvariant()}"
                : "BLIND — Roll d4 for movement direction";
            string blindActions = isSticky ? "Search / Buy / Skip" : "Move / Search / Buy / Skip";
            return string.IsNullOrEmpty(movementGuidance)
                ? $"BLIND — No draw this round\n{directionGuidance}\n{blindActions}"
                : $"BLIND — No draw this round\n{directionGuidance}\n{movementGuidance}\n{blindActions}";
        }

        private bool TryGetActiveBlindStatus(out TrapFloorBlindStatusState status)
        {
            if (trapFloorAbilityResolutionState != null
                && trapFloorTurnState != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && trapFloorAbilityResolutionState.TryGetBlindStatus(
                    trapFloorTurnState.ActivePlayerId,
                    trapFloorTurnState.CurrentRound,
                    out status))
            {
                return true;
            }

            status = null;
            return false;
        }

        private bool TryGetActiveSlowStatus(out TrapFloorSlowStatusState status)
        {
            if (trapFloorAbilityResolutionState != null
                && trapFloorTurnState != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && trapFloorAbilityResolutionState.TryGetSlowStatus(
                    trapFloorTurnState.ActivePlayerId,
                    trapFloorTurnState.CurrentRound,
                    out status))
            {
                return true;
            }

            status = null;
            return false;
        }

        private bool TryGetActiveStickyStatus(out TrapFloorStickyStatusState status)
        {
            if (trapFloorAbilityResolutionState != null
                && trapFloorTurnState != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && trapFloorAbilityResolutionState.TryGetStickyStatus(
                    trapFloorTurnState.ActivePlayerId,
                    trapFloorTurnState.CurrentRound,
                    out status))
            {
                return true;
            }

            status = null;
            return false;
        }

        private static string FormatSignedModifier(int modifier) =>
            modifier > 0 ? $"+{modifier}" : modifier.ToString();

        private bool HasEliminatedTrapFloorPlayers()
        {
            for (int i = 0; i < trapFloorTurnState.PlayerOrder.Count; i++)
            {
                if (trapFloorTurnState.GetPlayerState(trapFloorTurnState.PlayerOrder[i])
                    == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                {
                    return true;
                }
            }

            return false;
        }

        private void AdvanceTrapFloorTurn()
        {
            AdvanceTrapFloorTurn(false);
        }

        private void SkipTrapFloorTurn()
        {
            AdvanceTrapFloorTurn(true);
        }

        private void AdvanceTrapFloorTurn(bool skipped)
        {
            EnsureInitialized();
            if (trapFloorTurnState == null || trapFloorTurnService == null)
            {
                ShowMessage("Turn tracking is unavailable outside Trap Floor.");
                return;
            }
            bool wasFloorTurn = trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn;
            bool reactivatesPlayers = wasFloorTurn && HasEliminatedTrapFloorPlayers();
            PlayerId actor = trapFloorTurnState.IsCurrentFloorFailed
                ? trapFloorTurnState.PlayerOrder[0]
                : trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                ? trapFloorTurnState.ActivePlayerId
                : trapFloorTurnState.FloorOperatorPlayerId;
            TrapFloorTurnAdvanceResult result = skipped
                ? trapFloorTurnService.Skip(matchState, CreateCommandContext(actor))
                : trapFloorTurnService.Advance(matchState, CreateCommandContext(actor));
            if (!result.Succeeded)
            {
                string action = skipped ? "Skip Turn" : "End Turn";
                ShowMessage($"{action} rejected: {result.Error}.");
                return;
            }

            if (trapFloorPendingSearchState != null && trapFloorPendingSearchState.IsActive)
            {
                trapFloorPendingSearchState.Clear();
                runtimeUi?.CloseFocusedCardSelection();
                ReplaceCurrentUndoStateForSearchAssistance();
            }

            if (trapFloorAbilityResolutionService != null
                && trapFloorAbilityResolutionService.ClearMovementAssistance())
            {
                abilityTargetPresenter?.ClearCurrent();
                if (undoTrackedMatch == matchState && undoHistory.CurrentStateIndex >= 0)
                    undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
            }

            RefreshTrapFloorStatusUi();
            string skip = skipped ? $"{FormatPlayerName(actor)} skipped. " : string.Empty;
            if (wasFloorTurn)
            {
                string reactivated = reactivatesPlayers ? " All Players reactivated." : string.Empty;
                ShowMessage(
                    $"ROUND {trapFloorTurnState.CurrentRound} — "
                    + $"{FormatPlayerName(trapFloorTurnState.ActivePlayerId)} turn.{reactivated}");
            }
            else if (trapFloorTurnState.Phase == TrapFloorTurnPhase.FloorTurn)
            {
                ShowMessage(
                    $"{skip}Floor Turn — {FormatPlayerName(trapFloorTurnState.FloorOperatorPlayerId)} resolves the Floor.");
            }
            else
            {
                int handLimit = trapFloorTemplate.GameDefinition.ControllerConfiguration.MaximumHandSize;
                ShowMessage(
                    $"{skip}{FormatPlayerName(trapFloorTurnState.ActivePlayerId)} turn. "
                    + $"Draw up to {handLimit} if needed.");
            }
        }

        private string TrapFloorActionHelpText()
        {
            switch (trapFloorRoundState.Phase)
            {
                case TrapFloorRoundPhase.Start:
                    return trapFloorRoundState.CurrentRoundNumber == 1
                        ? string.Empty
                        : "Move your pawns by hand as the round starts, then continue.";
                case TrapFloorRoundPhase.Search:
                    return "Choose the player who searches next.";
                case TrapFloorRoundPhase.Trigger:
                    return "Carry out the card's effect at the table, then mark it done.";
                case TrapFloorRoundPhase.Floorfall:
                    return "Roll as many Floorfalls as your mode asks for, then continue.";
                case TrapFloorRoundPhase.End:
                    return "Check together who survived and whether you won, then continue.";
                case TrapFloorRoundPhase.Completed:
                    return "All 10 rounds are played. Decide the result together.";
                default:
                    return string.Empty;
            }
        }

        private void RefreshToolboxPlacementUi()
        {
            if (!toolboxPlacementHintActive)
            {
                return;
            }

            if (componentPlacementController != null && componentPlacementController.IsActive)
            {
                return;
            }

            toolboxPlacementHintActive = false;
            toolboxPlacementSubject = null;
            toolboxPlacementIcon = null;
            runtimeUi?.ClearPlacementHint();
        }

        private void ShowPlacementHint(string subject)
        {
            toolboxPlacementHintActive = true;
            toolboxPlacementSubject = subject;
            runtimeUi?.ShowPlacementHint(
                subject,
                componentPlacementController?.RotationDegrees ?? 0f,
                toolboxPlacementIcon);
        }

        private void HandlePlacementRotationChanged(float rotationDegrees)
        {
            if (!toolboxPlacementHintActive || string.IsNullOrWhiteSpace(toolboxPlacementSubject))
            {
                return;
            }

            runtimeUi?.ShowPlacementHint(toolboxPlacementSubject, rotationDegrees, toolboxPlacementIcon);
        }

        private void RequestTableReplacement(TabletopSessionSelection selection)
        {
            if (!IsInitialized || !HasCurrentTableContent())
            {
                TryReplaceTable(selection);
                return;
            }

            string replacementName = ReplacementDisplayName(selection);
            runtimeUi.ShowContextMenu(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                "REPLACE CURRENT TABLE?",
                $"The current table contains content. Loading {replacementName} will replace it and establish a new Reset baseline.",
                new[]
                {
                    new PrototypePopupActionOption(
                        selection.Kind == TabletopSessionKind.EmptyCustom
                            ? "Clear Table"
                            : $"Load {replacementName}",
                        true,
                        () => TryReplaceTable(selection)),
                    new PrototypePopupActionOption(
                        "Cancel",
                        true,
                        CloseContextMenu),
                },
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private bool TryReplaceTable(TabletopSessionSelection selection)
        {
            if (sessionBootstrapService == null || sessionTemplateCatalog == null)
            {
                gameTemplatesPanelError = "Game Template loading is not configured.";
                runtimeUi?.SetGameTemplatesError(gameTemplatesPanelError);
                return false;
            }

            if (selection.Kind == TabletopSessionKind.GameTemplate
                && availableTrapFloorTemplates.ContainsKey(selection.GameTemplateId))
            {
                try
                {
                    RegisterFreshTrapFloorTemplates();
                }
                catch (Exception exception)
                {
                    gameTemplatesPanelError = $"Trap Floor setup could not be prepared: {exception.Message}";
                    runtimeUi?.SetGameTemplatesError(gameTemplatesPanelError);
                    return false;
                }
            }

            List<PlayerId> activePlayerIds = CreatePrototypeActivePlayers(selection);
            TabletopSessionBootstrapRequest request = new TabletopSessionBootstrapRequest(
                tableActionActorId,
                selection,
                activePlayerIds,
                MatchId.New());
            TabletopSessionBootstrapResult result = sessionBootstrapService.TryCreate(
                request,
                sessionTemplateCatalog);
            if (!result.Succeeded)
            {
                gameTemplatesPanelError = FormatSessionBuildFailure(result.Issues);
                runtimeUi?.SetGameTemplatesError(gameTemplatesPanelError);
                return false;
            }

            TrapFloorTemplateDefinition candidateTemplate = null;
            PrototypeTemplateContext candidateContext = null;
            try
            {
                if (selection.Kind == TabletopSessionKind.GameTemplate)
                {
                    if (!availableTrapFloorTemplates.TryGetValue(
                            selection.GameTemplateId,
                            out candidateTemplate))
                    {
                        throw new InvalidOperationException(
                            "The selected Game Template has no registered prototype Presentation wiring.");
                    }

                    candidateContext = CreateTrapFloorPrototypeContext(
                        result.Session,
                        candidateTemplate,
                        tableActionActorId);
                }
            }
            catch (Exception exception)
            {
                gameTemplatesPanelError = exception.Message;
                runtimeUi?.SetGameTemplatesError(gameTemplatesPanelError);
                Debug.LogError($"Table replacement validation failed: {exception.Message}", this);
                return false;
            }

            TabletopSession previousSession = activeSession;
            PrototypeTemplateContext previousContext = prototypeTemplateContext;
            TrapFloorTemplateDefinition previousTemplate = trapFloorTemplate;
            bool hadActivePresentation = IsInitialized;

            try
            {
                if (hadActivePresentation)
                {
                    Shutdown();
                }

                activeSession = result.Session;
                emptyTableHandEnabled = true;
                prototypeTemplateContext = candidateContext;
                trapFloorTemplate = candidateTemplate;
                InitializeActiveSession(false);
                gameTemplatesPanelError = null;
                gameTemplatesPanelVisible = false;
                ShowActiveSessionUi();
                return true;
            }
            catch (Exception exception)
            {
                Shutdown();
                activeSession = previousSession;
                prototypeTemplateContext = previousContext;
                trapFloorTemplate = previousTemplate;
                gameTemplatesPanelError = $"Table replacement failed: {exception.Message}";
                Debug.LogError(gameTemplatesPanelError, this);

                if (hadActivePresentation && previousSession != null)
                {
                    try
                    {
                        InitializeActiveSession(false);
                        ShowActiveSessionUi();
                        gameTemplatesPanelVisible = true;
                        RefreshGameTemplatesPanelUi();
                    }
                    catch (Exception rollbackException)
                    {
                        Debug.LogError(
                            $"Previous table presentation could not be restored: {rollbackException.Message}",
                            this);
                    }
                }

                return false;
            }
        }

        private bool HasCurrentTableContent()
        {
            return matchState != null
                && (matchState.ObjectCount > 0
                    || matchState.Containers.Count > 0
                    || matchState.ContainerPlacements.Count > 0
                    || matchState.Seats.Count > 0
                    || matchState.PlacedConsoles.Count > 0
                    || matchState.PlayAreas.Count > 0);
        }

        private string ReplacementDisplayName(TabletopSessionSelection selection)
        {
            if (selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                return "a fresh Empty Table";
            }

            return sessionTemplateCatalog.TryGet(
                    selection.GameTemplateId,
                    out GameTemplateRegistration registration)
                ? registration.Template.DisplayName
                : "the selected Game Template";
        }

        private List<PlayerId> CreatePrototypeActivePlayers(TabletopSessionSelection selection)
        {
            int playerCount = 1;
            if (selection.Kind == TabletopSessionKind.GameTemplate)
            {
                if (!sessionTemplateCatalog.TryGet(selection.GameTemplateId, out GameTemplateRegistration registration))
                {
                    return new List<PlayerId> { tableActionActorId };
                }

                playerCount = registration.Template.RequiredPlayerCount;
            }

            List<PlayerId> players = new List<PlayerId>(playerCount)
            {
                tableActionActorId,
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
            if (hitResolver.TryResolve(screenPosition, out TabletopObjectView resolvedObjectView))
            {
                if (resolvedObjectView is DieView hitDie
                    && hitDie.IsBound
                    && hitDie.DieState != null)
                {
                    selectionState.Select(hitDie);
                    selectionPresenter.Refresh();
                    OpenContextMenu(
                        PrototypeContextMenuMode.Die,
                        screenPosition,
                        TabletopObjectId.Empty,
                        ContainerId.Empty,
                        hitDie.ObjectId);
                    return;
                }

                if (resolvedObjectView is CardView hitCard
                    && TryOpenCardContextMenu(hitCard, screenPosition))
                {
                    return;
                }

                if (resolvedObjectView is PawnView hitPawn && hitPawn.IsBound)
                {
                    selectionState.Select(hitPawn);
                    selectionPresenter.Refresh();
                    OpenPawnContextMenu(screenPosition, hitPawn.ObjectId);
                    return;
                }

                if (resolvedObjectView is TokenView hitToken && hitToken.IsBound)
                {
                    selectionState.Select(hitToken);
                    selectionPresenter.Refresh();
                    OpenTokenContextMenu(screenPosition, hitToken.ObjectId);
                    return;
                }
            }

            if (!dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container
                || !matchState.Containers.TryGetValue(target.ContainerId, out ContainerState container))
            {
                return;
            }

            if (container.Kind == ContainerKind.Deck
                && TryGetDeckPresentation(container.Id, out _, out _))
            {
                OpenContextMenu(
                    PrototypeContextMenuMode.Deck,
                    screenPosition,
                    TabletopObjectId.Empty,
                    container.Id,
                    TabletopObjectId.Empty);
            }
            else if (container.Kind == ContainerKind.Stack
                && stackViewsByContainerId.ContainsKey(container.Id))
            {
                OpenContextMenu(
                    PrototypeContextMenuMode.Stack,
                    screenPosition,
                    TabletopObjectId.Empty,
                    container.Id,
                    TabletopObjectId.Empty);
            }
            else if (container.Kind == ContainerKind.DiscardPile
                && TryGetRuntimeDiscardPile(container.Id, out _))
            {
                OpenContextMenu(
                    PrototypeContextMenuMode.DiscardPile,
                    screenPosition,
                    TabletopObjectId.Empty,
                    container.Id,
                    TabletopObjectId.Empty);
            }
            else if (container.Kind == ContainerKind.ConsoleSlot
                && TryResolveConsolePlacement(container.Id, out _, out _, out _))
            {
                OpenConsoleContextMenu(screenPosition, container.Id);
            }
        }

        private bool CanBeginCameraOrbit(Vector2 screenPosition)
        {
            if (!IsInitialized
                || hitResolver == null
                || (interactionRouter != null && interactionRouter.HasActiveInteraction))
            {
                return false;
            }

            if (hitResolver.TryResolve(screenPosition, out _))
            {
                return false;
            }

            bool canOrbit = dropTargetResolver == null
                || !dropTargetResolver.TryResolve(screenPosition, out CardDropTarget target)
                || target.Kind != CardDropTargetKind.Container;
            if (canOrbit)
            {
                CloseContextMenu();
            }

            return canOrbit;
        }

        private bool TryOpenCardContextMenu(CardView hitCard, Vector2 screenPosition)
        {
            if (hitCard == null || !hitCard.IsBound || hitCard.CardState == null)
            {
                return false;
            }

            if (floormasterLifecycleState?.PendingCard != null
                && floormasterLifecycleState.PendingCard.CardId == hitCard.ObjectId)
            {
                selectionState.Select(hitCard);
                selectionPresenter.Refresh();
                OpenContextMenu(
                    PrototypeContextMenuMode.PendingFloormasterCard,
                    screenPosition,
                    hitCard.ObjectId,
                    ContainerId.Empty,
                    TabletopObjectId.Empty);
                return true;
            }

            if (trapFloorTemplate != null && trapFloorTemplate.IsFloorCard(hitCard.ObjectId))
            {
                if (TrySelectPendingSearchFloor(hitCard.ObjectId))
                {
                    return true;
                }
                OpenContextMenu(
                    PrototypeContextMenuMode.FloorCard,
                    screenPosition,
                    hitCard.ObjectId,
                    ContainerId.Empty,
                    TabletopObjectId.Empty);
                return true;
            }

            ContainerId containerId = hitCard.CardState.BaseState.ContainerId;
            if (containerId.IsEmpty)
            {
                selectionState.Select(hitCard);
                selectionPresenter.Refresh();
                OpenContextMenu(
                    PrototypeContextMenuMode.TabletopCard,
                    screenPosition,
                    hitCard.ObjectId,
                    ContainerId.Empty,
                    TabletopObjectId.Empty);
                return true;
            }

            if (!matchState.Containers.TryGetValue(containerId, out ContainerState container))
            {
                return false;
            }

            if (container.Kind == ContainerKind.Deck
                && TryGetDeckPresentation(container.Id, out _, out _))
            {
                OpenContextMenu(
                    PrototypeContextMenuMode.Deck,
                    screenPosition,
                    hitCard.ObjectId,
                    containerId,
                    TabletopObjectId.Empty);
                return true;
            }

            if (container.Kind == ContainerKind.DiscardPile
                && TryGetRuntimeDiscardPile(containerId, out _))
            {
                OpenContextMenu(
                    PrototypeContextMenuMode.DiscardPile,
                    screenPosition,
                    hitCard.ObjectId,
                    containerId,
                    TabletopObjectId.Empty);
                return true;
            }

            if (container.Kind == ContainerKind.Stack
                && stackViewsByContainerId.ContainsKey(containerId))
            {
                selectionState.Select(hitCard);
                selectionPresenter.Refresh();
                OpenContextMenu(
                    PrototypeContextMenuMode.StackCard,
                    screenPosition,
                    hitCard.ObjectId,
                    containerId,
                    TabletopObjectId.Empty);
                return true;
            }

            selectionState.Select(hitCard);
            selectionPresenter.Refresh();
            OpenContextMenu(
                PrototypeContextMenuMode.ContainedCard,
                screenPosition,
                hitCard.ObjectId,
                containerId,
                TabletopObjectId.Empty);
            return true;
        }

        private void OpenContextMenu(
            PrototypeContextMenuMode mode,
            Vector2 screenPosition,
            TabletopObjectId cardId,
            ContainerId containerId,
            TabletopObjectId dieId)
        {
            contextMenuAnchorScreenPosition = screenPosition;
            contextMenuCardId = cardId;
            contextMenuContainerId = containerId;
            contextMenuDieId = dieId;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuConsoleId = ConsoleId.Empty;
            if (mode == PrototypeContextMenuMode.Deck)
            {
                selectedDrawCount = Mathf.Clamp(
                    selectedDrawCount,
                    1,
                    Math.Max(1, AvailableDrawableCount(containerId)));
            }

            SetContextMenuMode(mode);
        }

        private void OpenPawnContextMenu(Vector2 screenPosition, TabletopObjectId pawnId)
        {
            contextMenuAnchorScreenPosition = screenPosition;
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuContainerId = ContainerId.Empty;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = pawnId;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuConsoleId = ConsoleId.Empty;
            SetContextMenuMode(PrototypeContextMenuMode.Pawn);
        }

        private void OpenTokenContextMenu(Vector2 screenPosition, TabletopObjectId tokenId)
        {
            contextMenuAnchorScreenPosition = screenPosition;
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuContainerId = ContainerId.Empty;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = tokenId;
            contextMenuConsoleId = ConsoleId.Empty;
            SetContextMenuMode(PrototypeContextMenuMode.Token);
        }

        private void OpenConsoleContextMenu(Vector2 screenPosition, ContainerId slotContainerId)
        {
            contextMenuAnchorScreenPosition = screenPosition;
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuContainerId = slotContainerId;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuConsoleId = TryGetPlacedConsoleBySlot(slotContainerId, out ConsoleId consoleId)
                ? consoleId
                : ConsoleId.Empty;
            SetContextMenuMode(PrototypeContextMenuMode.Console);
        }

        private void SetContextMenuMode(PrototypeContextMenuMode mode)
        {
            contextMenuMode = mode;
            if (mode == PrototypeContextMenuMode.None)
            {
                runtimeUi?.CloseTabletopPopup();
                return;
            }

            RenderOpenTabletopPopup();
        }

        private void CloseContextMenu()
        {
            contextMenuMode = PrototypeContextMenuMode.None;
            contextMenuCardId = TabletopObjectId.Empty;
            contextMenuDieId = TabletopObjectId.Empty;
            contextMenuPawnId = TabletopObjectId.Empty;
            contextMenuTokenId = TabletopObjectId.Empty;
            contextMenuContainerId = ContainerId.Empty;
            contextMenuConsoleId = ConsoleId.Empty;
            contextMenuRenderedRevision = -1;
            runtimeUi?.CloseTabletopPopup();
        }

        private void DismissPopupFromSecondary(Vector2 _)
        {
            CloseContextMenu();
        }

        private void RefreshOpenTabletopPopup()
        {
            if (contextMenuMode == PrototypeContextMenuMode.None || !IsInitialized)
            {
                return;
            }

            if (!IsContextMenuTargetAvailable())
            {
                CloseContextMenu();
                return;
            }

            // Physics checkpoints advance the Match even while a uGUI Button owns a press.
            // Keep the Die menu and its Button instance alive until click/acceptance; Roll validates
            // current state through the physical authority. Other menus retain their existing policy.
            if (contextMenuMode != PrototypeContextMenuMode.Die
                && matchState != null && contextMenuRenderedRevision != matchState.Revision)
            {
                CloseContextMenu();
            }
        }

        private void RenderOpenTabletopPopup()
        {
            if (runtimeUi == null || !IsContextMenuTargetAvailable())
            {
                CloseContextMenu();
                return;
            }

            switch (contextMenuMode)
            {
                case PrototypeContextMenuMode.Deck:
                    ShowDeckContextMenu();
                    break;
                case PrototypeContextMenuMode.DrawCards:
                    ShowControllerDrawPopup();
                    break;
                case PrototypeContextMenuMode.CustomDrawCards:
                    ShowCustomControllerDrawPopup();
                    break;
                case PrototypeContextMenuMode.PopulateDeck:
                    ShowPopulateDeckQuantityPopup();
                    break;
                case PrototypeContextMenuMode.TabletopCard:
                    ShowTabletopCardContextMenu();
                    break;
                case PrototypeContextMenuMode.FloorCard:
                    ShowFloorCardContextMenu();
                    break;
                case PrototypeContextMenuMode.PendingFloormasterCard:
                    ShowPendingFloormasterCardContextMenu();
                    break;
                case PrototypeContextMenuMode.StackCard:
                    ShowStackCardContextMenu();
                    break;
                case PrototypeContextMenuMode.ContainedCard:
                    ShowContainedCardContextMenu();
                    break;
                case PrototypeContextMenuMode.Stack:
                    ShowStackContextMenu();
                    break;
                case PrototypeContextMenuMode.MergeDestination:
                    ShowMergeDestinationPopup();
                    break;
                case PrototypeContextMenuMode.Die:
                    ShowDieContextMenu();
                    break;
                case PrototypeContextMenuMode.Pawn:
                    ShowPawnContextMenu();
                    break;
                case PrototypeContextMenuMode.Token:
                    ShowTokenContextMenu();
                    break;
                case PrototypeContextMenuMode.Console:
                    ShowConsoleContextMenu();
                    break;
                case PrototypeContextMenuMode.DiscardPile:
                    ShowDiscardPileContextMenu();
                    break;
                default:
                    CloseContextMenu();
                    return;
            }

            contextMenuRenderedRevision = matchState.Revision;
        }

        private void ShowDeckContextMenu()
        {
            ContainerId targetDeckId = contextMenuContainerId;
            ContainerState targetDeck = matchState.GetContainer(targetDeckId);
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            if (!contextMenuCardId.IsEmpty)
            {
                AddInspectAction(actions, contextMenuCardId);
            }

            AddControllerDrawAction(actions, targetDeckId);
            if (HasLocalHand() && !IsAssistedControllerDeck(targetDeckId) && targetDeck.Count > 0)
            {
                actions.Add(new PrototypePopupActionOption(
                    "Draw to Hand",
                    true,
                    () => OpenDrawToHandPopup(targetDeckId)));
            }

            if (targetDeck.Count == 0)
            {
                actions.Add(new PrototypePopupActionOption(
                    "Populate Deck",
                    true,
                    () =>
                    {
                        selectedQuantity = 1;
                        SetContextMenuMode(PrototypeContextMenuMode.PopulateDeck);
                    }));
            }

            actions.Add(new PrototypePopupActionOption(
                "Shuffle",
                true,
                () =>
                {
                    ShuffleDeckResult result = ShuffleDeck(targetDeckId);
                    if (result.Succeeded)
                    {
                        CloseContextMenu();
                    }
                }));
            actions.Add(new PrototypePopupActionOption(
                "Move",
                true,
                () => BeginContainerMove(targetDeckId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForContainer(targetDeckId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "DECK",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowDiscardPileContextMenu()
        {
            ContainerId targetPileId = contextMenuContainerId;
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            if (!contextMenuCardId.IsEmpty)
            {
                AddInspectAction(actions, contextMenuCardId);
            }

            actions.Add(new PrototypePopupActionOption(
                "Move",
                true,
                () => BeginContainerMove(targetPileId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForContainer(targetPileId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "DISCARD PILE",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void AddDeleteActionIfRuntime(
            List<PrototypePopupActionOption> actions,
            TabletopComponentTarget target)
        {
            bool isTemplateComponent;
            switch (target.Kind)
            {
                case TabletopComponentTargetKind.Object:
                    isTemplateComponent = matchState.IsTemplateObject(target.ObjectId);
                    break;
                case TabletopComponentTargetKind.Container:
                    isTemplateComponent = matchState.IsTemplateContainer(target.ContainerId);
                    break;
                case TabletopComponentTargetKind.Console:
                    isTemplateComponent = false;
                    break;
                default:
                    return;
            }

            if (isTemplateComponent)
            {
                return;
            }

            actions.Add(new PrototypePopupActionOption(
                "Delete",
                true,
                () => DeleteComponent(target)));
        }

        private string DuplicateActionLabel(
            TabletopObjectId sourceObjectId,
            TabletopComponentKind componentKind)
        {
            return matchState.IsTemplateObject(sourceObjectId)
                ? $"Duplicate as Generic {componentKind}"
                : "Duplicate";
        }

        private void DeleteComponent(TabletopComponentTarget target)
        {
            DeleteTabletopComponentResult result = componentDeletionUseCase.Execute(
                matchState,
                activeSession.Request.ActivePlayerIds,
                new DeleteTabletopComponentRequest(CreateCommandContext(), target));
            if (!result.Succeeded)
            {
                string detail = result.Error == DeleteTabletopComponentError.ContainerNotEmpty
                    ? "Container must be empty."
                    : result.Error == DeleteTabletopComponentError.ConsoleNotEmpty
                        ? "Every Console Slot must be empty."
                        : result.Error.ToString();
                ShowMessage($"Delete rejected: {detail}");
                return;
            }

            CloseContextMenu();
            ProjectDeletedComponent(result);
            ShowMessage($"Deleted {result.ComponentKind}.");
        }

        private void ShowControllerDrawPopup()
        {
            ContainerId targetDeckId = contextMenuContainerId;
            int availableCount = AvailableControllerDeckCount(targetDeckId);
            ControllerConfigurationData configuration =
                trapFloorTemplate.GameDefinition.ControllerConfiguration;
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>
            {
                new PrototypePopupActionOption(
                    "Draw 1",
                    availableCount > 0,
                    () => DrawControllerCardsFromContext(targetDeckId, 1)),
                new PrototypePopupActionOption(
                    "Draw 5",
                    availableCount > 0,
                    () => DrawControllerCardsFromContext(targetDeckId, 5)),
                new PrototypePopupActionOption(
                    $"Draw To Hand Limit ({handComfortSettings.MaxHandCards})",
                    availableCount > 0,
                    () => DrawUpToConfiguredHandLimitFromContext(targetDeckId)),
                new PrototypePopupActionOption(
                    "Custom",
                    availableCount > 0,
                    () =>
                    {
                        selectedDrawCount = Mathf.Clamp(
                            selectedDrawCount,
                            1,
                            Math.Max(1, AvailableControllerDeckCount(targetDeckId)));
                        SetContextMenuMode(PrototypeContextMenuMode.CustomDrawCards);
                    }),
                new PrototypePopupActionOption(
                    handComfortSettings.CapLabel + "  -",
                    handComfortSettings.MaxHandCards > PlayerHandComfortSettings.MinimumMaxHandCards,
                    () => ChangeHandComfortCapFromDrawPopup(-1)),
                new PrototypePopupActionOption(
                    handComfortSettings.CapLabel + "  +",
                    true,
                    () => ChangeHandComfortCapFromDrawPopup(1)),
            };

            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "DRAW",
                (availableCount == 1
                    ? "1 Card remaining in Controller Deck."
                    : $"{availableCount} Cards remaining in Controller Deck.")
                    + (configuration != null
                        ? $"\nGame hand limit: {configuration.MaximumHandSize} (advisory)."
                        : string.Empty),
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ChangeHandComfortCapFromDrawPopup(int delta)
        {
            handComfortSettings.MaxHandCards += delta;
            SetContextMenuMode(PrototypeContextMenuMode.DrawCards);
        }

        private void ShowCustomControllerDrawPopup()
        {
            int availableCount = AvailableControllerDeckCount(contextMenuContainerId);
            selectedDrawCount = availableCount > 0
                ? Mathf.Clamp(selectedDrawCount, 1, availableCount)
                : 0;
            runtimeUi.ShowDrawCountPopup(
                contextMenuAnchorScreenPosition,
                selectedDrawCount,
                availableCount,
                () => ChangeSelectedControllerDrawCount(-1),
                () => ChangeSelectedControllerDrawCount(1),
                ConfirmSelectedControllerDrawCount,
                CloseContextMenu,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ChangeSelectedControllerDrawCount(int delta)
        {
            int availableCount = AvailableControllerDeckCount(contextMenuContainerId);
            if (availableCount <= 0)
            {
                selectedDrawCount = 0;
                runtimeUi?.SetDrawCountPopupValue(0, 0);
                return;
            }

            selectedDrawCount = Mathf.Clamp(selectedDrawCount + delta, 1, availableCount);
            runtimeUi?.SetDrawCountPopupValue(selectedDrawCount, availableCount);
        }

        private void ConfirmSelectedControllerDrawCount()
        {
            int availableCount = AvailableControllerDeckCount(contextMenuContainerId);
            if (availableCount <= 0)
            {
                runtimeUi?.SetDrawCountPopupValue(0, 0);
                return;
            }

            DrawControllerCardsFromContext(
                contextMenuContainerId,
                Mathf.Clamp(selectedDrawCount, 1, availableCount));
        }

        private void ShowPopulateDeckQuantityPopup()
        {
            ContainerState deck = matchState.GetContainer(contextMenuContainerId);
            int maximum = deck.Capacity > 0
                ? Math.Min(PopulateDeckUseCase.MaximumQuantity, deck.Capacity)
                : PopulateDeckUseCase.MaximumQuantity;
            selectedQuantity = Mathf.Clamp(selectedQuantity, 1, maximum);
            runtimeUi.ShowQuantityPopup(
                "POPULATE DECK",
                "Create generic blank Cards directly in this empty Deck.",
                "Populate",
                selectedQuantity,
                1,
                maximum,
                () => ChangeSelectedQuantity(-1, maximum),
                () => ChangeSelectedQuantity(1, maximum),
                ConfirmPopulateDeck,
                CloseContextMenu);
        }

        private void ConfirmPopulateDeck()
        {
            ContainerId targetDeckId = contextMenuContainerId;
            if (!matchState.Containers.TryGetValue(targetDeckId, out ContainerState deck))
            {
                ShowMessage("Populate Deck rejected: DeckMissing.");
                CloseContextMenu();
                return;
            }

            int maximum = deck.Capacity > 0
                ? Math.Min(PopulateDeckUseCase.MaximumQuantity, deck.Capacity)
                : PopulateDeckUseCase.MaximumQuantity;
            int quantity = Mathf.Clamp(selectedQuantity, 1, maximum);
            PopulateDeckResult result = populateDeckUseCase.Execute(
                matchState,
                activeSession.Request.ActivePlayerIds,
                new PopulateDeckRequest(CreateCommandContext(), targetDeckId, quantity));
            if (!result.Succeeded)
            {
                ShowMessage($"Populate Deck rejected: {result.Error}.");
                CloseContextMenu();
                return;
            }

            ProjectPopulatedDeckCards(targetDeckId, result.CardIds);
            ShowMessage($"Populated Deck with {quantity} generic Cards.");
            CloseContextMenu();
        }

        private void ShowTabletopCardContextMenu()
        {
            TabletopObjectId targetCardId = contextMenuCardId;
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            AddInspectAction(actions, targetCardId);
            actions.Add(new PrototypePopupActionOption(
                    "Flip",
                    true,
                    () => FlipContextCard(targetCardId)));
            actions.Add(new PrototypePopupActionOption(
                DuplicateActionLabel(targetCardId, TabletopComponentKind.Card),
                true,
                () => BeginDuplicatePlacement(targetCardId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetCardId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "CARD",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void FlipContextCard(TabletopObjectId targetCardId)
        {
            if (!TryGetCardView(targetCardId, out CardView targetCardView))
            {
                CloseContextMenu();
                return;
            }

            FlipInteractionResult result = flipCoordinator.Flip(targetCardView);
            ShowMessage(result.Succeeded ? "Card flipped." : $"Flip rejected: {result.Status}.");
            if (result.Succeeded)
            {
                CloseContextMenu();
            }
        }

        private void ShowPendingFloormasterCardContextMenu()
        {
            TrapFloorPendingFloormasterCard pendingCard = floormasterLifecycleState.PendingCard;
            bool canComplete = trapFloorRoundState != null
                && trapFloorRoundState.Phase == TrapFloorRoundPhase.Trigger;
            TabletopObjectId targetCardId = pendingCard.CardId;
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "PENDING FLOORMASTER CARD",
                $"Category: {pendingCard.Category}\nCarry out its effect at the table, then mark it done.",
                new[]
                {
                    new PrototypePopupActionOption(
                        "Inspect",
                        true,
                        () => OpenCardInspect(targetCardId)),
                    new PrototypePopupActionOption(
                        "Card effect done",
                        canComplete,
                        () =>
                        {
                            TrapFloorRoundTriggerResult result = CompletePendingFloormasterTriggerPrototype();
                            if (result.Succeeded)
                            {
                                CloseContextMenu();
                            }
                        }),
                    new PrototypePopupActionOption(
                        "Duplicate as Generic Card",
                        true,
                        () => BeginDuplicatePlacement(targetCardId)),
                },
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowFloorCardContextMenu()
        {
            if (!trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    contextMenuCardId,
                    out TrapFloorFloorCardState floorCard))
            {
                CloseContextMenu();
                return;
            }

            TabletopObjectId targetCardId = contextMenuCardId;
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            bool isCollapsed = trapFloorCollapseState != null
                && trapFloorCollapseState.IsCollapsed(targetCardId);
            if (floorCard.IsRevealed
                && floorCard.Content.HasSupportedAssistedTrapEffect
                && IsTrapFloorTrapPending(targetCardId))
            {
                actions.Add(new PrototypePopupActionOption(
                    "Resolve Trap",
                    CanResolveTrapFloorTrap(targetCardId),
                    () => ResolveTrapFloorTrap(targetCardId)));
            }
            else if (trapFloorObjectiveState != null && trapFloorObjectiveUseCase != null)
            {
                if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                    && !trapFloorObjectiveState.TryGetClaim(targetCardId, out _))
                {
                    actions.Add(new PrototypePopupActionOption(
                        "Claim Key",
                        !trapFloorObjectiveState.IsWon,
                        () => ClaimTrapFloorKey(targetCardId)));
                }
                else if (floorCard.Content.Category == TrapFloorFloorContentCategory.SecretExit)
                {
                    actions.Add(new PrototypePopupActionOption(
                        "Attempt Escape",
                        !trapFloorObjectiveState.IsWon,
                        () => AttemptTrapFloorEscape(targetCardId)));
                }
            }

            actions.Add(new PrototypePopupActionOption(
                "Inspect",
                true,
                () => OpenCardInspect(targetCardId)));
            actions.Add(new PrototypePopupActionOption(
                "Duplicate as Generic Card",
                true,
                () => BeginDuplicatePlacement(targetCardId)));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                $"FLOOR {floorCard.Coordinate}",
                isCollapsed
                    ? $"COLLAPSED HOLE — Floor {floorCard.Coordinate} is permanently unusable."
                    : floorCard.IsRevealed
                    ? FloorCardContextDescription(floorCard)
                    : "MYSTERY — content is hidden until Search is accepted.",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private string FloorCardContextDescription(TrapFloorFloorCardState floorCard)
        {
            string description =
                $"Revealed: {floorCard.Content.Category} — {floorCard.Content.DisplayName}";
            if (floorCard.Content.Category == TrapFloorFloorContentCategory.Trap)
            {
                if (WasTrapSafelyRevealed(floorCard.ObjectId))
                {
                    return $"{description}\nCAREFUL SEARCH — this reveal did not trigger the Trap; it remains a Trap.";
                }
                if (IsTrapFloorTrapNeutralized(floorCard.ObjectId))
                {
                    return $"{description}\nTRAP NEUTRALIZED — this Floor is safe from assisted Trap consequences.";
                }
                if (IsTrapFloorTrapResolved(floorCard.ObjectId))
                {
                    return $"{description}\nTRAP RESOLVED — its assisted consequence has already been applied or prevented.";
                }
                return $"{description}\n{TrapFloorTrapEffectDescription(floorCard.Content)}";
            }
            if (trapFloorObjectiveState == null)
            {
                return description;
            }

            if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key)
            {
                return trapFloorObjectiveState.TryGetClaim(
                        floorCard.ObjectId,
                        out TrapFloorCollectedKeyState claim)
                    ? $"{description}\nKEY STATUS: CLAIMED by {FormatPlayerName(claim.ClaimedByPlayerId)}."
                    : $"{description}\nKEY STATUS: UNCLAIMED — use Claim Key to collect it.";
            }

            if (floorCard.Content.Category == TrapFloorFloorContentCategory.SecretExit)
            {
                return $"{description}\nKEYS {trapFloorObjectiveState.CollectedKeyCount} / "
                    + trapFloorObjectiveState.RequiredKeyCount;
            }

            return description;
        }

        private bool CanResolveTrapFloorTrap(TabletopObjectId floorCardId)
        {
            return trapFloorTurnState != null
                && trapFloorTurnService != null
                && trapFloorAbilityResolutionService != null
                && trapFloorAbilityResolutionState != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && trap.IsPending
                && trap.AffectedPlayerId == trapFloorTurnState.ActivePlayerId;
        }

        private static string TrapFloorTrapEffectDescription(
            TrapFloorFloorContentDefinition content)
        {
            switch (content.TrapEffect)
            {
                case TrapFloorTrapEffectCategory.EliminateForCurrentRound:
                    return "Assisted consequence: eliminate the resolving Player for the current Round.";
                case TrapFloorTrapEffectCategory.BlindNextRound:
                    return "Assisted consequence: no Controller Card draw next Round; roll a d4 for movement direction.";
                case TrapFloorTrapEffectCategory.SlowNextRound:
                    return $"Assisted consequence: movement {FormatSignedModifier(content.MovementModifier.Value)} next Round.";
                case TrapFloorTrapEffectCategory.StickyNextRound:
                    return "Assisted consequence: stay in place during the next Round.";
                default:
                    return "Manual consequence: follow the authored Trap text.";
            }
        }

        private void ResolveTrapFloorTrap(TabletopObjectId floorCardId)
        {
            if (!CanResolveTrapFloorTrap(floorCardId)
                || !trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    floorCardId,
                    out TrapFloorFloorCardState floorCard)
                || !floorCard.IsRevealed
                || !floorCard.Content.HasSupportedAssistedTrapEffect)
            {
                ShowMessage("Resolve Trap is unavailable for this Trap or assistance state.");
                return;
            }

            PlayerId affectedPlayerId = trapFloorTurnState.ActivePlayerId;
            TrapFloorTurnAdvanceResult result = trapFloorAbilityResolutionService.ResolveSupportedTrap(
                matchState,
                CreateCommandContext(affectedPlayerId),
                floorCardId,
                trapFloorTurnService);
            if (!result.Succeeded)
            {
                ShowMessage($"Resolve Trap rejected: {result.Error}.");
                return;
            }

            CloseContextMenu();
            CloseCardInspect();
            RefreshTrapFloorStatusUi();
            if (floorCard.Content.TrapEffect == TrapFloorTrapEffectCategory.BlindNextRound)
            {
                ShowMessage(
                    $"{FormatPlayerShortName(affectedPlayerId)} will be BLIND during "
                    + $"Round {trapFloorTurnState.CurrentRound + 1}.");
            }
            else if (floorCard.Content.TrapEffect == TrapFloorTrapEffectCategory.SlowNextRound)
            {
                ShowMessage(
                    $"{FormatPlayerShortName(affectedPlayerId)} will be SLOWED "
                    + $"during Round {trapFloorTurnState.CurrentRound + 1}.");
            }
            else if (floorCard.Content.TrapEffect == TrapFloorTrapEffectCategory.StickyNextRound)
            {
                ShowMessage(
                    $"{FormatPlayerShortName(affectedPlayerId)} will be STICKY "
                    + $"during Round {trapFloorTurnState.CurrentRound + 1}.");
            }
            else
            {
                ShowMessage($"{FormatPlayerName(affectedPlayerId).ToUpperInvariant()} ELIMINATED");
            }
        }

        private void BeginTrapFloorSearch(TrapFloorSearchKind searchKind)
        {
            if (trapFloorRevealFloorUseCase == null
                || trapFloorTurnState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                ShowMessage("Search is available only during an active Player turn.");
                return;
            }
            if (trapFloorPendingSearchState != null && trapFloorPendingSearchState.IsActive)
            {
                ShowMessage("Complete or cancel the current Search first.");
                return;
            }

            pendingSearchKind = searchKind;
            if (!TryBuildSearchPaymentOptions(
                    out TrapFloorSearchConfiguration configuration,
                    out _,
                    out List<SearchPaymentCardOption> options))
            {
                pendingSearchKind = default;
                ShowMessage("Search assistance is unavailable because its authored configuration is invalid.");
                return;
            }

            ShowFocusedSearchHand(configuration, options);
        }

        private void SearchSelectedTrapFloorCard()
        {
            BeginTrapFloorSearch(TrapFloorSearchKind.Normal);
        }

        private void CarefulSearchSelectedTrapFloorCard()
        {
            BeginTrapFloorSearch(TrapFloorSearchKind.Careful);
        }

        private void ShowFocusedSearchHand(
            TrapFloorSearchConfiguration configuration,
            IReadOnlyList<SearchPaymentCardOption> options)
        {
            List<PrototypeFocusedCardOptionModel> cards =
                new List<PrototypeFocusedCardOptionModel>(options.Count);
            bool hasEligible = false;
            for (int i = 0; i < options.Count; i++)
            {
                SearchPaymentCardOption option = options[i];
                hasEligible |= option.Eligible;
                cards.Add(new PrototypeFocusedCardOptionModel(
                    option.CardId,
                    option.DisplayName,
                    option.Input.HasValue ? option.Input.Value.ToString() : "Not eligible",
                    option.Artwork,
                    option.Eligible));
            }

            string missing = pendingSearchKind == TrapFloorSearchKind.Careful
                ? MissingCarefulSearchRequirements(configuration, options)
                : string.Empty;
            string status;
            if (pendingSearchKind == TrapFloorSearchKind.Normal && !hasEligible)
            {
                status = "Search requires A, B, X, or Y.";
            }
            else if (!string.IsNullOrEmpty(missing))
            {
                status = $"Missing: {missing}";
            }
            else
            {
                status = pendingSearchKind == TrapFloorSearchKind.Careful
                    ? "Select exact A, B, X, and Y Card instances."
                    : "Select one eligible Card instance.";
            }

            runtimeUi.ShowFocusedCardSelection(new PrototypeFocusedCardSelectionModel(
                pendingSearchKind == TrapFloorSearchKind.Careful
                    ? "CAREFUL SEARCH — HAND"
                    : "SEARCH — HAND",
                pendingSearchKind == TrapFloorSearchKind.Careful
                    ? "Choose A + B + X + Y from the active Player's Hand."
                    : "Choose any one A, B, X, or Y from the active Player's Hand.",
                status,
                "Confirm Cards",
                cards,
                pendingSearchKind == TrapFloorSearchKind.Careful ? 4 : 1,
                selection => IsSearchPaymentReady(configuration, options, selection),
                ConfirmFocusedSearchPayment,
                CancelFocusedTrapFloorSearch));
        }

        private void ConfirmFocusedSearchPayment(IReadOnlyList<TabletopObjectId> paymentCardIds)
        {
            if (!TryBuildSearchPaymentOptions(
                    out TrapFloorSearchConfiguration configuration,
                    out _,
                    out List<SearchPaymentCardOption> options)
                || !IsSearchPaymentReady(configuration, options, paymentCardIds))
            {
                ShowMessage("Search payment is incomplete.");
                return;
            }

            TrapFloorSearchKind searchKind = pendingSearchKind;
            trapFloorPendingSearchState.Begin(
                trapFloorTurnState.ActivePlayerId,
                searchKind,
                paymentCardIds);
            pendingSearchKind = default;
            runtimeUi?.CloseFocusedCardSelection();
            ReplaceCurrentUndoStateForSearchAssistance();
            RefreshTrapFloorStatusUi();
            ShowMessage(searchKind == TrapFloorSearchKind.Careful
                ? "CAREFUL SEARCH — Place A + B + X + Y in your Console"
                : "SEARCH — Place the selected Card in your Console");
        }

        private void ConfirmTrapFloorSearch(TabletopObjectId floorCardId)
        {
            if (trapFloorPendingSearchState == null
                || !trapFloorPendingSearchState.IsActive
                || !trapFloorPendingSearchState.IsPaymentComplete
                || trapFloorPendingSearchState.PlayerId != trapFloorTurnState.ActivePlayerId)
            {
                ShowMessage("Search is not armed. Place the selected Card payment in your Console.");
                return;
            }

            TrapFloorSearchKind searchKind = trapFloorPendingSearchState.SearchKind;
            List<TabletopObjectId> payment =
                new List<TabletopObjectId>(trapFloorPendingSearchState.SelectedCardIds);
            TrapFloorRevealFloorResult result = trapFloorRevealFloorUseCase.Execute(
                matchState,
                new TrapFloorRevealFloorCommand(
                    CreateCommandContext(trapFloorTurnState.ActivePlayerId),
                    floorCardId,
                    searchKind,
                    payment));
            if (!result.Succeeded)
            {
                ShowMessage($"Search rejected: {result.Error}.");
                return;
            }

            trapFloorPendingSearchState.Clear();
            ReplaceCurrentUndoStateForSearchAssistance();
            if (TryGetCardView(floorCardId, out CardView floorCardView))
                floorCardView.ApplyAcceptedState();
            RefreshCardContentVisibility();
            selectionPresenter?.Refresh();
            activeFloorRevealActivity = result.RevealedActivity;
            ShowFocusedSearchReveal(result);
            if (result.TrapSuppressed)
            {
                ShowMessage(
                    $"{FormatPlayerShortName(result.RevealedActivity.ActorPlayerId)} revealed "
                    + $"{result.FloorCard.Content.DisplayName} safely.");
            }
            else if (result.SearchKind == TrapFloorSearchKind.Careful)
            {
                ShowMessage(
                    $"{FormatPlayerShortName(result.RevealedActivity.ActorPlayerId)} carefully searched "
                    + $"Floor {result.FloorCard.Coordinate}.");
            }
            else
            {
                ShowMessage(
                    $"{FormatPlayerShortName(result.RevealedActivity.ActorPlayerId)} searched "
                    + $"Floor {result.FloorCard.Coordinate} using {result.SearchedActivity.PaymentInputs[0]}.");
            }
        }

        private void ShowFocusedSearchReveal(TrapFloorRevealFloorResult result)
        {
            if (runtimeUi == null
                || matchState == null
                || trapFloorTemplate == null
                || !matchState.Cards.TryGetValue(
                    result.FloorCard.ObjectId,
                    out CardInstanceState card)
                || card.Face != CardFace.FaceUp
                || !trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    result.FloorCard.ObjectId,
                    out TrapFloorFloorCardState revealedFloor)
                || !revealedFloor.IsRevealed)
            {
                ShowMessage("Search resolved, but the revealed Card presentation is unavailable.");
                return;
            }

            Texture backArtwork = null;
            Texture frontArtwork = null;
            if (TryGetAuthoredCardDefinition(
                    card.BaseState.DefinitionId,
                    out CardDefinition definition))
            {
                backArtwork = definition.BackArtwork;
                frontArtwork = definition.FrontArtwork;
            }

            string body = result.TrapSuppressed
                ? $"{revealedFloor.Content.DisplayText}\n\nREVEALED SAFELY"
                : revealedFloor.Content.DisplayText;
            runtimeUi.ShowFocusedCardReveal(new PrototypeFocusedCardRevealModel(
                revealedFloor.ObjectId,
                backArtwork,
                frontArtwork,
                "SEARCH RESULT",
                $"{revealedFloor.Content.Category.ToString().ToUpperInvariant()} • REVEALED",
                revealedFloor.Content.DisplayName,
                revealedFloor.Content.DisplayText,
                body,
                "Return to Tabletop",
                CloseFocusedSearchReveal));
        }

        private void CancelFocusedTrapFloorSearch()
        {
            pendingSearchKind = default;
            runtimeUi?.CloseFocusedCardSelection();
            if (trapFloorPendingSearchState != null && trapFloorPendingSearchState.IsActive)
            {
                trapFloorPendingSearchState.Clear();
                ReplaceCurrentUndoStateForSearchAssistance();
                RefreshTrapFloorStatusUi();
                ShowMessage("Search assistance cancelled. Cards remain where the Player placed them.");
            }
        }

        private void CloseFocusedSearchReveal()
        {
            runtimeUi?.CloseFocusedCardSelection();
        }

        private void ReplaceCurrentUndoStateForSearchAssistance()
        {
            if (undoTrackedMatch != matchState || undoHistory.CurrentStateIndex < 0) return;
            undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
            RefreshUndoUi();
        }

        private void ReplaceCurrentUndoStateForPurchaseAssistance()
        {
            if (undoTrackedMatch != matchState || undoHistory.CurrentStateIndex < 0) return;
            undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
            RefreshUndoUi();
        }

        private bool TryBuildSearchPaymentOptions(
            out TrapFloorSearchConfiguration configuration,
            out ContainerState hand,
            out List<SearchPaymentCardOption> options)
        {
            configuration = null;
            options = new List<SearchPaymentCardOption>();
            hand = null;
            if (trapFloorTemplate == null
                || trapFloorTurnState == null
                || !TrapFloorSearchConfiguration.TryCreate(
                    trapFloorTemplate.GameDefinition,
                    out configuration)
                || !ControllerInputCardCatalog.TryCreate(
                    trapFloorTemplate.GameDefinition,
                    out ControllerInputCardCatalog catalog))
                return false;

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (!matchState.Containers.TryGetValue(player.HandContainerId, out hand)
                || hand.Kind != ContainerKind.Hand)
                return false;

            for (int i = 0; i < hand.Count; i++)
            {
                TabletopObjectId cardId = hand.GetObjectAt(i);
                if (!matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) return false;
                ControllerInput? input = catalog.TryGetInput(
                    card.BaseState.DefinitionId,
                    out ControllerInput resolvedInput)
                    ? resolvedInput
                    : (ControllerInput?)null;
                bool eligible = input.HasValue
                    && (pendingSearchKind == TrapFloorSearchKind.Normal
                        ? configuration.IsNormalSearchInput(input.Value)
                        : configuration.RequiredCarefulCount(input.Value) > 0);
                CardDefinition authoredDefinition = null;
                string displayName = TryGetAuthoredCardDefinition(
                        card.BaseState.DefinitionId,
                        out authoredDefinition)
                    ? authoredDefinition.DisplayName
                    : "Controller Card";
                options.Add(new SearchPaymentCardOption(
                    cardId,
                    input,
                    displayName,
                    authoredDefinition != null ? authoredDefinition.FrontArtwork : null,
                    eligible));
            }
            return true;
        }

        private bool TrySelectPendingSearchFloor(TabletopObjectId floorCardId)
        {
            if (trapFloorPendingSearchState == null
                || !trapFloorPendingSearchState.IsActive
                || !trapFloorPendingSearchState.IsPaymentComplete
                || !trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    floorCardId,
                    out TrapFloorFloorCardState floor)
                || floor.IsRevealed
                || (trapFloorCollapseState != null
                    && trapFloorCollapseState.IsCollapsed(floorCardId)))
            {
                return false;
            }

            ConfirmTrapFloorSearch(floorCardId);
            return true;
        }

        private static string MissingCarefulSearchRequirements(
            TrapFloorSearchConfiguration configuration,
            IReadOnlyList<SearchPaymentCardOption> options)
        {
            List<string> missing = new List<string>();
            for (int requirementIndex = 0;
                requirementIndex < configuration.CarefulRequirements.Count;
                requirementIndex++)
            {
                InputRequirementData requirement = configuration.CarefulRequirements[requirementIndex];
                int available = 0;
                for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
                {
                    if (options[optionIndex].Input == requirement.Input) available++;
                }
                if (available < requirement.Count) missing.Add(requirement.Input.ToString());
            }
            return string.Join(", ", missing);
        }

        private bool IsSearchPaymentReady(
            TrapFloorSearchConfiguration configuration,
            IReadOnlyList<SearchPaymentCardOption> options,
            IReadOnlyList<TabletopObjectId> selectedCardIds)
        {
            if (pendingSearchKind == TrapFloorSearchKind.Normal)
            {
                if (selectedCardIds.Count != 1) return false;
                for (int i = 0; i < options.Count; i++)
                    if (options[i].CardId == selectedCardIds[0]) return options[i].Eligible;
                return false;
            }

            int requiredTotal = 0;
            for (int requirementIndex = 0;
                requirementIndex < configuration.CarefulRequirements.Count;
                requirementIndex++)
            {
                InputRequirementData requirement = configuration.CarefulRequirements[requirementIndex];
                requiredTotal += requirement.Count;
                int selected = 0;
                for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
                {
                    SearchPaymentCardOption option = options[optionIndex];
                    if (option.Input == requirement.Input
                        && ContainsCardId(selectedCardIds, option.CardId))
                        selected++;
                }
                if (selected != requirement.Count) return false;
            }
            return selectedCardIds.Count == requiredTotal;
        }

        private static bool ContainsCardId(
            IReadOnlyList<TabletopObjectId> cardIds,
            TabletopObjectId cardId)
        {
            for (int i = 0; i < cardIds.Count; i++)
                if (cardIds[i] == cardId) return true;
            return false;
        }

        private void ClaimTrapFloorKey(TabletopObjectId floorCardId)
        {
            if (trapFloorObjectiveUseCase == null)
            {
                ShowMessage("Claim Key unavailable outside Trap Floor.");
                return;
            }

            TrapFloorObjectiveResult result = trapFloorObjectiveUseCase.ClaimKey(
                matchState,
                new TrapFloorClaimKeyCommand(CreateCommandContext(), floorCardId));
            if (!result.Succeeded)
            {
                ShowMessage($"Claim Key rejected: {result.Error}.");
                return;
            }

            bool keepInspectOpen = inspectedCardId == floorCardId;
            if (!keepInspectOpen)
            {
                CloseContextMenu();
            }

            RefreshTrapFloorFloorCardPresentation(floorCardId);
            RefreshTrapFloorStatusUi();
            if (keepInspectOpen)
            {
                RefreshCardInspectPopup();
            }

            ShowMessage(
                $"{FormatPlayerName(result.Activity.ActorPlayerId)} claimed "
                + $"{result.FloorCard.Content.DisplayName}. "
                + $"KEYS {result.CollectedKeyCount} / {result.RequiredKeyCount}.");
        }

        private void AttemptTrapFloorEscape(TabletopObjectId floorCardId)
        {
            if (trapFloorObjectiveUseCase == null)
            {
                ShowMessage("Attempt Escape unavailable outside Trap Floor.");
                return;
            }

            TrapFloorObjectiveResult result = trapFloorObjectiveUseCase.AttemptEscape(
                matchState,
                new TrapFloorAttemptEscapeCommand(CreateCommandContext(), floorCardId));
            if (!result.Succeeded)
            {
                if (result.Error == TrapFloorObjectiveError.RequiredKeysMissing)
                {
                    ShowMessage(
                        $"Attempt Escape rejected: Need {result.RequiredKeyCount} Keys — "
                        + $"{result.CollectedKeyCount} claimed.");
                }
                else if (result.Error == TrapFloorObjectiveError.PlayerAlreadyEscaped)
                {
                    ShowMessage("Attempt Escape rejected: This Player has already escaped.");
                }
                else
                {
                    ShowMessage($"Attempt Escape rejected: {result.Error}.");
                }

                return;
            }

            if (result.IsWon)
            {
                ShowTrapFloorVictory(result);
            }
            else
            {
                CloseContextMenu();
            }
            RefreshTrapFloorStatusUi();
            string escapedPlayer = FormatPlayerName(result.EscapedActivity.ActorPlayerId);
            ShowMessage(result.IsWon
                ? $"{escapedPlayer} escaped. TRAP FLOOR VICTORY."
                : $"{escapedPlayer} escaped. ESCAPED {result.EscapedPlayerCount} / "
                    + $"{result.ParticipatingPlayerCount}.");
        }

        private void ShowTrapFloorVictory(TrapFloorObjectiveResult result)
        {
            Vector2 popupPosition = contextMenuAnchorScreenPosition;
            CloseContextMenu();
            Action closeVictory = () => runtimeUi?.CloseTabletopPopup();
            runtimeUi.ShowContextMenu(
                popupPosition,
                "TRAP FLOOR VICTORY",
                $"{FormatPlayerName(result.EscapedActivity.ActorPlayerId)} escaped through "
                    + $"Floor {result.FloorCard.Coordinate}.\n"
                    + $"{CurrentTrapFloorObjectiveProgressText()}",
                new[]
                {
                    new PrototypePopupActionOption("Close", true, closeVictory),
                },
                closeVictory,
                _ => closeVictory());
        }

        private void ShowTrapFloorReveal(TrapFloorRevealFloorResult result)
        {
            CloseContextMenu();
            CloseCardInspect();
            activeFloorRevealActivity = result.RevealedActivity;
            inspectedCardId = result.FloorCard.ObjectId;
            inspectedCardRenderedRevision = result.Revision;
            if (!TryBuildCardInspectModel(inspectedCardId, out PrototypeCardInspectModel model))
            {
                activeFloorRevealActivity = null;
                inspectedCardId = TabletopObjectId.Empty;
                inspectedCardRenderedRevision = -1;
                return;
            }

            runtimeUi.ShowCardInspect(model, CloseCardInspect);
        }

        private void ShowStackCardContextMenu()
        {
            TabletopObjectId targetCardId = contextMenuCardId;
            ContainerId targetStackId = contextMenuContainerId;
            ContainerState stack = matchState.GetContainer(targetStackId);
            int index = stack.IndexOf(targetCardId);
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            AddInspectAction(actions, targetCardId);
            if (index < stack.Count - 1)
            {
                actions.Add(new PrototypePopupActionOption(
                    "Move Up",
                    true,
                    () => ReorderContextStackCard(targetCardId, targetStackId, 1)));
            }

            if (index > 0)
            {
                actions.Add(new PrototypePopupActionOption(
                    "Move Down",
                    true,
                    () => ReorderContextStackCard(targetCardId, targetStackId, -1)));
            }

            actions.Add(new PrototypePopupActionOption(
                "Move",
                true,
                () => BeginContainerMove(targetStackId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetCardId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "CARD",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ReorderContextStackCard(
            TabletopObjectId targetCardId,
            ContainerId targetStackId,
            int offset)
        {
            if (!TryGetCardView(targetCardId, out CardView targetCardView)
                || targetCardView.CardState.BaseState.ContainerId != targetStackId)
            {
                CloseContextMenu();
                return;
            }

            ReorderContainerResult result = MoveCardInContainer(targetCardView, targetStackId, offset);
            if (result.Succeeded)
            {
                CloseContextMenu();
            }
        }

        private void ShowContainedCardContextMenu()
        {
            TabletopObjectId targetCardId = contextMenuCardId;
            ContainerState container = matchState.GetContainer(contextMenuContainerId);
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            if (!IsHiddenHandContainer(container))
            {
                AddInspectAction(actions, targetCardId);
            }

            if (container.Kind == ContainerKind.ConsoleSlot
                && TryResolveConsolePlacement(container.Id, out _, out _, out _))
            {
                ContainerId targetSlotContainerId = container.Id;
                actions.Add(new PrototypePopupActionOption(
                    "Move",
                    true,
                    () => BeginConsoleMove(targetSlotContainerId)));
            }

            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "CARD",
                $"In {FormatContainerKind(container.Kind)}",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private static string FormatContainerKind(ContainerKind kind)
        {
            return kind == ContainerKind.ConsoleSlot
                ? "Console Slot"
                : kind == ContainerKind.DiscardPile ? "Discard Pile" : kind.ToString();
        }

        private void AddInspectAction(
            List<PrototypePopupActionOption> actions,
            TabletopObjectId targetCardId)
        {
            actions.Add(new PrototypePopupActionOption(
                "Inspect",
                true,
                () => OpenCardInspect(targetCardId)));
        }

        private void OpenCardInspect(TabletopObjectId targetCardId)
        {
            activeFloorRevealActivity = null;
            if (!TryBuildCardInspectModel(targetCardId, out PrototypeCardInspectModel model))
            {
                ShowMessage("Inspect unavailable: Card Presentation is no longer available.");
                CloseContextMenu();
                return;
            }

            CloseContextMenu();
            inspectedCardId = targetCardId;
            inspectedCardRenderedRevision = matchState.Revision;
            runtimeUi.ShowCardInspect(model, CloseCardInspect);
        }

        private void CloseCardInspect()
        {
            activeFloorRevealActivity = null;
            inspectedCardId = TabletopObjectId.Empty;
            inspectedCardRenderedRevision = -1;
            runtimeUi?.CloseCardInspect();
        }

        private void RefreshCardInspectPopup()
        {
            if (inspectedCardId.IsEmpty || !IsInitialized)
            {
                return;
            }

            if (!TryBuildCardInspectModel(inspectedCardId, out PrototypeCardInspectModel model))
            {
                CloseCardInspect();
                return;
            }

            if (inspectedCardRenderedRevision != matchState.Revision)
            {
                runtimeUi.RefreshCardInspect(model);
                inspectedCardRenderedRevision = matchState.Revision;
            }
        }

        private bool TryBuildCardInspectModel(
            TabletopObjectId targetCardId,
            out PrototypeCardInspectModel model)
        {
            if (matchState == null
                || !matchState.Cards.TryGetValue(targetCardId, out CardInstanceState card)
                || !TryGetCardVisualReferences(targetCardId, out PrototypeCardVisualReferences visualReferences))
            {
                model = null;
                return false;
            }

            if (trapFloorTemplate != null
                && trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    targetCardId,
                    out TrapFloorFloorCardState floorCard))
            {
                if (trapFloorCollapseState != null
                    && trapFloorCollapseState.IsCollapsed(targetCardId))
                {
                    Color holeColor = new Color(0.025f, 0.03f, 0.04f);
                    model = new PrototypeCardInspectModel(
                        $"Collapsed Floor {floorCard.Coordinate} | {targetCardId}",
                        card.Face,
                        new PrototypeCardInspectSideModel(
                            "HOLE",
                            $"Floor {floorCard.Coordinate} is permanently collapsed and unusable.",
                            null,
                            holeColor,
                            Color.white),
                        new PrototypeCardInspectSideModel(
                            "HOLE",
                            $"Floor {floorCard.Coordinate} is permanently collapsed and unusable.",
                            null,
                            holeColor,
                            Color.white),
                        false);
                    return true;
                }

                TrapFloorActivityEntry revealActivity = activeFloorRevealActivity != null
                    && activeFloorRevealActivity.FloorCardId == targetCardId
                        ? activeFloorRevealActivity
                        : null;
                string revealContext = revealActivity == null
                    ? string.Empty
                    : $"Revealed by: {FormatPlayerName(revealActivity.ActorPlayerId)}\n"
                        + $"Floor coordinate: {floorCard.Coordinate}\n";
                string objectiveContext = string.Empty;
                string frontTitle = floorCard.Content.DisplayName;
                string inputCostContext = TryGetAuthoredCardDefinition(
                        card.BaseState.DefinitionId,
                        out CardDefinition floorCardDefinition)
                    ? FormatInputCost(floorCardDefinition.InputCost)
                    : string.Empty;
                PrototypePopupActionOption? primaryAction = null;
                if (floorCard.Content.Category == TrapFloorFloorContentCategory.Trap)
                {
                    bool safelyRevealed = WasTrapSafelyRevealed(targetCardId);
                    bool neutralized = IsTrapFloorTrapNeutralized(targetCardId);
                    objectiveContext = safelyRevealed
                        ? "CAREFUL SEARCH — Trap did not trigger during this reveal.\nThe Floor remains a Trap.\n"
                        : neutralized
                        ? "TRAP NEUTRALIZED\nThis Floor is safe from assisted Trap consequences.\n"
                        : TrapFloorTrapEffectDescription(floorCard.Content) + "\n";
                    if (safelyRevealed)
                    {
                        frontTitle = $"{floorCard.Content.DisplayName} — REVEALED SAFELY";
                    }
                    else if (neutralized)
                    {
                        frontTitle = $"{floorCard.Content.DisplayName} — SAFE";
                    }
                    else if (IsTrapFloorTrapResolved(targetCardId))
                    {
                        frontTitle = $"{floorCard.Content.DisplayName} — RESOLVED";
                        objectiveContext = "TRAP RESOLVED\nIts assisted consequence has already been applied or prevented.\n";
                    }
                    else if (floorCard.Content.HasSupportedAssistedTrapEffect)
                    {
                        primaryAction = new PrototypePopupActionOption(
                            "Resolve Trap",
                            CanResolveTrapFloorTrap(targetCardId),
                            () => ResolveTrapFloorTrap(targetCardId));
                    }
                }
                else if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                    && trapFloorObjectiveState != null)
                {
                    if (trapFloorObjectiveState.TryGetClaim(
                            targetCardId,
                            out TrapFloorCollectedKeyState claim))
                    {
                        frontTitle = "KEY — CLAIMED";
                        objectiveContext = $"Key status: CLAIMED\n"
                            + $"Claimed by: {FormatPlayerName(claim.ClaimedByPlayerId)}\n";
                    }
                    else
                    {
                        frontTitle = "KEY — UNCLAIMED";
                        objectiveContext = "Key status: UNCLAIMED\n"
                            + "Claim this Key before attempting to escape.\n";
                        primaryAction = new PrototypePopupActionOption(
                            "Claim Key",
                            !trapFloorObjectiveState.IsWon,
                            () => ClaimTrapFloorKey(targetCardId));
                    }
                }

                string frontBody = $"{revealContext}{objectiveContext}"
                    + $"Category: {floorCard.Content.Category}\n"
                    + $"Content: {floorCard.Content.DisplayName}\n\n"
                    + floorCard.Content.DisplayText
                    + inputCostContext;
                model = new PrototypeCardInspectModel(
                    $"Floor {floorCard.Coordinate} | {targetCardId}",
                    card.Face,
                    new PrototypeCardInspectSideModel(
                        frontTitle,
                        frontBody,
                        null,
                        TrapFloorContentColor(floorCard.Content.Category),
                        new Color(0.04f, 0.06f, 0.08f)),
                    new PrototypeCardInspectSideModel(
                        "MYSTERY",
                        $"Floor {floorCard.Coordinate}\nContent remains hidden until Search.",
                        null,
                        new Color(0.10f, 0.19f, 0.42f),
                        Color.white),
                    false,
                    primaryAction);
                return true;
            }

            Color frontSurface = IsButtonCard(card)
                ? new Color(0.58f, 0.88f, 0.82f)
                : new Color(0.95f, 0.88f, 0.42f);
            bool contentVisible = ShouldShowCardContent(card);
            string obscuredContent = "Content is currently obscured by its Container.";
            bool hasAuthoredDefinition = TryGetAuthoredCardDefinition(
                card.BaseState.DefinitionId,
                out CardDefinition authoredDefinition);
            string authoredFrontTitle = hasAuthoredDefinition
                ? authoredDefinition.DisplayName
                : "FRONT";
            string authoredFrontBody = hasAuthoredDefinition
                ? authoredDefinition.Description + FormatInputCost(authoredDefinition.InputCost)
                : visualReferences.FrontLabel.text;
            model = new PrototypeCardInspectModel(
                targetCardId.ToString(),
                card.Face,
                new PrototypeCardInspectSideModel(
                    authoredFrontTitle,
                    contentVisible ? authoredFrontBody : obscuredContent,
                    null,
                    frontSurface,
                    new Color(0.06f, 0.08f, 0.10f)),
                new PrototypeCardInspectSideModel(
                    "BACK",
                    contentVisible ? visualReferences.BackLabel.text : obscuredContent,
                    null,
                    new Color(0.10f, 0.19f, 0.42f),
                    Color.white),
                false);
            return true;
        }

        private bool TryGetCardVisualReferences(
            TabletopObjectId targetCardId,
            out PrototypeCardVisualReferences resolvedReferences)
        {
            for (int i = 0; i < cardVisualReferences.Count; i++)
            {
                PrototypeCardVisualReferences candidate = cardVisualReferences[i];
                if (candidate != null
                    && candidate.CardView != null
                    && candidate.CardView.IsBound
                    && candidate.CardView.ObjectId == targetCardId)
                {
                    resolvedReferences = candidate;
                    return true;
                }
            }

            resolvedReferences = null;
            return false;
        }

        private void ShowStackContextMenu()
        {
            TryGetContextStack(out ContainerState stack, out StackRuntimeView stackView);
            ContainerId sourceStackId = stack.Id;
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            if (stack.Count >= 2)
            {
                actions.Add(new PrototypePopupActionOption(
                    "Split Stack",
                    true,
                    () =>
                    {
                        SplitStackResult result = SplitStack(stack, stackView);
                        if (result.Succeeded)
                        {
                            CloseContextMenu();
                        }
                    }));
            }

            if (HasValidMergeDestination(sourceStackId))
            {
                actions.Add(new PrototypePopupActionOption(
                    "Merge Into...",
                    true,
                    () => SetContextMenuMode(PrototypeContextMenuMode.MergeDestination)));
            }

            actions.Add(new PrototypePopupActionOption(
                "Move",
                true,
                () => BeginContainerMove(sourceStackId)));
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForContainer(sourceStackId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "STACK",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowDieContextMenu()
        {
            TabletopObjectId targetDieId = contextMenuDieId;
            TryGetDieView(targetDieId, out DieView targetDieView);
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>
            {
                new PrototypePopupActionOption(
                    "Roll",
                    true,
                    () => RollContextDie(targetDieId)),
                new PrototypePopupActionOption(
                    DuplicateActionLabel(targetDieId, TabletopComponentKind.Die),
                    true,
                    () => BeginDuplicatePlacement(targetDieId)),
            };
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetDieId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "DIE",
                $"d{targetDieView.DieState.SideCount}: {targetDieView.DieState.CurrentValue}",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowPawnContextMenu()
        {
            TabletopObjectId targetPawnId = contextMenuPawnId;
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>
            {
                new PrototypePopupActionOption(
                    DuplicateActionLabel(targetPawnId, TabletopComponentKind.Pawn),
                    true,
                    () => BeginDuplicatePlacement(targetPawnId)),
            };
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetPawnId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "PAWN",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowTokenContextMenu()
        {
            TabletopObjectId targetTokenId = contextMenuTokenId;
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>
            {
                new PrototypePopupActionOption(
                    DuplicateActionLabel(targetTokenId, TabletopComponentKind.Token),
                    true,
                    () => BeginDuplicatePlacement(targetTokenId)),
            };
            AddDeleteActionIfRuntime(
                actions,
                TabletopComponentTarget.ForObject(targetTokenId));
            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "TOKEN",
                string.Empty,
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void ShowConsoleContextMenu()
        {
            ConsoleId targetConsoleId = contextMenuConsoleId;
            ContainerId targetSlotContainerId = contextMenuContainerId;
            List<PrototypePopupActionOption> actions = new List<PrototypePopupActionOption>();
            if (IsAssistedTrapFloorConsoleSlot(targetSlotContainerId))
            {
                actions.Add(new PrototypePopupActionOption(
                    "Buy Ability",
                    true,
                    OpenActionAbilityPurchase));
            }

            actions.Add(new PrototypePopupActionOption(
                "Move",
                true,
                () => BeginConsoleMove(targetSlotContainerId)));
            if (!targetConsoleId.IsEmpty)
            {
                AddDeleteActionIfRuntime(
                    actions,
                    TabletopComponentTarget.ForConsole(targetConsoleId));
            }

            runtimeUi.ShowContextMenu(
                contextMenuAnchorScreenPosition,
                "CONSOLE",
                targetConsoleId.IsEmpty
                    ? string.Empty
                    : "Delete is available only while every Console Slot is empty.",
                actions,
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private void AddControllerDrawAction(
            List<PrototypePopupActionOption> actions,
            ContainerId targetDeckId)
        {
            if (!IsAssistedControllerDeck(targetDeckId))
            {
                return;
            }

            actions.Add(new PrototypePopupActionOption(
                "Draw...",
                true,
                () => SetContextMenuMode(PrototypeContextMenuMode.DrawCards)));
        }

        private void DrawControllerCardsFromContext(ContainerId targetDeckId, int requestedCount)
        {
            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (requestedCount <= 0
                || !IsAssistedControllerDeck(targetDeckId)
                || contextMenuContainerId != targetDeckId)
            {
                CloseContextMenu();
                return;
            }

            int drawCount = Math.Min(requestedCount, AvailableControllerDeckCount(targetDeckId));
            int deckLimitedCount = drawCount;
            drawCount = ClampToHandComfortCap(player.HandContainerId, drawCount);
            if (drawCount <= 0 && deckLimitedCount > 0)
            {
                ShowMessage(handComfortSettings.ReachedMessage + ".");
                CloseContextMenu();
                return;
            }

            if (drawCount <= 0)
            {
                ShowMessage("Controller Deck is empty.");
                CloseContextMenu();
                return;
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(targetDeckId, player.HandContainerId);
            DrawCardsResult result = new DrawCardsUseCase().Execute(
                matchState,
                new DrawCardsCommand(
                    CreateCommandContext(trapFloorTurnState.ActivePlayerId),
                    targetDeckId,
                    player.HandContainerId,
                    drawCount));
            ApplyLayout(targetDeckId);
            ApplyLayout(player.HandContainerId);
            presentationTransitions.AnimateCardsFromCurrentResults(
                transitionStarts,
                result.Succeeded ? handReflowDuration : returnDuration,
                result.Succeeded ? 0.035f : 0f);
            if (result.Succeeded)
            {
                ShowMessage(drawCount < deckLimitedCount
                    ? $"{handComfortSettings.ReachedMessage}: drew {drawCount} of {deckLimitedCount}."
                    : $"Drew {drawCount} Controller Card{(drawCount == 1 ? string.Empty : "s")}.");
                CloseContextMenu();
            }
            else
            {
                ShowMessage($"Controller draw rejected: {result.Error}.");
            }
        }

        private void DrawUpToConfiguredHandLimitFromContext(ContainerId targetDeckId)
        {
            if (!IsAssistedControllerDeck(targetDeckId)
                || contextMenuContainerId != targetDeckId)
            {
                CloseContextMenu();
                return;
            }

            ControllerInputHandDrawResult result = DrawUpToConfiguredHandLimit();
            if (result.Succeeded)
            {
                CloseContextMenu();
            }
        }

        private ControllerInputHandDrawResult DrawControllerCardsUpToComfortCap(
            TrapFloorPlayerSetupDefinition player,
            ContainerState hand)
        {
            int drawCount = Math.Min(
                handComfortSettings.RemainingFor(hand),
                AvailableControllerDeckCount(player.ControllerDeckId));
            if (drawCount <= 0)
            {
                ShowMessage(handComfortSettings.RemainingFor(hand) <= 0
                    ? handComfortSettings.ReachedMessage + "."
                    : "Controller Deck is empty.");
                return ControllerInputHandDrawResult.NoChange(matchState.Revision);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(player.ControllerDeckId, player.HandContainerId);
            DrawCardsResult result = new DrawCardsUseCase().Execute(
                matchState,
                new DrawCardsCommand(
                    CreateCommandContext(trapFloorTurnState.ActivePlayerId),
                    player.ControllerDeckId,
                    player.HandContainerId,
                    drawCount));
            ApplyLayout(player.ControllerDeckId);
            ApplyLayout(player.HandContainerId);
            presentationTransitions.AnimateCardsFromCurrentResults(
                transitionStarts,
                result.Succeeded ? handReflowDuration : returnDuration,
                result.Succeeded ? 0.035f : 0f);
            if (!result.Succeeded)
            {
                ShowMessage($"Controller draw rejected: {result.Error}.");
                return ControllerInputHandDrawResult.NoChange(matchState.Revision);
            }

            ShowMessage($"Drew {drawCount} Controller Card{(drawCount == 1 ? string.Empty : "s")} (hand cap {handComfortSettings.MaxHandCards}).");
            return ControllerInputHandDrawResult.Accepted(result.Revision, drawCount);
        }

        // Local comfort cap: limits only this player's own draws and drops into their own hand.
        private int ClampToHandComfortCap(ContainerId handId, int requestedCount)
        {
            if (!matchState.Containers.TryGetValue(handId, out ContainerState hand))
            {
                return requestedCount;
            }

            return Math.Min(requestedCount, handComfortSettings.RemainingFor(hand));
        }

        private bool IsOwnHandAtComfortCap(ContainerId containerId)
        {
            return handView != null
                && handView.IsBound
                && containerId == handView.ContainerId
                && matchState.Containers.TryGetValue(containerId, out ContainerState hand)
                && handComfortSettings.RemainingFor(hand) <= 0;
        }

        private void ShowHandCapReached()
        {
            ShowMessage(handComfortSettings.ReachedMessage + ".");
        }

        private void OpenActionAbilityPurchase()
        {
            CloseContextMenu();
            if (pendingControllerPurchaseState != null
                && pendingControllerPurchaseState.IsActive)
            {
                ShowMessage("Complete or cancel the current purchase first.");
                return;
            }
            ShowActionAbilityPurchase(string.Empty);
        }

        private void ShowActionAbilityPurchase(string statusMessage)
        {
            if (runtimeUi == null || trapFloorTemplate == null)
            {
                return;
            }

            runtimeUi.ShowActionAbilityPurchase(
                BuildActionAbilityPurchaseOptions(),
                TryPurchaseActionAbilityFromPopup,
                runtimeUi.CloseActionAbilityPurchase,
                statusMessage);
        }

        private void TryPurchaseActionAbilityFromPopup(string cardDefinitionStableId)
        {
            if (pendingControllerPurchaseState != null
                && pendingControllerPurchaseState.IsActive)
            {
                runtimeUi?.SetActionAbilityPurchaseStatus(
                    "Complete or cancel the current purchase first.");
                return;
            }

            if (!TryBuildPurchasePaymentOptions(
                    cardDefinitionStableId,
                    out CardDefinitionData purchasedDefinition,
                    out List<PurchasePaymentCardOption> options,
                    out string missing))
            {
                runtimeUi?.SetActionAbilityPurchaseStatus(
                    "Purchase unavailable because its authored input cost is invalid.");
                return;
            }

            pendingPurchaseDefinitionStableId = cardDefinitionStableId;
            List<PrototypeFocusedCardOptionModel> cards =
                new List<PrototypeFocusedCardOptionModel>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                PurchasePaymentCardOption option = options[i];
                cards.Add(new PrototypeFocusedCardOptionModel(
                    option.CardId,
                    option.DisplayName,
                    option.Input.HasValue ? option.Input.Value.ToString() : "Not eligible",
                    option.Artwork,
                    option.Eligible));
            }

            int requiredCardCount = PurchaseRequiredCardCount(purchasedDefinition.InputCost);
            string status = string.IsNullOrEmpty(missing)
                ? "Select the exact Controller Card instances for this purchase."
                : $"Missing: {missing}";
            runtimeUi.ShowFocusedCardSelection(new PrototypeFocusedCardSelectionModel(
                $"BUY {purchasedDefinition.DisplayName.ToUpperInvariant()} — HAND",
                $"Choose {FormatPurchaseInputCost(purchasedDefinition.InputCost)} from the active Player's Hand.",
                status,
                "Confirm Cards",
                cards,
                requiredCardCount,
                IsPurchasePaymentReady,
                ConfirmFocusedPurchasePayment,
                CancelFocusedPurchaseSelection));
        }

        private bool TryBuildPurchasePaymentOptions(
            string cardDefinitionStableId,
            out CardDefinitionData purchasedDefinition,
            out List<PurchasePaymentCardOption> options,
            out string missingRequirements)
        {
            purchasedDefinition = null;
            options = new List<PurchasePaymentCardOption>();
            missingRequirements = string.Empty;
            if (trapFloorTemplate == null
                || !trapFloorTemplate.GameDefinition.TryGetCard(
                    cardDefinitionStableId,
                    out purchasedDefinition)
                || purchasedDefinition.InputCost == null
                || purchasedDefinition.InputCost.IsEmpty
                || !ControllerInputCardCatalog.TryCreate(
                    trapFloorTemplate.GameDefinition,
                    out ControllerInputCardCatalog catalog))
                return false;

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (!matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState hand)
                || hand.Kind != ContainerKind.Hand)
                return false;

            Dictionary<ControllerInput, int> required =
                AggregatePurchaseRequirements(purchasedDefinition.InputCost);
            Dictionary<ControllerInput, int> available = new Dictionary<ControllerInput, int>();
            for (int i = 0; i < hand.Count; i++)
            {
                TabletopObjectId cardId = hand.GetObjectAt(i);
                if (!matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) return false;
                ControllerInput? input = catalog.TryGetInput(
                    card.BaseState.DefinitionId,
                    out ControllerInput resolvedInput)
                    ? resolvedInput
                    : (ControllerInput?)null;
                bool eligible = input.HasValue && required.ContainsKey(input.Value);
                if (eligible)
                {
                    available.TryGetValue(input.Value, out int count);
                    available[input.Value] = count + 1;
                }

                CardDefinition authoredDefinition = null;
                string displayName = TryGetAuthoredCardDefinition(
                        card.BaseState.DefinitionId,
                        out authoredDefinition)
                    ? authoredDefinition.DisplayName
                    : "Controller Card";
                options.Add(new PurchasePaymentCardOption(
                    cardId,
                    input,
                    displayName,
                    authoredDefinition != null ? authoredDefinition.FrontArtwork : null,
                    eligible));
            }

            List<string> missing = new List<string>();
            foreach (KeyValuePair<ControllerInput, int> requirement in required)
            {
                available.TryGetValue(requirement.Key, out int count);
                if (count < requirement.Value)
                    missing.Add($"{requirement.Key} ×{requirement.Value - count}");
            }
            missingRequirements = string.Join(", ", missing);
            return true;
        }

        private bool IsPurchasePaymentReady(IReadOnlyList<TabletopObjectId> selectedCardIds)
        {
            if (string.IsNullOrWhiteSpace(pendingPurchaseDefinitionStableId)
                || trapFloorTemplate == null
                || !trapFloorTemplate.GameDefinition.TryGetCard(
                    pendingPurchaseDefinitionStableId,
                    out CardDefinitionData purchasedDefinition))
                return false;

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (!matchState.Containers.TryGetValue(player.HandContainerId, out ContainerState hand))
                return false;
            for (int i = 0; i < selectedCardIds.Count; i++)
                if (!hand.Contains(selectedCardIds[i])) return false;
            return new ControllerInputCostEvaluator().EvaluateExact(
                matchState,
                selectedCardIds,
                purchasedDefinition.InputCost,
                trapFloorTemplate.GameDefinition).CanPay;
        }

        private void ConfirmFocusedPurchasePayment(
            IReadOnlyList<TabletopObjectId> selectedCardIds)
        {
            if (!IsPurchasePaymentReady(selectedCardIds))
            {
                ShowMessage("Purchase payment selection is incomplete.");
                return;
            }

            string definitionId = pendingPurchaseDefinitionStableId;
            PlayerId playerId = trapFloorTurnState.ActivePlayerId;
            pendingControllerPurchaseState.Begin(playerId, definitionId, selectedCardIds);
            pendingPurchaseDefinitionStableId = string.Empty;
            runtimeUi?.CloseFocusedCardSelection();
            ReplaceCurrentUndoStateForPurchaseAssistance();
            RefreshTrapFloorStatusUi();
            ShowMessage(
                $"BUY {FindPurchaseDisplayName(definitionId).ToUpperInvariant()} — "
                + "Place the selected Cards in your Console");
        }

        private void CancelFocusedPurchaseSelection()
        {
            pendingPurchaseDefinitionStableId = string.Empty;
            runtimeUi?.CloseFocusedCardSelection();
            ShowMessage("Purchase selection cancelled.");
        }

        private void ConfirmPendingActionAbilityPurchase()
        {
            if (pendingControllerPurchaseState == null
                || !pendingControllerPurchaseState.IsActive
                || !pendingControllerPurchaseState.IsPaymentComplete
                || trapFloorTurnState == null
                || pendingControllerPurchaseState.PlayerId != trapFloorTurnState.ActivePlayerId)
            {
                ShowMessage("Purchase payment is not ready.");
                return;
            }

            PurchaseActionOrAbility(
                pendingControllerPurchaseState.PurchasedCardDefinitionStableId);
            RefreshTrapFloorStatusUi();
        }

        private void CancelPendingActionAbilityPurchase()
        {
            pendingPurchaseDefinitionStableId = string.Empty;
            runtimeUi?.CloseFocusedCardSelection();
            runtimeUi?.CloseActionAbilityPurchase();
            if (pendingControllerPurchaseState == null
                || !pendingControllerPurchaseState.IsActive)
                return;

            pendingControllerPurchaseState.Clear();
            ReplaceCurrentUndoStateForPurchaseAssistance();
            RefreshTrapFloorStatusUi();
            ShowMessage(
                "Purchase assistance cancelled. Cards remain where the Player placed them.");
        }

        private string FindPurchaseDisplayName(string cardDefinitionStableId)
        {
            return trapFloorTemplate != null
                && trapFloorTemplate.GameDefinition.TryGetCard(
                    cardDefinitionStableId,
                    out CardDefinitionData definition)
                ? definition.DisplayName
                : "ABILITY";
        }

        private static Dictionary<ControllerInput, int> AggregatePurchaseRequirements(
            InputCostData cost)
        {
            Dictionary<ControllerInput, int> requirements =
                new Dictionary<ControllerInput, int>();
            for (int i = 0; i < cost.Requirements.Count; i++)
            {
                InputRequirementData requirement = cost.Requirements[i];
                requirements.TryGetValue(requirement.Input, out int count);
                requirements[requirement.Input] = checked(count + requirement.Count);
            }
            return requirements;
        }

        private static int PurchaseRequiredCardCount(InputCostData cost)
        {
            int count = 0;
            for (int i = 0; i < cost.Requirements.Count; i++)
                count = checked(count + cost.Requirements[i].Count);
            return count;
        }

        private List<PrototypeActionAbilityPurchaseOption> BuildActionAbilityPurchaseOptions()
        {
            List<PrototypeActionAbilityPurchaseOption> options =
                new List<PrototypeActionAbilityPurchaseOption>();
            GameDefinitionData gameDefinition = trapFloorTemplate.GameDefinition;
            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            ContainerState hand = matchState.GetContainer(player.HandContainerId);
            ControllerInputCostEvaluator evaluator = new ControllerInputCostEvaluator();

            for (int i = 0; i < gameDefinition.Cards.Count; i++)
            {
                CardDefinitionData definition = gameDefinition.Cards[i];
                if (definition.Quantity < 1
                    || definition.RepresentedControllerInput.HasValue
                    || definition.InputCost == null
                    || definition.InputCost.IsEmpty)
                {
                    continue;
                }

                string cost = FormatPurchaseInputCost(definition.InputCost);
                ControllerInputCostEvaluation evaluation = evaluator.Evaluate(
                    matchState,
                    hand,
                    definition.InputCost,
                    gameDefinition);
                string affordability = evaluation.CanPay
                    ? "Can afford"
                    : evaluation.Error == ControllerInputCostEvaluationError.InsufficientInput
                        ? $"Cannot afford. Need: {cost}"
                        : $"Unavailable: {evaluation.Error}";
                Texture artwork = null;
                if (Guid.TryParse(definition.StableId, out Guid definitionGuid)
                    && TryGetAuthoredCardDefinition(
                        new ObjectDefinitionId(definitionGuid),
                        out CardDefinition authoredDefinition))
                {
                    artwork = authoredDefinition.FrontArtwork;
                }
                options.Add(new PrototypeActionAbilityPurchaseOption(
                    definition.StableId,
                    definition.DisplayName,
                    definition.Description,
                    cost,
                    artwork,
                    evaluation.CanPay,
                    affordability));
            }

            return options;
        }

        private static string FormatPurchaseInputCost(InputCostData cost)
        {
            Dictionary<ControllerInput, int> totals = new Dictionary<ControllerInput, int>();
            List<ControllerInput> authoredOrder = new List<ControllerInput>();
            for (int i = 0; i < cost.Requirements.Count; i++)
            {
                InputRequirementData requirement = cost.Requirements[i];
                if (!totals.ContainsKey(requirement.Input))
                {
                    totals.Add(requirement.Input, 0);
                    authoredOrder.Add(requirement.Input);
                }

                totals[requirement.Input] = checked(totals[requirement.Input] + requirement.Count);
            }

            List<string> entries = new List<string>(authoredOrder.Count);
            for (int i = 0; i < authoredOrder.Count; i++)
            {
                ControllerInput input = authoredOrder[i];
                entries.Add($"{input} ×{totals[input]}");
            }

            return string.Join(", ", entries);
        }

        private bool IsAssistedTrapFloorConsoleSlot(ContainerId slotContainerId)
        {
            if (trapFloorTemplate == null || slotContainerId.IsEmpty)
            {
                return false;
            }

            if (trapFloorTurnState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                return false;
            }

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            if (player.MainSlotContainerId == slotContainerId)
            {
                return true;
            }

            for (int i = 0; i < player.SideSlotContainerIds.Count; i++)
            {
                if (player.SideSlotContainerIds[i] == slotContainerId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAssistedControllerDeck(ContainerId containerId)
        {
            if (trapFloorTemplate == null || containerId.IsEmpty)
            {
                return false;
            }

            if (trapFloorTurnState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                return false;
            }

            TrapFloorPlayerSetupDefinition player = GetAssistedTrapFloorPlayerSetup();
            return player.ControllerDeckId == containerId
                && matchState.Containers.TryGetValue(containerId, out ContainerState container)
                && container.Kind == ContainerKind.Deck
                && container.OwnerSeatId == player.SeatId
                && matchState.Seats.TryGetValue(player.SeatId, out SeatState seat)
                && seat.OccupantPlayerId == trapFloorTurnState.ActivePlayerId;
        }

        private void RollContextDie(TabletopObjectId targetDieId)
        {
            // Ignore callbacks retained from a closed/replaced menu; one accepted click launches once.
            if (contextMenuMode != PrototypeContextMenuMode.Die || contextMenuDieId != targetDieId) return;
            if (!TryGetDieView(targetDieId, out DieView targetDieView))
            {
                CloseContextMenu();
                return;
            }

            if (targetDieView.PhysicalObject == null || !targetDieView.PhysicalObject.Roll())
            {
                ShowMessage("Die Roll rejected: object is unavailable or controlled.");
                return;
            }
            ShowMessage("Rolling physically; result is accepted when the Die settles.");
            CloseContextMenu();
        }

        private void ShowMergeDestinationPopup()
        {
            TryGetContextStack(out ContainerState source, out _);
            ContainerId sourceStackId = source.Id;
            List<PrototypePopupActionOption> destinations = new List<PrototypePopupActionOption>();
            foreach (KeyValuePair<ContainerId, StackRuntimeView> pair in stackViewsByContainerId)
            {
                if (!IsValidMergeDestination(sourceStackId, pair.Key, pair.Value))
                {
                    continue;
                }

                ContainerId destinationStackId = pair.Key;
                string label = pair.Value.Visual != null && pair.Value.Visual.Label != null
                    ? pair.Value.Visual.Label.text
                    : "Stack";
                destinations.Add(new PrototypePopupActionOption(
                    label,
                    true,
                    () =>
                    {
                        MergeStacksResult result = MergeStacks(sourceStackId, destinationStackId);
                        if (result.Succeeded)
                        {
                            CloseContextMenu();
                        }
                    }));
            }

            runtimeUi.ShowMergeDestinationPopup(
                contextMenuAnchorScreenPosition,
                destinations,
                () => SetContextMenuMode(PrototypeContextMenuMode.Stack),
                CloseContextMenu,
                DismissPopupFromSecondary);
        }

        private bool IsCurrentContextCardInContainer(ContainerId containerId)
        {
            return !contextMenuCardId.IsEmpty
                && matchState.Cards.TryGetValue(contextMenuCardId, out CardInstanceState card)
                && card.BaseState.ContainerId == containerId;
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

        private bool IsContextMenuTargetAvailable()
        {
            if (matchState == null)
            {
                return false;
            }

            switch (contextMenuMode)
            {
                case PrototypeContextMenuMode.Deck:
                case PrototypeContextMenuMode.PopulateDeck:
                    return !contextMenuContainerId.IsEmpty
                        && matchState.Containers.TryGetValue(contextMenuContainerId, out ContainerState deck)
                        && deck.Kind == ContainerKind.Deck
                        && (contextMenuMode != PrototypeContextMenuMode.PopulateDeck || deck.Count == 0)
                        && (contextMenuCardId.IsEmpty
                            || (IsCurrentContextCardInContainer(contextMenuContainerId)
                                && TryGetCardView(contextMenuCardId, out _)))
                        && TryGetDeckPresentation(contextMenuContainerId, out _, out _);
                case PrototypeContextMenuMode.DrawCards:
                case PrototypeContextMenuMode.CustomDrawCards:
                    return IsAssistedControllerDeck(contextMenuContainerId)
                        && TryGetDeckPresentation(contextMenuContainerId, out _, out _);
                case PrototypeContextMenuMode.TabletopCard:
                    return IsCurrentContextCardInContainer(ContainerId.Empty)
                        && TryGetCardView(contextMenuCardId, out _);
                case PrototypeContextMenuMode.FloorCard:
                    return trapFloorTemplate != null
                        && matchState.Cards.ContainsKey(contextMenuCardId)
                        && trapFloorTemplate.IsFloorCard(contextMenuCardId)
                        && TryGetCardView(contextMenuCardId, out _);
                case PrototypeContextMenuMode.PendingFloormasterCard:
                    return floormasterLifecycleState?.PendingCard != null
                        && floormasterLifecycleState.PendingCard.CardId == contextMenuCardId
                        && matchState.Cards.ContainsKey(contextMenuCardId)
                        && TryGetCardView(contextMenuCardId, out _);
                case PrototypeContextMenuMode.StackCard:
                    return IsCurrentContextCardInContainer(contextMenuContainerId)
                        && matchState.Containers.TryGetValue(contextMenuContainerId, out ContainerState stackCardContainer)
                        && stackCardContainer.Kind == ContainerKind.Stack
                        && TryGetCardView(contextMenuCardId, out _);
                case PrototypeContextMenuMode.ContainedCard:
                    return IsCurrentContextCardInContainer(contextMenuContainerId)
                        && matchState.Containers.ContainsKey(contextMenuContainerId)
                        && TryGetCardView(contextMenuCardId, out _);
                case PrototypeContextMenuMode.Stack:
                case PrototypeContextMenuMode.MergeDestination:
                    return TryGetContextStack(out _, out _);
                case PrototypeContextMenuMode.DiscardPile:
                    return !contextMenuContainerId.IsEmpty
                        && matchState.Containers.TryGetValue(contextMenuContainerId, out ContainerState discardPile)
                        && discardPile.Kind == ContainerKind.DiscardPile
                        && (contextMenuCardId.IsEmpty
                            || (IsCurrentContextCardInContainer(contextMenuContainerId)
                                && TryGetCardView(contextMenuCardId, out _)))
                        && TryGetRuntimeDiscardPile(contextMenuContainerId, out _);
                case PrototypeContextMenuMode.Die:
                    return !contextMenuDieId.IsEmpty
                        && matchState.Dice.ContainsKey(contextMenuDieId)
                        && TryGetDieView(contextMenuDieId, out _);
                case PrototypeContextMenuMode.Pawn:
                    return !contextMenuPawnId.IsEmpty
                        && matchState.Pawns.ContainsKey(contextMenuPawnId)
                        && TryGetPawnView(contextMenuPawnId, out _);
                case PrototypeContextMenuMode.Token:
                    return !contextMenuTokenId.IsEmpty
                        && matchState.Tokens.ContainsKey(contextMenuTokenId)
                        && TryGetTokenView(contextMenuTokenId, out _);
                case PrototypeContextMenuMode.Console:
                    return TryResolveConsolePlacement(
                        contextMenuContainerId,
                        out _,
                        out _,
                        out _);
                default:
                    return false;
            }
        }

        private bool TryGetCardView(TabletopObjectId cardId, out CardView resolvedView)
        {
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView candidate = cardViews[i];
                if (candidate != null && candidate.IsBound && candidate.ObjectId == cardId)
                {
                    resolvedView = candidate;
                    return true;
                }
            }

            resolvedView = null;
            return false;
        }

        private bool TryGetDieView(TabletopObjectId dieId, out DieView resolvedView)
        {
            for (int i = 0; i < dieViews.Count; i++)
            {
                DieView candidate = dieViews[i];
                if (candidate != null
                    && candidate.IsBound
                    && candidate.DieState != null
                    && candidate.ObjectId == dieId)
                {
                    resolvedView = candidate;
                    return true;
                }
            }

            resolvedView = null;
            return false;
        }

        private bool TryGetPawnView(TabletopObjectId pawnId, out PawnView resolvedView)
        {
            for (int i = 0; i < pawnViews.Count; i++)
            {
                PawnView candidate = pawnViews[i];
                if (candidate != null && candidate.IsBound && candidate.ObjectId == pawnId)
                {
                    resolvedView = candidate;
                    return true;
                }
            }

            resolvedView = null;
            return false;
        }

        private bool TryGetTokenView(TabletopObjectId tokenId, out TokenView resolvedView)
        {
            for (int i = 0; i < tokenViews.Count; i++)
            {
                TokenView candidate = tokenViews[i];
                if (candidate != null && candidate.IsBound && candidate.ObjectId == tokenId)
                {
                    resolvedView = candidate;
                    return true;
                }
            }

            resolvedView = null;
            return false;
        }

        private bool TryGetPlacedConsoleBySlot(ContainerId slotContainerId, out ConsoleId consoleId)
        {
            foreach (KeyValuePair<ConsoleId, PlacedConsoleState> pair in matchState.PlacedConsoles)
            {
                if (pair.Value.Console.ContainsSlot(slotContainerId))
                {
                    consoleId = pair.Key;
                    return true;
                }
            }

            consoleId = ConsoleId.Empty;
            return false;
        }

        private bool TryResolveConsolePlacement(
            ContainerId slotContainerId,
            out TabletopPose pose,
            out float? surfaceHeight,
            out ConsoleView resolvedView)
        {
            pose = TabletopPose.Default;
            surfaceHeight = null;
            resolvedView = null;
            if (slotContainerId.IsEmpty
                || !matchState.Containers.TryGetValue(slotContainerId, out ContainerState slot)
                || slot.Kind != ContainerKind.ConsoleSlot)
            {
                return false;
            }

            ConsoleState console = null;
            foreach (SeatState seat in matchState.Seats.Values)
            {
                if (!seat.Console.ContainsSlot(slotContainerId))
                {
                    continue;
                }

                console = seat.Console;
                pose = seat.ConsolePose;
                surfaceHeight = seat.ConsoleSurfaceHeight;
                break;
            }

            if (console == null)
            {
                foreach (PlacedConsoleState placedConsole in matchState.PlacedConsoles.Values)
                {
                    if (!placedConsole.Console.ContainsSlot(slotContainerId))
                    {
                        continue;
                    }

                    console = placedConsole.Console;
                    pose = placedConsole.Pose;
                    surfaceHeight = placedConsole.SurfaceHeight;
                    break;
                }
            }

            if (console == null)
            {
                return false;
            }

            for (int i = 0; i < playerConsoleViews.Count; i++)
            {
                ConsoleView candidate = playerConsoleViews[i];
                if (candidate != null
                    && candidate.IsBound
                    && ReferenceEquals(candidate.ConsoleState, console))
                {
                    resolvedView = candidate;
                    return true;
                }
            }

            return false;
        }

        private void ApplyConsolePlacement(ContainerId slotContainerId)
        {
            if (!TryResolveConsolePlacement(
                    slotContainerId,
                    out TabletopPose pose,
                    out float? surfaceHeight,
                    out ConsoleView targetConsole))
            {
                throw new InvalidOperationException("Moved Console has no authoritative Presentation binding.");
            }

            ApplyConsolePose(targetConsole.transform, pose, surfaceHeight);
            targetConsole.ApplyAcceptedLayout();
            RefreshCardContentVisibility();
            Physics.SyncTransforms();
        }

        private bool TryGetRuntimeConsole(
            ConsoleId consoleId,
            out RuntimeConsoleInstance resolvedInstance)
        {
            for (int i = 0; i < runtimeConsoleInstances.Count; i++)
            {
                RuntimeConsoleInstance candidate = runtimeConsoleInstances[i];
                if (candidate.ConsoleId == consoleId
                    && candidate.View != null
                    && candidate.View.IsBound)
                {
                    resolvedInstance = candidate;
                    return true;
                }
            }

            resolvedInstance = null;
            return false;
        }

        private void ValidateTrapFloorConfiguration()
        {
            ValidateCommonConfiguration();
            RequireReference(gameBoardPhysicalSurface, nameof(gameBoardPhysicalSurface));
            RequireReference(gameBoardVisualRoot, nameof(gameBoardVisualRoot));
            RequireReference(gameBoardVisualMaterial, nameof(gameBoardVisualMaterial));
            RequireReference(trapFloorGameDefinition, nameof(trapFloorGameDefinition));
            if (!gameBoardVisualRoot.IsChildOf(gameBoardPhysicalSurface.transform))
            {
                throw new InvalidOperationException(
                    "The Trap Floor Board visual must be a child of its authored physical surface.");
            }

            ValidateFiniteGreaterThanZero(floorCardVisualScale, nameof(floorCardVisualScale));
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
            ValidateComponentCatalogs();
            ValidateToolboxPrefabReferences();
        }

        // Component library (doc 22): validated once; IDs are unique across every catalog. Resolves the pile
        // prefabs from the Base box first, in shelf order.
        private void ValidateComponentCatalogs()
        {
            if (componentLibrary == null)
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires its component library (doc 22).");
            }

            componentLibrary.Validate();
            catalogDeckPrefab = ResolveCatalogPilePrefab(ComponentCatalogKind.Deck);
            catalogStackPrefab = ResolveCatalogPilePrefab(ComponentCatalogKind.Stack);
            catalogDiscardPilePrefab = ResolveCatalogPilePrefab(ComponentCatalogKind.DiscardPile);
            catalogDeckPrefab.ValidateReferences();
            catalogDeckPrefab.GetView<DeckView>();
            catalogStackPrefab.ValidateReferences();
            ValidateStackLayoutAnchor(catalogStackPrefab);
            catalogDiscardPilePrefab.ValidateReferences();
            catalogDiscardPilePrefab.GetView<DiscardPileView>();
            ResolveCatalogSpawnPrefabs();
        }

        // Card, Pawn, Token, Die and Console prefabs come from the library: the first entry of each kind in shelf
        // order, the same rule as the piles (doc 22, C3a). Resolved once; the Console layout may ask for it before
        // the session validates, so it resolves on first use too.
        private void ResolveCatalogSpawnPrefabs()
        {
            if (catalogConsolePrefab != null)
            {
                return;
            }

            if (componentLibrary == null)
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires its component library (doc 22).");
            }

            // The library lists its catalogs once validated; the Console layout can be asked for first.
            if (componentLibrary.Catalogs.Count == 0)
            {
                componentLibrary.Validate();
            }

            catalogCardPrefab = ResolveCatalogEntry(ComponentCatalogKind.Card)
                .GetPrefabComponent<PrototypeCardVisualReferences>();
            catalogPawnPrefab = ResolveCatalogEntry(ComponentCatalogKind.Pawn).GetPrefabComponent<PawnView>();
            catalogTokenPrefab = ResolveCatalogEntry(ComponentCatalogKind.Token).GetPrefabComponent<TokenView>();
            catalogDiePrefab = ResolveCatalogEntry(ComponentCatalogKind.Die).GetPrefabComponent<DieView>();
            catalogConsolePrefab = ResolveCatalogEntry(ComponentCatalogKind.Console).GetPrefabComponent<ConsoleView>();
            catalogHandPrefab = ResolveCatalogEntry(ComponentCatalogKind.Hand)
                .GetPrefabComponent<PrototypeFixedContainerVisual>();
        }

        private ComponentCatalogEntry ResolveCatalogEntry(ComponentCatalogKind kind)
        {
            IReadOnlyList<ComponentCatalog> catalogs = componentLibrary.Catalogs;
            for (int i = 0; i < catalogs.Count; i++)
            {
                if (catalogs[i].TryGetFirst(kind, out ComponentCatalogEntry entry))
                {
                    return entry;
                }
            }

            throw new InvalidOperationException($"No catalog in the component library has a {kind} entry.");
        }

        private PrototypeFixedContainerVisual ResolveCatalogPilePrefab(ComponentCatalogKind kind)
        {
            return ResolveCatalogEntry(kind).GetPrefabComponent<PrototypeFixedContainerVisual>();
        }

        private void ValidateToolboxPrefabReferences()
        {
            RequireReference(catalogCardPrefab, nameof(catalogCardPrefab));
            RequireReference(catalogPawnPrefab, nameof(catalogPawnPrefab));
            RequireReference(catalogTokenPrefab, nameof(catalogTokenPrefab));
            RequireReference(catalogDiePrefab, nameof(catalogDiePrefab));

            if (catalogCardPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException("The catalog Card entry must reference a prefab asset.");
            }

            catalogCardPrefab.ValidateReferences();
            ValidateObjectPrefab(catalogPawnPrefab, nameof(catalogPawnPrefab));
            ValidateObjectPrefab(catalogTokenPrefab, nameof(catalogTokenPrefab));
            ValidateObjectPrefab(catalogDiePrefab, nameof(catalogDiePrefab));
            RequireReference(catalogHandPrefab, nameof(catalogHandPrefab));
            catalogHandPrefab.ValidateReferences();
            HandView catalogHandView = catalogHandPrefab.GetView<HandView>();
            if (ReferenceEquals(catalogHandView.LayoutAnchor, catalogHandPrefab.transform))
            {
                throw new InvalidOperationException("The catalog Hand must use its distinct authored layout anchor.");
            }

            ValidateConsolePrefabLayout();
        }

        // One Console definition for every Console (doc 19): the prefab authors exactly one Slot per layout
        // Card slot, and each Slot anchor sits at its layout position. Games never add, remove or hide Slots.
        private void ValidateConsolePrefabLayout()
        {
            RequireReference(catalogConsolePrefab, nameof(catalogConsolePrefab));
            if (catalogConsolePrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "The catalog Console entry must reference a prefab asset.");
            }

            ConsoleLayoutData layout = GetConsoleLayout();
            int cardSlotCount = layout.CardSlots.Count;
            ConsoleSlotView[] slotViews = catalogConsolePrefab.GetComponentsInChildren<ConsoleSlotView>(true);
            IReadOnlyList<Transform> anchors = catalogConsolePrefab.SlotAnchors;
            if (slotViews.Length != cardSlotCount || anchors.Count != cardSlotCount)
            {
                throw new InvalidOperationException(
                    $"The Console prefab authors {slotViews.Length} Slots and {anchors.Count} Slot anchors; "
                    + $"its layout '{layout.StableId}' has {cardSlotCount} Card slots.");
            }

            Transform root = catalogConsolePrefab.transform;
            for (int i = 0; i < cardSlotCount; i++)
            {
                ConsoleLayoutSlotData slot = layout.CardSlots[i];
                RequireReference(anchors[i], $"Console Slot anchor {i}");
                Vector3 local = root.InverseTransformPoint(anchors[i].position);
                if (Mathf.Abs(local.x - slot.X) > ConsoleLayoutAnchorTolerance
                    || Mathf.Abs(local.z - slot.Z) > ConsoleLayoutAnchorTolerance)
                {
                    throw new InvalidOperationException(
                        $"Console Slot anchor {i} ({anchors[i].name}) is at ({local.x}, {local.z}); "
                        + $"layout slot '{slot.Key}' is at ({slot.X}, {slot.Z}).");
                }

                // The Slot collider is the slot footprint (console-local x/z).
                BoxCollider footprint = slotViews[i].GetComponent<BoxCollider>();
                RequireReference(footprint, $"BoxCollider on Console Slot {i}");
                Vector3 scale = footprint.transform.lossyScale;
                Vector3 rootScale = root.lossyScale;
                float width = footprint.size.x * scale.x / rootScale.x;
                float depth = footprint.size.z * scale.z / rootScale.z;
                if (Mathf.Abs(width - slot.FootprintWidth) > ConsoleLayoutAnchorTolerance
                    || Mathf.Abs(depth - slot.FootprintDepth) > ConsoleLayoutAnchorTolerance)
                {
                    throw new InvalidOperationException(
                        $"Console Slot {i} ({slotViews[i].name}) collider is {width} x {depth}; "
                        + $"layout slot '{slot.Key}' footprint is {slot.FootprintWidth} x {slot.FootprintDepth}.");
                }
            }
        }

        // Resolved once from the Console prefab's ConsoleLayoutBinding; the layout asset does not change in play.
        private ConsoleLayoutData GetConsoleLayout()
        {
            if (consoleLayout != null)
            {
                return consoleLayout;
            }

            ResolveCatalogSpawnPrefabs();
            ConsoleLayoutBinding binding = catalogConsolePrefab.GetComponent<ConsoleLayoutBinding>();
            RequireReference(binding, $"{nameof(ConsoleLayoutBinding)} on the catalog Console prefab");
            consoleLayout = binding.ResolveLayoutData();
            return consoleLayout;
        }

        // Games never add, remove or restrict Console slots: every seat Console must match the layout.
        private void ValidateSeatConsolesMatchLayout()
        {
            ConsoleLayoutData layout = GetConsoleLayout();
            foreach (SeatState seat in matchState.Seats.Values)
            {
                if (seat.Console.SlotCount != layout.CardSlots.Count)
                {
                    throw new InvalidOperationException(
                        $"A seat Console has {seat.Console.SlotCount} Slots; the Console layout has {layout.CardSlots.Count} Card slots.");
                }

                for (int i = 0; i < seat.Console.SlotCount; i++)
                {
                    ContainerState slot = matchState.GetContainer(seat.Console.SlotContainerIds[i]);
                    if (slot.Capacity != layout.CardSlotCapacities[i])
                    {
                        throw new InvalidOperationException(
                            $"Seat Console Slot {i} has capacity {slot.Capacity}; layout slot '{layout.CardSlots[i].Key}' "
                            + $"has {layout.CardSlotCapacities[i]}.");
                    }
                }
            }
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
            if (catalogCardPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "TabletopPrototypeComposition requires catalogCardPrefab to reference a prefab asset.");
            }

            catalogCardPrefab.ValidateReferences();
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
            interactionOwnerId = InteractionOwnerId.New();
            coordinateConverter = PhysicalTabletopSurfaces.CreateTemplateLayoutConverter(
                targetCamera,
                worldUnitsPerTableUnit,
                tabletopLayerHeight,
                tabletopLocalOrderHeight);

            if (prototypeTemplateContext == null)
            {
                throw new InvalidOperationException("Trap Floor Presentation requires a selected Template session context.");
            }

            pendingControllerPurchaseState = pendingRestoredControllerPurchaseState
                ?? new PendingControllerPurchaseState(matchState.Id);

            BuildTrapFloorRevealRuntime();
            ProjectTemplateBoardSurface();
        }

        private void BuildTrapFloorRevealRuntime()
        {
            if (pendingRestoredTrapFloorState != null)
            {
                trapFloorActivityFeed = pendingRestoredTrapFloorState.Activity;
                trapFloorCollapseState = pendingRestoredTrapFloorState.Collapse;
                trapFloorObjectiveState = pendingRestoredTrapFloorState.Objective;
                trapFloorTurnState = pendingRestoredTrapFloorState.Turn;
                trapFloorAbilityResolutionState = pendingRestoredTrapFloorState.AbilityResolution;
                trapFloorPendingSearchState = pendingRestoredTrapFloorState.PendingSearch;
            }
            else
            {
                trapFloorActivityFeed = new TrapFloorActivityFeedState(matchState.Id);
                trapFloorCollapseState = new TrapFloorCollapseState(
                    matchState.Id,
                    trapFloorTemplate.FloorCardIds.Count);
                trapFloorObjectiveState = new TrapFloorObjectiveState(
                    matchState.Id,
                    trapFloorTemplate.ActiveMode.RequiredKeyCount,
                    trapFloorTemplate.ActiveMode.Behavior);
                trapFloorTurnState = new TrapFloorTurnState(
                    matchState.Id,
                    activeSession.Request.ActivePlayerIds);
                trapFloorAbilityResolutionState =
                    new TrapFloorAbilityResolutionState(matchState.Id);
                trapFloorPendingSearchState = new TrapFloorPendingSearchState(matchState.Id);
            }
            trapFloorCollapseUseCase = new TrapFloorCollapseUseCase(
                trapFloorTemplate,
                trapFloorCollapseState,
                trapFloorActivityFeed);
            trapFloorRevealFloorUseCase = new TrapFloorRevealFloorUseCase(
                trapFloorTemplate,
                trapFloorActivityFeed,
                trapFloorCollapseState,
                trapFloorAbilityResolutionState,
                trapFloorTurnState);
            trapFloorObjectiveUseCase = new TrapFloorObjectiveUseCase(
                trapFloorTemplate,
                trapFloorObjectiveState,
                trapFloorActivityFeed);
            trapFloorTurnService = new TrapFloorTurnService(
                trapFloorTemplate,
                trapFloorTurnState,
                controllerInputHandService,
                trapFloorActivityFeed,
                trapFloorAbilityResolutionState);
            trapFloorAbilityResolutionService = new TrapFloorAbilityResolutionService(
                trapFloorAbilityResolutionState,
                trapFloorTurnState,
                trapFloorActivityFeed,
                trapFloorTemplate,
                trapFloorCollapseState);
            ConsoleCardInteractionAccepted -= HandleTrapFloorConsoleCardInteractionAccepted;
            ConsoleCardInteractionAccepted += HandleTrapFloorConsoleCardInteractionAccepted;
            activeFloorRevealActivity = null;
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
            abilityTargetPresenter = new TrapFloorAbilityTargetPresenter();
        }

        private void BuildFloormasterLifecycleRuntime()
        {
            floormasterLifecycleState = new TrapFloorFloormasterLifecycleState(matchState.Id);
            floormasterLifecycleService = new TrapFloorFloormasterLifecycleService(
                trapFloorTemplate,
                authoritativeRandomValueSource,
                floormasterLifecycleState);
        }

        private void BuildTrapFloorRoundRuntime()
        {
            trapFloorRoundState = new TrapFloorRoundState(
                matchState.Id,
                activeSession.Request.ActivePlayerIds);
            trapFloorRoundOrchestrationService = new TrapFloorRoundOrchestrationService(
                trapFloorTemplate,
                matchState,
                trapFloorRoundState,
                floormasterLifecycleState,
                floormasterLifecycleService,
                floorfallService);
        }

        private void BuildToolboxRuntime()
        {
            TabletopPhysicsSettings.Apply();
            SetGameBoardActive(activeSession.Selection.Kind == TabletopSessionKind.GameTemplate);
            physicalSurfaceQuery = new PhysicalTabletopSurfaces(targetCamera, coordinateConverter);
            physicalSurfaceQuery.ValidateSetup();
            physicalAuthority = new LocalPhysicalObjectAuthority(matchState, activeSession.Request.ActivePlayerIds,
                () => localPlayerId, targetCamera, physicalSurfaceQuery, target => presentationTransitions.Stop(target, false),
                physicalInteraction);
            if (authoritativeRandomValueSource == null)
            {
                authoritativeRandomValueSource = new SystemRandomValueSource();
            }
            componentIdentitySource = new GuidTabletopComponentIdentitySource();
            componentCreationUseCase = new CreateTabletopComponentUseCase(componentIdentitySource, physicalSurfaceQuery,
                physicalSurfaceQuery.ResolveContainerSurfaceHeight, GetConsoleLayout().CardSlotCapacities);
            cardBatchCreationUseCase = new CreateGenericCardBatchUseCase(componentIdentitySource, physicalSurfaceQuery);
            populateDeckUseCase = new PopulateDeckUseCase(componentIdentitySource);
            componentDeletionUseCase = new DeleteTabletopComponentUseCase();
            componentDuplicationUseCase = new DuplicateTabletopComponentUseCase(componentCreationUseCase);
            toolboxSpawnSequence = 0;
            selectedQuantity = 1;
            toolboxPlacementHintActive = false;
            toolboxPlacementSubject = null;
            runtimeUi?.ClearActiveSessionTransientUi();
        }

        private void RebuildEmptyTableLooseObjectPresentation()
        {
            // Empty Table owns no scene-authored object Views. After Undo replaces the Match,
            // teardown has removed every runtime-owned View, so recreate exactly the objects in
            // the selected authoritative snapshot before interaction registries are rebuilt.
            foreach (CardInstanceState card in matchState.Cards.Values)
            {
                CardView view = CreateCardView(card, "CARD", out TabletopSelectionVisual selectionVisual);
                cardViews.Add(view);
                cardSelectionVisuals.Add(selectionVisual);
            }

            foreach (PawnState pawn in matchState.Pawns.Values)
            {
                PawnView view = CreatePawnView(pawn, out TabletopSelectionVisual selectionVisual);
                pawnViews.Add(view);
                pawnSelectionVisuals.Add(selectionVisual);
            }

            foreach (TokenState token in matchState.Tokens.Values)
            {
                TokenView view = CreateTokenView(token, out TabletopSelectionVisual selectionVisual, 1f);
                tokenViews.Add(view);
                tokenSelectionVisuals.Add(selectionVisual);
            }

            foreach (DieState die in matchState.Dice.Values)
            {
                DieView view = CreateDieView(
                    die,
                    $"d{die.SideCount}",
                    out TabletopSelectionVisual selectionVisual);
                dieViews.Add(view);
                dieSelectionVisuals.Add(selectionVisual);
            }

            Physics.SyncTransforms();
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
                localPlayer.HandContainerId,
                templateDefinition.BoardPlayAreaId,
                localPlayer.AvatarCardId,
                localPlayer.PawnId,
                TabletopObjectId.Empty,
                templateDefinition.CardLabels,
                CreateControllerInputDefinitions(templateDefinition.GameDefinition));
        }

        private static Dictionary<ObjectDefinitionId, ButtonCardDefinition> CreateControllerInputDefinitions(
            GameDefinitionData gameDefinition)
        {
            Dictionary<ObjectDefinitionId, ButtonCardDefinition> definitions =
                new Dictionary<ObjectDefinitionId, ButtonCardDefinition>();
            for (int i = 0; i < gameDefinition.Cards.Count; i++)
            {
                CardDefinitionData card = gameDefinition.Cards[i];
                if (!card.RepresentedControllerInput.HasValue)
                {
                    continue;
                }

                if (!Guid.TryParse(card.StableId, out Guid stableId))
                    throw new InvalidOperationException($"Controller Card '{card.DisplayName}' has an invalid stable ID.");
                ObjectDefinitionId definitionId = new ObjectDefinitionId(stableId);
                definitions.Add(
                    definitionId,
                    new ButtonCardDefinition(definitionId, ToButtonCardKind(card.RepresentedControllerInput.Value)));
            }

            return definitions;
        }

        private static ButtonCardKind ToButtonCardKind(ControllerInput input)
        {
            switch (input)
            {
                case ControllerInput.Up: return ButtonCardKind.Up;
                case ControllerInput.Down: return ButtonCardKind.Down;
                case ControllerInput.Left: return ButtonCardKind.Left;
                case ControllerInput.Right: return ButtonCardKind.Right;
                case ControllerInput.A: return ButtonCardKind.A;
                case ControllerInput.B: return ButtonCardKind.B;
                case ControllerInput.X: return ButtonCardKind.X;
                case ControllerInput.Y: return ButtonCardKind.Y;
                default: throw new ArgumentOutOfRangeException(nameof(input));
            }
        }

        private void ConfigureTabletopCameraFraming()
        {
            if (cameraInputAdapter == null
                || cameraInputAdapter.CameraController == null
                || coordinateConverter == null)
            {
                return;
            }

            Bounds? localBounds = null;
            // The hand is a camera tray; it is not part of the framed table area.

            if (consoleView != null && consoleView.gameObject.activeInHierarchy)
            {
                EncapsulateBounds(
                    ref localBounds,
                    CreatePresentationBounds(consoleView.transform, 0.75f));
            }

            Bounds? sharedBounds = null;
            if (matchState != null && !centralPlayAreaId.IsEmpty)
            {
                PlayAreaState playArea = matchState.GetPlayArea(centralPlayAreaId);
                sharedBounds = CreateWorldBounds(playArea.Bounds);
            }

            Bounds? surroundingPlayerBounds = null;
            if (playerLayout != null)
            {
                for (int i = 0; i < playerLayout.Seats.Count; i++)
                {
                    PlayerSeatLayoutEntry seat = playerLayout.Seats[i];
                    EncapsulateBounds(
                        ref surroundingPlayerBounds,
                        coordinateConverter.ToWorldPosition(seat.PlayerZonePose.Position));
                    EncapsulateBounds(
                        ref surroundingPlayerBounds,
                        coordinateConverter.ToWorldPosition(seat.HandAnchorPose.Position));
                    EncapsulateBounds(
                        ref surroundingPlayerBounds,
                        coordinateConverter.ToWorldPosition(seat.ConsoleAnchorPose.Position));
                }
            }

            for (int i = 0; i < playerConsoleViews.Count; i++)
            {
                ConsoleView playerConsole = playerConsoleViews[i];
                if (playerConsole != null && playerConsole.gameObject.activeInHierarchy)
                {
                    EncapsulateBounds(
                        ref surroundingPlayerBounds,
                        CreatePresentationBounds(playerConsole.transform, 0.5f));
                }
            }

            Bounds? mappingBounds = null;
            if (sceneControllerMappingArea != null)
            {
                mappingBounds = CreatePresentationBounds(sceneControllerMappingArea, 0.5f);
            }

            float seatYawDegrees = 0f;
            Vector3? localSeatAnchor = null;
            if (localSeatLayout != null)
            {
                Quaternion seatRotation = coordinateConverter.ToWorldRotation(
                    new TabletopPose(
                        TableCoordinate.Zero,
                        localSeatLayout.FacingRotationDegrees,
                        0,
                        0));
                seatYawDegrees = seatRotation.eulerAngles.y;
                localSeatAnchor = coordinateConverter.ToWorldPosition(
                    localSeatLayout.PlayerZonePose.Position);
            }

            cameraInputAdapter.CameraController.ConfigureFramingTargets(
                localBounds,
                sharedBounds,
                seatYawDegrees,
                mappingBounds,
                sceneControllerMappingArea != null
                    ? sceneControllerMappingArea.gameObject
                    : null,
                localSeatAnchor,
                surroundingPlayerBounds);
        }

        private static void EncapsulateBounds(ref Bounds? aggregate, Bounds addition)
        {
            if (aggregate.HasValue)
            {
                Bounds expanded = aggregate.Value;
                expanded.Encapsulate(addition);
                aggregate = expanded;
                return;
            }

            aggregate = addition;
        }

        private static void EncapsulateBounds(ref Bounds? aggregate, Vector3 point)
        {
            if (aggregate.HasValue)
            {
                Bounds expanded = aggregate.Value;
                expanded.Encapsulate(point);
                aggregate = expanded;
                return;
            }

            aggregate = new Bounds(point, Vector3.zero);
        }

        private Bounds CreateWorldBounds(TabletopBounds bounds)
        {
            Vector3 minimum = coordinateConverter.ToWorldPosition(bounds.Minimum);
            Vector3 maximum = coordinateConverter.ToWorldPosition(bounds.Maximum);
            Bounds worldBounds = new Bounds(minimum, Vector3.zero);
            worldBounds.Encapsulate(maximum);
            Vector3 size = worldBounds.size;
            size.y = Mathf.Max(size.y, 0.1f);
            worldBounds.size = size;
            return worldBounds;
        }

        private static Bounds CreatePresentationBounds(Transform root, float minimumPlanarExtent)
        {
            bool found = false;
            Bounds bounds = new Bounds(root.position, Vector3.zero);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            Vector3 extents = bounds.extents;
            extents.x = Mathf.Max(extents.x, minimumPlanarExtent);
            extents.y = Mathf.Max(extents.y, 0.05f);
            extents.z = Mathf.Max(extents.z, minimumPlanarExtent);
            bounds.extents = extents;
            return bounds;
        }

        private void ProjectTrapFloorCameraBookmark()
        {
            if (cameraInputAdapter.CameraController.ShowInitialGameView())
            {
                return;
            }

            IReadOnlyList<GameTemplateCameraBookmarkDefinition> bookmarks =
                prototypeTemplateContext.Session.CameraBookmarks;
            if (bookmarks.Count == 0)
            {
                return;
            }

            GameTemplateCameraBookmarkDefinition bookmark = bookmarks[0];
            cameraInputAdapter.CameraController.TransitionTo(
                new TabletopCameraBookmark(
                    bookmark.Name,
                    bookmark.FocusCoordinate,
                    bookmark.OrthographicSize));
        }

        private void ApplyAuthoredPose(Transform target, TabletopPose pose)
        {
            target.SetPositionAndRotation(
                coordinateConverter.ToWorldPosition(pose),
                coordinateConverter.ToWorldRotation(pose));
        }

        private void ApplyConsolePose(
            Transform target,
            TabletopPose pose,
            float? surfaceHeight)
        {
            Vector3 worldPosition = coordinateConverter.ToWorldPosition(pose);
            if (surfaceHeight.HasValue)
            {
                worldPosition.y = surfaceHeight.Value;
            }

            // Rest the Console's lowest body point on the surface (P1a); the authoritative pose and
            // surface height are unchanged.
            worldPosition.y += ComponentRestHeight.RestLift(target);
            target.SetPositionAndRotation(
                worldPosition,
                coordinateConverter.ToWorldRotation(pose));
        }

        private void ProjectTemplateBoardSurface()
        {
            if (gameBoardPhysicalSurface == null)
            {
                throw new InvalidOperationException(
                    "Trap Floor Presentation requires an authored Game Board physical surface.");
            }

            if (!(gameBoardPhysicalSurface is BoxCollider boardCollider))
            {
                throw new InvalidOperationException(
                    "Trap Floor's rectangular Board physical surface must use a BoxCollider.");
            }

            PlayAreaState board = matchState.GetPlayArea(centralPlayAreaId);
            TabletopPose boardPose = new TabletopPose(board.Bounds.Center, 0f, 0, 0);
            Transform boardTransform = boardCollider.transform;
            boardTransform.SetPositionAndRotation(
                coordinateConverter.ToWorldPosition(boardPose),
                coordinateConverter.ToWorldRotation(boardPose));

            Vector3 lossyScale = boardTransform.lossyScale;
            float scaleX = Mathf.Abs(lossyScale.x);
            float scaleY = Mathf.Abs(lossyScale.y);
            float scaleZ = Mathf.Abs(lossyScale.z);
            if (scaleX <= Mathf.Epsilon || scaleY <= Mathf.Epsilon || scaleZ <= Mathf.Epsilon)
            {
                throw new InvalidOperationException(
                    "Trap Floor Board physical surface requires a non-zero Transform scale.");
            }

            Vector3 size = boardCollider.size;
            size.x = (float)board.Bounds.Width
                * coordinateConverter.WorldXAxisPerTableUnit.magnitude / scaleX;
            size.z = (float)board.Bounds.Height
                * coordinateConverter.WorldYAxisPerTableUnit.magnitude / scaleZ;
            boardCollider.size = size;

            Vector3 up = coordinateConverter.WorldUp;
            float halfThickness = Mathf.Abs(Vector3.Dot(
                boardTransform.TransformVector(Vector3.up * (size.y * 0.5f)),
                up));
            Vector3 centerOffset = boardTransform.TransformVector(boardCollider.center);
            boardTransform.position = coordinateConverter.ToWorldPosition(boardPose)
                + (up * halfThickness)
                - centerOffset;

            PrepareGameBoardVisual(boardCollider);
        }

        private void SetGameBoardActive(bool active)
        {
            if (gameBoardPhysicalSurface == null)
            {
                return;
            }

            if (!active && gameBoardVisualRoot != null)
            {
                gameBoardVisualRoot.gameObject.SetActive(false);
            }

            gameBoardPhysicalSurface.enabled = active;
            gameBoardPhysicalSurface.gameObject.SetActive(active);

            if (active && gameBoardVisualRoot != null)
            {
                gameBoardVisualRoot.gameObject.SetActive(true);
            }
        }

        private void PrepareGameBoardVisual(BoxCollider boardCollider)
        {
            GameObject visualObject = gameBoardVisualRoot.gameObject;
            visualObject.SetActive(false);

            UnityCamera[] importedCameras = gameBoardVisualRoot.GetComponentsInChildren<UnityCamera>(true);
            for (int i = 0; i < importedCameras.Length; i++)
            {
                importedCameras[i].enabled = false;
            }

            Light[] importedLights = gameBoardVisualRoot.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < importedLights.Length; i++)
            {
                importedLights[i].enabled = false;
            }

            Collider[] importedColliders = gameBoardVisualRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < importedColliders.Length; i++)
            {
                importedColliders[i].enabled = false;
            }

            Rigidbody[] importedRigidbodies = gameBoardVisualRoot.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < importedRigidbodies.Length; i++)
            {
                importedRigidbodies[i].useGravity = false;
                importedRigidbodies[i].isKinematic = true;
                importedRigidbodies[i].detectCollisions = false;
            }

            Animator[] importedAnimators = gameBoardVisualRoot.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < importedAnimators.Length; i++)
            {
                importedAnimators[i].enabled = false;
            }

            Renderer[] importedRenderers = gameBoardVisualRoot.GetComponentsInChildren<Renderer>(true);
            List<Renderer> boardRenderers = new List<Renderer>();
            for (int i = 0; i < importedRenderers.Length; i++)
            {
                Renderer renderer = importedRenderers[i];
                renderer.enabled = UsesMaterial(renderer, gameBoardVisualMaterial);
                if (renderer.enabled)
                {
                    boardRenderers.Add(renderer);
                }
            }

            if (boardRenderers.Count == 0)
            {
                throw new InvalidOperationException(
                    "The imported Trap Floor Board visual has no renderer using its configured Board material.");
            }

            gameBoardVisualRoot.localPosition = Vector3.zero;
            gameBoardVisualRoot.localRotation = Quaternion.identity;
            gameBoardVisualRoot.localScale = Vector3.one;

            Bounds sourceBounds = CalculateLocalRendererBounds(boardCollider.transform, boardRenderers);
            Vector3 sourceSize = sourceBounds.size;
            if (sourceSize.x <= Mathf.Epsilon
                || sourceSize.y <= Mathf.Epsilon
                || sourceSize.z <= Mathf.Epsilon)
            {
                throw new InvalidOperationException(
                    "The imported Trap Floor Board visual requires non-zero three-dimensional renderer bounds.");
            }

            gameBoardVisualRoot.localScale = new Vector3(
                boardCollider.size.x / sourceSize.x,
                boardCollider.size.y / sourceSize.y,
                boardCollider.size.z / sourceSize.z);

            Bounds fittedBounds = CalculateLocalRendererBounds(boardCollider.transform, boardRenderers);
            Vector3 targetCenter = boardCollider.center;
            gameBoardVisualRoot.localPosition = new Vector3(
                targetCenter.x - fittedBounds.center.x,
                targetCenter.y - (boardCollider.size.y * 0.5f) - fittedBounds.min.y,
                targetCenter.z - fittedBounds.center.z);

            visualObject.SetActive(true);
        }

        private static bool UsesMaterial(Renderer renderer, Material material)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == material)
                {
                    return true;
                }
            }

            return false;
        }

        private static Bounds CalculateLocalRendererBounds(
            Transform targetSpace,
            IReadOnlyList<Renderer> renderers)
        {
            bool initialized = false;
            Bounds combined = default;
            for (int rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Bounds localBounds = renderer.localBounds;
                Vector3 center = localBounds.center;
                Vector3 extents = localBounds.extents;
                for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    Vector3 localCorner = center + new Vector3(
                        (cornerIndex & 1) == 0 ? -extents.x : extents.x,
                        (cornerIndex & 2) == 0 ? -extents.y : extents.y,
                        (cornerIndex & 4) == 0 ? -extents.z : extents.z);
                    Vector3 targetPoint = targetSpace.InverseTransformPoint(
                        renderer.transform.TransformPoint(localCorner));
                    if (!initialized)
                    {
                        combined = new Bounds(targetPoint, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        combined.Encapsulate(targetPoint);
                    }
                }
            }

            return combined;
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
            handContainerId = context.HandContainerId;
            primaryStackContainerId = ContainerId.Empty;
            centralPlayAreaId = context.CentralPlayAreaId;
            cardState = matchState.Cards[context.LooseCardId];
            pawnState = matchState.Pawns[context.PawnId];
            tokenState = context.TokenId.IsEmpty
                ? null
                : matchState.Tokens[context.TokenId];

            foreach (KeyValuePair<TabletopObjectId, string> label in context.LabelsByCardId)
            {
                labelsByCardId.Add(label.Key, label.Value);
            }

            foreach (KeyValuePair<ObjectDefinitionId, ButtonCardDefinition> definition in context.ButtonDefinitions)
            {
                buttonDefinitions.Add(definition.Key, definition.Value);
            }
        }

        private void RestorePresentationAfterFailedRebuild(
            MatchState previousMatch,
            PrototypeSessionUndoSnapshot previousSnapshot,
            string operationName)
        {
            bool wasRebuildingFromUndo = rebuildingFromUndo;
            try
            {
                Shutdown(true);
                activeSession.ReplaceCurrentMatch(previousMatch);
                pendingRestoredTrapFloorState = previousSnapshot.TrapFloor?.Restore();
                pendingRestoredControllerPurchaseState =
                    previousSnapshot.PendingControllerPurchase?.Restore();
                rebuildingFromUndo = true;
                InitializeActiveSession(false);
                Debug.LogWarning(
                    $"{operationName} Presentation rebuild failed; the previous authoritative state and Presentation were restored.",
                    this);
            }
            catch (Exception recoveryException)
            {
                Debug.LogError(
                    $"{operationName} Presentation recovery failed: {recoveryException.Message}",
                    this);
            }
            finally
            {
                pendingRestoredTrapFloorState = null;
                pendingRestoredControllerPurchaseState = null;
                rebuildingFromUndo = wasRebuildingFromUndo;
                RefreshUndoUi();
            }
        }

        private void ValidateTrapFloorPrefabReferences()
        {
            ValidateObjectPrefab(catalogPawnPrefab, nameof(catalogPawnPrefab));
            ValidateObjectPrefab(catalogTokenPrefab, nameof(catalogTokenPrefab));

            if (catalogConsolePrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "The catalog Console entry must reference a prefab asset.");
            }

            ConsoleSlotView[] slotViews = catalogConsolePrefab.GetComponentsInChildren<ConsoleSlotView>(true);
            ValidateSeatConsolesMatchLayout();

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
            // Every card, the local Avatar included, and every pawn and token is a catalog spawn (C3b-1).
            foreach (CardInstanceState card in matchState.Cards.Values)
            {
                CardView createdView = CreateCardView(
                    card,
                    labelsByCardId[card.BaseState.Id],
                    out TabletopSelectionVisual createdSelectionVisual);
                cardViews.Add(createdView);
                cardSelectionVisuals.Add(createdSelectionVisual);
            }

            foreach (PawnState pawn in matchState.Pawns.Values)
            {
                PawnView createdPawnView = CreatePawnView(pawn, out TabletopSelectionVisual createdSelectionVisual);
                pawnViews.Add(createdPawnView);
                pawnSelectionVisuals.Add(createdSelectionVisual);
            }

            ConfigureOfficialPawnPresentation();

            foreach (TokenState token in matchState.Tokens.Values)
            {
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
            localHandVisual = CreateLocalHandVisual();
            handView = localHandVisual.GetView<HandView>();
            handView.ConfigurePresentation(presentationTransitions, localHandVisual.FeedbackRenderer);
            if (handTrayRig == null)
            {
                handTrayRig = HandTrayRig.Create(
                    targetCamera,
                    localHandVisual.TargetCollider.gameObject.layer,
                    localHandVisual.FeedbackRenderer.sharedMaterial);
            }

            handTrayRig.Activate();
            handView.ConfigureTray(handTrayRig);

            for (int playerIndex = 0; playerIndex < trapFloorTemplate.Players.Count; playerIndex++)
            {
                TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[playerIndex];
                RuntimeDeckInstance controllerDeck = CreateRuntimeDeckInstance(
                    $"Player {playerIndex + 1} Controller Deck",
                    $"P{playerIndex + 1} CTRL",
                    player.ControllerDeckId,
                    false);
                runtimeDeckInstances.Add(controllerDeck);
                GameTemplatePileStyle deckPileStyle = ResolveTemplatePileStyle(player.ControllerDeckId)
                    ?? GameTemplatePileStyle.DefaultFor(ContainerKind.Deck);
                controllerDeck.View.ConfigureTableRest(true, deckPileStyle.MaximumPileHeight);
                ConfigurePileBay(controllerDeck.Visual, deckPileStyle, player.SeatId);

                controllerDeckViews.Add(controllerDeck.View);
                ContainerId actionAreaId = player.ActionAbilityAreaContainerId;
                StackRuntimeView actionArea = CreateStackRuntimeView(
                    $"P{playerIndex + 1} ACTIONS",
                    matchState.GetContainer(actionAreaId),
                    matchState.ContainerPlacements[actionAreaId],
                    false,
                    ResolveTemplatePileStyle(actionAreaId));
                stackViewsByContainerId.Add(actionAreaId, actionArea);

                if (player.LayoutSeatIndex != localPlayerLayoutSeatIndex
                    && matchState.TryGetContainerPlacement(player.HandContainerId, out ContainerPlacementState hiddenHandZone)
                    && hiddenHandZone.HasExtent)
                {
                    hiddenHandViews.Add(CreateHiddenHandView(
                        $"Player {playerIndex + 1} Hidden Hand",
                        matchState.GetContainer(player.HandContainerId),
                        hiddenHandZone));
                }

                // Every seat's Console, the local one included, is the catalog Console (C3b-1).
                RuntimeConsoleInstance playerConsole = CreateRuntimeConsoleInstance(
                    $"Player {playerIndex + 1} Console",
                    player.LayoutSeatIndex,
                    player.SeatId);
                runtimeConsoleInstances.Add(playerConsole);
                playerConsoleViews.Add(playerConsole.View);
                if (player.LayoutSeatIndex == localPlayerLayoutSeatIndex)
                {
                    consoleView = playerConsole.View;
                }
            }

        }

        // The local Hand from the catalog Hand entry, at the pose the scene Hand had (it lives in the camera tray;
        // the table plate stays hidden and is never a drop target).
        private PrototypeFixedContainerVisual CreateLocalHandVisual()
        {
            PrototypeFixedContainerVisual visual = Instantiate(catalogHandPrefab);
            GameObject root = PrepareRuntimeRoot(visual.gameObject, "Local Hand");
            root.transform.SetPositionAndRotation(LocalHandRestPosition, Quaternion.identity);
            visual.ValidateReferences();
            ApplyFixedContainerVisualHide(visual, true);
            return visual;
        }

        private void BindContainerViews()
        {
            ContainerState hand = matchState.GetContainer(handContainerId);

            handView.Bind(hand, localHandVisual.LayoutAnchor, coordinateConverter, cardViews);
            foreach (StackRuntimeView stackRuntimeView in stackViewsByContainerId.Values)
            {
                stackRuntimeView.View.Bind(
                    stackRuntimeView.Container,
                    stackRuntimeView.Placement,
                    stackRuntimeView.Visual.LayoutAnchor,
                    coordinateConverter,
                    cardViews);
            }

            consoleSlotViews.Clear();
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

            AssignPileBayMouthTargets();

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

            for (int i = 0; i < runtimeTokenContainerInstances.Count; i++)
            {
                RuntimeTokenContainerInstance instance = runtimeTokenContainerInstances[i];
                instance.View.Bind(
                    matchState.GetContainer(instance.ContainerId),
                    instance.Pose,
                    coordinateConverter,
                    tokenViews,
                    instance.DisplayLabel,
                    instance.ColumnCount,
                    instance.ColumnSpacing,
                    instance.RowSpacing);
            }

            ConfigureContainerLabel(localHandVisual.Label, "HAND");
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
            for (int i = 0; i < runtimeDeckInstances.Count; i++)
            {
                RuntimeDeckInstance instance = runtimeDeckInstances[i];
                ConfigureFixedContainer(instance.Visual, instance.View);
            }

            ConfigureHandTrayDropTarget();
            foreach (StackRuntimeView stackRuntimeView in stackViewsByContainerId.Values)
            {
                ConfigureStackDropTarget(stackRuntimeView);
            }

            for (int i = 0; i < consoleSlotViews.Count; i++)
            {
                ConfigureConsoleSlot(consoleSlotViews[i]);
            }

            for (int i = 0; i < runtimeTokenContainerInstances.Count; i++)
            {
                RuntimeTokenContainerInstance instance = runtimeTokenContainerInstances[i];
                instance.DropTarget.Configure(instance.View, instance.TargetCollider);
                instance.DropTarget.enabled = true;
                instance.TargetCollider.enabled = true;
            }
        }

        // The table hand plate stays bound for its HandView but is hidden and never a drop target;
        // the camera tray band receives hand drops and shows the hand feedback instead.
        private void ConfigureHandTrayDropTarget()
        {
            localHandVisual.DropTarget.ClearConfiguration();
            localHandVisual.DropTarget.enabled = false;
            localHandVisual.TargetCollider.enabled = false;
            localHandVisual.SetBasePlateHidden(true);
            localHandVisual.ClearFeedback();
            localHandVisual.Label.gameObject.SetActive(false);
            handTrayRig.ConfigureDropTarget(handView);
            feedbackTargetsByContainerId[handView.ContainerId] = new ContainerFeedbackTarget(handTrayRig);
        }

        private HiddenHandView CreateHiddenHandView(
            string name,
            ContainerState hand,
            ContainerPlacementState zone)
        {
            GameObject root = PrepareRuntimeRoot(new GameObject(name), name);
            HiddenHandView view = root.AddComponent<HiddenHandView>();
            GameObject labelRoot = new GameObject("Hidden Hand Count Label");
            labelRoot.layer = root.layer;
            labelRoot.transform.SetParent(root.transform, false);
            labelRoot.transform.localScale = Vector3.one * 0.34f;
            TextMesh label = labelRoot.AddComponent<TextMesh>();
            ConfigurePrototypeLabel(
                label,
                string.Empty,
                TrapFloorCoinAreaLabelCharacterSize,
                TrapFloorCoinAreaLabelFontSize);
            view.Bind(hand, zone, coordinateConverter, cardViews, label);
            return view;
        }

        private void ReleaseHiddenHandViews()
        {
            for (int i = 0; i < hiddenHandViews.Count; i++)
            {
                HiddenHandView hidden = hiddenHandViews[i];
                if (hidden == null)
                {
                    continue;
                }

                if (hidden.IsBound)
                {
                    hidden.Unbind();
                }

                Destroy(hidden.gameObject);
            }

            hiddenHandViews.Clear();
        }

        // Another seat's hand while this session has a local hand: shown face-down, contents not inspectable.
        private bool IsHiddenHandContainer(ContainerState container)
        {
            return container != null
                && container.Kind == ContainerKind.Hand
                && !handContainerId.IsEmpty
                && container.Id != handContainerId;
        }

        private void BuildInteractionGraph()
        {
            selectionState = new TabletopSelectionState();
            hitResolver = new TabletopObjectHitResolver(targetCamera, interactionLayerMask, maximumHitDistance);
            pointerProjector = new TabletopPointerProjector(
                targetCamera,
                coordinateConverter,
                coordinateConverter.ToWorldPosition(new TableCoordinate(0d, 0d)).y);
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
            dropTargetResolver.PhysicalSurfaces = physicalSurfaceQuery;
            tokenDropTargetResolver = new TokenDropTargetResolver(
                targetCamera,
                interactionLayerMask,
                maximumHitDistance);
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
                CommitToolboxPlacement,
                HandlePlacementRotationChanged);
            componentPlacementController.PhysicalSurfaces = physicalSurfaceQuery;
            inputFrameCoordinator.ConfigureComponentPlacement(componentPlacementController);
            componentPlacementInputConfiguredByComposition = true;
        }

        private void RebuildInteractionDependencies()
        {
            RegisterPhysicalViews();
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
                handReflowDuration,
                HandleConsoleCardInteractionAccepted);
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
                magneticDistance,
                tokenDropTargetResolver,
                new TransferTokenUseCase(),
                tokenContainerViews);
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
            interactionRouter.HandView = handView;
            hitResolver.HandPicker = handView;
            moveCoordinator.IsContainerAtComfortCap = IsOwnHandAtComfortCap;
            moveCoordinator.ComfortCapRejected = ShowHandCapReached;
            containedCardDragCoordinator.IsContainerAtComfortCap = IsOwnHandAtComfortCap;
            containedCardDragCoordinator.ComfortCapRejected = ShowHandCapReached;
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

        private void RegisterPhysicalViews()
        {
            foreach (CardView view in cardViews) physicalAuthority?.Register(view);
            foreach (PawnView view in pawnViews) physicalAuthority?.Register(view);
            foreach (TokenView view in tokenViews) physicalAuthority?.Register(view);
            foreach (DieView view in dieViews) physicalAuthority?.Register(view);
        }

        private void RebuildLayoutViewCollection()
        {
            layoutViews.Clear();
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

            for (int i = 0; i < runtimeDiscardPileInstances.Count; i++)
            {
                DiscardPileView runtimeDiscardView = runtimeDiscardPileInstances[i].View;
                if (runtimeDiscardView != null && runtimeDiscardView.IsBound)
                {
                    layoutViews.Add(runtimeDiscardView);
                }
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
            if (containerId.IsEmpty)
            {
                return;
            }

            if (containerId == handContainerId && handView != null)
            {
                handView.ApplyAcceptedLayout();
            }
            else if (stackViewsByContainerId.TryGetValue(containerId, out StackRuntimeView stackRuntimeView))
            {
                stackRuntimeView.View.ApplyAcceptedLayout();
            }
            else
            {
                for (int i = 0; i < hiddenHandViews.Count; i++)
                {
                    if (hiddenHandViews[i] != null && hiddenHandViews[i].ContainerId == containerId)
                    {
                        hiddenHandViews[i].ApplyAcceptedLayout();
                    }
                }

                for (int i = 0; i < controllerDeckViews.Count; i++)
                {
                    if (controllerDeckViews[i].ContainerId == containerId)
                    {
                        controllerDeckViews[i].ApplyAcceptedLayout();
                    }
                }

                if (TryGetRuntimeDiscardPile(containerId, out RuntimeDiscardPileInstance discardPile))
                {
                    discardPile.View.ApplyAcceptedLayout();
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

        private bool TryGetDeckPresentation(
            ContainerId containerId,
            out DeckView resolvedView,
            out PrototypeFixedContainerVisual resolvedVisual)
        {
            for (int i = 0; i < runtimeDeckInstances.Count; i++)
            {
                RuntimeDeckInstance instance = runtimeDeckInstances[i];
                if (instance.ContainerId == containerId
                    && instance.View != null
                    && instance.View.IsBound
                    && instance.Visual != null)
                {
                    resolvedView = instance.View;
                    resolvedVisual = instance.Visual;
                    return true;
                }
            }

            resolvedView = null;
            resolvedVisual = null;
            return false;
        }

        // A transfer into a pile with an arrival face (a Discard Pile) changes the Card's face in the same
        // command; show the accepted face. Runs once per accepted transfer, not per frame.
        private void RefreshAcceptedCardFaces()
        {
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView view = cardViews[i];
                if (view != null
                    && view.IsBound
                    && view.CardState != null
                    && view.IsFacePresentationConfigured
                    && view.DisplayedFace != view.CardState.Face)
                {
                    view.ApplyAcceptedFacePresentation();
                }
            }
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
            if (!containerId.IsEmpty
                && matchState.Containers.TryGetValue(containerId, out ContainerState hiddenHand)
                && IsHiddenHandContainer(hiddenHand))
            {
                return false;
            }

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
            return CreateCommandContext(localPlayerId);
        }

        private CommandContext CreateCommandContext(PlayerId requestingPlayerId)
        {
            return new CommandContext(
                CommandId.New(),
                matchState.Id,
                requestingPlayerId,
                matchState.Revision);
        }

        private CardView CreateCardView(
            CardInstanceState card,
            string label,
            out TabletopSelectionVisual selectionVisual)
        {
            PrototypeCardVisualReferences createdVisualReferences = Instantiate(catalogCardPrefab);
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
            ApplySelectionHighlightHide(selectionVisual);
            ConfigureCardVisuals(createdVisualReferences, card, label);
            createdView.Bind(card, coordinateConverter);
            if (trapFloorTemplate != null && trapFloorTemplate.IsFloorCard(card.BaseState.Id))
            {
                clone.transform.localScale = Vector3.one * floorCardVisualScale;
                floorfallTargetPresenter.Register(
                    card.BaseState.Id,
                    createdVisualReferences.FaceUpRenderer);
                abilityTargetPresenter.Register(
                    card.BaseState.Id,
                    createdVisualReferences.FaceUpRenderer,
                    createdVisualReferences.FaceDownRenderer);
            }

            cardVisualReferences.Add(createdVisualReferences);
            return createdView;
        }

        private PawnView CreatePawnView(
            PawnState pawn,
            out TabletopSelectionVisual selectionVisual)
        {
            PawnView createdView = Instantiate(catalogPawnPrefab);
            GameObject root = PrepareRuntimeRoot(createdView.gameObject, "Trap Floor Pawn");
            selectionVisual = createdView.GetComponent<TabletopSelectionVisual>();
            ValidateRuntimeSelectionVisual(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            ApplySelectionHighlightHide(selectionVisual);
            createdView.Bind(pawn, coordinateConverter);
            runtimePawnInstances.Add(new RuntimeObjectInstance(root, createdView, selectionVisual));
            return createdView;
        }

        private void ConfigureOfficialPawnPresentation()
        {
            ClearOfficialPawnPresentation();
            for (int playerIndex = 0; playerIndex < trapFloorTemplate.Players.Count; playerIndex++)
            {
                TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[playerIndex];
                int playerNumber = player.LayoutSeatIndex + 1;
                PawnView officialView = null;
                for (int viewIndex = 0; viewIndex < pawnViews.Count; viewIndex++)
                {
                    PawnView candidate = pawnViews[viewIndex];
                    if (candidate != null && candidate.IsBound && candidate.ObjectId == player.PawnId)
                    {
                        officialView = candidate;
                        break;
                    }
                }

                if (officialView == null)
                {
                    throw new InvalidOperationException("Official Trap Floor Pawn has no bound Presentation View.");
                }

                Color playerColor = PlayerPrototypeColor(player.LayoutSeatIndex);
                Renderer[] renderers = officialView.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null || !renderer.gameObject.name.StartsWith("Pawn", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    MaterialPropertyBlock properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties);
                    properties.SetColor("_BaseColor", playerColor);
                    properties.SetColor("_Color", playerColor);
                    renderer.SetPropertyBlock(properties);
                    officialPawnRenderers.Add(renderer);
                }

                GameObject labelRoot = new GameObject($"P{playerNumber} Pawn Label");
                labelRoot.layer = officialView.gameObject.layer;
                labelRoot.transform.SetParent(officialView.transform, false);
                labelRoot.transform.localPosition = new Vector3(0f, 1.02f, 0f);
                labelRoot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                labelRoot.transform.localScale = Vector3.one * 0.45f;
                TextMesh label = labelRoot.AddComponent<TextMesh>();
                ConfigurePrototypeLabel(label, $"P{playerNumber}", 0.16f, 64);
                ApplyLabelRendererHide(label, PrototypeVisualHide.PawnOwnerLabel);
                officialPawnLabels.Add(labelRoot);
            }
        }

        private void ClearOfficialPawnPresentation()
        {
            for (int i = 0; i < officialPawnRenderers.Count; i++)
            {
                Renderer renderer = officialPawnRenderers[i];
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(null);
                }
            }

            officialPawnRenderers.Clear();
            while (officialPawnLabels.Count > 0)
            {
                int lastIndex = officialPawnLabels.Count - 1;
                GameObject labelRoot = officialPawnLabels[lastIndex];
                officialPawnLabels.RemoveAt(lastIndex);
                DestroyRuntimeOwnedGameObject(labelRoot);
            }
        }

        private static Color PlayerPrototypeColor(int playerIndex)
        {
            switch (playerIndex)
            {
                case 0: return new Color(0.20f, 0.55f, 0.96f);
                case 1: return new Color(0.94f, 0.28f, 0.24f);
                case 2: return new Color(0.28f, 0.78f, 0.38f);
                case 3: return new Color(0.92f, 0.72f, 0.18f);
                default: return new Color(0.75f, 0.75f, 0.75f);
            }
        }

        private TokenView CreateTokenView(
            TokenState token,
            out TabletopSelectionVisual selectionVisual,
            float visualScale)
        {
            TokenView createdView = Instantiate(catalogTokenPrefab);
            GameObject root = PrepareRuntimeRoot(createdView.gameObject, "Token");
            selectionVisual = createdView.GetComponent<TabletopSelectionVisual>();
            ValidateRuntimeSelectionVisual(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            ApplySelectionHighlightHide(selectionVisual);
            createdView.Bind(token, coordinateConverter);
            createdView.transform.localScale = Vector3.one * visualScale;
            runtimeTokenInstances.Add(new RuntimeObjectInstance(root, createdView, selectionVisual));
            return createdView;
        }

        private RuntimeTokenContainerInstance CreateTokenContainerInstance(
            string name,
            ContainerId containerId,
            TabletopPose pose,
            string displayLabel,
            float width,
            float depth,
            int columnCount,
            double columnSpacing,
            double rowSpacing,
            Color color)
        {
            GameObject root = PrepareRuntimeRoot(new GameObject(name), name);
            root.layer = catalogTokenPrefab.gameObject.layer;
            TokenContainerView view = root.AddComponent<TokenContainerView>();
            TabletopTokenContainerDropTarget dropTarget =
                root.AddComponent<TabletopTokenContainerDropTarget>();

            GameObject boundary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (boundary.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(boundary, gameObject.scene);
            }

            boundary.name = "Coin Area";
            boundary.layer = root.layer;
            boundary.transform.SetParent(root.transform, false);
            boundary.transform.localPosition = new Vector3(0f, 0.0125f, 0f);
            boundary.transform.localScale = new Vector3(width, 0.025f, depth);
            Renderer renderer = boundary.GetComponent<Renderer>();
            renderer.sharedMaterial = catalogHandPrefab.FeedbackRenderer.sharedMaterial;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Color areaColor = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 1f);
            properties.SetColor("_BaseColor", areaColor);
            properties.SetColor("_Color", areaColor);
            properties.SetFloat("_Metallic", 0f);
            properties.SetFloat("_Smoothness", 0.08f);
            renderer.SetPropertyBlock(properties);

            BoxCollider targetCollider = boundary.GetComponent<BoxCollider>();
            targetCollider.enabled = false;
            dropTarget.enabled = false;

            GameObject labelRoot = new GameObject("Coin Count Label");
            labelRoot.layer = root.layer;
            labelRoot.transform.SetParent(root.transform, false);
            labelRoot.transform.localPosition = new Vector3(0f, 0.065f, -(depth * 0.5f) - 0.24f);
            labelRoot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelRoot.transform.localScale = Vector3.one * 0.34f;
            TextMesh label = labelRoot.AddComponent<TextMesh>();
            ConfigurePrototypeLabel(
                label,
                displayLabel,
                TrapFloorCoinAreaLabelCharacterSize,
                TrapFloorCoinAreaLabelFontSize);
            view.Configure(label);

            ApplyAuthoredPose(root.transform, pose);
            return new RuntimeTokenContainerInstance(
                root,
                view,
                dropTarget,
                targetCollider,
                containerId,
                pose,
                displayLabel,
                columnCount,
                columnSpacing,
                rowSpacing);
        }

        private DieView CreateDieView(
            DieState die,
            string name,
            out TabletopSelectionVisual selectionVisual)
        {
            DieView createdView = Instantiate(catalogDiePrefab);
            GameObject root = PrepareRuntimeRoot(createdView.gameObject, name);
            ConfigurePrototypeLabel(createdView.ResultLabel, createdView.ResultLabel.text, 0.18f, 64);
            ApplyLabelRendererHide(createdView.ResultLabel, PrototypeVisualHide.DieResultLabel);
            selectionVisual = createdView.GetComponent<TabletopSelectionVisual>();
            ValidateRuntimeSelectionVisual(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            ApplySelectionHighlightHide(selectionVisual);
            createdView.Bind(die, coordinateConverter);
            runtimeDieInstances.Add(new RuntimeObjectInstance(root, createdView, selectionVisual));
            return createdView;
        }

        private RuntimeDeckInstance CreateRuntimeDeckInstance(
            string name,
            string displayLabel,
            ContainerId containerId,
            bool labelIsDecoration)
        {
            PrototypeFixedContainerVisual visual = Instantiate(catalogDeckPrefab);
            GameObject root = PrepareRuntimeRoot(visual.gameObject, name);
            visual.ValidateReferences();
            DeckView view = visual.GetView<DeckView>();
            ConfigureContainerLabel(visual.Label, displayLabel);
            visual.DropTarget.ClearConfiguration();
            visual.DropTarget.enabled = false;
            visual.TargetCollider.enabled = false;
            visual.ClearFeedback();
            ApplyFixedContainerVisualHide(visual, labelIsDecoration);
            return new RuntimeDeckInstance(root, visual, view, containerId);
        }

        private RuntimeConsoleInstance CreateRuntimeConsoleInstance(
            string name,
            int layoutSeatIndex,
            SeatId seatId)
        {
            ConsoleView view = Instantiate(catalogConsolePrefab);
            GameObject root = PrepareRuntimeRoot(view.gameObject, name);
            if (!playerLayout.TryGetSeat(layoutSeatIndex, out _))
            {
                throw new InvalidOperationException("Trap Floor Console references a missing Player Layout Seat.");
            }

            SeatState seat = matchState.GetSeat(seatId);
            ApplyConsolePose(
                root.transform,
                seat.ConsolePose,
                seat.ConsoleSurfaceHeight);
            ConsoleSlotView[] slotViews = view.GetComponentsInChildren<ConsoleSlotView>(true);
            Array.Sort(slotViews, (left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
            SelectConsoleSlots(
                slotViews,
                seat.Console.SlotCount,
                "Runtime Console Slot visual",
                out ConsoleSlotView[] activeSlotViews,
                out PrototypeConsoleSlotVisual[] slotVisuals);

            return new RuntimeConsoleInstance(root, view, layoutSeatIndex, activeSlotViews, slotVisuals);
        }

        private RuntimeConsoleInstance CreateRuntimeConsoleInstance(
            string name,
            PlacedConsoleState placedConsole)
        {
            ConsoleView view = Instantiate(catalogConsolePrefab);
            GameObject root = PrepareRuntimeRoot(view.gameObject, name);
            ApplyConsolePose(root.transform, placedConsole.Pose, placedConsole.SurfaceHeight);
            ConsoleSlotView[] slotViews = view.GetComponentsInChildren<ConsoleSlotView>(true);
            Array.Sort(
                slotViews,
                (left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
            SelectConsoleSlots(
                slotViews,
                placedConsole.Console.SlotCount,
                "Runtime freeform Console Slot visual",
                out ConsoleSlotView[] visibleSlotViews,
                out PrototypeConsoleSlotVisual[] slotVisuals);

            return new RuntimeConsoleInstance(
                root,
                view,
                -1,
                visibleSlotViews,
                slotVisuals,
                placedConsole.Id);
        }

        // Binds every authored Slot in authored order. Every Console Slot is always usable, so a count
        // mismatch is a data error, never a reason to hide Slots. Presentation-only.
        private static void SelectConsoleSlots(
            ConsoleSlotView[] orderedSlotViews,
            int requiredCount,
            string visualLabel,
            out ConsoleSlotView[] activeSlotViews,
            out PrototypeConsoleSlotVisual[] activeSlotVisuals)
        {
            if (orderedSlotViews.Length != requiredCount)
            {
                throw new InvalidOperationException(
                    $"The Console prefab has {orderedSlotViews.Length} authored Slots; its Console state needs {requiredCount}.");
            }

            activeSlotViews = new ConsoleSlotView[requiredCount];
            activeSlotVisuals = new PrototypeConsoleSlotVisual[requiredCount];
            for (int i = 0; i < requiredCount; i++)
            {
                activeSlotVisuals[i] = orderedSlotViews[i].GetComponent<PrototypeConsoleSlotVisual>();
                RequireReference(activeSlotVisuals[i], $"{visualLabel} {i}");
                activeSlotVisuals[i].ValidateReferences();
                activeSlotViews[i] = orderedSlotViews[i];
            }
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
            visualReferences.AlignFaceLabelsToSurface(tabletopLocalOrderHeight);
            bool isButtonCard = IsButtonCard(card);
            bool isFloorCard = trapFloorTemplate != null
                && trapFloorTemplate.IsFloorCard(card.BaseState.Id);
            Color frontColor = isButtonCard
                ? new Color(0.58f, 0.88f, 0.82f)
                : new Color(0.95f, 0.88f, 0.42f);
            string frontLabel = label;
            string backLabel = isFloorCard ? "MYSTERY" : visualReferences.BackLabel.text;
            bool backLabelIsPlaceholder = true;
            Color backColor = new Color(0.10f, 0.19f, 0.42f);
            if (TryGetAuthoredCardDefinition(card.BaseState.DefinitionId, out CardDefinition authoredDefinition))
            {
                frontLabel = authoredDefinition.DisplayName;
                ApplyCardArtwork(visualReferences.FaceUpRenderer, authoredDefinition.FrontArtwork);
                ApplyCardArtwork(visualReferences.FaceDownRenderer, authoredDefinition.BackArtwork);
            }
            if (isFloorCard
                && trapFloorTemplate.TryGetFloorCardState(
                    matchState,
                    card.BaseState.Id,
                    out TrapFloorFloorCardState floorCard))
            {
                if (trapFloorCollapseState != null
                    && trapFloorCollapseState.IsCollapsed(card.BaseState.Id))
                {
                    frontColor = new Color(0.025f, 0.03f, 0.04f);
                    backColor = frontColor;
                    frontLabel = $"HOLE\n{floorCard.Coordinate}";
                    backLabel = frontLabel;
                    backLabelIsPlaceholder = false;
                }
                else
                {
                    frontColor = TrapFloorContentColor(floorCard.Content.Category);
                    if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                        && trapFloorObjectiveState != null)
                    {
                        bool isClaimed = trapFloorObjectiveState.TryGetClaim(card.BaseState.Id, out _);
                        frontLabel = isClaimed
                            ? $"KEY — CLAIMED\n{floorCard.Content.DisplayName}"
                            : $"KEY — UNCLAIMED\n{floorCard.Content.DisplayName}";
                    }
                    else
                    {
                        frontLabel = $"{floorCard.Content.Category.ToString().ToUpperInvariant()}\n"
                            + floorCard.Content.DisplayName;
                    }
                }
            }

            ApplyCardColor(
                visualReferences.FaceUpRenderer,
                frontColor);
            ApplyCardColor(visualReferences.FaceDownRenderer, backColor);
            ConfigurePrototypeLabel(
                visualReferences.FrontLabel,
                frontLabel,
                isFloorCard
                    ? TrapFloorFloorLabelCharacterSize
                    : TrapFloorCardLabelCharacterSize,
                TrapFloorCardLabelFontSize);
            ConfigurePrototypeLabel(
                visualReferences.BackLabel,
                backLabel,
                TrapFloorCardBackLabelCharacterSize,
                TrapFloorCardLabelFontSize);
            visualReferences.SetBackLabelHidden(
                backLabelIsPlaceholder && HidesPrototypeVisual(PrototypeVisualHide.CardPlaceholderLabels));
            PrototypeVisualHide faceLabelPart = backLabelIsPlaceholder
                ? PrototypeVisualHide.CardFaceLabels
                : PrototypeVisualHide.None; // collapsed floor card: HOLE (x,y) stays visible on both sides
            ApplyLabelRendererHide(visualReferences.FrontLabel, faceLabelPart);
            ApplyLabelRendererHide(visualReferences.BackLabel, faceLabelPart);
        }

        private static Color TrapFloorContentColor(TrapFloorFloorContentCategory category)
        {
            switch (category)
            {
                case TrapFloorFloorContentCategory.Trap:
                    return new Color(0.86f, 0.34f, 0.28f);
                case TrapFloorFloorContentCategory.Friend:
                    return new Color(0.40f, 0.76f, 0.70f);
                case TrapFloorFloorContentCategory.Key:
                    return new Color(0.95f, 0.78f, 0.26f);
                case TrapFloorFloorContentCategory.SecretExit:
                    return new Color(0.56f, 0.48f, 0.84f);
                case TrapFloorFloorContentCategory.Entry:
                    return new Color(0.40f, 0.70f, 0.42f);
                case TrapFloorFloorContentCategory.Ability:
                    return new Color(0.42f, 0.66f, 0.90f);
                default:
                    return new Color(0.95f, 0.88f, 0.42f);
            }
        }

        private bool HidesPrototypeVisual(PrototypeVisualHide part)
        {
            return (prototypeVisualHide & part) != 0;
        }

        private void ApplyFixedContainerVisualHide(PrototypeFixedContainerVisual visual, bool labelIsDecoration)
        {
            visual.Label.gameObject.SetActive(
                !labelIsDecoration || !HidesPrototypeVisual(PrototypeVisualHide.ContainerLabels));
            visual.SetBasePlateHidden(HidesPrototypeVisual(PrototypeVisualHide.ContainerPlates));
        }

        private void ApplySelectionHighlightHide(TabletopSelectionVisual selectionVisual)
        {
            bool visible = !HidesPrototypeVisual(PrototypeVisualHide.SelectionHighlight);
            Renderer[] renderers = selectionVisual.HighlightRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }

        private void ApplyLabelRendererHide(TextMesh label, PrototypeVisualHide part)
        {
            Renderer labelRenderer = label != null ? label.GetComponent<Renderer>() : null;
            if (labelRenderer != null)
            {
                labelRenderer.enabled = !HidesPrototypeVisual(part);
            }
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

        private static void ApplyCardArtwork(Renderer renderer, Texture2D artwork)
        {
            if (artwork == null)
            {
                return;
            }

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetTexture("_BaseMap", artwork);
            properties.SetTexture("_MainTex", artwork);
            renderer.SetPropertyBlock(properties);
        }

        private bool TryGetAuthoredCardDefinition(
            ObjectDefinitionId definitionId,
            out CardDefinition definition)
        {
            if (trapFloorTemplate != null
                && trapFloorGameDefinition != null
                && trapFloorGameDefinition.TryGetCard(definitionId, out definition))
            {
                return true;
            }

            definition = null;
            return false;
        }

        private void HandleConsoleCardInteractionAccepted(IConsoleCardInteraction interaction)
        {
            if (interaction == null)
            {
                return;
            }

            ConsoleCardBehavior behavior = ConsoleCardBehavior.None;
            string cardName = "Card";
            if (TryGetAuthoredCardDefinition(interaction.CardDefinitionId, out CardDefinition definition))
            {
                behavior = definition.ConsoleBehavior;
                cardName = definition.DisplayName;
            }

            bool inserted = interaction is ConsoleCardInserted;
            string verb = inserted ? "inserted" : "removed";
            string direction = inserted ? "into" : "from";
            ShowMessage(
                $"{FormatPlayerName(interaction.ActorPlayerId)} {verb} {cardName} {direction} Console");

            try
            {
                ConsoleCardInteractionAccepted?.Invoke(interaction, behavior);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void HandleTrapFloorConsoleCardInteractionAccepted(
            IConsoleCardInteraction interaction,
            ConsoleCardBehavior behavior)
        {
            HandlePendingPurchaseConsoleInteraction(interaction);
            HandlePendingSearchConsoleInteraction(interaction);

            if (!(interaction is ConsoleCardInserted insertion)
                || behavior != ConsoleCardBehavior.ActivateOnInsert
                || trapFloorAbilityResolutionService == null
                || !TryGetAuthoredCardDefinition(
                    interaction.CardDefinitionId,
                    out CardDefinition definition))
            {
                return;
            }

            TrapFloorAbilityEffect effect =
                TrapFloorAbilityResolutionService.ResolveEffect(definition.EffectMetadata);
            if (effect != TrapFloorAbilityEffect.Disarm
                && effect != TrapFloorAbilityEffect.Shield
                && effect != TrapFloorAbilityEffect.Dodge
                && effect != TrapFloorAbilityEffect.Rush
                && effect != TrapFloorAbilityEffect.Check)
            {
                return;
            }

            TrapFloorAbilityActivationResult result = trapFloorAbilityResolutionService.Activate(
                matchState,
                insertion,
                definition.ToData());
            if (!result.Succeeded)
            {
                ShowMessage(TrapFloorAbilityRejectionMessage(effect, result.Error));
                return;
            }

            if (undoTrackedMatch == matchState && undoHistory.CurrentStateIndex >= 0)
            {
                // The accepted transfer already owns this transaction. Replace its After snapshot
                // with the Trap Floor interpretation instead of creating a second history entry.
                undoHistory.ReplaceCurrentState(CaptureUndoSnapshot());
                RefreshUndoUi();
            }

            CloseContextMenu();
            CloseCardInspect();
            RefreshTrapFloorStatusUi();
            if (effect == TrapFloorAbilityEffect.Dodge)
            {
                RefreshMovementAssistancePresentation();
                ShowMessage("DODGE — Move to a highlighted adjacent Floor");
            }
            else if (effect == TrapFloorAbilityEffect.Rush)
            {
                RefreshMovementAssistancePresentation();
                ShowMessage("RUSH — Move up to 3 spaces");
            }
            else if (effect == TrapFloorAbilityEffect.Check)
            {
                RefreshNeutralizedTrapPresentation();
                ShowMessage(
                    $"{FormatPlayerShortName(interaction.ActorPlayerId)} checked "
                    + $"{result.Trap.Content.DisplayName} — Trap neutralized");
            }
            else
            {
                string action = effect == TrapFloorAbilityEffect.Disarm
                    ? "used Disarm on"
                    : "used Shield against";
                ShowMessage(
                    $"{FormatPlayerShortName(interaction.ActorPlayerId)} {action} "
                    + result.Trap.Content.DisplayName);
            }
        }

        private void HandlePendingPurchaseConsoleInteraction(
            IConsoleCardInteraction interaction)
        {
            if (interaction == null
                || pendingControllerPurchaseState == null
                || !pendingControllerPurchaseState.IsActive
                || !TryGetTrapFloorPlayerSetup(
                    pendingControllerPurchaseState.PlayerId,
                    out TrapFloorPlayerSetupDefinition player))
            {
                return;
            }

            bool changed = interaction is ConsoleCardInserted
                ? pendingControllerPurchaseState.RecordConsoleInsertion(
                    interaction.ActorPlayerId,
                    interaction.ConsoleOwnerPlayerId,
                    interaction.SlotContainerId,
                    interaction.CardInstanceId,
                    player.SideSlotContainerIds)
                : interaction is ConsoleCardRemoved
                    && pendingControllerPurchaseState.RecordConsoleRemoval(
                        interaction.ActorPlayerId,
                        interaction.ConsoleOwnerPlayerId,
                        interaction.SlotContainerId,
                        interaction.CardInstanceId,
                        player.SideSlotContainerIds);
            if (!changed) return;

            ReplaceCurrentUndoStateForPurchaseAssistance();
            RefreshTrapFloorStatusUi();
            string abilityName = FindPurchaseDisplayName(
                pendingControllerPurchaseState.PurchasedCardDefinitionStableId).ToUpperInvariant();
            ShowMessage(pendingControllerPurchaseState.IsPaymentComplete
                ? "PAYMENT READY — Confirm Purchase"
                : $"BUY {abilityName} — Place the selected Cards in your Console");
        }

        private void HandlePendingSearchConsoleInteraction(IConsoleCardInteraction interaction)
        {
            if (interaction == null
                || trapFloorPendingSearchState == null
                || !trapFloorPendingSearchState.IsActive
                || !TryGetTrapFloorPlayerSetup(
                    trapFloorPendingSearchState.PlayerId,
                    out TrapFloorPlayerSetupDefinition player))
            {
                return;
            }

            bool changed = interaction is ConsoleCardInserted
                ? trapFloorPendingSearchState.RecordConsoleInsertion(
                    interaction.ActorPlayerId,
                    interaction.ConsoleOwnerPlayerId,
                    interaction.SlotContainerId,
                    interaction.CardInstanceId,
                    player.SideSlotContainerIds)
                : interaction is ConsoleCardRemoved
                    && trapFloorPendingSearchState.RecordConsoleRemoval(
                        interaction.ActorPlayerId,
                        interaction.ConsoleOwnerPlayerId,
                        interaction.SlotContainerId,
                        interaction.CardInstanceId,
                        player.SideSlotContainerIds);
            if (!changed) return;

            ReplaceCurrentUndoStateForSearchAssistance();
            RefreshTrapFloorStatusUi();
            string prefix = trapFloorPendingSearchState.SearchKind == TrapFloorSearchKind.Careful
                ? "CAREFUL SEARCH"
                : "SEARCH";
            ShowMessage(trapFloorPendingSearchState.IsPaymentComplete
                ? $"{prefix} — Choose a Floor"
                : trapFloorPendingSearchState.SearchKind == TrapFloorSearchKind.Careful
                    ? "CAREFUL SEARCH — Place A + B + X + Y in your Console"
                    : "SEARCH — Place the selected Card in your Console");
        }

        private static string TrapFloorAbilityRejectionMessage(
            TrapFloorAbilityEffect effect,
            TrapFloorAbilityActivationError error)
        {
            switch (error)
            {
                case TrapFloorAbilityActivationError.NoUnresolvedTrap:
                    return effect == TrapFloorAbilityEffect.Check
                        ? "Check unavailable — no unresolved Trap."
                        : $"{effect} has no revealed unresolved Trap for the active Player.";
                case TrapFloorAbilityActivationError.NoPendingTrapConsequence:
                    return "Shield has no pending assisted Trap consequence for the active Player.";
                case TrapFloorAbilityActivationError.AbilityAlreadyUsed:
                    return $"This {effect} Card has already activated.";
                case TrapFloorAbilityActivationError.ActorIsNotActivePlayer:
                    return $"{effect} assistance activates only for the active Player.";
                case TrapFloorAbilityActivationError.PawnFloorUnavailable:
                    return $"{effect} could not identify the active Player's current Floor.";
                case TrapFloorAbilityActivationError.NoValidDodgeDestination:
                    return "Dodge unavailable — no adjacent Floor.";
                case TrapFloorAbilityActivationError.NoValidRushDestination:
                    return "Rush unavailable — no reachable Floor.";
                default:
                    return $"{effect} assistance could not resolve ({error}).";
            }
        }

        private void RefreshMovementAssistancePresentation()
        {
            if (abilityTargetPresenter == null) return;
            IReadOnlyList<TabletopObjectId> targetFloorCardIds =
                trapFloorAbilityResolutionState?.ActiveRushAssistance?.TargetFloorCardIds
                ?? trapFloorAbilityResolutionState?.ActiveDodgeAssistance?.TargetFloorCardIds;
            if (targetFloorCardIds == null)
            {
                abilityTargetPresenter.ClearCurrent();
                return;
            }

            abilityTargetPresenter.Show(targetFloorCardIds);
        }

        private void RefreshNeutralizedTrapPresentation()
        {
            if (abilityTargetPresenter == null) return;
            List<TabletopObjectId> neutralizedFloorCardIds = new List<TabletopObjectId>();
            if (trapFloorAbilityResolutionState != null)
            {
                for (int i = 0; i < trapFloorAbilityResolutionState.TrapRecords.Count; i++)
                {
                    TrapFloorTrapResolutionRecord trap =
                        trapFloorAbilityResolutionState.TrapRecords[i];
                    if (trap.Disposition == TrapFloorTrapResolutionDisposition.Neutralized)
                        neutralizedFloorCardIds.Add(trap.FloorCardId);
                }
            }
            abilityTargetPresenter.ShowNeutralized(neutralizedFloorCardIds);
        }

        private bool IsTrapFloorTrapNeutralized(TabletopObjectId floorCardId)
        {
            return trapFloorAbilityResolutionState != null
                && trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && trap.Disposition == TrapFloorTrapResolutionDisposition.Neutralized;
        }

        private bool WasTrapSafelyRevealed(TabletopObjectId floorCardId)
        {
            if (trapFloorAbilityResolutionState != null
                && trapFloorAbilityResolutionState.TryGetTrap(floorCardId, out _))
                return false;
            if (trapFloorActivityFeed == null) return false;
            for (int i = trapFloorActivityFeed.Entries.Count - 1; i >= 0; i--)
            {
                TrapFloorActivityEntry entry = trapFloorActivityFeed.Entries[i];
                if (entry.FloorCardId == floorCardId
                    && entry.Kind == TrapFloorActivityKind.SafelyRevealedTrap)
                    return true;
            }
            return false;
        }

        private bool IsTrapFloorTrapPending(TabletopObjectId floorCardId)
        {
            return trapFloorAbilityResolutionState != null
                && trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && trap.IsPending;
        }

        private bool IsTrapFloorTrapResolved(TabletopObjectId floorCardId)
        {
            return trapFloorAbilityResolutionState != null
                && trapFloorAbilityResolutionState.TryGetTrap(
                    floorCardId,
                    out TrapFloorTrapResolutionRecord trap)
                && !trap.IsPending;
        }

        private static string FormatInputCost(InputCostDefinition inputCost)
        {
            if (inputCost == null || inputCost.Requirements.Count == 0)
            {
                return string.Empty;
            }

            List<string> entries = new List<string>(inputCost.Requirements.Count);
            for (int i = 0; i < inputCost.Requirements.Count; i++)
            {
                InputRequirement requirement = inputCost.Requirements[i];
                entries.Add($"{requirement.Input} x{requirement.Count}");
            }

            return $"\n\nInput Cost: {string.Join(", ", entries)}";
        }

        // Every Stack (template, Toolbox or Split) is the catalog's Stack pile in its bay; pileStyle null uses
        // the kind default (no mark).
        private StackRuntimeView CreateStackRuntimeView(
            string name,
            ContainerState container,
            ContainerPlacementState placement,
            bool labelIsDecoration,
            GameTemplatePileStyle pileStyle = null)
        {
            PrototypeFixedContainerVisual visual = Instantiate(catalogStackPrefab);
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
            GameTemplatePileStyle style = pileStyle ?? GameTemplatePileStyle.DefaultFor(container.Kind);
            view.ConfigureTableRest(true, style.MaximumPileHeight);

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
            ApplyFixedContainerVisualHide(visual, labelIsDecoration);
            ConfigurePileBay(visual, style, container.OwnerSeatId);
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
            slotVisual.SetEmptyStateHidden(HidesPrototypeVisual(PrototypeVisualHide.EmptySlotPlate));
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
            if (activeSession != null
                && activeSession.Selection.Kind == TabletopSessionKind.EmptyCustom)
            {
                if (Time.unscaledTime <= operationMessageUntil)
                {
                    return operationMessage;
                }

                return $"Empty Table | Objects {matchState?.ObjectCount ?? 0}";
            }

            string counts = $"Trap Floor | Floor Cards {CurrentFloorCardCount()}";
            string objective = CurrentTrapFloorObjectiveText();
            string activity = CurrentTrapFloorActivityText();
            string persistentStatus = string.IsNullOrEmpty(objective)
                ? counts
                : $"{counts}\n{objective}";
            if (!string.IsNullOrEmpty(activity))
            {
                persistentStatus = $"{persistentStatus}\n{activity}";
            }

            return Time.unscaledTime <= operationMessageUntil
                ? $"{operationMessage}\n{persistentStatus}"
                : persistentStatus;
        }

        private string CurrentTrapFloorActivityText()
        {
            if (trapFloorActivityFeed == null || trapFloorActivityFeed.Entries.Count == 0)
            {
                return string.Empty;
            }

            int firstIndex = Math.Max(0, trapFloorActivityFeed.Entries.Count - 2);
            string text = string.Empty;
            for (int i = firstIndex; i < trapFloorActivityFeed.Entries.Count; i++)
            {
                TrapFloorActivityEntry entry = trapFloorActivityFeed.Entries[i];
                string line;
                switch (entry.Kind)
                {
                    case TrapFloorActivityKind.SearchedFloor:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} searched Floor {entry.Coordinate}"
                            + (entry.PaymentInputs.Count > 0 ? $" using {entry.PaymentInputs[0]}" : string.Empty);
                        break;
                    case TrapFloorActivityKind.CarefullySearchedFloor:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} carefully searched Floor {entry.Coordinate}";
                        break;
                    case TrapFloorActivityKind.RevealedFloorContent:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} revealed "
                            + $"{entry.ContentName} [{entry.ContentCategory}]";
                        break;
                    case TrapFloorActivityKind.SafelyRevealedTrap:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} revealed {entry.ContentName} safely";
                        break;
                    case TrapFloorActivityKind.ClaimedKey:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} claimed {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.WonGame:
                        line = "TRAP FLOOR VICTORY";
                        break;
                    case TrapFloorActivityKind.PlayerEscaped:
                        line = $"{FormatPlayerName(entry.ActorPlayerId).ToUpperInvariant()} ESCAPED";
                        break;
                    case TrapFloorActivityKind.TriggeredFloorfall:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} triggered Floorfall";
                        break;
                    case TrapFloorActivityKind.RolledFloorfall:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} rolled "
                            + $"{entry.XAxisResult}/{entry.YAxisResult} for Floor {entry.Coordinate}";
                        break;
                    case TrapFloorActivityKind.CollapsedFloor:
                        line = $"Floor {entry.Coordinate} collapsed";
                        break;
                    case TrapFloorActivityKind.SkippedTurn:
                        line = $"{FormatPlayerName(entry.ActorPlayerId)} skipped their turn";
                        break;
                    case TrapFloorActivityKind.PlayerEliminated:
                        line = entry.ContentDefinitionId.IsEmpty
                            ? $"{FormatPlayerName(entry.ActorPlayerId).ToUpperInvariant()} ELIMINATED"
                            : $"{FormatPlayerShortName(entry.ActorPlayerId)} was eliminated by {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.AllPlayersEliminated:
                        line = "ALL PLAYERS ELIMINATED";
                        break;
                    case TrapFloorActivityKind.PlayersReactivated:
                        line = "ALL PLAYERS REACTIVATED";
                        break;
                    case TrapFloorActivityKind.UsedDisarm:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} used Disarm on {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.UsedShield:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} used Shield against {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.UsedDodge:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} used Dodge on {entry.ContentName}";
                        break;
                    case TrapFloorActivityKind.UsedRush:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} activated Rush";
                        break;
                    case TrapFloorActivityKind.UsedCheck:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} checked "
                            + $"{entry.ContentName} — Trap neutralized";
                        break;
                    case TrapFloorActivityKind.BlindApplied:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} resolved "
                            + $"{entry.ContentName} — Blind applies next Round";
                        break;
                    case TrapFloorActivityKind.BlindDirectionRolled:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} rolled "
                            + $"{entry.BlindDirectionResult} for Blind direction";
                        break;
                    case TrapFloorActivityKind.SlowApplied:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} resolved "
                            + $"{entry.ContentName} — Slow applies next Round";
                        break;
                    case TrapFloorActivityKind.StickyApplied:
                        line = $"{FormatPlayerShortName(entry.ActorPlayerId)} resolved "
                            + $"{entry.ContentName} — Sticky applies next Round";
                        break;
                    default:
                        line = string.Empty;
                        break;
                }

                text = string.IsNullOrEmpty(text) ? line : $"{text}\n{line}";
            }

            return text;
        }

        private string CurrentTrapFloorObjectiveText()
        {
            if (trapFloorObjectiveState == null)
            {
                return string.Empty;
            }

            string progress = CurrentTrapFloorObjectiveProgressText();
            if (trapFloorTurnState != null && trapFloorTurnState.IsCurrentFloorFailed)
            {
                return $"{progress} | ALL PLAYERS ELIMINATED";
            }
            return trapFloorObjectiveState.IsWon ? $"{progress} | VICTORY" : progress;
        }

        private string CurrentTrapFloorObjectiveProgressText()
        {
            string keys =
                $"KEYS {trapFloorObjectiveState.CollectedKeyCount} / {trapFloorObjectiveState.RequiredKeyCount}";
            return trapFloorObjectiveState.ModeBehavior == ModeBehavior.Survival
                ? $"{keys}\nESCAPED {trapFloorObjectiveState.EscapedPlayerCount} / "
                    + $"{activeSession.Request.ActivePlayerIds.Count}"
                : keys;
        }

        private string CurrentTrapFloorPlayerStatesText()
        {
            if (trapFloorTurnState == null) return string.Empty;

            string text = "PLAYERS ";
            for (int i = 0; i < trapFloorTurnState.PlayerOrder.Count; i++)
            {
                PlayerId playerId = trapFloorTurnState.PlayerOrder[i];
                string state = trapFloorObjectiveState != null
                    && trapFloorObjectiveState.IsPlayerEscaped(playerId)
                        ? "ESCAPED"
                        : trapFloorTurnState.GetPlayerState(playerId)
                            == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound
                            ? "ELIMINATED"
                            : "ACTIVE";
                if (trapFloorAbilityResolutionState != null
                    && trapFloorAbilityResolutionState.TryGetBlindStatus(
                        playerId,
                        trapFloorTurnState.CurrentRound,
                        out _))
                {
                    state += " / BLIND";
                }
                if (trapFloorAbilityResolutionState != null
                    && trapFloorAbilityResolutionState.TryGetSlowStatus(
                        playerId,
                        trapFloorTurnState.CurrentRound,
                        out _))
                {
                    state += " / SLOW";
                }
                if (trapFloorAbilityResolutionState != null
                    && trapFloorAbilityResolutionState.TryGetStickyStatus(
                        playerId,
                        trapFloorTurnState.CurrentRound,
                        out _))
                {
                    state += " / STICKY";
                }
                if (i > 0) text += " | ";
                text += $"{FormatPlayerName(playerId)} {state}";
            }
            return text;
        }

        private string CurrentTrapFloorCollapseStatusText()
        {
            if (trapFloorCollapseState == null)
            {
                return string.Empty;
            }

            TrapFloorCollapseRollState roll = trapFloorCollapseState.LastRoll;
            if (trapFloorCollapseState.IsBoardExhausted)
            {
                return $"USABLE FLOORS 0 / {trapFloorCollapseState.TotalFloorCount} | BOARD EXHAUSTED";
            }

            if (trapFloorCollapseState.IsCollapsePending)
            {
                return roll != null && roll.RequiredReroll
                    ? $"FLOORFALL {roll.XAxisResult}/{roll.YAxisResult}: ALREADY A HOLE — REROLLING"
                    : "FLOORFALL: ROLLING 2d6";
            }

            string usable = $"USABLE FLOORS {trapFloorCollapseState.UsableFloorCount}"
                + $" / {trapFloorCollapseState.TotalFloorCount}";
            string collapse = roll == null
                ? usable
                : $"{usable} | LAST {roll.XAxisResult}/{roll.YAxisResult} → {roll.Coordinate}";
            if (trapFloorTemplate == null) return collapse;
            return $"{trapFloorTemplate.ActiveMode.DisplayName} · {trapFloorTemplate.ActiveMode.Behavior} game · "
                + $"{FormatCollapseSchedule(trapFloorTemplate.ActiveMode.Collapse.ScheduleKind)}\n{collapse}";
        }

        private static string FormatCollapseSchedule(CollapseScheduleKind schedule)
        {
            switch (schedule)
            {
                case CollapseScheduleKind.RoundBased:
                    return "floor collapses each round";
                case CollapseScheduleKind.RealTime:
                    return "floor collapses in real time";
                default:
                    return "no collapse";
            }
        }

        private void ApplyCollapsedFloorPresentation(TabletopObjectId floorCardId)
        {
            if (!matchState.Cards.TryGetValue(floorCardId, out CardInstanceState card)
                || !TryGetCardVisualReferences(floorCardId, out PrototypeCardVisualReferences visualReferences))
            {
                return;
            }

            string label = labelsByCardId.TryGetValue(floorCardId, out string configuredLabel)
                ? configuredLabel
                : "FLOOR";
            ConfigureCardVisuals(visualReferences, card, label);
            visualReferences.SetCardContentVisible(true);
            visualReferences.CardView.ApplyAcceptedState();
            if (inspectedCardId == floorCardId)
            {
                CloseCardInspect();
            }
        }

        private void RefreshTrapFloorFloorCardPresentation(TabletopObjectId floorCardId)
        {
            if (!matchState.Cards.TryGetValue(floorCardId, out CardInstanceState card)
                || !TryGetCardVisualReferences(floorCardId, out PrototypeCardVisualReferences visualReferences))
            {
                return;
            }

            string label = labelsByCardId.TryGetValue(floorCardId, out string configuredLabel)
                ? configuredLabel
                : "FLOOR";
            ConfigureCardVisuals(visualReferences, card, label);
            visualReferences.SetCardContentVisible(ShouldShowCardContent(card));
        }

        private int CurrentFloorCardCount()
        {
            if (trapFloorTemplate == null || matchState == null)
            {
                return 0;
            }

            int count = 0;
            foreach (TabletopObjectId objectId in trapFloorTemplate.FloorCardIds.Values)
            {
                if (matchState.Cards.ContainsKey(objectId))
                {
                    count++;
                }
            }

            return count;
        }

        private string OfficialSearchAvailabilityText()
        {
            if (trapFloorRoundState == null)
            {
                return "Official Search is unavailable outside Trap Floor round orchestration.";
            }

            if (trapFloorRoundState.Phase != TrapFloorRoundPhase.Search)
            {
                return $"Official Search is unavailable during {trapFloorRoundState.Phase}.";
            }

            if (trapFloorRoundState.HasCompletedSearchTrigger(localPlayerId))
            {
                return $"{FormatPlayerName(localPlayerId)} already completed Search + Trigger this round.";
            }

            if (floormasterLifecycleState?.HasPendingCard == true)
            {
                return "Search blocked: resolve the pending Card first.";
            }

            return "Official Search is unavailable.";
        }

        private int ContainerCount(ContainerId containerId)
        {
            return matchState != null && matchState.Containers.TryGetValue(containerId, out ContainerState container)
                ? container.Count
                : 0;
        }

        private CardView FindCardView(TabletopObjectId objectId)
        {
            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView candidate = cardViews[i];
                if (candidate != null && candidate.IsBound && candidate.ObjectId == objectId)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Authoritative Card has no bound Presentation View.");
        }

        private string FormatPlayerName(PlayerId playerId)
        {
            if (trapFloorTemplate != null && matchState != null)
            {
                for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[i];
                    if (matchState.GetSeat(player.SeatId).OccupantPlayerId == playerId)
                    {
                        return $"Player {player.LayoutSeatIndex + 1}";
                    }
                }
            }

            return "Participating Player";
        }

        private string FormatPlayerShortName(PlayerId playerId)
        {
            if (trapFloorTemplate != null && matchState != null)
            {
                for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[i];
                    if (matchState.GetSeat(player.SeatId).OccupantPlayerId == playerId)
                    {
                        return $"P{player.LayoutSeatIndex + 1}";
                    }
                }
            }

            return "Player";
        }

        private int AvailableDrawableCount(ContainerId sourceDeckContainerId)
        {
            if (matchState == null
                || !matchState.Containers.TryGetValue(sourceDeckContainerId, out ContainerState deck)
                || !matchState.Containers.TryGetValue(handContainerId, out ContainerState hand))
            {
                return 0;
            }

            int handSpace = hand.Capacity == 0
                ? deck.Count
                : Math.Max(0, hand.Capacity - hand.Count);
            return Math.Min(deck.Count, handSpace);
        }

        private int AvailableControllerDeckCount(ContainerId sourceDeckContainerId)
        {
            return IsAssistedControllerDeck(sourceDeckContainerId)
                ? matchState.GetContainer(sourceDeckContainerId).Count
                : 0;
        }

        private TrapFloorPlayerSetupDefinition GetAssistedTrapFloorPlayerSetup()
        {
            if (trapFloorTemplate == null
                || trapFloorTurnState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
            {
                throw new InvalidOperationException(
                    "Controller assistance requires an active Trap Floor Player turn.");
            }

            if (TryGetTrapFloorPlayerSetup(
                    trapFloorTurnState.ActivePlayerId,
                    out TrapFloorPlayerSetupDefinition player))
                return player;

            throw new InvalidOperationException("The active Player has no authored Controller Hand setup.");
        }

        private bool TryGetTrapFloorPlayerSetup(
            PlayerId playerId,
            out TrapFloorPlayerSetupDefinition resolvedPlayer)
        {
            if (trapFloorTemplate != null && matchState != null && !playerId.IsEmpty)
            {
                for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
                {
                    TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[i];
                    if (matchState.Seats.TryGetValue(player.SeatId, out SeatState seat)
                        && seat.OccupantPlayerId == playerId)
                    {
                        resolvedPlayer = player;
                        return true;
                    }
                }
            }

            resolvedPlayer = null;
            return false;
        }

        private void ShowMessage(string message)
        {
            operationMessage = message;
            operationMessageUntil = Time.unscaledTime + 2.5f;
            runtimeUi?.SetStatusMessage(CurrentStatusText());
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
            ReleaseSceneOwnedFixedContainer(localHandVisual, handView, handContainerId);
            if (localHandVisual != null)
            {
                DestroyRuntimeOwnedGameObject(localHandVisual.gameObject);
                localHandVisual = null;
            }

            if (handTrayRig != null)
            {
                handTrayRig.Deactivate();
            }

            ReleaseHiddenHandViews();

            handView = null;
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
                    visual.DropTarget.enabled = false;
                    visual.TargetCollider.enabled = false;
                    visual.ClearFeedback();
                }

                if (view != null && view.IsBound)
                {
                    view.Unbind();
                }

                controllerDeckViews.Remove(view);
                layoutViews.Remove(view);
                feedbackTargetsByContainerId.Remove(instance.ContainerId);
                runtimeDeckInstances.RemoveAt(lastIndex);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ReleaseRuntimeDeckInstance(ContainerId containerId)
        {
            for (int i = runtimeDeckInstances.Count - 1; i >= 0; i--)
            {
                RuntimeDeckInstance instance = runtimeDeckInstances[i];
                if (instance.ContainerId != containerId)
                {
                    continue;
                }

                GameObject root = instance.Root;
                PrototypeFixedContainerVisual visual = instance.Visual;
                DeckView view = instance.View;
                DisableRuntimeInteraction(root);
                if (visual != null)
                {
                    visual.DropTarget.ClearConfiguration();
                    visual.DropTarget.enabled = false;
                    visual.TargetCollider.enabled = false;
                    visual.ClearFeedback();
                }

                if (view != null && view.IsBound)
                {
                    view.Unbind();
                }

                controllerDeckViews.Remove(view);
                layoutViews.Remove(view);
                feedbackTargetsByContainerId.Remove(containerId);
                runtimeDeckInstances.RemoveAt(i);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
                return;
            }

            throw new InvalidOperationException("Deleted runtime Deck has no Presentation instance.");
        }

        private void ReleaseRuntimeConsoleInstances()
        {
            while (runtimeConsoleInstances.Count > 0)
            {
                int lastIndex = runtimeConsoleInstances.Count - 1;
                RuntimeConsoleInstance instance = runtimeConsoleInstances[lastIndex];
                GameObject root = instance.Root;
                DisableRuntimeInteraction(root);
                if (instance.View != null && instance.View.IsBound)
                {
                    instance.View.Unbind();
                }

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

                playerConsoleViews.Remove(instance.View);
                runtimeConsoleInstances.RemoveAt(lastIndex);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
            }
        }

        private void ReleaseRuntimeConsoleInstance(ConsoleId consoleId)
        {
            for (int instanceIndex = runtimeConsoleInstances.Count - 1; instanceIndex >= 0; instanceIndex--)
            {
                RuntimeConsoleInstance instance = runtimeConsoleInstances[instanceIndex];
                if (instance.ConsoleId != consoleId)
                {
                    continue;
                }

                GameObject root = instance.Root;
                DisableRuntimeInteraction(root);
                if (instance.View != null && instance.View.IsBound)
                {
                    instance.View.Unbind();
                }

                for (int i = 0; i < instance.SlotViews.Length; i++)
                {
                    ConsoleSlotView slotView = instance.SlotViews[i];
                    PrototypeConsoleSlotVisual slotVisual = instance.SlotVisuals[i];
                    ContainerId slotContainerId = slotView != null
                        ? slotView.ContainerId
                        : ContainerId.Empty;
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

                    if (!slotContainerId.IsEmpty)
                    {
                        consoleSlotVisualsByContainerId.Remove(slotContainerId);
                        feedbackTargetsByContainerId.Remove(slotContainerId);
                    }
                }

                playerConsoleViews.Remove(instance.View);
                runtimeConsoleInstances.RemoveAt(instanceIndex);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
                return;
            }

            throw new InvalidOperationException("Deleted runtime Console has no Presentation instance.");
        }

        private void ReleaseRuntimeTokenContainerInstances()
        {
            while (runtimeTokenContainerInstances.Count > 0)
            {
                int lastIndex = runtimeTokenContainerInstances.Count - 1;
                RuntimeTokenContainerInstance instance = runtimeTokenContainerInstances[lastIndex];
                GameObject root = instance.Root;
                if (instance.DropTarget != null)
                {
                    instance.DropTarget.ClearConfiguration();
                    instance.DropTarget.enabled = false;
                }

                if (instance.TargetCollider != null)
                {
                    instance.TargetCollider.enabled = false;
                }

                if (instance.View != null && instance.View.IsBound)
                {
                    instance.View.Unbind();
                }

                tokenContainerViews.Remove(instance.View);
                runtimeTokenContainerInstances.RemoveAt(lastIndex);
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

        private void ReleaseRuntimeObjectInstance<TView>(
            TabletopObjectId objectId,
            List<RuntimeObjectInstance> instances,
            List<TView> views,
            List<TabletopSelectionVisual> selectionVisuals)
            where TView : TabletopObjectView
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                RuntimeObjectInstance instance = instances[i];
                TabletopObjectView view = instance.View;
                if (view == null || view.ObjectId != objectId)
                {
                    continue;
                }

                GameObject root = instance.Root;
                TabletopSelectionVisual selectionVisual = instance.SelectionVisual;
                DisableRuntimeInteraction(root);
                selectionVisual?.SetSelected(false);
                if (view.IsBound)
                {
                    view.Unbind();
                }

                if (view is TView typedView)
                {
                    views.Remove(typedView);
                }

                selectionVisuals.Remove(selectionVisual);
                presentationTransitions?.Forget(view.transform);
                instances.RemoveAt(i);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
                return;
            }

            throw new InvalidOperationException("Deleted runtime Object has no Presentation instance.");
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

            TabletopContainerDropTarget[] dropTargets =
                root.GetComponentsInChildren<TabletopContainerDropTarget>(true);
            for (int i = 0; i < dropTargets.Length; i++)
            {
                if (dropTargets[i] == null)
                {
                    continue;
                }

                dropTargets[i].ClearConfiguration();
                dropTargets[i].enabled = false;
            }

            TabletopTokenContainerDropTarget[] tokenDropTargets =
                root.GetComponentsInChildren<TabletopTokenContainerDropTarget>(true);
            for (int i = 0; i < tokenDropTargets.Length; i++)
            {
                if (tokenDropTargets[i] == null)
                {
                    continue;
                }

                tokenDropTargets[i].ClearConfiguration();
                tokenDropTargets[i].enabled = false;
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                if (rigidbodies[i] == null)
                {
                    continue;
                }

                if (!rigidbodies[i].isKinematic)
                {
                    rigidbodies[i].linearVelocity = Vector3.zero;
                    rigidbodies[i].angularVelocity = Vector3.zero;
                }

                rigidbodies[i].isKinematic = true;
                rigidbodies[i].useGravity = false;
                rigidbodies[i].detectCollisions = false;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            root.SetActive(false);
        }

        private void DestroyRuntimeOwnedGameObject(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(false);
            root.name = $"{root.name} (Pending Runtime Destruction)";
            Destroy(root);
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

        private readonly struct GameObjectActivationSnapshot
        {
            public GameObjectActivationSnapshot(GameObject target, bool wasActive)
            {
                Target = target;
                WasActive = wasActive;
            }

            public GameObject Target { get; }

            public bool WasActive { get; }
        }

        private readonly struct SearchPaymentCardOption
        {
            public SearchPaymentCardOption(
                TabletopObjectId cardId,
                ControllerInput? input,
                string displayName,
                Texture artwork,
                bool eligible)
            {
                CardId = cardId;
                Input = input;
                DisplayName = displayName ?? "Controller Card";
                Artwork = artwork;
                Eligible = eligible;
            }

            public TabletopObjectId CardId { get; }
            public ControllerInput? Input { get; }
            public string DisplayName { get; }
            public Texture Artwork { get; }
            public bool Eligible { get; }
        }

        private readonly struct PurchasePaymentCardOption
        {
            public PurchasePaymentCardOption(
                TabletopObjectId cardId,
                ControllerInput? input,
                string displayName,
                Texture artwork,
                bool eligible)
            {
                CardId = cardId;
                Input = input;
                DisplayName = displayName ?? "Controller Card";
                Artwork = artwork;
                Eligible = eligible;
            }

            public TabletopObjectId CardId { get; }
            public ControllerInput? Input { get; }
            public string DisplayName { get; }
            public Texture Artwork { get; }
            public bool Eligible { get; }
        }

        private enum PrototypeContextMenuMode
        {
            None,
            Deck,
            DrawCards,
            CustomDrawCards,
            PopulateDeck,
            TabletopCard,
            FloorCard,
            PendingFloormasterCard,
            StackCard,
            ContainedCard,
            Stack,
            MergeDestination,
            Die,
            Pawn,
            Token,
            Console,
            DiscardPile,
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
                ContainerId handContainerId,
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
                HandContainerId = handContainerId;
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
            public ContainerId HandContainerId { get; }
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

        private void ReleaseRuntimeCardInstance(TabletopObjectId objectId)
        {
            for (int i = runtimeCardInstances.Count - 1; i >= 0; i--)
            {
                RuntimeCardInstance instance = runtimeCardInstances[i];
                CardView view = instance.View;
                if (view == null || view.ObjectId != objectId)
                {
                    continue;
                }

                GameObject root = instance.Root;
                TabletopSelectionVisual selectionVisual = instance.SelectionVisual;
                PrototypeCardVisualReferences visualReferences = instance.VisualReferences;
                DisableRuntimeInteraction(root);
                selectionVisual?.Clear();
                if (view.IsBound)
                {
                    view.Unbind();
                }

                cardViews.Remove(view);
                cardSelectionVisuals.Remove(selectionVisual);
                cardVisualReferences.Remove(visualReferences);
                labelsByCardId.Remove(objectId);
                presentationTransitions?.Forget(view.transform);
                runtimeCardInstances.RemoveAt(i);
                instance.ClearReferences();
                DestroyRuntimeOwnedGameObject(root);
                return;
            }

            throw new InvalidOperationException("Deleted runtime Card has no Presentation instance.");
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

        private sealed class RuntimeDiscardPileInstance
        {
            public RuntimeDiscardPileInstance(
                GameObject root,
                PrototypeFixedContainerVisual visual,
                DiscardPileView view,
                ContainerId containerId)
            {
                Root = root;
                Visual = visual;
                View = view;
                ContainerId = containerId;
            }

            public GameObject Root { get; private set; }
            public PrototypeFixedContainerVisual Visual { get; private set; }
            public DiscardPileView View { get; private set; }
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
                PrototypeConsoleSlotVisual[] slotVisuals,
                ConsoleId consoleId = default)
            {
                Root = root;
                View = view;
                LayoutSeatIndex = layoutSeatIndex;
                SlotViews = slotViews;
                SlotVisuals = slotVisuals;
                ConsoleId = consoleId;
            }

            public GameObject Root { get; private set; }
            public ConsoleView View { get; private set; }
            public int LayoutSeatIndex { get; }
            public ConsoleId ConsoleId { get; }
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

        private sealed class RuntimeTokenContainerInstance
        {
            public RuntimeTokenContainerInstance(
                GameObject root,
                TokenContainerView view,
                TabletopTokenContainerDropTarget dropTarget,
                Collider targetCollider,
                ContainerId containerId,
                TabletopPose pose,
                string displayLabel,
                int columnCount,
                double columnSpacing,
                double rowSpacing)
            {
                Root = root;
                View = view;
                DropTarget = dropTarget;
                TargetCollider = targetCollider;
                ContainerId = containerId;
                Pose = pose;
                DisplayLabel = displayLabel;
                ColumnCount = columnCount;
                ColumnSpacing = columnSpacing;
                RowSpacing = rowSpacing;
            }

            public GameObject Root { get; private set; }
            public TokenContainerView View { get; private set; }
            public TabletopTokenContainerDropTarget DropTarget { get; private set; }
            public Collider TargetCollider { get; private set; }
            public ContainerId ContainerId { get; }
            public TabletopPose Pose { get; }
            public string DisplayLabel { get; }
            public int ColumnCount { get; }
            public double ColumnSpacing { get; }
            public double RowSpacing { get; }

            public void ClearReferences()
            {
                Root = null;
                View = null;
                DropTarget = null;
                TargetCollider = null;
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
            private readonly HandTrayRig handTrayRig;

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

            public ContainerFeedbackTarget(HandTrayRig trayRig)
            {
                handTrayRig = trayRig ?? throw new ArgumentNullException(nameof(trayRig));
                Clear();
            }

            public void SetValid()
            {
                if (handTrayRig != null)
                {
                    handTrayRig.ShowFeedback(HandTrayFeedback.Valid);
                    return;
                }

                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ShowValidTarget();
                    return;
                }

                authoredFixedContainerVisual.ShowValidTarget();
            }

            public void SetSource()
            {
                if (handTrayRig != null)
                {
                    handTrayRig.ShowFeedback(HandTrayFeedback.Source);
                    return;
                }

                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ShowSourceTarget();
                    return;
                }

                authoredFixedContainerVisual.ShowSourceTarget();
            }

            public void SetInvalid()
            {
                if (handTrayRig != null)
                {
                    handTrayRig.ShowFeedback(HandTrayFeedback.Invalid);
                    return;
                }

                if (authoredSlotVisual != null)
                {
                    authoredSlotVisual.ShowInvalidTarget();
                    return;
                }

                authoredFixedContainerVisual.ShowInvalidTarget();
            }

            public void Clear()
            {
                if (handTrayRig != null)
                {
                    handTrayRig.ShowFeedback(HandTrayFeedback.None);
                    return;
                }

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
