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
    /// <summary>
    /// Pile tools (owner, 2026-10-18, as in Tabletop Simulator):
    /// <list type="bullet">
    /// <item>Duplicate a Deck, Stack or Discard Pile with all its cards (same cards, order and faces).</item>
    /// <item>Split a Deck or Stack into 2 to 6 piles: the cards are dealt into stacks beside it, the top part first;
    /// the pile itself keeps the bottom part.</item>
    /// <item>Card on card, landing taps and tidying small stacks: see TabletopComposition.Stacking.cs.</item>
    /// </list>
    /// Every change goes through the command door, as one Undo step.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const int MaximumSplitPiles = 6;
        private const double SplitPileSpacing = 1.4d;
        private int selectedSplitCount = 2;

        // ---------- Duplicate a pile with its cards ----------

        private static bool TryPileComponentKind(ContainerKind kind, out TabletopComponentKind componentKind)
        {
            switch (kind)
            {
                case ContainerKind.Deck:
                    componentKind = TabletopComponentKind.Deck;
                    return true;
                case ContainerKind.Stack:
                    componentKind = TabletopComponentKind.Stack;
                    return true;
                case ContainerKind.DiscardPile:
                    componentKind = TabletopComponentKind.DiscardPile;
                    return true;
                default:
                    componentKind = default;
                    return false;
            }
        }

        internal void AddPileDuplicateAction(List<PopupActionOption> actions, ContainerId pileId)
        {
            if (!matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                || !TryPileComponentKind(pile.Kind, out _))
            {
                return;
            }

            actions.Add(new PopupActionOption(
                pile.Count > 0 ? $"Duplicate ({pile.Count} {(pile.Count == 1 ? "card" : "cards")})" : "Duplicate",
                true,
                () => BeginPileDuplicatePlacement(pileId)));
        }

        private void BeginPileDuplicatePlacement(ContainerId sourceId)
        {
            EnsureInitialized();
            if (componentPlacementController == null)
            {
                throw new InvalidOperationException("Component placement is not configured.");
            }

            if (!matchState.Containers.TryGetValue(sourceId, out ContainerState source)
                || !TryPileComponentKind(source.Kind, out TabletopComponentKind componentKind)
                || !matchState.TryGetContainerPlacement(sourceId, out ContainerPlacementState placement))
            {
                ShowMessage("Duplicate rejected: SourcePileInvalid.");
                ContextMenus.CloseContextMenu();
                return;
            }

            componentPlacementController.Cancel();
            GameObject previewRoot = CreateToolboxPlacementPreview(componentKind, 0);
            ContextMenus.CloseContextMenu();
            componentPlacementController.BeginCustomComponentPlacement(
                componentKind,
                previewRoot,
                placement.Pose.RotationDegrees,
                0,
                toolboxSpawnSequence * ToolboxPhysicalOrderStride,
                pose => CommitPileDuplicate(sourceId, pose));
            ShowPlacementHint($"Duplicate {ToolboxComponentLabel(componentKind)}");
            KeepToolboxPlacing(false);
        }

        private bool CommitPileDuplicate(ContainerId sourceId, TabletopPose requestedPose)
        {
            CreateTabletopComponentResult result = TableAuthority.Submit<DuplicatePileRequest, CreateTabletopComponentResult>(
                new DuplicatePileRequest(CreateCommandContext(), sourceId, requestedPose));
            if (!result.Succeeded)
            {
                ShowMessage($"Duplicate rejected: {result.Error}.");
                return false;
            }

            toolboxSpawnSequence++;
            // R2: the pile and its cards got their views from the table's view sync.
            ShowMessage(result.CardIds.Count > 0
                ? $"Duplicated the {ToolboxComponentLabel(result.ComponentKind)} with its {result.CardIds.Count} cards."
                : $"Duplicated the {ToolboxComponentLabel(result.ComponentKind)}.");
            return true;
        }

        // ---------- Split into N piles ----------

        internal bool CanSplitIntoPiles(ContainerId pileId)
        {
            return matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                && (pile.Kind == ContainerKind.Deck || pile.Kind == ContainerKind.Stack)
                && pile.Count >= 2
                && !ActiveCartridge.IsAssistedControllerDeck(pileId);
        }

        internal void AddSplitIntoPilesAction(List<PopupActionOption> actions, ContainerId pileId)
        {
            if (!CanSplitIntoPiles(pileId)) return;
            actions.Add(new PopupActionOption(
                "Split into...",
                true,
                () =>
                {
                    selectedSplitCount = 2;
                    ContextMenus.SetContextMenuMode(ContextMenuMode.SplitPile);
                }));
        }

        private int MaximumSplitCount(ContainerId pileId)
        {
            return matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                ? Math.Max(2, Math.Min(MaximumSplitPiles, pile.Count))
                : 2;
        }

        internal void ShowSplitPilePopup()
        {
            int maximum = MaximumSplitCount(contextMenuContainerId);
            selectedSplitCount = Mathf.Clamp(selectedSplitCount, 2, maximum);
            runtimeUi.ShowQuantityPopup(
                "SPLIT INTO PILES",
                "The cards are dealt into this many piles beside it, the top part first.",
                "Split",
                selectedSplitCount,
                2,
                maximum,
                () => ChangeSplitCount(-1),
                () => ChangeSplitCount(1),
                ConfirmSplitPile,
                ContextMenus.CloseContextMenu);
        }

        private void ChangeSplitCount(int delta)
        {
            int maximum = MaximumSplitCount(contextMenuContainerId);
            selectedSplitCount = Mathf.Clamp(selectedSplitCount + delta, 2, maximum);
            runtimeUi?.SetQuantityPopupValue(selectedSplitCount, 2, maximum);
        }

        private void ConfirmSplitPile()
        {
            ContainerId pileId = contextMenuContainerId;
            int piles = selectedSplitCount;
            ContextMenus.CloseContextMenu();
            SplitIntoPiles(pileId, piles);
        }

        // N-1 splits, top part first, in one Undo step: pile k gets the k-th share of the cards (the first shares
        // one card bigger when they do not divide evenly); the source keeps share 0, the bottom of the pile.
        private void SplitIntoPiles(ContainerId sourceId, int piles)
        {
            EnsureInitialized();
            if (!CanSplitIntoPiles(sourceId)
                || !matchState.TryGetContainerPlacement(sourceId, out ContainerPlacementState placement))
            {
                ShowMessage("Split unavailable.");
                return;
            }

            ContainerState source = matchState.GetContainer(sourceId);
            int count = source.Count;
            piles = Mathf.Clamp(piles, 2, Math.Min(MaximumSplitPiles, count));
            int share = count / piles;
            int extra = count % piles;
            int[] starts = new int[piles];
            for (int k = 1; k < piles; k++)
            {
                starts[k] = starts[k - 1] + share + (k - 1 < extra ? 1 : 0);
            }

            TabletopPose sourcePose = placement.Pose;
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> transitionStarts =
                CaptureContainerCardTransforms(sourceId);
            List<StackRuntimeView> made = new List<StackRuntimeView>(piles - 1);
            SplitStackError failure = SplitStackError.None;
            SessionService.BeginUndoBatch();
            try
            {
                for (int k = piles - 1; k >= 1; k--)
                {
                    ContainerId newStackId = Spawner.CreateDeterministicDynamicStackId(dynamicStackSequence++);
                    while (matchState.Containers.ContainsKey(newStackId) || matchState.ContainerPlacements.ContainsKey(newStackId))
                    {
                        newStackId = Spawner.CreateDeterministicDynamicStackId(dynamicStackSequence++);
                    }

                    TabletopPose newPose = new TabletopPose(
                        new TableCoordinate(sourcePose.Position.X + (k * SplitPileSpacing), sourcePose.Position.Y),
                        sourcePose.RotationDegrees,
                        sourcePose.Layer,
                        sourcePose.LocalOrder);
                    SplitStackResult result = TableAuthority.Submit<SplitStackCommand, SplitStackResult>(
                        new SplitStackCommand(
                            CreateCommandContext(),
                            sourceId,
                            newStackId,
                            new StackSplitSpecification(starts[k]),
                            newPose));
                    if (!result.Succeeded)
                    {
                        failure = result.Error;
                        break;
                    }

                    // R2: the table's view sync made the new pile's view and laid it and the source out.
                    made.Add(Registry.stackViewsByContainerId[newStackId]);
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            AnimatePileCards(transitionStarts, true); // SMOOTH-1: card by card, the top shares first
            for (int i = 0; i < made.Count; i++)
            {
                presentationTransitions.Appear(made[i].Root.transform, Feel.PileMoveSeconds);
            }

            for (int i = 0; i < made.Count; i++)
            {
                if (made[i].View.ContainerState != null && made[i].View.ContainerState.Count == 1)
                {
                    WatchStackForCollapse(made[i].View.ContainerId); // a single card goes back to being a card
                }
            }

            RefreshCardContentVisibility();
            ShowMessage(failure == SplitStackError.None
                ? $"Split into {piles} piles."
                : $"Split stopped after {made.Count + 1} piles: {failure}.");
        }
    }
}
