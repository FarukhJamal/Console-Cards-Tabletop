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
    /// <summary>Building the runtime graph and the template board surface; restoring the template context. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        private void BuildRuntimeGraph()
        {
            interactionOwnerId = InteractionOwnerId.New();
            coordinateConverter = PhysicalTabletopSurfaces.CreateTemplateLayoutConverter(
                targetCamera,
                worldUnitsPerTableUnit,
                tabletopLayerHeight,
                tabletopLocalOrderHeight);

            if (templateContext == null)
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
            // Rush/Dodge/Spring help reads where the Pawn really is (physical state first), not its placed pose.
            trapFloorAbilityResolutionService.PawnPositionResolver = PawnTablePosition;
            trapFloorTakePrizeUseCase = new TrapFloorTakePrizeUseCase(
                trapFloorTemplate,
                trapFloorCollapseState,
                trapFloorTurnState);
            trapFloorShopUseCase = new TrapFloorBuyFromShopUseCase(trapFloorTemplate, trapFloorTurnState);
            ConsoleCardInteractionAccepted -= HandleTrapFloorConsoleCardInteractionAccepted;
            ConsoleCardInteractionAccepted += HandleTrapFloorConsoleCardInteractionAccepted;
            activeFloorRevealActivity = null;
            SyncLandingPawnTiles();
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
            reachHintShown = false;
            boardFeedback?.HideReach();
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

        private static TemplateContext CreateTrapFloorTemplateContext(
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

            return new TemplateContext(
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
            ApplyTemplateBoardMat(boardCollider); // C3e: drawn as a board mat when the Board Mat exists
        }

        // C3c: the Base Box's Game Board entry replaces the scene board (which is switched off). Without the
        // entry the scene board stays in use.
        private void AdoptCatalogGameBoard()
        {
            if (adoptedGameBoard != null || componentLibrary == null || componentLibrary.BaseBox == null) return;
            ComponentCatalogEntry entry = null;
            IReadOnlyList<ComponentCatalogEntry> baseEntries = componentLibrary.BaseBox.Entries;
            for (int i = 0; i < baseEntries.Count && entry == null; i++)
            {
                // The C3c board model entry (GameBoardView); the C3e Board Mat entry is used separately.
                if (baseEntries[i] != null
                    && baseEntries[i].Kind == ComponentCatalogKind.GameBoard
                    && baseEntries[i].Prefab != null
                    && baseEntries[i].Prefab.GetComponent<GameBoardView>() != null)
                {
                    entry = baseEntries[i];
                }
            }

            if (entry == null)
            {
                return;
            }

            GameBoardView prefab = entry.Prefab.GetComponent<GameBoardView>();
            if (prefab == null || !prefab.IsComplete)
            {
                Debug.LogWarning("[Board] The Game Board entry's prefab has no complete GameBoardView; the scene board stays.", this);
                return;
            }

            Transform parent = gameBoardPhysicalSurface != null ? gameBoardPhysicalSurface.transform.parent : null;
            GameBoardView instance = Instantiate(prefab, parent, false);
            instance.name = entry.Prefab.name;
            instance.gameObject.SetActive(false);
            if (gameBoardPhysicalSurface != null) gameBoardPhysicalSurface.gameObject.SetActive(false);
            gameBoardPhysicalSurface = instance.Surface;
            gameBoardVisualRoot = instance.VisualRoot;
            if (instance.VisualMaterial != null) gameBoardVisualMaterial = instance.VisualMaterial;
            adoptedGameBoard = instance;
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

            if (active && gameBoardVisualRoot != null && templateBoardMat == null)
            {
                gameBoardVisualRoot.gameObject.SetActive(true);
            }

            if (templateBoardMat != null)
            {
                templateBoardMat.gameObject.SetActive(active); // C3e: the board mat stands in for the board model
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

        private void RestoreTemplateContext(bool restoreInitialBaseline)
        {
            TemplateContext context = templateContext;
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
            SessionUndoSnapshot previousSnapshot,
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
                ConsoleSlotVisual slotVisual = slotViews[i].GetComponent<ConsoleSlotVisual>();
                RequireReference(slotVisual, $"ConsoleSlotVisual on prefab Slot {i}");
                slotVisual.ValidateReferences();
            }
        }

        private static void ValidateObjectPrefab(TabletopObjectView objectView, string fieldName)
        {
            if (objectView.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    $"TabletopComposition requires {fieldName} to reference a prefab asset.");
            }

            TabletopSelectionVisual selectionVisual = objectView.GetComponent<TabletopSelectionVisual>();
            if (selectionVisual == null
                || !selectionVisual.IsConfigured
                || !ReferenceEquals(selectionVisual.ObjectView, objectView))
            {
                throw new InvalidOperationException(
                    $"TabletopComposition requires {fieldName} to own an explicit selection visual.");
            }
        }

        // The authored label, or for a card made during play (a taken prize, a purchase) its definition's name.
        // Rebuilds (Undo, Redo, seat switch) reload only the template's labels, so the second case matters.
        private string ResolveCardLabel(CardInstanceState card)
        {
            TabletopObjectId id = card.BaseState.Id;
            if (labelsByCardId.TryGetValue(id, out string label)) return label;
            label = TryGetCardLook(card.BaseState.DefinitionId, out CardDefinition definition)
                ? definition.DisplayName
                : TryGetAvatarLook(card.BaseState.DefinitionId, out AvatarDefinition avatar)
                    ? avatar.DisplayName
                    : "CARD";
            labelsByCardId[id] = label;
            return label;
        }
    }
}
