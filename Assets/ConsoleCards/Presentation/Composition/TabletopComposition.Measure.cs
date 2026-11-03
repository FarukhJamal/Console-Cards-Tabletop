using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// HUD-B (owner, 2026-10-30: "everything the same as the reference"; design board 7B, the rail): the Measure tool
    /// (M) and the camera tool (Space + drag, or its rail button). With Measure a drag on the table draws a dashed
    /// line from where it started, with its length (in cm, and in card widths) in the middle; the line stays until
    /// the next measure or another tool. It is a view helper only: not table state, not saved, not in Undo. The
    /// line and the words are copies of the Draw tool's line and label prefabs (the board feedback set). The camera
    /// drag is done by the input frame (ConfigurePointerTools); V, P and M choose Grab, Ping and Measure.
    /// </summary>
    public sealed partial class TabletopComposition
    {
        private const float MeasureLiftCards = 0.12f; // above the mats and boards (in card widths)
        private const float CardWidthCm = 6.3f; // a poker card
        private static readonly Color MeasureColour = new Color(0.024f, 0.937f, 0.941f, 1f);

        private LineRenderer measureLine;
        private TextMesh measureLabel;
        private bool measuring;
        private Vector3 measureStart;
        private Vector3 measureEnd;
        private float measureShownLength = -1f;

        // The left button belongs to the tool (not the pieces) with Pan or Measure, or while Space is held.
        private bool PointerToolTakesLeft() =>
            activeTool == TableToolKind.Pan || activeTool == TableToolKind.Measure || SpaceHeldForCamera();

        private bool PointerToolPans() => activeTool == TableToolKind.Pan || SpaceHeldForCamera();

        private static bool SpaceHeldForCamera() =>
            Keyboard.current != null && Keyboard.current.spaceKey.isPressed && !TableKeys.TypingInUi;

        // V, P and M choose Grab, Ping and Measure (board 7B's rail keys).
        private void HandleToolKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed) return;
            if (runtimeUi != null && (runtimeUi.IsPauseMenuOpen || runtimeUi.IsTableSettingsOpen)) return;
            if (HandleDrawKeys(keyboard)) return; // board 17: D, and 1 2 3 E while drawing
            if (keyboard.vKey.wasPressedThisFrame) ChooseTool(TableToolKind.Grab);
            else if (keyboard.pKey.wasPressedThisFrame) ChooseTool(TableToolKind.Ping);
            else if (keyboard.mKey.wasPressedThisFrame) ChooseTool(TableToolKind.Measure);
        }

        private void UpdateMeasure()
        {
            if (activeTool != TableToolKind.Measure || SpaceHeldForCamera())
            {
                measuring = false;
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                // QA: a click in another window (this game in the background) started a measure here.
                if (!UnityEngine.Application.isFocused) return;
                EventSystem events = EventSystem.current;
                if (events != null && events.IsPointerOverGameObject()) return;
                if (!TryProjectFlickPoint(screen, out measureStart)) return;
                measuring = true;
                measureEnd = measureStart;
                measureShownLength = -1f;
                ShowMeasure();
                SendMeasureLive(true);
                return;
            }

            if (!measuring) return;
            if (!mouse.leftButton.isPressed)
            {
                measuring = false; // the line stays until the next measure or another tool
                SendMeasureLive(true);
                return;
            }

            if (TryProjectFlickPoint(screen, out Vector3 end) && (end - measureEnd).sqrMagnitude > 0.000001f)
            {
                measureEnd = end;
                ShowMeasure();
                SendMeasureLive(false);
            }
        }

        private void ShowMeasure()
        {
            if (!EnsureMarkSet()) return;
            if (measureLine == null) measureLine = NewMeasureLine("Measure");
            if (measureLabel == null) measureLabel = NewMeasureLabel("Measure Length");
            DrawMeasure(measureLine, measureLabel, measureStart, measureEnd, MeasureColour, ref measureShownLength);
        }

        private LineRenderer NewMeasureLine(string name)
        {
            LineRenderer line = Instantiate(markSet.TableMarkLine, MarkRoot());
            line.name = name;
            line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.positionCount = 2;
            line.sortingOrder = 2;
            if (markSet.TableMarkDashMaterial != null)
            {
                line.sharedMaterial = markSet.TableMarkDashMaterial;
                line.textureMode = LineTextureMode.Tile;
            }

            return line;
        }

        private TextMesh NewMeasureLabel(string name)
        {
            TextMesh label = Instantiate(markSet.TableMarkLabel, MarkRoot());
            label.name = name;
            return label;
        }

        private void DrawMeasure(LineRenderer line, TextMesh label, Vector3 start, Vector3 end, Color colour, ref float shownLength)
        {
            float card = Mathf.Max(0.0001f, WorldPerCardWidth());
            float width = card * 0.05f;
            line.widthMultiplier = width;
            line.textureScale = new Vector2(1f / (4f * width), 1f);
            line.startColor = colour;
            line.endColor = colour;
            Vector3 up = Vector3.up * (card * MeasureLiftCards);
            line.SetPosition(0, start + up);
            line.SetPosition(1, end + up);
            if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);

            Vector3 flat = end - start;
            flat.y = 0f;
            float length = flat.magnitude;
            float shown = Mathf.Round(length / card * CardWidthCm * 10f) / 10f;
            if (!Mathf.Approximately(shown, shownLength))
            {
                shownLength = shown;
                float cards = length / card;
                label.text = $"{shown:0.0} cm · {cards:0.0} cards";
            }

            label.color = colour;
            float yaw = targetCamera != null ? targetCamera.transform.eulerAngles.y : 0f;
            Vector3 middle = ((start + end) * 0.5f) + (Vector3.up * (card * MeasureLiftCards * 1.5f));
            label.transform.SetPositionAndRotation(middle, Quaternion.Euler(90f, yaw, 0f));
            float scale = card * 0.6f / LabelPrefabLetterHeight;
            label.transform.localScale = new Vector3(scale, scale, scale);
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
        }

        private void HideMeasure()
        {
            bool wasShown = measureLine != null && measureLine.gameObject.activeSelf;
            measuring = false;
            if (measureLine != null) measureLine.gameObject.SetActive(false);
            if (measureLabel != null) measureLabel.gameObject.SetActive(false);
            if (wasShown && IsOnlineTable) online.SendMeasure(localPlayerId, Vector3.zero, Vector3.zero, false);
        }

        // ---------- the others' measures (owner, 2026-11-03: "the tools on the right should sync in multiplayer") ----------

        private const float MeasureSendSeconds = 0.08f;
        private float measureSentAt = -1f;

        private void SendMeasureLive(bool now)
        {
            if (!IsOnlineTable) return;
            if (!now && Time.unscaledTime - measureSentAt < MeasureSendSeconds) return;
            measureSentAt = Time.unscaledTime;
            online.SendMeasure(localPlayerId, measureStart, measureEnd, true);
        }

        private sealed class RemoteMeasure
        {
            public LineRenderer Line;
            public TextMesh Label;
            public float ShownLength = -1f;
        }

        private readonly Dictionary<PlayerId, RemoteMeasure> remoteMeasures = new Dictionary<PlayerId, RemoteMeasure>();

        /// <summary>Another player's measure, in their seat's colour (hidden when they put the tool away).</summary>
        internal void ShowRemoteMeasure(PlayerId player, Vector3 start, Vector3 end, bool visible)
        {
            if (!IsInitialized || !EnsureMarkSet()) return;
            if (!remoteMeasures.TryGetValue(player, out RemoteMeasure measure) || measure.Line == null || measure.Label == null)
            {
                if (!visible) return;
                measure = new RemoteMeasure
                {
                    Line = NewMeasureLine("Measure (other player)"),
                    Label = NewMeasureLabel("Measure Length (other player)"),
                };
                remoteMeasures[player] = measure;
            }

            if (!visible)
            {
                measure.Line.gameObject.SetActive(false);
                measure.Label.gameObject.SetActive(false);
                return;
            }

            DrawMeasure(measure.Line, measure.Label, start, end, FeedbackColour(player), ref measure.ShownLength);
        }
    }
}
