using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Saving;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// RULES-SHEET (R4; "Console Cards — Game Rules and Templates", assist levels): the referee of a game a player
    /// made. It reads the game's rules sheet and follows every accepted move: whose turn it is (a turn is a run of
    /// moves by one player; when someone else moves, the turn has passed), cards drawn and played this turn (from the
    /// mover's hand size going up or down), actions this turn, and the hand size, checked at the end of a turn or at any
    /// time. Free: silent. Reminders: a referee line on the table news. Full: a warning to the player who moved (and
    /// the news line on the other computers). Every computer replays the same moves, so each works it out alike and
    /// nothing extra is sent. It warns; it does not refuse moves (refusing needs the host's command gate, which is
    /// Trap Floor's only today).
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private readonly Dictionary<PlayerId, int> sheetHandCounts = new Dictionary<PlayerId, int>();
        private readonly List<SeatState> sheetSeats = new List<SeatState>(8);
        private PlayerId sheetTurnPlayer;
        private int sheetDraws;
        private int sheetPlays;
        private int sheetActions;
        private string sheetGameId = string.Empty;

        internal void FollowRulesSheet(AuthoritativeActionAcceptance acceptance)
        {
            PlayerGameFile game = PlayerGamesPanel.activePlayerGame;
            if (game == null || TableFlipQuiet || PlayAssist.IsFree || !RulesSheet.HasContent(game.sheet, game.sheetText)) return;
            if (!string.Equals(sheetGameId, game.id, StringComparison.Ordinal)) ResetRulesSheetReferee(game.id);
            PlayerId actor = acceptance.ActorPlayerId;
            if (actor.IsEmpty || !TryGetSheetHand(actor, out int hand)) return;

            if (actor != sheetTurnPlayer)
            {
                if (!sheetTurnPlayer.IsEmpty) CheckSheetHandAtTurnEnd(game, sheetTurnPlayer);
                CheckSheetTurnOrder(game, actor);
                sheetTurnPlayer = actor;
                sheetDraws = 0;
                sheetPlays = 0;
                sheetActions = 0;
            }

            int before = sheetHandCounts.TryGetValue(actor, out int known) ? known : hand;
            sheetHandCounts[actor] = hand;
            int change = hand - before;
            if (acceptance.Kind == AuthoritativeActionKind.DrawCards) sheetDraws += Mathf.Max(1, change);
            else if (change > 0) sheetDraws += change; // taken into the hand another way
            else if (change < 0) sheetPlays -= change; // left the hand: played (or discarded)
            if (acceptance.RecordMode == AuthoritativeActionRecordMode.Transaction) sheetActions++;

            string name = SheetPlayerName(actor);
            if (OverSheetLimit(game, "sheet.limit.draws", sheetDraws, change > 0 || acceptance.Kind == AuthoritativeActionKind.DrawCards, out int draws))
            {
                SheetSay(actor, $"{name} has drawn {sheetDraws} card(s) this turn; the rules allow {draws}.");
            }

            if (OverSheetLimit(game, "sheet.limit.plays", sheetPlays, change < 0, out int plays))
            {
                SheetSay(actor, $"{name} has played {sheetPlays} card(s) this turn; the rules allow {plays}.");
            }

            if (OverSheetLimit(game, "sheet.limit.actions", sheetActions, acceptance.RecordMode == AuthoritativeActionRecordMode.Transaction,
                    out int actions))
            {
                SheetSay(actor, $"{name} has taken {sheetActions} actions this turn; the rules allow {actions}.");
            }

            if (change > 0 && string.Equals(RulesSheet.ValueOf(game.sheet, "sheet.limit.hand.check"), "at any time", StringComparison.Ordinal)
                && OverSheetLimit(game, "sheet.limit.hand", hand, true, out int most))
            {
                SheetSay(actor, $"{name} holds {hand} cards; the rules allow {most}: discard down.");
            }
        }

        private void ResetRulesSheetReferee(string gameId)
        {
            sheetGameId = gameId ?? string.Empty;
            sheetHandCounts.Clear();
            sheetTurnPlayer = PlayerId.Empty;
            sheetDraws = 0;
            sheetPlays = 0;
            sheetActions = 0;
        }

        // True only on the move that first passes the limit (so each limit is said once a turn).
        private static bool OverSheetLimit(PlayerGameFile game, string key, int count, bool grew, out int limit)
        {
            limit = 0;
            if (!grew || !int.TryParse(RulesSheet.ValueOf(game.sheet, key), out limit)) return false;
            return count == limit + 1;
        }

        private void CheckSheetHandAtTurnEnd(PlayerGameFile game, PlayerId player)
        {
            if (string.Equals(RulesSheet.ValueOf(game.sheet, "sheet.limit.hand.check"), "at any time", StringComparison.Ordinal)) return;
            if (!int.TryParse(RulesSheet.ValueOf(game.sheet, "sheet.limit.hand"), out int most) || !TryGetSheetHand(player, out int hand)) return;
            if (hand > most) SheetSay(player, $"{SheetPlayerName(player)} ended the turn with {hand} cards; the rules allow {most}: discard down.");
        }

        // Clockwise or counter-clockwise round the table (seen from above), from the player who moved last.
        private void CheckSheetTurnOrder(PlayerGameFile game, PlayerId actor)
        {
            string order = RulesSheet.ValueOf(game.sheet, "sheet.turn.order");
            bool clockwise = string.Equals(order, "clockwise", StringComparison.Ordinal);
            if (sheetTurnPlayer.IsEmpty || (!clockwise && !string.Equals(order, "counter-clockwise", StringComparison.Ordinal))) return;
            sheetSeats.Clear();
            foreach (SeatState seat in matchState.Seats.Values)
            {
                if (!seat.OccupantPlayerId.IsEmpty) sheetSeats.Add(seat);
            }

            if (sheetSeats.Count < 3) return; // with two players every other mover is the next one
            sheetSeats.Sort(CompareSeatAngle);
            int at = -1;
            for (int i = 0; i < sheetSeats.Count; i++)
            {
                if (sheetSeats[i].OccupantPlayerId == sheetTurnPlayer) at = i;
            }

            if (at < 0) return;
            int next = clockwise ? (at + 1) % sheetSeats.Count : (at - 1 + sheetSeats.Count) % sheetSeats.Count;
            PlayerId expected = sheetSeats[next].OccupantPlayerId;
            if (expected != actor) SheetSay(actor, $"Turns go {order}: it is {SheetPlayerName(expected)}'s turn, not {SheetPlayerName(actor)}'s.");
        }

        // Clockwise seen from above: the angle round the table's middle, going down.
        private static int CompareSeatAngle(SeatState a, SeatState b)
        {
            double angleA = Math.Atan2(a.TablePose.Position.Y, a.TablePose.Position.X);
            double angleB = Math.Atan2(b.TablePose.Position.Y, b.TablePose.Position.X);
            return angleB.CompareTo(angleA);
        }

        private bool TryGetSheetHand(PlayerId player, out int count)
        {
            count = 0;
            // A made game's draws go to this screen's hand (DrawTargetHandFor), not a seat's: the mover played here.
            if (IsPlayerOnThisComputer(player) && matchState.Containers.TryGetValue(handContainerId, out ContainerState mine))
            {
                count = mine.Count;
                return true;
            }

            foreach (SeatState seat in matchState.Seats.Values)
            {
                if (seat.OccupantPlayerId != player) continue;
                if (!matchState.Containers.TryGetValue(seat.HandContainerId, out ContainerState hand)) return false;
                count = hand.Count;
                return true;
            }

            return false;
        }

        private string SheetPlayerName(PlayerId player)
        {
            string name = HasOnline ? Online.NameOf(player) : null;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            int number = 0;
            foreach (SeatState seat in matchState.Seats.Values)
            {
                number++;
                if (seat.OccupantPlayerId == player) return "Player " + number;
            }

            return "A player";
        }

        // Reminders: the referee line. Full: a warning to the player who moved, the line on the other computers.
        private void SheetSay(PlayerId actor, string text)
        {
            if (PlayAssist.IsQuiet)
            {
                Whisper(text);
                return;
            }

            if (IsPlayerOnThisComputer(actor)) ShowNotice(text);
            else runtimeUi?.ShowTableNews("Referee: " + text, WhisperColour);
        }
    }
}
