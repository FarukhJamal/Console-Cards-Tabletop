using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.GameTemplates.Privacy
{
    // PRIVACY-SCENE-2 (s147, doc 10 §56). The public table template a seed-free client builds its tabletop from: which
    // seat sits where, which containers belong to each seat, and the board's grid and playmat size. Every value is
    // public structure (the same for every recipient); it names no hidden card, no deck order and no seed. Development
    // path only: nothing in the live game sends it.

    /// <summary>One seat as every recipient may know it: its place in the layout and the containers that belong to it.</summary>
    public sealed class RecipientSeatLayout
    {
        public int LayoutSeatIndex;
        public SeatId Seat;
        public ContainerId Hand;
        public ContainerId ControllerDeck;
        public ContainerId ActionArea;
        /// <summary>PRIVACY-P6C: the seat's Console slots that take payment cards (every slot but Main; public).</summary>
        public List<ContainerId> SideSlots = new List<ContainerId>();
        /// <summary>The seat's pawn (pawns are public pieces), or empty.</summary>
        public TabletopObjectId Pawn;
    }

    /// <summary>The game's board as every recipient may know it: its play area, grid and playmat size (table units).</summary>
    public sealed class RecipientBoardLayout
    {
        public PlayAreaId CentralPlayArea;
        /// <summary>The board grid's dimensions (rows, columns, cell size, spacing). Null: the game has no board grid.</summary>
        public GridDefinitionData Grid;
        public int RemovedSpaces;
        public float MatWidth;
        public float MatDepth;
    }

    public sealed class RecipientPublicLayout
    {
        public List<RecipientSeatLayout> Seats = new List<RecipientSeatLayout>();
        public RecipientBoardLayout Board;

        /// <summary>
        /// Checks the layout against the table the recipient received: every seat, container and play area it names
        /// is in that table, and the only piece it names is a seat's pawn, which the recipient may identify. Null when
        /// it is consistent, else what is wrong.
        /// </summary>
        public static string Check(RecipientPublicLayout layout, MatchState received)
        {
            if (layout == null) return "no layout";
            if (received == null) return "no table";
            HashSet<int> indexes = new HashSet<int>();
            foreach (RecipientSeatLayout seat in layout.Seats)
            {
                if (seat == null) return "an empty seat entry";
                if (!indexes.Add(seat.LayoutSeatIndex)) return $"layout seat {seat.LayoutSeatIndex} is listed twice";
                if (!received.Seats.ContainsKey(seat.Seat)) return $"layout seat {seat.LayoutSeatIndex}: the seat is not in the table";
                if (!seat.Hand.IsEmpty && !received.Containers.ContainsKey(seat.Hand)) return $"layout seat {seat.LayoutSeatIndex}: no hand";
                if (!seat.ControllerDeck.IsEmpty && !received.Containers.ContainsKey(seat.ControllerDeck)) return $"layout seat {seat.LayoutSeatIndex}: no deck";
                if (!seat.ActionArea.IsEmpty && !received.Containers.ContainsKey(seat.ActionArea)) return $"layout seat {seat.LayoutSeatIndex}: no action area";
                if (!seat.Pawn.IsEmpty)
                {
                    if (!received.Pawns.ContainsKey(seat.Pawn)) return $"layout seat {seat.LayoutSeatIndex}: the pawn is not a public piece of the table";
                }
            }

            if (layout.Board != null && !layout.Board.CentralPlayArea.IsEmpty && !received.PlayAreas.ContainsKey(layout.Board.CentralPlayArea))
                return "the board's play area is not in the table";
            return null;
        }
    }

    /// <summary>
    /// PRIVACY-SCENE-2 (s147): a deterministic development table state for checking a recipient's scene. Applied to a
    /// copy of the host's table only (never to a live table and never on a client); it moves cards with the table's
    /// own container transfers and turns cards over directly. It is fixture data, not a game rule: no Trap Floor rule
    /// runs and nothing is decided at random. Same table in, same table out.
    /// </summary>
    public static class RecipientViewFixture
    {
        public sealed class Result
        {
            public int HandCards;
            public int ConsoleFaceUp;
            public int ConsoleFaceDown;
            public TabletopObjectId LooseFaceUp;
            public TabletopObjectId LooseFaceDown;
            public TabletopObjectId RevealedTile;
            public int UnrevealedTiles;
            public readonly List<string> Notes = new List<string>();
        }

        /// <param name="handCards">How many cards each seat's hand receives from the top of its Controller deck.</param>
        /// <param name="floorTiles">The board's tiles; the face-down one with the smallest id is turned face up.</param>
        /// <param name="consoleSlots">Which two slots of each Console get the face-up and the face-down card (null: the last two).</param>
        public static Result Apply(MatchState match, IReadOnlyList<RecipientSeatLayout> seats, IEnumerable<TabletopObjectId> floorTiles, int handCards = 3, IReadOnlyList<int> consoleSlots = null)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            Result result = new Result();
            ContainerTransferService transfer = new ContainerTransferService();

            bool TakeTop(ContainerId deckId, out CardInstanceState card)
            {
                card = null;
                if (deckId.IsEmpty || !match.Containers.TryGetValue(deckId, out ContainerState deck) || deck.Count == 0) return false;
                return match.Cards.TryGetValue(deck.GetObjectAt(deck.TopIndex), out card);
            }

            bool MoveTop(ContainerId deckId, ContainerId toId, CardFace face)
            {
                if (!TakeTop(deckId, out CardInstanceState card) || !match.Containers.TryGetValue(toId, out ContainerState to)) return false;
                if (!transfer.MoveBetweenContainers(card.BaseState, match.GetContainer(deckId), to).Succeeded) return false;
                card.SetFace(face);
                return true;
            }

            foreach (RecipientSeatLayout seat in seats)
            {
                for (int i = 0; i < handCards; i++)
                    if (MoveTop(seat.ControllerDeck, seat.Hand, CardFace.FaceUp)) result.HandCards++;

                if (match.TryGetSeatConsole(seat.Seat, out ConsoleState console) && console.SlotCount >= 2)
                {
                    // An occupied slot with one public card, and one with a face-down card (hidden from everyone, its
                    // owner too: owner decision 1, s143).
                    int up = consoleSlots != null && consoleSlots.Count >= 2 ? consoleSlots[0] : console.SlotCount - 1;
                    int down = consoleSlots != null && consoleSlots.Count >= 2 ? consoleSlots[1] : console.SlotCount - 2;
                    if (up < 0 || down < 0 || up >= console.SlotCount || down >= console.SlotCount || up == down)
                        throw new ArgumentOutOfRangeException(nameof(consoleSlots));
                    if (MoveTop(seat.ControllerDeck, console.SlotContainerIds[up], CardFace.FaceUp)) result.ConsoleFaceUp++;
                    if (MoveTop(seat.ControllerDeck, console.SlotContainerIds[down], CardFace.FaceDown)) result.ConsoleFaceDown++;
                }
                else
                {
                    result.Notes.Add($"seat {seat.LayoutSeatIndex + 1}: no Console with two slots");
                }
            }

            // Two loose cards on the table next to the first seat's deck: one face up (public), one face down (hidden).
            if (seats.Count > 0 && match.ContainerPlacements.TryGetValue(seats[0].ControllerDeck, out ContainerPlacementState deckPlace))
            {
                result.LooseFaceUp = PutLoose(match, transfer, seats[0].ControllerDeck, deckPlace.Pose, 3.0, CardFace.FaceUp);
                result.LooseFaceDown = PutLoose(match, transfer, seats[0].ControllerDeck, deckPlace.Pose, 6.0, CardFace.FaceDown);
            }

            List<TabletopObjectId> faceDown = new List<TabletopObjectId>();
            if (floorTiles != null)
                foreach (TabletopObjectId id in floorTiles)
                    if (match.Cards.TryGetValue(id, out CardInstanceState tile) && tile.Face == CardFace.FaceDown) faceDown.Add(id);
            faceDown.Sort((a, b) => a.Value.CompareTo(b.Value));
            if (faceDown.Count > 0)
            {
                match.Cards[faceDown[0]].SetFace(CardFace.FaceUp);
                result.RevealedTile = faceDown[0];
            }

            result.UnrevealedTiles = Math.Max(0, faceDown.Count - 1);
            return result;
        }

        private static TabletopObjectId PutLoose(MatchState match, ContainerTransferService transfer, ContainerId deckId, TabletopPose deckPose, double along, CardFace face)
        {
            ContainerState deck = match.GetContainer(deckId);
            if (deck.Count == 0) return TabletopObjectId.Empty;
            CardInstanceState card = match.Cards[deck.GetObjectAt(deck.TopIndex)];
            if (!transfer.RemoveFromContainer(card.BaseState, deck).Succeeded) return TabletopObjectId.Empty;
            double radians = deckPose.RotationDegrees * Math.PI / 180d;
            TableCoordinate at = new TableCoordinate(
                deckPose.Position.X + (Math.Cos(radians) * along),
                deckPose.Position.Y + (Math.Sin(radians) * along));
            card.BaseState.SetPose(new TabletopPose(at, deckPose.RotationDegrees, 0, 0));
            card.SetFace(face);
            return card.BaseState.Id;
        }
    }
}
