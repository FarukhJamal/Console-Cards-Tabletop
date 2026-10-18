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
    /// Every view on the table and how to find one: the lists of card, pawn, token, die, pile, Console and stack views, and the lookups by ID.
    /// (R1c: moved out of TabletopComposition; the table reaches it as <c>Registry</c> and it
    /// reaches the table through <c>host</c>.)
    /// </summary>
    internal sealed class ViewRegistry
    {
        private readonly TabletopComposition host;

        internal ViewRegistry(TabletopComposition host)
        {
            this.host = host;
        }

        // ---------- from TabletopComposition.Piles.cs ----------


        internal bool TryGetDeckPresentation(
            ContainerId containerId,
            out DeckView resolvedView,
            out FixedContainerVisual resolvedVisual)
        {
            for (int i = 0; i < runtimeDeckInstances.Count; i++)
            {
                RuntimeDeckInstance instance = runtimeDeckInstances[i];
                if (instance.ContainerId == containerId
                    && instance.View != null
                    && instance.View.IsBound
                    && instance.Visual != null)
                {
                    resolvedView = instance.View;
                    resolvedVisual = instance.Visual;
                    return true;
                }
            }

            resolvedView = null;
            resolvedVisual = null;
            return false;
        }
        // ---------- from TabletopComposition.Toolbox.cs ----------


        internal bool TryGetRuntimeDiscardPile(ContainerId containerId, out RuntimeDiscardPileInstance instance)
        {
            for (int i = 0; i < runtimeDiscardPileInstances.Count; i++)
            {
                if (runtimeDiscardPileInstances[i].ContainerId == containerId
                    && runtimeDiscardPileInstances[i].View != null)
                {
                    instance = runtimeDiscardPileInstances[i];
                    return true;
                }
            }

            instance = null;
            return false;
        }
        // ---------- from TabletopComposition.Views.cs ----------

        internal bool TryGetCardView(TabletopObjectId cardId, out CardView resolvedView)
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

        internal bool TryGetDieView(TabletopObjectId dieId, out DieView resolvedView)
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

        internal bool TryGetPawnView(TabletopObjectId pawnId, out PawnView resolvedView)
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

        internal bool TryGetTokenView(TabletopObjectId tokenId, out TokenView resolvedView)
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

        internal bool TryGetPlacedConsoleBySlot(ContainerId slotContainerId, out ConsoleId consoleId)
        {
            foreach (KeyValuePair<ConsoleId, PlacedConsoleState> pair in host.matchState.PlacedConsoles)
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

        internal bool TryGetRuntimeConsole(
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
        // ---------- from TabletopComposition.cs ----------


        internal readonly List<RuntimeCardInstance> runtimeCardInstances = new List<RuntimeCardInstance>();
        internal readonly List<RuntimeObjectInstance> runtimePawnInstances = new List<RuntimeObjectInstance>();
        internal readonly List<RuntimeObjectInstance> runtimeTokenInstances = new List<RuntimeObjectInstance>();
        internal readonly List<RuntimeObjectInstance> runtimeDieInstances = new List<RuntimeObjectInstance>();
        internal readonly List<RuntimeDeckInstance> runtimeDeckInstances = new List<RuntimeDeckInstance>();
        internal readonly List<RuntimeDiscardPileInstance> runtimeDiscardPileInstances =
            new List<RuntimeDiscardPileInstance>();
        internal readonly List<RuntimeConsoleInstance> runtimeConsoleInstances = new List<RuntimeConsoleInstance>();
        internal readonly List<RuntimeTokenContainerInstance> runtimeTokenContainerInstances =
            new List<RuntimeTokenContainerInstance>();
        internal readonly List<GameObject> runtimeOwnedStackRoots = new List<GameObject>();
        internal readonly List<CardView> cardViews = new List<CardView>();
        internal readonly List<CardVisualReferences> cardVisualReferences =
            new List<CardVisualReferences>();
        internal readonly List<TabletopSelectionVisual> cardSelectionVisuals = new List<TabletopSelectionVisual>();
        internal readonly List<TabletopSelectionVisual> pawnSelectionVisuals = new List<TabletopSelectionVisual>();
        internal readonly List<TabletopSelectionVisual> tokenSelectionVisuals = new List<TabletopSelectionVisual>();
        internal readonly List<TabletopSelectionVisual> dieSelectionVisuals = new List<TabletopSelectionVisual>();
        internal readonly List<PawnView> pawnViews = new List<PawnView>();
        internal readonly List<TokenView> tokenViews = new List<TokenView>();
        internal readonly List<TokenContainerView> tokenContainerViews = new List<TokenContainerView>();
        internal readonly List<DieView> dieViews = new List<DieView>();
        internal readonly List<DeckView> controllerDeckViews = new List<DeckView>();
        internal readonly List<ConsoleView> playerConsoleViews = new List<ConsoleView>();
        internal readonly List<IContainerLayoutView> layoutViews = new List<IContainerLayoutView>();
        internal readonly Dictionary<ContainerId, StackRuntimeView> stackViewsByContainerId =
            new Dictionary<ContainerId, StackRuntimeView>();
        internal readonly Dictionary<ContainerId, ConsoleSlotVisual> consoleSlotVisualsByContainerId =
            new Dictionary<ContainerId, ConsoleSlotVisual>();
        // Other seats' hands as face-down piles on their hand zones; rebuilt with each session.
        internal readonly List<HiddenHandView> hiddenHandViews = new List<HiddenHandView>();
        internal readonly List<ConsoleSlotView> consoleSlotViews = new List<ConsoleSlotView>();
    }
}
