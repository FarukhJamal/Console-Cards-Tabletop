using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Board 17 "Draw tool (right rail + bar)" (owner, 2026-10-30): the Draw bar, built into the table HUD. It opens to the
    /// left of the rail, level with the Draw icon (the rail's fifth button), 50 board px high: TOOL | LINE | SIZE | COLOUR |
    /// Undo, CLEAR, ✕; under it one line of keys (or the label's words box). Measured from the board (x1.5).
    /// </summary>
    public static partial class TableHudBuilder
    {
        private static readonly string[] DrawIcons =
        {
            "HudDraw", "DrawHighlighter", "DrawLabel", "DrawEraser", "DrawLinePlain", "DrawLineDashed", "DrawLineDotted",
            "DrawLineWavy", "DrawLineBrush", "DrawLineSwirl",
        };

        private static readonly Color DrawGroupWords = Hex("9ba4a7");
        private static readonly Color DrawClearWords = Hex("ff7a6b");
        private static readonly Color DrawHintWords = Hex("9ba4a7");

        private static void BuildDrawBar17(RectTransform root, SerializedObject so)
        {
            // The rail's fifth button (Draw): its centre is 14 + 8/1.5 + 4 x 60/1.5 + 18 board px down.
            float centre = (14f * K) + 8f + (4f * 60f) + 27f;
            float barHeight = 50f * K;
            float right = -((14f * K) + 72f + 12f);
            RectTransform holder = Rect("Draw", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            TableDrawBarView view = holder.gameObject.AddComponent<TableDrawBarView>();

            RectTransform bar = Rect("Bar", holder, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(right, -(centre - (barHeight * 0.5f))), new Vector2(1200f, barHeight));
            Fill(bar, GlassStrong, round, Orange, 1.5f);
            Row(bar.gameObject, 12, 12, 0, 0, 9f, TextAnchor.MiddleLeft);
            Fit(bar.gameObject, true, false);

            // TOOL: pen 1, highlighter 2, label 3, eraser E
            RectTransform tools = DrawGroup17(bar, "Tool", "TOOL", 3f);
            string[] toolIcons = { "HudDraw", "DrawHighlighter", "DrawLabel", "DrawEraser" };
            string[] toolKeys = { "1", "2", "3", "E" };
            Button[] modeButtons = new Button[4];
            Image[] modeFills = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                modeButtons[i] = DrawButton17(tools, toolIcons[i], 54f, out modeFills[i], out RectTransform rect);
                DrawIcon17(rect, toolIcons[i], 27f, 27f);
                DrawKey17(rect, toolKeys[i]);
            }

            DrawRule17(bar, "Rule1");

            // LINE: plain, dashed, dotted, wavy, brush, swirl (the board's order)
            RectTransform lines = DrawGroup17(bar, "Line", "LINE", 6f);
            string[] lineIcons = { "DrawLinePlain", "DrawLineDashed", "DrawLineDotted", "DrawLineWavy", "DrawLineBrush", "DrawLineSwirl" };
            Button[] styleButtons = new Button[6];
            Image[] styleFills = new Image[6];
            for (int i = 0; i < 6; i++)
            {
                styleButtons[i] = DrawButton17(lines, lineIcons[i], 66f, out styleFills[i], out RectTransform rect);
                DrawIcon17(rect, lineIcons[i], 42f, 18f);
            }

            DrawRule17(bar, "Rule2");

            // SIZE: thin, medium, thick
            RectTransform sizes = DrawGroup17(bar, "Size", "SIZE", 6f);
            float[] dots = { 9f, 15f, 22f };
            Button[] sizeButtons = new Button[3];
            Image[] sizeFills = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                sizeButtons[i] = DrawButton17(sizes, "Size" + (i + 1), 48f, out sizeFills[i], out RectTransform rect);
                Image dot = Shape(Centred("Dot", rect, dots[i], dots[i]), Soft, circle);
                dot.raycastTarget = false;
            }

            DrawRule17(bar, "Rule3");

            // COLOUR: your seat's colour first, then white, yellow, teal, blue, green
            RectTransform colours = DrawGroup17(bar, "Colour", "COLOUR", 9f);
            Button[] swatchButtons = new Button[6];
            Image[] swatchImages = new Image[6];
            Image[] swatchRings = new Image[6];
            for (int i = 0; i < 6; i++)
            {
                RectTransform rect = Child("Swatch" + (i + 1), colours);
                Size(rect.gameObject, 41f, 41f, 41f);
                Image catcher = rect.gameObject.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f);
                swatchRings[i] = Shape(Centred("Ring", rect, 41f, 41f), Orange, ring != null ? ring : circle);
                swatchRings[i].raycastTarget = false;
                swatchRings[i].enabled = i == 0;
                swatchImages[i] = Shape(Centred("Colour", rect, 31f, 31f), Words, circle);
                swatchImages[i].raycastTarget = false;
                swatchButtons[i] = rect.gameObject.AddComponent<Button>();
                swatchButtons[i].targetGraphic = swatchImages[i];
            }

            DrawRule17(bar, "Rule4");

            // Undo, CLEAR (asks again), ✕
            Button undo = DrawButton17(bar, "Undo", 54f, out _, out RectTransform undoRect);
            DrawIcon17(undoRect, "HudUndo", 27f, 27f);
            Button clear = DrawButton17(bar, "Clear", 126f, out Image clearFill, out RectTransform clearRect);
            Text clearLabel = Label(Centred("Label", clearRect, 120f, 40f), bold, 16, DrawClearWords, TextAnchor.MiddleCenter, "CLEAR");
            clearLabel.raycastTarget = false;
            Button close = DrawButton17(bar, "Close", 54f, out Image closeFill, out RectTransform closeRect);
            closeFill.color = new Color(0f, 0f, 0f, 0f);
            Outline closeEdge = closeRect.gameObject.AddComponent<Outline>();
            closeEdge.effectColor = Edge;
            closeEdge.effectDistance = new Vector2(1.5f, -1.5f);
            Text closeX = Label(Centred("Label", closeRect, 50f, 50f), bold, 26, Soft, TextAnchor.MiddleCenter, "\u00d7");
            closeX.raycastTarget = false;

            // Under the bar: the keys, or the label's words box.
            float lineTop = centre + (barHeight * 0.5f) + (6f * K);
            RectTransform keys = Rect("Keys", holder, Vector2.one, Vector2.one, Vector2.one, new Vector2(right, -lineTop),
                new Vector2(600f, 26f * K));
            Fill(keys, new Color(0.078f, 0.094f, 0.098f, 0.7f), round, null, 0f).raycastTarget = false;
            Row(keys.gameObject, 15, 15, 0, 0, 9f, TextAnchor.MiddleLeft);
            Fit(keys.gameObject, true, false);
            DrawHintWords17(keys, "Drag to draw  ·");
            DrawHintKey17(keys, "SHIFT");
            DrawHintWords17(keys, "straight line  ·");
            DrawHintKey17(keys, "ESC");
            DrawHintWords17(keys, "back to Select  ·  drawings stay until erased");

            RectTransform wordsRect = Rect("LabelWords", holder, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(right, -lineTop), new Vector2(560f, 26f * K + 6f));
            Fill(wordsRect, GlassStrong, round, Cyan, 1.5f);
            InputField words = wordsRect.gameObject.AddComponent<InputField>();
            words.targetGraphic = wordsRect.GetComponent<Image>();
            Text typed = Label(Rect("Text", wordsRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-30f, 0f)), medium, 17, Words, TextAnchor.MiddleLeft, string.Empty);
            typed.supportRichText = false;
            Text placeholder = Label(Rect("Placeholder", wordsRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-30f, 0f)), medium, 17, DrawHintWords, TextAnchor.MiddleLeft,
                "Type the label's words, then click the table");
            words.textComponent = typed;
            words.placeholder = placeholder;
            words.lineType = InputField.LineType.SingleLine;
            wordsRect.gameObject.SetActive(false);
            bar.gameObject.SetActive(false);
            keys.gameObject.SetActive(false);

            SerializedObject drawSo = new SerializedObject(view);
            drawSo.FindProperty("barRoot").objectReferenceValue = bar.gameObject;
            Assign17(drawSo, "modeButtons", modeButtons);
            Assign17(drawSo, "modeFills", modeFills);
            Assign17(drawSo, "styleButtons", styleButtons);
            Assign17(drawSo, "styleFills", styleFills);
            Assign17(drawSo, "sizeButtons", sizeButtons);
            Assign17(drawSo, "sizeFills", sizeFills);
            Assign17(drawSo, "swatchButtons", swatchButtons);
            Assign17(drawSo, "swatchImages", swatchImages);
            Assign17(drawSo, "swatchRings", swatchRings);
            drawSo.FindProperty("undoButton").objectReferenceValue = undo;
            drawSo.FindProperty("clearButton").objectReferenceValue = clear;
            drawSo.FindProperty("clearLabel").objectReferenceValue = clearLabel;
            drawSo.FindProperty("clearFill").objectReferenceValue = clearFill;
            drawSo.FindProperty("closeButton").objectReferenceValue = close;
            drawSo.FindProperty("keysLine").objectReferenceValue = keys.gameObject;
            drawSo.FindProperty("labelInput").objectReferenceValue = words;
            drawSo.FindProperty("onFill").colorValue = HoverFill;
            drawSo.FindProperty("clearWords").colorValue = DrawClearWords;
            drawSo.ApplyModifiedPropertiesWithoutUndo();
            so.FindProperty("drawBar").objectReferenceValue = view;
        }

        // A group of the bar: its buttons in a row and its name just above the bar.
        private static RectTransform DrawGroup17(RectTransform bar, string name, string title, float spacing)
        {
            RectTransform group = Child(name, bar);
            Row(group.gameObject, 0, 0, 0, 0, spacing, TextAnchor.MiddleLeft);
            Fit(group.gameObject, true, false);
            Size(group.gameObject, -1f, 54f);
            // On a dark tag so it reads over a light table too.
            RectTransform titleRect = Rect("Title", group, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f),
                new Vector2(0f, (7f * K) + 4f), new Vector2(80f, 22f));
            titleRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Shape7(titleRect, GlassStrong, round).raycastTarget = false;
            Row(titleRect.gameObject, 6, 6, 0, 0, 0f, TextAnchor.MiddleLeft);
            Fit(titleRect.gameObject, true, false);
            Text titleText = FitLabel(Child("Label", titleRect), bold, 14, DrawGroupWords, title);
            titleText.raycastTarget = false;
            return group;
        }

        // A bar button (36 board px high): its chosen fill (set by the view), the hover (dark teal, cyan edge).
        private static Button DrawButton17(RectTransform parent, string name, float width, out Image fill, out RectTransform rect)
        {
            rect = Child(name, parent);
            Size(rect.gameObject, width, 54f, width);
            fill = Shape7(Rect("Fill", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                new Color(0f, 0f, 0f, 0f), round);
            fill.raycastTarget = false;
            RectTransform hoverRect = Rect("Hover", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            Image hover = Shape7(hoverRect, HoverFill, round);
            hover.raycastTarget = true;
            Outline hoverEdge = hoverRect.gameObject.AddComponent<Outline>();
            hoverEdge.effectColor = HoverEdge;
            hoverEdge.effectDistance = new Vector2(1.5f, -1.5f);
            hoverEdge.useGraphicAlpha = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hover;
            ColorBlock colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = Color.white;
            colours.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colours.selectedColor = new Color(1f, 1f, 1f, 0f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0f);
            button.colors = colours;
            return button;
        }

        private static void DrawIcon17(RectTransform button, string icon, float width, float height)
        {
            Sprite picture = AssetDatabase.LoadAssetAtPath<Sprite>(HudArt + icon + ".png");
            RectTransform iconRect = Centred("Icon", button, width, height);
            if (picture == null)
            {
                Label(iconRect, bold, 15, Soft, TextAnchor.MiddleCenter, icon.Substring(0, 1)).raycastTarget = false;
                return;
            }

            Image image = iconRect.gameObject.AddComponent<Image>();
            image.sprite = picture;
            image.preserveAspect = true;
            image.color = Soft;
            image.raycastTarget = false;
        }

        // The key chip at the button's top right (1, 2, 3, E).
        private static void DrawKey17(RectTransform button, string key)
        {
            RectTransform chip = Rect("Key", button, Vector2.one, Vector2.one, Vector2.one, new Vector2(5f, 9f),
                new Vector2(22f, 24f));
            chip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Fill(chip, KeyFill, round, KeyEdge, 1.5f).raycastTarget = false;
            Label(Centred("Label", chip, 22f, 24f), bold, 13, Words, TextAnchor.MiddleCenter, key).raycastTarget = false;
        }

        private static void DrawRule17(RectTransform bar, string name)
        {
            RectTransform rule = Child(name, bar);
            Shape(rule, Edge, null).raycastTarget = false;
            Size(rule.gameObject, 1.5f, 42f, 1.5f);
        }

        private static void DrawHintWords17(RectTransform line, string words)
        {
            Text text = FitLabel(Child("Words", line), medium, 16, DrawHintWords, words);
            text.raycastTarget = false;
        }

        private static void DrawHintKey17(RectTransform line, string key)
        {
            RectTransform chip = Child("Key", line);
            Fill(chip, KeyFill, round, KeyEdge, 1.5f).raycastTarget = false;
            Row(chip.gameObject, 6, 6, 0, 0, 0f, TextAnchor.MiddleCenter);
            Fit(chip.gameObject, true, false);
            Size(chip.gameObject, -1f, 24f);
            FitLabel(Child("Label", chip), bold, 13, Words, key).alignment = TextAnchor.MiddleCenter;
        }

        private static void Assign17(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
