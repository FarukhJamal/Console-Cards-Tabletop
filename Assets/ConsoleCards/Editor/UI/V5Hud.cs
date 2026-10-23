using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// V5 table screen (owner, 2026-10-24: "follow the Figma"). Lays out and skins the table HUD after the Figma V5
    /// screen (Content/UI/Figma/Components/Reference.png) with the owner's exported components, at their drawn size
    /// (the 2x exports of the 1440 design are 2/3 of a UI unit per pixel on the 1920 canvas):
    /// top bar (Top: TRAP FLOOR, Undo, Redo, SHOP, TABLE), round bar (Top: ROUND over the turn), hint bar (Hint),
    /// Rules card (Rules frame, Rule_Entry rows with an orange arrow), player card (Rules frame around
    /// Container_Player_card), action buttons (Button_Enabled), the Log (Log, always showing the newest lines),
    /// + TOOLBOX (Toolbox_Button) and the Controls bar (Controls: the keys as Frame_move chips, then the tools).
    /// Part of Console Cards > UI > Apply Kit Look (UI-1); setting the same values again changes nothing.
    /// </summary>
    public static class V5Hud
    {
        public const string DerivedPath = "Assets/ConsoleCards/Content/UI/Figma/Kit/";

        private const float Margin = 28f;
        private const float BarHeight = 87f; // the Top and Controls bars: 130 export pixels
        private const float ExportScale = 1.5f; // export pixels per UI unit
        private const float BarsCentre = 195f; // round bar and hint bar, from the screen centre
        private const int KeyChipCount = 6;

        private static readonly Color White = UiKit.Hex("e8eced");
        private static readonly Color Muted = UiKit.Hex("9ba4a7");
        private static readonly Color Orange = UiKit.Hex("ff7b2e");
        private static readonly Color Cyan = UiKit.Hex("06eff0");
        private static readonly Color Dark = UiKit.Hex("16191a");
        private static readonly Color ChipDark = UiKit.Hex("1c1c1c");
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        // Export or derived sprite -> 9-slice border (left, bottom, right, top) in export pixels.
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "Toolbox_Button", new Vector4(12f, 12f, 12f, 12f) },
            { "Button_Enabled", new Vector4(16f, 16f, 16f, 16f) },
            { "Frame_move", new Vector4(12f, 12f, 12f, 12f) },
            { "Rule_Entry", new Vector4(10f, 10f, 10f, 10f) },
            { "Container_Player_card", new Vector4(12f, 12f, 12f, 12f) },
            { "Status", new Vector4(10f, 10f, 10f, 10f) },
            { "Line", Vector4.zero },
            { "Undo", Vector4.zero },
            { "Redo", Vector4.zero },
            { "ShopIcon", Vector4.zero },
            { "TableIcon", Vector4.zero },
            { "cyan_on ellipse", Vector4.zero },
        };

        private static readonly Dictionary<string, Vector4> DerivedBorders = new Dictionary<string, Vector4>
        {
            { "V5_HintBar", new Vector4(44f, 0f, 44f, 0f) },
            { "V5_Bullet", Vector4.zero },
            { "V5_Dot", Vector4.zero },
        };

        private static readonly HashSet<string> Prefabs = new HashSet<string>
        {
            "SessionBar", "TrapFloorHud", "RulesCard", "RulesLine", "RulesHeading", "HintStrip", "ActivityLog", "ToolBar",
            "ActionRow",
        };

        private static Font bold;
        private static Font medium;

        /// <summary>The prefabs this pass styles whole (the generic frames pass leaves them alone).</summary>
        public static bool Handles(string prefab) => Prefabs.Contains(prefab);

        public static void Prepare()
        {
            foreach (KeyValuePair<string, Vector4> sprite in Borders)
            {
                string path = KitFrames.FigmaPath + sprite.Key + ".png";
                if (File.Exists(path)) UiKit.ConfigureTexture(path, sprite.Value);
            }

            foreach (KeyValuePair<string, Vector4> sprite in DerivedBorders)
            {
                string path = DerivedPath + sprite.Key + ".png";
                if (File.Exists(path)) UiKit.ConfigureTexture(path, sprite.Value);
            }
        }

        public static void Apply(GameObject root, Action counted)
        {
            if (Figma("Top") == null || Derived("V5_HintBar") == null) return; // exports not in the project yet
            bold = UiKit.RequireFont("Oxanium-Bold.ttf");
            medium = UiKit.RequireFont("Oxanium-Medium.ttf");
            string before = Snapshot(root);
            switch (root.name)
            {
                case "SessionBar":
                    TopBar(root.transform);
                    break;
                case "TrapFloorHud":
                    RoundBar(root.transform);
                    PlayerCard(root.transform);
                    break;
                case "RulesCard":
                    RulesCard(root.transform);
                    break;
                case "RulesLine":
                    RulesLine(root.transform);
                    break;
                case "RulesHeading":
                    Style(Find(root.transform, "Text"), bold, 14, Muted);
                    break;
                case "HintStrip":
                    HintBar(root.transform);
                    break;
                case "ActivityLog":
                    Log(root);
                    break;
                case "ToolBar":
                    ControlsBar(root);
                    break;
                case "ActionRow":
                    ActionButton(root.transform);
                    break;
                case "ComponentToolbox":
                    ToolboxButton(root.transform);
                    break;
                case "FeedbackMessages":
                    Place(Find(root.transform, "Messages"), Vector2.zero, Vector2.zero, new Vector2(Margin, 207f),
                        new Vector2(413f, 184f));
                    break;
            }

            if (Snapshot(root) != before) counted();
        }

        // ---------- Pieces ----------

        // Top bar: cyan dot and game name, Undo, Redo, SHOP, TABLE on the Top bar; no boxes around them.
        private static void TopBar(Transform root)
        {
            RectTransform bar = Find(root, "Bar");
            if (bar == null) return;
            Place(bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(bar.sizeDelta.x, BarHeight));
            Unfit(bar, false, true);
            Row(bar, 50, 50, 22f);
            Skin(Find(bar, "Fill"), Figma("Top"), 130f / BarHeight);
            Off(Find(bar, "Shadow"));

            SessionBarView view = root.GetComponent<SessionBarView>();
            SetColor(view, "gameChipColor", Clear);
            SetColor(view, "tableChipColor", Clear);
            SetColor(view, "iconColor", White);

            RectTransform chip = Find(bar, "Chip");
            Row(chip, 0, 0, 12f);
            RectTransform glyph = Find(chip, "Glyph");
            Picture(glyph, Figma("cyan_on ellipse"), Color.white);
            Size(glyph, 16f, 16f);
            Style(Find(chip, "Words/Title"), bold, 20, White);
            Style(Find(chip, "Words/Subtitle"), medium, 13, Muted);

            IconOnly(Find(bar, "UndoButton"), Figma("Undo"));
            IconOnly(Find(bar, "RedoButton"), Figma("Redo"));
            WordButton(Find(bar, "ShopButton"), Figma("ShopIcon"), "SHOP");
            WordButton(Find(bar, "TableButton"), Figma("TableIcon"), "TABLE");

            // The Table menu is a menu: the orange frame (doc 36 §7).
            RectTransform menu = Find(bar, "TableMenu");
            Skin(Find(menu, "Fill"), Figma("Player Profile"), ExportScale);
            Off(Find(menu, "Shadow"));
            Tint(Find(menu, "Header"), Clear);
            Style(Find(menu, "Header/Title"), bold, 22, White);
        }

        // Round bar: ROUND (orange) over the turn line, the player's dot in front, Keys at the right.
        private static void RoundBar(Transform root)
        {
            RectTransform strip = Find(root, "TurnStrip");
            if (strip == null) return;
            Place(strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(BarsCentre, -Margin), new Vector2(728f, BarHeight));
            Unfit(strip, true, true);
            Skin(strip, Figma("Top"), 130f / BarHeight);
            Row(strip, 50, 50, 14f);

            RectTransform title = Ensure(strip, "Title", 1);
            VerticalLayoutGroup column = title.GetComponent<VerticalLayoutGroup>();
            if (column == null) column = title.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 0f;
            column.childAlignment = TextAnchor.MiddleLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;
            Flexible(title);
            RectTransform round = Find(strip, "Round") ?? Find(title, "Round");
            RectTransform phase = Find(strip, "Phase") ?? Find(title, "Phase");
            if (round != null && round.parent != title) round.SetParent(title, false);
            if (phase != null && phase.parent != title) phase.SetParent(title, false);
            if (round != null) round.SetSiblingIndex(0);
            Style(round, bold, 14, Orange);
            Style(phase, bold, 21, White);

            RectTransform chip = Find(strip, "PlayerChip");
            if (chip != null)
            {
                chip.SetSiblingIndex(0);
                Image dot = chip.GetComponent<Image>();
                if (dot != null)
                {
                    dot.sprite = Derived("V5_Dot");
                    dot.type = Image.Type.Simple;
                    dot.preserveAspect = true;
                }

                Size(chip, 22f, 22f);
                Active(Find(chip, "Icon"), false);
            }

            RectTransform keys = Find(strip, "Keys");
            if (keys != null) keys.SetAsLastSibling();
            Style(keys, bold, 16, White);
        }

        // Player card (top right): the screwed Rules frame around the card body.
        private static void PlayerCard(Transform root)
        {
            RectTransform card = Find(root, "StatusCard");
            if (card == null) return;
            Place(card, Vector2.one, Vector2.one, new Vector2(-Margin, -Margin), new Vector2(358f, card.sizeDelta.y));
            Column(card, 24, 24, 22, 26, 10f);
            Skin(Find(card, "Fill"), Figma("Rules"), ExportScale);
            Off(Find(card, "Shadow"));
            Tint(Find(card, "Header"), Clear);
            Style(Find(card, "Header/Title"), bold, 26, White, TextAnchor.MiddleLeft);
            Tint(Find(card, "HeaderRule"), Clear);

            RectTransform body = Find(card, "Body");
            if (body == null) return;
            Image bodyImage = body.GetComponent<Image>();
            if (bodyImage == null) bodyImage = body.gameObject.AddComponent<Image>();
            Skin(body, Figma("Container_Player_card"), ExportScale);
            bodyImage.raycastTarget = false;
            Column(body, 18, 18, 14, 16, 8f);
            Style(Find(body, "Detail"), medium, 17, White);
            Style(Find(body, "ContainerCounts"), medium, 15, Muted);
            Style(Find(body, "ActionsTitle"), bold, 14, Muted);
            Style(Find(body, "ActionHelp"), medium, 14, Muted);
        }

        // Rules card (left): the screwed frame, "RULES · <set>", rows in Rule_Entry boxes.
        private static void RulesCard(Transform root)
        {
            RectTransform card = Find(root, "Card");
            if (card == null) return;
            Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -127f), new Vector2(461f, card.sizeDelta.y));
            Column(card, 28, 28, 24, 30, 12f);
            Skin(Find(card, "Fill"), Figma("Rules"), ExportScale);
            Off(Find(card, "Shadow"));
            Tint(Find(card, "Header"), Clear);
            RectTransform title = Find(card, "Header/Title");
            if (title != null)
            {
                title.anchoredPosition = new Vector2(0f, title.anchoredPosition.y);
                title.sizeDelta = new Vector2(112f, title.sizeDelta.y);
                Text words = title.GetComponent<Text>();
                if (words != null) words.text = "RULES ·";
            }

            Style(title, bold, 24, White);
            RectTransform setName = Find(card, "Header/SetName");
            if (setName != null)
            {
                setName.anchoredPosition = new Vector2(112f, setName.anchoredPosition.y);
                setName.sizeDelta = new Vector2(-112f - 60f, setName.sizeDelta.y);
            }

            Style(setName, bold, 24, White);
            Tint(Find(card, "Header/CollapseButton"), Clear);
            Tint(Find(card, "Header/CollapseButton/Chevron"), White);
            Skin(Find(card, "Header/HouseRulesBadge"), Figma("Status"), ExportScale);
            Style(Find(card, "Header/HouseRulesBadge/Label"), bold, 12, Dark);
            Tint(Find(card, "HeaderRule"), Clear);
            Tint(Find(card, "Body"), Clear);
            VerticalLayoutGroup lines = Find(card, "Body/Lines")?.GetComponent<VerticalLayoutGroup>();
            if (lines != null) lines.spacing = 10f;
        }

        private static void RulesLine(Transform root)
        {
            Image box = root.GetComponent<Image>();
            if (box == null) box = root.gameObject.AddComponent<Image>();
            Skin(root, Figma("Rule_Entry"), ExportScale);
            box.raycastTarget = false;
            HorizontalLayoutGroup row = Row(root as RectTransform, 14, 16, 12f, 12, 12);
            if (row != null) row.childAlignment = TextAnchor.MiddleLeft;

            RectTransform bullet = Ensure(root as RectTransform, "Bullet", 0);
            Picture(bullet, Derived("V5_Bullet"), Color.white);
            Size(bullet, 12f, 14f);
            Active(Find(root, "Number"), false);
            Style(Find(root, "Text"), medium, 17, White);
            Tint(Find(root, "ChangedMark"), Cyan);
            Skin(Find(root, "ChangeButton"), Figma("Frame_move"), ExportScale);
            Style(Find(root, "ChangeButton/Label"), bold, 13, White);
        }

        // Hint bar under the round bar: the HINT chip, the line, Later and Got it as dark chips.
        private static void HintBar(Transform root)
        {
            RectTransform card = Find(root, "Card");
            if (card == null) return;
            Place(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(BarsCentre, -127f), new Vector2(728f, 72f));
            Row(card, 32, 32, 14f);
            Skin(Find(card, "Fill"), Derived("V5_HintBar"), 108f / 72f);
            Off(Find(card, "Shadow"));
            RectTransform badge = Find(card, "Badge");
            Skin(badge, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ConsoleCards/Content/UI/Kit/KitAccent.png"), 1f, Cyan);
            Size(badge, 62f, 30f);
            Style(Find(badge, "Label"), bold, 15, Dark, TextAnchor.MiddleCenter);
            Style(Find(card, "Text"), medium, 17, White);
            ChipButton(Find(card, "LaterButton"), 34f);
            ChipButton(Find(card, "GotItButton"), 34f);
        }

        // The Log (bottom left, above + TOOLBOX): always open on the newest lines; L or a click grows it.
        private static void Log(GameObject root)
        {
            Transform t = root.transform;
            Active(Find(t, "Tab"), false);
            RectTransform panel = Find(t, "Panel");
            if (panel == null) return;
            Place(panel, Vector2.zero, Vector2.zero, new Vector2(Margin, 95f), new Vector2(413f, 300f));
            Skin(Find(panel, "Fill"), Figma("Log"), ExportScale);
            Off(Find(panel, "Shadow"));
            Tint(Find(panel, "Header"), Clear);
            Style(Find(panel, "Header/Title"), bold, 17, Cyan);
            Style(Find(panel, "Viewport/Content/Lines"), medium, 16, White);

            ActivityLogView view = root.GetComponent<ActivityLogView>();
            if (view == null) return;
            SerializedObject so = new SerializedObject(view);
            SetFloat(so, "peekHeight", 96f);
            SetReference(so, "panelRect", panel);
            SetReference(so, "panelButton", panel.GetComponent<Button>());
            SetReference(so, "header", Find(panel, "Header")?.gameObject);
            SetReference(so, "viewport", Find(panel, "Viewport"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Controls bar (bottom right): the keys as chips, a rule, then the tools as dark chips (the chosen one cyan).
        private static void ControlsBar(GameObject root)
        {
            Transform t = root.transform;
            RectTransform bar = Find(t, "Bar");
            if (bar == null) return;
            Place(bar, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, 24f), new Vector2(bar.sizeDelta.x, BarHeight));
            Unfit(bar, false, true);
            Skin(bar, Figma("Controls"), 130f / BarHeight);
            Row(bar, 52, 52, 14f);

            RectTransform strip = Find(bar, "KeysStrip");
            Tint(strip, Clear);
            Row(strip, 0, 0, 14f);
            Active(Find(strip, "Keys"), false);
            ToolBarView view = root.GetComponent<ToolBarView>();
            SerializedObject so = view != null ? new SerializedObject(view) : null;
            SerializedProperty chips = so?.FindProperty("keyChips");
            if (chips != null && chips.arraySize != KeyChipCount) chips.arraySize = KeyChipCount;
            for (int i = 0; i < KeyChipCount; i++)
            {
                RectTransform chip = Ensure(strip, "Chip" + i, -1);
                Row(chip, 0, 0, 10f);
                RectTransform divider = Ensure(chip, "Divider", 0);
                Picture(divider, Figma("Line"), Color.white);
                Size(divider, 2f, 27f);
                RectTransform key = Ensure(chip, "Key", 1);
                Skin(key, Figma("Frame_move"), ExportScale);
                Row(key, 10, 10, 0f);
                Size(key, -1f, 34f);
                Text keyText = Words(Ensure(key, "Text", 0), bold, 15, White, "DRAG");
                Text action = Words(Ensure(chip, "Action", 2), medium, 16, White, "move");
                if (chips != null)
                {
                    SerializedProperty entry = chips.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("root").objectReferenceValue = chip.gameObject;
                    entry.FindPropertyRelative("key").objectReferenceValue = keyText;
                    entry.FindPropertyRelative("action").objectReferenceValue = action;
                    entry.FindPropertyRelative("divider").objectReferenceValue = divider.gameObject;
                }

                Active(divider, i > 0);
                Active(chip, i == 0);
            }

            RectTransform end = Ensure(strip, "End", -1);
            end.SetAsLastSibling();
            Picture(end, Figma("Line"), Color.white);
            Size(end, 2f, 40f);

            RectTransform tools = Find(bar, "Tools");
            Off(Find(tools, "Shadow"));
            Tint(Find(tools, "Fill"), Clear);
            Picture(Find(tools, "Divider"), Figma("Line"), Color.white);
            ChipButton(Find(tools, "KeysButton"), -1f);
            ChipButton(Find(tools, "HelpButton"), -1f);
            Tint(Find(tools, "KeysButton/Icon"), White);

            // SelectionActions and DrawActions open just above the bar.
            MoveY(Find(t, "SelectionActions"), 123f);
            MoveY(Find(t, "DrawActions"), 123f);

            if (so == null) return;
            SetFloatColor(so, "idleFill", ChipDark);
            SetFloatColor(so, "activeFill", Cyan);
            SetFloatColor(so, "idleIcon", White);
            SetFloatColor(so, "activeIcon", Dark);
            SetFloat(so, "keyChipsWidth", 560f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Rows of actions (End turn, Skip turn, menu rows): the dark Button_Enabled plate.
        private static void ActionButton(Transform root)
        {
            Skin(root, Figma("Button_Enabled"), ExportScale);
            Size(root as RectTransform, -1f, 54f);
            Style(Find(root, "Label"), bold, 18, White);
        }

        // + TOOLBOX bottom left (Toolbox_Button); the placing card moves under the hint bar.
        private static void ToolboxButton(Transform root)
        {
            RectTransform open = Find(root, "OpenButton");
            if (open != null)
            {
                Place(open, Vector2.zero, Vector2.zero, new Vector2(Margin, 24f), new Vector2(196f, 55f));
                Skin(Find(open, "Fill"), Figma("Toolbox_Button"), ExportScale);
                Off(Find(open, "Shadow"));
                RectTransform label = Find(open, "Label");
                Style(label, bold, 20, White, TextAnchor.MiddleCenter);
                Text words = label != null ? label.GetComponent<Text>() : null;
                if (words != null) words.text = "+  TOOLBOX";
            }

            RectTransform placing = Find(root, "PlacingCard");
            if (placing != null)
            {
                Place(placing, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(BarsCentre, -215f), placing.sizeDelta);
            }
        }

        // ---------- Helpers ----------

        private static void IconOnly(RectTransform button, Sprite icon)
        {
            if (button == null) return;
            Tint(button, Clear);
            RectTransform iconRect = Find(button, "Icon");
            Image image = iconRect != null ? iconRect.GetComponent<Image>() : null;
            if (image != null && icon != null)
            {
                image.sprite = icon;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                iconRect.sizeDelta = new Vector2(27f, 27f);
            }

            Hover(button.GetComponent<Button>(), image);
        }

        private static void WordButton(RectTransform button, Sprite icon, string word)
        {
            if (button == null) return;
            Tint(button, Clear);
            Row(button, 4, 4, 12f);
            RectTransform iconRect = Find(button, "Icon");
            Picture(iconRect, icon, White);
            Size(iconRect, 27f, 27f);
            RectTransform label = Find(button, "Label");
            Text text = label != null ? label.GetComponent<Text>() : null;
            if (text != null) text.text = word;
            Style(label, bold, 20, White);
            Hover(button.GetComponent<Button>(), text);
        }

        private static void ChipButton(RectTransform button, float height)
        {
            if (button == null) return;
            Skin(button, Figma("Frame_move"), ExportScale);
            if (height > 0f) Size(button, -1f, height);
            Style(Find(button, "Label"), bold, 14, White);
        }

        // Words light up cyan under the pointer (the button itself is see-through).
        private static void Hover(Button button, Graphic target)
        {
            if (button == null || target == null) return;
            button.targetGraphic = target;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Cyan;
            colors.pressedColor = new Color(Cyan.r * 0.8f, Cyan.g * 0.8f, Cyan.b * 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white; // the view dims disabled icons itself
            button.colors = colors;
        }

        private static RectTransform Find(Transform root, string path)
        {
            return root != null ? root.Find(path) as RectTransform : null;
        }

        private static RectTransform Ensure(RectTransform parent, string name, int index)
        {
            if (parent == null) return null;
            RectTransform child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                GameObject go = new GameObject(name, typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                child = (RectTransform)go.transform;
                child.SetParent(parent, false);
            }

            if (index >= 0 && child.GetSiblingIndex() != index) child.SetSiblingIndex(index);
            return child;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void MoveY(RectTransform rect, float y)
        {
            if (rect != null) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        private static void Unfit(RectTransform rect, bool width, bool height)
        {
            ContentSizeFitter fitter = rect != null ? rect.GetComponent<ContentSizeFitter>() : null;
            if (fitter == null) return;
            if (width) fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            if (height) fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private static HorizontalLayoutGroup Row(RectTransform rect, int left, int right, float spacing)
        {
            return Row(rect, left, right, spacing, 0, 0);
        }

        private static HorizontalLayoutGroup Row(RectTransform rect, int left, int right, float spacing, int top, int bottom)
        {
            if (rect == null) return null;
            HorizontalLayoutGroup row = rect.GetComponent<HorizontalLayoutGroup>();
            if (row == null) row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(left, right, top, bottom);
            row.spacing = spacing;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return row;
        }

        private static void Column(RectTransform rect, int left, int right, int top, int bottom, float spacing)
        {
            VerticalLayoutGroup column = rect != null ? rect.GetComponent<VerticalLayoutGroup>() : null;
            if (column == null) return;
            column.padding = new RectOffset(left, right, top, bottom);
            column.spacing = spacing;
        }

        private static void Size(RectTransform rect, float width, float height)
        {
            if (rect == null) return;
            LayoutElement element = rect.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f)
            {
                element.minWidth = width;
                element.preferredWidth = width;
            }

            if (height >= 0f)
            {
                element.minHeight = height;
                element.preferredHeight = height;
            }
        }

        private static void Flexible(RectTransform rect)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
        }

        // A 9-sliced export at its drawn size (pixels per unit multiplier = export pixels per UI unit).
        private static void Skin(Transform target, Sprite sprite, float exportScale, Color? color = null)
        {
            if (target == null || sprite == null) return;
            Image image = target.GetComponent<Image>();
            if (image == null) image = target.gameObject.AddComponent<Image>();
            image.enabled = true;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = exportScale;
            image.color = color ?? Color.white;
        }

        // A whole picture (icon, dot, rule) in its rect.
        private static void Picture(RectTransform target, Sprite sprite, Color color)
        {
            if (target == null || sprite == null) return;
            Image image = target.GetComponent<Image>();
            if (image == null) image = target.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void Tint(Transform target, Color color)
        {
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image != null) image.color = color;
        }

        private static void Off(Transform target)
        {
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image != null) image.enabled = false;
        }

        private static void Active(Transform target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
        }

        private static void Style(Transform target, Font font, int size, Color color, TextAnchor? alignment = null)
        {
            Text text = target != null ? target.GetComponent<Text>() : null;
            if (text == null) return;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            if (alignment.HasValue) text.alignment = alignment.Value;
        }

        private static Text Words(RectTransform rect, Font font, int size, Color color, string sample)
        {
            Text text = rect.GetComponent<Text>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<Text>();
                text.text = sample;
            }

            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static void SetColor(UnityEngine.Object target, string property, Color color)
        {
            if (target == null) return;
            SerializedObject so = new SerializedObject(target);
            SetFloatColor(so, property, color);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloatColor(SerializedObject so, string property, Color color)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.colorValue = color;
        }

        private static void SetFloat(SerializedObject so, string property, float value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.floatValue = value;
        }

        private static void SetReference(SerializedObject so, string property, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.objectReferenceValue = value;
        }

        private static Sprite Figma(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(KitFrames.FigmaPath + name + ".png");
        }

        private static Sprite Derived(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(DerivedPath + name + ".png");
        }

        // Everything the pass can change, to tell whether a run changed the prefab.
        private static string Snapshot(GameObject root)
        {
            StringBuilder text = new StringBuilder(4096);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                text.Append(t.name).Append(t.gameObject.activeSelf).Append(t.GetSiblingIndex())
                    .Append(t.parent != null ? t.parent.name : string.Empty);
                foreach (Component component in t.GetComponents<Component>())
                {
                    if (component != null) text.Append(EditorJsonUtility.ToJson(component));
                }
            }

            return text.ToString();
        }
    }
}
