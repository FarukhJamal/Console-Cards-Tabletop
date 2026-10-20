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
    /// <summary>Console mats: sides, flipping, duplicating, symbol slots and dice on the mat. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
        // ---- C3d: the double-sided Console mat (doc 22 §14) ----
        // The side facing up is Runtime State (ConsoleState.ShowsBack). Slots on the side facing down keep their
        // cards under the mat, out of sight, and cannot be used; slots on the side facing up work as before.

        private static ConsoleState ResolveConsoleState(RuntimeConsoleInstance instance)
        {
            return instance != null && instance.View != null && instance.View.IsBound
                ? instance.View.ConsoleState
                : null;
        }

        internal bool ConsoleMatHasBackSide()
        {
            return catalogConsolePrefab != null
                && Registry.runtimeConsoleInstances.Count > 0
                && GetConsoleLayout().HasBackSide;
        }

        private void ApplyAllConsoleSides()
        {
            for (int i = 0; i < Registry.runtimeConsoleInstances.Count; i++)
            {
                ApplyConsoleSide(Registry.runtimeConsoleInstances[i]);
            }
        }

        // Shows the side facing up: its slots take cards, the other side's cards rest out of sight under the mat.
        private void ApplyConsoleSide(RuntimeConsoleInstance instance)
        {
            ConsoleState console = ResolveConsoleState(instance);
            if (console == null)
            {
                return;
            }

            ConsoleLayoutData layout = GetConsoleLayout();
            bool back = console.ShowsBack;
            for (int i = 0; i < instance.SlotViews.Length && i < layout.CardSlots.Count; i++)
            {
                ConsoleSlotView slotView = instance.SlotViews[i];
                if (slotView == null || !slotView.IsBound)
                {
                    continue;
                }

                bool hidden = (layout.CardSlots[i].Side == ConsoleMatSide.Back) != back;
                slotView.SetSideHidden(hidden ? consoleHiddenCardDepth : 0f);
                ConsoleSlotVisual slotVisual = i < instance.SlotVisuals.Length ? instance.SlotVisuals[i] : null;
                if (slotVisual != null)
                {
                    slotVisual.TargetCollider.enabled = !hidden;
                    slotVisual.DropTarget.enabled = !hidden;
                    slotVisual.SetEmptyStateHidden(hidden || HidesTableVisual(TableVisualHide.EmptySlotPlate));
                    if (hidden)
                    {
                        slotVisual.ClearFeedback();
                    }
                }

                if (hidden)
                {
                    hiddenConsoleSlotIds.Add(slotView.ContainerId);
                }
                else
                {
                    hiddenConsoleSlotIds.Remove(slotView.ContainerId);
                }
            }

            ConsoleMatFaces faces = instance.View.GetComponent<ConsoleMatFaces>();
            if (faces != null && !faces.IsTurning)
            {
                faces.SetSideImmediate(back);
            }
        }

        // Before a Console's presentation is released: a running turn ends and its slots leave the hidden set.
        private void ForgetConsoleSide(RuntimeConsoleInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            ConsoleMatFaces faces = instance.View != null ? instance.View.GetComponent<ConsoleMatFaces>() : null;
            faces?.CompleteTurn();
            for (int i = 0; i < instance.SlotViews.Length; i++)
            {
                if (instance.SlotViews[i] != null && instance.SlotViews[i].IsBound)
                {
                    hiddenConsoleSlotIds.Remove(instance.SlotViews[i].ContainerId);
                }
            }
        }

        // Cards under a mat's side facing down cannot be picked, although their colliders are still there.
        private bool IsUnderConsoleMat(TabletopObjectView view)
        {
            return hiddenConsoleSlotIds.Count > 0
                && view is CardView card
                && card.BoundState != null
                && hiddenConsoleSlotIds.Contains(card.BoundState.ContainerId);
        }

        private RuntimeConsoleInstance FindConsoleInstanceBySlot(ContainerId slotContainerId)
        {
            for (int i = 0; i < Registry.runtimeConsoleInstances.Count; i++)
            {
                RuntimeConsoleInstance instance = Registry.runtimeConsoleInstances[i];
                for (int s = 0; s < instance.SlotViews.Length; s++)
                {
                    ConsoleSlotView slotView = instance.SlotViews[s];
                    if (slotView != null && slotView.IsBound && slotView.ContainerId == slotContainerId)
                    {
                        return instance;
                    }
                }
            }

            return null;
        }

        // The Console whose mat (plus a small margin) is under the pointer, whichever side faces up.
        internal bool TryFindConsoleUnderPointer(Vector2 screenPosition, out RuntimeConsoleInstance found)
        {
            found = null;
            if (pointerProjector == null
                || coordinateConverter == null
                || catalogConsolePrefab == null
                || Registry.runtimeConsoleInstances.Count == 0
                || !pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                return false;
            }

            Vector3 world = coordinateConverter.ToWorldPosition(new TabletopPose(coordinate, 0f, 0, 0));
            ConsoleLayoutData layout = GetConsoleLayout();
            float halfWidth = (layout.MatWidth * 0.5f) + 0.2f;
            float halfDepth = (layout.MatDepth * 0.5f) + 0.2f;
            for (int i = 0; i < Registry.runtimeConsoleInstances.Count; i++)
            {
                RuntimeConsoleInstance instance = Registry.runtimeConsoleInstances[i];
                if (instance.Root == null
                    || !instance.Root.activeInHierarchy
                    || instance.SlotViews.Length == 0
                    || ResolveConsoleState(instance) == null)
                {
                    continue;
                }

                Vector3 local = instance.View.transform.InverseTransformPoint(world);
                if (Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.z) <= halfDepth)
                {
                    found = instance;
                    return true;
                }
            }

            return false;
        }

        // F turns the Console under the pointer over, unless a card is selected (then F turns that card).
        private void HandleConsoleFlipShortcut()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (!IsInitialized
                || keyboard == null
                || mouse == null
                || !keyboard.fKey.wasPressedThisFrame
                || keyboard.leftCtrlKey.isPressed
                || keyboard.rightCtrlKey.isPressed
                || (interactionRouter != null && interactionRouter.HasActiveInteraction)
                || (selectionState != null && selectionState.HasSelection && selectionState.SelectedView is CardView)
                || !ConsoleMatHasBackSide()
                || !TryFindConsoleUnderPointer(mouse.position.ReadValue(), out RuntimeConsoleInstance instance))
            {
                return;
            }

            FlipConsoleFromSlot(instance.SlotViews[0].ContainerId);
        }

        internal void FlipConsoleFromSlot(ContainerId slotContainerId)
        {
            RuntimeConsoleInstance instance = FindConsoleInstanceBySlot(slotContainerId);
            ConsoleState console = ResolveConsoleState(instance);
            if (console == null || !ConsoleMatHasBackSide())
            {
                ShowMessage("Flip rejected: Console unavailable.");
                ContextMenus.CloseContextMenu();
                return;
            }

            ConsoleMatFaces faces = instance.View.GetComponent<ConsoleMatFaces>();
            faces?.CompleteTurn();
            bool toBack = !console.ShowsBack;
            CommandResult result = TableAuthority.Submit<FlipConsoleCommand, CommandResult>(
                new FlipConsoleCommand(CreateCommandContext(), slotContainerId, toBack));
            if (!result.Succeeded)
            {
                ShowMessage($"Flip rejected: {result.Status}.");
                return;
            }

            ContextMenus.CloseContextMenu();
            ShowMessage(toBack ? "Console turned over: controller side up." : "Console turned over: symbol side up.");
            if (faces == null || faces.TurnPivot == null)
            {
                ApplyConsoleSide(instance);
                return;
            }

            faces.BeginTurn(
                toBack,
                () => OnConsoleTurnMidpoint(instance, faces),
                () => OnConsoleTurnLanded(instance));
            AddConsoleSideRiders(instance, faces, !toBack, false);
        }

        // The mat stands on edge: the side facing up changes, so its cards come up with it.
        private void OnConsoleTurnMidpoint(RuntimeConsoleInstance instance, ConsoleMatFaces faces)
        {
            ConsoleState console = ResolveConsoleState(instance);
            if (console == null)
            {
                return;
            }

            ApplyConsoleSide(instance);
            AddConsoleSideRiders(instance, faces, console.ShowsBack, true);
        }

        private void OnConsoleTurnLanded(RuntimeConsoleInstance instance)
        {
            if (ResolveConsoleState(instance) == null)
            {
                return;
            }

            for (int i = 0; i < instance.SlotViews.Length; i++)
            {
                if (instance.SlotViews[i] != null && instance.SlotViews[i].IsBound)
                {
                    instance.SlotViews[i].ApplyAcceptedLayout();
                }
            }

            ApplyConsoleSide(instance);
            FlashFlipAt(instance.View.transform.position);
        }

        // Cards in the slots of one side turn with the mat: the outgoing side up to the midpoint, the incoming
        // side from it.
        private void AddConsoleSideRiders(RuntimeConsoleInstance instance, ConsoleMatFaces faces, bool backSide, bool incoming)
        {
            ConsoleLayoutData layout = GetConsoleLayout();
            for (int i = 0; i < instance.SlotViews.Length && i < layout.CardSlots.Count; i++)
            {
                ConsoleSlotView slotView = instance.SlotViews[i];
                if (slotView == null
                    || !slotView.IsBound
                    || (layout.CardSlots[i].Side == ConsoleMatSide.Back) != backSide)
                {
                    continue;
                }

                IReadOnlyList<TabletopObjectId> cardIds = slotView.ContainerState.ObjectIds;
                for (int c = 0; c < cardIds.Count; c++)
                {
                    if (!Registry.TryGetCardView(cardIds[c], out CardView cardView))
                    {
                        continue;
                    }

                    if (incoming)
                    {
                        faces.AddIncomingRider(cardView.transform);
                    }
                    else
                    {
                        faces.AddRider(cardView.transform);
                    }
                }
            }
        }

        // The same landing flash as a Trap Floor reveal, where board feedback is available.
        private void FlashFlipAt(Vector3 position)
        {
            if (!playerFeedback || !EnsureBoardFeedback())
            {
                return;
            }

            boardFeedback.Flash(position, new Color(0.34f, 0.94f, 0.86f, 1f));
        }

        internal void BeginCardFlipTurn(CardView card)
        {
            if (cardFlipTurns == null)
            {
                cardFlipTurns = new CardFlipTurnPresenter
                {
                    Landed = FlashFlipAt,
                };
            }

            cardFlipTurns.Begin(card, Time.unscaledTime);
        }

        // Duplicate places a second Console with the other side up, where the player puts it.
        internal void BeginConsoleDuplicate(ContainerId slotContainerId)
        {
            EnsureInitialized();
            RuntimeConsoleInstance instance = FindConsoleInstanceBySlot(slotContainerId);
            ConsoleState console = ResolveConsoleState(instance);
            if (console == null
                || componentPlacementController == null
                || !TryResolveConsolePlacement(slotContainerId, out TabletopPose sourcePose, out _, out _))
            {
                ShowMessage("Duplicate rejected: Console unavailable.");
                ContextMenus.CloseContextMenu();
                return;
            }

            bool otherSideUp = !console.ShowsBack;
            componentPlacementController.Cancel();
            GameObject previewRoot = CreateToolboxPlacementPreview(TabletopComponentKind.Console, 0);
            ConsoleMatFaces previewFaces = previewRoot.GetComponentInChildren<ConsoleMatFaces>(true);
            previewFaces?.SetSideImmediate(otherSideUp);
            ContextMenus.CloseContextMenu();
            componentPlacementController.BeginCustomComponentPlacement(
                TabletopComponentKind.Console,
                previewRoot,
                sourcePose.RotationDegrees,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose => CommitConsoleDuplicate(pose, otherSideUp));
            ShowPlacementHint(otherSideUp ? "Duplicate Console (controller side up)" : "Duplicate Console (symbol side up)");
            KeepToolboxPlacing(false);
        }

        private bool CommitConsoleDuplicate(TabletopPose requestedPose, bool showsBack)
        {
            CreateTabletopComponentResult result = TableAuthority.Submit<CreateTabletopComponentRequest, CreateTabletopComponentResult>(
                new CreateTabletopComponentRequest(
                    CreateCommandContext(),
                    TabletopComponentKind.Console,
                    requestedPose,
                    actionKind: AuthoritativeActionKind.DuplicateComponent,
                    consoleShowsBack: showsBack));
            if (!result.Succeeded)
            {
                ShowMessage($"Duplicate rejected: {result.Error}.");
                return false;
            }

            toolboxSpawnSequence++;
            // R2: the Console got its view from the table's view sync.
            ShowMessage(showsBack
                ? "Duplicated the Console with the controller side up."
                : "Duplicated the Console with the symbol side up.");
            return true;
        }

        // ---- Symbol magnet (ICardDropMagnet) ----
        // A card fits the Console slots marked with its Console symbol or any of its icons (ICON-1) and is pulled
        // into the nearest one; a card without symbols fits any slot.

        bool ICardDropMagnet.IsActive => IsInitialized
            && catalogConsolePrefab != null
            && Registry.runtimeConsoleInstances.Count > 0
            && TryGetDraggedCard(out CardView card)
            && CardHasConsoleSymbols(ConsoleCardDefinition(card));

        bool ICardDropMagnet.TryAdjust(
            ContainerId directContainerId,
            bool hasPoint,
            TableCoordinate point,
            out ContainerId adjustedContainerId)
        {
            adjustedContainerId = directContainerId;
            if (catalogConsolePrefab == null
                || Registry.runtimeConsoleInstances.Count == 0
                || !TryGetDraggedCard(out CardView card))
            {
                return false;
            }

            CardDefinition definition = ConsoleCardDefinition(card);
            if (!CardHasConsoleSymbols(definition))
            {
                return false;
            }

            bool directIsSlot = TryGetConsoleSlotSymbol(directContainerId, out ConsoleSymbol directSymbol);
            if (!directContainerId.IsEmpty && !directIsSlot)
            {
                return false; // a Deck, Stack, pile or Hand under the pointer keeps the card
            }

            if (directIsSlot && (directSymbol == ConsoleSymbol.None || definition.CarriesSymbol(directSymbol)))
            {
                return false;
            }

            ContainerId nearest = hasPoint ? FindNearestSymbolSlot(definition, point) : ContainerId.Empty;
            adjustedContainerId = nearest; // Empty: a slot with another symbol refuses the card; it stays on the table
            return directIsSlot || !nearest.IsEmpty;
        }

        private bool TryGetDraggedCard(out CardView card)
        {
            card = previewSession != null && previewSession.IsActive ? previewSession.ActiveView as CardView : null;
            if (card != null)
            {
                return true;
            }

            for (int i = 0; i < Registry.cardViews.Count; i++)
            {
                CardView candidate = Registry.cardViews[i];
                if (candidate != null && candidate.PhysicalObject != null && candidate.PhysicalObject.IsHeld)
                {
                    card = candidate;
                    return true;
                }
            }

            return false;
        }

        // The card's definition, cached by definition ID (looked up again when the cached one is gone, for example
        // after a new table made the game's runtime cards again).
        private CardDefinition ConsoleCardDefinition(CardView card)
        {
            if (card == null || card.CardState == null)
            {
                return null;
            }

            ObjectDefinitionId definitionId = card.CardState.BaseState.DefinitionId;
            if (!consoleCardsByDefinition.TryGetValue(definitionId, out CardDefinition definition) || definition == null)
            {
                definition = TryGetCardLook(definitionId, out CardDefinition found) ? found : null;
                consoleCardsByDefinition[definitionId] = definition;
            }

            return definition;
        }

        private static bool CardHasConsoleSymbols(CardDefinition definition) =>
            definition != null && (definition.ConsoleSymbol != ConsoleSymbol.None || definition.HasEdgeSymbols);

        private bool TryGetConsoleSlotSymbol(ContainerId slotContainerId, out ConsoleSymbol symbol)
        {
            symbol = ConsoleSymbol.None;
            if (slotContainerId.IsEmpty)
            {
                return false;
            }

            ConsoleLayoutData layout = GetConsoleLayout();
            for (int i = 0; i < Registry.runtimeConsoleInstances.Count; i++)
            {
                ConsoleSlotView[] slotViews = Registry.runtimeConsoleInstances[i].SlotViews;
                for (int s = 0; s < slotViews.Length && s < layout.CardSlots.Count; s++)
                {
                    if (slotViews[s] != null && slotViews[s].IsBound && slotViews[s].ContainerId == slotContainerId)
                    {
                        symbol = layout.CardSlots[s].Symbol;
                        return true;
                    }
                }
            }

            return false;
        }

        // The nearest free, shown slot whose symbol the card carries (its Console symbol or any icon).
        private ContainerId FindNearestSymbolSlot(CardDefinition definition, TableCoordinate point)
        {
            ConsoleLayoutData layout = GetConsoleLayout();
            ContainerId nearest = ContainerId.Empty;
            double nearestDistance = consoleSymbolMagnetDistance;
            for (int i = 0; i < Registry.runtimeConsoleInstances.Count; i++)
            {
                ConsoleSlotView[] slotViews = Registry.runtimeConsoleInstances[i].SlotViews;
                for (int s = 0; s < slotViews.Length && s < layout.CardSlots.Count; s++)
                {
                    ConsoleSlotView slotView = slotViews[s];
                    if (slotView == null
                        || !slotView.IsBound
                        || slotView.IsSideHidden
                        || !definition.CarriesSymbol(layout.CardSlots[s].Symbol)
                        || slotView.ContainerState.IsFull
                        || slotView.LayoutAnchor == null)
                    {
                        continue;
                    }

                    TableCoordinate centre = coordinateConverter.ToTableCoordinate(slotView.LayoutAnchor.position);
                    double dx = centre.X - point.X;
                    double dy = centre.Y - point.Y;
                    double distance = Math.Sqrt((dx * dx) + (dy * dy));
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = slotView.ContainerId;
                    }
                }
            }

            return nearest;
        }

        // ---- Icon turn (ICON-1) ----

        // Card-local directions of the 8 icon places (CardIconPlace order): +z is the top of the card, +x its right.
        private static readonly Vector3[] CardIconDirections =
        {
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, -1f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0.7071068f, 0f, 0.7071068f),
            new Vector3(0.7071068f, 0f, -0.7071068f),
            new Vector3(-0.7071068f, 0f, -0.7071068f),
            new Vector3(-0.7071068f, 0f, 0.7071068f),
        };

        private static readonly float[] SquareSlotTurns = { 0f, 90f, 180f, 270f };
        private static readonly float[] LongSlotTurns = { 0f, 180f };

        private Func<CardView, ConsoleSlotView, float> ConsoleCardTurn =>
            consoleCardTurn ?? (consoleCardTurn = CardTurnInSlot);

        // How far a card turns in a Console slot on top of the slot orientation: the turn (0/180 in a long slot, any
        // quarter turn in a square one) that points the card's icon matching the slot's symbol most outward on the
        // mat. Edge slots point out across their edge, corner slots out through their corner. 0 when the card has no
        // matching icon (a card matched by its Console symbol alone keeps the slot orientation).
        private float CardTurnInSlot(CardView card, ConsoleSlotView slot)
        {
            CardDefinition definition = ConsoleCardDefinition(card);
            if (definition == null || !definition.HasEdgeSymbols || !TryGetSlotLayout(slot, out ConsoleLayoutSlotData data))
            {
                return 0f;
            }

            ConsoleSymbol symbol = data.Symbol;
            if (symbol == ConsoleSymbol.None || !TryGetSlotOutward(data, out Vector3 outward))
            {
                return 0f;
            }

            float[] turns = IsSquareSlot(data) ? SquareSlotTurns : LongSlotTurns;
            float bestTurn = 0f;
            float bestScore = float.NegativeInfinity;
            for (int t = 0; t < turns.Length; t++)
            {
                Quaternion relative = Quaternion.Euler(0f, slot.OrientationOffsetDegrees + turns[t], 0f);
                for (int place = 0; place < CardDefinition.EdgeIconCount; place++)
                {
                    if (definition.EdgeSymbol((CardIconPlace)place) != symbol)
                    {
                        continue;
                    }

                    float score = Vector3.Dot(relative * CardIconDirections[place], outward);
                    if (score > bestScore + 0.001f)
                    {
                        bestScore = score;
                        bestTurn = turns[t];
                    }
                }
            }

            return float.IsNegativeInfinity(bestScore) ? 0f : bestTurn;
        }

        private bool TryGetSlotLayout(ConsoleSlotView slot, out ConsoleLayoutSlotData data)
        {
            data = null;
            if (slot == null)
            {
                return false;
            }

            ConsoleLayoutData layout = GetConsoleLayout();
            for (int i = 0; i < Registry.runtimeConsoleInstances.Count; i++)
            {
                ConsoleSlotView[] slotViews = Registry.runtimeConsoleInstances[i].SlotViews;
                for (int s = 0; s < slotViews.Length && s < layout.CardSlots.Count; s++)
                {
                    if (ReferenceEquals(slotViews[s], slot))
                    {
                        data = layout.CardSlots[s];
                        return true;
                    }
                }
            }

            return false;
        }

        // Console-local direction from the mat's middle out through the slot: corner slots ("Corners" group) point
        // diagonally, other slots across the nearest mat edge (positions measured against the farthest slot each way).
        private bool TryGetSlotOutward(ConsoleLayoutSlotData data, out Vector3 outward)
        {
            outward = Vector3.zero;
            float x = data.X;
            float z = data.Z;
            if (Mathf.Abs(x) < 0.0001f && Mathf.Abs(z) < 0.0001f)
            {
                return false;
            }

            if (string.Equals(data.Group, "Corners", StringComparison.OrdinalIgnoreCase))
            {
                outward = new Vector3(Mathf.Sign(x), 0f, Mathf.Sign(z)).normalized;
                return true;
            }

            ConsoleLayoutData layout = GetConsoleLayout();
            float reachX = 0.0001f;
            float reachZ = 0.0001f;
            for (int i = 0; i < layout.CardSlots.Count; i++)
            {
                reachX = Mathf.Max(reachX, Mathf.Abs(layout.CardSlots[i].X));
                reachZ = Mathf.Max(reachZ, Mathf.Abs(layout.CardSlots[i].Z));
            }

            outward = Mathf.Abs(x) / reachX >= Mathf.Abs(z) / reachZ
                ? new Vector3(Mathf.Sign(x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(z));
            return true;
        }

        private static bool IsSquareSlot(ConsoleLayoutSlotData data) =>
            data.FootprintWidth > 0f
            && Mathf.Abs(data.FootprintWidth - data.FootprintDepth) <= 0.05f * data.FootprintWidth;

        // ---- Dice on the D-pad (back side) ----

        // A die that comes to rest on a D-pad arrow of a Console showing its back is set on the arrow's centre,
        // and the number picker opens. One die at a time; a die already on its arrow is left alone.
        private void CompleteConsoleDieSnaps()
        {
            if (!consoleDieCheckPending || !IsInitialized || matchState == null)
            {
                return;
            }

            consoleDieCheckPending = false;
            if (!ConsoleMatHasBackSide())
            {
                return;
            }

            for (int d = 0; d < Registry.dieViews.Count; d++)
            {
                DieView dieView = Registry.dieViews[d];
                if (dieView == null || !dieView.IsBound || dieView.DieState == null)
                {
                    continue;
                }

                TabletopObjectId dieId = dieView.ObjectId;
                if (!TryFindDieSpot(dieView, out RuntimeConsoleInstance instance, out ConsoleLayoutSlotData spot, out Vector3 spotWorld, out _))
                {
                    diceOnConsoleSpots.Remove(dieId);
                    continue;
                }

                TabletopObjectState state = dieView.DieState.BaseState;
                if (state.PhysicalState == null
                    || state.PhysicalState.Mode != PhysicalObjectMode.Sleeping
                    || (dieView.PhysicalObject != null && dieView.PhysicalObject.IsHeld))
                {
                    continue;
                }

                if (diceOnConsoleSpots.TryGetValue(dieId, out string spotKey) && spotKey == spot.Key)
                {
                    continue; // already set on this arrow
                }

                if (!TryPlaceDieOnSpot(dieView, instance, spotWorld, null))
                {
                    continue;
                }

                diceOnConsoleSpots[dieId] = spot.Key;
                OpenDieNumberPicker(dieView);
                return;
            }
        }

        private bool TryFindDieSpot(
            DieView dieView,
            out RuntimeConsoleInstance foundInstance,
            out ConsoleLayoutSlotData foundSpot,
            out Vector3 foundWorld,
            out float foundDistance)
        {
            foundInstance = null;
            foundSpot = null;
            foundWorld = Vector3.zero;
            foundDistance = float.MaxValue;
            if (dieView == null || !dieView.IsBound || !dieView.BoundState.ContainerId.IsEmpty)
            {
                return false;
            }

            Vector3 diePosition = dieView.PhysicalObject != null && dieView.PhysicalObject.Body != null
                ? dieView.PhysicalObject.Body.position
                : dieView.transform.position;
            ConsoleLayoutData layout = GetConsoleLayout();
            for (int i = 0; i < Registry.runtimeConsoleInstances.Count; i++)
            {
                RuntimeConsoleInstance instance = Registry.runtimeConsoleInstances[i];
                ConsoleState console = ResolveConsoleState(instance);
                if (console == null || instance.Root == null || !instance.Root.activeInHierarchy)
                {
                    continue;
                }

                for (int s = 0; s < layout.Slots.Count; s++)
                {
                    ConsoleLayoutSlotData slot = layout.Slots[s];
                    if (slot.Kind != ConsoleLayoutSlotKind.Die
                        || (slot.Side == ConsoleMatSide.Back) != console.ShowsBack)
                    {
                        continue;
                    }

                    Vector3 world = instance.View.transform.TransformPoint(new Vector3(slot.X, 0f, slot.Z));
                    float dx = world.x - diePosition.x;
                    float dz = world.z - diePosition.z;
                    float distance = Mathf.Sqrt((dx * dx) + (dz * dz));
                    if (distance <= consoleDieSpotRadius && distance < foundDistance)
                    {
                        foundInstance = instance;
                        foundSpot = slot;
                        foundWorld = world;
                        foundDistance = distance;
                    }
                }
            }

            return foundInstance != null;
        }

        // Puts the die on the spot's centre through physical authority. value null keeps the die's own turn;
        // a value turns that number up. The die then settles and the number is read from how it lies.
        private bool TryPlaceDieOnSpot(DieView dieView, RuntimeConsoleInstance instance, Vector3 spotWorld, int? value)
        {
            TabletopObjectState state = dieView.DieState.BaseState;
            if (physicalAuthority == null
                || dieView.PhysicalObject == null
                || dieView.PhysicalObject.IsHeld
                || state.IsUserLocked
                || activeSession == null)
            {
                return false;
            }

            TabletopPose spotPose = new TabletopPose(coordinateConverter.ToTableCoordinate(spotWorld), 0f, 0, 0);
            if (!physicalAuthority.Surfaces.TryResolveAuthoredLooseObject(
                    spotPose,
                    localPlayerId,
                    dieView.PhysicalObject.PhysicalCollider,
                    false,
                    out PhysicalObjectState placed))
            {
                return false;
            }

            Quaternion rotation;
            if (value.HasValue)
            {
                if (!dieView.TryGetValueRotation(value.Value, instance.View.transform.eulerAngles.y, out rotation))
                {
                    return false;
                }
            }
            else
            {
                rotation = dieView.PhysicalObject.Body != null ? dieView.PhysicalObject.Body.rotation : dieView.transform.rotation;
            }

            placed = placed.WithRotation(new PhysicalRotation(rotation.x, rotation.y, rotation.z, rotation.w));
            CommandResult result = TableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(
                new CommitPhysicalObjectCommand(
                    CreateCommandContext(),
                    state.Id,
                    placed,
                    state.PhysicalRevision,
                    null,
                    AuthoritativeActionRecordMode.Transaction));
            if (!result.Succeeded)
            {
                return false;
            }

            dieView.ApplyAcceptedState();
            return true;
        }

        private void OpenDieNumberPicker(DieView dieView)
        {
            UnityCamera camera = hitResolver != null ? hitResolver.TargetCamera : null;
            if (camera == null || runtimeUi == null)
            {
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(dieView.transform.position);
            selectionState.Select(dieView);
            selectionPresenter.Refresh();
            ContextMenus.OpenContextMenu(
                ContextMenuMode.Die,
                new Vector2(screen.x, screen.y),
                TabletopObjectId.Empty,
                ContainerId.Empty,
                dieView.ObjectId);
        }

        // The number picker: one row per face, shown in the Die menu while the die is on a D-pad arrow.
        internal void AddDieNumberActions(List<PopupActionOption> actions, DieView dieView)
        {
            if (dieView == null
                || dieView.DieState == null
                || !ConsoleMatHasBackSide()
                || !TryFindDieSpot(dieView, out _, out ConsoleLayoutSlotData spot, out _, out _))
            {
                return;
            }

            TabletopObjectId dieId = dieView.ObjectId;
            int faces = Math.Min(dieView.DieState.SideCount, 20);
            for (int value = 1; value <= faces; value++)
            {
                int chosen = value;
                actions.Add(new PopupActionOption(
                    $"Show {chosen} ({DescribeDieSpot(spot)})",
                    chosen != dieView.DieState.CurrentValue,
                    () => SetConsoleDieNumber(dieId, chosen)));
            }
        }

        private void SetConsoleDieNumber(TabletopObjectId dieId, int value)
        {
            if (contextMenuMode != ContextMenuMode.Die || contextMenuDieId != dieId)
            {
                return;
            }

            if (!Registry.TryGetDieView(dieId, out DieView dieView)
                || !TryFindDieSpot(dieView, out RuntimeConsoleInstance instance, out ConsoleLayoutSlotData spot, out Vector3 spotWorld, out _))
            {
                ShowMessage("Number not set: the die is not on a D-pad arrow.");
                ContextMenus.CloseContextMenu();
                return;
            }

            if (!TryPlaceDieOnSpot(dieView, instance, spotWorld, value))
            {
                ShowMessage("Number not set: the die is held, locked or unavailable.");
                return;
            }

            diceOnConsoleSpots[dieId] = spot.Key;
            ContextMenus.CloseContextMenu();
            ShowMessage($"The die on the {DescribeDieSpot(spot)} now shows {value}.");
        }

        private static string DescribeDieSpot(ConsoleLayoutSlotData spot)
        {
            switch (spot.Key)
            {
                case "PadUp": return "Up arrow";
                case "PadLeft": return "Left arrow";
                case "PadRight": return "Right arrow";
                case "PadDown": return "Down arrow";
                default: return spot.Key;
            }
        }
    }
}
