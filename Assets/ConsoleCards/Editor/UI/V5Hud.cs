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
    /// V5 table screen (owner, 2026-10-24: "match everything the Figma reference is doing"). Lays out and skins the
    /// table HUD as the Figma V5 screen (Content/UI/Figma/Components/Reference.png) with the owner's exported
    /// components at their drawn size (the 2x exports of the 1440 design are 2/3 of a UI unit per pixel on the 1920
    /// canvas):
    /// top bar (Top: dot, game, Undo, Redo, SHOP, TABLE); round bar (Top: ROUND over "RED'S TURN - 2 MOVES", the
    /// players with coloured dots, the turn player's ringed); hint bar; Rules card ("RULES · GAME ///", Rule_Entry rows
    /// with an orange arrow that light cyan on hover, footer "House rules: 1 change" and &lt; FOLD &gt;); player card
    /// ("RED PLAYER ///" on Title_Label, rows Avatar, Keys, Status chip, In hand chip, Floor) with END TURN and the
    /// other actions under it as screwed buttons; the Log and + TOOLBOX bottom left in small screwed frames; the
    /// Controls bar bottom right holding only the keys, the table tools in a small screwed bar above it.
    /// Part of Console Cards > UI > Apply Kit Look (UI-1); setting the same values again changes nothing.
    /// </summary>
    public static class V5Hud
    {
        public const string DerivedPath = "Assets/ConsoleCards/Content/UI/Figma/Kit/";

        private const float Margin = 28f;
        private const float BarHeight = 87f; // the Top and Controls bars: 130 export pixels
        private const float ExportScale = 1.5f; // export pixels per UI unit
        private const float SmallFrameScale = 3f; // the Rules frame at half size: small screwed panels and buttons
        private const float BarsCentre = 195f; // round bar and hint bar, from the screen centre
        private const float CardWidth = 358f;
        private const int KeyChipCount = 6;
        private const int PlayerSlotCount = 6;

        private static readonly Color White = UiKit.Hex("e8eced");
        private static readonly Color Muted = UiKit.Hex("9ba4a7");
        private static readonly Color Slashes = UiKit.Hex("6b7275");
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
            { "Selected_Rule_entry", new Vector4(14f, 10f, 10f, 10f) },
            { "Container_Player_card", new Vector4(12f, 12f, 12f, 12f) },
            { "Title_Label", Vector4.zero },
            { "Status", new Vector4(10f, 10f, 10f, 10f) },
            { "Line", Vector4.zero },
            { "Line 1", Vector4.zero },
            { "Undo", Vector4.zero },
            { "Redo", Vector4.zero },
            { "ShopIcon", Vector4.zero },
            { "TableIcon", Vector4.zero },
            { "leftArrowIcon", Vector4.zero },
            { "RightArrowIcon", Vector4.zero },
            { "cyan_on ellipse", Vector4.zero },
        };

        private static readonly Dictionary<string, Vector4> DerivedBorders = new Dictionary<string, Vector4>
        {
            { "V5_HintBar", new Vector4(44f, 0f, 44f, 0f) },
            { "V5_Bullet", Vector4.zero },
            { "V5_Dot", Vector4.zero },
            { "V5_DotPlain", Vector4.zero },
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
                    RoundBar(root);
                    PlayerCard(root);
                    break;
                case "RulesCard":
                    RulesCard(root);
                    break;
                case "RulesLine":
                    RulesLine(root);
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
                    Place(Find(root.transform, "Messages"), Vector2.zero, Vector2.zero, new Vector2(Margin, 245f),
                        new Vector2(420f, 184f));
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
            Row(bar, 50, 50, 30f);
            Skin(Find(bar, "Fill"), Figma("Top"), 130f / BarHeight);
            Off(Find(bar, "Shadow"));

            SessionBarView view = root.GetComponent<SessionBarView>();
            if (view != null)
            {
                SerializedObject so = new SerializedObject(view);
                SetColorValue(so, "gameChipColor", Clear);
                SetColorValue(so, "tableChipColor", Clear);
                SetColorValue(so, "iconColor", White);
                SetBool(so, "showSubtitle", false);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

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

        // Round bar: ROUND (orange) over the turn line; the players at the right, the turn player's dot ringed.
        private static void RoundBar(GameObject root)
        {
            RectTransform strip = Find(root.transform, "TurnStrip");
            if (strip == null) return;
            Place(strip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(BarsCentre, -Margin), new Vector2(728f, BarHeight));
            Unfit(strip, true, true);
            Skin(strip, Figma("Top"), 130f / BarHeight);
            Row(strip, 50, 50, 18f);

            RectTransform title = Ensure(strip, "Title", 0);
            VerticalLayoutGroup column = title.GetComponent<VerticalLayoutGroup>();
            if (column == null) column = title.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(0, 0, 0, 0);
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
            Style(round, bold, 15, Orange);
            Style(phase, bold, 22, White);

            // The single colour chip is replaced by the players' dots (the view keeps it hidden).
            RectTransform chip = Find(strip, "PlayerChip");
            if (chip != null) chip.SetAsLastSibling();
            Active(chip, false);

            RectTransform players = Ensure(strip, "Players", 1);
            Row(players, 0, 0, 16f);
            TrapFloorHudView view = root.GetComponent<TrapFloorHudView>();
            SerializedObject so = view != null ? new SerializedObject(view) : null;
            SerializedProperty slots = so?.FindProperty("playerSlots");
            if (slots != null && slots.arraySize != PlayerSlotCount) slots.arraySize = PlayerSlotCount;
            for (int i = 0; i < PlayerSlotCount; i++)
            {
                RectTransform slot = Ensure(players, "Player" + i, i);
                Row(slot, 0, 0, 7f);
                RectTransform dot = Ensure(slot, "Dot", 0);
                Picture(dot, Derived("V5_DotPlain"), Color.white);
                Size(dot, 20f, 20f);
                Text name = Words(Ensure(slot, "Name", 1), medium, 17, White, "Blue");
                if (slots != null)
                {
                    SerializedProperty entry = slots.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("root").objectReferenceValue = slot.gameObject;
                    entry.FindPropertyRelative("dot").objectReferenceValue = dot.GetComponent<Image>();
                    entry.FindPropertyRelative("name").objectReferenceValue = name;
                }

                Active(slot, i < 4);
            }

            // The Keys move to the player card (the view hides this line while the card shows).
            RectTransform keys = Find(strip, "Keys");
            if (keys != null) keys.SetSiblingIndex(2);
            Style(keys, bold, 16, White);

            if (so == null) return;
            SetReference(so, "activeDot", Derived("V5_Dot"));
            SetReference(so, "idleDot", Derived("V5_DotPlain"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Player card (top right): "RED PLAYER ///", the five rows; END TURN and the other actions under the card.
        private static void PlayerCard(GameObject root)
        {
            Transform t = root.transform;
            RectTransform card = Find(t, "StatusCard") ?? Find(t, "RightColumn/StatusCard");
            if (card == null) return;

            // A right column: the card, then the buttons, each sized by its content.
            RectTransform right = Ensure((RectTransform)t, "RightColumn", -1);
            Place(right, Vector2.one, Vector2.one, new Vector2(-Margin, -Margin), new Vector2(CardWidth, right.sizeDelta.y));
            VerticalLayoutGroup rightColumn = right.GetComponent<VerticalLayoutGroup>();
            if (rightColumn == null) rightColumn = right.gameObject.AddComponent<VerticalLayoutGroup>();
            ColumnLayout(rightColumn, 0, 0, 0, 0, 10f);
            Fit(right, false, true);
            if (card.parent != right) card.SetParent(right, false);
            card.SetSiblingIndex(0);
            Unfit(card, true, true);

            Column(card, 16, 16, 16, 18, 12f);
            Skin(Find(card, "Fill"), Figma("Rules"), ExportScale);
            Off(Find(card, "Shadow"));
            RectTransform header = Find(card, "Header");
            Stretched(header, Figma("Title_Label"), Color.white);
            Size(header, -1f, 64f);
            RectTransform cardTitle = Find(header, "Title");
            if (cardTitle != null)
            {
                cardTitle.anchorMin = Vector2.zero;
                cardTitle.anchorMax = Vector2.one;
                cardTitle.pivot = new Vector2(0f, 0.5f);
                cardTitle.offsetMin = new Vector2(18f, 0f);
                cardTitle.offsetMax = new Vector2(-70f, 0f);
            }

            Style(cardTitle, bold, 28, White, TextAnchor.MiddleLeft);
            SlashMark(header, Orange, 28);
            Tint(Find(card, "HeaderRule"), Clear);

            RectTransform body = Find(card, "Body");
            if (body == null) return;
            Skin(body, Figma("Container_Player_card"), ExportScale);
            body.GetComponent<Image>().raycastTarget = false;
            Column(body, 18, 18, 4, 4, 0f);
            Style(Find(body, "Detail"), medium, 17, White);
            Style(Find(body, "ContainerCounts"), medium, 15, Muted);
            Style(Find(body, "ActionsTitle"), bold, 14, Muted);
            Style(Find(body, "ActionHelp"), medium, 14, Muted);

            RectTransform rows = Ensure(body, "Rows", 0);
            VerticalLayoutGroup rowsColumn = rows.GetComponent<VerticalLayoutGroup>();
            if (rowsColumn == null) rowsColumn = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            ColumnLayout(rowsColumn, 0, 0, 0, 0, 0f);
            Text avatar = TextRow(rows, 0, "Avatar", "Clairvoyant", 18);
            Text keys = TextRow(rows, 2, "Keys", "1 / 3", 22);
            Text status = ChipRow(rows, 4, "Status", "OVERCHARGED +1", Figma("Status"), Dark, out Image statusChip);
            Text inHand = ChipRow(rows, 6, "In hand", "4 CARDS", Figma("Frame_move"), White, out _);
            Text floor = TextRow(rows, 8, "Floor", "1 · Easy", 18);
            for (int i = 1; i < 8; i += 2) Separator(rows, "Rule" + i, i);

            // The buttons under the card: Floorfall details, then the actions.
            RectTransform buttons = Ensure(right, "Buttons", 1);
            VerticalLayoutGroup buttonsColumn = buttons.GetComponent<VerticalLayoutGroup>();
            if (buttonsColumn == null) buttonsColumn = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            ColumnLayout(buttonsColumn, 0, 0, 0, 0, 8f);
            RectTransform floorfall = Find(body, "Floorfall") ?? Find(buttons, "Floorfall");
            if (floorfall != null && floorfall.parent != buttons) floorfall.SetParent(buttons, false);
            if (floorfall != null) floorfall.SetSiblingIndex(0);
            Skin(floorfall, Figma("Rules"), SmallFrameScale);
            VerticalLayoutGroup floorfallColumn = floorfall != null ? floorfall.GetComponent<VerticalLayoutGroup>() : null;
            if (floorfallColumn != null) floorfallColumn.padding = new RectOffset(22, 22, 14, 14);
            RectTransform actions = Find(body, "Actions") ?? Find(buttons, "Actions");
            if (actions != null && actions.parent != buttons) actions.SetParent(buttons, false);
            if (actions != null) actions.SetSiblingIndex(1);
            VerticalLayoutGroup actionsColumn = actions != null ? actions.GetComponent<VerticalLayoutGroup>() : null;
            if (actionsColumn != null) ColumnLayout(actionsColumn, 0, 0, 0, 0, 8f);

            PopupActionRowView template = ActionTemplate((RectTransform)t);

            TrapFloorHudView view = root.GetComponent<TrapFloorHudView>();
            if (view == null) return;
            SerializedObject so = new SerializedObject(view);
            SetReference(so, "cardRows", rows.gameObject);
            SetReference(so, "cardTitle", cardTitle != null ? cardTitle.GetComponent<Text>() : null);
            SetReference(so, "avatarValue", avatar);
            SetReference(so, "keysValue", keys);
            SetReference(so, "statusChip", statusChip);
            SetReference(so, "statusValue", status);
            SetReference(so, "inHandValue", inHand);
            SetReference(so, "floorValue", floor);
            SetReference(so, "actionTemplate", template);
            SetColorValue(so, "statusText", Dark);
            SetColorValue(so, "noStatusText", Muted);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // END TURN, SKIP TURN...: a small screwed frame, a dot (cyan for End Turn) and the word; the view clones it.
        private static PopupActionRowView ActionTemplate(RectTransform hud)
        {
            RectTransform button = Ensure(hud, "ActionTemplate", -1);
            Active(button, false);
            Skin(button, Figma("Rules"), SmallFrameScale);
            Image plate = button.GetComponent<Image>();
            Button press = button.GetComponent<Button>();
            if (press == null) press = button.gameObject.AddComponent<Button>();
            press.targetGraphic = plate;
            ColorBlock colors = press.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            press.colors = colors;
            HorizontalLayoutGroup row = Row(button, 24, 24, 14f);
            row.childAlignment = TextAnchor.MiddleCenter;
            Size(button, -1f, 58f);
            RectTransform dot = Ensure(button, "Dot", 0);
            Picture(dot, Derived("V5_DotPlain"), Cyan);
            Size(dot, 18f, 18f);
            Text label = Words(Ensure(button, "Label", 1), bold, 21, White, "END TURN");
            PopupActionRowView view = button.GetComponent<PopupActionRowView>();
            if (view == null) view = button.gameObject.AddComponent<PopupActionRowView>();
            SerializedObject so = new SerializedObject(view);
            SetReference(so, "button", press);
            SetReference(so, "label", label);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // Rules card (left): "RULES · TRAP FLOOR ///", the rows, footer "House rules: 1 change" and < FOLD >.
        private static void RulesCard(GameObject root)
        {
            Transform t = root.transform;
            RectTransform card = Find(t, "Card");
            if (card == null) return;
            Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -127f), new Vector2(461f, card.sizeDelta.y));
            Column(card, 26, 26, 22, 22, 12f);
            Skin(Find(card, "Fill"), Figma("Rules"), ExportScale);
            Off(Find(card, "Shadow"));
            RectTransform header = Find(card, "Header");
            Tint(header, Clear);
            RectTransform title = Find(header, "Title");
            if (title != null)
            {
                title.anchorMin = Vector2.zero;
                title.anchorMax = Vector2.one;
                title.pivot = new Vector2(0f, 0.5f);
                title.offsetMin = new Vector2(4f, 0f);
                title.offsetMax = new Vector2(-60f, 0f);
            }

            Style(title, bold, 26, White, TextAnchor.MiddleLeft);
            Active(Find(header, "SetName"), false);
            Active(Find(header, "CollapseButton"), false);
            Skin(Find(header, "HouseRulesBadge"), Figma("Status"), ExportScale);
            Style(Find(header, "HouseRulesBadge/Label"), bold, 12, Dark);
            SlashMark(header, Slashes, 26);
            Tint(Find(card, "HeaderRule"), Clear);
            RectTransform body = Find(card, "Body");
            Tint(body, Clear);
            Size(body, -1f, 345f);
            VerticalLayoutGroup lines = Find(card, "Body/Lines")?.GetComponent<VerticalLayoutGroup>();
            if (lines != null) lines.spacing = 10f;

            // Footer: the house rules line, then < FOLD > (previous set, fold, next set).
            RectTransform footer = Ensure(card, "Footer", body != null ? body.GetSiblingIndex() + 1 : -1);
            Row(footer, 2, 0, 10f);
            Size(footer, -1f, 44f);
            Text info = Words(Ensure(footer, "Info", 0), medium, 16, Muted, "House rules: 1 change");
            Flexible((RectTransform)info.transform);
            RectTransform pill = Ensure(footer, "Fold", 1);
            Skin(pill, Figma("Frame_move"), ExportScale);
            pill.GetComponent<Image>().raycastTarget = false;
            HorizontalLayoutGroup pillRow = Row(pill, 12, 12, 16f);
            pillRow.childAlignment = TextAnchor.MiddleCenter;
            Size(pill, -1f, 40f);
            Button previous = ArrowButton(pill, "Previous", 0, Figma("leftArrowIcon"));
            RectTransform wordRect = Ensure(pill, "Word", 1);
            Text word = Words(wordRect, bold, 16, White, "FOLD");
            word.raycastTarget = true;
            Button fold = wordRect.GetComponent<Button>();
            if (fold == null) fold = wordRect.gameObject.AddComponent<Button>();
            Hover(fold, word);
            Button next = ArrowButton(pill, "Next", 2, Figma("RightArrowIcon"));

            RulesCardView view = root.GetComponent<RulesCardView>();
            if (view == null) return;
            SerializedObject so = new SerializedObject(view);
            SetReference(so, "titleLabel", title != null ? title.GetComponent<Text>() : null);
            SetReference(so, "footerLabel", info);
            SetReference(so, "foldButton", fold);
            SetReference(so, "foldLabel", word);
            SetReference(so, "foldPreviousButton", previous);
            SetReference(so, "foldNextButton", next);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A rule: Rule_Entry box, orange arrow, text; the row lights (Selected_Rule_entry) and proposes a change.
        private static void RulesLine(GameObject root)
        {
            RectTransform rect = (RectTransform)root.transform;
            Skin(rect, Figma("Rule_Entry"), ExportScale);
            Image box = root.GetComponent<Image>();
            box.raycastTarget = true;
            Button press = root.GetComponent<Button>();
            if (press == null) press = root.AddComponent<Button>();
            press.targetGraphic = box;
            press.transition = Selectable.Transition.SpriteSwap;
            SpriteState states = press.spriteState;
            states.highlightedSprite = Figma("Selected_Rule_entry");
            states.pressedSprite = Figma("Selected_Rule_entry");
            states.selectedSprite = null;
            states.disabledSprite = null;
            press.spriteState = states;
            Navigation navigation = press.navigation;
            navigation.mode = Navigation.Mode.None;
            press.navigation = navigation;
            HorizontalLayoutGroup row = Row(rect, 14, 16, 12f, 12, 12);
            row.childAlignment = TextAnchor.MiddleLeft;

            RectTransform bullet = Ensure(rect, "Bullet", 0);
            Picture(bullet, Derived("V5_Bullet"), Color.white);
            Size(bullet, 12f, 14f);
            Active(Find(rect, "Number"), false);
            Style(Find(rect, "Text"), medium, 17, White);
            Tint(Find(rect, "ChangedMark"), Cyan);
            Skin(Find(rect, "ChangeButton"), Figma("Frame_move"), ExportScale);
            Style(Find(rect, "ChangeButton/Label"), bold, 13, White);

            RulesCardLineView view = root.GetComponent<RulesCardLineView>();
            if (view == null) return;
            SerializedObject so = new SerializedObject(view);
            SetReference(so, "rowButton", press);
            so.ApplyModifiedPropertiesWithoutUndo();
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

        // The Log (bottom left, above + TOOLBOX): a small screwed panel always open on the newest lines.
        private static void Log(GameObject root)
        {
            Transform t = root.transform;
            Active(Find(t, "Tab"), false);
            RectTransform panel = Find(t, "Panel");
            if (panel == null) return;
            Place(panel, Vector2.zero, Vector2.zero, new Vector2(Margin, 125f), new Vector2(420f, 300f));
            Skin(Find(panel, "Fill"), Figma("Rules"), SmallFrameScale);
            Off(Find(panel, "Shadow"));
            Tint(Find(panel, "Header"), Clear);
            Style(Find(panel, "Header/Title"), bold, 17, White);
            Style(Find(panel, "Viewport/Content/Lines"), medium, 17, White);

            ActivityLogView view = root.GetComponent<ActivityLogView>();
            if (view == null) return;
            SerializedObject so = new SerializedObject(view);
            SetFloat(so, "peekHeight", 110f);
            SetFloat(so, "peekInset", 22f);
            SetReference(so, "panelRect", panel);
            SetReference(so, "panelButton", panel.GetComponent<Button>());
            SetReference(so, "header", Find(panel, "Header")?.gameObject);
            SetReference(so, "viewport", Find(panel, "Viewport"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Controls bar (bottom right): only the keys, as chips; the table tools sit in a small screwed bar above it.
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
            Active(Find(strip, "End"), false);
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

            // The tools: out of the Controls bar into their own small bar above its right end.
            RectTransform tools = Find(bar, "Tools") ?? Find(t, "Tools");
            if (tools != null)
            {
                if (tools.parent != t) tools.SetParent(t, false);
                Place(tools, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, 24f + BarHeight + 10f), tools.sizeDelta);
                Fit(tools, true, true);
                Row(tools, 18, 18, 8f, 10, 10);
                Off(Find(tools, "Shadow"));
                Skin(Find(tools, "Fill"), Figma("Rules"), SmallFrameScale);
                Picture(Find(tools, "Divider"), Figma("Line"), Color.white);
                ChipButton(Find(tools, "KeysButton"), -1f);
                ChipButton(Find(tools, "HelpButton"), -1f);
                Tint(Find(tools, "KeysButton/Icon"), White);
            }

            // SelectionActions and DrawActions open above the tools.
            MoveY(Find(t, "SelectionActions"), 24f + BarHeight + 10f + 76f + 10f);
            MoveY(Find(t, "DrawActions"), 24f + BarHeight + 10f + 76f + 10f);

            if (so == null) return;
            SetColorValue(so, "idleFill", ChipDark);
            SetColorValue(so, "activeFill", Cyan);
            SetColorValue(so, "idleIcon", White);
            SetColorValue(so, "activeIcon", Dark);
            SetFloat(so, "keyChipsWidth", 720f);
            SetReference(so, "keysBar", bar.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Rows of actions in popups (the Shop's list): the dark Button_Enabled plate.
        private static void ActionButton(Transform root)
        {
            Skin(root, Figma("Button_Enabled"), ExportScale);
            Size(root as RectTransform, -1f, 54f);
            Style(Find(root, "Label"), bold, 18, White);
        }

        // + TOOLBOX bottom left in a small screwed frame; the placing card moves under the hint bar.
        private static void ToolboxButton(Transform root)
        {
            RectTransform open = Find(root, "OpenButton");
            if (open != null)
            {
                Place(open, Vector2.zero, Vector2.zero, new Vector2(Margin, 24f), new Vector2(228f, 74f));
                Skin(Find(open, "Fill"), Figma("Rules"), SmallFrameScale);
                Off(Find(open, "Shadow"));
                RectTransform label = Find(open, "Label");
                Style(label, bold, 22, White, TextAnchor.MiddleCenter);
                Text words = label != null ? label.GetComponent<Text>() : null;
                if (words != null) words.text = "+  TOOLBOX";
            }

            // The open Toolbox ends above the Log (it ran down over it) and starts level with the Rules card.
            foreach (string path in new[] { "PanelGroup/Panel", "PanelGroup/PanelShadow" })
            {
                RectTransform panel = Find(root, path);
                if (panel == null) continue;
                panel.anchorMin = Vector2.zero;
                panel.anchorMax = new Vector2(0f, 1f);
                panel.offsetMin = new Vector2(Margin, 125f + 110f + 12f);
                panel.offsetMax = new Vector2(Margin + 564f, -127f);
            }

            Off(Find(root, "PanelGroup/PanelShadow"));

            RectTransform placing = Find(root, "PlacingCard");
            if (placing != null)
            {
                Place(placing, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(BarsCentre, -215f), placing.sizeDelta);
            }
        }

        // ---------- Row builders ----------

        private static Text TextRow(RectTransform rows, int index, string label, string sample, int valueSize)
        {
            RectTransform row = RowShell(rows, label, index);
            Text value = Words(Ensure(row, "Value", 1), valueSize > 18 ? bold : medium, valueSize, White, sample);
            value.alignment = TextAnchor.MiddleRight;
            return value;
        }

        private static Text ChipRow(RectTransform rows, int index, string label, string sample, Sprite chipSprite,
            Color textColor, out Image chipImage)
        {
            RectTransform row = RowShell(rows, label, index);
            RectTransform chip = Ensure(row, "Chip", 1);
            Skin(chip, chipSprite, ExportScale);
            chipImage = chip.GetComponent<Image>();
            chipImage.raycastTarget = false;
            HorizontalLayoutGroup chipRow = Row(chip, 12, 12, 0f);
            chipRow.childAlignment = TextAnchor.MiddleCenter;
            Size(chip, -1f, 32f);
            return Words(Ensure(chip, "Text", 0), bold, 14, textColor, sample);
        }

        private static RectTransform RowShell(RectTransform rows, string label, int index)
        {
            RectTransform row = Ensure(rows, label.Replace(" ", string.Empty) + "Row", index);
            Row(row, 0, 0, 10f);
            Size(row, -1f, 58f);
            Text name = Words(Ensure(row, "Label", 0), medium, 18, White, label);
            name.text = label;
            Flexible((RectTransform)name.transform);
            return row;
        }

        private static void Separator(RectTransform rows, string name, int index)
        {
            RectTransform rule = Ensure(rows, name, index);
            Stretched(rule, Figma("Line 1"), Color.white);
            Size(rule, -1f, 1f);
        }

        // "///" at the right of a card's header.
        private static void SlashMark(RectTransform header, Color color, int size)
        {
            if (header == null) return;
            RectTransform slashes = Ensure(header, "Slashes", -1);
            slashes.anchorMin = new Vector2(1f, 0f);
            slashes.anchorMax = new Vector2(1f, 1f);
            slashes.pivot = new Vector2(1f, 0.5f);
            slashes.anchoredPosition = new Vector2(-14f, 0f);
            slashes.sizeDelta = new Vector2(60f, 0f);
            Text text = Words(slashes, bold, size, color, "///");
            text.text = "///";
            text.fontStyle = FontStyle.Italic;
            text.alignment = TextAnchor.MiddleRight;
        }

        private static Button ArrowButton(RectTransform parent, string name, int index, Sprite icon)
        {
            RectTransform arrow = Ensure(parent, name, index);
            Picture(arrow, icon, White);
            Image image = arrow.GetComponent<Image>();
            image.raycastTarget = true;
            Size(arrow, 18f, 18f);
            Button button = arrow.GetComponent<Button>();
            if (button == null) button = arrow.gameObject.AddComponent<Button>();
            Hover(button, image);
            return button;
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

        private static void SetColorValue(SerializedObject so, string property, Color color)
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

        private static void SetBool(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p != null) p.boolValue = value;
        }

        private static void Fit(RectTransform rect, bool width, bool height)
        {
            if (rect == null) return;
            ContentSizeFitter fitter = rect.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = width ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = height ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        }

        // A column that sizes its children to their preferred heights and stretches them across.
        private static void ColumnLayout(VerticalLayoutGroup column, int left, int right, int top, int bottom, float spacing)
        {
            column.padding = new RectOffset(left, right, top, bottom);
            column.spacing = spacing;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
        }

        // A plain picture stretched over its rect (a rule line, a header plate).
        private static void Stretched(RectTransform target, Sprite sprite, Color color)
        {
            if (target == null || sprite == null) return;
            Image image = target.GetComponent<Image>();
            if (image == null) image = target.gameObject.AddComponent<Image>();
            image.enabled = true;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = color;
            image.raycastTarget = false;
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
