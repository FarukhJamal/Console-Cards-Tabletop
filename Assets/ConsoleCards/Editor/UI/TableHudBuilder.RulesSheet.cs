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
    /// RULES-SHEET (R2): the Rules sheet panel (RulesSheetPanelView + RulesSheetRowView) in Final UI/Notices/RulesSheetPanel.
    /// Laid out in board pixels (1280 x 720, x1.5): top line with title, copy picker and DONE; ten section tabs on the
    /// left; the section's rows and its written rules on the right. Built once, then kept (edit it freely).
    /// </summary>
    public static partial class TableHudBuilder
    {
        public const string RulesSheetPath = Folder + "RulesSheetPanel.prefab";
        private const string RulesSheetLabel = "RulesSheetBoard1";

        private static void BuildRulesSheet()
        {
            RulesSheetPanelView existing = AssetDatabase.LoadAssetAtPath<RulesSheetPanelView>(RulesSheetPath);
            if (existing != null && System.Array.IndexOf(AssetDatabase.GetLabels(existing.gameObject), RulesSheetLabel) < 0)
            {
                AssetDatabase.DeleteAsset(RulesSheetPath); // made by an older version of this builder: built again
            }

            if (!File.Exists(RulesSheetPath))
            {
                B.Prepare();
                Save<RulesSheetPanelView>(RulesSheetPath, "RulesSheetPanel", BuildRulesSheetPanel);
                GameObject made = AssetDatabase.LoadAssetAtPath<GameObject>(RulesSheetPath);
                if (made != null) AssetDatabase.SetLabels(made, new[] { RulesSheetLabel });
            }

            AssignCatalogEntry(UiPrefabIds.RulesSheetPanel, AssetDatabase.LoadAssetAtPath<RulesSheetPanelView>(RulesSheetPath),
                UiLayer.Modal, UiRetention.CachedSingleInstance);
        }

        private static void BuildRulesSheetPanel(RectTransform root, RulesSheetPanelView view)
        {
            Button dismiss = InvisibleButton(root, "DismissOverlay");
            dismiss.gameObject.SetActive(false);

            RectTransform panel = B.Box(root, "Panel", 20, 16, 1240, 688, B.Panel, Orange, 2f);

            // ---------- the top line ----------
            Text title = B.TextAt(panel, "Title", 20, 12, 600, 40, B.bold, 16, B.Words, TextAnchor.MiddleLeft, "RULES SHEET");
            Button previousCopy = B.SmallButton(panel, "PreviousCopy", 620, 14, 36, 36, "<", B.Edge, B.Words);
            Text copyLabel = B.TextAt(panel, "CopyLabel", 660, 14, 300, 36, B.medium, 13, B.Words, TextAnchor.MiddleCenter, string.Empty);
            Button nextCopy = B.SmallButton(panel, "NextCopy", 964, 14, 36, 36, ">", B.Edge, B.Words);
            Button copy = B.SmallButton(panel, "Copy", 1006, 14, 96, 36, "COPY", B.Edge, B.Words);
            Button done = Pill13(panel, "Done", 1110, 12, 110, 40, "DONE", Orange, B.Ink, out Text doneLabel);

            // ---------- the ten section tabs ----------
            string[] names = { "Basics", "Components", "Decks", "Setup", "Your turn", "Limits", "Actions", "Card effects",
                "Scoring and winning", "House rules" };
            Button[] sectionButtons = new Button[names.Length];
            Text[] sectionLabels = new Text[names.Length];
            Image[] sectionPills = new Image[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform section = B.Rect(panel, "Section" + (i + 1), 20, 64 + (i * 54), 220, 48);
                Image catcher = section.gameObject.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f); // takes the clicks; the pill below shows the section you are on
                Button button = section.gameObject.AddComponent<Button>();
                button.targetGraphic = catcher;
                button.transition = Selectable.Transition.None;
                sectionButtons[i] = button;
                RectTransform pill = B.Box(section, "Pill", 0, 0, 220, 48, B.TealFill, B.Edge, 1f);
                pill.GetComponent<Image>().raycastTarget = false;
                sectionPills[i] = pill.GetComponent<Image>();
                sectionLabels[i] = B.TextAt(section, "Label", 14, 0, 200, 48, B.medium, 13, B.Words, TextAnchor.MiddleLeft, names[i]);
            }

            Button clear = B.SmallButton(panel, "ClearSection", 20, 612, 220, 36, "CLEAR SECTION", B.Edge, B.Words);

            // ---------- the right area ----------
            Text sectionTitle = B.TextAt(panel, "SectionTitle", 260, 64, 900, 30, B.bold, 15, B.Words, TextAnchor.MiddleLeft, string.Empty);
            Text sectionHint = B.TextAt(panel, "SectionHint", 260, 94, 960, 24, B.medium, 11, B.Muted, TextAnchor.MiddleLeft, string.Empty);
            RectTransform rows = B.Rect(panel, "Rows", 260, 124, 960, 432);
            RulesSheetRowView rowTemplate = RulesSheetRow13(rows);

            // ---------- the written rules ----------
            B.TextAt(panel, "WrittenLabel", 260, 560, 960, 20, B.medium, 11, B.Muted, TextAnchor.MiddleLeft,
                "WRITTEN RULES FOR THIS SECTION · one rule per line · only players follow them");
            InputField sectionText = Input13(panel, "SectionText", 260, 582, 960, 84, "[Write rules the game cannot check]", true);
            Text typed = sectionText.textComponent;
            typed.alignment = TextAnchor.UpperLeft;
            typed.verticalOverflow = VerticalWrapMode.Truncate;
            Text hint = sectionText.placeholder as Text;
            if (hint != null)
            {
                hint.alignment = TextAnchor.UpperLeft;
                hint.verticalOverflow = VerticalWrapMode.Truncate;
            }

            foreach (Graphic g in new Graphic[] { typed, hint })
            {
                if (g == null) continue;
                RectTransform r = g.rectTransform; // inset 6 board px top and bottom, like Field10
                r.anchoredPosition += new Vector2(0f, -6f * 1.5f);
                r.sizeDelta -= new Vector2(0f, 12f * 1.5f);
            }

            Text status = B.TextAt(panel, "Status", 260, 668, 960, 18, B.medium, 11, Warn13, TextAnchor.MiddleLeft, string.Empty);

            // ---------- wiring ----------
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("titleLabel").objectReferenceValue = title;
            SetArray(so.FindProperty("sectionButtons"), sectionButtons);
            SetArray(so.FindProperty("sectionLabels"), sectionLabels);
            SetArray(so.FindProperty("sectionPills"), sectionPills);
            so.FindProperty("sectionTitleLabel").objectReferenceValue = sectionTitle;
            so.FindProperty("sectionHintLabel").objectReferenceValue = sectionHint;
            so.FindProperty("rowsRoot").objectReferenceValue = rows;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            so.FindProperty("rowPitch").floatValue = 72f * 1.5f;
            so.FindProperty("sectionTextInput").objectReferenceValue = sectionText;
            so.FindProperty("previousCopyButton").objectReferenceValue = previousCopy;
            so.FindProperty("copyLabel").objectReferenceValue = copyLabel;
            so.FindProperty("nextCopyButton").objectReferenceValue = nextCopy;
            so.FindProperty("copyButton").objectReferenceValue = copy;
            so.FindProperty("clearButton").objectReferenceValue = clear;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("doneButton").objectReferenceValue = done;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // One rule row: the rule's name, the value picker (< value >) or typed value, a clear button and its sentence.
        private static RulesSheetRowView RulesSheetRow13(RectTransform rows)
        {
            RectTransform row = B.Rect(rows, "Row", 0, 0, 960, 70);
            Text nameLabel = B.TextAt(row, "Name", 0, 0, 280, 40, B.bold, 13, B.Words, TextAnchor.MiddleLeft, string.Empty);
            Button previous = B.SmallButton(row, "Previous", 290, 2, 40, 36, "<", B.Edge, B.Words);
            RectTransform valueBox = B.Box(row, "ValueBox", 334, 2, 360, 36, B.Well, B.Edge, 1f);
            Text value = B.TextAt(valueBox, "Value", 0, 0, 360, 36, B.bold, 14, B.Words, TextAnchor.MiddleCenter, string.Empty);
            InputField valueInput = Input13(row, "ValueInput", 334, 2, 360, 36, "[players decide]", false);
            Button next = B.SmallButton(row, "Next", 698, 2, 40, 36, ">", B.Edge, B.Words);
            Button clear = B.SmallButton(row, "Clear", 744, 2, 40, 36, "×", B.Edge, B.Words);
            Text sentence = B.TextAt(row, "Sentence", 0, 42, 960, 24, B.medium, 11, B.Muted, TextAnchor.MiddleLeft, string.Empty);

            RulesSheetRowView view = row.gameObject.AddComponent<RulesSheetRowView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("valueLabel").objectReferenceValue = value;
            so.FindProperty("valueInput").objectReferenceValue = valueInput;
            so.FindProperty("previousButton").objectReferenceValue = previous;
            so.FindProperty("previousLabel").objectReferenceValue = previous.GetComponentInChildren<Text>(true);
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("nextLabel").objectReferenceValue = next.GetComponentInChildren<Text>(true);
            so.FindProperty("clearButton").objectReferenceValue = clear;
            so.FindProperty("sentenceLabel").objectReferenceValue = sentence;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }
    }
}
