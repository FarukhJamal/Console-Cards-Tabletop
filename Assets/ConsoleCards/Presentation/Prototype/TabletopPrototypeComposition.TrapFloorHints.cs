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
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>
    /// Trap Floor hint helpers (doc 23 §10, layer C, H3). Each helper is named by a rule's hintHelper field and
    /// runs only while a rule in play names it; the hint service ticks it each frame and asks it about every table
    /// event. The helpers explain and mark tiles; they never block a move or change game state.
    /// <list type="bullet">
    /// <item>trapfloor.reach (rule "Move up to N tiles"): reach markers while holding your pawn, and a RULE
    /// reminder while it is over a tile the rules do not allow. A landing the rules do not allow is a rule check
    /// (warning, no flip, Undo move or Keep move), not a hint.</item>
    /// <item>trapfloor.prize (rule "Standing on an Ability or Friend?"): you stand on a prize you may take.</item>
    /// <item>trapfloor.traps-waiting (rule "Reaction abilities"): a Trap waits until End Turn.</item>
    /// <item>trapfloor.collapse (the floor-turn rules): the tile the floor dice point at, marked.</item>
    /// </list>
    /// </summary>
    public sealed partial class TabletopPrototypeComposition
    {
        // R4 hints (doc 23 §9), behind the hint helper "trapfloor.reach" since H3: the tiles in reach light while
        // the active player holds their pawn, with a RULE reminder over a tile the rules do not allow. A landing the
        // rules do not allow does not flip its tile; a warning offers Undo move or Keep move. Nothing is blocked.
        private bool reachHintShown;
        private bool reachHintHeld;
        private int moveTurnRound = -1;
        private PlayerId moveTurnPlayer;
        private TrapFloorCoordinate? moveStartTile;
        private bool guidedMoveThisTurn;
        private int tooFarStateIndex = -1;
        private readonly List<TabletopObjectId> reachTileIds = new List<TabletopObjectId>();
        private readonly List<Vector3> reachPositions = new List<Vector3>();
        private static readonly Color ReachMarkerColour = new Color(0.55f, 0.95f, 0.4f, 0.9f);
        private int[,] reachSteps;
        // H3 (owner, 2026-10-15): the face-down tile of a landing the rules do not allow (it does not flip unless
        // the table keeps the move), and the tile under the held pawn for the RULE reminder while holding.
        private TabletopObjectId notAllowedLandingFloorCardId;
        private bool holdStartKnown;
        private TrapFloorCoordinate holdStart;
        private bool holdTileKnown;
        private TrapFloorCoordinate holdTile;

        // ---------- R4 hints: reach and "why" (doc 23 §9), run by the hint helpers below (H3) ----------

        // A move destination from an Ability or a Trap (Rush, Dodge, Spring, Crane) is being shown.
        private bool IsGuidedMoveShown() =>
            trapFloorAbilityResolutionState != null
            && (trapFloorAbilityResolutionState.ActiveRushAssistance != null
                || trapFloorAbilityResolutionState.ActiveDodgeAssistance != null);

        // Each player turn starts its own move count (the round and the active player name the turn).
        private void EnsureMoveTurn()
        {
            if (trapFloorTurnState == null) return;
            if (moveTurnRound == trapFloorTurnState.CurrentRound && moveTurnPlayer == trapFloorTurnState.ActivePlayerId) return;
            moveTurnRound = trapFloorTurnState.CurrentRound;
            moveTurnPlayer = trapFloorTurnState.ActivePlayerId;
            moveStartTile = null;
            guidedMoveThisTurn = false;
            tooFarStateIndex = -1;
        }

        private bool TryGetActivePawnTile(out TabletopObjectId pawnId, out TrapFloorCoordinate tile)
        {
            pawnId = TabletopObjectId.Empty;
            tile = default;
            return trapFloorTemplate != null
                && trapFloorTurnState != null
                && !trapFloorTurnState.IsCurrentFloorFailed
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && TryGetTrapFloorPlayerSetup(trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player)
                && (pawnId = player.PawnId) != TabletopObjectId.Empty
                && TryReadPawnTile(pawnId, out tile);
        }

        // True while the active player holds their own pawn (dragged or lifted). Polled each frame; no allocation.
        private bool IsActivePawnHeld()
        {
            if (trapFloorTemplate == null
                || trapFloorTurnState == null
                || trapFloorTurnState.IsCurrentFloorFailed
                || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || !TryGetTrapFloorPlayerSetup(trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player))
            {
                return false;
            }

            if (interactionStateMachine != null
                && interactionStateMachine.Phase == TabletopInteractionPhase.DraggingObject
                && interactionStateMachine.ActiveObjectId == player.PawnId)
            {
                return true;
            }

            for (int i = 0; i < pawnViews.Count; i++)
            {
                PawnView view = pawnViews[i];
                if (view == null || !view.IsBound || view.ObjectId != player.PawnId) continue;
                if (view.PhysicalObject != null && view.PhysicalObject.IsHeld) return true;
            }

            return false;
        }

        // Any pawn held or dragged (for the hint's console note when it is not the active player's).
        private bool IsAnyPawnHeld(out TabletopObjectId pawnId)
        {
            pawnId = TabletopObjectId.Empty;
            if (interactionStateMachine != null
                && interactionStateMachine.Phase == TabletopInteractionPhase.DraggingObject
                && matchState != null
                && matchState.Pawns.ContainsKey(interactionStateMachine.ActiveObjectId))
            {
                pawnId = interactionStateMachine.ActiveObjectId;
                return true;
            }

            for (int i = 0; i < pawnViews.Count; i++)
            {
                PawnView view = pawnViews[i];
                if (view != null && view.IsBound && view.PhysicalObject != null && view.PhysicalObject.IsHeld)
                {
                    pawnId = view.ObjectId;
                    return true;
                }
            }

            return false;
        }

        // Called once a frame by the reach helper while a rule in play names it.
        private void UpdateReachHint(bool hintsOn)
        {
            TabletopObjectId heldPawnId = TabletopObjectId.Empty;
            bool anyHeld = trapFloorTemplate != null && IsAnyPawnHeld(out heldPawnId);
            bool show = anyHeld
                && hintsOn
                && !IsGuidedMoveShown()
                && IsActivePawnHeld();
            if (anyHeld != reachHintHeld)
            {
                reachHintHeld = anyHeld;
                if (anyHeld && !show) LogReachSkipped(heldPawnId, hintsOn);
            }

            if (show == reachHintShown) return;
            reachHintShown = show;
            if (!show)
            {
                boardFeedback?.HideReach();
                return;
            }

            EnsureMoveTurn();
            if (guidedMoveThisTurn)
            {
                Debug.Log("[Hints] No reach lights: this turn already used an Ability or Trap move.", this);
                return;
            }

            if (!TryGetActivePawnTile(out _, out TrapFloorCoordinate current))
            {
                Debug.Log("[Hints] No reach lights: the active pawn is not over a floor tile.", this);
                return;
            }

            TrapFloorBlindMovementDirection? onlyDirection = null;
            if (TryGetActiveBlindStatus(out TrapFloorBlindStatusState blind))
            {
                if (!blind.IsDirectionResolved)
                {
                    Debug.Log("[Hints] No reach lights: roll the Blind direction first.", this);
                    return;
                }

                onlyDirection = blind.MovementDirection;
            }

            TrapFloorCoordinate start = moveStartTile ?? current;
            int allowance = MoveAllowance(out _);
            ComputeMoveSteps(start, onlyDirection);
            reachTileIds.Clear();
            for (int x = TrapFloorCoordinate.MinimumAxisValue; x <= trapFloorTemplate.GridColumns; x++)
            {
                for (int y = TrapFloorCoordinate.MinimumAxisValue; y <= trapFloorTemplate.GridRows; y++)
                {
                    int steps = reachSteps[x, y];
                    if (steps <= 0 || steps > allowance) continue;
                    if (trapFloorTemplate.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out TabletopObjectId id))
                        reachTileIds.Add(id);
                }
            }

            reachPositions.Clear();
            for (int i = 0; i < reachTileIds.Count; i++)
            {
                if (TryGetCardView(reachTileIds[i], out CardView tileView)) reachPositions.Add(tileView.transform.position);
            }

            if (EnsureBoardFeedback()) boardFeedback.ShowReach(reachPositions, ReachMarkerColour);
        }

        // Console note (once per pick-up) for why a held pawn shows no reach.
        private void LogReachSkipped(TabletopObjectId heldPawnId, bool hintsOn)
        {
            string reason;
            if (!hintsOn) reason = "Hints are off in the Table menu";
            else if (IsGuidedMoveShown()) reason = "an Ability or Trap destination is shown";
            else if (trapFloorTurnState == null || trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
                reason = "it is not a player turn";
            else if (trapFloorTurnState.IsCurrentFloorFailed) reason = "this round failed";
            else reason = $"this pawn is not the turn player's ({FormatPlayerName(trapFloorTurnState.ActivePlayerId)})";
            Debug.Log($"[Hints] No reach lights: {reason}.", this);
        }

        // The stable ID of the active player's Avatar (the card in their Main slot at setup); empty if unknown.
        private string ActiveAvatarStableId()
        {
            if (trapFloorTurnState == null
                || matchState == null
                || !TryGetTrapFloorPlayerSetup(trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player)
                || !matchState.Cards.TryGetValue(player.AvatarCardId, out CardInstanceState avatarCard)
                || !TryGetAvatarLook(avatarCard.BaseState.DefinitionId, out AvatarDefinition avatar))
            {
                return string.Empty;
            }

            return avatar.StableId;
        }

        // Tiles the active player may move this turn: the rules' "move.tiles" (default 2) or their Avatar's own
        // count (move.tiles.avatar.<id>, the Athlete), plus Overcharged or
        // minus Slow; Stuck allows none. <paramref name="why"/> names the changes, empty when there are none.
        private int MoveAllowance(out string why)
        {
            int tiles = 2;
            string modeId = trapFloorTemplate.ActiveMode.StableId;
            if (trapFloorTemplate.GameDefinition.TryGetRuleSetting(RuleSettingKeys.MoveTiles, modeId, out string raw)
                && int.TryParse(raw, out int parsed)
                && parsed >= 0)
            {
                tiles = parsed;
            }

            // An Avatar with its own count (the Athlete) uses it instead.
            string avatarId = ActiveAvatarStableId();
            if (!string.IsNullOrEmpty(avatarId)
                && trapFloorTemplate.GameDefinition.TryGetRuleSetting(
                    RuleSettingKeys.MoveTilesAvatarPrefix + avatarId, modeId, out string avatarRaw)
                && int.TryParse(avatarRaw, out int avatarTiles)
                && avatarTiles >= 0)
            {
                tiles = avatarTiles;
            }

            why = string.Empty;
            if (TryGetActiveStickyStatus(out _))
            {
                why = "STUCK";
                return 0;
            }

            if (TryGetActiveSlowStatus(out TrapFloorSlowStatusState slow) && slow.MovementModifier != 0)
            {
                why = $"{tiles}, {(slow.MovementModifier > 0 ? "OVERCHARGED" : "SLOW")} {FormatSignedModifier(slow.MovementModifier)}";
                tiles += slow.MovementModifier;
            }

            return Mathf.Max(0, tiles);
        }

        // Steps from the start along each straight line (up, down, left, right) until a hole or the edge (-1: not
        // reachable in one straight line). With a Blind direction only that line counts. Fills reachSteps; reuses it.
        private void ComputeMoveSteps(TrapFloorCoordinate start, TrapFloorBlindMovementDirection? onlyDirection)
        {
            int columns = trapFloorTemplate.GridColumns;
            int rows = trapFloorTemplate.GridRows;
            if (reachSteps == null || reachSteps.GetLength(0) != columns + 1 || reachSteps.GetLength(1) != rows + 1)
                reachSteps = new int[columns + 1, rows + 1];
            for (int x = 0; x <= columns; x++)
            {
                for (int y = 0; y <= rows; y++) reachSteps[x, y] = -1;
            }

            if (start.X < 1 || start.Y < 1 || start.X > columns || start.Y > rows) return;
            reachSteps[start.X, start.Y] = 0;
            for (int d = 0; d < 4; d++)
            {
                TrapFloorBlindMovementDirection direction = (TrapFloorBlindMovementDirection)d;
                if (onlyDirection.HasValue && onlyDirection.Value != direction) continue;
                int dx = direction == TrapFloorBlindMovementDirection.Right ? 1 : direction == TrapFloorBlindMovementDirection.Left ? -1 : 0;
                int dy = direction == TrapFloorBlindMovementDirection.Up ? 1 : direction == TrapFloorBlindMovementDirection.Down ? -1 : 0;
                int x = start.X + dx, y = start.Y + dy, step = 1;
                while (IsWalkableTile(x, y))
                {
                    reachSteps[x, y] = step++;
                    x += dx;
                    y += dy;
                }
            }
        }

        // On the grid and not a hole.
        private bool IsWalkableTile(int x, int y)
        {
            return x >= TrapFloorCoordinate.MinimumAxisValue
                && y >= TrapFloorCoordinate.MinimumAxisValue
                && x <= trapFloorTemplate.GridColumns
                && y <= trapFloorTemplate.GridRows
                && trapFloorTemplate.TryGetFloorCardId(new TrapFloorCoordinate(x, y), out TabletopObjectId id)
                && (trapFloorCollapseState == null || !trapFloorCollapseState.IsCollapsed(id));
        }

        // After the active pawn lands on another tile (owner, 2026-10-15, H3): a rule check, not a hint, so it runs
        // with hints off too. The count runs from where the pawn stood when the turn started (moving 2 then 2 is a
        // 4-tile move). Returns why the rules do not allow the move, or null; a move not allowed does not flip its
        // tile, and the turn menu offers Undo move or Keep move (nothing is blocked).
        private string CheckActivePawnMove(TrapFloorCoordinate? from, TrapFloorCoordinate to)
        {
            EnsureMoveTurn();
            notAllowedLandingFloorCardId = TabletopObjectId.Empty;
            if (!moveStartTile.HasValue)
            {
                if (!from.HasValue) return null; // the pawn came from off the grid: nothing to count from
                moveStartTile = from;
            }

            tooFarStateIndex = -1;
            if (guidedMoveThisTurn || !IsWalkableTile(to.X, to.Y)) return null;
            string problem = DescribeMoveProblem(moveStartTile.Value, to);
            if (problem == null) return null;
            tooFarStateIndex = undoHistory.CurrentStateIndex;
            return problem;
        }

        // Why the rules do not allow a move from start to a tile this turn, or null. Reuses reachSteps; call it on
        // an event or when the held pawn's tile changes, not every frame.
        private string DescribeMoveProblem(TrapFloorCoordinate start, TrapFloorCoordinate to)
        {
            if (to.Equals(start)) return null;
            TrapFloorBlindMovementDirection? onlyDirection = null;
            if (TryGetActiveBlindStatus(out TrapFloorBlindStatusState blind))
            {
                if (!blind.IsDirectionResolved) return "BLIND: roll the Blind direction before moving.";
                onlyDirection = blind.MovementDirection;
            }

            int allowance = MoveAllowance(out string why);
            if (allowance == 0 && why == "STUCK") return "STUCK: this turn you stay where you are.";
            if (!IsWalkableTile(to.X, to.Y)) return "That tile is a hole.";
            ComputeMoveSteps(start, onlyDirection);
            int steps = reachSteps[to.X, to.Y];
            if (steps < 0)
            {
                return onlyDirection.HasValue
                    ? $"BLIND: this turn you move {onlyDirection.Value.ToString().ToLowerInvariant()} only."
                    : to.X == start.X || to.Y == start.Y
                        ? "A hole is in the way; moves cannot cross holes."
                        : "Moves go in one straight line (up, down, left or right), with no turning.";
            }

            return steps > allowance
                ? $"That is {steps} tiles from where the turn started; the rules allow {allowance}"
                    + (string.IsNullOrEmpty(why) ? "." : $" ({why}).")
                : null;
        }

        // While the active player holds their pawn (hints on), the tile under it is checked whenever it changes;
        // over a tile the rules do not allow, a RULE reminder in the hint strip says why. Per frame: a position
        // lookup only; text is made only when the tile changes.
        private void UpdateHoldReminder(bool hintsOn)
        {
            bool held = hintsOn && trapFloorTemplate != null && !IsGuidedMoveShown() && IsActivePawnHeld();
            if (!held)
            {
                holdStartKnown = false;
                holdTileKnown = false;
                if (IsShownHint(HoldHintId)) HideShownHint();
                return;
            }

            if (!holdStartKnown)
            {
                EnsureMoveTurn();
                if (guidedMoveThisTurn || !TryGetActivePawnTile(out _, out TrapFloorCoordinate pickedUpFrom)) return;
                holdStart = moveStartTile ?? pickedUpFrom;
                holdStartKnown = true;
                holdTileKnown = false;
            }

            if (!TryGetHeldPawnTile(out TrapFloorCoordinate over))
            {
                holdTileKnown = false;
                if (IsShownHint(HoldHintId)) HideShownHint(); // off the board
                return;
            }

            if (holdTileKnown && over.Equals(holdTile)) return;
            holdTile = over;
            holdTileKnown = true;
            string problem = DescribeMoveProblem(holdStart, over);
            if (problem == null)
            {
                if (IsShownHint(HoldHintId)) HideShownHint();
                return;
            }

            ShowHelperHint(HelperReminder(HoldHintId, HintTriggers.PawnDragged, "own",
                $"Not allowed here: {problem} Put the pawn on a lit tile.", HoldHintPriority));
        }

        // The tile under the active player's pawn view right now (it moves while held; the match state does not).
        private bool TryGetHeldPawnTile(out TrapFloorCoordinate tile)
        {
            tile = default;
            if (coordinateConverter == null
                || matchState == null
                || !TryGetTrapFloorPlayerSetup(trapFloorTurnState.ActivePlayerId, out TrapFloorPlayerSetupDefinition player))
            {
                return false;
            }

            for (int i = 0; i < pawnViews.Count; i++)
            {
                PawnView view = pawnViews[i];
                if (view == null || !view.IsBound || view.ObjectId != player.PawnId) continue;
                return trapFloorTemplate.TryGetFloorCoordinateAt(
                    matchState,
                    coordinateConverter.ToTableCoordinate(view.transform.position),
                    out tile);
            }

            return false;
        }

        // Shows a helper's reminder in the hint strip now (not tied to an event). Reminders show with hints on.
        private void ShowHelperHint(HintEntry hint)
        {
            if (runtimeUi == null || hintService == null || hintService.Level == HintLevel.Off || hint == null) return;
            if (shownHint != null
                && runtimeUi.IsHintShown
                && shownHint.Priority > hint.Priority
                && !string.Equals(shownHint.StableId, hint.StableId, StringComparison.Ordinal))
            {
                return;
            }

            if (!runtimeUi.ShowHint("RULE", hint.Text, DismissShownHint, HideShownHint)) return;
            shownHint = hint;
        }

        // Keep move: the table agrees to a move the rules do not allow, so its face-down tile flips now.
        private void KeepNotAllowedMove()
        {
            TabletopObjectId floorCardId = notAllowedLandingFloorCardId;
            notAllowedLandingFloorCardId = TabletopObjectId.Empty;
            tooFarStateIndex = -1;
            if (!floorCardId.IsEmpty
                && trapFloorTurnState != null
                && trapFloorTurnState.Phase == TrapFloorTurnPhase.PlayerTurn
                && trapFloorTemplate.TryGetFloorCardState(matchState, floorCardId, out TrapFloorFloorCardState floorCard)
                && !floorCard.IsRevealed
                && (trapFloorCollapseState == null || !trapFloorCollapseState.IsCollapsed(floorCardId)))
            {
                landedFloorCards.Add(floorCardId);
                TriggerLandedTile(floorCardId, trapFloorTurnState.ActivePlayerId);
            }

            RefreshTrapFloorStatusUi();
        }

        // "Undo move" is offered only while the move is still the latest step in the history.
        private bool IsTooFarMoveUndoable()
        {
            return tooFarStateIndex >= 0
                && moveTurnRound == trapFloorTurnState.CurrentRound
                && moveTurnPlayer == trapFloorTurnState.ActivePlayerId
                && undoHistory.CurrentStateIndex == tooFarStateIndex;
        }

        private void UndoTooFarMove()
        {
            tooFarStateIndex = -1;
            notAllowedLandingFloorCardId = TabletopObjectId.Empty;
            UndoLatestAction();
        }

        // ---------- H3: Trap Floor hint helpers (doc 23 §10) ----------

        private const string HoldHintId = "helper.trapfloor.reach.hold";
        private const string PrizeHintId = "helper.trapfloor.prize";
        private const string TrapsWaitingHintId = "helper.trapfloor.traps-waiting";
        private const string CollapseHintId = "helper.trapfloor.collapse";
        // The holding reminder outranks every tip; the other reminders let a rule's own tip show first (twice at most).
        private const int HoldHintPriority = 8;
        private const int HelperHintPriority = 5;

        // Added once with the hint service; each runs only while a rule in play names it.
        private void AddTrapFloorHintHelpers()
        {
            hintService.AddHelper(new ReachHintHelper(this));
            hintService.AddHelper(new PrizeHintHelper(this));
            hintService.AddHelper(new TrapsWaitingHintHelper(this));
            hintService.AddHelper(new CollapseTargetHintHelper(this));
        }

        private bool IsShownHint(string stableId) =>
            shownHint != null && string.Equals(shownHint.StableId, stableId, StringComparison.Ordinal);

        private static HintEntry HelperReminder(string stableId, string trigger, string context, string text, int priority) =>
            new HintEntry(stableId, trigger, context, text, HintKind.Reminder, priority, 0);

        // Marks the Floorfall collapse target with the board feedback marker (the tile's art is never recoloured).
        private void ShowFloorfallTarget(TabletopObjectId floorCardId)
        {
            if (floorfallTargetPresenter == null || floorCardId.IsEmpty) return;
            if (EnsureBoardFeedback() && TryGetCardView(floorCardId, out CardView tileView))
                floorfallTargetPresenter.Show(floorCardId, tileView.transform.position, boardFeedback);
            else
                floorfallTargetPresenter.Clear();
        }

        private abstract class TrapFloorHintHelper : IGameHintHelper
        {
            protected readonly TabletopPrototypeComposition owner;

            protected TrapFloorHintHelper(TabletopPrototypeComposition owner, string helperId)
            {
                this.owner = owner;
                HelperId = helperId;
            }

            public string HelperId { get; }

            public virtual void Tick(HintLevel level)
            {
            }

            public virtual HintEntry Explain(string trigger, string context, HintLevel level) => null;

            public virtual void Stop()
            {
            }

            protected static bool Is(string value, string expected) =>
                string.Equals(value, expected, StringComparison.Ordinal);
        }

        // trapfloor.reach: the reach markers (doc 23 §9) and, while holding the pawn over a tile the rules do not
        // allow, a RULE reminder saying why. (The landing check itself is a rule check and runs without hints.)
        private sealed class ReachHintHelper : TrapFloorHintHelper
        {
            public ReachHintHelper(TabletopPrototypeComposition owner) : base(owner, "trapfloor.reach")
            {
            }

            public override void Tick(HintLevel level)
            {
                owner.UpdateReachHint(level != HintLevel.Off);
                owner.UpdateHoldReminder(level != HintLevel.Off);
            }

            public override void Stop()
            {
                owner.reachHintShown = false;
                owner.boardFeedback?.HideReach();
                owner.UpdateHoldReminder(false);
            }
        }

        // trapfloor.prize: the active pawn stands on a face-up Ability or Friend it may take.
        private sealed class PrizeHintHelper : TrapFloorHintHelper
        {
            public PrizeHintHelper(TabletopPrototypeComposition owner) : base(owner, "trapfloor.prize")
            {
            }

            public override HintEntry Explain(string trigger, string context, HintLevel level)
            {
                bool moved = Is(trigger, HintTriggers.PawnMoved) && Is(context, "own");
                bool revealed = Is(trigger, HintTriggers.ContentRevealed)
                    && (Is(context, nameof(TrapFloorFloorContentCategory.Ability))
                        || Is(context, nameof(TrapFloorFloorContentCategory.Friend)));
                if ((!moved && !revealed)
                    || owner.tooFarStateIndex >= 0 // the move is not allowed: say that, not "take it"
                    || (owner.pendingControllerPurchaseState != null && owner.pendingControllerPurchaseState.IsActive)
                    || !owner.TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prize))
                {
                    return null;
                }

                return HelperReminder(PrizeHintId, trigger, context,
                    $"You are standing on {prize.Content.DisplayName}. Choose {TakePrizeLabel(prize)} in the turn menu, "
                    + "or leave it: the next player to land here may take it.",
                    HelperHintPriority);
            }
        }

        // trapfloor.traps-waiting: a Trap that hit the active player waits until End Turn; a Reaction can stop it.
        private sealed class TrapsWaitingHintHelper : TrapFloorHintHelper
        {
            private readonly List<string> lines = new List<string>(4);

            public TrapsWaitingHintHelper(TabletopPrototypeComposition owner) : base(owner, "trapfloor.traps-waiting")
            {
            }

            public override HintEntry Explain(string trigger, string context, HintLevel level)
            {
                if (!Is(trigger, HintTriggers.ContentRevealed)
                    || !Is(context, nameof(TrapFloorFloorContentCategory.Trap))
                    || owner.trapFloorTurnState == null
                    || owner.trapFloorTurnState.IsCurrentFloorFailed
                    || owner.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
                {
                    return null;
                }

                lines.Clear();
                int waiting = owner.PendingEndTurnEffects(owner.trapFloorTurnState.ActivePlayerId, lines);
                if (waiting == 0) return null;
                string what = waiting == 1
                    ? $"Waiting until you end your turn: {lines[0]}"
                    : $"{waiting} effects wait until you end your turn: {string.Join(" ", lines)}";
                return HelperReminder(TrapsWaitingHintId, trigger, context,
                    $"{what} Play a Reaction (Dodge, Shield) first if you want to stop it.",
                    HelperHintPriority);
            }
        }

        // trapfloor.collapse: during the floor turn, the tile the floor dice point at is marked (and explained once
        // it collapses) until the next player turn. Reads only object references each frame; no allocation.
        private sealed class CollapseTargetHintHelper : TrapFloorHintHelper
        {
            private TrapFloorCollapseRollState rollBeforeFloorTurn;
            private TrapFloorCollapseRollState shownRoll;

            public CollapseTargetHintHelper(TabletopPrototypeComposition owner) : base(owner, "trapfloor.collapse")
            {
            }

            public override void Tick(HintLevel level)
            {
                TrapFloorCollapseState collapse = owner.trapFloorCollapseState;
                TrapFloorTurnState turn = owner.trapFloorTurnState;
                bool floorTurn = collapse != null
                    && turn != null
                    && !turn.IsCurrentFloorFailed
                    && turn.Phase == TrapFloorTurnPhase.FloorTurn;
                if (!floorTurn)
                {
                    rollBeforeFloorTurn = collapse?.LastRoll; // a roll from an earlier floor turn is not shown
                    Hide();
                    return;
                }

                TrapFloorCollapseRollState roll = collapse.LastRoll;
                if (level == HintLevel.Off || roll == null || roll == rollBeforeFloorTurn)
                {
                    Hide();
                    return;
                }

                if (roll == shownRoll) return;
                shownRoll = roll;
                owner.ShowFloorfallTarget(roll.FloorCardId);
            }

            public override HintEntry Explain(string trigger, string context, HintLevel level)
            {
                TrapFloorCollapseRollState roll = owner.trapFloorCollapseState?.LastRoll;
                if (!Is(trigger, HintTriggers.GameActivity)
                    || !Is(context, nameof(TrapFloorActivityKind.CollapsedFloor))
                    || roll == null)
                {
                    return null;
                }

                return HelperReminder(CollapseHintId, trigger, context,
                    $"The floor dice rolled {roll.XAxisResult} and {roll.YAxisResult}: column {roll.XAxisResult}, "
                    + $"row {roll.YAxisResult} is a hole now (marked). Moves cannot cross holes.",
                    HelperHintPriority);
            }

            public override void Stop()
            {
                Hide();
            }

            private void Hide()
            {
                if (shownRoll == null) return;
                shownRoll = null;
                owner.floorfallTargetPresenter?.Clear();
            }
        }
    }
}
