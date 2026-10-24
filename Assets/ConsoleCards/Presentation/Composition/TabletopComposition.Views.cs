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
    /// <summary>Finding, binding and creating piece views. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {

        internal bool TryResolveConsolePlacement(
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

            for (int i = 0; i < Registry.playerConsoleViews.Count; i++)
            {
                ConsoleView candidate = Registry.playerConsoleViews[i];
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

        private void BindObjectViews()
        {
            // Every card, the local Avatar included, and every pawn and token is a catalog spawn (C3b-1).
            foreach (CardInstanceState card in matchState.Cards.Values)
            {
                CardView createdView = Spawner.CreateCardView(
                    card,
                    ResolveCardLabel(card),
                    out TabletopSelectionVisual createdSelectionVisual);
                Registry.cardViews.Add(createdView);
                Registry.cardSelectionVisuals.Add(createdSelectionVisual);
            }

            foreach (PawnState pawn in matchState.Pawns.Values)
            {
                PawnView createdPawnView = Spawner.CreatePawnView(pawn, out TabletopSelectionVisual createdSelectionVisual);
                Registry.pawnViews.Add(createdPawnView);
                Registry.pawnSelectionVisuals.Add(createdSelectionVisual);
            }

            Spawner.ConfigureOfficialPawnPresentation();

            foreach (TokenState token in matchState.Tokens.Values)
            {
                TokenView createdTokenView = Spawner.CreateTokenView(
                    token,
                    out TabletopSelectionVisual createdSelectionVisual,
                    CoinVisualScale);
                Registry.tokenViews.Add(createdTokenView);
                Registry.tokenSelectionVisuals.Add(createdSelectionVisual);
            }

            foreach (DieState die in matchState.Dice.Values)
            {
                DieView createdDieView = Spawner.CreateDieView(
                    die,
                    $"d{die.SideCount}",
                    out TabletopSelectionVisual createdSelectionVisual);
                Registry.dieViews.Add(createdDieView);
                Registry.dieSelectionVisuals.Add(createdSelectionVisual);
            }
        }

        private void BuildContainerViews()
        {
            localHandVisual = Spawner.CreateLocalHandVisual();
            handView = localHandVisual.GetView<HandView>();
            handView.ConfigurePresentation(presentationTransitions, localHandVisual.FeedbackRenderer);
            if (handsOnTable && TryPlaceLocalHandOnTable())
            {
                handTrayRig?.Deactivate(); // F3: the hand lies on its table zone; no screen tray
            }
            else
            {
                if (handTrayRig == null)
                {
                    handTrayRig = HandTrayRig.Create(
                        targetCamera,
                        localHandVisual.TargetCollider.gameObject.layer,
                        localHandVisual.FeedbackRenderer.sharedMaterial);
                }

                handTrayRig.Activate();
                handView.ConfigureTray(handTrayRig);
            }

            IReadOnlyList<CartridgeSeat> seats = ActiveCartridge.Seats; // CART-1b: the game's seats
            for (int playerIndex = 0; playerIndex < seats.Count; playerIndex++)
            {
                CartridgeSeat player = seats[playerIndex];
                // TABLE-1: a seat's own Controller Deck and Action stack. A deck shared by every seat (no owner) and
                // the game's other piles get their views with the table's placed piles.
                if (matchState.Containers.TryGetValue(player.ControllerDeckId, out ContainerState seatDeck)
                    && !seatDeck.OwnerSeatId.IsEmpty)
                {
                    RuntimeDeckInstance controllerDeck = Spawner.CreateRuntimeDeckInstance(
                        $"Player {playerIndex + 1} Controller Deck",
                        $"P{playerIndex + 1} CTRL",
                        player.ControllerDeckId,
                        false);
                    Registry.runtimeDeckInstances.Add(controllerDeck);
                    GameTemplatePileStyle deckPileStyle = ResolveTemplatePileStyle(player.ControllerDeckId)
                        ?? GameTemplatePileStyle.DefaultFor(ContainerKind.Deck);
                    controllerDeck.View.ConfigureTableRest(true, deckPileStyle.MaximumPileHeight);
                    ConfigurePileBay(controllerDeck.Visual, deckPileStyle, player.SeatId);

                    Registry.controllerDeckViews.Add(controllerDeck.View);
                }

                ContainerId actionAreaId = player.ActionAreaContainerId;
                if (!actionAreaId.IsEmpty && matchState.Containers.ContainsKey(actionAreaId))
                {
                    StackRuntimeView actionArea = Spawner.CreateStackRuntimeView(
                        $"P{playerIndex + 1} ACTIONS",
                        matchState.GetContainer(actionAreaId),
                        matchState.ContainerPlacements[actionAreaId],
                        false,
                        ResolveTemplatePileStyle(actionAreaId));
                    Registry.stackViewsByContainerId.Add(actionAreaId, actionArea);
                }

                if (player.LayoutSeatIndex != localPlayerLayoutSeatIndex
                    && matchState.TryGetContainerPlacement(player.HandContainerId, out ContainerPlacementState hiddenHandZone)
                    && hiddenHandZone.HasExtent)
                {
                    Registry.hiddenHandViews.Add(CreateHiddenHandView(
                        $"Player {playerIndex + 1} Hidden Hand",
                        matchState.GetContainer(player.HandContainerId),
                        hiddenHandZone));
                }

                // Every seat's Console, the local one included, is the catalog Console (C3b-1).
                RuntimeConsoleInstance playerConsole = Spawner.CreateRuntimeConsoleInstance(
                    $"Player {playerIndex + 1} Console",
                    player.LayoutSeatIndex,
                    player.SeatId);
                Registry.runtimeConsoleInstances.Add(playerConsole);
                Registry.playerConsoleViews.Add(playerConsole.View);
                if (player.LayoutSeatIndex == localPlayerLayoutSeatIndex)
                {
                    consoleView = playerConsole.View;
                }
            }

        }

        // Hands on table (F3): the local hand plate sits on this seat's hand zone, turned to the seat.
        private bool TryPlaceLocalHandOnTable()
        {
            if (localHandVisual == null
                || matchState == null
                || !matchState.TryGetContainerPlacement(handContainerId, out ContainerPlacementState zone))
            {
                return false;
            }

            localHandVisual.transform.SetPositionAndRotation(
                ContainerViewBinding.PlacementWorldPosition(zone, coordinateConverter),
                coordinateConverter.ToWorldRotation(zone.Pose));
            return true;
        }

        private void BindContainerViews()
        {
            ContainerState hand = matchState.GetContainer(handContainerId);

            handView.Bind(hand, localHandVisual.LayoutAnchor, coordinateConverter, Registry.cardViews);
            foreach (StackRuntimeView stackRuntimeView in Registry.stackViewsByContainerId.Values)
            {
                stackRuntimeView.View.Bind(
                    stackRuntimeView.Container,
                    stackRuntimeView.Placement,
                    stackRuntimeView.Visual.LayoutAnchor,
                    coordinateConverter,
                    Registry.cardViews);
            }

            Registry.consoleSlotViews.Clear();
            for (int i = 0; i < Registry.runtimeConsoleInstances.Count; i++)
            {
                RuntimeConsoleInstance instance = Registry.runtimeConsoleInstances[i];
                CartridgeSeat player = ActiveCartridge.Seats[instance.LayoutSeatIndex];
                BindConsole(
                    instance.View,
                    matchState.GetSeat(player.SeatId).Console,
                    instance.SlotViews,
                    instance.SlotVisuals);
            }

            AssignPileBayMouthTargets();

            for (int i = 0; i < Registry.runtimeDeckInstances.Count; i++)
            {
                RuntimeDeckInstance instance = Registry.runtimeDeckInstances[i];
                ContainerState controllerDeck = matchState.GetContainer(instance.ContainerId);
                instance.View.Bind(
                    controllerDeck,
                    matchState.ContainerPlacements[instance.ContainerId],
                    coordinateConverter,
                    Registry.cardViews);
            }

            for (int i = 0; i < Registry.runtimeTokenContainerInstances.Count; i++)
            {
                RuntimeTokenContainerInstance instance = Registry.runtimeTokenContainerInstances[i];
                instance.View.Bind(
                    matchState.GetContainer(instance.ContainerId),
                    instance.Pose,
                    coordinateConverter,
                    Registry.tokenViews,
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
            IReadOnlyList<ConsoleSlotVisual> slotVisuals)
        {
            if (slotViews.Count != runtimeConsole.SlotCount || slotVisuals.Count != runtimeConsole.SlotCount)
            {
                throw new InvalidOperationException("The game's Console presentation does not match its Runtime Slot count.");
            }

            List<ConsoleSlotView> orderedSlots = new List<ConsoleSlotView>(runtimeConsole.SlotCount);
            ConsoleLayoutData layout = GetConsoleLayout();
            for (int i = 0; i < runtimeConsole.SlotCount; i++)
            {
                ContainerState slot = matchState.GetContainer(runtimeConsole.SlotContainerIds[i]);
                ConsoleSlotView slotView = slotViews[i];
                ConsoleSlotVisual slotVisual = slotVisuals[i];
                // Cards in a slot face the slot's layout default orientation relative to the Console (A1h).
                if (i < layout.CardSlots.Count)
                {
                    slotView.ConfigureCardOrientation(
                        targetConsole.transform,
                        layout.CardSlots[i].DefaultOrientation == ConsoleSlotOrientation.Landscape ? 90f : 0f);
                    slotView.SetCardTurn(ConsoleCardTurn); // ICON-1: a card turns its matching icon outward
                }

                slotView.Bind(slot, slotVisual.LayoutAnchor, coordinateConverter, Registry.cardViews);
                Registry.consoleSlotViews.Add(slotView);
                Registry.consoleSlotVisualsByContainerId.Add(slot.Id, slotVisual);
                orderedSlots.Add(slotView);
            }

            targetConsole.Bind(runtimeConsole, targetConsole.LayoutAnchor, orderedSlots);
        }

        // Owner, 2026-10-25 (front end Players step): each seat can pick its colour; seat -> colour, null = in seat order.
        private static int[] seatColourOrder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSeatColours() => seatColourOrder = null;

        internal static int SeatColourCount => PlayerColors.Length;

        /// <summary>Sets which colour each seat plays (applied when a table starts; null puts them back in seat order).</summary>
        internal static void SetSeatColourOrder(int[] order)
        {
            seatColourOrder = order != null ? (int[])order.Clone() : null;
        }

        private static int SeatColourIndex(int playerIndex)
        {
            return seatColourOrder != null && playerIndex >= 0 && playerIndex < seatColourOrder.Length
                ? seatColourOrder[playerIndex]
                : playerIndex;
        }

        internal static Color PlayerSeatColor(int playerIndex) => ColourByIndex(SeatColourIndex(playerIndex));

        internal static string PlayerColorName(int playerIndex) => ColourNameByIndex(SeatColourIndex(playerIndex));

        internal static Color ColourByIndex(int colourIndex)
        {
            return colourIndex >= 0 && colourIndex < PlayerColors.Length
                ? PlayerColors[colourIndex]
                : new Color(0.75f, 0.75f, 0.75f);
        }

        internal static string ColourNameByIndex(int colourIndex)
        {
            return colourIndex >= 0 && colourIndex < PlayerColorNames.Length
                ? PlayerColorNames[colourIndex]
                : "GREY";
        }

        // The layout seat index of a participating player; it picks the player's colour.
        internal bool TryGetSeatIndex(PlayerId playerId, out int seatIndex)
        {
            seatIndex = -1;
            if (!TryGetGameSeat(playerId, out CartridgeSeat seat)) return false;
            seatIndex = seat.LayoutSeatIndex;
            return true;
        }

        private void ConfigureStackDropTarget(StackRuntimeView stackRuntimeView)
        {
            ConfigureFixedContainer(stackRuntimeView.Visual, stackRuntimeView.View);
            stackRuntimeView.DropTarget = stackRuntimeView.Visual.DropTarget;
        }

        private void ConfigureConsoleSlot(ConsoleSlotView slotView)
        {
            if (!Registry.consoleSlotVisualsByContainerId.TryGetValue(
                    slotView.ContainerId,
                    out ConsoleSlotVisual slotVisual))
            {
                throw new InvalidOperationException("A bound Console Slot is missing its authored visual references.");
            }

            slotVisual.DropTarget.Configure(slotView, slotVisual.TargetCollider);
            slotVisual.DropTarget.enabled = true;
            slotVisual.TargetCollider.enabled = true;
            slotVisual.ClearFeedback();
            slotVisual.SetEmptyStateHidden(HidesTableVisual(TableVisualHide.EmptySlotPlate));
            feedbackTargetsByContainerId[slotView.ContainerId] = new ContainerFeedbackTarget(slotVisual);
        }

        internal bool IsButtonCard(CardInstanceState card)
        {
            return buttonDefinitions.ContainsKey(card.BaseState.DefinitionId);
        }
    }
}
