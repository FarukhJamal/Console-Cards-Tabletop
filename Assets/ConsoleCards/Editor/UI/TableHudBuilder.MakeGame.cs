using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;
using B = ConsoleCards.Editor.UI.BoardScreensBuilder;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// MAKE-B (owner, 2026-10-30: "everything the same as the reference"; design board 13 "Make a game · Save step"):
    /// Make a game in Final UI/Notices/MakeGamePanel, measured from the board (1280 x 720, x1.5). The top bar with the
    /// steps (a green mark on each step that is done) and EXIT; on the left the step, or on SAVE the game's name,
    /// players, ABOUT, one line about it and the rules one line each, with SAVE OVER and SAVE AS NEW; HOW THE RULES
    /// CARD WILL LOOK; WHAT GETS SAVED with its warning line; the three steps explained. The same GameSavePanelView.
    /// A panel built before board 13 is replaced once; after that it is kept (edit it freely).
    /// </summary>
    public static partial class TableHudBuilder
    {
        public const string MakeGamePath = Folder + "MakeGamePanel.prefab";
        private const string MakeGameLabel = "MakeGameBoard13b"; // b: the done steps' tick

        private static readonly Color Teal13 = UiKit.Hex("138a8c");
        private static readonly Color Green13 = UiKit.Hex("2fd46b");
        private static readonly Color Warn13 = UiKit.Hex("ffc93b");

        private static void BuildMakeGame()
        {
            GameSavePanelView existing = AssetDatabase.LoadAssetAtPath<GameSavePanelView>(MakeGamePath);
            if (existing != null && System.Array.IndexOf(AssetDatabase.GetLabels(existing.gameObject), MakeGameLabel) < 0)
            {
                AssetDatabase.DeleteAsset(MakeGamePath); // made before board 13 (by this builder): built again
            }

            if (!File.Exists(MakeGamePath))
            {
                B.Prepare();
                Save<GameSavePanelView>(MakeGamePath, "MakeGamePanel", BuildMakeGamePanel);
                GameObject made = AssetDatabase.LoadAssetAtPath<GameObject>(MakeGamePath);
                if (made != null) AssetDatabase.SetLabels(made, new[] { MakeGameLabel });
            }

            AssignCatalogEntry(UiPrefabIds.GameSavePanel, AssetDatabase.LoadAssetAtPath<GameSavePanelView>(MakeGamePath),
                UiLayer.Modal, UiRetention.CachedSingleInstance);
        }

        private static void BuildMakeGamePanel(RectTransform root, GameSavePanelView view)
        {
            // The view closes on its overlay; here it is hidden (the steps keep the table live under the panels).
            Button dismiss = InvisibleButton(root, "DismissOverlay");
            dismiss.gameObject.SetActive(false);

            // ---------- the top bar ----------
            RectTransform bar = B.Box(root, "TopBar", 20, 16, 1240, 60, B.Panel, B.Edge, 1f);
            Text title = B.TextAt(bar, "Title", 13, 0, 160, 60, B.bold, 15, B.Words, TextAnchor.MiddleLeft, "MAKE A GAME");
            float[] stepX = { 160, 286, 426, 545 };
            float[] stepW = { 96, 110, 96, 101 };
            string[] names = { "DECK", "CARDS", "DEAL", "SAVE" };
            Button[] stepButtons = new Button[4];
            Text[] stepLabels = new Text[4];
            GameObject[] doneMarks = new GameObject[4];
            GameObject[] numberMarks = new GameObject[4];
            Image[] pills = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                RectTransform step = B.Rect(bar, "Step" + (i + 1), stepX[i], 9, stepW[i], 42);
                Image catcher = step.gameObject.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f); // takes the clicks; the highlight below shows the step you are on
                RectTransform highlight = B.Box(step, "Highlight", 0, 0, stepW[i], 42, B.TealFill, B.Cyan, 1f);
                highlight.GetComponent<Image>().raycastTarget = false;
                pills[i] = highlight.GetComponent<Image>();
                Button button = step.gameObject.AddComponent<Button>();
                button.targetGraphic = catcher;
                button.transition = Selectable.Transition.None;
                stepButtons[i] = button;
                RectTransform done = B.Box(step, "Done", 9, 10, 22, 22, B.Well, Green13, 1f);
                Image tick = B.Rect(done, "Tick", 3, 3, 16, 16).gameObject.AddComponent<Image>();
                tick.sprite = B.check;
                tick.color = Green13;
                tick.raycastTarget = false;
                doneMarks[i] = done.gameObject;
                RectTransform number = B.Box(step, "Number", 9, 10, 22, 22, B.Cyan, null, 0f);
                number.GetComponent<Image>().sprite = B.circle;
                number.GetComponent<Image>().type = Image.Type.Simple;
                B.TextAt(number, "Digit", 0, 0, 22, 22, B.bold, 12, B.Ink, TextAnchor.MiddleCenter, (i + 1).ToString());
                numberMarks[i] = number.gameObject;
                stepLabels[i] = B.TextAt(step, "Label", 38, 0, stepW[i] - 40, 42, B.bold, 13, B.Muted, TextAnchor.MiddleLeft, names[i]);
                if (i < 3) B.TextAt(bar, "Dash" + i, stepX[i] + stepW[i] + 2, 0, 24, 60, B.medium, 13, B.Faint, TextAnchor.MiddleCenter, "–");
            }

            B.TextAt(bar, "Note", 700, 0, 464, 60, B.medium, 12, B.Muted, TextAnchor.MiddleRight,
                "Steps can be done in any order · the table stays live");
            Button exit = B.SmallButton(bar, "Exit", 1170, 12, 57, 36, "EXIT", B.Edge, B.Words);

            // ---------- the left panel: a step, or the details on SAVE ----------
            RectTransform left = B.Box(root, "Left", 20, 92, 560, 612, B.Panel, Orange, 2f);
            RectTransform details = B.Rect(left, "Details", 0, 0, 560, 612);
            Field13(details, "NameLabel", 20, 20, "GAME NAME");
            InputField nameInput = Input13(details, "Name", 20, 44, 520, 38, "[YOUR GAME NAME]", false);
            Field13(details, "PlayersLabel", 20, 92, "PLAYERS");
            Button fewerMinimum = B.SmallButton(details, "FewerMinimum", 20, 113, 35, 35, "–", B.Edge, B.Words);
            Text minimum = B.TextAt(details, "Minimum", 55, 113, 36, 35, B.bold, 16, B.Words, TextAnchor.MiddleCenter, "2");
            Button moreMinimum = B.SmallButton(details, "MoreMinimum", 91, 113, 35, 35, "+", B.Edge, B.Words);
            B.TextAt(details, "To", 126, 113, 27, 35, B.medium, 14, B.Muted, TextAnchor.MiddleCenter, "to");
            Button fewerMaximum = B.SmallButton(details, "FewerMaximum", 153, 113, 35, 35, "–", B.Edge, B.Words);
            Text maximum = B.TextAt(details, "Maximum", 188, 113, 36, 35, B.bold, 16, B.Words, TextAnchor.MiddleCenter, "4");
            Button moreMaximum = B.SmallButton(details, "MoreMaximum", 224, 113, 35, 35, "+", B.Edge, B.Words);
            Field13(details, "AboutLabel", 390, 92, "ABOUT");
            RectTransform minutes = B.Box(details, "Minutes", 390, 113, 150, 37, B.Well, B.Edge, 1f);
            Button minutesButton = minutes.gameObject.AddComponent<Button>();
            minutesButton.targetGraphic = minutes.GetComponent<Image>();
            Text minutesLabel = B.TextAt(minutes, "Label", 12, 0, 100, 37, B.medium, 14, B.Words, TextAnchor.MiddleLeft, "20 min");
            B.TextAt(minutes, "Caret", 120, 0, 24, 37, B.bold, 13, B.Muted, TextAnchor.MiddleCenter, "›");
            Field13(details, "OneLineLabel", 20, 160, "ONE LINE ABOUT IT");
            InputField about = Input13(details, "About", 20, 185, 520, 37, "Shown on the game sheet", false);
            Field13(details, "RulesLabel", 20, 232, "RULES · one line each");
            B.TextAt(details, "DragNote", 380, 232, 160, 16, B.medium, 11, B.Faint, TextAnchor.MiddleRight, "drag ⁝⁝ to reorder");
            RectTransform ruleRows = B.Rect(details, "Rules", 20, 256, 520, 252);
            SaveRuleRow template = RuleRow13(ruleRows);
            InputField newRule = Input13(ruleRows, "NewRule", 0, 0, 520, 36, "Type a rule and press Enter…", false);
            foreach (Outline outline in newRule.GetComponents<Outline>()) outline.effectColor = B.Cyan;
            Handle13(newRule.transform, B.Faint);
            foreach (Text words in newRule.GetComponentsInChildren<Text>(true))
            {
                words.rectTransform.anchoredPosition += new Vector2(30f, 0f); // after the handle
            }
            // The saved text (the rows write into it); hidden.
            InputField rules = Input13(details, "RulesText", 20, 256, 520, 36, string.Empty, true);
            rules.gameObject.SetActive(false);
            Button saveOver = Pill13(details, "SaveOver", 20, 520, 256, 48, "SAVE OVER", B.Well, B.Words, out Text saveOverLabel);
            Button saveAsNew = Pill13(details, "SaveAsNew", 284, 520, 256, 48, "SAVE AS NEW", Teal13, B.Words, out _);
            B.TextAt(details, "SaveNote", 20, 578, 520, 16, B.medium, 11, B.Faint, TextAnchor.MiddleLeft,
                "SAVE OVER only shows when editing a game you made.");

            RectTransform stepSection = B.Rect(left, "Step", 0, 0, 560, 612);
            Text stepTitle = B.TextAt(stepSection, "Title", 20, 20, 520, 24, B.bold, 15, B.Words, TextAnchor.MiddleLeft, "1 · DECK");
            Text stepText = B.TextAt(stepSection, "Text", 20, 54, 520, 220, B.medium, 13, B.Soft, TextAnchor.UpperLeft, string.Empty);
            stepText.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform picker = B.Box(stepSection, "Picker", 20, 290, 520, 44, B.Well, B.Edge, 1f);
            Button previousPick = B.SmallButton(picker, "Previous", 4, 4, 36, 36, "‹", B.Edge, B.Cyan);
            Text pick = B.TextAt(picker, "Pick", 44, 0, 432, 44, B.bold, 14, B.Words, TextAnchor.MiddleCenter, "No deck");
            Button nextPick = B.SmallButton(picker, "Next", 480, 4, 36, 36, "›", B.Edge, B.Cyan);
            Button actionA = Pill13(stepSection, "ActionA", 20, 346, 256, 44, "NEW DECK…", B.Well, B.Cyan, out Text actionALabel);
            Button actionB = Pill13(stepSection, "ActionB", 284, 346, 256, 44, "PUT ON TABLE", B.Well, B.Cyan, out Text actionBLabel);
            Button back = Pill13(left, "Back", 20, 548, 120, 44, "BACK", B.Well, B.Words, out _);
            Button next = Pill13(left, "Next", 420, 548, 120, 44, "NEXT", Teal13, B.Words, out _);
            stepSection.gameObject.SetActive(false);

            // ---------- HOW THE RULES CARD WILL LOOK ----------
            RectTransform preview = B.Box(root, "Preview", 600, 92, 300, 340, B.Panel, B.Edge, 1f);
            Field13(preview, "Heading", 15, 15, "HOW THE RULES CARD WILL LOOK");
            Text previewTitle = B.TextAt(preview, "Title", 15, 40, 230, 22, B.bold, 14, B.Words, TextAnchor.MiddleLeft, "RULES · [GAME]");
            B.TextAt(preview, "Marks", 255, 40, 30, 22, B.bold, 14, B.Muted, TextAnchor.MiddleRight, "///");
            RectTransform previewRows = B.Rect(preview, "Rows", 15, 69, 270, 255);
            RectTransform previewRow = B.Rect(previewRows, "RowTemplate", 0, 0, 270, 36);
            B.Box(previewRow, "Plate", 0, 0, 270, 36, B.Row, B.Edge, 1f).GetComponent<Image>().raycastTarget = false;
            Text previewText = B.TextAt(previewRow, "Text", 12, 0, 250, 36, B.medium, 13, B.Words, TextAnchor.MiddleLeft, "› [RULE 1]");
            previewText.horizontalOverflow = HorizontalWrapMode.Wrap;
            previewText.verticalOverflow = VerticalWrapMode.Truncate;

            // ---------- WHAT GETS SAVED ----------
            RectTransform saved = B.Box(root, "Saved", 920, 92, 340, 340, B.Panel, B.Edge, 1f);
            Field13(saved, "Heading", 15, 15, "WHAT GETS SAVED");
            RectTransform savedRows = B.Rect(saved, "Rows", 15, 38, 310, 200);
            RectTransform savedRow = B.Rect(savedRows, "RowTemplate", 0, 0, 310, 27);
            B.TextAt(savedRow, "Label", 0, 0, 140, 27, B.medium, 13, B.Soft, TextAnchor.MiddleLeft, "Table");
            B.TextAt(savedRow, "Value", 120, 0, 190, 27, B.medium, 13, B.Words, TextAnchor.MiddleRight, "Hexagon");
            B.Line(savedRow, "Rule", 0, 26, 310, 1, B.Edge);
            RectTransform warning = B.Box(saved, "Warning", 15, 286, 310, 40, UiKit.Hex("2a2412"), Warn13, 1f);
            Text status = B.TextAt(warning, "Text", 12, 0, 290, 40, B.medium, 12, Warn13, TextAnchor.MiddleLeft, string.Empty);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text summary = B.TextAt(saved, "Summary", 15, 250, 310, 30, B.medium, 12, B.Muted, TextAnchor.UpperLeft, string.Empty);
            summary.gameObject.SetActive(false); // WHAT GETS SAVED has it row by row

            // ---------- the three steps ----------
            RectTransform help = B.Box(root, "Steps", 600, 448, 660, 256, B.Panel, B.Edge, 1f);
            StepHelp13(help, 15, "1 · DECK", "Pick which decks start on the table. NEW DECK opens the Deck builder.");
            B.Line(help, "Rule1", 220, 15, 1, 226, B.Edge);
            StepHelp13(help, 235, "2 · CARDS", "Change any card's words or picture. Opens the Card studio, comes back here.");
            B.Line(help, "Rule2", 440, 15, 1, 226, B.Edge);
            StepHelp13(help, 455, "3 · DEAL", "How many cards each hand gets at START, and who goes first.");

            // A cancel button the view needs; EXIT does the same.
            Button cancel = B.SmallButton(root, "Cancel", 0, 0, 10, 10, string.Empty, B.Edge, B.Words);
            cancel.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = dismiss;
            so.FindProperty("closeButton").objectReferenceValue = exit;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("summaryLabel").objectReferenceValue = summary;
            so.FindProperty("nameInput").objectReferenceValue = nameInput;
            so.FindProperty("descriptionInput").objectReferenceValue = about;
            so.FindProperty("rulesInput").objectReferenceValue = rules;
            so.FindProperty("fewerMinimumButton").objectReferenceValue = fewerMinimum;
            so.FindProperty("moreMinimumButton").objectReferenceValue = moreMinimum;
            so.FindProperty("minimumLabel").objectReferenceValue = minimum;
            so.FindProperty("fewerMaximumButton").objectReferenceValue = fewerMaximum;
            so.FindProperty("moreMaximumButton").objectReferenceValue = moreMaximum;
            so.FindProperty("maximumLabel").objectReferenceValue = maximum;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.FindProperty("saveOverButton").objectReferenceValue = saveOver;
            so.FindProperty("saveOverLabel").objectReferenceValue = saveOverLabel;
            so.FindProperty("saveAsNewButton").objectReferenceValue = saveAsNew;
            SetArray(so.FindProperty("stepButtons"), stepButtons);
            SetArray(so.FindProperty("stepLabels"), stepLabels);
            so.FindProperty("stepSection").objectReferenceValue = stepSection.gameObject;
            so.FindProperty("stepTitleLabel").objectReferenceValue = stepTitle;
            so.FindProperty("stepTextLabel").objectReferenceValue = stepText;
            so.FindProperty("pickerRow").objectReferenceValue = picker.gameObject;
            so.FindProperty("previousPickButton").objectReferenceValue = previousPick;
            so.FindProperty("pickLabel").objectReferenceValue = pick;
            so.FindProperty("nextPickButton").objectReferenceValue = nextPick;
            so.FindProperty("actionAButton").objectReferenceValue = actionA;
            so.FindProperty("actionALabel").objectReferenceValue = actionALabel;
            so.FindProperty("actionBButton").objectReferenceValue = actionB;
            so.FindProperty("actionBLabel").objectReferenceValue = actionBLabel;
            so.FindProperty("detailsSection").objectReferenceValue = details.gameObject;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("minutesButton").objectReferenceValue = minutesButton;
            so.FindProperty("minutesLabel").objectReferenceValue = minutesLabel;
            so.FindProperty("ruleRows").objectReferenceValue = ruleRows;
            so.FindProperty("ruleRowTemplate").objectReferenceValue = template;
            so.FindProperty("ruleRowPitch").floatValue = 46f * 1.5f;
            so.FindProperty("newRuleInput").objectReferenceValue = newRule;
            so.FindProperty("previewTitle").objectReferenceValue = previewTitle;
            so.FindProperty("previewRows").objectReferenceValue = previewRows;
            so.FindProperty("previewRowTemplate").objectReferenceValue = previewRow.gameObject;
            so.FindProperty("previewRowPitch").floatValue = 42f * 1.5f;
            so.FindProperty("savedRows").objectReferenceValue = savedRows;
            so.FindProperty("savedRowTemplate").objectReferenceValue = savedRow.gameObject;
            so.FindProperty("savedRowPitch").floatValue = 27f * 1.5f;
            SetArray(so.FindProperty("stepDoneMarks"), doneMarks);
            SetArray(so.FindProperty("stepNumberMarks"), numberMarks);
            SetArray(so.FindProperty("stepPills"), pills);
            so.FindProperty("boardSteps").boolValue = true;
            so.FindProperty("stepDoneColor").colorValue = Green13;
            so.FindProperty("stepNowColor").colorValue = B.Words;
            so.FindProperty("stepIdleColor").colorValue = B.Muted;
            so.FindProperty("warningBox").objectReferenceValue = warning.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Field13(Transform parent, string name, float x, float y, string text)
        {
            Text label = B.TextAt(parent, name, x, y, 360, 16, B.bold, 11, B.Muted, TextAnchor.MiddleLeft, text);
            label.fontStyle = FontStyle.Bold;
        }

        private static InputField Input13(Transform parent, string name, float x, float y, float width, float height,
            string placeholderText, bool multiLine)
        {
            RectTransform rect = B.Box(parent, name, x, y, width, height, B.Well, B.Edge, 1f);
            InputField field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = rect.GetComponent<Image>();
            Text text = B.TextAt(rect, "Text", 10, 0, width - 20, height, B.medium, 14, B.Words, TextAnchor.MiddleLeft, string.Empty);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text placeholder = B.TextAt(rect, "Placeholder", 10, 0, width - 20, height, B.medium, 14, B.Faint,
                TextAnchor.MiddleLeft, placeholderText);
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = multiLine ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            return field;
        }

        // A rule row: the ⁝⁝ handle, the words (type over them to change the rule) and × to remove it.
        private static SaveRuleRow RuleRow13(RectTransform rows)
        {
            RectTransform row = B.Box(rows, "RuleTemplate", 0, 0, 520, 36, B.Row, B.Edge, 1f);
            RectTransform handle = Handle13(row, B.Muted);
            InputField field = Input13(row, "Words", 34, 3, 440, 30, string.Empty, false);
            Object.DestroyImmediate(field.GetComponent<Outline>());
            Object.DestroyImmediate(field.GetComponent<Outline>());
            field.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            Button remove = B.SmallButton(row, "Remove", 484, 6, 28, 24, "×", B.Edge, B.Words);
            SaveRuleRow view = row.gameObject.AddComponent<SaveRuleRow>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("textField").objectReferenceValue = field;
            so.FindProperty("removeButton").objectReferenceValue = remove;
            so.FindProperty("handle").objectReferenceValue = handle;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // Six dots (two columns of three), the drag handle.
        private static RectTransform Handle13(Transform parent, Color colour)
        {
            RectTransform handle = B.Rect(parent, "Handle", 8, 6, 20, 24);
            Image catcher = handle.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            for (int c = 0; c < 2; c++)
            {
                for (int r = 0; r < 3; r++)
                {
                    RectTransform dot = B.Box(handle, $"Dot{c}{r}", 5 + (c * 6), 5 + (r * 6), 3, 3, colour, null, 0f);
                    dot.GetComponent<Image>().sprite = B.circle;
                    dot.GetComponent<Image>().raycastTarget = false;
                }
            }

            return handle;
        }

        private static Button Pill13(Transform parent, string name, float x, float y, float width, float height, string words,
            Color fill, Color colour, out Text label)
        {
            RectTransform rect = B.Box(parent, name, x, y, width, height, fill, B.Edge, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            label = B.TextAt(rect, "Label", 0, 0, width, height, B.bold, 14, colour, TextAnchor.MiddleCenter, words);
            return button;
        }

        private static void StepHelp13(Transform parent, float x, string title, string text)
        {
            B.TextAt(parent, "Title" + title.Substring(0, 1), x, 15, 190, 20, B.bold, 13, B.Words, TextAnchor.MiddleLeft, title);
            Text body = B.TextAt(parent, "Text" + title.Substring(0, 1), x, 40, 190, 120, B.medium, 12, B.Soft,
                TextAnchor.UpperLeft, text);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
    }
}
