using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    public sealed class PopupActionRowView : ReusableUiView, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        // Board 9 (owner, 2026-10-30): each row shows its key on the right and, under the pointer, a teal fill with a
        // cyan bar on the left; Delete is red. All optional: a row made before board 9 works as before.
        [SerializeField] private GameObject keyChip;
        [SerializeField] private Text keyLabel;
        [SerializeField] private GameObject hoverBar;
        [SerializeField] private Color deleteText = new Color(1f, 0.478f, 0.420f, 1f);
        private Color plainText;
        private bool plainTextKnown;
        // POP-2 (owner approved Popups v2, 2026-10-29): Delete asks again in place, on its own row (no dialog).
        [SerializeField] private Color confirmFill = new Color(0.478f, 0.118f, 0.086f, 1f);
        [SerializeField] private Color confirmText = new Color(1f, 0.890f, 0.875f, 1f);
        [SerializeField, Min(0.5f)] private float confirmSeconds = 3f;

        private PopupActionOption option;
        private bool bound;
        private bool armed;
        private float armedAt;
        private Color idleFill;
        private Color idleText;

        public void ValidateReferences()
        {
            if (button == null || label == null)
            {
                throw new InvalidOperationException(
                    "PopupActionRowView requires its authored Button and label references.");
            }
        }

        public void Bind(PopupActionOption option)
        {
            RequireAcquired();
            ValidateReferences();
            Unbind();
            FitText.Shrink(label); // MP-P: a long label shrinks to fit its button instead of running past it
            label.text = option.Label;
            if (!plainTextKnown)
            {
                plainText = label.color;
                plainTextKnown = true;
            }

            label.color = keyChip != null && option.Label.StartsWith("Delete", StringComparison.Ordinal) ? deleteText : plainText;
            string key = KeyFor(option.Label);
            if (keyChip != null) keyChip.SetActive(key.Length > 0);
            if (keyLabel != null) keyLabel.text = key;
            if (hoverBar != null) hoverBar.SetActive(false);
            button.interactable = option.Enabled;
            this.option = option;
            bound = true;
            button.onClick.AddListener(Press);
        }

        private void Press()
        {
            if (!bound) return;
            if (!option.Label.StartsWith("Delete", StringComparison.Ordinal))
            {
                option.Selected.Invoke();
                return;
            }

            if (!armed)
            {
                armed = true;
                armedAt = Time.unscaledTime;
                if (button.targetGraphic != null)
                {
                    idleFill = button.targetGraphic.color;
                    button.targetGraphic.color = confirmFill;
                }

                idleText = label.color;
                label.color = confirmText;
                label.text = $"{option.Label}? Click again";
                return;
            }

            Disarm();
            option.Selected.Invoke();
        }

        private void Disarm()
        {
            if (!armed) return;
            armed = false;
            if (button != null && button.targetGraphic != null) button.targetGraphic.color = idleFill;
            if (label != null)
            {
                label.color = idleText;
                if (bound) label.text = option.Label;
            }
        }

        private void Update()
        {
            if (armed && Time.unscaledTime - armedAt > confirmSeconds) Disarm();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hoverBar != null && button != null && button.interactable) hoverBar.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hoverBar != null) hoverBar.SetActive(false);
        }

        /// <summary>The key that does this over the piece (the table's own keys), or empty.</summary>
        public static string KeyFor(string action)
        {
            if (string.IsNullOrEmpty(action)) return string.Empty;
            if (action.Length == 6 && Starts(action, "Draw ") && char.IsDigit(action[5])) return action.Substring(5, 1);
            if (action == "Draw…" || action == "Draw...") return "1–9";
            if (Starts(action, "Deal")) return "T";
            if (action == "Shuffle" || Starts(action, "Shuffle the")) return "R";
            if (Starts(action, "Roll")) return "R";
            if (Starts(action, "Flip")) return "F";
            if (Starts(action, "Rotate")) return "Q / E";
            if (Starts(action, "Delete")) return "Del";
            return string.Empty;
        }

        /// <summary>
        /// Board 9: which group a piece's action sits in (0 PLAY, 1 ARRANGE, 2 TABLE), or -1 for one that is not a
        /// piece action (a game's question, OK, Not yet): a menu with any of those shows no groups.
        /// </summary>
        public static int GroupOf(string action)
        {
            if (string.IsNullOrEmpty(action)) return -1;
            if (Starts(action, "Shuffle as") || Starts(action, "Flourish") || Starts(action, "Spread")
                || Starts(action, "Split") || Starts(action, "Look through") || Starts(action, "Merge")
                || Starts(action, "Gather") || Starts(action, "Sort") || Starts(action, "Fan") || Starts(action, "Stack")
                || Starts(action, "Deal another way"))
            {
                return 1;
            }

            if (Starts(action, "Draw") || Starts(action, "Deal") || Starts(action, "Shuffle") || Starts(action, "Flip")
                || Starts(action, "Roll") || Starts(action, "Set the face") || Starts(action, "Inspect")
                || Starts(action, "Into") || Starts(action, "Take") || Starts(action, "Play") || Starts(action, "Reveal")
                || Starts(action, "Hide") || Starts(action, "Show") || Starts(action, "Pick up"))
            {
                return 0;
            }

            if (Starts(action, "Lock") || Starts(action, "Unlock") || Starts(action, "Duplicate") || Starts(action, "Move")
                || Starts(action, "Delete") || Starts(action, "Rotate") || Starts(action, "Columns")
                || Starts(action, "Rows") || Starts(action, "Resize") || Starts(action, "Copy")
                || Starts(action, "Remove"))
            {
                return 2;
            }

            return -1;
        }

        private static bool Starts(string text, string start) => text.StartsWith(start, StringComparison.Ordinal);

        public override void Unbind()
        {
            Disarm();
            bound = false;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }

            if (label != null)
            {
                label.text = string.Empty;
            }
        }
    }

    public readonly struct PopupActionOption
    {
        public PopupActionOption(string label, bool enabled, Action selected)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("A popup action requires a label.", nameof(label));
            }

            Label = label;
            Enabled = enabled;
            Selected = selected ?? throw new ArgumentNullException(nameof(selected));
        }

        public string Label { get; }

        public bool Enabled { get; }

        public Action Selected { get; }
    }
}
