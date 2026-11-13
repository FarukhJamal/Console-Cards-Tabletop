using System;
using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// P-1 (owner, 2026-11-13: "a Tabletop-Simulator-style hand area on the table in front of each seat"): each seat
    /// has a marked hand area at its edge of the table, placed from the seat's hand zone in the table layout. A card
    /// dropped in your area goes into your Hand (your screen hand shows it). Another player's area takes cards only in
    /// Free (owner: "allowed in Free only, refused in Full, for now"; later each game decides): in Reminders and Full
    /// it is shown (its cards face down, as before) but takes nothing. The move is the usual card transfer, so it is replicated, checked by the
    /// host and undone as before. Checked once a frame, with no allocation once the areas exist.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float HandAreaWidthCards = 6.5f;
        private const float HandAreaDepthCards = 2.4f;

        private readonly List<TableHandArea> handAreas = new List<TableHandArea>(8);
        private Transform handAreaRoot;
        private Func<ContainerId, ContainerState> handAreaStates;

        // From UpdatePolish, after the hand row.
        private void UpdateHandAreas()
        {
            int used = 0;
            if (IsInitialized
                && matchState != null
                && !privateViewActive
                && coordinateConverter != null
                && localHandVisual != null
                && localHandVisual.TargetCollider != null)
            {
                handAreaStates ??= id => matchState != null && matchState.Containers.TryGetValue(id, out ContainerState state) ? state : null;
                float card = WorldPerCardWidth();
                Vector2 size = new Vector2(HandAreaWidthCards * card, HandAreaDepthCards * card);
                IReadOnlyList<CartridgeSeat> seats = TableSeats;
                bool free = PlayAssist.IsFree;
                for (int i = 0; i < seats.Count; i++)
                {
                    CartridgeSeat seat = seats[i];
                    ContainerId hand = seat.HandContainerId;
                    if (hand.IsEmpty || !matchState.Containers.ContainsKey(hand)) continue;
                    bool mine = seat.LayoutSeatIndex == localPlayerLayoutSeatIndex;
                    if (mine && handsOnTable) continue; // the hand already lies on its table zone, which takes drops itself
                    if (!TryGetHandAreaPose(hand, mine, out Vector3 centre, out Quaternion rotation)) continue;
                    PlayerId occupant = matchState.Seats.TryGetValue(seat.SeatId, out SeatState seatState)
                        ? seatState.OccupantPlayerId
                        : PlayerId.Empty;
                    Color colour = occupant.IsEmpty ? Color.white : FeedbackColour(occupant);
                    colour.a = mine ? 0.55f : 0.35f;
                    // Every seat's area is shown (its hand's cards lie there face down to the others, as before); another
                    // player's area takes a dropped card only in Free (owner, 2026-11-13).
                    // Owner, 2026-11-14 (TTS video): your area is a tall box standing over the table's edge, from below the
                    // rim to well above the standing cards and reaching in over the rim, so letting go anywhere near your
                    // hand puts the card in it.
                    if (mine)
                    {
                        Vector3 inward = rotation * Vector3.forward;
                        HandAreaAt(used++, seat.LayoutSeatIndex).Show(hand, handAreaStates, centre + (inward * (0.2f * card)), rotation,
                            new Vector2(HandAreaWidthCards * card, 2.6f * card), colour, true, 3.4f * card, 1.2f * card);
                    }
                    else
                    {
                        HandAreaAt(used++, seat.LayoutSeatIndex).Show(hand, handAreaStates, centre, rotation, size, colour, free);
                    }
                }
            }

            for (int i = used; i < handAreas.Count; i++) handAreas[i].Hide();

            // Owner, 2026-11-13 / 11-14 (TTS video): each area is a hidden zone; its box lights only while a card is carried
            // and the pointer is over it.
            UnityEngine.Camera viewer = hitResolver != null ? hitResolver.TargetCamera : null;
            bool carrying = (moveCoordinator != null && moveCoordinator.HasActiveInteraction)
                || (interactionRouter != null && interactionRouter.HasActiveInteraction)
                || IsCarryingHandCards;
            bool pointer = carrying && viewer != null && UnityEngine.InputSystem.Mouse.current != null;
            Ray ray = pointer ? viewer.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue()) : default;
            for (int i = 0; i < used; i++) handAreas[i].SetHighlighted(pointer && handAreas[i].Contains(ray));
        }

        private bool TryGetHandAreaPose(ContainerId hand, bool mine, out Vector3 centre, out Quaternion rotation)
        {
            if (mine && TryGetHandEdgePose(out centre, out rotation)) return true; // your area is your hand row, past the edge
            if (matchState.TryGetContainerPlacement(hand, out ContainerPlacementState zone))
            {
                centre = ContainerViewBinding.PlacementWorldPosition(zone, coordinateConverter);
                rotation = coordinateConverter.ToWorldRotation(zone.Pose);
                return true;
            }

            if (mine && TryGetHandEdgePose(out centre, out rotation)) return true;
            centre = default;
            rotation = Quaternion.identity;
            return false;
        }

        private const float HandRowPastEdgeCards = 0.45f; // card widths past the table's edge (owner's mark, 2026-11-13)
        private const float HandRowLiftCards = 0.3f;      // card widths above the table's top (owner, 2026-11-14: no clipping)

        // Owner, 2026-11-13 (screenshot, "above edge"): this screen's hand row stands just past the table's edge in front
        // of its Console, turned to the seat. False without a Console or a table to measure.
        private bool TryGetLocalHandRowPose(out Vector3 surface, out Quaternion seatRotation)
        {
            surface = default;
            seatRotation = Quaternion.identity;
            if (privateViewActive || !TryGetTableWorldBounds(out Bounds table)) return false;
            IReadOnlyList<CartridgeSeat> seats = TableSeats;
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].LayoutSeatIndex != localPlayerLayoutSeatIndex) continue;
                if (!matchState.Seats.TryGetValue(seats[i].SeatId, out SeatState seat)
                    || seat.Console == null
                    || seat.Console.SlotContainerIds.Count == 0
                    || !TryResolveConsolePlacement(seat.Console.SlotContainerIds[0], out _, out _, out ConsoleView consoleView)
                    || consoleView == null)
                {
                    return false;
                }

                Vector3 console = consoleView.transform.position;
                Vector3 outward = console - table.center;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.0001f) return false;
                outward.Normalize();
                float toEdge = DistanceToBoundsEdge(console, outward, table);
                surface = console + (outward * (toEdge + (HandRowPastEdgeCards * WorldPerCardWidth())));
                surface.y = table.max.y + (HandRowLiftCards * WorldPerCardWidth()); // floats clear of the rim, as in TTS
                seatRotation = Quaternion.LookRotation(-outward, Vector3.up);
                return true;
            }

            return false;
        }

        private static float DistanceToBoundsEdge(Vector3 from, Vector3 direction, Bounds bounds)
        {
            float x = direction.x > 0.0001f ? (bounds.max.x - from.x) / direction.x
                : direction.x < -0.0001f ? (bounds.min.x - from.x) / direction.x
                : float.MaxValue;
            float z = direction.z > 0.0001f ? (bounds.max.z - from.z) / direction.z
                : direction.z < -0.0001f ? (bounds.min.z - from.z) / direction.z
                : float.MaxValue;
            return Mathf.Max(0f, Mathf.Min(x, z));
        }

        // Owner, 2026-11-13 (TTS video): cards drawn or moved from a pile into this screen's hand fly from that pile.
        private void NoteHandArrival(ContainerId from, ContainerId to)
        {
            if (handView == null || !handView.IsBound || to != handView.ContainerId || matchState == null) return;
            if (from.IsEmpty)
            {
                // A loose card let go over the hand: it flies to the row from where it was let go (under the pointer),
                // not from the screen hand.
                if (pointerProjector == null || UnityEngine.InputSystem.Mouse.current == null
                    || !pointerProjector.TryProjectScreenPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue(), out TableCoordinate dropped))
                {
                    return;
                }

                handEdgeArrivalFrom = coordinateConverter.ToWorldPosition(dropped);
                handEdgeArrivalAt = Time.unscaledTime;
                return;
            }

            if (!matchState.TryGetContainerPlacement(from, out ContainerPlacementState pile)) return;
            handEdgeArrivalFrom = ContainerViewBinding.PlacementWorldPosition(pile, coordinateConverter);
            handEdgeArrivalAt = Time.unscaledTime;
        }

        private TableHandArea HandAreaAt(int index, int layoutSeat)
        {
            if (handAreaRoot == null)
            {
                handAreaRoot = new GameObject("Hand Areas").transform;
                handAreaRoot.SetParent(transform, false);
            }

            while (handAreas.Count <= index)
            {
                handAreas.Add(TableHandArea.Create(
                    handAreaRoot,
                    $"Hand Area {handAreas.Count + 1}",
                    localHandVisual.TargetCollider.gameObject.layer,
                    localHandVisual.FeedbackRenderer != null ? localHandVisual.FeedbackRenderer.sharedMaterial : null));
            }

            return handAreas[index];
        }
    }
}
