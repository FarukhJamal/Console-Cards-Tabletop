using System;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// Board 17 "Draw tool (right rail + bar)" (owner, 2026-10-30): the Draw tool's bar, part of the table HUD. It opens to
    /// the left of the rail, level with the Draw icon, while Draw is chosen: TOOL (pen N, highlighter K, label O,
    /// eraser X; I opens and closes it) | LINE (plain, dashed, dotted, wavy, brush, swirl) | SIZE (thin, medium, thick) | COLOUR (your seat's
    /// colour first) | Undo, CLEAR (asks again in place: CLEAR ALL?) and ✕ (back to Select). Under it one line of keys,
    /// and the label's words box while the label tool is chosen. Built by Console Cards > UI > Create Missing Table HUD.
    /// </summary>
    public sealed class TableDrawBarView : MonoBehaviour
    {
        [SerializeField] private GameObject barRoot;
        [SerializeField] private Button[] modeButtons = new Button[0];
        [SerializeField] private Image[] modeFills = new Image[0];
        [SerializeField] private Button[] styleButtons = new Button[0];
        [SerializeField] private Image[] styleFills = new Image[0];
        [SerializeField] private Button[] sizeButtons = new Button[0];
        [SerializeField] private Image[] sizeFills = new Image[0];
        [SerializeField] private Button[] swatchButtons = new Button[0];
        [SerializeField] private Image[] swatchImages = new Image[0];
        [SerializeField] private Image[] swatchRings = new Image[0];
        [SerializeField] private Button undoButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Text clearLabel;
        [SerializeField] private Image clearFill;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject keysLine;
        [SerializeField] private InputField labelInput;

        [Header("Colours")]
        [SerializeField] private Color onFill = new Color(0.110f, 0.235f, 0.239f, 1f);
        [SerializeField] private Color offFill = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color clearWords = new Color(1f, 0.478f, 0.420f, 1f);
        [SerializeField] private Color askWords = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color askFill = new Color(0.851f, 0.204f, 0.165f, 1f);

        private Action<int> chooseMode;
        private Action<int> chooseStyle;
        private Action<int> chooseSize;
        private Action<int> chooseColour;
        private Action undo;
        private Action clear;
        private Action close;
        private bool bound;

        public bool IsComplete => barRoot != null
            && modeButtons != null && modeButtons.Length >= 4
            && styleButtons != null && styleButtons.Length >= 6
            && sizeButtons != null && sizeButtons.Length >= 3
            && swatchButtons != null && swatchButtons.Length > 0
            && clearButton != null && closeButton != null && labelInput != null;

        public bool IsShown => barRoot != null && barRoot.activeSelf;

        public int SwatchCount => swatchButtons != null ? swatchButtons.Length : 0;

        /// <summary>The words typed for the next label.</summary>
        public string LabelText => labelInput != null ? labelInput.text : string.Empty;

        public void Bind(Action<int> mode, Action<int> style, Action<int> size, Action<int> colour, Action undoLine,
            Action clearAll, Action closeBar)
        {
            chooseMode = mode;
            chooseStyle = style;
            chooseSize = size;
            chooseColour = colour;
            undo = undoLine;
            clear = clearAll;
            close = closeBar;
            if (bound) return; // the listeners call through the fields above
            bound = true;
            Listen(modeButtons, i => chooseMode?.Invoke(i));
            Listen(styleButtons, i => chooseStyle?.Invoke(i));
            Listen(sizeButtons, i => chooseSize?.Invoke(i));
            Listen(swatchButtons, i => chooseColour?.Invoke(i));
            if (undoButton != null) undoButton.onClick.AddListener(() => undo?.Invoke());
            clearButton.onClick.AddListener(() => clear?.Invoke());
            closeButton.onClick.AddListener(() => close?.Invoke());
        }

        private static void Listen(Button[] buttons, Action<int> press)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                int index = i;
                buttons[i].onClick.AddListener(() => press(index));
            }
        }

        public void Show(bool shown)
        {
            if (barRoot != null && barRoot.activeSelf != shown) barRoot.SetActive(shown);
            if (keysLine != null && keysLine.activeSelf != shown) keysLine.SetActive(shown);
            if (!shown && labelInput != null && labelInput.gameObject.activeSelf) labelInput.gameObject.SetActive(false);
        }

        /// <summary>Lights the tool (0 pen, 1 highlighter, 2 label, 3 eraser); the words box shows for labels.</summary>
        public void SetMode(int mode)
        {
            Light(modeFills, mode);
            bool label = mode == 2 && IsShown;
            if (labelInput != null && labelInput.gameObject.activeSelf != label) labelInput.gameObject.SetActive(label);
            if (keysLine != null && IsShown && keysLine.activeSelf == label) keysLine.SetActive(!label);
        }

        public void SetStyle(int index) => Light(styleFills, index);

        public void SetSize(int index) => Light(sizeFills, index);

        public void SetSwatches(Color[] colours, int chosen)
        {
            for (int i = 0; i < swatchButtons.Length; i++)
            {
                bool used = colours != null && i < colours.Length;
                if (swatchButtons[i] != null && swatchButtons[i].gameObject.activeSelf != used)
                {
                    swatchButtons[i].gameObject.SetActive(used);
                }

                if (!used) continue;
                if (i < swatchImages.Length && swatchImages[i] != null) swatchImages[i].color = colours[i];
                if (i < swatchRings.Length && swatchRings[i] != null) swatchRings[i].enabled = i == chosen;
            }
        }

        /// <summary>CLEAR asks again in place: red, CLEAR ALL?</summary>
        public void SetClearAsking(bool asking)
        {
            if (clearLabel != null)
            {
                clearLabel.text = asking ? "CLEAR ALL?" : "CLEAR";
                clearLabel.color = asking ? askWords : clearWords;
            }

            if (clearFill != null) clearFill.color = asking ? askFill : offFill;
        }

        private void Light(Image[] fills, int chosen)
        {
            for (int i = 0; fills != null && i < fills.Length; i++)
            {
                if (fills[i] != null) fills[i].color = i == chosen ? onFill : offFill;
            }
        }
    }
}
