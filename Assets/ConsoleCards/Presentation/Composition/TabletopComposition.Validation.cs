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
    /// <summary>Checks on the scene and prefab references before the table starts. (R1a: moved here unchanged from TabletopComposition.cs.)</summary>
    public sealed partial class TabletopComposition
    {
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
            ValidateFiniteGreaterThanZero(largeCardVisualScale, nameof(largeCardVisualScale));
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
                    "TabletopComposition requires its component library (doc 22).");
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
                    "TabletopComposition requires its component library (doc 22).");
            }

            // The library lists its catalogs once validated; the Console layout can be asked for first.
            if (componentLibrary.Catalogs.Count == 0)
            {
                componentLibrary.Validate();
            }

            catalogCardPrefab = ResolveCatalogEntry(ComponentCatalogKind.Card)
                .GetPrefabComponent<CardVisualReferences>();
            catalogPawnPrefab = ResolveCatalogEntry(ComponentCatalogKind.Pawn).GetPrefabComponent<PawnView>();
            catalogTokenPrefab = ResolveCatalogEntry(ComponentCatalogKind.Token).GetPrefabComponent<TokenView>();
            catalogDiePrefab = ResolveCatalogEntry(ComponentCatalogKind.Die).GetPrefabComponent<DieView>();
            catalogConsolePrefab = ResolveCatalogEntry(ComponentCatalogKind.Console).GetPrefabComponent<ConsoleView>();
            catalogHandPrefab = ResolveCatalogEntry(ComponentCatalogKind.Hand)
                .GetPrefabComponent<FixedContainerVisual>();
            boardMatPrefab = FindBoardMatPrefab(componentLibrary); // C3e; null until the Board Mat is made
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

        private FixedContainerVisual ResolveCatalogPilePrefab(ComponentCatalogKind kind)
        {
            return ResolveCatalogEntry(kind).GetPrefabComponent<FixedContainerVisual>();
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
                throw new InvalidOperationException("TabletopComposition requires an initialized Camera input adapter.");
            }

            if (!objectInputAdapter.HasValidActionConfiguration)
            {
                throw new InvalidOperationException("TabletopComposition requires a valid Object input adapter action configuration.");
            }

            if (objectInputAdapter.IsInitialized)
            {
                throw new InvalidOperationException("TabletopComposition requires the Object input adapter to begin uninitialized.");
            }

            if (cameraInputAdapter.HasScrollRoutingPolicy)
            {
                throw new InvalidOperationException("TabletopComposition requires the Camera input adapter to begin without a scroll routing policy.");
            }

            if (inputFrameCoordinator.enabled)
            {
                throw new InvalidOperationException("TabletopComposition requires the input-frame coordinator to begin disabled.");
            }

            if (!inputFrameCoordinator.IsInitialized)
            {
                throw new InvalidOperationException("TabletopComposition requires an initialized input-frame coordinator.");
            }

            if (!ReferenceEquals(inputFrameCoordinator.CameraInputAdapter, cameraInputAdapter)
                || !ReferenceEquals(inputFrameCoordinator.ObjectInputAdapter, objectInputAdapter))
            {
                throw new InvalidOperationException("TabletopComposition requires the input-frame coordinator to reference the supplied adapters.");
            }

            if (cameraInputAdapter.IsExternallyDriven || objectInputAdapter.IsExternallyDriven)
            {
                throw new InvalidOperationException("TabletopComposition requires input adapters to begin without an external frame driver.");
            }

            if (inputFrameCoordinator.HasSelectionPresenter)
            {
                throw new InvalidOperationException("TabletopComposition requires the input-frame coordinator to begin without a selection presenter.");
            }
        }

        private void ValidateCardPrefabReferences()
        {
            if (catalogCardPrefab.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException(
                    "TabletopComposition requires catalogCardPrefab to reference a prefab asset.");
            }

            catalogCardPrefab.ValidateReferences();
        }

        private void ValidatePreInitializationState()
        {
            if (!cameraInputAdapter.IsInitialized)
            {
                throw new InvalidOperationException("TabletopComposition requires an initialized Camera input adapter.");
            }

            if (!objectInputAdapter.HasValidActionConfiguration)
            {
                throw new InvalidOperationException("TabletopComposition requires a valid Object input adapter action configuration.");
            }

            if (objectInputAdapter.IsInitialized)
            {
                throw new InvalidOperationException("TabletopComposition requires the Object input adapter to begin uninitialized.");
            }

            if (cameraInputAdapter.HasScrollRoutingPolicy)
            {
                throw new InvalidOperationException("TabletopComposition requires the Camera input adapter to begin without a scroll routing policy.");
            }

            if (inputFrameCoordinator.enabled)
            {
                throw new InvalidOperationException("TabletopComposition requires the input-frame coordinator to begin disabled.");
            }

            if (!inputFrameCoordinator.IsInitialized)
            {
                throw new InvalidOperationException("TabletopComposition requires an initialized input-frame coordinator.");
            }

            if (!ReferenceEquals(inputFrameCoordinator.CameraInputAdapter, cameraInputAdapter)
                || !ReferenceEquals(inputFrameCoordinator.ObjectInputAdapter, objectInputAdapter))
            {
                throw new InvalidOperationException("TabletopComposition requires the input-frame coordinator to reference the supplied adapters.");
            }

            if (cameraInputAdapter.IsExternallyDriven || objectInputAdapter.IsExternallyDriven)
            {
                throw new InvalidOperationException("TabletopComposition requires input adapters to begin without an external frame driver.");
            }

            if (inputFrameCoordinator.HasSelectionPresenter)
            {
                throw new InvalidOperationException("TabletopComposition requires the input-frame coordinator to begin without a selection presenter.");
            }
        }
    }
}
