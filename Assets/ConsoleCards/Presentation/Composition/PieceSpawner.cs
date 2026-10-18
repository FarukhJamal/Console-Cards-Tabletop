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
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Makes the pieces seen on the table from the catalog prefabs: cards, pawns, tokens, dice, decks, Consoles, token trays, stacks and the local hand plate.
    /// (R1c: moved out of TabletopComposition; the table reaches it as <c>Spawner</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class PieceSpawner
    {
        private readonly TabletopComposition host;

        internal PieceSpawner(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.Views.cs ----------


        // The local Hand from the catalog Hand entry, at the pose the scene Hand had (it lives in the camera tray;
        // the table plate stays hidden and is never a drop target).
        internal FixedContainerVisual CreateLocalHandVisual()
        {
            FixedContainerVisual visual = UnityEngine.Object.Instantiate(host.catalogHandPrefab);
            GameObject root = PrepareRuntimeRoot(visual.gameObject, "Local Hand");
            root.transform.SetPositionAndRotation(LocalHandRestPosition, Quaternion.identity);
            visual.ValidateReferences();
            host.ApplyFixedContainerVisualHide(visual, true);
            return visual;
        }

        internal CardView CreateCardView(
            CardInstanceState card,
            string label,
            out TabletopSelectionVisual selectionVisual)
        {
            CardVisualReferences createdVisualReferences = UnityEngine.Object.Instantiate(host.catalogCardPrefab);
            GameObject clone = createdVisualReferences.gameObject;
            if (clone.scene != host.gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(clone, host.gameObject.scene);
            }

            clone.name = $"Card {label}";
            RuntimeCardInstance runtimeCardInstance = new RuntimeCardInstance(clone);
            host.Registry.runtimeCardInstances.Add(runtimeCardInstance);
            createdVisualReferences.ValidateReferences();
            CardView createdView = createdVisualReferences.CardView;
            selectionVisual = createdVisualReferences.SelectionVisual;
            runtimeCardInstance.SetReferences(createdView, selectionVisual, createdVisualReferences);
            selectionVisual.SetSelected(false);
            host.ApplySelectionHighlightHide(selectionVisual);
            ConfigureCardVisuals(createdVisualReferences, card, label);
            createdView.Bind(card, host.coordinateConverter);
            if (host.trapFloorTemplate != null && host.trapFloorTemplate.IsFloorCard(card.BaseState.Id))
            {
                clone.transform.localScale = Vector3.one * host.floorCardVisualScale;
                host.abilityTargetPresenter.Register(
                    card.BaseState.Id,
                    createdVisualReferences.FaceUpRenderer,
                    createdVisualReferences.FaceDownRenderer);
            }

            else if (host.CardSizeClassOf(card) == CardSizeClass.Large)
            {
                // Hero/avatar-size card: wider and longer (model and click box), never taller, so the slot
                // alone decides its height on the Console.
                clone.transform.localScale = new Vector3(host.largeCardVisualScale, 1f, host.largeCardVisualScale);
            }

            host.Registry.cardVisualReferences.Add(createdVisualReferences);
            return createdView;
        }

        internal PawnView CreatePawnView(
            PawnState pawn,
            out TabletopSelectionVisual selectionVisual)
        {
            PawnView createdView = UnityEngine.Object.Instantiate(host.catalogPawnPrefab);
            GameObject root = PrepareRuntimeRoot(createdView.gameObject, "Trap Floor Pawn");
            selectionVisual = createdView.GetComponent<TabletopSelectionVisual>();
            ValidateRuntimeSelectionVisual(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            host.ApplySelectionHighlightHide(selectionVisual);
            createdView.Bind(pawn, host.coordinateConverter);
            host.Registry.runtimePawnInstances.Add(new RuntimeObjectInstance(root, createdView, selectionVisual));
            return createdView;
        }

        internal void ConfigureOfficialPawnPresentation()
        {
            ClearOfficialPawnPresentation();
            for (int playerIndex = 0; playerIndex < host.trapFloorTemplate.Players.Count; playerIndex++)
            {
                TrapFloorPlayerSetupDefinition player = host.trapFloorTemplate.Players[playerIndex];
                int playerNumber = player.LayoutSeatIndex + 1;
                PawnView officialView = null;
                for (int viewIndex = 0; viewIndex < host.Registry.pawnViews.Count; viewIndex++)
                {
                    PawnView candidate = host.Registry.pawnViews[viewIndex];
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
                host.ApplyLabelRendererHide(label, TableVisualHide.PawnOwnerLabel);
                officialPawnLabels.Add(labelRoot);
            }
        }

        internal void ClearOfficialPawnPresentation()
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
                host.DestroyRuntimeOwnedGameObject(labelRoot);
            }
        }

        internal TokenView CreateTokenView(
            TokenState token,
            out TabletopSelectionVisual selectionVisual,
            float visualScale)
        {
            TokenView createdView = UnityEngine.Object.Instantiate(host.catalogTokenPrefab);
            GameObject root = PrepareRuntimeRoot(createdView.gameObject, "Token");
            selectionVisual = createdView.GetComponent<TabletopSelectionVisual>();
            ValidateRuntimeSelectionVisual(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            host.ApplySelectionHighlightHide(selectionVisual);
            createdView.Bind(token, host.coordinateConverter);
            createdView.transform.localScale = Vector3.one * visualScale;
            host.Registry.runtimeTokenInstances.Add(new RuntimeObjectInstance(root, createdView, selectionVisual));
            return createdView;
        }

        internal RuntimeTokenContainerInstance CreateTokenContainerInstance(
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
            root.layer = host.catalogTokenPrefab.gameObject.layer;
            TokenContainerView view = root.AddComponent<TokenContainerView>();
            TabletopTokenContainerDropTarget dropTarget =
                root.AddComponent<TabletopTokenContainerDropTarget>();

            GameObject boundary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (boundary.scene != host.gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(boundary, host.gameObject.scene);
            }

            boundary.name = "Coin Area";
            boundary.layer = root.layer;
            boundary.transform.SetParent(root.transform, false);
            boundary.transform.localPosition = new Vector3(0f, 0.0125f, 0f);
            boundary.transform.localScale = new Vector3(width, 0.025f, depth);
            Renderer renderer = boundary.GetComponent<Renderer>();
            renderer.sharedMaterial = host.catalogHandPrefab.FeedbackRenderer.sharedMaterial;
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

            host.ApplyAuthoredPose(root.transform, pose);
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

        internal DieView CreateDieView(
            DieState die,
            string name,
            out TabletopSelectionVisual selectionVisual)
        {
            DieView createdView = UnityEngine.Object.Instantiate(host.catalogDiePrefab);
            GameObject root = PrepareRuntimeRoot(createdView.gameObject, name);
            ConfigureWorldLabel(createdView.ResultLabel, createdView.ResultLabel.text, 0.18f, 64);
            host.ApplyLabelRendererHide(createdView.ResultLabel, TableVisualHide.DieResultLabel);
            selectionVisual = createdView.GetComponent<TabletopSelectionVisual>();
            ValidateRuntimeSelectionVisual(createdView, selectionVisual);
            selectionVisual.SetSelected(false);
            host.ApplySelectionHighlightHide(selectionVisual);
            createdView.Bind(die, host.coordinateConverter);
            host.Registry.runtimeDieInstances.Add(new RuntimeObjectInstance(root, createdView, selectionVisual));
            return createdView;
        }

        internal RuntimeDeckInstance CreateRuntimeDeckInstance(
            string name,
            string displayLabel,
            ContainerId containerId,
            bool labelIsDecoration)
        {
            FixedContainerVisual visual = UnityEngine.Object.Instantiate(host.catalogDeckPrefab);
            GameObject root = PrepareRuntimeRoot(visual.gameObject, name);
            visual.ValidateReferences();
            DeckView view = visual.GetView<DeckView>();
            ConfigureContainerLabel(visual.Label, displayLabel);
            visual.DropTarget.ClearConfiguration();
            visual.DropTarget.enabled = false;
            visual.TargetCollider.enabled = false;
            visual.ClearFeedback();
            host.ApplyFixedContainerVisualHide(visual, labelIsDecoration);
            return new RuntimeDeckInstance(root, visual, view, containerId);
        }

        internal RuntimeConsoleInstance CreateRuntimeConsoleInstance(
            string name,
            int layoutSeatIndex,
            SeatId seatId)
        {
            ConsoleView view = UnityEngine.Object.Instantiate(host.catalogConsolePrefab);
            GameObject root = PrepareRuntimeRoot(view.gameObject, name);
            if (!host.playerLayout.TryGetSeat(layoutSeatIndex, out _))
            {
                throw new InvalidOperationException("Trap Floor Console references a missing Player Layout Seat.");
            }

            SeatState seat = host.matchState.GetSeat(seatId);
            host.ApplyConsolePose(
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

        internal RuntimeConsoleInstance CreateRuntimeConsoleInstance(
            string name,
            PlacedConsoleState placedConsole)
        {
            ConsoleView view = UnityEngine.Object.Instantiate(host.catalogConsolePrefab);
            GameObject root = PrepareRuntimeRoot(view.gameObject, name);
            host.ApplyConsolePose(root.transform, placedConsole.Pose, placedConsole.SurfaceHeight);
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
        internal static void SelectConsoleSlots(
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

        internal GameObject PrepareRuntimeRoot(GameObject root, string name)
        {
            if (root.scene != host.gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(root, host.gameObject.scene);
            }

            root.name = name;
            return root;
        }

        internal static void ValidateRuntimeSelectionVisual(
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

        internal void ConfigureCardVisuals(
            CardVisualReferences visualReferences,
            CardInstanceState card,
            string label)
        {
            visualReferences.AlignFaceLabelsToSurface(host.tabletopLocalOrderHeight);
            bool isButtonCard = host.IsButtonCard(card);
            bool isFloorCard = host.trapFloorTemplate != null
                && host.trapFloorTemplate.IsFloorCard(card.BaseState.Id);
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
            if (host.TryGetCardLook(card.BaseState.DefinitionId, out CardDefinition authoredDefinition))
            {
                frontLabel = authoredDefinition.DisplayName;
                frontArtwork = host.FrontArt(authoredDefinition);
                backArtwork = host.BackArt(authoredDefinition);
            }
            else if (host.TryGetAvatarLook(card.BaseState.DefinitionId, out AvatarDefinition avatarDefinition))
            {
                frontLabel = avatarDefinition.DisplayName;
                frontArtwork = host.AvatarArt(avatarDefinition);
            }
            if (isFloorCard
                && host.trapFloorTemplate.TryGetFloorCardState(
                    host.matchState,
                    card.BaseState.Id,
                    out TrapFloorFloorCardState floorCard))
            {
                if (host.trapFloorCollapseState != null
                    && host.trapFloorCollapseState.IsCollapsed(card.BaseState.Id))
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
                    Texture2D contentFront = host.TryGetAuthoredCardDefinition(floorCard.Content.Id, out CardDefinition contentDefinition)
                        ? host.FrontArt(contentDefinition)
                        : null;
                    if (contentFront != null)
                    {
                        frontArtwork = contentFront;
                    }

                    backArtwork = host.CurrentBoardCardBack();
                    if (floorCard.Content.Category == TrapFloorFloorContentCategory.Key
                        && host.trapFloorObjectiveState != null)
                    {
                        bool isClaimed = host.trapFloorObjectiveState.TryGetClaim(card.BaseState.Id, out _);
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
            host.ApplyCardArtwork(visualReferences.FaceUpRenderer, frontArtwork);
            host.ApplyCardArtwork(visualReferences.FaceDownRenderer, backArtwork);
            host.ApplySuppliedModelArtwork(
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
                backLabelIsPlaceholder && host.HidesTableVisual(TableVisualHide.CardPlaceholderLabels));
            TableVisualHide faceLabelPart = backLabelIsPlaceholder
                ? TableVisualHide.CardFaceLabels
                : TableVisualHide.None; // collapsed floor card: HOLE (x,y) stays visible on both sides
            host.ApplyLabelRendererHide(visualReferences.FrontLabel, faceLabelPart);
            host.ApplyLabelRendererHide(visualReferences.BackLabel, faceLabelPart);
            if (frontArtwork != null)
            {
                SetLabelRendererVisible(visualReferences.FrontLabel, false);
            }

            if (backArtwork != null)
            {
                SetLabelRendererVisible(visualReferences.BackLabel, false);
            }
        }

        internal static void SetLabelRendererVisible(TextMesh label, bool visible)
        {
            Renderer labelRenderer = label != null ? label.GetComponent<Renderer>() : null;
            if (labelRenderer != null)
            {
                labelRenderer.enabled = visible;
            }
        }

        // Every Stack (template, Toolbox or Split) is the catalog's Stack pile in its bay; pileStyle null uses
        // the kind default (no mark).
        internal StackRuntimeView CreateStackRuntimeView(
            string name,
            ContainerState container,
            ContainerPlacementState placement,
            bool labelIsDecoration,
            GameTemplatePileStyle pileStyle = null)
        {
            FixedContainerVisual visual = UnityEngine.Object.Instantiate(host.catalogStackPrefab);
            GameObject root = visual.gameObject;
            if (root.scene != host.gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(root, host.gameObject.scene);
            }

            root.name = name;
            host.Registry.runtimeOwnedStackRoots.Add(root);

            visual.ValidateReferences();
            StackView view = visual.GetView<StackView>();
            ConfigureContainerLabel(visual.Label, name);
            GameTemplatePileStyle style = pileStyle ?? GameTemplatePileStyle.DefaultFor(container.Kind);
            view.ConfigureTableRest(true, style.MaximumPileHeight);

            view.Bind(container, placement, visual.LayoutAnchor, host.coordinateConverter, host.Registry.cardViews);
            StackRuntimeView stackRuntimeView = new StackRuntimeView(
                StackViewOwnership.RuntimeOwned,
                root,
                visual,
                view,
                container,
                placement,
                visual.DropTarget);
            host.ConfigureFixedContainer(visual, view);
            host.ApplyFixedContainerVisualHide(visual, labelIsDecoration);
            host.ConfigurePileBay(visual, style, container.OwnerSeatId);
            return stackRuntimeView;
        }

        internal ContainerId CreateDeterministicDynamicStackId(int sequence)
        {
            byte[] bytes = host.localSeatId.Value.ToByteArray();
            bytes[0] = (byte)(bytes[0] ^ 0x5a);
            bytes[1] = (byte)(bytes[1] ^ sequence);
            bytes[2] = (byte)(bytes[2] ^ (sequence >> 8));
            return new ContainerId(new Guid(bytes));
        }
        // ---------- from TabletopComposition.cs ----------

        internal const float TrapFloorFloorLabelCharacterSize = 0.16f;
        internal static readonly Vector3 LocalHandRestPosition = new Vector3(0f, 0f, -1.86f);
        internal readonly List<GameObject> officialPawnLabels = new List<GameObject>();
        internal readonly List<Renderer> officialPawnRenderers = new List<Renderer>();
    }
}
