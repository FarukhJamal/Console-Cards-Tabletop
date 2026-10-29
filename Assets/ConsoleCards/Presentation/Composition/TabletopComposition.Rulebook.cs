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
using ConsoleCards.Presentation.TableSurface;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// BOOK-1 (owner, 2026-10-30): the rulebook as a book lying closed by your seat (Resources/Table/TableRulebook). Its
    /// click, the HUD's Rulebook button and RULES in the Esc menu open it in front of the camera; while it is read the
    /// camera and the table's own input wait. Without its prefab, or with the V5 HUD, the screen rulebook opens as
    /// before.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        [Tooltip("BOOK-1: how far beside your Console the closed rulebook lies (world units, to your right).")]
        [SerializeField, Min(0f)] private float rulebookBesideConsole = 0.9f;

        [Tooltip("Board 7B: how far left of the game's board the closed rulebook lies (world units).")]
        [SerializeField, Min(0f)] private float rulebookBesideBoard = 0.3f;

        [Tooltip("Board 7B: how far the closed rulebook is turned on the table (degrees; negative turns it left).")]
        [SerializeField] private float rulebookTurn = -12f;

        private TableRulebookObject rulebookObject;
        private bool rulebookObjectMissing;
        private bool rulebookCameraWasOn;
        private bool rulebookObjectInputWasOn;
        private bool rulebookInputWasOn;
        private bool rulebookReading;
        private Func<bool> openRulebookObject;
        private Action<RulesCardModel> rebindRulebookObject;

        /// <summary>Puts the book by this screen's seat with these rules (null rules or the V5 HUD: no book).</summary>
        internal void RefreshRulebookObject(RulesCardModel rules)
        {
            if (runtimeUi == null) return;
            if (rules == null || !runtimeUi.IsTableHudShown || !EnsureRulebookObject())
            {
                if (rulebookObject != null)
                {
                    rulebookObject.PutDownNow();
                    rulebookObject.gameObject.SetActive(false);
                }

                runtimeUi.SetPhysicalRulebook(null, null);
                return;
            }

            rulebookObject.gameObject.SetActive(true);
            rulebookObject.Bind(rules);
            PlaceRulebookObject();
            runtimeUi.SetPhysicalRulebook(
                openRulebookObject ??= OpenRulebookObject,
                rebindRulebookObject ??= RebindRulebookObject);
        }

        private bool EnsureRulebookObject()
        {
            if (rulebookObject != null) return true;
            if (rulebookObjectMissing) return false;
            TableRulebookObject prefab = Resources.Load<TableRulebookObject>(TableRulebookObject.ResourcePath);
            if (prefab == null || !prefab.IsComplete)
            {
                rulebookObjectMissing = true;
                Debug.LogWarning(
                    "The table rulebook (a book by your seat) is missing, so the screen rulebook opens: run Console Cards > "
                    + "UI > Create Missing Table HUD (HUD-B).");
                return false;
            }

            rulebookObject = Instantiate(prefab);
            rulebookObject.name = "TableRulebook";
            rulebookObject.Wire(
                targetCamera,
                IsRulebookObjectBlocked,
                () => OpenRulebookObject(),
                SetRulebookReading,
                () => runtimeUi?.OpenScreenRulebook());
            return true;
        }

        private bool OpenRulebookObject()
        {
            if (rulebookObject == null || !rulebookObject.gameObject.activeInHierarchy || targetCamera == null) return false;
            runtimeUi?.ClosePanelsForBook();
            rulebookObject.Open(targetCamera);
            return true;
        }

        private void RebindRulebookObject(RulesCardModel rules)
        {
            if (rulebookObject != null && rules != null) rulebookObject.Bind(rules);
        }

        /// <summary>A rebuilt or closed table: the book lies down at once and the input comes back first.</summary>
        internal void PutDownRulebookObject()
        {
            if (rulebookObject != null) rulebookObject.PutDownNow();
            else if (rulebookReading) SetRulebookReading(false);
        }

        internal bool IsReadingRulebook => rulebookReading;

        private bool IsRulebookObjectBlocked() => runtimeUi == null || runtimeUi.IsTableCovered;

        // While the book is read, the camera keys (A / D turn the pages) and the table's input wait.
        private void SetRulebookReading(bool reading)
        {
            if (reading == rulebookReading) return;
            rulebookReading = reading;
            runtimeUi?.SetPhysicalRulebookReading(reading);
            if (reading)
            {
                rulebookCameraWasOn = cameraInputAdapter != null && cameraInputAdapter.enabled;
                rulebookObjectInputWasOn = objectInputAdapter != null && objectInputAdapter.enabled;
                rulebookInputWasOn = inputFrameCoordinator != null && inputFrameCoordinator.enabled;
                if (inputFrameCoordinator != null) inputFrameCoordinator.enabled = false;
                if (objectInputAdapter != null) objectInputAdapter.enabled = false; // the coordinator lets go of both
                if (cameraInputAdapter != null) cameraInputAdapter.enabled = false;
                return;
            }

            // The adapters first, then the coordinator (it takes them over again) when the table still has it on.
            if (cameraInputAdapter != null && rulebookCameraWasOn) cameraInputAdapter.enabled = true;
            if (objectInputAdapter != null && rulebookObjectInputWasOn) objectInputAdapter.enabled = true;
            if (inputFrameCoordinator != null && rulebookInputWasOn && frameCoordinatorEnabledByComposition)
            {
                inputFrameCoordinator.enabled = true;
            }
        }

        // Board 7B: left of the game's board (its playmat) as you sit, by its edge nearest you, turned a little. Where
        // Consoles leave no room: to the right of this screen's Console, facing the player; on a table without seats, near
        // the screen's lower right corner.
        private void PlaceRulebookObject()
        {
            if (TryGetBoardRulebookPose(out Vector3 position, out Quaternion rotation)
                || TryGetSeatRulebookPose(out position, out rotation)
                || TryGetCornerRulebookPose(out position, out rotation))
            {
                rulebookObject.PlaceOnTable(position, rotation);
            }
        }

        private bool TryGetBoardRulebookPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (gameBoardPhysicalSurface == null || !gameBoardPhysicalSurface.enabled
                || !gameBoardPhysicalSurface.gameObject.activeInHierarchy
                || !TryGetSeatRulebookPose(out Vector3 seatPosition, out Quaternion facing))
            {
                return false;
            }

            Bounds board = gameBoardPhysicalSurface.bounds;
            if (templateBoardMat != null && templateBoardMat.gameObject.activeInHierarchy)
            {
                Renderer[] mat = templateBoardMat.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < mat.Length; i++)
                {
                    if (mat[i].enabled) board.Encapsulate(mat[i].bounds);
                }
            }

            Vector3 right = facing * Vector3.right;
            Vector3 forward = facing * Vector3.forward;
            float halfAcross = (Mathf.Abs(right.x) * board.extents.x) + (Mathf.Abs(right.z) * board.extents.z);
            float halfDeep = (Mathf.Abs(forward.x) * board.extents.x) + (Mathf.Abs(forward.z) * board.extents.z);
            float bookHalf = rulebookObject.transform.lossyScale.x * 0.65f;
            float bookHalfDeep = rulebookObject.transform.lossyScale.z * 0.82f;
            Vector3 centre = board.center;
            centre.y = seatPosition.y;
            rotation = facing * Quaternion.Euler(0f, rulebookTurn, 0f);
            Vector3 beside = centre - (right * (halfAcross + rulebookBesideBoard + bookHalf));
            float reach = Mathf.Max(bookHalf, bookHalfDeep) + 0.1f;

            // By the board's near edge; where a Console is in the way, a little further toward you.
            for (int step = 0; step < 8; step++)
            {
                position = beside - (forward * (halfDeep - (bookHalfDeep * 0.4f) + (step * bookHalfDeep * 0.5f)));
                if (IsClearOfConsoles(position, reach)) return true;
            }

            return false;
        }

        // Not on any Console (its pieces' outline, widened by the book's reach).
        private bool IsClearOfConsoles(Vector3 position, float reach)
        {
            for (int i = 0; i < Registry.playerConsoleViews.Count; i++)
            {
                ConsoleView view = Registry.playerConsoleViews[i];
                if (view == null) continue;
                Renderer[] renderers = view.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds bounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                if (Mathf.Abs(position.x - bounds.center.x) < bounds.extents.x + reach
                    && Mathf.Abs(position.z - bounds.center.z) < bounds.extents.z + reach)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryGetSeatRulebookPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (playerLayout == null || coordinateConverter == null || playerLayout.Seats.Count == 0
                || !TryGetSeatIndex(localPlayerId, out int seatIndex)
                || !playerLayout.TryGetSeat(seatIndex, out PlayerSeatLayoutEntry seat))
            {
                return false;
            }

            Vector3 centre = Vector3.zero;
            for (int i = 0; i < playerLayout.Seats.Count; i++)
            {
                centre += coordinateConverter.ToWorldPosition(playerLayout.Seats[i].PlayerZonePose.Position);
            }

            centre /= playerLayout.Seats.Count;
            Vector3 zone = coordinateConverter.ToWorldPosition(seat.PlayerZonePose.Position);
            Vector3 console = coordinateConverter.ToWorldPosition(seat.ConsoleAnchorPose.Position);
            Vector3 towardCentre = centre - zone;
            towardCentre.y = 0f;
            if (towardCentre.sqrMagnitude < 0.0001f) return false;
            rotation = Quaternion.LookRotation(towardCentre.normalized, Vector3.up);
            Vector3 right = rotation * Vector3.right;
            float consoleHalf = ConsoleHalfWidth(console, right);
            float bookHalf = rulebookObject.transform.lossyScale.x * 0.65f;
            position = console + (right * (consoleHalf + rulebookBesideConsole + bookHalf));
            position.y = console.y;
            return true;
        }

        // Half the width, along this direction, of the Console nearest the point (0 without one).
        private float ConsoleHalfWidth(Vector3 near, Vector3 along)
        {
            float best = float.MaxValue;
            float half = 0f;
            for (int i = 0; i < Registry.playerConsoleViews.Count; i++)
            {
                ConsoleView view = Registry.playerConsoleViews[i];
                if (view == null) continue;
                float distance = (view.transform.position - near).sqrMagnitude;
                if (distance >= best) continue;
                Renderer[] renderers = view.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds bounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                best = distance;
                half = (Mathf.Abs(along.x) * bounds.extents.x) + (Mathf.Abs(along.z) * bounds.extents.z);
            }

            return half;
        }

        private bool TryGetCornerRulebookPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            UnityCamera eye = targetCamera;
            if (eye == null) return false;
            Ray ray = eye.ViewportPointToRay(new Vector3(0.84f, 0.22f, 0f));
            Plane table = new Plane(Vector3.up, new Vector3(0f, tabletopHeight, 0f));
            if (!table.Raycast(ray, out float distance)) return false;
            position = ray.GetPoint(distance);
            Vector3 forward = eye.transform.forward;
            forward.y = 0f;
            rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
            return true;
        }
    }
}
