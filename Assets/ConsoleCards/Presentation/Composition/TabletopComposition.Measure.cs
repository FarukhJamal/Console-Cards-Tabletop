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
                EventSystem events = EventSystem.current;
                if (events != null && events.IsPointerOverGameObject()) return;
                if (!TryProjectFlickPoint(screen, out measureStart)) return;
                measuring = true;
                measureEnd = measureStart;
                measureShownLength = -1f;
                ShowMeasure();
                return;
            }

            if (!measuring) return;
            if (!mouse.leftButton.isPressed)
            {
                measuring = false; // the line stays until the next measure or another tool
                return;
            }

            if (TryProjectFlickPoint(screen, out Vector3 end) && (end - measureEnd).sqrMagnitude > 0.000001f)
            {
                measureEnd = end;
                ShowMeasure();
            }
        }

        private void ShowMeasure()
        {
            if (!EnsureMarkSet()) return;
            if (measureLine == null)
            {
                measureLine = Instantiate(markSet.TableMarkLine, MarkRoot());
                measureLine.name = "Measure";
                measureLine.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                measureLine.useWorldSpace = true;
                measureLine.alignment = LineAlignment.TransformZ;
                measureLine.positionCount = 2;
                measureLine.sortingOrder = 2;
                if (markSet.TableMarkDashMaterial != null)
                {
                    measureLine.sharedMaterial = markSet.TableMarkDashMaterial;
                    measureLine.textureMode = LineTextureMode.Tile;
                }
            }

            if (measureLabel == null)
            {
                measureLabel = Instantiate(markSet.TableMarkLabel, MarkRoot());
                measureLabel.name = "Measure Length";
            }

            float card = Mathf.Max(0.0001f, WorldPerCardWidth());
            float width = card * 0.05f;
            measureLine.widthMultiplier = width;
            measureLine.textureScale = new Vector2(1f / (4f * width), 1f);
            measureLine.startColor = MeasureColour;
            measureLine.endColor = MeasureColour;
            Vector3 up = Vector3.up * (card * MeasureLiftCards);
            measureLine.SetPosition(0, measureStart + up);
            measureLine.SetPosition(1, measureEnd + up);
            if (!measureLine.gameObject.activeSelf) measureLine.gameObject.SetActive(true);

            Vector3 flat = measureEnd - measureStart;
            flat.y = 0f;
            float length = flat.magnitude;
            float shown = Mathf.Round(length / card * CardWidthCm * 10f) / 10f;
            if (!Mathf.Approximately(shown, measureShownLength))
            {
                measureShownLength = shown;
                float cards = length / card;
                measureLabel.text = $"{shown:0.0} cm · {cards:0.0} cards";
            }

            measureLabel.color = MeasureColour;
            float yaw = targetCamera != null ? targetCamera.transform.eulerAngles.y : 0f;
            Vector3 middle = ((measureStart + measureEnd) * 0.5f) + (Vector3.up * (card * MeasureLiftCards * 1.5f));
            measureLabel.transform.SetPositionAndRotation(middle, Quaternion.Euler(90f, yaw, 0f));
            float scale = card * 0.6f / LabelPrefabLetterHeight;
            measureLabel.transform.localScale = new Vector3(scale, scale, scale);
            if (!measureLabel.gameObject.activeSelf) measureLabel.gameObject.SetActive(true);
        }

        private void HideMeasure()
        {
            measuring = false;
            if (measureLine != null) measureLine.gameObject.SetActive(false);
            if (measureLabel != null) measureLabel.gameObject.SetActive(false);
        }
    }
}
