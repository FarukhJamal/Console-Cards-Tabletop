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
using UnityEngine.EventSystems;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// TOOLS-1 (doc 30, Part H): the tool bar's tools, bottom right.
    /// <list type="bullet">
    /// <item><b>Grab</b>: the normal hand (drag, wheel, F, right-click, Shift-drag flick, Alt-click ping).</item>
    /// <item><b>Select</b> (or Ctrl held), as in Tabletop Simulator: every component can be selected except game
    /// boards and Consoles (and what sits on a Console, hands, floor tiles and locked pieces). A click on a piece adds
    /// it or takes it out (a click on a card in a deck, stack or discard pile picks the whole pile), a click on the
    /// table clears, a drag draws a box and selects everything inside. <b>Group into deck</b> (or G) gathers the
    /// selected cards and the cards of the selected piles into one deck: into the first selected deck if there is
    /// one, otherwise a new deck where the first selected card or pile lay; emptied piles go away. One Undo step.
    /// Pawns, tokens, dice and bags stay where they are.</item>
    /// <item><b>Ping</b>: a plain click pings (no Alt needed).</item>
    /// <item><b>Flick</b>: a plain press-pull-release flicks (no Shift needed).</item>
    /// <item><b>Draw</b>: the drawing tool (pen, highlighter, labels, eraser; TabletopComposition.Marks.cs). Greyed
    /// until its panel and line prefabs exist (Create Missing Gameplay UI Prefabs and Board Feedback Prefabs).</item>
    /// </list>
    /// The keys strip names the keys for what you are doing right now; Keys hides it (remembered), Hints off hides
    /// it too. Esc with nothing in progress clears the selection, then goes back to Grab. The selection is a view
    /// helper only: not table state, not saved, not in Undo. Nothing here allocates per frame: the texts are built
    /// once, the outlines are pooled and the lists are reused.
    /// </summary>
    public sealed partial class TabletopComposition : ITabletopSelectHandler
    {
        private const string KeysStripHiddenPref = "ConsoleCards.KeysStripHidden";
        private const string HandFanPref = "ConsoleCards.HandFan";
        private const float SelectBoxThresholdPixels = 8f;
        private static readonly Color SelectionColour = new Color(0.18f, 0.65f, 0.54f, 1f); // teal, like the box

        private TableToolKind activeTool = TableToolKind.Grab;
        private bool toolBarShown;
        private bool keysStripHidden;
        private bool keysStripPrefLoaded;
        private bool handFan = true;
        private bool toolsHadInteraction;

        // One selected component: a loose piece (card, pawn, token, die) or a pile (deck, stack, discard pile, bag).
        private struct ToolPick
        {
            public TabletopObjectView View;
            public ContainerId Pile;

            public bool IsPile => View == null;

            public bool Same(ToolPick other) => IsPile ? !other.IsPile && other.Pile == Pile : ReferenceEquals(View, other.View);
        }

        private readonly List<ToolPick> toolSelection = new List<ToolPick>();
        private readonly List<CardView> groupCards = new List<CardView>();
        private readonly List<ContainerId> groupEmptied = new List<ContainerId>();
        private readonly List<Vector3> toolSelectionPositions = new List<Vector3>();
        private readonly List<float> toolSelectionYaws = new List<float>();
        private int shownToolSelectionCount = -1;

        private bool selectPressActive;
        private bool selectBoxShown;
        private Vector2 selectPressScreen;
        private ToolPick selectPressPick;
        private bool selectPressHasPick;

        private string keysGrab;
        private string keysCarry;
        private string keysDie;
        private string keysSelect;
        private string keysSelected;
        private string keysPing;
        private string keysFlick;
        private string keysPile;

        internal TableToolKind ActiveTool => activeTool;

        // ---------- The bar ----------

        /// <summary>Shows the tool bar for this table (called with the rest of the table's UI).</summary>
        internal void RefreshToolBar()
        {
            if (runtimeUi == null) return;
            if (!keysStripPrefLoaded)
            {
                keysStripPrefLoaded = true;
                keysStripHidden = PlayerPrefs.GetInt(KeysStripHiddenPref, 0) == 1;
                handFan = PlayerPrefs.GetInt(HandFanPref, 1) == 1;
            }

            BuildToolKeyTexts();
            if (handView != null) handView.TrayFan = handFan;
            bool hasTray = handView != null && handView.IsTrayMode;
            toolBarShown = runtimeUi.ShowToolBar(
                ChooseTool,
                ToggleKeysStrip,
                GroupToolSelection,
                ClearToolSelection,
                hasTray ? ToggleHandStyle : (Action)null);
            if (!toolBarShown) return;
            runtimeUi.SetHandStyle(handFan);
            bool drawReady = DrawToolReady();
            runtimeUi.SetToolEnabled(TableToolKind.Draw, drawReady);
            if (!drawReady && activeTool == TableToolKind.Draw) activeTool = TableToolKind.Grab;
            RefreshDrawPanel();
            runtimeUi.SetActiveTool(activeTool);
            runtimeUi.SetToolKeysWanted(!keysStripHidden);
            shownToolSelectionCount = -1;
            RefreshToolBarState();
        }

        private void ChooseTool(TableToolKind kind)
        {
            if (kind == TableToolKind.Draw && !DrawToolReady())
            {
                ShowMessage("The Draw tool needs its panel and lines: run Console Cards > UI > Create Missing Gameplay UI "
                    + "Prefabs and Console Cards > Feedback > Create Missing Board Feedback Prefabs.");
                return;
            }

            if (activeTool == TableToolKind.Draw && kind != TableToolKind.Draw) CancelDraw();
            activeTool = kind;
            if (kind != TableToolKind.Select && !Keys.SelectModifierHeld) CancelSelectPress();
            if (kind == TableToolKind.Draw && toolSelection.Count > 0) ClearToolSelection(); // the panel sits where the selection buttons do
            if (toolBarShown) runtimeUi.SetActiveTool(kind);
            ShowDrawPanel(kind == TableToolKind.Draw);
            RefreshToolBarState();
        }

        // Owner, 2026-10-19: the hand tray as a fan or a straight row (remembered).
        private void ToggleHandStyle()
        {
            handFan = !handFan;
            PlayerPrefs.SetInt(HandFanPref, handFan ? 1 : 0);
            if (handView != null) handView.TrayFan = handFan;
            if (toolBarShown) runtimeUi.SetHandStyle(handFan);
        }

        private void ToggleKeysStrip()
        {
            keysStripHidden = !keysStripHidden;
            PlayerPrefs.SetInt(KeysStripHiddenPref, keysStripHidden ? 1 : 0);
            if (toolBarShown) runtimeUi.SetToolKeysWanted(!keysStripHidden);
        }

        // The texts are built once per table (the key names come from the Input Actions asset).
        private void BuildToolKeyTexts()
        {
            string ping = Keys.PingModifierName;
            string action = Keys.ActionModifierName;
            string select = Keys.SelectModifierName;
            string group = Keys.GroupSelectionName;
            string turn = Keys.RotateLeftName + "/" + Keys.RotateRightName;
            string roll = Keys.RollName;
            keysGrab = KeyName("Drag") + " move   " + KeyName(turn) + " turn   " + KeyName("F") + " flip   "
                + KeyName(roll) + " roll   " + KeyName("Right-click") + " menu   " + KeyName(action + "+drag") + " flick   "
                + KeyName(ping + "+click") + " ping   " + KeyName("Hold " + ping) + " peek   "
                + KeyName("Drag table") + " select   " + KeyName(select + "+click") + " add";
            keysCarry = KeyName("Wheel") + " raise   " + KeyName(turn) + " turn   " + KeyName("Esc") + " cancel   "
                + "drop on a pile to put it there";
            keysDie = KeyName("Hold " + action) + " or shake the mouse: shake   let go to roll   " + KeyName("Wheel") + " raise   "
                + KeyName("Esc") + " cancel";
            keysPile = KeyName("1–9") + " draw   " + KeyName(roll) + " shuffle   " + KeyName(Keys.DealName) + " deal   "
                + KeyName("Hold") + " carry the stack   "
                + KeyName("Hold " + ping) + " peek   " + KeyName("Right-click") + " menu";
            keysSelect = KeyName("Click") + " add or remove a piece   " + KeyName("Drag") + " box   "
                + KeyName("Click table") + " clear   " + KeyName("Esc") + " back to Grab";
            keysSelected = KeyName(group) + " group cards into a deck   " + KeyName("Drag one") + " move them all   "
                + KeyName("F") + " flip   " + KeyName(turn) + " turn   " + KeyName(Keys.DeleteName) + " delete   "
                + KeyName("Esc") + " clear";
            keysPing = KeyName("Click") + " ping   " + KeyName("Middle tap") + " ping   " + KeyName("Esc") + " back to Grab";
            keysFlick = KeyName("Press a piece, pull back, let go") + " flick   " + KeyName("Esc") + " back to Grab";
            keysDraw = KeyName("Drag") + " draw (Pen, Marker)   " + KeyName(Keys.ActionModifierName + "+drag") + " straight line   "
                + KeyName("STYLE") + " plain, brush, swirl, wavy, dashed, dotted   "
                + KeyName("Click") + " place a label (Label)   "
                + KeyName("Drag over a drawing") + " erase (Erase)   " + KeyName("Ctrl+Z") + " undo   "
                + KeyName("Esc") + " back to Grab";
        }

        private static string KeyName(string key) => "<b><color=#F2A81D>" + key + "</color></b>";

        // ---------- Every frame ----------

        private void UpdateTools()
        {
            if (!IsInitialized) return;
            bool interacting = interactionRouter != null && interactionRouter.HasActiveInteraction;
            if (!interacting && !toolsHadInteraction && !IsTypingInUi())
            {
                if (Keys.GroupSelectionPressedThisFrame && toolSelection.Count > 0) GroupToolSelection();
                else if (Keys.CancelPressedThisFrame) StepBackTool();
            }

            toolsHadInteraction = interacting;
            PruneToolSelection();
            DrawToolSelection();
            RefreshToolBarState();
        }

        // Esc: first the selection goes, then the tool goes back to Grab.
        private void StepBackTool()
        {
            if (componentPlacementController != null && componentPlacementController.IsActive) return;
            if (toolSelection.Count > 0)
            {
                ClearToolSelection();
                return;
            }

            if (activeTool != TableToolKind.Grab) ChooseTool(TableToolKind.Grab);
        }

        private void RefreshToolBarState()
        {
            if (!toolBarShown || runtimeUi == null) return;
            runtimeUi.SetSelectionCount(toolSelection.Count);
            runtimeUi.SetToolKeys(CurrentToolKeys());
        }

        private string CurrentToolKeys()
        {
            if (interactionRouter != null && interactionRouter.HasActiveInteraction)
            {
                if (interactionRouter.ActiveRoute == TabletopInteractionRoute.Select) return keysSelect;
                if (interactionRouter.ActiveRoute == TabletopInteractionRoute.Flick) return keysFlick;
                if (interactionRouter.ActiveRoute == TabletopInteractionRoute.Draw) return keysDraw;
                return moveCoordinator != null && moveCoordinator.ActiveView is DieView ? keysDie : keysCarry;
            }

            if (toolSelection.Count > 0) return keysSelected;
            switch (activeTool)
            {
                case TableToolKind.Select: return keysSelect;
                case TableToolKind.Ping: return keysPing;
                case TableToolKind.Flick: return keysFlick;
                case TableToolKind.Draw: return keysDraw;
                default:
                    if (Keys.SelectModifierHeld) return keysSelect;
                    return IsPointerOnDrawablePile() ? keysPile : keysGrab; // POL-4: over a deck or stack
            }
        }

        private static bool IsTypingInUi()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected == null) return false;
            UnityEngine.UI.InputField field = selected.GetComponent<UnityEngine.UI.InputField>();
            return field != null && field.isFocused;
        }

        // ---------- Selecting (ITabletopSelectHandler) ----------

        bool ITabletopSelectHandler.WantsSelect()
        {
            return IsInitialized
                && tableModeStart == null
                && (componentPlacementController == null || !componentPlacementController.IsActive)
                && (activeTool == TableToolKind.Select || Keys.SelectModifierHeld);
        }

        // Owner, 2026-10-19: as in Tabletop Simulator, a drag on the empty table draws a selection box with Grab too
        // (no need to switch tools). Not while Alt (ping) or Shift (flick) is held.
        bool ITabletopSelectHandler.WantsBoxOnEmptyTable()
        {
            return IsInitialized
                && tableModeStart == null
                && (componentPlacementController == null || !componentPlacementController.IsActive)
                && (activeTool == TableToolKind.Grab || activeTool == TableToolKind.Select)
                && !Keys.PingModifierHeld
                && !Keys.ActionModifierHeld;
        }

        bool ITabletopSelectHandler.TryBeginSelect(TabletopObjectView viewUnderPointer, Vector2 screenPosition)
        {
            selectPressHasPick = TryPickFromView(viewUnderPointer, out selectPressPick);
            selectPressScreen = screenPosition;
            selectPressActive = true;
            selectBoxShown = false;
            ContextMenus.CloseContextMenu();
            return true;
        }

        void ITabletopSelectHandler.UpdateSelect(Vector2 screenPosition)
        {
            if (!selectPressActive) return;
            if (!selectBoxShown
                && (screenPosition - selectPressScreen).sqrMagnitude < SelectBoxThresholdPixels * SelectBoxThresholdPixels)
            {
                return;
            }

            selectBoxShown = true;
            if (toolBarShown) runtimeUi.ShowSelectionBox(selectPressScreen, screenPosition);
        }

        void ITabletopSelectHandler.ReleaseSelect(Vector2 screenPosition)
        {
            if (!selectPressActive) return;
            bool box = selectBoxShown;
            bool hasPick = selectPressHasPick;
            ToolPick pick = selectPressPick;
            CancelSelectPress();
            if (box)
            {
                // With Grab a new box starts a new selection (Ctrl keeps adding); the Select tool always adds.
                if (activeTool != TableToolKind.Select && !Keys.SelectModifierHeld) toolSelection.Clear();
                SelectInBox(selectPressScreen, screenPosition);
            }
            else if (hasPick)
            {
                int index = IndexOfPick(pick);
                if (index >= 0) toolSelection.RemoveAt(index);
                else toolSelection.Add(pick);
                PlayCardSound(false);
            }
            else
            {
                toolSelection.Clear();
                selectionState?.ClearSelection(); // a click on the empty table clears everything, as before
            }

            DrawToolSelection();
            RefreshToolBarState();
        }

        void ITabletopSelectHandler.CancelSelect()
        {
            CancelSelectPress();
        }

        private void CancelSelectPress()
        {
            selectPressActive = false;
            selectBoxShown = false;
            selectPressHasPick = false;
            selectPressPick = default;
            if (toolBarShown && runtimeUi != null) runtimeUi.HideSelectionBox();
        }

        // What a click on this view selects: the piece itself when it lies loose, the whole pile when it is a card in a
        // selectable pile. Nothing for boards, Consoles and what sits on them, hands, floor tiles and locked pieces.
        private bool TryPickFromView(TabletopObjectView view, out ToolPick pick)
        {
            pick = default;
            if (view == null || !view.IsBound || view.BoundState == null) return false;
            if (IsSelectableLoosePiece(view))
            {
                pick.View = view;
                return true;
            }

            ContainerId containerId = view.BoundState.ContainerId;
            if (view is CardView && IsSelectablePile(containerId))
            {
                pick.Pile = containerId;
                return true;
            }

            return false;
        }

        private bool IsSelectableLoosePiece(TabletopObjectView view)
        {
            if (view == null || !view.IsBound || view.BoundState == null || !view.gameObject.activeInHierarchy) return false;
            TabletopObjectState state = view.BoundState;
            if (!state.ContainerId.IsEmpty || state.IsUserLocked) return false;
            if (view is CardView card)
            {
                return !card.IsOverlayPresentation
                    && !ActiveCartridge.IsFixedBoardPiece(state.Id); // a game's board tiles are the board
            }

            return view is PawnView || view is TokenView || view is DieView;
        }

        // Decks, stacks and discard piles lying on the table, and token bags. Not hands, Console slots or other
        // containers that belong to a Console or a board.
        private bool IsSelectablePile(ContainerId containerId)
        {
            if (containerId.IsEmpty
                || matchState == null
                || !matchState.Containers.TryGetValue(containerId, out ContainerState container)
                || !matchState.TryGetContainerPlacement(containerId, out _))
            {
                return false;
            }

            switch (container.Kind)
            {
                case ContainerKind.Deck:
                case ContainerKind.Stack:
                case ContainerKind.DiscardPile:
                    return true;
                default:
                    return IsTokenBag(containerId);
            }
        }

        private bool IsTokenBag(ContainerId containerId)
        {
            List<TokenContainerView> bags = Registry.tokenContainerViews;
            for (int i = 0; i < bags.Count; i++)
            {
                if (bags[i] != null && bags[i].IsBound && bags[i].ContainerId == containerId) return true;
            }

            return false;
        }

        private int IndexOfPick(ToolPick pick)
        {
            for (int i = 0; i < toolSelection.Count; i++)
            {
                if (toolSelection[i].Same(pick)) return i;
            }

            return -1;
        }

        private void AddPick(ToolPick pick)
        {
            if (IndexOfPick(pick) < 0) toolSelection.Add(pick);
        }

        private void SelectInBox(Vector2 screenA, Vector2 screenB)
        {
            UnityCamera camera = hitResolver != null ? hitResolver.TargetCamera : null;
            if (camera == null) return;
            Vector2 min = Vector2.Min(screenA, screenB);
            Vector2 max = Vector2.Max(screenA, screenB);
            BoxSelectViews(Registry.cardViews, camera, min, max);
            BoxSelectViews(Registry.pawnViews, camera, min, max);
            BoxSelectViews(Registry.tokenViews, camera, min, max);
            BoxSelectViews(Registry.dieViews, camera, min, max);
            foreach (KeyValuePair<ContainerId, ContainerState> pair in matchState.Containers)
            {
                if (!IsSelectablePile(pair.Key) || !TryGetPileWorldPose(pair.Key, out Vector3 position, out _)) continue;
                if (InScreenBox(camera, position, min, max)) AddPick(new ToolPick { Pile = pair.Key });
            }
        }

        private void BoxSelectViews<TView>(List<TView> views, UnityCamera camera, Vector2 min, Vector2 max)
            where TView : TabletopObjectView
        {
            for (int i = 0; i < views.Count; i++)
            {
                TView view = views[i];
                if (!IsSelectableLoosePiece(view)) continue;
                if (InScreenBox(camera, PieceRestPosition(view), min, max)) AddPick(new ToolPick { View = view });
            }
        }

        private static bool InScreenBox(UnityCamera camera, Vector3 world, Vector2 min, Vector2 max)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            return screen.z > 0f && screen.x >= min.x && screen.x <= max.x && screen.y >= min.y && screen.y <= max.y;
        }

        private static Vector3 PieceRestPosition(TabletopObjectView view)
        {
            return view.PhysicalObject != null && view.PhysicalObject.Body != null
                ? view.PhysicalObject.Body.position
                : view.transform.position;
        }

        private bool TryGetPileWorldPose(ContainerId pileId, out Vector3 position, out float yaw)
        {
            if (matchState != null && matchState.TryGetContainerPlacement(pileId, out ContainerPlacementState placement))
            {
                position = coordinateConverter.ToWorldPosition(placement.Pose);
                yaw = coordinateConverter.ToWorldRotation(placement.Pose).eulerAngles.y;
                return true;
            }

            position = Vector3.zero;
            yaw = 0f;
            return false;
        }

        // A selected piece that went into a pile or was locked, or a pile that was emptied away or rebuilt (Undo, a new
        // table), leaves the selection.
        private void PruneToolSelection()
        {
            for (int i = toolSelection.Count - 1; i >= 0; i--)
            {
                ToolPick pick = toolSelection[i];
                bool keep = pick.IsPile ? IsSelectablePile(pick.Pile) : IsSelectableLoosePiece(pick.View);
                if (!keep) toolSelection.RemoveAt(i);
            }
        }

        private void DrawToolSelection()
        {
            if (toolSelection.Count == 0)
            {
                if (shownToolSelectionCount != 0 && boardFeedback != null) boardFeedback.HideSelection();
                shownToolSelectionCount = 0;
                return;
            }

            if (!EnsureBoardFeedback()) return;
            toolSelectionPositions.Clear();
            toolSelectionYaws.Clear();
            toolSelectionSizes.Clear();
            for (int i = 0; i < toolSelection.Count; i++)
            {
                ToolPick pick = toolSelection[i];
                // On top of a card or pile, around the base of a pawn, token or die, sized to the piece.
                Vector3 position;
                float yaw;
                float size;
                bool found = pick.IsPile
                    ? TryPileOutline(pick.Pile, out position, out yaw, out size)
                    : TryOutlineOf(pick.View, out position, out yaw, out size);
                if (!found) continue;
                toolSelectionPositions.Add(position);
                toolSelectionYaws.Add(yaw);
                toolSelectionSizes.Add(size);
            }

            boardFeedback.ShowSelection(toolSelectionPositions, toolSelectionYaws, toolSelectionSizes, SelectionColour);
            shownToolSelectionCount = toolSelection.Count;
        }

        private void ClearToolSelection()
        {
            toolSelection.Clear();
            DrawToolSelection();
            RefreshToolBarState();
        }

        // ---------- Group into deck ----------

        // Gathers the selected loose cards and the cards of the selected decks, stacks and discard piles into one deck:
        // the first selected deck, or a new deck where the first selected card or pile lay. Piles left empty go away
        // (not the game's own piles). Pawns, tokens, dice and bags stay. One Undo step, then the landing tap and flash.
        private void GroupToolSelection()
        {
            PruneToolSelection();
            ContainerId deckId = ContainerId.Empty;
            bool hasPose = false;
            TabletopPose deckPose = TabletopPose.Default;
            int cardSources = 0;
            for (int i = 0; i < toolSelection.Count; i++)
            {
                ToolPick pick = toolSelection[i];
                if (pick.IsPile)
                {
                    if (!matchState.Containers.TryGetValue(pick.Pile, out ContainerState pile) || pile.Kind == ContainerKind.Generic
                        || IsTokenBag(pick.Pile))
                    {
                        continue;
                    }

                    cardSources++;
                    if (deckId.IsEmpty && pile.Kind == ContainerKind.Deck) deckId = pick.Pile;
                    if (!hasPose && matchState.TryGetContainerPlacement(pick.Pile, out ContainerPlacementState placement))
                    {
                        deckPose = placement.Pose;
                        hasPose = true;
                    }
                }
                else if (pick.View is CardView card)
                {
                    cardSources++;
                    if (!hasPose)
                    {
                        deckPose = RestPose(card);
                        hasPose = true;
                    }
                }
            }

            if (cardSources < 2 || !hasPose)
            {
                ShowMessage("Select two or more cards or piles to group them into a deck.");
                return;
            }

            // The cards to move, collected first: moving changes the piles.
            groupCards.Clear();
            groupEmptied.Clear();
            for (int i = 0; i < toolSelection.Count; i++)
            {
                ToolPick pick = toolSelection[i];
                if (!pick.IsPile)
                {
                    if (pick.View is CardView card) groupCards.Add(card);
                    continue;
                }

                if (pick.Pile == deckId
                    || IsTokenBag(pick.Pile)
                    || !matchState.Containers.TryGetValue(pick.Pile, out ContainerState pile)
                    || pile.Kind == ContainerKind.Generic)
                {
                    continue;
                }

                IReadOnlyList<TabletopObjectId> ids = pile.ObjectIds;
                for (int c = ids.Count - 1; c >= 0; c--) // top card first, so the pile keeps its order on top of the deck
                {
                    if (Registry.TryGetCardView(ids[c], out CardView contained)) groupCards.Add(contained);
                }

                groupEmptied.Add(pick.Pile);
            }

            int moved = 0;
            bool createdDeck = false;
            SessionService.BeginUndoBatch();
            try
            {
                if (deckId.IsEmpty)
                {
                    CreateTabletopComponentResult created = TableAuthority.Submit<CreateTabletopComponentRequest, CreateTabletopComponentResult>(
                        new CreateTabletopComponentRequest(
                            CreateCommandContext(),
                            TabletopComponentKind.Deck,
                            deckPose,
                            containerArrivalFace: ToolboxArrivalFace(TabletopComponentKind.Deck)));
                    if (!created.Succeeded)
                    {
                        ShowMessage($"Group rejected: {created.Error}.");
                        return;
                    }

                    deckId = created.ContainerId;
                    createdDeck = true;
                }

                // R2: a new deck got its view from the table's view sync (which also rebuilt the transfer coordinator).
                CardDropTarget target = CardDropTarget.ForContainer(deckId);
                for (int i = 0; i < groupCards.Count && transferCoordinator != null; i++)
                {
                    CardView card = groupCards[i];
                    if (card != null && card.IsBound && transferCoordinator.Transfer(card, target).Succeeded) moved++;
                }

                // Piles the group emptied go away, as in Tabletop Simulator; the game's own piles stay.
                for (int i = 0; i < groupEmptied.Count; i++)
                {
                    ContainerId emptiedId = groupEmptied[i];
                    if (matchState.IsTemplateContainer(emptiedId)
                        || !matchState.Containers.TryGetValue(emptiedId, out ContainerState emptied)
                        || emptied.Count != 0)
                    {
                        continue;
                    }

                    TableAuthority.Submit<DeleteTabletopComponentRequest, DeleteTabletopComponentResult>(
                        new DeleteTabletopComponentRequest(
                            CreateCommandContext(),
                            TabletopComponentTarget.ForContainer(emptiedId)));
                }

                if (createdDeck && moved == 0 && matchState.Containers.TryGetValue(deckId, out ContainerState empty) && empty.Count == 0)
                {
                    TableAuthority.Submit<DeleteTabletopComponentRequest, DeleteTabletopComponentResult>(
                        new DeleteTabletopComponentRequest(
                            CreateCommandContext(),
                            TabletopComponentTarget.ForContainer(deckId)));
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            int wanted = groupCards.Count;
            groupCards.Clear();
            groupEmptied.Clear();
            toolSelection.Clear();
            DrawToolSelection();
            RefreshToolBarState();
            if (moved == 0)
            {
                ShowMessage("Could not group those cards.");
                return;
            }

            if (matchState.Containers.TryGetValue(deckId, out ContainerState made))
            {
                watchedPileCounts[deckId] = made.Count; // the landing tap below is this deck's only sound
            }

            ScheduleLandingFeedback(deckId, true);
            ShowMessage(moved == wanted ? $"Grouped {moved} cards into a deck." : $"Grouped {moved} of {wanted} cards into a deck.");
        }

        // ---------- Reset ----------

        private void ClearTools()
        {
            CancelDraw(); // a line being drawn does not carry over to a rebuilt table (the drawings themselves stay)
            CancelSelectPress();
            toolSelection.Clear();
            shownToolSelectionCount = -1;
            if (boardFeedback != null) boardFeedback.HideSelection();
            toolsHadInteraction = false;
        }
    }
}
