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
    /// <summary>Finding, binding and creating piece views. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
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

        private void BindObjectViews()
        {
            // Every card, the local Avatar included, and every pawn and token is a catalog spawn (C3b-1).
            foreach (CardInstanceState card in matchState.Cards.Values)
            {
                CardView createdView = CreateCardView(
                    card,
                    ResolveCardLabel(card),
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
        private FixedContainerVisual CreateLocalHandVisual()
        {
            FixedContainerVisual visual = Instantiate(catalogHandPrefab);
            GameObject root = PrepareRuntimeRoot(visual.gameObject, "Local Hand");
            root.transform.SetPositionAndRotation(LocalHandRestPosition, Quaternion.identity);
            visual.ValidateReferences();
            ApplyFixedContainerVisualHide(visual, true);
            return visual;
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
            IReadOnlyList<ConsoleSlotVisual> slotVisuals)
        {
            if (slotViews.Count != runtimeConsole.SlotCount || slotVisuals.Count != runtimeConsole.SlotCount)
            {
                throw new InvalidOperationException("Trap Floor Console presentation does not match its Runtime Slot count.");
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
                }

                slotView.Bind(slot, slotVisual.LayoutAnchor, coordinateConverter, cardViews);
                consoleSlotViews.Add(slotView);
                consoleSlotVisualsByContainerId.Add(slot.Id, slotVisual);
                orderedSlots.Add(slotView);
            }

            targetConsole.Bind(runtimeConsole, targetConsole.LayoutAnchor, orderedSlots);
        }

        private CardView CreateCardView(
            CardInstanceState card,
            string label,
            out TabletopSelectionVisual selectionVisual)
        {
            CardVisualReferences createdVisualReferences = Instantiate(catalogCardPrefab);
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
                abilityTargetPresenter.Register(
                    card.BaseState.Id,
                    createdVisualReferences.FaceUpRenderer,
                    createdVisualReferences.FaceDownRenderer);
            }

            else if (CardSizeClassOf(card) == CardSizeClass.Large)
            {
                // Hero/avatar-size card: wider and longer (model and click box), never taller, so the slot
                // alone decides its height on the Console.
                clone.transform.localScale = new Vector3(largeCardVisualScale, 1f, largeCardVisualScale);
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

                Color playerColor = PlayerSeatColor(player.LayoutSeatIndex);
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
                ConfigureWorldLabel(label, $"P{playerNumber}", 0.16f, 64);
                ApplyLabelRendererHide(label, TableVisualHide.PawnOwnerLabel);
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

        private static Color PlayerSeatColor(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < PlayerColors.Length
                ? PlayerColors[playerIndex]
                : new Color(0.75f, 0.75f, 0.75f);
        }

        private static string PlayerColorName(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < PlayerColorNames.Length
                ? PlayerColorNames[playerIndex]
                : "GREY";
        }

        // The layout seat index of a participating player; it picks the player's colour.
        private bool TryGetTrapFloorSeatIndex(PlayerId playerId, out int seatIndex)
        {
            seatIndex = -1;
            if (playerId.IsEmpty || trapFloorTemplate == null || matchState == null) return false;
            for (int i = 0; i < trapFloorTemplate.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition player = trapFloorTemplate.Players[i];
                if (matchState.Seats.TryGetValue(player.SeatId, out SeatState seat) && seat.OccupantPlayerId == playerId)
                {
                    seatIndex = player.LayoutSeatIndex;
                    return true;
                }
            }

            return false;
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
            ConfigureWorldLabel(
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
            ConfigureWorldLabel(createdView.ResultLabel, createdView.ResultLabel.text, 0.18f, 64);
            ApplyLabelRendererHide(createdView.ResultLabel, TableVisualHide.DieResultLabel);
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
            FixedContainerVisual visual = Instantiate(catalogDeckPrefab);
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
                out ConsoleSlotVisual[] slotVisuals);

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
                out ConsoleSlotVisual[] slotVisuals);

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
            out ConsoleSlotVisual[] activeSlotVisuals)
        {
            if (orderedSlotViews.Length != requiredCount)
            {
                throw new InvalidOperationException(
                    $"The Console prefab has {orderedSlotViews.Length} authored Slots; its Console state needs {requiredCount}.");
            }

            activeSlotViews = new ConsoleSlotView[requiredCount];
            activeSlotVisuals = new ConsoleSlotVisual[requiredCount];
            for (int i = 0; i < requiredCount; i++)
            {
                activeSlotVisuals[i] = orderedSlotViews[i].GetComponent<ConsoleSlotVisual>();
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
            CardVisualReferences visualReferences,
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
            Texture2D frontArtwork = null;
            Texture2D backArtwork = null;
            // C3c: real cards look right on any table (a box tile on an Empty Table), not only in their game.
            if (TryGetCardLook(card.BaseState.DefinitionId, out CardDefinition authoredDefinition))
            {
                frontLabel = authoredDefinition.DisplayName;
                frontArtwork = FrontArt(authoredDefinition);
                backArtwork = BackArt(authoredDefinition);
            }
            else if (TryGetAvatarLook(card.BaseState.DefinitionId, out AvatarDefinition avatarDefinition))
            {
                frontLabel = avatarDefinition.DisplayName;
                frontArtwork = AvatarArt(avatarDefinition);
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
                    frontArtwork = null;
                    backArtwork = null;
                }
                else
                {
                    frontColor = TrapFloorContentColor(floorCard.Content.Category);
                    Texture2D contentFront = TryGetAuthoredCardDefinition(floorCard.Content.Id, out CardDefinition contentDefinition)
                        ? FrontArt(contentDefinition)
                        : null;
                    if (contentFront != null)
                    {
                        frontArtwork = contentFront;
                    }

                    backArtwork = CurrentBoardCardBack();
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

            // No art (including a hole) puts the plate back on its plain material.
            ApplyCardArtwork(visualReferences.FaceUpRenderer, frontArtwork);
            ApplyCardArtwork(visualReferences.FaceDownRenderer, backArtwork);
            ApplySuppliedModelArtwork(
                visualReferences,
                frontArtwork,
                backArtwork,
                frontArtwork != null ? Color.white : frontColor,
                backArtwork != null ? Color.white : backColor);

            // Art carries its own colours, so the tint stays white wherever art is shown.
            ApplyCardColor(
                visualReferences.FaceUpRenderer,
                frontArtwork != null ? Color.white : frontColor);
            ApplyCardColor(visualReferences.FaceDownRenderer, backArtwork != null ? Color.white : backColor);
            ConfigureWorldLabel(
                visualReferences.FrontLabel,
                frontLabel,
                isFloorCard
                    ? TrapFloorFloorLabelCharacterSize
                    : TrapFloorCardLabelCharacterSize,
                TrapFloorCardLabelFontSize);
            ConfigureWorldLabel(
                visualReferences.BackLabel,
                backLabel,
                TrapFloorCardBackLabelCharacterSize,
                TrapFloorCardLabelFontSize);
            visualReferences.SetBackLabelHidden(
                backLabelIsPlaceholder && HidesTableVisual(TableVisualHide.CardPlaceholderLabels));
            TableVisualHide faceLabelPart = backLabelIsPlaceholder
                ? TableVisualHide.CardFaceLabels
                : TableVisualHide.None; // collapsed floor card: HOLE (x,y) stays visible on both sides
            ApplyLabelRendererHide(visualReferences.FrontLabel, faceLabelPart);
            ApplyLabelRendererHide(visualReferences.BackLabel, faceLabelPart);
            if (frontArtwork != null)
            {
                SetLabelRendererVisible(visualReferences.FrontLabel, false);
            }

            if (backArtwork != null)
            {
                SetLabelRendererVisible(visualReferences.BackLabel, false);
            }
        }

        private static void SetLabelRendererVisible(TextMesh label, bool visible)
        {
            Renderer labelRenderer = label != null ? label.GetComponent<Renderer>() : null;
            if (labelRenderer != null)
            {
                labelRenderer.enabled = visible;
            }
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
            FixedContainerVisual visual = Instantiate(catalogStackPrefab);
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
    }
}
