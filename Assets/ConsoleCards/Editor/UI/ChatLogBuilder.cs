using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// CHAT-1, designer note 03 (2026-11-17: "add a text chat box for players; the later wireframe places Chat beside Log
    /// in the Activity / Chat panel"): adds to the Log prefab (Final UI/ActivityLog) the LOG and CHAT tabs in its header,
    /// the chat's list beside the log's lines and the message box (a field and SEND) along the panel's bottom, and links
    /// them to the view. It adds only what is missing and keeps the panel's own look (V5 and the kit look restyle it),
    /// so it can be run again.
    /// </summary>
    public static class ChatLogBuilder
    {
        private const float TabsWidth = 170f;
        private const float RowHeight = 44f;
        private static readonly Color Orange = new Color(0.949f, 0.463f, 0.180f, 1f);
        private static readonly Color Ink = new Color(0.102f, 0.071f, 0.024f, 1f);
        private static readonly Color FieldFill = new Color(1f, 1f, 1f, 0.1f);
        private static readonly Color Hint = new Color(1f, 1f, 1f, 0.45f);

        [MenuItem("Console Cards/UI/Add Chat to the Log (designer note 03)")]
        public static void AddChat()
        {
            string path = GameplayUiBuilder.ActivityLogPath;
            if (AssetDatabase.LoadAssetAtPath<ActivityLogView>(path) == null)
            {
                Debug.LogWarning("[CHAT-1] No Log prefab: run Console Cards > UI > Create Missing Gameplay UI Prefabs (Final UI) first.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ActivityLogView view = root.GetComponent<ActivityLogView>();
                RectTransform panel = root.transform.Find("Panel") as RectTransform;
                RectTransform header = panel != null ? panel.Find("Header") as RectTransform : null;
                RectTransform viewport = panel != null ? panel.Find("Viewport") as RectTransform : null;
                Transform linesTransform = viewport != null ? viewport.Find("Content/Lines") : null;
                Text lines = linesTransform != null ? linesTransform.GetComponent<Text>() : null;
                if (view == null || panel == null || header == null || viewport == null || lines == null)
                {
                    Debug.LogWarning("[CHAT-1] The Log prefab is not the expected shape (Panel, Header, Viewport/Content/Lines); nothing changed.");
                    return;
                }

                Text title = header.Find("Title") != null ? header.Find("Title").GetComponent<Text>() : null;
                Font words = lines.font;
                Font heading = title != null && title.font != null ? title.font : words;
                if (title != null)
                {
                    title.text = "ACTIVITY";
                    RectTransform titleRect = title.rectTransform;
                    titleRect.offsetMax = new Vector2(Mathf.Min(titleRect.offsetMax.x, -TabsWidth - 10f), titleRect.offsetMax.y);
                }

                // ---------- the tabs, on the right of the header ----------
                RectTransform tabs = Ensure("Tabs", header);
                tabs.anchorMin = new Vector2(1f, 0f);
                tabs.anchorMax = new Vector2(1f, 1f);
                tabs.pivot = new Vector2(1f, 0.5f);
                tabs.anchoredPosition = new Vector2(-8f, 0f);
                tabs.sizeDelta = new Vector2(TabsWidth, 0f);
                HorizontalLayoutGroup tabRow = Component<HorizontalLayoutGroup>(tabs.gameObject);
                tabRow.spacing = 6f;
                tabRow.childAlignment = TextAnchor.MiddleRight;
                tabRow.childControlWidth = true;
                tabRow.childControlHeight = true;
                tabRow.childForceExpandWidth = false;
                tabRow.childForceExpandHeight = true;
                Button logTab = Tab(tabs, "LogTab", "LOG", heading, out Text logLabel);
                Button chatTab = Tab(tabs, "ChatTab", "CHAT", heading, out Text chatLabel);

                // ---------- the chat's list, beside the log's lines ----------
                Transform chatLinesTransform = lines.transform.parent.Find("ChatLines");
                Text chatLines;
                if (chatLinesTransform == null)
                {
                    GameObject copy = Object.Instantiate(lines.gameObject, lines.transform.parent, false);
                    copy.name = "ChatLines";
                    chatLines = copy.GetComponent<Text>();
                }
                else
                {
                    chatLines = chatLinesTransform.GetComponent<Text>();
                }

                chatLines.text = "No messages yet. Say hello!";
                chatLines.supportRichText = true;
                chatLines.gameObject.SetActive(false);

                // ---------- the message box, along the bottom of the panel ----------
                RectTransform row = Ensure("ChatRow", panel);
                row.anchorMin = Vector2.zero;
                row.anchorMax = new Vector2(1f, 0f);
                row.pivot = new Vector2(0.5f, 0f);
                row.anchoredPosition = new Vector2(0f, 14f);
                row.sizeDelta = new Vector2(-28f, RowHeight);
                HorizontalLayoutGroup rowLayout = Component<HorizontalLayoutGroup>(row.gameObject);
                rowLayout.spacing = 8f;
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = true;
                InputField field = Field(row, words);
                Button send = Send(row, heading);
                row.gameObject.SetActive(false);

                SerializedObject so = new SerializedObject(view);
                so.FindProperty("logTab").objectReferenceValue = logTab;
                so.FindProperty("logTabLabel").objectReferenceValue = logLabel;
                so.FindProperty("chatTab").objectReferenceValue = chatTab;
                so.FindProperty("chatTabLabel").objectReferenceValue = chatLabel;
                so.FindProperty("chatBody").objectReferenceValue = chatLines;
                so.FindProperty("chatRow").objectReferenceValue = row.gameObject;
                so.FindProperty("chatField").objectReferenceValue = field;
                so.FindProperty("chatSendButton").objectReferenceValue = send;
                so.FindProperty("chatRowHeight").floatValue = RowHeight + 8f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[CHAT-1] The Log has its LOG and CHAT tabs and the message box.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform Ensure(string name, Transform parent)
        {
            Transform found = parent.Find(name);
            if (found != null) return (RectTransform)found;
            GameObject made = new GameObject(name, typeof(RectTransform));
            made.layer = parent.gameObject.layer;
            made.transform.SetParent(parent, false);
            return (RectTransform)made.transform;
        }

        private static T Component<T>(GameObject go) where T : UnityEngine.Component
        {
            T found = go.GetComponent<T>();
            return found != null ? found : go.AddComponent<T>();
        }

        private static Text Words(RectTransform parent, string name, Font font, int size, Color colour, TextAnchor anchor,
            string text)
        {
            RectTransform rect = Ensure(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text label = Component<Text>(rect.gameObject);
            label.font = font;
            label.fontSize = size;
            label.color = colour;
            label.alignment = anchor;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        private static Button Tab(RectTransform tabs, string name, string text, Font font, out Text label)
        {
            RectTransform rect = Ensure(name, tabs);
            LayoutElement size = Component<LayoutElement>(rect.gameObject);
            size.preferredWidth = 78f;
            Image hit = Component<Image>(rect.gameObject);
            hit.color = new Color(1f, 1f, 1f, 0f);
            Button button = Component<Button>(rect.gameObject);
            button.targetGraphic = hit;
            label = Words(rect, "Label", font, 15, Color.white, TextAnchor.MiddleCenter, text);
            return button;
        }

        private static InputField Field(RectTransform row, Font font)
        {
            RectTransform rect = Ensure("Field", row);
            LayoutElement size = Component<LayoutElement>(rect.gameObject);
            size.flexibleWidth = 1f;
            Image fill = Component<Image>(rect.gameObject);
            fill.color = FieldFill;
            InputField field = Component<InputField>(rect.gameObject);
            field.targetGraphic = fill;
            Text placeholder = Words(rect, "Placeholder", font, 16, Hint, TextAnchor.MiddleLeft, "Say something...");
            placeholder.fontStyle = FontStyle.Italic;
            Text typed = Words(rect, "Text", font, 16, Color.white, TextAnchor.MiddleLeft, string.Empty);
            typed.supportRichText = false;
            Inset(placeholder.rectTransform);
            Inset(typed.rectTransform);
            field.textComponent = typed;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 200;
            return field;
        }

        private static void Inset(RectTransform rect)
        {
            rect.offsetMin = new Vector2(12f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);
        }

        private static Button Send(RectTransform row, Font font)
        {
            RectTransform rect = Ensure("Send", row);
            LayoutElement size = Component<LayoutElement>(rect.gameObject);
            size.preferredWidth = 84f;
            Image fill = Component<Image>(rect.gameObject);
            fill.color = Orange;
            Button button = Component<Button>(rect.gameObject);
            button.targetGraphic = fill;
            Words(rect, "Label", font, 15, Ink, TextAnchor.MiddleCenter, "SEND");
            return button;
        }
    }
}
