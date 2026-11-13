using System.Collections.Generic;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Owner, 2026-11-03 ("show them on the table edge like Tabletop Simulator and select them", "both at once"): your
    /// hand also stands as a row of cards at your edge of the table, faces turned to you, while it stays at the bottom
    /// of the screen. Only this screen shows it (the others see the backs at your edge, as before). The row is for
    /// picking several cards at once:
    /// <list type="bullet">
    /// <item>a click on a card in the row selects it (or takes it out of the selection); a box over the row selects
    /// those cards, as for anything on the table;</item>
    /// <item>a drag on a card in the row carries every selected hand card with it (the card alone when it was not
    /// selected), and the other selected pieces follow; let go over a deck, stack or discard pile and the cards go into
    /// it, anywhere else and they are laid face up in a row there. One Undo step.</item>
    /// </list>
    /// The row's cards are copies of the cards' faces (no colliders, picked by a ray against each card), rebuilt only
    /// when the hand changes; nothing is allocated per frame.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float HandEdgeLean = 72f;            // degrees up from lying flat (owner, 2026-11-13: standing, as in TTS)
        private const float HandEdgeOverlap = 0.58f;       // of a card's width between two cards in the row
        private const float HandEdgeMaxRowCards = 7f;      // the row is never wider than this many cards
        private const float HandEdgeLayerGap = 0.004f;     // world units between neighbours (no flicker)
        private const float HandEdgeSelectedLift = 0.22f;  // of a card's length: a selected card rises out of the row
        private const float HandEdgeFollow = 16f;          // how fast the shown cards reach where they should be
        private const float HandCarryLift = 0.12f;         // world units the carried cards ride above the table
        private const float HandRowSpacing = 1.08f;        // of a card's width between cards laid on the table
        private const float HandEdgeSize = 1.25f;          // the row's cards a little larger than on the table, to read

        private readonly List<CardView> handEdgeCards = new List<CardView>(16);
        private readonly List<Transform> handEdgeProxies = new List<Transform>(16);
        private readonly List<Renderer> handEdgeCopyRenderers = new List<Renderer>(32);
        private readonly List<Renderer> handEdgeSourceRenderers = new List<Renderer>(32);
        // Owner, 2026-11-13 (TTS video): a card drawn into the hand flies from its pile to the row, one after another;
        // the cards already in the row keep their place when it is made again.
        private readonly List<float> handEdgeStartAt = new List<float>(16);
        private readonly Dictionary<TabletopObjectId, Pose> handEdgeLastPose = new Dictionary<TabletopObjectId, Pose>(16);
        private Vector3 handEdgeArrivalFrom;
        private float handEdgeArrivalAt = -10f;
        private ContainerId handEdgeContainerId;
        private Transform handEdgeRoot;
        private MaterialPropertyBlock handEdgeBlock;
        private Vector3 handEdgeScale = Vector3.one;
        private float handEdgeHalfWidth = 0.35f;   // in the card's own (unscaled) units
        private float handEdgeHalfLength = 0.5f;

        // A drag that started on the row.
        private bool handEdgePressActive;
        private CardView handEdgePressCard;
        private bool handCarryActive;
        private Vector3 handCarryPoint;
        private Vector3 handCarryGrab;
        private readonly List<CardView> handCarryCards = new List<CardView>(16);
        private readonly List<PhysicalLooseObject> handCarryFollowers = new List<PhysicalLooseObject>(16);
        private readonly List<Vector3> handCarryOffsets = new List<Vector3>(16);
        private readonly List<ContainerId> handCarryPiles = new List<ContainerId>(8);
        private bool handCarryDropPending;
        private ContainerId handCarryDropTarget;

        // POL-7: selected hand cards that come along when a selected piece on the table is dragged.
        private readonly List<CardView> groupHandCards = new List<CardView>(16);

        internal bool IsCarryingHandCards => handCarryActive || handCarryDropPending;

        // ---------- The row ----------

        // Every frame (from UpdatePolish): the row matches the hand, and each card moves toward where it should be.
        private void UpdateHandEdge()
        {
            if (!TryGetHandEdgePose(out Vector3 surface, out Quaternion seatRotation))
            {
                if (handEdgeProxies.Count > 0 || handEdgeCards.Count > 0) ClearHandEdge();
                return;
            }

            if (!HandEdgeMatchesHand()) RebuildHandEdge();
            int count = handEdgeProxies.Count;
            if (count == 0) return;

            Vector3 right = seatRotation * Vector3.right;
            Vector3 forward = seatRotation * Vector3.forward;
            Vector3 toMiddle = coordinateConverter.ToWorldPosition(TabletopPose.Default) - surface;
            toMiddle.y = 0f;
            // The face (the card's top while it lies face up) turns toward the seat, away from the middle.
            float sign = toMiddle.sqrMagnitude < 0.0001f || Vector3.Dot(forward, toMiddle) >= 0f ? -1f : 1f;
            Quaternion lean = Quaternion.AngleAxis(sign * HandEdgeLean, right);
            Quaternion standing = lean * seatRotation;
            Vector3 normal = lean * Vector3.up;
            Vector3 up = standing * Vector3.forward; // the card's long side, rising from the table
            float width = handEdgeHalfWidth * 2f * Mathf.Abs(handEdgeScale.x);
            float length = handEdgeHalfLength * 2f * Mathf.Abs(handEdgeScale.z);
            float spacing = width * HandEdgeOverlap;
            if (count > 1) spacing = Mathf.Min(spacing, (width * HandEdgeMaxRowCards) / (count - 1));
            float rise = (0.5f * length * Mathf.Sin(HandEdgeLean * Mathf.Deg2Rad)) + 0.002f;
            float follow = 1f - Mathf.Exp(-HandEdgeFollow * Mathf.Max(0f, Time.unscaledDeltaTime));

            int carriedIndex = 0;
            int carriedCount = CarriedHandCardCount();
            for (int i = 0; i < count; i++)
            {
                Transform proxy = handEdgeProxies[i];
                CardView card = handEdgeCards[i];
                if (proxy == null) continue;
                if (i < handEdgeStartAt.Count && Time.unscaledTime < handEdgeStartAt[i]) continue; // waits at its pile
                Vector3 position;
                Quaternion rotation;
                if (IsHandCardCarried(card))
                {
                    position = HandRowPosition(CarryAnchor(), seatRotation, carriedIndex, carriedCount) + (Vector3.up * HandCarryLift);
                    rotation = seatRotation;
                    carriedIndex++;
                }
                else
                {
                    float along = (i - ((count - 1) * 0.5f)) * spacing;
                    position = surface + (right * along) + (Vector3.up * rise) + (normal * (i * HandEdgeLayerGap));
                    if (IndexOfPick(new ToolPick { View = card }) >= 0) position += up * (length * HandEdgeSelectedLift);
                    rotation = standing;
                }

                proxy.SetPositionAndRotation(
                    Vector3.Lerp(proxy.position, position, follow),
                    Quaternion.Slerp(proxy.rotation, rotation, follow));
            }
        }

        // Where the row stands: this screen's hand zone, turned to the seat (the Empty Table: the near edge).
        private bool TryGetHandEdgePose(out Vector3 surface, out Quaternion seatRotation)
        {
            surface = default;
            seatRotation = Quaternion.identity;
            if (!IsInitialized || matchState == null || handsOnTable || handView == null || !handView.IsBound
                || coordinateConverter == null)
            {
                return false;
            }

            if (TryGetLocalHandRowPose(out surface, out seatRotation)) return true; // owner, 2026-11-13: past the table's edge

            ContainerId handId = handView.ContainerId;
            if (handId.IsEmpty || !matchState.Containers.ContainsKey(handId)) return false;
            if (matchState.TryGetContainerPlacement(handId, out ContainerPlacementState zone))
            {
                surface = ContainerViewBinding.PlacementWorldPosition(zone, coordinateConverter);
                seatRotation = coordinateConverter.ToWorldRotation(zone.Pose);
                return true;
            }

            if (!TryGetTableWorldBounds(out Bounds table)) return false;
            surface = new Vector3(table.center.x, table.max.y, table.min.z + (0.12f * table.size.z));
            seatRotation = classicFrame.Rotation;
            return true;
        }

        private bool HandEdgeMatchesHand()
        {
            ContainerId handId = handView.ContainerId;
            if (handId != handEdgeContainerId || !matchState.Containers.TryGetValue(handId, out ContainerState hand)) return false;
            if (hand.Count != handEdgeCards.Count) return false;
            for (int i = 0; i < hand.Count; i++)
            {
                CardView card = handEdgeCards[i];
                if (card == null || !card.IsBound || card.ObjectId != hand.GetObjectAt(i)) return false;
            }

            return true;
        }

        // Made again only when the hand changes: one copy of each card's face, at the table's card size.
        private void RebuildHandEdge()
        {
            handEdgeLastPose.Clear();
            for (int i = 0; i < handEdgeProxies.Count; i++)
            {
                if (handEdgeProxies[i] != null && handEdgeCards[i] != null && handEdgeCards[i].IsBound)
                    handEdgeLastPose[handEdgeCards[i].ObjectId] = new Pose(handEdgeProxies[i].position, handEdgeProxies[i].rotation);
            }

            bool arriving = Time.unscaledTime - handEdgeArrivalAt < 1.5f;
            ClearHandEdge();
            ContainerId handId = handView.ContainerId;
            if (!matchState.Containers.TryGetValue(handId, out ContainerState hand)) return;
            handEdgeContainerId = handId;
            if (handEdgeRoot == null)
            {
                handEdgeRoot = new GameObject("Hand At Table Edge").transform;
                handEdgeRoot.SetParent(transform, false);
            }

            if (handEdgeBlock == null) handEdgeBlock = new MaterialPropertyBlock();
            handEdgeScale = TableCardScale() * HandEdgeSize;
            for (int i = 0; i < hand.Count; i++)
            {
                if (!Registry.TryGetCardView(hand.GetObjectAt(i), out CardView card) || card == null || !card.IsFacePresentationConfigured)
                {
                    continue;
                }

                if (handEdgeCards.Count == 0) MeasureHandEdgeCard(card);
                Transform proxy = new GameObject("Hand Card").transform;
                proxy.SetParent(handEdgeRoot, false);
                proxy.localScale = handEdgeScale;
                GameObject face = Instantiate(card.FaceUpVisualRoot, proxy, false);
                face.SetActive(true);
                Transform source = card.FaceUpVisualRoot.transform;
                face.transform.localPosition = source.localPosition;
                face.transform.localRotation = source.localRotation;
                face.transform.localScale = source.localScale;
                Collider[] colliders = face.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++) Destroy(colliders[c]);
                CopyPropertyBlocks(card.FaceUpVisualRoot, face);
                // A card already in the row keeps its place; a card just drawn starts at its pile and flies to the row in
                // its turn (the slide queue); any other starts where the card is in the tray.
                float startAt = 0f;
                if (handEdgeLastPose.TryGetValue(card.ObjectId, out Pose last))
                {
                    proxy.SetPositionAndRotation(last.position, last.rotation);
                }
                else if (arriving)
                {
                    proxy.SetPositionAndRotation(handEdgeArrivalFrom, card.transform.rotation);
                    startAt = Time.unscaledTime + NextCardSlideDelay(QueuedCardSlideSeconds);
                }
                else
                {
                    proxy.SetPositionAndRotation(card.transform.position, card.transform.rotation);
                }

                handEdgeCards.Add(card);
                handEdgeProxies.Add(proxy);
                handEdgeStartAt.Add(startAt);
            }
        }

        // The face's look set at run time (picture, colours) is copied too.
        private void CopyPropertyBlocks(GameObject from, GameObject to)
        {
            from.GetComponentsInChildren(true, handEdgeSourceRenderers);
            to.GetComponentsInChildren(true, handEdgeCopyRenderers);
            int count = Mathf.Min(handEdgeSourceRenderers.Count, handEdgeCopyRenderers.Count);
            for (int i = 0; i < count; i++)
            {
                if (!handEdgeSourceRenderers[i].HasPropertyBlock()) continue;
                handEdgeSourceRenderers[i].GetPropertyBlock(handEdgeBlock);
                handEdgeCopyRenderers[i].SetPropertyBlock(handEdgeBlock);
            }

            handEdgeSourceRenderers.Clear();
            handEdgeCopyRenderers.Clear();
        }

        // A card's size in its own units, from its collider (read even while the collider is off in the hand).
        private void MeasureHandEdgeCard(CardView card)
        {
            handEdgeHalfWidth = 0.35f;
            handEdgeHalfLength = 0.5f;
            Collider collider = card.PhysicalObject != null ? card.PhysicalObject.PhysicalCollider : null;
            if (collider is BoxCollider box)
            {
                float a = Mathf.Abs(box.size.x);
                float b = Mathf.Abs(box.size.z);
                if (Mathf.Min(a, b) > 0.0001f)
                {
                    handEdgeHalfWidth = 0.5f * a;
                    handEdgeHalfLength = 0.5f * b;
                }
            }
        }

        // The scale a card has on the table (not the tray's): any card that is not in this screen's hand.
        private Vector3 TableCardScale()
        {
            ContainerId handId = handView != null ? handView.ContainerId : ContainerId.Empty;
            for (int i = 0; i < Registry.cardViews.Count; i++)
            {
                CardView card = Registry.cardViews[i];
                if (card == null || !card.IsBound || card.BoundState == null || card.BoundState.ContainerId == handId) continue;
                return card.transform.lossyScale;
            }

            return Vector3.one;
        }

        // Called with the table's other clean-ups (a new table, shutdown) and before the row is made again.
        private void ClearHandEdge()
        {
            for (int i = 0; i < handEdgeProxies.Count; i++)
            {
                if (handEdgeProxies[i] != null) Destroy(handEdgeProxies[i].gameObject);
            }

            handEdgeProxies.Clear();
            handEdgeCards.Clear();
            handEdgeStartAt.Clear();
            handEdgeContainerId = ContainerId.Empty;
        }

        // The card of the row under the pointer (the front-most one), or null.
        private CardView HandEdgeCardAt(Vector2 screenPosition)
        {
            UnityCamera camera = hitResolver != null ? hitResolver.TargetCamera : null;
            if (camera == null || handEdgeProxies.Count == 0) return null;
            Ray ray = camera.ScreenPointToRay(screenPosition);
            for (int i = handEdgeProxies.Count - 1; i >= 0; i--)
            {
                Transform proxy = handEdgeProxies[i];
                if (proxy == null || IsHandCardCarried(handEdgeCards[i])) continue;
                Vector3 normal = proxy.rotation * Vector3.up;
                Plane plane = new Plane(normal, proxy.position);
                if (!plane.Raycast(ray, out float distance)) continue;
                Vector3 local = Quaternion.Inverse(proxy.rotation) * (ray.GetPoint(distance) - proxy.position);
                float halfWidth = handEdgeHalfWidth * Mathf.Abs(handEdgeScale.x);
                float halfLength = handEdgeHalfLength * Mathf.Abs(handEdgeScale.z);
                if (Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.z) <= halfLength) return handEdgeCards[i];
            }

            return null;
        }

        // A box drawn over the row selects those cards (from SelectInBox).
        private void BoxSelectHandEdge(UnityCamera camera, Vector2 min, Vector2 max)
        {
            for (int i = 0; i < handEdgeProxies.Count; i++)
            {
                if (handEdgeProxies[i] == null || !IsSelectableHandCard(handEdgeCards[i])) continue;
                if (InScreenBox(camera, handEdgeProxies[i].position, min, max)) AddPick(new ToolPick { View = handEdgeCards[i] });
            }
        }

        // ---------- A press on the row (from the select handler) ----------

        // True when the press landed on the row: a click selects, a drag carries.
        private bool TryBeginHandEdgePress(Vector2 screenPosition)
        {
            handEdgePressActive = false;
            handEdgePressCard = HandEdgeCardAt(screenPosition);
            if (handEdgePressCard == null || !IsSelectableHandCard(handEdgePressCard)) return false;
            handEdgePressActive = true;
            return true;
        }

        // The pointer moved past the click distance: the selected hand cards (or this one) are picked up.
        private void BeginHandCarry(Vector2 screenPosition)
        {
            if (!handEdgePressActive || handCarryActive || pointerProjector == null
                || !pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate grab))
            {
                return;
            }

            if (IndexOfPick(new ToolPick { View = handEdgePressCard }) < 0)
            {
                // As on the table: picking up a card that is not selected takes it alone.
                toolSelection.Clear();
                toolSelection.Add(new ToolPick { View = handEdgePressCard });
                DrawToolSelection();
                RefreshToolBarState();
            }

            handCarryGrab = coordinateConverter.ToWorldPosition(grab);
            handCarryPoint = handCarryGrab;
            handCarryCards.Clear();
            handCarryFollowers.Clear();
            handCarryOffsets.Clear();
            handCarryPiles.Clear();
            for (int i = 0; i < handEdgeCards.Count; i++)
            {
                CardView card = handEdgeCards[i];
                if (IndexOfPick(new ToolPick { View = card }) >= 0) handCarryCards.Add(card);
            }

            // The other selected pieces on the table come along; selected piles follow when it is let go.
            for (int i = 0; i < toolSelection.Count; i++)
            {
                ToolPick pick = toolSelection[i];
                if (pick.IsPile)
                {
                    handCarryPiles.Add(pick.Pile);
                    continue;
                }

                if (IsSelectableHandCard(pick.View) || !IsSelectableLoosePiece(pick.View)) continue;
                PhysicalLooseObject follower = pick.View.PhysicalObject;
                if (follower == null || follower.Body == null) continue;
                Vector3 offset = follower.Body.position - handCarryGrab;
                if (!follower.FollowWorld(handCarryGrab + offset)) continue;
                handCarryFollowers.Add(follower);
                handCarryOffsets.Add(offset);
            }

            handCarryActive = handCarryCards.Count > 0;
            if (handCarryActive) PlayCardSound(false);
        }

        private void UpdateHandCarry(Vector2 screenPosition)
        {
            if (!handCarryActive || pointerProjector == null
                || !pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate point))
            {
                return;
            }

            handCarryPoint = coordinateConverter.ToWorldPosition(point);
            Vector3 moved = handCarryPoint - handCarryGrab;
            moved.y = 0f;
            for (int i = 0; i < handCarryFollowers.Count; i++)
            {
                PhysicalLooseObject follower = handCarryFollowers[i];
                if (follower != null && follower.View != null && follower.View.IsBound) follower.FollowWorld(handCarryGrab + handCarryOffsets[i] + moved);
            }
        }

        // Let go: the cards go into the pile under the pointer, or onto the table (on the next frame, once the
        // gesture has fully ended).
        private void ReleaseHandCarry()
        {
            if (!handCarryActive) return;
            handCarryDropTarget = FindHandDropPile(handCarryPoint);
            handCarryDropPending = true;
            handCarryActive = false;
        }

        private void CancelHandCarry()
        {
            handEdgePressActive = false;
            handEdgePressCard = null;
            if (!handCarryActive) return;
            handCarryActive = false;
            for (int i = 0; i < handCarryFollowers.Count; i++)
            {
                if (handCarryFollowers[i] != null && handCarryFollowers[i].IsHeld) handCarryFollowers[i].Cancel();
            }

            ClearHandCarryLists();
        }

        private void ClearHandCarryLists()
        {
            handCarryCards.Clear();
            handCarryFollowers.Clear();
            handCarryOffsets.Clear();
            handCarryPiles.Clear();
        }

        // Called from Update.
        private void CompletePendingHandCarry()
        {
            if (!handCarryDropPending) return;
            if (interactionRouter != null && interactionRouter.HasActiveInteraction) return;
            handCarryDropPending = false;
            Vector3 moved = handCarryPoint - handCarryGrab;
            moved.y = 0f;
            for (int i = 0; i < handCarryFollowers.Count; i++)
            {
                PhysicalLooseObject follower = handCarryFollowers[i];
                if (follower == null || follower.View == null || !follower.View.IsBound || !follower.IsHeld) continue;
                if (!follower.Release()) follower.Cancel();
            }

            SessionService.BeginUndoBatch();
            try
            {
                PlaceHandCards(handCarryCards, handCarryPoint, handCarryDropTarget);
                if (moved.sqrMagnitude > 0.0001f)
                {
                    for (int i = 0; i < handCarryPiles.Count; i++) MovePileBy(handCarryPiles[i], moved);
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            ClearHandCarryLists();
            handEdgePressCard = null;
            PruneToolSelection();
            DrawToolSelection();
            RefreshToolBarState();
            Physics.SyncTransforms();
        }

        // Hand cards into a pile, or laid face up in a row on the table around a point. Part of the caller's Undo step.
        private void PlaceHandCards(List<CardView> cards, Vector3 anchor, ContainerId pileId)
        {
            if (transferCoordinator == null || cards.Count == 0) return;
            bool intoPile = !pileId.IsEmpty && matchState.Containers.ContainsKey(pileId);
            TryGetHandEdgePose(out _, out Quaternion seatRotation);
            int placed = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                if (card == null || !card.IsBound || card.BoundState == null) continue;
                CardDropTarget target;
                if (intoPile)
                {
                    target = CardDropTarget.ForContainer(pileId);
                }
                else
                {
                    TableCoordinate at = coordinateConverter.ToTableCoordinate(HandRowPosition(anchor, seatRotation, i, cards.Count));
                    TabletopPose pose = card.BoundState.Pose;
                    target = CardDropTarget.ForTabletop(new TabletopPose(at, pose.RotationDegrees, pose.Layer, pose.LocalOrder));
                }

                if (transferCoordinator.Transfer(card, target).Succeeded) placed++;
            }

            if (placed == 0) return;
            if (intoPile) ScheduleLandingFeedback(pileId, true);
            PlayCardSound(true);
        }

        // Cards laid in a row on the table, centred on a point, along the seat's edge.
        private Vector3 HandRowPosition(Vector3 anchor, Quaternion seatRotation, int index, int count)
        {
            float width = handEdgeHalfWidth * 2f * Mathf.Abs(handEdgeScale.x);
            Vector3 right = seatRotation * Vector3.right;
            float along = (index - ((count - 1) * 0.5f)) * width * HandRowSpacing;
            return anchor + (right * along);
        }

        // A deck, stack or discard pile under the drop point (not one of the selected piles).
        private ContainerId FindHandDropPile(Vector3 world)
        {
            if (matchState == null) return ContainerId.Empty;
            TableCoordinate point = coordinateConverter.ToTableCoordinate(world);
            double reach = Mathf.Max(StackCarryMergeReach, handEdgeHalfLength * 1.5f * Mathf.Abs(handEdgeScale.z) / Mathf.Max(0.0001f, WorldPerTableUnitForHand()));
            double bestDistance = reach * reach;
            ContainerId best = ContainerId.Empty;
            foreach (KeyValuePair<ContainerId, ContainerState> pair in matchState.Containers)
            {
                ContainerKind kind = pair.Value.Kind;
                if (kind != ContainerKind.Deck && kind != ContainerKind.Stack && kind != ContainerKind.DiscardPile) continue;
                if (IndexOfPick(new ToolPick { Pile = pair.Key }) >= 0
                    || !matchState.TryGetContainerPlacement(pair.Key, out ContainerPlacementState placement))
                {
                    continue;
                }

                double dx = placement.Pose.Position.X - point.X;
                double dy = placement.Pose.Position.Y - point.Y;
                double distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pair.Key;
                }
            }

            return best;
        }

        private float WorldPerTableUnitForHand()
        {
            Vector3 a = coordinateConverter.ToWorldPosition(new TableCoordinate(0d, 0d));
            Vector3 b = coordinateConverter.ToWorldPosition(new TableCoordinate(1d, 0d));
            return Vector3.Distance(a, b);
        }

        // ---------- Carried cards, shown flat in a row under the pointer ----------

        private int CarriedHandCardCount() => handCarryActive || handCarryDropPending ? handCarryCards.Count : groupHandCards.Count;

        private bool IsHandCardCarried(CardView card)
        {
            if (card == null) return false;
            List<CardView> carried = handCarryActive || handCarryDropPending ? handCarryCards : groupHandCards;
            for (int i = 0; i < carried.Count; i++)
            {
                if (ReferenceEquals(carried[i], card)) return true;
            }

            return false;
        }

        // Where carried hand cards are shown: under the pointer, or beside the piece being dragged (POL-7).
        private Vector3 CarryAnchor()
        {
            if (handCarryActive || handCarryDropPending) return handCarryPoint;
            if (groupLeader != null && groupLeader.PhysicalObject != null && groupLeader.PhysicalObject.Body != null)
            {
                return GroupHandAnchor(groupLeader.PhysicalObject.Body.position);
            }

            return handCarryPoint;
        }

        // POL-7: hand cards dragged along with a piece on the table lie in a row just past it.
        private Vector3 GroupHandAnchor(Vector3 leader)
        {
            TryGetHandEdgePose(out _, out Quaternion seatRotation);
            float width = handEdgeHalfWidth * 2f * Mathf.Abs(handEdgeScale.x);
            Vector3 right = seatRotation * Vector3.right;
            float half = (groupHandCards.Count - 1) * 0.5f * width * HandRowSpacing;
            Vector3 anchor = leader + (right * (half + (width * HandRowSpacing)));
            anchor.y = leader.y;
            return anchor;
        }

        // POL-7, at the start of a group drag: the selected hand cards come along.
        private void CollectGroupHandCards()
        {
            groupHandCards.Clear();
            for (int i = 0; i < toolSelection.Count; i++)
            {
                ToolPick pick = toolSelection[i];
                if (!pick.IsPile && pick.View is CardView card && IsSelectableHandCard(card)) groupHandCards.Add(card);
            }
        }
    }
}
