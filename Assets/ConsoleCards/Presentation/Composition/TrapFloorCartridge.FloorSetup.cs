using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Stage 2 (owner, 2026-11-17: "set up the floor yourselves"). With the rule "Tiles are placed by the players"
    /// (rulebook › Setup, <see cref="RuleSettingKeys.FloorPlacement"/>), Full and Reminders hand the floor to the players
    /// in the Buy Phase before round 1 of every floor: the dealing computer (offline, or the host) gathers every floor
    /// tile face down into one deck beside the board; the players shuffle it and lay every tile face down on a space of
    /// the board; then Floor set up — Done (turn menu, or a floor tile's menu) checks that every space holds exactly one
    /// face-down tile, sets the floor to that layout on every computer (<see cref="TrapFloorArrangeFloorCommand"/>),
    /// puts each tile square on its space and locks it. The Buy Phase cannot end before that. Online only the host
    /// finishes it; offline anyone. Free already leaves the whole floor to the players (FreeFloor.cs).
    /// Event-time only: nothing here runs per frame.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        /// <summary>The rule in play: the players lay the floor out themselves (Full and Reminders).</summary>
        internal bool FloorPlacedByPlayers()
        {
            if (PlayAssist.IsFree || host.trapFloorTemplate == null) return false;
            if (FloorByPlayersChoice.HasValue) return FloorByPlayersChoice.Value; // TILES-1: the game sheet's choice
            return RuleFloorByPlayers(host.RulesCardPanel.ActiveRuleSet, host.trapFloorTemplate.ActiveMode.StableId);
        }

        // TILES-1: what the rule set says, apart from the game sheet's choice (a null mode: the first entry).
        internal static bool RuleFloorByPlayers(RuleSetDefinition rules, string modeStableId)
        {
            if (rules == null) return false;
            for (int s = 0; s < rules.Sections.Count; s++)
            {
                RuleSection section = rules.Sections[s];
                if (section == null) continue;
                for (int e = 0; e < section.Entries.Count; e++)
                {
                    RuleEntry entry = section.Entries[e];
                    if (entry != null
                        && string.Equals(entry.SettingKey, RuleSettingKeys.FloorPlacement, StringComparison.Ordinal)
                        && entry.AppliesToMode(modeStableId))
                    {
                        return RuleSettingKeys.IsFloorPlacedByPlayers(entry.Value);
                    }
                }
            }

            return false;
        }

        /// <summary>The players are laying the floor out (Full and Reminders): its tiles move like any card.</summary>
        internal bool IsFloorSetupOpen =>
            !PlayAssist.IsFree && host.trapFloorTurnState != null && host.trapFloorTurnState.IsFloorSetupPending;

        /// <summary>This computer may finish the floor setup: offline any, online the host.</summary>
        internal bool CanFinishFloorSetup() => IsFloorSetupOpen && IsDealingComputer;

        // DealBuyPhase (the dealing computer, inside the step that opened the Buy Phase): hands the floor to the players
        // once, before round 1, when the rule asks for it and no tile has been turned or taken yet.
        private void BeginFloorSetupIfChosen(TrapFloorTurnState turn)
        {
            if (turn.IsFloorSetupPending
                || turn.FloorArrangement.Count > 0
                || !FloorPlacedByPlayers()
                || host.trapFloorCollapseState == null
                || host.trapFloorCollapseState.CollapsedFloorCount > 0)
            {
                return;
            }

            List<CardView> tiles = new List<CardView>();
            foreach (TabletopObjectId tileId in host.trapFloorTemplate.FloorCardIds.Values)
            {
                if (!host.matchState.Cards.TryGetValue(tileId, out CardInstanceState tile)) return;
                if (tile.Face != CardFace.FaceDown || !tile.BaseState.ContainerId.IsEmpty) return; // play has begun
                if (host.Registry.TryGetCardView(tileId, out CardView view) && view != null && view.IsBound) tiles.Add(view);
            }

            if (tiles.Count != host.trapFloorTemplate.FloorCardIds.Count) return;
            turn.BeginFloorSetup();
            UnlockFloorTiles();
            host.GroupCardsIntoDeck(tiles);
            MoveFloorDeckBesideBoard();
            Debug.Log($"[Floor] Setup by the players: {tiles.Count} floor tiles gathered into one deck.");
            // The setup flag is game state outside the command door: the joined computers get it with the whole table.
            if (host.HasOnline) host.Online.MarkDirty();
            host.FeedbackLine(
                $"SET UP THE FLOOR — shuffle the floor deck beside the board and lay all {tiles.Count} tiles face down on the "
                    + "board, one on each space. Then turn menu › Floor set up — Done.",
                FeedbackSeverity.Info);
        }

        // The gathered deck goes beside the board (left of column 1, middle row), so every space is free to lay a tile on.
        private void MoveFloorDeckBesideBoard()
        {
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            ContainerId deckId = ContainerId.Empty;
            foreach (TabletopObjectId tileId in template.FloorCardIds.Values)
            {
                if (host.matchState.Cards.TryGetValue(tileId, out CardInstanceState tile) && !tile.BaseState.ContainerId.IsEmpty)
                {
                    deckId = tile.BaseState.ContainerId;
                    break;
                }
            }

            int row = Math.Max(1, (template.GridRows + 1) / 2);
            if (deckId.IsEmpty || !template.TryGetHomeCellPose(new TrapFloorCoordinate(1, row), out TabletopPose edge)) return;
            TabletopPose beside = new TabletopPose(
                new TableCoordinate(edge.Position.X - (1.6d * template.Grid.ColumnPitch), edge.Position.Y),
                edge.RotationDegrees,
                edge.Layer,
                0);
            MoveContainerResult moved = host.TableAuthority.Submit<MoveContainerCommand, MoveContainerResult>(
                new MoveContainerCommand(host.CreateCommandContext(), deckId, beside));
            if (!moved.Succeeded) Debug.LogWarning($"[Floor] The floor deck stayed on the board: {moved.Error}.");
        }

        // The turn menu and a floor tile's menu while the players lay the floor out.
        internal void AddFloorSetupActions(List<PopupActionOption> actions)
        {
            if (!IsFloorSetupOpen) return;
            bool here = CanFinishFloorSetup();
            actions.Insert(0, new PopupActionOption(
                here ? "Floor set up — Done" : "Waiting for the host to finish the floor",
                here,
                FinishFloorSetup));
        }

        /// <summary>The status line while the floor waits for the players.</summary>
        internal string FloorSetupStatusText() =>
            "SET UP THE FLOOR — lay every floor tile face down on a space, then turn menu › Floor set up — Done";

        // Done: every space holds one face-down tile; the floor becomes that layout on every computer, each tile is put
        // square on its space and locked (one Undo step).
        internal void FinishFloorSetup()
        {
            host.ContextMenus.CloseContextMenu();
            if (!CanFinishFloorSetup()) return;
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;
            int cells = template.CellCount;
            TabletopObjectId[] byCell = new TabletopObjectId[cells];
            TabletopPose[] homes = new TabletopPose[cells];
            for (int i = 0; i < cells; i++)
            {
                if (!template.TryGetHomeCellPose(template.CellAt(i), out homes[i]))
                {
                    host.ShowMessage("The floor cannot be set up: a space of the board was not found.");
                    return;
                }
            }

            int notOnTable = 0;
            int faceUp = 0;
            int offBoard = 0;
            int doubled = 0;
            double halfWidth = template.Grid.CellWidth * 0.5d;
            double halfHeight = template.Grid.CellHeight * 0.5d;
            foreach (TabletopObjectId tileId in template.FloorCardIds.Values)
            {
                if (!host.matchState.Cards.TryGetValue(tileId, out CardInstanceState tile) || !tile.BaseState.ContainerId.IsEmpty)
                {
                    notOnTable++;
                    continue;
                }

                if (tile.Face != CardFace.FaceDown) faceUp++;
                TableCoordinate at = TilePosition(tile.BaseState);
                int found = -1;
                double nearest = double.MaxValue;
                for (int i = 0; i < cells; i++)
                {
                    double dx = at.X - homes[i].Position.X;
                    double dy = at.Y - homes[i].Position.Y;
                    double distance = (dx * dx) + (dy * dy);
                    if (Math.Abs(dx) <= halfWidth && Math.Abs(dy) <= halfHeight && distance < nearest)
                    {
                        nearest = distance;
                        found = i;
                    }
                }

                if (found < 0) offBoard++;
                else if (!byCell[found].IsEmpty) doubled++;
                else byCell[found] = tileId;
            }

            int empty = 0;
            for (int i = 0; i < cells; i++)
                if (byCell[i].IsEmpty) empty++;
            if (notOnTable + faceUp + offBoard + doubled + empty > 0)
            {
                List<string> problems = new List<string>(5);
                if (notOnTable > 0) problems.Add($"{notOnTable} still in a deck or a hand");
                if (offBoard > 0) problems.Add($"{offBoard} off the board");
                if (doubled > 0) problems.Add($"{doubled} on a space that already has one");
                if (faceUp > 0) problems.Add($"{faceUp} face up (turn them face down)");
                if (empty > 0) problems.Add($"{empty} space{(empty == 1 ? " is" : "s are")} empty");
                string text = "The floor cannot start yet: " + string.Join("; ", problems) + ".";
                host.FeedbackLine(text, FeedbackSeverity.Warning);
                host.ShowMessage(text);
                return;
            }

            int locked = 0;
            host.SessionService.BeginUndoBatch();
            try
            {
                TrapFloorTurnAdvanceResult result = host.TableAuthority.Submit<TrapFloorArrangeFloorCommand, TrapFloorTurnAdvanceResult>(
                    new TrapFloorArrangeFloorCommand(host.CreateCommandContext(), byCell));
                if (!result.Succeeded)
                {
                    host.ShowMessage($"The floor setup was refused (it cannot be used): {result.Error}.");
                    return;
                }

                for (int i = 0; i < cells; i++)
                {
                    if (!host.Registry.TryGetCardView(byCell[i], out CardView view) || view == null || view.BoundState == null) continue;
                    TabletopObjectState state = view.BoundState;
                    TabletopPose square = new TabletopPose(homes[i].Position, homes[i].RotationDegrees, state.Pose.Layer, state.Pose.LocalOrder);
                    if (!host.MoveLoosePiece(view, state, square))
                        Debug.LogWarning($"[Floor] Setup: tile {i + 1} could not be put square on {template.CellAt(i)}.");
                    CommandResult lockResult = host.TableAuthority.Submit<SetObjectLockedCommand, CommandResult>(
                        new SetObjectLockedCommand(host.CreateCommandContext(), byCell[i], true));
                    if (lockResult.Succeeded) locked++;
                }
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            Debug.Log($"[Floor] Setup by the players done: {cells} tiles laid out, {locked} locked.");
            host.FeedbackLine(
                $"THE FLOOR IS SET — {cells} tiles, as the players laid them out. Finish buying (Done Buying) to start round 1.",
                FeedbackSeverity.Gain);
            RefreshTrapFloorStatusUi();
        }

        // Where a tile lies now (its physics place first, as for pawns).
        private TableCoordinate TilePosition(TabletopObjectState tile)
        {
            PhysicalObjectState physical = tile.PhysicalState;
            if (physical != null && host.coordinateConverter != null)
            {
                return host.coordinateConverter.ToTableCoordinate(
                    new Vector3(physical.Position.X, physical.Position.Y, physical.Position.Z));
            }

            return tile.Pose.Position;
        }

        // The floor set up on another computer (the host): say so here too.
        private void OnFloorArranged(TrapFloorArrangeFloorCommand command, TrapFloorTurnAdvanceResult result)
        {
            if (!result.Succeeded || !host.ViewSync.Ready || !host.Replicated.Replaying) return;
            host.FeedbackLine("THE FLOOR IS SET — as the players laid it out.", FeedbackSeverity.Gain);
            RefreshTrapFloorStatusUi();
        }
    }
}
