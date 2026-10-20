using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Seats along the table's edge (CART-1c, doc 31 PL-CARTRIDGE). A game whose cartridge says its seats go along
    /// the edge (<see cref="SeatPlacement.AlongEdge"/>) has each seat moved out along the line from the table's centre
    /// through its place on the classic table, until the seat's pieces stand just inside the edge: its Console (with
    /// the cards in it), its piles and hand zone, and the loose cards lying in its area. The seats keep their order and
    /// their facing; on the classic table they go home. It happens when a game's table starts or is reset, and when the
    /// table's shape or size changes during a game. Event-time only; nothing here runs per frame.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        // Room left between a seat's pieces and the table's edge, and the step the search takes (table units).
        private const float SeatEdgeMargin = 0.35f;
        private const float SeatEdgeStep = 0.1f;

        private readonly TableOutline seatOutline = new TableOutline(256);
        private readonly List<ContainerId> seatContainers = new List<ContainerId>(8);
        private readonly List<CardView> seatLooseCards = new List<CardView>(8);
        private readonly Vector2[] seatCorners = new Vector2[4];

        /// <summary>
        /// Moves the seats of the game in play to the table's edge (or home on the classic table). With
        /// <paramref name="foldIntoStart"/> the moves become part of the table's start (Undo does not take them back);
        /// otherwise they are one Undo step. Returns how many seats moved.
        /// </summary>
        internal int ArrangeSeatsAlongEdge(bool foldIntoStart)
        {
            IGameCartridge cartridge = ActiveCartridge;
            IReadOnlyList<CartridgeSeat> seats = cartridge.Seats;
            if (cartridge.SeatPlacement != SeatPlacement.AlongEdge
                || seats.Count == 0
                || !IsInitialized
                || rebuildingFromUndo
                || matchState == null
                || coordinateConverter == null
                || !FindClassicTable())
            {
                return 0;
            }

            float unit = classicFrame.WorldPerTableUnit;
            bool home = tableSpec.shape == TableShape.Classic;
            if (!home && !seatOutline.Build(HoldSpecFor(tableSpec), unit, 1f))
            {
                return 0;
            }

            int moved = 0;
            if (foldIntoStart) SessionService.BeginUndoAmend();
            else SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < seats.Count; i++)
                {
                    if (MoveSeatToEdge(seats[i], home, unit)) moved++;
                }
            }
            finally
            {
                if (foldIntoStart) SessionService.EndUndoAmend();
                else SessionService.EndUndoBatch();
                seatContainers.Clear();
                seatLooseCards.Clear();
            }

            if (moved > 0) ConfigureTabletopCameraFraming(); // the seat views frame the seats where they are now
            return moved;
        }

        // A new or reset game table: the seats start at the edge, as part of the table's start. A problem here is
        // logged and leaves the seats where the template put them; it never stops the table.
        internal void ArrangeSeatsAtStart()
        {
            try
            {
                ArrangeSeatsAlongEdge(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private bool MoveSeatToEdge(CartridgeSeat seat, bool home, float unit)
        {
            if (!matchState.Seats.TryGetValue(seat.SeatId, out SeatState seatState)
                || seatState.Console == null
                || seatState.Console.SlotContainerIds.Count == 0)
            {
                return false;
            }

            ContainerId consoleSlotId = seatState.Console.SlotContainerIds[0];
            if (!TryResolveConsolePlacement(consoleSlotId, out TabletopPose consolePose, out _, out ConsoleView consoleView)
                || consoleView == null)
            {
                return false;
            }

            Quaternion inverse = Quaternion.Inverse(classicFrame.Rotation);
            Vector2 homeFlat = TableFlat(coordinateConverter.ToWorldPosition(seat.HomeConsolePose), inverse);
            float homeRadius = homeFlat.magnitude;
            if (homeRadius < 1e-3f) return false; // a seat in the middle has no way out
            Vector2 direction = homeFlat / homeRadius;
            Vector2 currentFlat = TableFlat(coordinateConverter.ToWorldPosition(consolePose), inverse);

            // The seat's pieces and the ground they cover (flat, in the table's frame).
            Bounds group = AllBounds(consoleView);
            CollectSeatPieces(seat, ref group, unit);
            FlatCorners(group, inverse, seatCorners);

            float radius = home ? homeRadius : EdgeRadius(direction, currentFlat, homeRadius, unit);
            Vector2 shiftFlat = (direction * radius) - currentFlat;
            if (shiftFlat.sqrMagnitude < 0.0001f * unit * unit) return false; // already there
            Vector3 shift = classicFrame.Rotation * new Vector3(shiftFlat.x, 0f, shiftFlat.y);

            PlayerId occupant = seatState.OccupantPlayerId.IsEmpty ? localPlayerId : seatState.OccupantPlayerId;
            bool any = SubmitSeatMove(consoleSlotId, consolePose, shift, CreateCommandContext());
            for (int i = 0; i < seatContainers.Count; i++)
            {
                if (matchState.TryGetContainerPlacement(seatContainers[i], out ContainerPlacementState placement))
                {
                    // A seat's own piles move for that seat's player (another player's pile cannot be moved).
                    any |= SubmitSeatMove(seatContainers[i], placement.Pose, shift, CreateCommandContext(occupant));
                }
            }

            for (int i = 0; i < seatLooseCards.Count; i++)
            {
                CardView card = seatLooseCards[i];
                if (card == null || !card.IsBound) continue;
                TabletopObjectState state = card.BoundState;
                TabletopPose pose = new TabletopPose(
                    coordinateConverter.ToTableCoordinate(card.transform.position + shift),
                    state.Pose.RotationDegrees,
                    state.Pose.Layer,
                    state.Pose.LocalOrder);
                any |= MoveLoosePiece(card, state, pose);
            }

            return any;
        }

        // The farthest place out along the direction where every corner of the seat's ground stands inside the edge
        // with room to spare; never nearer the centre than its classic place (the game keeps its room).
        private float EdgeRadius(Vector2 direction, Vector2 currentFlat, float homeRadius, float unit)
        {
            float margin = SeatEdgeMargin * unit;
            float step = SeatEdgeStep * unit;
            for (float radius = seatOutline.DistanceToEdge(direction); radius > homeRadius; radius -= step)
            {
                Vector2 shift = (direction * radius) - currentFlat;
                bool fits = true;
                for (int c = 0; c < seatCorners.Length && fits; c++)
                {
                    Vector2 corner = seatCorners[c] + shift;
                    fits = seatOutline.Contains(corner) && seatOutline.DistanceToOutline(corner) >= margin;
                }

                if (fits) return radius;
            }

            return homeRadius;
        }

        // The seat's piles and hand zone (any container of the seat with a place on the table), and the loose cards
        // lying in its area (for example cards waiting beside the Console); the ground they cover is added to group.
        private void CollectSeatPieces(CartridgeSeat seat, ref Bounds group, float unit)
        {
            seatContainers.Clear();
            seatLooseCards.Clear();
            foreach (KeyValuePair<ContainerId, ContainerPlacementState> pair in matchState.ContainerPlacements)
            {
                if (!matchState.Containers.TryGetValue(pair.Key, out ContainerState container)
                    || container.OwnerSeatId != seat.SeatId)
                {
                    continue;
                }

                bool movable = container.Kind == ContainerKind.Deck
                    || container.Kind == ContainerKind.Stack
                    || container.Kind == ContainerKind.DiscardPile
                    || (container.Kind == ContainerKind.Hand && pair.Value.HasExtent);
                if (!movable) continue;
                seatContainers.Add(pair.Key);
                group.Encapsulate(new Bounds(
                    coordinateConverter.ToWorldPosition(pair.Value.Pose),
                    new Vector3(2f * unit, 0.5f * unit, 2.8f * unit)));
            }

            Bounds area = group;
            area.Expand(new Vector3(0.6f * unit, 2f * unit, 0.6f * unit));
            for (int i = 0; i < Registry.cardViews.Count; i++)
            {
                CardView card = Registry.cardViews[i];
                if (card == null
                    || !card.IsBound
                    || card.BoundState == null
                    || !card.BoundState.ContainerId.IsEmpty
                    || card.IsContainerLayoutApplied
                    || card.IsOverlayPresentation
                    || ActiveCartridge.IsFixedBoardPiece(card.BoundState.Id)
                    || !area.Contains(card.transform.position))
                {
                    continue;
                }

                seatLooseCards.Add(card);
            }
        }

        private bool SubmitSeatMove(ContainerId containerId, TabletopPose from, Vector3 shift, CommandContext context)
        {
            TabletopPose to = new TabletopPose(
                coordinateConverter.ToTableCoordinate(coordinateConverter.ToWorldPosition(from) + shift),
                from.RotationDegrees,
                from.Layer,
                from.LocalOrder);
            MoveContainerResult result = TableAuthority.Submit<MoveContainerCommand, MoveContainerResult>(
                new MoveContainerCommand(context, containerId, to));
            if (!result.Succeeded)
            {
                Debug.LogWarning($"[Seats] A seat's piece could not move to the edge: {result.Error}.", this);
            }

            // R2: the pile, hand zone or Console was set in its new place by the table's view sync.
            return result.Succeeded;
        }

        // A point of a seat's (for example where cards are dealt to it), moved with the seat: by how far its Console
        // now stands from its home place. Unchanged while the seat is at home.
        internal TableCoordinate SeatMovedPoint(CartridgeSeat seat, SeatState state, TableCoordinate point)
        {
            if (state?.Console == null || state.Console.SlotContainerIds.Count == 0)
            {
                return point;
            }

            TabletopPose consolePose = state.ConsolePose;
            return new TableCoordinate(
                point.X + (consolePose.Position.X - seat.HomeConsolePose.Position.X),
                point.Y + (consolePose.Position.Y - seat.HomeConsolePose.Position.Y));
        }

        // A world point on the table's frame, flat (x, z from the table's centre).
        private Vector2 TableFlat(Vector3 world, Quaternion inverse)
        {
            Vector3 local = inverse * (world - classicFrame.Centre);
            return new Vector2(local.x, local.z);
        }

        // The four flat corners of a world box on the table's frame.
        private void FlatCorners(Bounds bounds, Quaternion inverse, Vector2[] corners)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            corners[0] = TableFlat(new Vector3(min.x, bounds.center.y, min.z), inverse);
            corners[1] = TableFlat(new Vector3(max.x, bounds.center.y, min.z), inverse);
            corners[2] = TableFlat(new Vector3(max.x, bounds.center.y, max.z), inverse);
            corners[3] = TableFlat(new Vector3(min.x, bounds.center.y, max.z), inverse);
        }
    }
}
