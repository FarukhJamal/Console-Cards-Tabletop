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
    /// UX-1 (doc 30 A2): the cursor, gestures and the free table ping.
    /// <list type="bullet">
    /// <item><b>Table ping</b>: Alt + left click, or a quick middle-button tap, anywhere on the table. A ring pulses
    /// there in your colour for two seconds with a short beep, then fades. It only marks a spot: it never selects or
    /// moves anything, is not table state (not saved, not in Undo or the log), and no game system reads it. With
    /// multiplayer it becomes a small message to the others, not a command (A6).</item>
    /// <item><b>Flick</b>: hold Shift, press on a loose card, coin, token, pawn or die, pull back and let go. An arrow
    /// shows where it will go and how hard (teal = soft, orange = hard); the piece slides across the table. One Undo
    /// step once it rests. A tiny pull does nothing.</item>
    /// <item><b>Dice shake</b>: while carrying a die, hold Shift to shake it (it rattles in the hand); let go and it
    /// leaves tumbling, a real roll.</item>
    /// <item><b>Cursor</b>: an open hand over a piece, a closed hand while carrying, a stack mark while a press on a
    /// stack waits to pick the whole stack up, "no" over a pile that will not take the card, a target while Alt is
    /// held (ping) and a cross-hair while aiming a flick. Over the menus, the normal arrow.</item>
    /// </list>
    /// Nothing here allocates per frame: the rings and arrow are pooled, the cursor changes only when its kind does.
    /// </summary>
    public sealed partial class TabletopComposition : ITabletopFlickHandler
    {
        private const string TablePingSoundPath = "UiSounds/TablePing";
        private const string DiceShakeSoundPath = "UiSounds/DiceShake";
        // POL-0: the numbers come from Resources/FeelSettings (Feel), the keys from the Input Actions asset (Keys).
        private float TablePingSeconds => Feel.PingSeconds;
        private float TablePingVolume => Feel.PingVolume;
        private float FlickFullPullTableUnits => Feel.FlickFullPull;
        private float FlickDeadZone => Feel.FlickDeadZone;
        private float FlickMinSpeedTableUnits => Feel.FlickMinSpeed;
        private float FlickMaxSpeedTableUnits => Feel.FlickMaxSpeed;
        private float DiceRattleInterval => Feel.DiceRattleInterval;
        private float DiceRattleVolume => Feel.DiceRattleVolume;
        private static readonly Color FlickSoftColour = new Color(0.16f, 0.78f, 0.74f, 1f);  // teal
        private static readonly Color FlickHardColour = new Color(0.94f, 0.45f, 0.16f, 1f);  // orange

        private AudioClip tablePingClip;
        private AudioClip diceShakeClip;
        private bool dragTargetRejects;

        private TabletopObjectView flickView;
        private Vector3 flickAnchorWorld;
        private float flickWorldPerTableUnit = 1f;
        private Vector3 flickVelocity;

        private DieView shakingDie;
        private float nextDiceRattleAt;
        // POL-5: swinging the mouse back and forth while carrying a die shakes it too.
        private float mouseShakeEnergy;
        private float lastShakeDirection;
        private float flickStrength;

        // ---------- Table ping ----------

        // Asked by the input frame (Alt-click or a middle tap); true when a ping was shown.
        private bool TryPingTable(Vector2 screenPosition)
        {
            if (!IsInitialized || tableModeStart != null || pointerProjector == null)
            {
                return false;
            }

            if (!TryResolvePingPoint(screenPosition, out Vector3 point))
            {
                return false;
            }

            if (EnsureBoardFeedback())
            {
                boardFeedback.Ping(point, FeedbackColour(localPlayerId), TablePingSeconds);
            }

            EnsureCardSounds();
            if (tablePingClip != null) PlayTableSound(tablePingClip, TablePingVolume, 1f); // POL-8: through the sound player

            return true;
        }

        // The spot under the pointer: on top of whatever is there (a card, a board), or on the table.
        private bool TryResolvePingPoint(Vector2 screenPosition, out Vector3 point)
        {
            UnityCamera camera = hitResolver != null ? hitResolver.TargetCamera : null;
            if (camera != null
                && Physics.Raycast(camera.ScreenPointToRay(screenPosition), out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            if (pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                point = coordinateConverter.ToWorldPosition(coordinate);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        // ---------- Flick ----------

        bool ITabletopFlickHandler.WantsFlick()
        {
            // TOOLS-1: the Flick tool flicks without Shift.
            return IsInitialized
                && tableModeStart == null
                && (Keys.ActionModifierHeld || activeTool == TableToolKind.Flick);
        }

        bool ITabletopFlickHandler.TryBeginFlick(TabletopObjectView view, Vector2 screenPosition)
        {
            if (view == null
                || !view.IsBound
                || view.BoundState == null
                || view.PhysicalObject == null
                || !view.BoundState.ContainerId.IsEmpty
                || view.BoundState.IsUserLocked
                || view.PhysicalObject.IsHeld
                || (trapFloorTemplate != null && trapFloorTemplate.IsFloorCard(view.BoundState.Id))
                || !TryProjectFlickPoint(screenPosition, out Vector3 anchor))
            {
                return false;
            }

            ContextMenus.CloseContextMenu();
            flickView = view;
            flickAnchorWorld = anchor;
            flickVelocity = Vector3.zero;
            flickWorldPerTableUnit = Mathf.Max(
                0.0001f,
                Vector3.Distance(
                    coordinateConverter.ToWorldPosition(new TableCoordinate(0d, 0d)),
                    coordinateConverter.ToWorldPosition(new TableCoordinate(1d, 0d))));
            if (boardFeedback != null) boardFeedback.HideAim();
            flickStrength = 0f;
            StartFlickChargeSound(); // POL-5: a charge that rises with the pull
            return true;
        }

        void ITabletopFlickHandler.UpdateFlick(Vector2 screenPosition)
        {
            if (flickView == null) return;
            if (!flickView.IsBound || !flickView.BoundState.ContainerId.IsEmpty)
            {
                ((ITabletopFlickHandler)this).CancelFlick();
                return;
            }

            if (!TryProjectFlickPoint(screenPosition, out Vector3 pointer)) return;

            // Pull back like a slingshot: the piece goes away from the pointer.
            Vector3 pull = flickAnchorWorld - pointer;
            pull.y = 0f;
            float fullPull = FlickFullPullTableUnits * flickWorldPerTableUnit;
            float linear = Mathf.Clamp01(pull.magnitude / fullPull);
            if (linear < FlickDeadZone)
            {
                flickVelocity = Vector3.zero;
                flickStrength = 0f;
                SetFlickChargeSound(0f);
                if (boardFeedback != null) boardFeedback.HideAim();
                return;
            }

            // POL-5: a power curve, so short pulls stay gentle and long ones really go.
            float strength = Mathf.Pow(linear, Mathf.Max(0.1f, Feel.FlickCurve));
            flickStrength = strength;
            SetFlickChargeSound(strength);

            Vector3 direction = pull.normalized;
            flickVelocity = direction
                * (Mathf.Lerp(FlickMinSpeedTableUnits, FlickMaxSpeedTableUnits, strength) * flickWorldPerTableUnit);
            if (EnsureBoardFeedback())
            {
                Vector3 from = flickView.transform.position;
                Vector3 to = from + (direction * (fullPull * (0.35f + strength)));
                boardFeedback.ShowAim(from, to, Color.Lerp(FlickSoftColour, FlickHardColour, strength));
            }
        }

        void ITabletopFlickHandler.ReleaseFlick(Vector2 screenPosition)
        {
            if (boardFeedback != null) boardFeedback.HideAim();
            TabletopObjectView view = flickView;
            Vector3 velocity = flickVelocity;
            float strength = flickStrength;
            flickView = null;
            flickVelocity = Vector3.zero;
            flickStrength = 0f;
            if (view == null || !view.IsBound || view.PhysicalObject == null || velocity == Vector3.zero)
            {
                StopFlickChargeSound(false, 0f);
                return;
            }

            bool flicked = view.PhysicalObject.Flick(velocity);
            StopFlickChargeSound(flicked, strength); // POL-5: a snap on release
            if (!flicked) ShowMessage("Flick rejected.");
        }

        void ITabletopFlickHandler.CancelFlick()
        {
            flickView = null;
            flickVelocity = Vector3.zero;
            flickStrength = 0f;
            StopFlickChargeSound(false, 0f);
            if (boardFeedback != null) boardFeedback.HideAim();
        }

        private bool TryProjectFlickPoint(Vector2 screenPosition, out Vector3 point)
        {
            if (pointerProjector != null && pointerProjector.TryProjectScreenPoint(screenPosition, out TableCoordinate coordinate))
            {
                point = coordinateConverter.ToWorldPosition(coordinate);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        // ---------- Every frame: dice shake and the cursor ----------

        private void UpdateGestures()
        {
            UpdateTools(); // TOOLS-1: G, Esc, the selection outlines and the keys strip
            UpdatePolish(); // POL-1 to POL-8: carry height and ghost, group carry, hover, Q/E, R, numbers, Delete
            UpdateDiceShake();
            UpdateTableCursor();
        }

        private void UpdateDiceShake()
        {
            DieView die = moveCoordinator != null ? moveCoordinator.ActiveView as DieView : null;
            if (die == null || die.PhysicalObject == null || !die.PhysicalObject.IsHeld)
            {
                shakingDie = null;
                mouseShakeEnergy = 0f;
                return;
            }

            if (!ReferenceEquals(die, shakingDie))
            {
                shakingDie = die;
                nextDiceRattleAt = 0f;
                mouseShakeEnergy = 0f;
                lastShakeDirection = 0f;
            }

            // POL-5: each fast change of direction of the mouse adds to the shake; it dies away when the mouse calms.
            float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            Mouse mouse = Mouse.current;
            Vector2 delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            float speed = delta.magnitude / deltaTime;
            float direction = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? Mathf.Sign(delta.x) : Mathf.Sign(delta.y) * 2f;
            if (speed >= Feel.MouseShakeSpeed && direction != 0f)
            {
                if (lastShakeDirection != 0f && direction != lastShakeDirection) mouseShakeEnergy += 0.35f;
                lastShakeDirection = direction;
            }

            mouseShakeEnergy = Mathf.Clamp01(mouseShakeEnergy - (deltaTime * 1.2f));
            bool modifier = Keys.ActionModifierHeld;
            bool shake = modifier || mouseShakeEnergy > 0.2f;
            float strength = modifier ? 1f : mouseShakeEnergy;
            die.PhysicalObject.Shaking = shake;
            die.PhysicalObject.ShakeStrength = strength;
            if (!shake || Time.unscaledTime < nextDiceRattleAt) return;
            // A harder shake rattles faster and louder.
            nextDiceRattleAt = Time.unscaledTime + (DiceRattleInterval * Mathf.Lerp(1.5f, 0.75f, strength));
            EnsureCardSounds();
            if (diceShakeClip == null) return;
            PlayTableSound(diceShakeClip, DiceRattleVolume * Mathf.Lerp(0.5f, 1f, strength), UnityEngine.Random.Range(0.9f, 1.15f));
        }

        private void ClearGestures()
        {
            flickView = null;
            flickVelocity = Vector3.zero;
            if (boardFeedback != null) boardFeedback.HideAim();
            if (shakingDie != null && shakingDie.PhysicalObject != null) shakingDie.PhysicalObject.Shaking = false;
            shakingDie = null;
            mouseShakeEnergy = 0f;
            ClearTools(); // TOOLS-1: no selection carries over to a rebuilt table
            ClearPolish(); // POL-1 to POL-8: no group carry, hover or charge sound carries over
        }

        // ---------- Cursor ----------

        private enum TableCursorKind
        {
            Default = 0,
            Hand = 1,
            Grab = 2,
            Stack = 3,
            No = 4,
            Ping = 5,
            Aim = 6,
        }

        private TableCursorKind shownCursor = TableCursorKind.Default;

        private void UpdateTableCursor()
        {
            TableCursorKind kind = ResolveTableCursor();
            if (kind == shownCursor) return;
            shownCursor = kind;
            ApplyTableCursor(kind);
        }

        private TableCursorKind ResolveTableCursor()
        {
            if (!IsInitialized) return TableCursorKind.Default;
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject()) return TableCursorKind.Default;
            if (flickView != null) return TableCursorKind.Aim;
            if (IsCarryingStack) return TableCursorKind.Grab;
            if (interactionRouter != null && interactionRouter.HasActiveInteraction)
            {
                CardView pressed = containedCardDragCoordinator != null ? containedCardDragCoordinator.ActiveCardView : null;
                if (pressed != null
                    && pressed.CardState != null
                    && containedCardDragCoordinator.Phase == TabletopInteractionPhase.Pressed
                    && Registry.stackViewsByContainerId.ContainsKey(pressed.CardState.BaseState.ContainerId))
                {
                    return TableCursorKind.Stack;
                }

                return dragTargetRejects ? TableCursorKind.No : TableCursorKind.Grab;
            }

            if (componentPlacementController != null && componentPlacementController.IsActive) return TableCursorKind.Default;
            if ((Keys.PingModifierHeld || activeTool == TableToolKind.Ping) && tableModeStart == null) return TableCursorKind.Ping;
            if ((Keys.ActionModifierHeld || activeTool == TableToolKind.Flick)
                && selectionState != null
                && !selectionState.HoveredObjectId.IsEmpty)
            {
                return TableCursorKind.Aim;
            }

            if (selectionState != null && !selectionState.HoveredObjectId.IsEmpty) return TableCursorKind.Hand;
            return TableCursorKind.Default;
        }

        private void ApplyTableCursor(TableCursorKind kind)
        {
            if (kind == TableCursorKind.Default)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }

            // POL-0: the pictures and hot spots come from Resources/CursorSet (replace them there).
            CursorSet set = Cursors;
            Texture2D texture = null;
            Vector2 hotspot = Vector2.zero;
            if (set != null)
            {
                switch (kind)
                {
                    case TableCursorKind.Hand: texture = set.Hand; hotspot = set.HandHotspot; break;
                    case TableCursorKind.Grab: texture = set.Grab; hotspot = set.GrabHotspot; break;
                    case TableCursorKind.Stack: texture = set.Stack; hotspot = set.StackHotspot; break;
                    case TableCursorKind.No: texture = set.No; hotspot = set.NoHotspot; break;
                    case TableCursorKind.Ping: texture = set.Ping; hotspot = set.PingHotspot; break;
                    case TableCursorKind.Aim: texture = set.Aim; hotspot = set.AimHotspot; break;
                }
            }

            Cursor.SetCursor(texture, texture != null ? hotspot : Vector2.zero, CursorMode.Auto);
        }

        private void ResetTableCursor()
        {
            shownCursor = TableCursorKind.Default;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
