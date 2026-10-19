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
    /// POL-1 to POL-8 (doc 30 Part H): the polish that makes the table feel like Tabletop Simulator.
    /// <list type="bullet">
    /// <item><b>Carry (POL-1)</b>: the mouse wheel raises or lowers the carried piece; a ghost outline lies straight
    /// below it (or on the board space it will snap to) so you see where it lands.</item>
    /// <item><b>Drop (POL-2)</b>: Q / E turn the carried piece, the selection, the piece under the pointer or the
    /// selected piece (15°, 45° with Shift); pieces hitting the table and each other make a sound by material and
    /// speed, with a small puff on a hard landing.</item>
    /// <item><b>Hover (POL-3)</b>: an outline in your colour on the piece under the pointer; after a moment a tooltip
    /// (its name, owner, a pile's card count); Alt held over a card shows it large.</item>
    /// <item><b>Piles (POL-4)</b>: number keys over a deck or stack draw that many; R over it shuffles.</item>
    /// <item><b>Dice (POL-5)</b>: R over a die rolls it (or rolls the selected dice); the result floats up for a
    /// moment when it settles.</item>
    /// <item><b>Selection (POL-7)</b>: drag a selected piece and the other selected pieces come along (piles follow
    /// when it is let go); F flips, Q / E turn and Delete removes the whole selection. Grabbing a piece that is not
    /// selected drops the selection, as in Tabletop Simulator.</item>
    /// <item><b>Sound (POL-8)</b>: one sound player with voices, per-material sounds, random pitch and crowd damping.</item>
    /// </list>
    /// The numbers are in Resources/FeelSettings. Nothing is allocated per frame (texts are built when the piece
    /// under the pointer changes; lists are reused).
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const string FlickChargeSoundPath = "TableSounds/FlickCharge";
        private const string FlickSnapSoundPath = "TableSounds/FlickSnap";
        private const string RiffleSoundPath = "TableSounds/Riffle";
        private const float ImpactMuteAfterTableSeconds = 1.2f;
        private const float GroupCancelDistance = 0.02f;
        private static readonly Color DieResultColour = new Color(0.949f, 0.659f, 0.114f, 1f); // mustard

        private TableSoundPlayer tableSounds;
        private AudioClip flickChargeClip;
        private AudioClip flickSnapClip;
        private AudioClip riffleClip;
        private bool polishHooked;
        private MatchState polishMatch;
        private float impactsMutedUntil;
        private bool overlayShown;
        private bool presetSuppressionSet;

        // Hover
        private TabletopObjectView hoverView;
        private ContainerId hoverPileId;
        private bool hoverIsHandCard;
        private float hoverSince;
        private string hoverTitle;
        private string hoverDetail;
        private bool hoverTextsBuilt;
        private bool peekShown;
        private TabletopObjectId peekCardId;
        private readonly RaycastHit[] polishHits = new RaycastHit[16];

        // Group carry (POL-7)
        private TabletopObjectView lastCarried;
        private TabletopObjectView groupLeader;
        private Vector3 groupLeaderStart;
        private readonly List<PhysicalLooseObject> groupFollowers = new List<PhysicalLooseObject>();
        private readonly List<Vector3> groupOffsets = new List<Vector3>();
        private readonly List<ContainerId> groupPiles = new List<ContainerId>();
        private readonly List<float> toolSelectionSizes = new List<float>();

        // ---------- Every frame ----------

        private void UpdatePolish()
        {
            if (!IsInitialized || matchState == null) return;
            EnsurePolishHooks();
            if (!ReferenceEquals(polishMatch, matchState))
            {
                // A new table, Undo or Redo: pieces are placed again; no clatter for that.
                polishMatch = matchState;
                impactsMutedUntil = Time.unscaledTime + ImpactMuteAfterTableSeconds;
            }

            UpdateStackCarryMotion(); // POL-4
            UpdateGroupCarry();
            UpdateCarryHeightAndGhost();
            UpdateHover();
            HandlePolishKeys();
        }

        private void EnsurePolishHooks()
        {
            if (!polishHooked)
            {
                polishHooked = true;
                PhysicalLooseObject.Impact += HandlePieceImpact;
                PhysicalLooseObject.DieSettled += HandleDieSettled;
            }

            if (!presetSuppressionSet && cameraInputAdapter != null)
            {
                presetSuppressionSet = true;
                cameraInputAdapter.PresetKeysSuppressed = IsPointerOnDrawablePile;
            }
        }

        private void UnhookPolish()
        {
            if (!polishHooked) return;
            polishHooked = false;
            PhysicalLooseObject.Impact -= HandlePieceImpact;
            PhysicalLooseObject.DieSettled -= HandleDieSettled;
        }

        /// <summary>Shows the overlay (tooltips, peek, die results) with the rest of the table's UI.</summary>
        internal void RefreshTableOverlay()
        {
            if (runtimeUi == null) return;
            overlayShown = runtimeUi.ShowTableOverlay(targetCamera);
        }

        // Called from ClearGestures (a new table, shutdown): nothing carried over.
        private void ClearPolish()
        {
            EndGroupCarry(false);
            lastCarried = null;
            ClearHover();
            if (boardFeedback != null) boardFeedback.HideLanding();
            tableSounds?.StopLoop();
            UnhookPolish();
        }

        private void EnsureTableSounds()
        {
            if (tableSounds != null) return;
            tableSounds = new TableSoundPlayer(transform);
            flickChargeClip = Resources.Load<AudioClip>(FlickChargeSoundPath);
            flickSnapClip = Resources.Load<AudioClip>(FlickSnapSoundPath);
            riffleClip = Resources.Load<AudioClip>(RiffleSoundPath);
        }

        // Owner, 2026-10-19: a placed piece makes the sound of its material landing.
        private void PlayPlacementSound(TabletopComponentKind kind)
        {
            EnsureTableSounds();
            switch (kind)
            {
                case TabletopComponentKind.Card:
                    PlayCardSound(false);
                    break;
                case TabletopComponentKind.Deck:
                case TabletopComponentKind.Stack:
                case TabletopComponentKind.DiscardPile:
                    PlayCardSound(true);
                    break;
                case TabletopComponentKind.Die:
                    tableSounds.PlayImpact(TableSoundMaterial.Die, Feel.ImpactFullSpeed * 0.6f);
                    break;
                case TabletopComponentKind.Pawn:
                    tableSounds.PlayImpact(TableSoundMaterial.Wood, Feel.ImpactFullSpeed * 0.6f);
                    break;
                case TabletopComponentKind.Token:
                    tableSounds.PlayImpact(TableSoundMaterial.Chip, Feel.ImpactFullSpeed * 0.6f);
                    break;
                default:
                    tableSounds.PlayImpact(TableSoundMaterial.Soft, Feel.ImpactFullSpeed * 0.8f);
                    break;
            }
        }

        // Owner, 2026-10-19: the riffle has its own sound, timed to the animation.
        private void PlayShuffleSound()
        {
            EnsureTableSounds();
            if (riffleClip != null) tableSounds.Play(riffleClip, Feel.CardSoundVolume * 1.4f, 1f);
            else PlayCardSound(true);
        }

        // ---------- Sound (POL-8) and the landing puff (POL-2) ----------

        private void HandlePieceImpact(PhysicalLooseObject piece, float speed, Vector3 point)
        {
            if (!IsInitialized || piece == null || piece.View == null || Time.unscaledTime < impactsMutedUntil) return;
            EnsureTableSounds();
            tableSounds.PlayImpact(MaterialOf(piece.View), speed);
            float puffSpeed = Feel.ImpactPuffSpeed;
            if (puffSpeed > 0f && speed >= puffSpeed && !piece.IsHeld && EnsureBoardFeedback())
            {
                boardFeedback.Puff(point, new Color(1f, 1f, 1f, 0.6f));
            }
        }

        private static TableSoundMaterial MaterialOf(TabletopObjectView view)
        {
            if (view is DieView) return TableSoundMaterial.Die;
            if (view is PawnView) return TableSoundMaterial.Wood;
            if (view is TokenView) return TableSoundMaterial.Chip;
            return TableSoundMaterial.Card;
        }

        // ---------- Dice result (POL-5) ----------

        private void HandleDieSettled(PhysicalLooseObject die, int value)
        {
            if (!IsInitialized || die == null || die.View == null || runtimeUi == null || !overlayShown) return;
            float seconds = Feel.DieResultSeconds;
            if (seconds <= 0f) return;
            Vector3 above = PieceRestPosition(die.View) + (Vector3.up * (WorldPerCardWidth() * 0.5f));
            runtimeUi.ShowFloatLabel(above, value.ToString(), DieResultColour, seconds);
        }

        // ---------- Carry: wheel height and the landing ghost (POL-1 / POL-2) ----------

        private TabletopObjectView CurrentCarried()
        {
            TabletopObjectView view = moveCoordinator != null ? moveCoordinator.ActiveView : null;
            return view != null && view.PhysicalObject != null && view.PhysicalObject.IsHeld ? view : null;
        }

        private void UpdateCarryHeightAndGhost()
        {
            TabletopObjectView carried = CurrentCarried();
            FeelSettings feel = Feel;
            if (carried == null)
            {
                if (boardFeedback != null) boardFeedback.HideLanding();
                return;
            }

            float notches = Keys.WheelNotches;
            if (notches != 0f) carried.PhysicalObject.RaiseCarry(notches * feel.CarryRaiseStep, feel.CarryRaiseMax);

            if (!feel.ShowLandingGhost || !EnsureBoardFeedback()) return;
            Rigidbody body = carried.PhysicalObject.Body;
            if (body == null || !TryFindGround(body, body.position, out Vector3 ground))
            {
                boardFeedback.HideLanding();
                return;
            }

            float yaw = body.rotation.eulerAngles.y;
            if ((carried is CardView || carried is PawnView || carried is TokenView)
                && TryFindBoardSpace(ground, out GameBoardMatView board, out Vector3 centre))
            {
                // The board space it will be set on, squared to the board for a card.
                ground = new Vector3(centre.x, ground.y, centre.z);
                if (carried is CardView)
                {
                    float boardYaw = board.transform.eulerAngles.y;
                    yaw = boardYaw + (Mathf.Round(Mathf.DeltaAngle(boardYaw, yaw) / 90f) * 90f);
                }
            }

            Color colour = FeedbackColour(localPlayerId);
            colour.a = 0.55f;
            boardFeedback.ShowLanding(ground, yaw, colour, SizeOf(carried));
        }

        // The first surface straight below a point, skipping the carried piece and the pieces carried with it.
        private bool TryFindGround(Rigidbody carried, Vector3 origin, out Vector3 point)
        {
            point = Vector3.zero;
            int count = Physics.RaycastNonAlloc(
                new Ray(origin, Vector3.down),
                polishHits,
                100f,
                ~0,
                QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Rigidbody hitBody = polishHits[i].rigidbody;
                if (hitBody != null && (hitBody == carried || IsGroupFollowerBody(hitBody))) continue;
                if (polishHits[i].distance < best)
                {
                    best = polishHits[i].distance;
                    point = polishHits[i].point;
                }
            }

            return best < float.MaxValue;
        }

        private bool IsGroupFollowerBody(Rigidbody candidate)
        {
            for (int i = 0; i < groupFollowers.Count; i++)
            {
                if (groupFollowers[i] != null && groupFollowers[i].Body == candidate) return true;
            }

            return false;
        }

        // The outline marker's long side as authored (BoardFeedbackBuilder's MoveTarget quad: 0.72 x 0.98).
        private const float OutlineNominalExtent = 0.98f;

        // Owner, 2026-10-19 ("can't see any highlights"): the outline sits just above a card's face, or around the
        // base of anything else (it used to sit at the piece's centre, inside pawns and dice), and is sized from the
        // piece's real collider (it used to assume the authored tile size).
        private bool TryOutlineOf(TabletopObjectView view, out Vector3 position, out float yaw, out float size)
        {
            position = Vector3.zero;
            yaw = 0f;
            size = 1f;
            if (view == null || !view.IsBound) return false;
            yaw = view.transform.eulerAngles.y;
            Collider collider = view.PhysicalObject != null ? view.PhysicalObject.PhysicalCollider : null;
            if (collider == null)
            {
                position = PieceRestPosition(view);
                return true;
            }

            Bounds bounds = collider.bounds;
            bool card = view is CardView;
            float extent = card ? LongestSide(collider) : Mathf.Max(bounds.size.x, bounds.size.z);
            size = Mathf.Max(0.05f, extent * (card ? 1.08f : 1.45f) / OutlineNominalExtent);
            position = new Vector3(
                bounds.center.x,
                card ? bounds.max.y + 0.002f : bounds.min.y + 0.002f,
                bounds.center.z);
            return true;
        }

        private static float LongestSide(Collider collider)
        {
            if (collider is BoxCollider box)
            {
                Vector3 s = Vector3.Scale(box.size, box.transform.lossyScale);
                return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            }

            Bounds bounds = collider.bounds;
            return Mathf.Max(bounds.size.x, bounds.size.z);
        }

        // A pile's outline: around its top card (so it shows on top of the pile), else at its place.
        private bool TryPileOutline(ContainerId pileId, out Vector3 position, out float yaw, out float size)
        {
            if (matchState != null
                && matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                && pile.TryPeekTop(out TabletopObjectId top)
                && Registry.TryGetCardView(top, out CardView topCard)
                && TryOutlineOf(topCard, out position, out yaw, out size))
            {
                size *= 1.04f;
                return true;
            }

            size = 1.1f;
            return TryGetPileWorldPose(pileId, out position, out yaw);
        }

        private float SizeOf(TabletopObjectView view)
        {
            return TryOutlineOf(view, out _, out _, out float size) ? size : 1f;
        }

        // ---------- Group carry (POL-7) ----------

        private void UpdateGroupCarry()
        {
            TabletopObjectView carried = CurrentCarried();
            if (groupLeader == null && carried != null && !ReferenceEquals(carried, lastCarried))
            {
                if (toolSelection.Count > 0)
                {
                    int index = IndexOfPick(new ToolPick { View = carried });
                    if (index >= 0) StartGroupCarry(carried);
                    else ClearToolSelection(); // grabbing a piece that is not selected drops the selection
                }
            }

            lastCarried = carried;
            if (groupLeader == null) return;
            if (ReferenceEquals(carried, groupLeader) && groupLeader.PhysicalObject != null && groupLeader.PhysicalObject.Body != null)
            {
                Vector3 leader = groupLeader.PhysicalObject.Body.position;
                for (int i = 0; i < groupFollowers.Count; i++)
                {
                    PhysicalLooseObject follower = groupFollowers[i];
                    if (follower != null && follower.View != null && follower.View.IsBound) follower.FollowWorld(leader + groupOffsets[i]);
                }

                return;
            }

            // The leader was let go (or put back with Escape).
            bool cancelled = groupLeader.PhysicalObject == null
                || groupLeader.PhysicalObject.Body == null
                || PlanarDistance(groupLeader.PhysicalObject.Body.position, groupLeaderStart) < GroupCancelDistance;
            EndGroupCarry(!cancelled);
        }

        private void StartGroupCarry(TabletopObjectView leader)
        {
            if (leader.PhysicalObject == null || leader.PhysicalObject.Body == null) return;
            groupLeader = leader;
            Vector3 leaderPosition = leader.PhysicalObject.Body.position;
            groupLeaderStart = leaderPosition;
            groupFollowers.Clear();
            groupOffsets.Clear();
            groupPiles.Clear();
            for (int i = 0; i < toolSelection.Count; i++)
            {
                ToolPick pick = toolSelection[i];
                if (pick.IsPile)
                {
                    groupPiles.Add(pick.Pile);
                    continue;
                }

                if (ReferenceEquals(pick.View, leader)) continue;
                PhysicalLooseObject follower = pick.View != null ? pick.View.PhysicalObject : null;
                if (follower == null || follower.Body == null || pick.View.BoundState == null || pick.View.BoundState.IsUserLocked) continue;
                Vector3 offset = follower.Body.position - leaderPosition;
                if (!follower.FollowWorld(leaderPosition + offset)) continue;
                groupFollowers.Add(follower);
                groupOffsets.Add(offset);
            }
        }

        private void EndGroupCarry(bool released)
        {
            if (groupLeader == null) return;
            Vector3 moved = groupLeader.PhysicalObject != null && groupLeader.PhysicalObject.Body != null
                ? groupLeader.PhysicalObject.Body.position - groupLeaderStart
                : Vector3.zero;
            moved.y = 0f;
            for (int i = 0; i < groupFollowers.Count; i++)
            {
                PhysicalLooseObject follower = groupFollowers[i];
                if (follower == null || follower.View == null || !follower.View.IsBound || !follower.IsHeld) continue;
                if (released)
                {
                    if (!follower.Release()) follower.Cancel();
                }
                else
                {
                    follower.Cancel();
                }
            }

            if (released && moved.sqrMagnitude > 0.0001f)
            {
                for (int i = 0; i < groupPiles.Count; i++) MovePileBy(groupPiles[i], moved);
            }

            groupLeader = null;
            groupFollowers.Clear();
            groupOffsets.Clear();
            groupPiles.Clear();
        }

        // A selected pile follows the group when it is let go, gliding to its new place.
        private void MovePileBy(ContainerId pileId, Vector3 worldOffset)
        {
            if (matchState == null || !matchState.TryGetContainerPlacement(pileId, out ContainerPlacementState placement)) return;
            TabletopPose start = placement.Pose;
            Vector3 world = coordinateConverter.ToWorldPosition(start) + worldOffset;
            TabletopPose pose = new TabletopPose(
                coordinateConverter.ToTableCoordinate(world),
                start.RotationDegrees,
                start.Layer,
                start.LocalOrder);
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts = CaptureContainerCardTransforms(pileId);
            if (CommitContainerMove(pileId, pose)) AnimatePileCards(starts, false);
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt((dx * dx) + (dz * dz));
        }

        // ---------- Hover, tooltip and peek (POL-3) ----------

        private void UpdateHover()
        {
            Mouse mouse = Mouse.current;
            EventSystem eventSystem = EventSystem.current;
            bool blocked = mouse == null
                || tableModeStart != null
                || (interactionRouter != null && interactionRouter.HasActiveInteraction)
                || (componentPlacementController != null && componentPlacementController.IsActive)
                || (eventSystem != null && eventSystem.IsPointerOverGameObject())
                || hitResolver == null;
            TabletopObjectView view = null;
            ContainerId pileId = ContainerId.Empty;
            bool handCard = false;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            if (!blocked && hitResolver.TryResolve(pointer, out TabletopObjectView resolved) && resolved != null && resolved.IsBound)
            {
                if (IsSelectableLoosePiece(resolved))
                {
                    view = resolved;
                }
                else if (resolved is CardView && resolved.BoundState != null)
                {
                    ContainerId containerId = resolved.BoundState.ContainerId;
                    if (IsSelectablePile(containerId))
                    {
                        pileId = containerId;
                        view = resolved;
                    }
                    else if (!containerId.IsEmpty && containerId == handContainerId)
                    {
                        view = resolved;
                        handCard = true;
                    }
                }
            }

            bool sameTarget = ReferenceEquals(view, hoverView) && pileId == hoverPileId;
            if (!sameTarget)
            {
                hoverView = view;
                hoverPileId = pileId;
                hoverIsHandCard = handCard;
                hoverSince = Time.unscaledTime;
                hoverTextsBuilt = false;
                HidePeekAndTooltip();
            }

            if (hoverView == null)
            {
                if (boardFeedback != null) boardFeedback.HideHover();
                return;
            }

            DrawHoverOutline();
            float rested = Time.unscaledTime - hoverSince;
            bool altHeld = Keys.PingModifierHeld;
            if (altHeld && rested >= Feel.PeekDelay && ShowPeekForHover()) return;
            if (!altHeld && peekShown) HidePeek();
            if (!peekShown && rested >= Feel.TooltipDelay && overlayShown) ShowTooltipForHover(pointer);
        }

        private void DrawHoverOutline()
        {
            if (!Feel.HoverOutline || hoverIsHandCard || !EnsureBoardFeedback())
            {
                if (boardFeedback != null) boardFeedback.HideHover();
                return;
            }

            Vector3 position;
            float yaw;
            float size;
            if (!hoverPileId.IsEmpty)
            {
                if (IndexOfPick(new ToolPick { Pile = hoverPileId }) >= 0
                    || !TryPileOutline(hoverPileId, out position, out yaw, out size))
                {
                    boardFeedback.HideHover();
                    return;
                }
            }
            else
            {
                if (IndexOfPick(new ToolPick { View = hoverView }) >= 0)
                {
                    boardFeedback.HideHover(); // the selection outline is already there
                    return;
                }

                if (!TryOutlineOf(hoverView, out position, out yaw, out size))
                {
                    boardFeedback.HideHover();
                    return;
                }
            }

            Color colour = FeedbackColour(localPlayerId);
            colour.a = 0.9f;
            boardFeedback.ShowHover(position, yaw, colour, size);
        }

        private void ShowTooltipForHover(Vector2 pointer)
        {
            if (!hoverTextsBuilt)
            {
                hoverTextsBuilt = true;
                BuildHoverTexts(out hoverTitle, out hoverDetail);
            }

            if (string.IsNullOrEmpty(hoverTitle)) return;
            runtimeUi.ShowTooltip(pointer, hoverTitle, hoverDetail);
        }

        private void BuildHoverTexts(out string title, out string detail)
        {
            title = null;
            detail = null;
            if (!hoverPileId.IsEmpty && matchState.Containers.TryGetValue(hoverPileId, out ContainerState pile))
            {
                title = PileName(pile.Kind, hoverPileId);
                int count = pile.Count;
                detail = count == 1 ? "1 card" : count + " cards";
                return;
            }

            if (hoverView is CardView card)
            {
                title = VisibleCardTitle(card.ObjectId);
                detail = hoverIsHandCard
                    ? "In your hand"
                    : card.CardState != null && card.CardState.Face == CardFace.FaceUp ? "Face up" : "Face down";
                return;
            }

            if (hoverView is DieView die && die.DieState != null)
            {
                title = "Die (d" + die.DieState.SideCount + ")";
                detail = "Shows " + die.DieState.CurrentValue;
                return;
            }

            title = hoverView is PawnView ? "Pawn" : hoverView is TokenView ? "Token" : "Piece";
            PlayerId owner = hoverView.BoundState != null ? hoverView.BoundState.OwnerPlayerId : PlayerId.Empty;
            detail = owner.IsEmpty ? null : PlayerLabel(owner);
        }

        private string PileName(ContainerKind kind, ContainerId pileId)
        {
            switch (kind)
            {
                case ContainerKind.Deck: return "Deck";
                case ContainerKind.Stack: return "Stack";
                case ContainerKind.DiscardPile: return "Discard pile";
                default: return IsTokenBag(pileId) ? "Bag" : "Pile";
            }
        }

        // The card's name as it lies (a face-down card shows its back's name).
        private string VisibleCardTitle(TabletopObjectId cardId)
        {
            if (!CardInspectPanel.TryBuildCardInspectModel(cardId, out CardInspectModel model)) return "Card";
            CardInspectSideModel side = model.AuthoritativeFace == CardFace.FaceUp ? model.Front : model.Back;
            return string.IsNullOrWhiteSpace(side.Title) ? "Card" : side.Title;
        }

        // Alt held over a card (loose, the top card of a pile, or a card in your hand) shows it large.
        private bool ShowPeekForHover()
        {
            if (!overlayShown) return false;
            TabletopObjectId cardId = TabletopObjectId.Empty;
            if (!hoverPileId.IsEmpty)
            {
                if (matchState.Containers.TryGetValue(hoverPileId, out ContainerState pile)
                    && pile.Count > 0
                    && pile.TryPeekTop(out TabletopObjectId top))
                {
                    cardId = top;
                }
            }
            else if (hoverView is CardView)
            {
                cardId = hoverView.ObjectId;
            }

            if (cardId.IsEmpty) return false;
            if (peekShown && cardId == peekCardId) return true;
            if (!CardInspectPanel.TryBuildCardInspectModel(cardId, out CardInspectModel model)) return false;
            CardInspectSideModel side = model.AuthoritativeFace == CardFace.FaceUp ? model.Front : model.Back;
            runtimeUi.HideTooltip();
            runtimeUi.ShowPeek(side.Title, side.Body, side.Artwork, side.SurfaceColor, side.TextColor);
            peekShown = true;
            peekCardId = cardId;
            return true;
        }

        private void HidePeek()
        {
            if (!peekShown) return;
            peekShown = false;
            peekCardId = TabletopObjectId.Empty;
            runtimeUi?.HidePeek();
        }

        private void HidePeekAndTooltip()
        {
            HidePeek();
            if (overlayShown) runtimeUi?.HideTooltip();
        }

        private void ClearHover()
        {
            hoverView = null;
            hoverPileId = ContainerId.Empty;
            hoverIsHandCard = false;
            hoverTextsBuilt = false;
            HidePeekAndTooltip();
            if (boardFeedback != null) boardFeedback.HideHover();
        }

        // Used by the camera: number keys draw from a deck or stack under the pointer instead of changing the view.
        private bool IsPointerOnDrawablePile()
        {
            return !hoverPileId.IsEmpty
                && matchState != null
                && matchState.Containers.TryGetValue(hoverPileId, out ContainerState pile)
                && (pile.Kind == ContainerKind.Deck || pile.Kind == ContainerKind.Stack);
        }

        // ---------- Keys: Q/E, R, numbers, Delete, F for the selection (POL-2, 4, 5, 7) ----------

        private void HandlePolishKeys()
        {
            if (tableModeStart != null || IsTypingInUi()) return;
            if (componentPlacementController != null && componentPlacementController.IsActive) return;
            TableKeys keys = Keys;
            FeelSettings feel = Feel;
            bool left = keys.RotateLeftPressedThisFrame;
            bool right = keys.RotateRightPressedThisFrame;
            if (left || right)
            {
                float step = keys.ActionModifierHeld ? feel.RotateShiftStepDegrees : feel.RotateStepDegrees;
                RotateByKey(left ? -step : step);
            }

            TabletopObjectView carried = CurrentCarried();
            bool interacting = interactionRouter != null && interactionRouter.HasActiveInteraction;
            if (interacting || carried != null) return;

            if (keys.RollPressedThisFrame) RollOrShuffleByKey();
            if (keys.DealPressedThisFrame) DealByKey(); // DEAL-1

            int number = keys.NumberPressedThisFrame;
            if (number > 0 && IsPointerOnDrawablePile()) DrawCards(hoverPileId, number);

            if (keys.DeletePressedThisFrame) DeleteByKey();

            if (keys.FlipPressedThisFrame && toolSelection.Count > 0) FlipToolSelection();
        }

        private void RotateByKey(float degrees)
        {
            TabletopObjectView carried = CurrentCarried();
            if (carried != null)
            {
                carried.PhysicalObject.RotateHeld(degrees);
                return;
            }

            if (interactionRouter != null && interactionRouter.HasActiveInteraction) return;
            if (rotationCoordinator == null) return;
            if (toolSelection.Count > 0)
            {
                for (int i = 0; i < toolSelection.Count; i++)
                {
                    ToolPick pick = toolSelection[i];
                    if (pick.IsPile) RotatePileBy(pick.Pile, degrees);
                    else WithSelected(pick.View, () => rotationCoordinator.RotateSelected(degrees));
                }

                return;
            }

            if (hoverView != null && hoverPileId.IsEmpty && !hoverIsHandCard)
            {
                TabletopObjectView target = hoverView;
                WithSelected(target, () => rotationCoordinator.RotateSelected(degrees));
                return;
            }

            if (selectionState != null && selectionState.HasSelection) rotationCoordinator.RotateSelected(degrees);
        }

        private void RotatePileBy(ContainerId pileId, float degrees)
        {
            if (matchState == null || !matchState.TryGetContainerPlacement(pileId, out ContainerPlacementState placement)) return;
            TabletopPose start = placement.Pose;
            TabletopPose turned = new TabletopPose(
                start.Position,
                Mathf.Repeat(start.RotationDegrees + degrees, 360f),
                start.Layer,
                start.LocalOrder);
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts = CaptureContainerCardTransforms(pileId);
            if (CommitContainerMove(pileId, turned)) AnimatePileCards(starts, false);
        }

        // Runs a single-piece action (rotate, flip) on this piece, then puts the selection back as it was.
        private void WithSelected(TabletopObjectView view, Action action)
        {
            if (view == null || selectionState == null || !view.IsBound) return;
            TabletopObjectView previous = selectionState.SelectedView;
            selectionState.Select(view);
            try
            {
                action();
            }
            finally
            {
                if (previous != null && previous.IsBound) selectionState.Select(previous);
                else selectionState.ClearSelection();
            }
        }

        private void RollOrShuffleByKey()
        {
            // The selected dice first (as in Tabletop Simulator), then the die or pile under the pointer.
            int rolled = 0;
            for (int i = 0; i < toolSelection.Count; i++)
            {
                if (toolSelection[i].View is DieView die && die.PhysicalObject != null && die.PhysicalObject.Roll()) rolled++;
            }

            if (rolled > 0) return;
            if (hoverView is DieView hoveredDie && hoverPileId.IsEmpty)
            {
                if (hoveredDie.PhysicalObject == null || !hoveredDie.PhysicalObject.Roll()) ShowMessage("That die can't be rolled now.");
                return;
            }

            if (IsPointerOnDrawablePile()) ShuffleDeck(hoverPileId);
        }

        private void DeleteByKey()
        {
            if (toolSelection.Count > 0)
            {
                int deleted = 0;
                int kept = 0;
                SessionService.BeginUndoBatch();
                try
                {
                    for (int i = 0; i < toolSelection.Count; i++)
                    {
                        ToolPick pick = toolSelection[i];
                        bool template = pick.IsPile
                            ? matchState.IsTemplateContainer(pick.Pile)
                            : matchState.IsTemplateObject(pick.View.ObjectId);
                        if (template)
                        {
                            kept++;
                            continue;
                        }

                        TabletopComponentTarget target = pick.IsPile
                            ? TabletopComponentTarget.ForContainer(pick.Pile)
                            : TabletopComponentTarget.ForObject(pick.View.ObjectId);
                        DeleteTabletopComponentResult result = TableAuthority.Submit<DeleteTabletopComponentRequest, DeleteTabletopComponentResult>(
                            new DeleteTabletopComponentRequest(CreateCommandContext(), target));
                        if (result.Succeeded) deleted++;
                        else kept++;
                    }
                }
                finally
                {
                    SessionService.EndUndoBatch();
                }

                toolSelection.Clear();
                DrawToolSelection();
                RefreshToolBarState();
                ShowMessage(kept == 0
                    ? $"Deleted {deleted} piece{(deleted == 1 ? string.Empty : "s")}."
                    : $"Deleted {deleted}; the game's own pieces stay ({kept}).");
                return;
            }

            TabletopObjectView view = hoverView != null && hoverPileId.IsEmpty && !hoverIsHandCard
                ? hoverView
                : selectionState != null && selectionState.HasSelection ? selectionState.SelectedView : null;
            if (view == null || !IsSelectableLoosePiece(view)) return;
            if (matchState.IsTemplateObject(view.ObjectId))
            {
                ShowMessage("The game's own pieces can't be deleted.");
                return;
            }

            ContextMenus.DeleteComponent(TabletopComponentTarget.ForObject(view.ObjectId));
        }

        // F with a selection: every selected loose card flips. The selected piece itself is flipped by the
        // table's own F (it always handles the one selected piece), so it is skipped here.
        private void FlipToolSelection()
        {
            if (flipCoordinator == null || selectionState == null) return;
            TabletopObjectView handledByTable = selectionState.HasSelection && IsSelectableLoosePiece(selectionState.SelectedView)
                ? selectionState.SelectedView
                : null;
            for (int i = 0; i < toolSelection.Count; i++)
            {
                if (!(toolSelection[i].View is CardView card) || ReferenceEquals(card, handledByTable)) continue;
                WithSelected(card, () => flipCoordinator.FlipSelected());
            }
        }

        // ---------- Flick and dice-shake sounds (POL-5) ----------

        private void StartFlickChargeSound()
        {
            EnsureTableSounds();
            tableSounds.StartLoop(flickChargeClip, 0f, 0.8f);
        }

        private void SetFlickChargeSound(float strength)
        {
            tableSounds?.SetLoop(Feel.FlickChargeVolume * strength, 0.8f + (0.9f * strength));
        }

        private void StopFlickChargeSound(bool released, float strength)
        {
            if (tableSounds == null) return;
            tableSounds.StopLoop();
            if (released) tableSounds.Play(flickSnapClip, 0.25f + (0.5f * strength), 0.9f + (0.3f * strength));
        }

        private void PlayTableSound(AudioClip clip, float volume, float pitch)
        {
            EnsureTableSounds();
            tableSounds.Play(clip, volume, pitch);
        }

        // ---------- Spread (POL-4) ----------

        internal void AddSpreadAction(List<PopupActionOption> actions, ContainerId pileId)
        {
            if (!matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                || pile.Count < 2
                || (pile.Kind != ContainerKind.Deck && pile.Kind != ContainerKind.Stack && pile.Kind != ContainerKind.DiscardPile)
                || TrapFloorGame.IsAssistedControllerDeck(pileId))
            {
                return;
            }

            actions.Add(new PopupActionOption(
                "Spread",
                true,
                () =>
                {
                    ContextMenus.CloseContextMenu();
                    SpreadPile(pileId);
                }));
        }

        // Lays the pile's cards out in rows beside it, top card first, each keeping its face. One Undo step.
        // Gather them again with Select (a box) and Group into deck.
        private void SpreadPile(ContainerId pileId)
        {
            if (transferCoordinator == null
                || !matchState.Containers.TryGetValue(pileId, out ContainerState pile)
                || !matchState.TryGetContainerPlacement(pileId, out ContainerPlacementState placement))
            {
                ShowMessage("Spread unavailable.");
                return;
            }

            FeelSettings feel = Feel;
            int perRow = Mathf.Max(2, feel.SpreadRowLength);
            double spacing = feel.SpreadSpacing;
            TabletopPose origin = placement.Pose;
            // Collected first: every transfer changes the pile.
            IReadOnlyList<TabletopObjectId> ids = pile.ObjectIds;
            List<CardView> cards = new List<CardView>(ids.Count);
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                if (Registry.TryGetCardView(ids[i], out CardView card)) cards.Add(card);
            }

            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts = CaptureContainerCardTransforms(pileId);
            int moved = 0;
            SessionService.BeginUndoBatch();
            try
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    int row = i / perRow;
                    int column = i % perRow;
                    TabletopPose pose = new TabletopPose(
                        new TableCoordinate(
                            origin.Position.X + ((column + 1) * spacing),
                            origin.Position.Y - (row * spacing * 1.5d)),
                        origin.RotationDegrees,
                        origin.Layer,
                        origin.LocalOrder);
                    if (transferCoordinator.Transfer(cards[i], CardDropTarget.ForTabletop(pose)).Succeeded) moved++;
                }
            }
            finally
            {
                SessionService.EndUndoBatch();
            }

            AnimatePileCards(starts, true);
            ShowMessage(moved == cards.Count ? $"Spread {moved} cards." : $"Spread {moved} of {cards.Count} cards.");
        }
    }
}
