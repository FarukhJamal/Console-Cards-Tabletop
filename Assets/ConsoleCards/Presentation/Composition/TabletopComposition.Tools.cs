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
using UnityEngine.EventSystems;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// TOOLS-1 (doc 30, Part H): the tool bar's tools, bottom right.
    /// <list type="bullet">
    /// <item><b>Grab</b>: the normal hand (drag, wheel, F, right-click, Shift-drag flick, Alt-click ping).</item>
    /// <item><b>Select</b> (or Ctrl held with Grab): a click on a loose card adds it to the selection or takes it out,
    /// a click on the table clears it, a drag draws a box and selects every loose card inside. With two or more
    /// cards selected, <b>Group into deck</b> (or G) makes one deck of them where the first one lay: one Undo step.
    /// Floor tiles and locked cards are never selected.</item>
    /// <item><b>Ping</b>: a plain click pings (no Alt needed).</item>
    /// <item><b>Flick</b>: a plain press-pull-release flicks (no Shift needed).</item>
    /// <item><b>Draw</b>: greyed until T5.</item>
    /// </list>
    /// The keys strip names the keys for what you are doing right now; Keys hides it (remembered), Hints off hides
    /// it too. Esc with nothing in progress clears the selection, then goes back to Grab. The selection is a view
    /// helper only: not table state, not saved, not in Undo. Nothing here allocates per frame: the texts are built
    /// once, the outlines are pooled and the lists are reused.
    /// </summary>
    public sealed partial class TabletopComposition : ITabletopSelectHandler
    {
        private const string KeysStripHiddenPref = "ConsoleCards.KeysStripHidden";
        private const float SelectBoxThresholdPixels = 8f;
        private static readonly Color SelectionColour = new Color(0.18f, 0.65f, 0.54f, 1f); // teal, like the box

        private TableToolKind activeTool = TableToolKind.Grab;
        private bool toolBarShown;
        private bool keysStripHidden;
        private bool keysStripPrefLoaded;
        private bool toolsHadInteraction;

        private readonly List<CardView> toolSelection = new List<CardView>();
        private readonly List<Vector3> toolSelectionPositions = new List<Vector3>();
        private readonly List<float> toolSelectionYaws = new List<float>();
        private int shownToolSelectionCount = -1;

        private bool selectPressActive;
        private bool selectBoxShown;
        private Vector2 selectPressScreen;
        private CardView selectPressCard;

        private string keysGrab;
        private string keysCarry;
        private string keysDie;
        private string keysSelect;
        private string keysSelected;
        private string keysPing;
        private string keysFlick;

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
            }

            BuildToolKeyTexts();
            toolBarShown = runtimeUi.ShowToolBar(ChooseTool, ToggleKeysStrip, GroupToolSelection, ClearToolSelection);
            if (!toolBarShown) return;
            runtimeUi.SetToolEnabled(TableToolKind.Draw, false); // T5
            runtimeUi.SetActiveTool(activeTool);
            runtimeUi.SetToolKeysWanted(!keysStripHidden);
            shownToolSelectionCount = -1;
            RefreshToolBarState();
        }

        private void ChooseTool(TableToolKind kind)
        {
            if (kind == TableToolKind.Draw) return; // T5
            activeTool = kind;
            if (kind != TableToolKind.Select && !Keys.SelectModifierHeld) CancelSelectPress();
            if (toolBarShown) runtimeUi.SetActiveTool(kind);
            RefreshToolBarState();
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
            keysGrab = KeyName("Drag") + " move   " + KeyName("Wheel") + " rotate   " + KeyName("F") + " flip   "
                + KeyName("Right-click") + " menu   " + KeyName(action + "+drag") + " flick   "
                + KeyName(ping + "+click") + " ping   " + KeyName(select + "+click") + " select";
            keysCarry = KeyName("Wheel") + " rotate   " + KeyName("Esc") + " cancel   drop on a pile to put it there";
            keysDie = KeyName("Hold " + action) + " shake   let go to roll   " + KeyName("Esc") + " cancel";
            keysSelect = KeyName("Click") + " add or remove a card   " + KeyName("Drag") + " box   "
                + KeyName("Click table") + " clear   " + KeyName("Esc") + " back to Grab";
            keysSelected = KeyName(group) + " group into deck   " + KeyName("Click") + " add or remove   "
                + KeyName("Drag") + " box   " + KeyName("Esc") + " clear";
            keysPing = KeyName("Click") + " ping   " + KeyName("Middle tap") + " ping   " + KeyName("Esc") + " back to Grab";
            keysFlick = KeyName("Press a piece, pull back, let go") + " flick   " + KeyName("Esc") + " back to Grab";
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
                return moveCoordinator != null && moveCoordinator.ActiveView is DieView ? keysDie : keysCarry;
            }

            if (toolSelection.Count > 0) return keysSelected;
            switch (activeTool)
            {
                case TableToolKind.Select: return keysSelect;
                case TableToolKind.Ping: return keysPing;
                case TableToolKind.Flick: return keysFlick;
                default: return Keys.SelectModifierHeld ? keysSelect : keysGrab;
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

        bool ITabletopSelectHandler.TryBeginSelect(TabletopObjectView viewUnderPointer, Vector2 screenPosition)
        {
            CardView card = viewUnderPointer as CardView;
            selectPressCard = IsStackableLooseCard(card) ? card : null;
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
            CardView card = selectPressCard;
            CancelSelectPress();
            if (box)
            {
                SelectCardsInBox(selectPressScreen, screenPosition);
            }
            else if (card != null)
            {
                int index = toolSelection.IndexOf(card);
                if (index >= 0) toolSelection.RemoveAt(index);
                else toolSelection.Add(card);
                PlayCardSound(false);
            }
            else
            {
                toolSelection.Clear();
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
            selectPressCard = null;
            if (toolBarShown && runtimeUi != null) runtimeUi.HideSelectionBox();
        }

        private void SelectCardsInBox(Vector2 screenA, Vector2 screenB)
        {
            UnityCamera camera = hitResolver != null ? hitResolver.TargetCamera : null;
            if (camera == null) return;
            Vector2 min = Vector2.Min(screenA, screenB);
            Vector2 max = Vector2.Max(screenA, screenB);
            List<CardView> cards = Registry.cardViews;
            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                if (card == null || !card.gameObject.activeInHierarchy || !IsStackableLooseCard(card)) continue;
                Vector3 screen = camera.WorldToScreenPoint(RestPosition(card));
                if (screen.z <= 0f || screen.x < min.x || screen.x > max.x || screen.y < min.y || screen.y > max.y) continue;
                if (!toolSelection.Contains(card)) toolSelection.Add(card);
            }
        }

        // A selected card that went into a pile, was locked or was rebuilt (Undo, a new table) leaves the selection.
        private void PruneToolSelection()
        {
            for (int i = toolSelection.Count - 1; i >= 0; i--)
            {
                CardView card = toolSelection[i];
                if (card == null || !card.gameObject.activeInHierarchy || !IsStackableLooseCard(card)) toolSelection.RemoveAt(i);
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
            for (int i = 0; i < toolSelection.Count; i++)
            {
                CardView card = toolSelection[i];
                toolSelectionPositions.Add(RestPosition(card));
                toolSelectionYaws.Add(card.transform.eulerAngles.y);
            }

            boardFeedback.ShowSelection(toolSelectionPositions, toolSelectionYaws, SelectionColour);
            shownToolSelectionCount = toolSelection.Count;
        }

        private void ClearToolSelection()
        {
            toolSelection.Clear();
            DrawToolSelection();
            RefreshToolBarState();
        }

        // ---------- Group into deck ----------

        // One deck where the first selected card lies; every selected card goes in, in the order they were picked.
        // One Undo step. The cards glide in (the transfer's own animation), then the landing tap and flash.
        private void GroupToolSelection()
        {
            PruneToolSelection();
            if (toolSelection.Count < 2)
            {
                ShowMessage("Select two or more loose cards to group them.");
                return;
            }

            TabletopPose deckPose = RestPose(toolSelection[0]);
            ContainerId deckId = ContainerId.Empty;
            int moved = 0;
            SessionService.BeginUndoBatch();
            try
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
                // R2: the deck got its view from the table's view sync (which also rebuilt the transfer coordinator).
                CardDropTarget target = CardDropTarget.ForContainer(deckId);
                for (int i = 0; i < toolSelection.Count && transferCoordinator != null; i++)
                {
                    if (transferCoordinator.Transfer(toolSelection[i], target).Succeeded) moved++;
                }

                if (moved == 0 && matchState.Containers.TryGetValue(deckId, out ContainerState empty) && empty.Count == 0)
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

            int selected = toolSelection.Count;
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
            ShowMessage(moved == selected ? $"Grouped {moved} cards into a deck." : $"Grouped {moved} of {selected} cards into a deck.");
        }

        // ---------- Reset ----------

        private void ClearTools()
        {
            CancelSelectPress();
            toolSelection.Clear();
            shownToolSelectionCount = -1;
            if (boardFeedback != null) boardFeedback.HideSelection();
            toolsHadInteraction = false;
        }
    }
}
