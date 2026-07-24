using System;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Prototype
{
    public sealed class TabletopPrototypeComposition : MonoBehaviour
    {
        [SerializeField] internal UnityCamera targetCamera;
        [SerializeField] internal TabletopCameraInputAdapter cameraInputAdapter;
        [SerializeField] internal TabletopObjectInputAdapter objectInputAdapter;
        [SerializeField] internal TabletopInputFrameCoordinator inputFrameCoordinator;
        [SerializeField] internal CardView cardView;
        [SerializeField] internal PawnView pawnView;
        [SerializeField] internal TokenView tokenView;
        [SerializeField] internal TabletopSelectionVisual cardSelectionVisual;
        [SerializeField] internal GameObject cardHighlightRoot;
        [SerializeField] internal TabletopSelectionVisual pawnSelectionVisual;
        [SerializeField] internal GameObject pawnHighlightRoot;
        [SerializeField] internal TabletopSelectionVisual tokenSelectionVisual;
        [SerializeField] internal GameObject tokenHighlightRoot;

        [SerializeField] internal LayerMask interactionLayerMask;
        [SerializeField] internal float maximumHitDistance = 100f;
        [SerializeField] internal float dragThresholdPixels = 8f;
        [SerializeField] internal float worldUnitsPerTableUnit = 1f;
        [SerializeField] internal float tabletopHeight = 0f;

        private bool cameraRoutingConfiguredByComposition;
        private bool frameCoordinatorEnabledByComposition;
        private bool objectAdapterInitializedByComposition;
        private bool cardViewBoundByComposition;
        private bool pawnViewBoundByComposition;
        private bool tokenViewBoundByComposition;
        private bool cardSelectionVisualConfiguredByComposition;
        private bool pawnSelectionVisualConfiguredByComposition;
        private bool tokenSelectionVisualConfiguredByComposition;

        private MatchState matchState;
        private PlayerId localPlayerId;
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

        public bool IsInitialized { get; private set; }

        public MatchState MatchState => matchState;

        public PlayerId LocalPlayerId => localPlayerId;

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

        public void Initialize()
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition is already initialized.");
            }

            try
            {
                ValidateConfiguration();

                PlayerId createdPlayerId = PlayerId.New();
                InteractionOwnerId createdOwnerId = InteractionOwnerId.New();
                TabletopCoordinateConverter createdConverter = new TabletopCoordinateConverter(
                    worldUnitsPerTableUnit,
                    tabletopHeight,
                    0f,
                    0f);

                CardInstanceState createdCardState = CreateCardState();
                PawnState createdPawnState = CreatePawnState();
                TokenState createdTokenState = CreateTokenState();
                MatchState createdMatchState = new MatchState(
                    MatchId.New(),
                    GameTemplateId.Empty,
                    0,
                    new[] { createdCardState },
                    new[] { createdPawnState },
                    new[] { createdTokenState },
                    Array.Empty<ContainerState>(),
                    Array.Empty<SeatState>());

                TabletopSelectionState createdSelectionState = new TabletopSelectionState();
                TabletopObjectHitResolver createdHitResolver = new TabletopObjectHitResolver(
                    targetCamera,
                    interactionLayerMask,
                    maximumHitDistance);
                TabletopPointerProjector createdPointerProjector = new TabletopPointerProjector(
                    targetCamera,
                    createdConverter,
                    tabletopHeight);
                LocalInteractionLockService createdLockService = new LocalInteractionLockService();
                TabletopInteractionStateMachine createdStateMachine =
                    new TabletopInteractionStateMachine(dragThresholdPixels);
                TabletopDragPreviewSession createdPreviewSession = new TabletopDragPreviewSession();
                MoveObjectUseCase moveUseCase = new MoveObjectUseCase();
                RotateObjectUseCase rotateUseCase = new RotateObjectUseCase();
                FlipCardUseCase flipUseCase = new FlipCardUseCase();
                TabletopMoveInteractionCoordinator createdMoveCoordinator =
                    new TabletopMoveInteractionCoordinator(
                        createdMatchState,
                        createdPlayerId,
                        createdOwnerId,
                        createdSelectionState,
                        createdHitResolver,
                        createdPointerProjector,
                        createdLockService,
                        createdStateMachine,
                        createdPreviewSession,
                        moveUseCase);
                TabletopRotationCoordinator createdRotationCoordinator =
                    new TabletopRotationCoordinator(
                        createdMatchState,
                        createdPlayerId,
                        createdOwnerId,
                        createdSelectionState,
                        createdLockService,
                        rotateUseCase);
                TabletopCardFlipCoordinator createdFlipCoordinator =
                    new TabletopCardFlipCoordinator(
                        createdMatchState,
                        createdPlayerId,
                        createdOwnerId,
                        createdSelectionState,
                        createdLockService,
                        flipUseCase);
                TabletopInteractionInputRoutingPolicy createdRoutingPolicy =
                    new TabletopInteractionInputRoutingPolicy(
                        createdSelectionState,
                        createdMoveCoordinator);
                TabletopSelectionPresenter createdSelectionPresenter = null;

                localPlayerId = createdPlayerId;
                interactionOwnerId = createdOwnerId;
                coordinateConverter = createdConverter;
                cardState = createdCardState;
                pawnState = createdPawnState;
                tokenState = createdTokenState;
                matchState = createdMatchState;
                selectionState = createdSelectionState;
                hitResolver = createdHitResolver;
                pointerProjector = createdPointerProjector;
                lockService = createdLockService;
                interactionStateMachine = createdStateMachine;
                previewSession = createdPreviewSession;
                moveCoordinator = createdMoveCoordinator;
                rotationCoordinator = createdRotationCoordinator;
                flipCoordinator = createdFlipCoordinator;
                inputRoutingPolicy = createdRoutingPolicy;

                cardView.Bind(cardState, coordinateConverter);
                cardViewBoundByComposition = true;
                pawnView.Bind(pawnState, coordinateConverter);
                pawnViewBoundByComposition = true;
                tokenView.Bind(tokenState, coordinateConverter);
                tokenViewBoundByComposition = true;

                cardSelectionVisual.Configure(cardView, cardHighlightRoot);
                cardSelectionVisualConfiguredByComposition = true;
                pawnSelectionVisual.Configure(pawnView, pawnHighlightRoot);
                pawnSelectionVisualConfiguredByComposition = true;
                tokenSelectionVisual.Configure(tokenView, tokenHighlightRoot);
                tokenSelectionVisualConfiguredByComposition = true;

                createdSelectionPresenter = new TabletopSelectionPresenter(
                    selectionState,
                    cardSelectionVisual,
                    pawnSelectionVisual,
                    tokenSelectionVisual);
                selectionPresenter = createdSelectionPresenter;

                cameraInputAdapter.ConfigureScrollRoutingPolicy(inputRoutingPolicy);
                cameraRoutingConfiguredByComposition = true;

                objectInputAdapter.Initialize(
                    moveCoordinator,
                    rotationCoordinator,
                    flipCoordinator,
                    inputRoutingPolicy);
                objectAdapterInitializedByComposition = true;

                inputFrameCoordinator.ConfigureSelectionPresenter(selectionPresenter);
                inputFrameCoordinator.enabled = true;
                frameCoordinatorEnabledByComposition = true;

                if (!cameraInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator)
                    || !objectInputAdapter.IsExternallyDrivenBy(inputFrameCoordinator))
                {
                    throw new InvalidOperationException("TabletopInputFrameCoordinator failed to attach both input adapters.");
                }

                selectionPresenter.Refresh();
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

            if (moveCoordinator != null)
            {
                moveCoordinator.Reset();
            }

            if (previewSession != null)
            {
                previewSession.Reset();
            }

            if (lockService != null)
            {
                lockService.Clear();
            }

            selectionPresenter?.Clear();
            selectionPresenter = null;
            ClearSelectionVisualIfOwned(cardSelectionVisual, ref cardSelectionVisualConfiguredByComposition);
            ClearSelectionVisualIfOwned(pawnSelectionVisual, ref pawnSelectionVisualConfiguredByComposition);
            ClearSelectionVisualIfOwned(tokenSelectionVisual, ref tokenSelectionVisualConfiguredByComposition);
            DeactivateHighlightRoot(cardHighlightRoot);
            DeactivateHighlightRoot(pawnHighlightRoot);
            DeactivateHighlightRoot(tokenHighlightRoot);

            UnbindIfOwned(cardView, ref cardViewBoundByComposition);
            UnbindIfOwned(pawnView, ref pawnViewBoundByComposition);
            UnbindIfOwned(tokenView, ref tokenViewBoundByComposition);

            matchState = null;
            localPlayerId = PlayerId.Empty;
            interactionOwnerId = InteractionOwnerId.Empty;
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

        private void ValidateConfiguration()
        {
            RequireReference(targetCamera, nameof(targetCamera));
            RequireReference(cameraInputAdapter, nameof(cameraInputAdapter));
            RequireReference(objectInputAdapter, nameof(objectInputAdapter));
            RequireReference(inputFrameCoordinator, nameof(inputFrameCoordinator));
            RequireReference(cardView, nameof(cardView));
            RequireReference(pawnView, nameof(pawnView));
            RequireReference(tokenView, nameof(tokenView));
            RequireReference(cardSelectionVisual, nameof(cardSelectionVisual));
            RequireReference(cardHighlightRoot, nameof(cardHighlightRoot));
            RequireReference(pawnSelectionVisual, nameof(pawnSelectionVisual));
            RequireReference(pawnHighlightRoot, nameof(pawnHighlightRoot));
            RequireReference(tokenSelectionVisual, nameof(tokenSelectionVisual));
            RequireReference(tokenHighlightRoot, nameof(tokenHighlightRoot));

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
            ValidatePreInitializationState();
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

            if (cardSelectionVisual.IsConfigured
                || pawnSelectionVisual.IsConfigured
                || tokenSelectionVisual.IsConfigured)
            {
                throw new InvalidOperationException("TabletopPrototypeComposition requires selection visuals to begin unconfigured.");
            }
        }

        private static CardInstanceState CreateCardState()
        {
            return new CardInstanceState(
                CreateBaseState(
                    TabletopObjectKind.Card,
                    new TabletopPose(new TableCoordinate(-2d, 0d), 0f, 0, 0)),
                CardFace.FaceUp);
        }

        private static PawnState CreatePawnState()
        {
            return new PawnState(
                CreateBaseState(
                    TabletopObjectKind.Pawn,
                    new TabletopPose(new TableCoordinate(0d, 0d), 0f, 0, 0)));
        }

        private static TokenState CreateTokenState()
        {
            return new TokenState(
                CreateBaseState(
                    TabletopObjectKind.Token,
                    new TabletopPose(new TableCoordinate(2d, 0d), 0f, 0, 0)));
        }

        private static TabletopObjectState CreateBaseState(
            TabletopObjectKind kind,
            TabletopPose pose)
        {
            return new TabletopObjectState(
                TabletopObjectId.New(),
                ObjectDefinitionId.New(),
                kind,
                pose,
                ContainerId.Empty,
                PlayerId.Empty,
                ObjectVisibility.Public,
                false);
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

        private static void ClearSelectionVisualIfOwned(
            TabletopSelectionVisual visual,
            ref bool configuredByComposition)
        {
            if (!configuredByComposition)
            {
                return;
            }

            if (visual != null)
            {
                visual.Clear();
            }

            configuredByComposition = false;
        }

        private static void DeactivateHighlightRoot(GameObject highlightRoot)
        {
            if (highlightRoot != null)
            {
                highlightRoot.SetActive(false);
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
    }
}
