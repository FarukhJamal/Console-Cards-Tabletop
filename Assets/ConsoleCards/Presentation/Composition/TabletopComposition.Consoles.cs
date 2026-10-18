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

        private bool ConsoleMatHasBackSide()
        {
            return catalogConsolePrefab != null
                && runtimeConsoleInstances.Count > 0
                && GetConsoleLayout().HasBackSide;
        }

        private void ApplyAllConsoleSides()
        {
            for (int i = 0; i < runtimeConsoleInstances.Count; i++)
            {
                ApplyConsoleSide(runtimeConsoleInstances[i]);
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
            for (int i = 0; i < runtimeConsoleInstances.Count; i++)
            {
                RuntimeConsoleInstance instance = runtimeConsoleInstances[i];
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
        private bool TryFindConsoleUnderPointer(Vector2 screenPosition, out RuntimeConsoleInstance found)
        {
            found = null;
            if (pointerProjector == null
                || coordinateConverter == null
                || catalogConsolePrefab == null
                || runtimeConsoleInstances.Count == 0
                || !pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                return false;
            }

            Vector3 world = coordinateConverter.ToWorldPosition(new TabletopPose(coordinate, 0f, 0, 0));
            ConsoleLayoutData layout = GetConsoleLayout();
            float halfWidth = (layout.MatWidth * 0.5f) + 0.2f;
            float halfDepth = (layout.MatDepth * 0.5f) + 0.2f;
            for (int i = 0; i < runtimeConsoleInstances.Count; i++)
            {
                RuntimeConsoleInstance instance = runtimeConsoleInstances[i];
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

        private void FlipConsoleFromSlot(ContainerId slotContainerId)
        {
            RuntimeConsoleInstance instance = FindConsoleInstanceBySlot(slotContainerId);
            ConsoleState console = ResolveConsoleState(instance);
            if (console == null || !ConsoleMatHasBackSide())
            {
                ShowMessage("Flip rejected: Console unavailable.");
                CloseContextMenu();
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

            CloseContextMenu();
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
                    if (!TryGetCardView(cardIds[c], out CardView cardView))
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

        private void BeginCardFlipTurn(CardView card)
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
        private void BeginConsoleDuplicate(ContainerId slotContainerId)
        {
            EnsureInitialized();
            RuntimeConsoleInstance instance = FindConsoleInstanceBySlot(slotContainerId);
            ConsoleState console = ResolveConsoleState(instance);
            if (console == null
                || componentPlacementController == null
                || !TryResolveConsolePlacement(slotContainerId, out TabletopPose sourcePose, out _, out _))
            {
                ShowMessage("Duplicate rejected: Console unavailable.");
                CloseContextMenu();
                return;
            }

            bool otherSideUp = !console.ShowsBack;
            componentPlacementController.Cancel();
            GameObject previewRoot = CreateToolboxPlacementPreview(TabletopComponentKind.Console, 0);
            ConsoleMatFaces previewFaces = previewRoot.GetComponentInChildren<ConsoleMatFaces>(true);
            previewFaces?.SetSideImmediate(otherSideUp);
            CloseContextMenu();
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
            ProjectCreatedToolboxComponent(result);
            ShowMessage(showsBack
                ? "Duplicated the Console with the controller side up."
                : "Duplicated the Console with the symbol side up.");
            return true;
        }

        // ---- Symbol magnet (ICardDropMagnet) ----

        bool ICardDropMagnet.IsActive => IsInitialized
            && catalogConsolePrefab != null
            && runtimeConsoleInstances.Count > 0
            && TryGetDraggedCard(out CardView card)
            && CardConsoleSymbol(card) != ConsoleSymbol.None;

        bool ICardDropMagnet.TryAdjust(
            ContainerId directContainerId,
            bool hasPoint,
            TableCoordinate point,
            out ContainerId adjustedContainerId)
        {
            adjustedContainerId = directContainerId;
            if (catalogConsolePrefab == null
                || runtimeConsoleInstances.Count == 0
                || !TryGetDraggedCard(out CardView card))
            {
                return false;
            }

            ConsoleSymbol symbol = CardConsoleSymbol(card);
            if (symbol == ConsoleSymbol.None)
            {
                return false;
            }

            bool directIsSlot = TryGetConsoleSlotSymbol(directContainerId, out ConsoleSymbol directSymbol);
            if (!directContainerId.IsEmpty && !directIsSlot)
            {
                return false; // a Deck, Stack, pile or Hand under the pointer keeps the card
            }

            if (directIsSlot && (directSymbol == ConsoleSymbol.None || directSymbol == symbol))
            {
                return false;
            }

            ContainerId nearest = hasPoint ? FindNearestSymbolSlot(symbol, point) : ContainerId.Empty;
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

            for (int i = 0; i < cardViews.Count; i++)
            {
                CardView candidate = cardViews[i];
                if (candidate != null && candidate.PhysicalObject != null && candidate.PhysicalObject.IsHeld)
                {
                    card = candidate;
                    return true;
                }
            }

            return false;
        }

        private ConsoleSymbol CardConsoleSymbol(CardView card)
        {
            if (card == null || card.CardState == null)
            {
                return ConsoleSymbol.None;
            }

            ObjectDefinitionId definitionId = card.CardState.BaseState.DefinitionId;
            if (!consoleSymbolsByDefinition.TryGetValue(definitionId, out ConsoleSymbol symbol))
            {
                symbol = TryGetCardLook(definitionId, out CardDefinition definition)
                    ? definition.ConsoleSymbol
                    : ConsoleSymbol.None;
                consoleSymbolsByDefinition[definitionId] = symbol;
            }

            return symbol;
        }

        private bool TryGetConsoleSlotSymbol(ContainerId slotContainerId, out ConsoleSymbol symbol)
        {
            symbol = ConsoleSymbol.None;
            if (slotContainerId.IsEmpty)
            {
                return false;
            }

            ConsoleLayoutData layout = GetConsoleLayout();
            for (int i = 0; i < runtimeConsoleInstances.Count; i++)
            {
                ConsoleSlotView[] slotViews = runtimeConsoleInstances[i].SlotViews;
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

        private ContainerId FindNearestSymbolSlot(ConsoleSymbol symbol, TableCoordinate point)
        {
            ConsoleLayoutData layout = GetConsoleLayout();
            ContainerId nearest = ContainerId.Empty;
            double nearestDistance = consoleSymbolMagnetDistance;
            for (int i = 0; i < runtimeConsoleInstances.Count; i++)
            {
                ConsoleSlotView[] slotViews = runtimeConsoleInstances[i].SlotViews;
                for (int s = 0; s < slotViews.Length && s < layout.CardSlots.Count; s++)
                {
                    ConsoleSlotView slotView = slotViews[s];
                    if (slotView == null
                        || !slotView.IsBound
                        || slotView.IsSideHidden
                        || layout.CardSlots[s].Symbol != symbol
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

            for (int d = 0; d < dieViews.Count; d++)
            {
                DieView dieView = dieViews[d];
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
            for (int i = 0; i < runtimeConsoleInstances.Count; i++)
            {
                RuntimeConsoleInstance instance = runtimeConsoleInstances[i];
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
            OpenContextMenu(
                ContextMenuMode.Die,
                new Vector2(screen.x, screen.y),
                TabletopObjectId.Empty,
                ContainerId.Empty,
                dieView.ObjectId);
        }

        // The number picker: one row per face, shown in the Die menu while the die is on a D-pad arrow.
        private void AddDieNumberActions(List<PopupActionOption> actions, DieView dieView)
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

            if (!TryGetDieView(dieId, out DieView dieView)
                || !TryFindDieSpot(dieView, out RuntimeConsoleInstance instance, out ConsoleLayoutSlotData spot, out Vector3 spotWorld, out _))
            {
                ShowMessage("Number not set: the die is not on a D-pad arrow.");
                CloseContextMenu();
                return;
            }

            if (!TryPlaceDieOnSpot(dieView, instance, spotWorld, value))
            {
                ShowMessage("Number not set: the die is held, locked or unavailable.");
                return;
            }

            diceOnConsoleSpots[dieId] = spot.Key;
            CloseContextMenu();
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
