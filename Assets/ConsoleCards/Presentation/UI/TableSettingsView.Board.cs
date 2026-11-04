using System;
using ConsoleCards.Presentation.UI.Toolbox;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>
    /// MENU-1 to design board 6 "Settings" (owner, 2026-10-30: "everything the same as the reference"): the page's
    /// tabs GAME, LOOK, CONTROLS, SOUND, PROFILES with a cyan mark on the open one; GAME adds Show the rules card, Ask
    /// before Reset and Tips while placing (FIRST GAMES, ALWAYS, OFF); LOOK has Light and Quality (‹ ›), colour-blind
    /// seats, Hands on table, the hand as a fan and Full feedback; SOUND the room sound; PROFILES who is playing. All
    /// optional: a panel without them works as before.
    /// </summary>
    public sealed partial class TableSettingsView
    {
        [Header("Board 6 (optional)")]
        [Tooltip("The cyan mark beside each tab, shown on the open one.")]
        [SerializeField] private GameObject[] tabMarks = Array.Empty<GameObject>();
        [SerializeField] private ToolboxSwitch rulesCardSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch askResetSwitch = new ToolboxSwitch();
        [SerializeField] private GameObject tipsModeRow;
        [SerializeField] private Button[] tipsModeButtons = Array.Empty<Button>();
        [SerializeField] private Image[] tipsModeFills = Array.Empty<Image>();
        [SerializeField] private Text[] tipsModeLabels = Array.Empty<Text>();
        [SerializeField] private Color optionOn = new Color(0.110f, 0.235f, 0.239f, 1f);
        [SerializeField] private Color optionOff = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private Color optionOnWords = new Color(0.910f, 0.925f, 0.929f, 1f);
        [SerializeField] private Color optionOffWords = new Color(0.608f, 0.643f, 0.655f, 1f);
        [SerializeField] private GameObject lightRow;
        [SerializeField] private Button lightPrevious;
        [SerializeField] private Button lightNext;
        [SerializeField] private Text lightValue;
        [SerializeField] private GameObject qualityRow;
        [SerializeField] private Button qualityPrevious;
        [SerializeField] private Button qualityNext;
        [SerializeField] private Text qualityValue;
        [SerializeField] private ToolboxSwitch colourBlindSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch roomSoundSwitch = new ToolboxSwitch();
        [SerializeField] private ToolboxSwitch handFanSwitch = new ToolboxSwitch();
        [SerializeField] private Text profileLabel;

        private void BindBoard(TableSettingsModel settings)
        {
            if (rulesCardSwitch.IsComplete) BindSwitch(rulesCardSwitch, settings.RulesCardOn, settings.SetRulesCard, FlipRulesCard);
            if (askResetSwitch.IsComplete)
            {
                BindSwitch(askResetSwitch, settings.AskBeforeReset, settings.SetAskBeforeReset, FlipAskReset);
            }

            if (colourBlindSwitch.IsComplete)
            {
                BindSwitch(colourBlindSwitch, settings.ColourBlindOn, settings.SetColourBlind, FlipColourBlind);
            }

            if (roomSoundSwitch.IsComplete) BindSwitch(roomSoundSwitch, settings.RoomSoundOn, settings.SetRoomSound, FlipRoomSound);
            if (handFanSwitch.IsComplete) BindSwitch(handFanSwitch, settings.HandFanOn, settings.SetHandFan, FlipHandFan);

            if (tipsModeRow != null)
            {
                tipsModeRow.SetActive(settings.SetTipsMode != null);
                for (int i = 0; i < tipsModeButtons.Length; i++)
                {
                    if (tipsModeButtons[i] == null) continue;
                    int mode = i;
                    Clear(tipsModeButtons[i]);
                    tipsModeButtons[i].onClick.AddListener(() => PickTipsMode(mode));
                }

                ShowTipsMode(settings.TipsMode);
            }

            BindCycle(lightRow, lightPrevious, lightNext, lightValue, settings.LightText, settings.CycleLight);
            BindCycle(qualityRow, qualityPrevious, qualityNext, qualityValue, settings.QualityText, settings.CycleQuality);
            BindScreenRow();
            BindAssistRow();
            if (profileLabel != null) profileLabel.text = settings.ProfileText ?? string.Empty;
        }

        private void UnbindBoard()
        {
            if (rulesCardSwitch.IsComplete) Clear(rulesCardSwitch.Button);
            if (askResetSwitch.IsComplete) Clear(askResetSwitch.Button);
            if (colourBlindSwitch.IsComplete) Clear(colourBlindSwitch.Button);
            if (roomSoundSwitch.IsComplete) Clear(roomSoundSwitch.Button);
            if (handFanSwitch.IsComplete) Clear(handFanSwitch.Button);
            for (int i = 0; i < tipsModeButtons.Length; i++) Clear(tipsModeButtons[i]);
            Clear(lightPrevious);
            Clear(lightNext);
            Clear(qualityPrevious);
            Clear(qualityNext);
            Clear(screenPrevious);
            Clear(screenNext);
            for (int i = 0; i < assistButtons.Length; i++) Clear(assistButtons[i]);
        }

        // Owner, 2026-11-03 (doc 57): GAME › Assist, FREE / REMINDERS / FULL. Made at run time from the Tips row (same
        // three-choice look), so the settings prefab does not change.
        private GameObject assistRow;
        private Button[] assistButtons = Array.Empty<Button>();
        private Image[] assistFills = Array.Empty<Image>();
        private Text[] assistLabels = Array.Empty<Text>();
        private Text assistHint;

        private void BindAssistRow()
        {
            if (tipsModeRow == null || tipsModeButtons.Length < 3) return;
            if (assistRow == null)
            {
                assistRow = Instantiate(tipsModeRow, tipsModeRow.transform.parent, false);
                assistRow.name = "AssistRow";
                assistRow.transform.SetSiblingIndex(tipsModeRow.transform.GetSiblingIndex());
                assistButtons = new Button[3];
                assistFills = new Image[3];
                assistLabels = new Text[3];
                for (int i = 0; i < 3; i++)
                {
                    assistButtons[i] = Twin<Button>(tipsModeButtons[i]);
                    assistFills[i] = i < tipsModeFills.Length ? Twin<Image>(tipsModeFills[i]) : null;
                    assistLabels[i] = i < tipsModeLabels.Length ? Twin<Text>(tipsModeLabels[i]) : null;
                    if (assistLabels[i] != null) assistLabels[i].text = PlayAssist.Name((PlayAssistLevel)i);
                }

                Text[] texts = assistRow.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i].transform.parent != assistRow.transform) continue;
                    if (texts[i].name == "Title") texts[i].text = "Assist";
                    else if (texts[i].name == "Hint") assistHint = texts[i];
                }
            }

            assistRow.SetActive(true);
            for (int i = 0; i < assistButtons.Length; i++)
            {
                if (assistButtons[i] == null) continue;
                int level = i;
                Clear(assistButtons[i]);
                assistButtons[i].onClick.AddListener(() =>
                {
                    PlayAssist.Level = (PlayAssistLevel)level;
                    ShowAssist();
                });
            }

            ShowAssist();
        }

        private void ShowAssist()
        {
            int level = (int)PlayAssist.Level;
            for (int i = 0; i < assistButtons.Length; i++)
            {
                bool on = i == level;
                if (assistFills[i] != null) assistFills[i].color = on ? optionOn : optionOff;
                if (assistLabels[i] != null) assistLabels[i].color = on ? optionOnWords : optionOffWords;
            }

            if (assistHint != null) assistHint.text = PlayAssist.Describe(PlayAssist.Level);
        }

        // The copy of a part of the Tips row, found in the Assist row by the same path.
        private T Twin<T>(Component original) where T : Component
        {
            if (original == null || assistRow == null) return null;
            string path = original.transform.name;
            Transform walk = original.transform.parent;
            while (walk != null && walk != tipsModeRow.transform)
            {
                path = walk.name + "/" + path;
                walk = walk.parent;
            }

            if (walk == null) return null;
            Transform twin = assistRow.transform.Find(path);
            return twin != null ? twin.GetComponent<T>() : null;
        }

        // QA Q7 (2026-11-03): LOOK > Screen, full screen or a window. Made at run time from the Quality row (same
        // look, < value >), so the settings prefab does not change.
        private GameObject screenRow;
        private Button screenPrevious;
        private Button screenNext;
        private Text screenValue;

        private void BindScreenRow()
        {
            if (qualityRow == null || qualityValue == null) return;
            if (screenRow == null)
            {
                screenRow = Instantiate(qualityRow, qualityRow.transform.parent, false);
                screenRow.name = "ScreenRow";
                screenRow.transform.SetSiblingIndex(qualityRow.transform.GetSiblingIndex() + 1);
                Button[] buttons = screenRow.GetComponentsInChildren<Button>(true);
                screenPrevious = buttons.Length > 0 ? buttons[0] : null;
                screenNext = buttons.Length > 1 ? buttons[buttons.Length - 1] : null;
                string valueName = qualityValue.name;
                Text[] texts = screenRow.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    Text text = texts[i];
                    if (text.transform.parent == screenRow.transform && text.name == "Title") text.text = "Screen";
                    else if (text.transform.parent == screenRow.transform && text.name == "Hint")
                    {
                        text.text = "Full screen or a window (Alt+Enter also switches).";
                    }
                    else if (screenValue == null && text.name == valueName) screenValue = text;
                }
            }

            screenRow.SetActive(qualityRow.activeSelf);
            Clear(screenPrevious);
            Clear(screenNext);
            if (screenPrevious != null) screenPrevious.onClick.AddListener(SwitchScreenMode);
            if (screenNext != null) screenNext.onClick.AddListener(SwitchScreenMode);
            ShowScreenMode(Screen.fullScreen);
        }

        private void SwitchScreenMode()
        {
            bool full = !Screen.fullScreen;
            if (full)
            {
                Resolution native = Screen.currentResolution;
                Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                // A 16:9 window most of the screen's size, so the menus keep their shape.
                int height = Mathf.Max(720, Mathf.RoundToInt(Screen.currentResolution.height * 0.8f));
                Screen.SetResolution(Mathf.RoundToInt(height * 16f / 9f), height, FullScreenMode.Windowed);
            }

            ShowScreenMode(full);
        }

        private void ShowScreenMode(bool full)
        {
            if (screenValue != null) screenValue.text = full ? "Full screen" : "Window";
        }

        private static void BindCycle(GameObject row, Button previous, Button next, Text value, string text, Action<int> cycle)
        {
            if (row == null) return;
            row.SetActive(cycle != null);
            Clear(previous);
            Clear(next);
            if (value != null) value.text = text ?? string.Empty;
            if (cycle == null) return;
            if (previous != null) previous.onClick.AddListener(() => cycle(-1));
            if (next != null) next.onClick.AddListener(() => cycle(1));
        }

        private void PickTipsMode(int mode)
        {
            if (model?.SetTipsMode == null) return;
            model.TipsMode = mode;
            ShowTipsMode(mode);
            model.SetTipsMode(mode);
        }

        private void ShowTipsMode(int mode)
        {
            for (int i = 0; i < tipsModeButtons.Length; i++)
            {
                bool on = i == mode;
                if (i < tipsModeFills.Length && tipsModeFills[i] != null) tipsModeFills[i].color = on ? optionOn : optionOff;
                if (i < tipsModeLabels.Length && tipsModeLabels[i] != null)
                {
                    tipsModeLabels[i].color = on ? optionOnWords : optionOffWords;
                }
            }
        }

        private void FlipRulesCard() => Flip(rulesCardSwitch, ref model.RulesCardOn, model.SetRulesCard);
        private void FlipAskReset() => Flip(askResetSwitch, ref model.AskBeforeReset, model.SetAskBeforeReset);
        private void FlipColourBlind() => Flip(colourBlindSwitch, ref model.ColourBlindOn, model.SetColourBlind);
        private void FlipRoomSound() => Flip(roomSoundSwitch, ref model.RoomSoundOn, model.SetRoomSound);
        private void FlipHandFan() => Flip(handFanSwitch, ref model.HandFanOn, model.SetHandFan);
    }
}
