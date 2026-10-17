using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
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
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>Tabletop visuals hidden at bind time without deleting their code paths.</summary>
    [Flags]
    public enum TableVisualHide
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

    /// <summary>
    /// The table session (composition root): its fields, start-up, shutdown and the Unity messages. The rest of
    /// the class lives in partial files by job (R1a, doc 30 A5): Piles, Undo, TrapFloor, TrapFloorStatus,
    /// Feedback, Toolbox, Session, RulesCard, Seats, ContextMenus, CardInspect, Shop, Views, Release, Validation,
    /// Build, Camera, Boards, Consoles, Interaction and CardArt, plus Table, TableMode, CardEditor, DeckEditor,
    /// GameSelect, PlayerGames, Profiles and TrapFloorHints.
    /// </summary>
    public sealed partial class TabletopComposition : MonoBehaviour, IContainedCardDragFeedback, ICardDropMagnet
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
        private static readonly Rect DeveloperControlsPanelRect = new Rect(16f, 330f, 280f, 320f);

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
        private GameBoardView adoptedGameBoard;
        // C3e (doc 22 §15): the Board Mat prefab, the placed boards' views, Trap Floor's board drawn as a mat and its
        // grid setting, and cards already set on a space.
        private const float CardSpaceWidth = 1.1f;
        private const float CardSpaceDepth = 1.5f;
        private const float CardSpaceGap = 0.1f;
        private const float SmallSpaceWidth = 0.72f;
        private const float SmallSpaceDepth = 1f;
        private const float BoardSnapTolerance = 0.04f;
        private readonly GameBoardUseCases boardUseCases = new GameBoardUseCases();
        private readonly Dictionary<BoardId, GameBoardMatView> boardViews = new Dictionary<BoardId, GameBoardMatView>();
        private readonly Dictionary<TabletopObjectId, Vector3> cardsSetOnBoardSpaces = new Dictionary<TabletopObjectId, Vector3>();
        private GameBoardMatView boardMatPrefab;
        private GameBoardMatView templateBoardMat;
        private BoardGrid? templateBoardGrid;
        private bool boardSnapCheckPending;
        private Vector2 boardMenuScreenPosition;
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
        [SerializeField] internal TableUiController runtimeUi;
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
        [Tooltip("Console mat (doc 22 §14): how far a card with a symbol is pulled into a slot with the same symbol.")]
        [SerializeField, Min(0f)] internal float consoleSymbolMagnetDistance = 1.1f;
        [Tooltip("Console mat: how far under the mat the cards of the side facing down rest, out of sight.")]
        [SerializeField, Min(0.01f)] internal float consoleHiddenCardDepth = 0.6f;
        [Tooltip("Console mat: how close to a D-pad arrow a die must come to rest to be set on it.")]
        [SerializeField, Min(0.05f)] internal float consoleDieSpotRadius = 0.45f;
        [Tooltip("Game board (doc 22 §15): how close a board must come to another's edge to join it.")]
        [SerializeField, Min(0f)] internal float boardJoinDistance = 0.8f;
        [SerializeField] internal float feedbackDuration = 0.18f;
        [SerializeField] internal float shuffleCompression = 0.06f;
        [SerializeField] internal float floorCardVisualScale = 0.62f;
        [Tooltip("Scale of Large (hero/avatar) cards relative to a Standard card (doc 24).")]
        [SerializeField] internal float largeCardVisualScale = 1.486f;
        [Tooltip("Player feedback (doc 25): announcement banners, the message stack and the turn ping.")]
        [SerializeField] internal bool playerFeedback = true;
        [Tooltip("Hot-seat (doc 18 §15.11): one screen shared by every player; it passes to whoever's turn it is after a handover screen. Also in the Table menu.")]
        [SerializeField] internal bool hotSeatMode;
        [Tooltip("Camera view after this screen changes seat (Seat row or hot-seat). Mid: that seat's console and the board, from behind the seat.")]
        [SerializeField] internal TabletopCameraPreset seatSwitchCameraView = TabletopCameraPreset.Mid;
        [Tooltip("Seconds the camera takes to travel to the new seat; the new hand slides in as it arrives.")]
        [SerializeField, Min(0.1f)] internal float seatSwitchCameraSeconds = 1.2f;
        [Tooltip("Hands on table (doc 25, F3): your hand lies face up at your table edge instead of the screen tray. Also in the Table menu.")]
        [SerializeField] internal bool handsOnTable;
        [Tooltip("Full feedback (doc 25, F3): every banner, sound and camera nudge. Off is Minimal: the turn banner, messages and board markers.")]
        [SerializeField] internal bool fullFeedback = true;
        [Tooltip("Pawn hop (doc 25, F4): seconds and height of the hop when End Turn moves a pawn for the player.")]
        [SerializeField, Min(0.05f)] internal float pawnHopSeconds = 0.5f;
        [SerializeField, Min(0f)] internal float pawnHopHeight = 0.35f;
        [Header("Art test (doc 24, A2; Play mode, right-click the component > Apply Art Test)")]
        [Tooltip("An art key, for example trap-floor/fronts/shield.png.")]
        [SerializeField] internal string artTestKey;
        [SerializeField] internal Texture2D artTestTexture;
        [SerializeField] internal bool showDeveloperControls;
        [Tooltip("Table visuals hidden at bind time. After changing in Play mode, press Reset or restart the session.")]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("prototypeVisualHide")]
        internal TableVisualHide tableVisualHide =
            TableVisualHide.CardPlaceholderLabels
            | TableVisualHide.ContainerLabels
            | TableVisualHide.SelectionHighlight
            | TableVisualHide.DieResultLabel
            | TableVisualHide.PawnOwnerLabel
            | TableVisualHide.CardFaceLabels;

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
        private readonly List<CardVisualReferences> cardVisualReferences =
            new List<CardVisualReferences>();
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
        // Card art is drawn through one shared material per (base material, picture), made once and reused.
        // Texture overrides in a MaterialPropertyBlock are not drawn on every render path, so art never goes there.
        private readonly Dictionary<(Material, Texture2D), Material> cardArtMaterials = new Dictionary<(Material, Texture2D), Material>();
        private readonly Dictionary<Material, Material> cardArtBaseMaterials = new Dictionary<Material, Material>();
        // Content (doc 24, A2–A4): every picture is looked up by art key through the content service
        // (your pictures, then the active pack, then built-in, then the asset's texture).
        private ContentArtService contentArt;
        // The player profile in use (doc 28): every save (pictures, packs, cards, decks, rules, hints) goes in its folder.
        private ProfileService profiles;
        // This table's cards (doc 27 L2-1): the game's cards with the player's changes and new cards. Rebuilt when
        // the Trap Floor templates are; its runtime cards are destroyed with it.
        private GameContent trapFloorContent;
        private ContentArtPanelActions cardArtActions;
        private bool artRefreshing;
        private MaterialPropertyBlock cardSlotProperties; // created on first use: Unity forbids it in field initializers
        private readonly Dictionary<ObjectDefinitionId, ButtonCardDefinition> buttonDefinitions =
            new Dictionary<ObjectDefinitionId, ButtonCardDefinition>();
        private readonly Dictionary<ContainerId, StackRuntimeView> stackViewsByContainerId =
            new Dictionary<ContainerId, StackRuntimeView>();
        private readonly Dictionary<ContainerId, ContainerFeedbackTarget> feedbackTargetsByContainerId =
            new Dictionary<ContainerId, ContainerFeedbackTarget>();
        private readonly Dictionary<ContainerId, ConsoleSlotVisual> consoleSlotVisualsByContainerId =
            new Dictionary<ContainerId, ConsoleSlotVisual>();
        private readonly List<GameObject> officialPawnLabels = new List<GameObject>();
        private readonly List<Renderer> officialPawnRenderers = new List<Renderer>();

        private bool cameraRoutingConfiguredByComposition;
        private bool frameCoordinatorEnabledByComposition;
        private bool controlsPanelInputBlockConfiguredByComposition;
        private bool uiInputConfiguredByComposition;
        private bool componentPlacementInputConfiguredByComposition;
        private bool objectAdapterInitializedByComposition;
        private bool gameTemplatesPanelVisible;
        private readonly ActiveSessionUndoHistory<SessionUndoSnapshot> undoHistory =
            new ActiveSessionUndoHistory<SessionUndoSnapshot>();
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
        // Hints (doc 23 §10): the service, the Tips switch (Learning) and the hint in the strip. Viewer settings.
        private HintService hintService;
        private bool tipsEnabled = true;
        private HintEntry shownHint;
        private bool hintLibraryWarned;
        private bool hintCardDragSeen;
        private bool hintPawnDragSeen;
        private static string[] actionHintTriggers;
        // House rules (doc 23, R3): the rule set in play (null = the game's default), the sets saved on this
        // computer, the set the current templates were built with, and the open proposal. Kept across Undo and Reset.
        private RuleSetDefinition activeRuleSet;
        private RuleSetDefinition templatesRuleSet;
        private readonly List<RuleSetDefinition> houseRuleSets = new List<RuleSetDefinition>();
        private readonly List<HouseRuleSetRecord> houseRuleRecords = new List<HouseRuleSetRecord>();
        private HouseRulesStore houseRulesStore;
        private bool houseRulesLoaded;
        private RulesCardActions rulesCardActions;
        private RuleProposalActions ruleProposalActions;
        private bool ruleProposalOpen;
        private bool ruleProposalAgreeing;
        private string ruleProposalEntryId;
        private int ruleProposalSetIndex = -1;
        private string ruleProposalValue;
        private string ruleProposalText;
        private readonly List<PlayerId> ruleProposalVoters = new List<PlayerId>();
        private readonly List<bool> ruleProposalAgreed = new List<bool>();
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
        private TemplateContext templateContext;
        private TrapFloorTemplateDefinition trapFloorTemplate;
        private TrapFloorActivityFeedState trapFloorActivityFeed;
        private TrapFloorRevealFloorUseCase trapFloorRevealFloorUseCase;
        private TrapFloorObjectiveState trapFloorObjectiveState;
        private TrapFloorObjectiveUseCase trapFloorObjectiveUseCase;
        private TrapFloorCollapseState trapFloorCollapseState;
        private TrapFloorCollapseUseCase trapFloorCollapseUseCase;
        private TrapFloorTurnState trapFloorTurnState;
        private TrapFloorTurnService trapFloorTurnService;
        private TrapFloorTakePrizeUseCase trapFloorTakePrizeUseCase;
        // The Shop (doc 18 §15.4, owner 2026-10-15): every Ability for sale on your turn, open while the rule
        // "shop.enabled" is on. The source says whether the Hand picker pays for a Shop card or a floor prize.
        private TrapFloorBuyFromShopUseCase trapFloorShopUseCase;
        private PendingPurchaseSource pendingPurchaseSource;
        // G2a landing flips: the tile each pawn stood on when last checked, and whether an accepted move is
        // waiting to be checked. Checked once per accepted move, never per frame.
        private readonly Dictionary<TabletopObjectId, TrapFloorCoordinate?> landingPawnTiles =
            new Dictionary<TabletopObjectId, TrapFloorCoordinate?>();
        private bool landingCheckPending;
        // Owner, 2026-10-15: a tile the active pawn lands on turns over only once no pawn stands on it (the pawn
        // moves aside or ahead), or when the turn ends. Its card still counts for the player who landed.
        private readonly HashSet<TabletopObjectId> landedFloorCards = new HashSet<TabletopObjectId>();
        private TabletopObjectId waitingLandingFloorCardId;
        private PlayerId waitingLandingActor;
        private TrapFloorAbilityResolutionState trapFloorAbilityResolutionState;
        private TrapFloorAbilityResolutionService trapFloorAbilityResolutionService;
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
        private int boardCardBacksRound;
        // Player feedback (doc 25, F1): what has already been announced.
        private TrapFloorActivityFeedState announcedFeed;
        private TrapFloorTurnState announcedTurnState;
        private long announcedSequence;
        private int announcedRound;
        private PlayerId announcedActivePlayer;
        private TrapFloorTurnPhase announcedPhase;
        // Board feedback markers (doc 25, F2), made once from Resources/BoardFeedbackSet.
        private BoardFeedbackPresenter boardFeedback;
        private bool boardFeedbackUnavailable;
        private readonly List<Vector3> boardFeedbackTraps = new List<Vector3>(8);
        private readonly List<Vector3> boardFeedbackTargets = new List<Vector3>(16);
        // Seats (H1b): the screen hand belongs to this screen's seat. Hot-seat mode (H2) passes the screen to the
        // player whose turn it is, behind a handover cover, and summarises what happened since they last had it.
        // The shared log also feeds the "What happened" panel (F4): its latest ActivityLogLines lines.
        private const int HandoverLogSize = 40;
        private const int ActivityLogLines = 20;
        private const int HandoverSummaryLines = 6;
        private bool announceTurnAfterRebuild;
        private PlayerId handSeatSyncFailedFor;
        private bool hotSeatHandoverOpen;
        private PlayerId hotSeatHandoverFor;
        private readonly string[] handoverLog = new string[HandoverLogSize];
        private readonly int[] handoverLogRound = new int[HandoverLogSize];
        private readonly StringBuilder activityLogText = new StringBuilder(1024);
        private long shownActivityLogCount = -1;
        private bool activityLogUnavailable;
        // F4: pawns moved by End Turn hop along their path (a visual offset only; the pawn is already there).
        private struct PawnHop
        {
            public Transform Visual;
            public Vector3 RestLocal;
            public Vector3 StartWorld;
            public float Started;
        }

        private readonly List<PawnHop> pawnHops = new List<PawnHop>(4);
        private long handoverLogCount;
        private readonly Dictionary<PlayerId, long> seatLeftAtLog = new Dictionary<PlayerId, long>();
        private readonly Dictionary<PlayerId, int> seatLeftHandCount = new Dictionary<PlayerId, int>();
        private readonly StringBuilder handoverSummary = new StringBuilder(256);
        // A seat switch first slides the screen hand off the screen, then rebuilds for the new seat (H1b).
        private bool seatSwitchPending;
        private PlayerId seatSwitchTarget;
        private bool seatSwitchFromHandover;
        private float seatSwitchDeadline;
        private float seatRevealAt = -1f;
        // Spring/Crane target the active player still has to move to (G2c); applied by End Turn if left.
        private PlayerId pendingLaunchPlayerId;
        private TabletopObjectId pendingLaunchTargetId;
        // Groups every accepted action between Begin/EndUndoBatch into one Undo step (End Turn + Trap effects).
        private bool undoBatchActive;
        private SessionUndoSnapshot undoBatchBefore;
        private AuthoritativeActionAcceptance? undoBatchLastAcceptance;
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
        private ContextMenuMode contextMenuMode;
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
        private FixedContainerVisual catalogDeckPrefab;
        private FixedContainerVisual catalogStackPrefab;
        private FixedContainerVisual catalogDiscardPilePrefab;
        // Object and Console prefabs resolved from the box catalogs (doc 22, C3a); no prefab field in the scene.
        private CardVisualReferences catalogCardPrefab;
        private PawnView catalogPawnPrefab;
        private TokenView catalogTokenPrefab;
        private DieView catalogDiePrefab;
        private ConsoleView catalogConsolePrefab;
        private FixedContainerVisual catalogHandPrefab;
        // The local Hand, built from the catalog Hand entry for each session (C3b-1).
        private FixedContainerVisual localHandVisual;
        private ConsoleView consoleView;
        private ConsoleLayoutData consoleLayout;
        // C3d (doc 22 §14): slots on a Console side facing down, the turn-over of flipped cards, dice on D-pad
        // arrows, and each card definition's Console symbol.
        private readonly HashSet<ContainerId> hiddenConsoleSlotIds = new HashSet<ContainerId>();
        private readonly Dictionary<TabletopObjectId, string> diceOnConsoleSpots = new Dictionary<TabletopObjectId, string>();
        private readonly Dictionary<ObjectDefinitionId, ConsoleSymbol> consoleSymbolsByDefinition =
            new Dictionary<ObjectDefinitionId, ConsoleSymbol>();
        private CardFlipTurnPresenter cardFlipTurns;
        private bool consoleDieCheckPending;
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

        public Rect ControlsPanelScreenRect => DeveloperControlsPanelRect;

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
                throw new InvalidOperationException("TabletopComposition is already initialized.");
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
            if (templateContext == null)
            {
                throw new InvalidOperationException("The selected Game Template has no Trap Floor prototype wiring.");
            }

            try
            {
                // Restore authoritative state and its authored Template/Mode metadata before any
                // Presentation validation asks the active definition for Console or Board data.
                RestoreTemplateContext(restoreInitialBaseline);
                ValidateTrapFloorConfiguration();
                presentationTransitions = new TabletopPresentationTransitionController();
                BuildRuntimeGraph();
                BuildToolboxRuntime();
                BuildFloorfallRuntime();
                BindObjectViews();
                HoldWaitingLandingTileView();
                BuildContainerViews();
                BindContainerViews();
                ProjectUnprojectedPlacedComponents();
                ConfigureTabletopCameraFraming();
                if (!rebuildingFromUndo) ProjectTrapFloorCameraBookmark();
                RefreshCardContentVisibility();
                ConfigureDropTargets();
                BuildInteractionGraph();
                inputFrameCoordinator.ConfigureUiInput(
                    HandleSecondaryPointerPressed,
                    CanBeginCameraOrbit);
                uiInputConfiguredByComposition = true;
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
                inputFrameCoordinator.ConfigureUiInput(
                    HandleSecondaryPointerPressed,
                    CanBeginCameraOrbit);
                uiInputConfiguredByComposition = true;
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
            FinishPawnHops();
            if (!rebuildingFromUndo)
            {
                // A new or reset table: forget who had the screen when (Undo and seat switches keep it).
                seatLeftAtLog.Clear();
                seatLeftHandCount.Clear();
                handSeatSyncFailedFor = PlayerId.Empty;
                seatSwitchPending = false;
                seatRevealAt = -1f;
                handTrayRig?.SetCleared(false, true);
                handoverLogCount = 0; // a new or reset table starts a new log
                shownActivityLogCount = -1;
                landedFloorCards.Clear();
                templateBoardGrid = null; // a new table draws the game's board with its own grid
                waitingLandingFloorCardId = TabletopObjectId.Empty;
                waitingLandingActor = PlayerId.Empty;
            }

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
            boardFeedback?.HideAll();
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

            if (uiInputConfiguredByComposition && inputFrameCoordinator != null)
            {
                inputFrameCoordinator.ClearUiInput();
            }

            uiInputConfiguredByComposition = false;

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
            ReleaseBoardViews();
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
            contextMenuMode = ContextMenuMode.None;
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
                templateContext = null;
            }

            IsInitialized = false;
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
                AdoptCatalogGameBoard(); // C3c: the board comes from the catalog when it has one
                EnsureContentArt(); // your pictures and the active pack load before the first table
                PrepareTemplateCatalog();
                ApplySavedTable(); // T1: the table shape saved in the profile, before the first table
                if (!TryReplaceTable(TabletopSessionSelection.EmptyCustom))
                {
                    throw new InvalidOperationException(
                        gameTemplatesPanelError ?? "Empty Table startup failed.");
                }

                OfferProfileChoiceAtStart(); // doc 28, P2: with several profiles, ask who is playing
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"TabletopComposition failed to start an Empty Table: {exception.Message}",
                    this);
                gameTemplatesPanelError = exception.Message;
            }
        }

        private void OnDestroy()
        {
            Shutdown();
            boardFeedback?.Destroy();
            boardFeedback = null;
            ReleaseCardArtMaterials();
            contentArt?.Dispose(); // destroys the pictures the content service loaded
            DisposeCardEditor(); // and those the card editor loaded for another game
            trapFloorContent?.Destroy(); // destroys the cards made from the player's content
            trapFloorContent = null;
            for (int i = 0; i < houseRuleSets.Count; i++)
            {
                if (houseRuleSets[i] != null) Destroy(houseRuleSets[i]);
            }

            houseRuleSets.Clear();
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
            if (IsInitialized && !rebuildingFromUndo)
            {
                if (seatSwitchPending
                    && (handTrayRig == null
                        || !handTrayRig.isActiveAndEnabled
                        || handTrayRig.IsCleared
                        || Time.unscaledTime >= seatSwitchDeadline))
                {
                    CompleteSeatSwitch();
                }

                if (handoverLogCount != shownActivityLogCount)
                {
                    RefreshActivityLog();
                }

                if (seatRevealAt >= 0f && Time.unscaledTime >= seatRevealAt)
                {
                    seatRevealAt = -1f;
                    handTrayRig?.SetCleared(false); // the new hand slides in as the camera arrives
                }

                UpdateHotSeat();
            }

            if (HandleRedoShortcut()) return;
            if (HandleUndoShortcut()) return;
            HandleHandTrayShortcut();
            HandleConsoleFlipShortcut();
            physicalAuthority?.Tick();
            CompletePendingLandingFlip();
            hintService?.TickHelpers(); // H3: the game's hint helpers (reach markers, collapse target)
            UpdateHintTriggers();
            RefreshBoardCardBacksIfRoundChanged();
            CompletePhysicalFloorfallIfSettled();
            CompletePhysicalFloorCollapseIfSettled();
            CompletePhysicalBlindDirectionIfSettled();
            CompleteConsoleDieSnaps();
            CompleteBoardCardSnaps();
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
            AnnounceTrapFloorFeedback();
            RefreshBoardFeedback();
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
            FixedContainerVisual visual,
            GameTemplatePileStyle pileStyle,
            SeatId ownerSeatId)
        {
            visual.TargetCollider.isTrigger = true;
            ConfigureBayFootprint(visual, pileStyle.BayMark);
            pileBays.Add(visual.Bay);
            pileBayOwnerSeats.Add(ownerSeatId);
        }

        // The bay's outer size is the pile footprint the placement uses (ConsoleAdjacentPlacementSettings).
        private static void ConfigureBayFootprint(FixedContainerVisual visual, GameTemplateBayMark mark)
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

        private List<PopupActionOption> AddHandTrayToggleAction(
            List<PopupActionOption> actions)
        {
            if (handTrayRig != null && handView != null && handView.IsTrayMode)
            {
                if (toggleHandTrayAction == null)
                {
                    toggleHandTrayAction = ToggleHandTray;
                }

                actions.Add(new PopupActionOption(
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

        // One table for every player colour (UX-1): pawns, the turn strip chip and turn text all read it.
        private static readonly Color[] PlayerColors =
        {
            new Color(0.20f, 0.55f, 0.96f),
            new Color(0.94f, 0.28f, 0.24f),
            new Color(0.28f, 0.78f, 0.38f),
            new Color(0.92f, 0.72f, 0.18f),
        };

        private static readonly string[] PlayerColorNames = { "BLUE", "RED", "GREEN", "YELLOW" };

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

        private enum ContextMenuMode
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

        private sealed class TemplateContext
        {
            private readonly Dictionary<TabletopObjectId, string> labelsByCardId;
            private readonly Dictionary<ObjectDefinitionId, ButtonCardDefinition> buttonDefinitions;

            public TemplateContext(
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

            public CardVisualReferences VisualReferences { get; private set; }

            public void SetReferences(
                CardView view,
                TabletopSelectionVisual selectionVisual,
                CardVisualReferences visualReferences)
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
                FixedContainerVisual visual,
                DeckView view,
                ContainerId containerId)
            {
                Root = root;
                Visual = visual;
                View = view;
                ContainerId = containerId;
            }

            public GameObject Root { get; private set; }
            public FixedContainerVisual Visual { get; private set; }
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
                FixedContainerVisual visual,
                DiscardPileView view,
                ContainerId containerId)
            {
                Root = root;
                Visual = visual;
                View = view;
                ContainerId = containerId;
            }

            public GameObject Root { get; private set; }
            public FixedContainerVisual Visual { get; private set; }
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
                ConsoleSlotVisual[] slotVisuals,
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
            public ConsoleSlotVisual[] SlotVisuals { get; private set; }

            public void ClearReferences()
            {
                Root = null;
                View = null;
                SlotViews = Array.Empty<ConsoleSlotView>();
                SlotVisuals = Array.Empty<ConsoleSlotVisual>();
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
                FixedContainerVisual visual,
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

            public FixedContainerVisual Visual { get; private set; }

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
            private readonly ConsoleSlotVisual authoredSlotVisual;
            private readonly FixedContainerVisual authoredFixedContainerVisual;
            private readonly HandTrayRig handTrayRig;

            public ContainerFeedbackTarget(ConsoleSlotVisual slotVisual)
            {
                authoredSlotVisual = slotVisual ?? throw new ArgumentNullException(nameof(slotVisual));
                Clear();
            }

            public ContainerFeedbackTarget(FixedContainerVisual fixedContainerVisual)
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
