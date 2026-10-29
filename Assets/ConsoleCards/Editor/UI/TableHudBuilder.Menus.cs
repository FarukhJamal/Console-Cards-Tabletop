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
    /// MENU-1 to design boards 5 and 6 (owner, 2026-10-30: "everything the same as the reference"), in Final UI/Notices,
    /// measured from the boards (1280 x 720, x1.5):
    /// <list type="bullet">
    /// <item>The Esc menu (board 5): PAUSED and the game on the right, "Online games keep going…", RESUME (Esc) in teal,
    /// ▸ RULES (R), ▸ HOW TO PLAY (H), ▸ SAVE TABLE AS A GAME, ▸ SETTINGS; under the line MAIN MENU and EXIT GAME, and
    /// RESET / CLEAR TABLE (not on the board; kept so the table can still be reset) as one small row.</item>
    /// <item>Settings (board 6) as a page over the table: the top bar, Settings, the tabs GAME, LOOK, CONTROLS, SOUND,
    /// PROFILES with their note, the rows with switches, Tips while placing (FIRST GAMES, ALWAYS, OFF) and BACK.</item>
    /// </list>
    /// Made with the HUD (Create Missing Table HUD (HUD-B)). Menus made before the boards are replaced once (they are the
    /// builder's own); after that they are kept (edit them freely).
    /// </summary>
    public static partial class TableHudBuilder
    {
        public const string PauseMenuPath = Folder + "TablePauseMenu.prefab";
        public const string SettingsPath = Folder + "TableSettings.prefab";
        private const string PauseLabel = "PauseBoard5";
        private const string SettingsLabel = "SettingsBoard6";

        private static readonly Color RowFill = Hex("1b1f20");
        private static readonly Color SwitchOff = Hex("3c4143");
        private static readonly Color Item5 = Hex("292e2f");
        private static readonly Color ItemEdge5 = Hex("424547");
        private static readonly Color Dark5 = Hex("1c1f1f");
        private static readonly Color Faint5 = Hex("6b7477");
        private static readonly Color Page6 = Hex("101314");
        private static readonly Color Dots6 = Hex("1e2628");
        private static readonly Color SwitchOff6 = Hex("4b5357");
        private static readonly Color KnobOn6 = Hex("102021");

        private const string ControlsText =
            "<b>Esc</b>   this menu, or one step back (drop the selection, back to Select and move)\n"
            + "<b>Drag</b>   move a piece          <b>Wheel</b>   turn a held piece, or zoom\n"
            + "<b>Space + drag</b>   move the camera          <b>WASD / arrows</b>   move the camera\n"
            + "<b>V</b>   select and move          <b>P</b>   ping          <b>M</b>   measure\n"
            + "<b>Q / E</b>   turn          <b>F</b>   flip          <b>R</b>   roll a die (elsewhere: the rulebook)          <b>T</b>   deal\n"
            + "<b>1–9</b>   over a deck: draw that many (elsewhere 1–4 change the view)\n"
            + "<b>Ctrl + click</b>   select more          <b>G</b>   group cards into a deck          <b>Del</b>   remove\n"
            + "<b>B</b>   the Toolbox          <b>H</b>   show or hide your hand          <b>L</b>   activity log\n"
            + "<b>Enter</b>   end turn          <b>Ctrl Z / Ctrl Y</b>   undo / redo          <b>Right-click</b>   a piece's menu";

        private static void BuildMenus()
        {
            B.Prepare();
            HudSprite("HudPlay", Vector4.zero);
            HudSprite("HudPill", new Vector4(31f, 31f, 31f, 31f));
            ReplaceUnlabelled(PauseMenuPath, PauseLabel);
            ReplaceUnlabelled(SettingsPath, SettingsLabel);
            bool newPause = !File.Exists(PauseMenuPath);
            bool newSettings = !File.Exists(SettingsPath);
            Save<TablePauseMenuView>(PauseMenuPath, "TablePauseMenu", BuildPauseMenu);
            Save<TableSettingsView>(SettingsPath, "TableSettings", BuildSettings);
            if (newPause) LabelAsset(PauseMenuPath, PauseLabel);
            if (newSettings) LabelAsset(SettingsPath, SettingsLabel);
            AssignCatalogEntry(UiPrefabIds.TablePauseMenu, AssetDatabase.LoadAssetAtPath<TablePauseMenuView>(PauseMenuPath),
                UiLayer.Modal, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(UiPrefabIds.TableSettings, AssetDatabase.LoadAssetAtPath<TableSettingsView>(SettingsPath),
                UiLayer.Modal, UiRetention.CachedSingleInstance);
        }

        private static void ReplaceUnlabelled(string path, string label)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && System.Array.IndexOf(AssetDatabase.GetLabels(existing), label) < 0)
            {
                AssetDatabase.DeleteAsset(path); // made before the board (by this builder): built again
            }
        }

        private static void LabelAsset(string path, string label)
        {
            GameObject made = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (made != null) AssetDatabase.SetLabels(made, new[] { label });
        }

        // ---------- the Esc menu (board 5) ----------

        private static void BuildPauseMenu(RectTransform root, TablePauseMenuView view)
        {
            Button backdrop = DimBackdrop(root);
            backdrop.GetComponent<Image>().color = new Color(0.024f, 0.035f, 0.039f, 0.72f);
            RectTransform card = Rect("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(630f, 0f));
            Plate(card, Orange, Hex("222527"));
            VerticalLayoutGroup column = Column(card.gameObject, 33, 33, 33, 30, 15);
            column.childForceExpandHeight = false;
            Fit(card.gameObject, false, true);

            RectTransform header = Child("Header", card);
            Row(header.gameObject, 0, 0, 0, 0, 15).childAlignment = TextAnchor.MiddleLeft;
            Size(header.gameObject, -1f, 40f);
            Text title = Label(Child("Title", header), bold, 30, Words, TextAnchor.MiddleLeft, "PAUSED");
            Size(title.gameObject, -1f, 40f).flexibleWidth = 1f;
            Text subtitle = Label(Child("Subtitle", header), medium, 19, Muted, TextAnchor.MiddleRight, "Trap Floor · Round 3");
            Size(subtitle.gameObject, 360f, 40f, 200f);
            Text lead = Label(Child("Lead", card), medium, 19, Muted, TextAnchor.MiddleLeft,
                "Online games keep going while this is open.");
            Size(lead.gameObject, -1f, 30f);

            Button resume = Item5Row(card, "Resume", "RESUME", "ESC", true, false);
            Button rules = Item5Row(card, "Rules", "RULES", "R", false, false);
            Button howTo = Item5Row(card, "HowToPlay", "HOW TO PLAY", "H", false, false);
            Button save = Item5Row(card, "SaveGame", "SAVE TABLE AS A GAME", null, false, false);
            Button settings = Item5Row(card, "Settings", "SETTINGS", null, false, false);
            MenuRule(card, "Rule");

            Button mainMenu = Item5Row(card, "MainMenu", "MAIN MENU", null, false, true);
            Button exit = Item5Row(card, "Exit", "EXIT GAME", null, false, true);

            // Not on board 5: Reset and Clear, kept as one small row so the table can still be reset.
            RectTransform table = Child("TableSection", card);
            Row(table.gameObject, 0, 0, 0, 0, 12);
            Size(table.gameObject, -1f, 54f);
            Button newTable = Small5(table, "NewTable", "NEW TABLE…");
            newTable.gameObject.SetActive(false);
            Button reset = Small5(table, "Reset", "RESET TABLE");
            Button clear = Small5(table, "Clear", "CLEAR TABLE");

            Text note = Label(Child("Note", card), medium, 18, Faint5, TextAnchor.UpperLeft,
                "MAIN MENU keeps this table: TABLE on the main menu brings it back. Reset, Clear, Main menu and Exit ask for "
                + "a second press.");
            Size(note.gameObject, -1f, 54f);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("subtitleLabel").objectReferenceValue = subtitle;
            so.FindProperty("backdropButton").objectReferenceValue = backdrop;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("rulesButton").objectReferenceValue = rules;
            so.FindProperty("howToPlayButton").objectReferenceValue = howTo;
            so.FindProperty("saveGameButton").objectReferenceValue = save;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.FindProperty("tableSection").objectReferenceValue = table.gameObject;
            so.FindProperty("newTableButton").objectReferenceValue = newTable;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.FindProperty("clearButton").objectReferenceValue = clear;
            so.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
            so.FindProperty("exitButton").objectReferenceValue = exit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A board-5 row: 75 high, ▸ (or RESUME's dot), the words (the row's first Text), a key chip on the right.
        private static Button Item5Row(Transform parent, string name, string words, string key, bool resume, bool dark)
        {
            RectTransform rect = Child(name, parent);
            Image fill = Shape(rect, resume ? Teal : dark ? Dark5 : Item5, rounded);
            fill.raycastTarget = true;
            Outline edge = rect.gameObject.AddComponent<Outline>();
            edge.effectColor = resume ? Cyan : ItemEdge5;
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            edge.useGraphicAlpha = false;
            Outline edge2 = rect.gameObject.AddComponent<Outline>();
            edge2.effectColor = resume ? Cyan : ItemEdge5;
            edge2.effectDistance = new Vector2(-1.5f, 1.5f);
            edge2.useGraphicAlpha = false;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colours = button.colors;
            colours.highlightedColor = resume ? new Color(1.1f, 1.1f, 1.1f, 1f) : new Color(0.75f, 1.35f, 1.35f, 1f);
            button.colors = colours;
            Size(rect.gameObject, -1f, 75f);
            Row(rect.gameObject, 24, 24, 0, 0, 18).childAlignment = TextAnchor.MiddleLeft;

            Text label = null;
            if (!dark)
            {
                RectTransform mark = Child("Mark", rect);
                if (resume)
                {
                    Shape(mark, Words, circle);
                    Size(mark.gameObject, 12f, 12f, 12f);
                }
                else
                {
                    // ▸ is not in Oxanium: a small orange triangle (the board's die shape sprite would be too big).
                    Image arrow = Shape(mark, Orange, AssetDatabase.LoadAssetAtPath<Sprite>(HudArt + "HudPlay.png"));
                    arrow.preserveAspect = true;
                    if (arrow.sprite == null) arrow.color = new Color(0f, 0f, 0f, 0f);
                    Size(mark.gameObject, 12f, 15f, 12f);
                    if (arrow.sprite == null)
                    {
                        Label(Rect("Glyph", mark, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 6f)),
                            bold, 20, Orange, TextAnchor.MiddleCenter, "›");
                    }
                }

                mark.SetAsFirstSibling();
            }

            label = FitLabel(Child("Label", rect), bold, 22, resume ? Hex("f4fefe") : Words, words);
            Size(label.gameObject, -1f, 75f).flexibleWidth = 1f;
            label.transform.SetSiblingIndex(rect.childCount - 1);
            if (!string.IsNullOrEmpty(key))
            {
                RectTransform chip = Child("Key", rect);
                Image chipFill = Shape(chip, new Color(0f, 0f, 0f, 0f), rounded);
                Outline chipEdge = chip.gameObject.AddComponent<Outline>();
                chipEdge.effectColor = resume ? Hex("7fe9ea") : ItemEdge5;
                chipEdge.effectDistance = new Vector2(1.5f, -1.5f);
                chipEdge.useGraphicAlpha = false;
                _ = chipFill;
                Row(chip.gameObject, 9, 9, 0, 0, 0f, TextAnchor.MiddleCenter);
                Size(chip.gameObject, -1f, 30f);
                FitLabel(Child("Label", chip), bold, 16, resume ? Words : Muted, key).alignment = TextAnchor.MiddleCenter;
            }

            return button;
        }

        private static Button Small5(Transform parent, string name, string words)
        {
            RectTransform rect = Child(name, parent);
            Image fill = Shape(rect, Dark5, rounded);
            fill.raycastTarget = true;
            Outline edge = rect.gameObject.AddComponent<Outline>();
            edge.effectColor = ItemEdge5;
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            edge.useGraphicAlpha = false;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            Size(rect.gameObject, -1f, 54f).flexibleWidth = 1f;
            Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), bold, 17,
                Soft, TextAnchor.MiddleCenter, words);
            return button;
        }

        private static void MenuRule(Transform parent, string name)
        {
            RectTransform rule = Child(name, parent);
            Shape(rule, Edge, null);
            Size(rule.gameObject, -1f, 2f);
        }

        private static Button DimBackdrop(RectTransform root)
        {
            RectTransform rect = Rect("Backdrop", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            Image dim = rect.gameObject.AddComponent<Image>();
            dim.color = new Color(0.024f, 0.047f, 0.051f, 0.55f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = dim;
            button.transition = Selectable.Transition.None;
            return button;
        }

        // ---------- Settings (board 6) ----------

        private static void BuildSettings(RectTransform root, TableSettingsView view)
        {
            // The page covers the table: its own dark, dotted background (a press on it does nothing).
            Button backdrop = DimBackdrop(root);
            backdrop.interactable = false; // a press beside the rows does not close the page
            Image page = backdrop.GetComponent<Image>();
            page.color = Page6;
            Sprite dots = HudSprite("HudDots", Vector4.zero);
            if (dots != null)
            {
                RectTransform dotRect = Rect("Dots", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                    Vector2.zero);
                Image dotImage = dotRect.gameObject.AddComponent<Image>();
                dotImage.sprite = dots;
                dotImage.type = Image.Type.Tiled;
                dotImage.color = Dots6;
                dotImage.raycastTarget = false;
            }

            RectTransform canvas = Rect("Board", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1920f, 1080f));

            // The top bar.
            RectTransform bar = B.Box(canvas, "TopBar", 24, 20, 1232, 56, Dark5, Edge, 1f);
            B.Stripe(bar, 18, 12, 10, 32);
            B.Stripe(bar, 1204, 12, 10, 32);
            B.TextAt(bar, "Brand", 56, 0, 220, 56, B.bold, 17, Words, TextAnchor.MiddleLeft, "CONSOLE CARDS");
            B.TextAt(bar, "Play", 270, 0, 60, 56, B.bold, 14, Words, TextAnchor.MiddleLeft, "PLAY");
            B.TextAt(bar, "Create", 337, 0, 80, 56, B.bold, 14, Words, TextAnchor.MiddleLeft, "CREATE");
            B.TextAt(bar, "Sandbox", 424, 0, 90, 56, B.bold, 14, Words, TextAnchor.MiddleLeft, "SANDBOX");
            B.TextAt(bar, "Settings", 1080, 0, 110, 56, B.bold, 13, Cyan, TextAnchor.MiddleRight, "SETTINGS");
            B.Line(bar, "SettingsLine", 1098, 40, 76, 2, Cyan);

            B.TextAt(canvas, "Title", 48, 100, 400, 40, B.bold, 30, Words, TextAnchor.MiddleLeft, "Settings");
            B.TextAt(canvas, "Lead", 48, 140, 600, 20, B.medium, 14, Muted, TextAnchor.MiddleLeft,
                "Kept on this computer. Changes apply at once.");

            // The tabs.
            string[] tabNames = { "GAME", "LOOK", "CONTROLS", "SOUND", "PROFILES" };
            Button[] tabButtons = new Button[tabNames.Length];
            Image[] tabFills = new Image[tabNames.Length];
            Text[] tabLabels = new Text[tabNames.Length];
            GameObject[] tabMarks = new GameObject[tabNames.Length];
            for (int i = 0; i < tabNames.Length; i++)
            {
                RectTransform tab = B.Rect(canvas, tabNames[i] + "Tab", 48, 180 + (i * 54), 240, 48);
                Image fill = Shape(tab, new Color(0f, 0f, 0f, 0f), rounded);
                fill.raycastTarget = true;
                Outline edge = tab.gameObject.AddComponent<Outline>(); // shows with the open tab's fill only
                edge.effectColor = TileEdgeHover;
                edge.effectDistance = new Vector2(1.5f, -1.5f);
                Outline edge2 = tab.gameObject.AddComponent<Outline>();
                edge2.effectColor = TileEdgeHover;
                edge2.effectDistance = new Vector2(-1.5f, 1.5f);
                Button button = tab.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                button.transition = Selectable.Transition.None;
                RectTransform mark = B.Rect(tab, "Mark", 16, 14, 4, 20);
                Shape(mark, Cyan, rounded);
                tabMarks[i] = mark.gameObject;
                tabLabels[i] = B.TextAt(tab, "Label", 30, 0, 200, 48, B.bold, 14, CatWords, TextAnchor.MiddleLeft, tabNames[i]);
                tabButtons[i] = button;
                tabFills[i] = fill;
            }

            Text tabNote = B.TextAt(canvas, "TabNote", 48, 464, 240, 60, B.medium, 12, Faint5, TextAnchor.UpperLeft,
                "LOOK: light (Day, Evening, Arcade), quality, colour-blind seats.\nCONTROLS: the keys.\n"
                + "PROFILES: who plays on this computer.");
            tabNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            tabNote.lineSpacing = 1.15f;

            // The pages (rows in a column, so a hidden row leaves no gap).
            RectTransform pagesRoot = B.Rect(canvas, "Pages", 312, 180, 920, 430);
            GameObject[] pages = new GameObject[tabNames.Length];
            RectTransform game = Page6Column(pagesRoot, "GamePage");
            RectTransform look = Page6Column(pagesRoot, "LookPage");
            RectTransform controls = Page6Column(pagesRoot, "ControlsPage");
            RectTransform sound = Page6Column(pagesRoot, "SoundPage");
            RectTransform profiles = Page6Column(pagesRoot, "ProfilesPage");
            pages[0] = game.gameObject;
            pages[1] = look.gameObject;
            pages[2] = controls.gameObject;
            pages[3] = sound.gameObject;
            pages[4] = profiles.gameObject;

            SerializedObject so = new SerializedObject(view);

            // GAME
            Switch6(game, "HintsRow", "Show hints", "The HINT line under the round bar says what to do now.", so, "hintsSwitch");
            Switch6(game, "RulesCardRow", "Show the rules card", "The game's rules on the left of the table.", so,
                "rulesCardSwitch");
            Switch6(game, "HotSeatRow", "One screen for all players", "On one computer, the camera follows whoever's turn it is.",
                so, "hotSeatSwitch");
            Switch6(game, "AskResetRow", "Ask before Reset", "A second press is needed to clear the table.", so, "askResetSwitch");
            RectTransform tipsRow = Row6(game, "TipsModeRow", "Tips while placing", "Short tips next to the cursor for new players.");
            RectTransform seg = B.Box(tipsRow, "Options", 648, 13, 256, 34, Dark5, ItemEdge5, 1f);
            string[] modes = { "FIRST GAMES", "ALWAYS", "OFF" };
            float[] modeX = { 3, 120, 200 };
            float[] modeW = { 115, 78, 52 };
            Button[] modeButtons = new Button[3];
            Image[] modeFills = new Image[3];
            Text[] modeLabels = new Text[3];
            for (int i = 0; i < modes.Length; i++)
            {
                RectTransform option = B.Rect(seg, "Option" + i, modeX[i], 3, modeW[i], 28);
                modeFills[i] = Shape(option, i == 0 ? TileSelected : new Color(0f, 0f, 0f, 0f), rounded);
                modeFills[i].raycastTarget = true;
                modeButtons[i] = option.gameObject.AddComponent<Button>();
                modeButtons[i].targetGraphic = modeFills[i];
                modeButtons[i].transition = Selectable.Transition.None;
                modeLabels[i] = B.TextAt(option, "Label", 0, 0, modeW[i], 28, B.bold, 12, i == 0 ? Words : Muted,
                    TextAnchor.MiddleCenter, modes[i]);
            }

            RectTransform seatRow = Row6(game, "SeatRow", "Seat", "Which seat this screen plays.");
            Button seat = B.SmallButton(seatRow, "SeatButton", 704, 13, 200, 34, "Seat: Player 1", ItemEdge5, Cyan);
            Text seatLabel = seat.GetComponentInChildren<Text>();
            Switch6(game, "HandShownRow", "Show hand", "Your hand is up on the screen (H).", so, "handShownSwitch");

            // LOOK
            RectTransform lightRow = Cycle6(look, "LightRow", "Light", "Day, Evening or Arcade.", out Button lightPrevious,
                out Button lightNext, out Text lightValue);
            RectTransform qualityRow = Cycle6(look, "QualityRow", "Quality", "Lower it if the table feels slow.",
                out Button qualityPrevious, out Button qualityNext, out Text qualityValue);
            Switch6(look, "ColourBlindRow", "Colour-blind seat colours",
                "Seat colours people with colour blindness tell apart (from the next table).", so, "colourBlindSwitch");
            Switch6(look, "HandsOnTableRow", "Hands on table", "Your hand lies at your table edge.", so, "handsOnTableSwitch");
            Switch6(look, "HandFanRow", "Hand as a fan", "Off: your hand is a straight row.", so, "handFanSwitch");
            Switch6(look, "FeedbackRow", "Full feedback", "Banners, sounds and camera nudges.", so, "feedbackSwitch");

            // CONTROLS
            RectTransform keysBox = Child("Keys", controls);
            Shape(keysBox, Item5, rounded);
            Size(keysBox.gameObject, -1f, 400f);
            Text keys = Label(Rect("Text", keysBox, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-48f, -36f)), medium, 19, Soft, TextAnchor.UpperLeft, ControlsText);
            keys.supportRichText = true;
            keys.lineSpacing = 1.3f;

            // SOUND
            Switch6(sound, "RoomSoundRow", "Room sound", "A quiet hum of the console around the table.", so, "roomSoundSwitch");

            // PROFILES
            RectTransform profileRow = Row6(profiles, "ProfileRow", "Who is playing", string.Empty);
            Text profileText = B.TextAt(profileRow, "Text", 16, 31, 880, 20, B.medium, 13, Muted, TextAnchor.UpperLeft,
                "Playing as Player 1 on this computer.");
            profileText.supportRichText = true;

            // Kept for the view, not on the board: the old Tips switch, the empty-table note and a close button.
            RectTransform hidden = Rect("NotOnBoard", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(920f, 400f));
            hidden.gameObject.SetActive(false);
            Switch6(hidden, "TipsRow", "Tips", "Teach the table and the game", so, "tipsSwitch");
            Text empty = Label(Child("Empty", hidden), medium, 16, Muted, TextAnchor.MiddleLeft,
                "This table has no seats, so there is nothing to set here.");
            Button close = B.SmallButton(hidden, "Close", 0, 0, 30, 30, "×", ItemEdge5, Words);

            for (int i = 1; i < pages.Length; i++) pages[i].SetActive(false);

            // BACK and its note.
            RectTransform backRect = B.Box(canvas, "Back", 48, 640, 102, 48, Dark5, Edge, 1f);
            Button back = backRect.gameObject.AddComponent<Button>();
            back.targetGraphic = backRect.GetComponent<Image>();
            RectTransform dot = B.Rect(backRect, "Dot", 20, 20, 8, 8);
            Shape(dot, Faint5, circle);
            B.TextAt(backRect, "Label", 36, 0, 60, 48, B.bold, 14, Words, TextAnchor.MiddleLeft, "BACK");
            B.TextAt(canvas, "BackNote", 700, 640, 532, 48, B.medium, 13, Faint5, TextAnchor.MiddleRight,
                "Opened from a game, BACK returns to the Esc menu.");

            so.FindProperty("backdropButton").objectReferenceValue = backdrop;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.FindProperty("closeButton").objectReferenceValue = close;
            SetArray(so.FindProperty("tabButtons"), tabButtons);
            SetArray(so.FindProperty("tabFills"), tabFills);
            SetArray(so.FindProperty("tabLabels"), tabLabels);
            SetArray(so.FindProperty("pages"), pages);
            SetArray(so.FindProperty("tabMarks"), tabMarks);
            so.FindProperty("seatRow").objectReferenceValue = seatRow.gameObject;
            so.FindProperty("seatButton").objectReferenceValue = seat;
            so.FindProperty("seatLabel").objectReferenceValue = seatLabel;
            so.FindProperty("tableEmptyNote").objectReferenceValue = empty.gameObject;
            so.FindProperty("tabOn").colorValue = TileSelected;
            so.FindProperty("tabOff").colorValue = new Color(0f, 0f, 0f, 0f);
            so.FindProperty("tabOnWords").colorValue = Words;
            so.FindProperty("tabOffWords").colorValue = CatWords;
            so.FindProperty("tipsModeRow").objectReferenceValue = tipsRow.gameObject;
            SetArray(so.FindProperty("tipsModeButtons"), modeButtons);
            SetArray(so.FindProperty("tipsModeFills"), modeFills);
            SetArray(so.FindProperty("tipsModeLabels"), modeLabels);
            so.FindProperty("optionOn").colorValue = TileSelected;
            so.FindProperty("optionOff").colorValue = new Color(0f, 0f, 0f, 0f);
            so.FindProperty("optionOnWords").colorValue = Words;
            so.FindProperty("optionOffWords").colorValue = Muted;
            so.FindProperty("lightRow").objectReferenceValue = lightRow.gameObject;
            so.FindProperty("lightPrevious").objectReferenceValue = lightPrevious;
            so.FindProperty("lightNext").objectReferenceValue = lightNext;
            so.FindProperty("lightValue").objectReferenceValue = lightValue;
            so.FindProperty("qualityRow").objectReferenceValue = qualityRow.gameObject;
            so.FindProperty("qualityPrevious").objectReferenceValue = qualityPrevious;
            so.FindProperty("qualityNext").objectReferenceValue = qualityNext;
            so.FindProperty("qualityValue").objectReferenceValue = qualityValue;
            so.FindProperty("profileLabel").objectReferenceValue = profileText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Page6Column(RectTransform pagesRoot, string name)
        {
            RectTransform page = Rect(name, pagesRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero,
                Vector2.zero);
            VerticalLayoutGroup column = Column(page.gameObject, 0, 0, 0, 0, 12);
            column.childForceExpandHeight = false;
            return page;
        }

        // A board-6 row (60 high): the title and its line on the left; parts on the right are placed in board units.
        private static RectTransform Row6(Transform page, string name, string title, string hint)
        {
            RectTransform row = Child(name, page);
            Image fill = Shape(row, Item5, rounded);
            fill.raycastTarget = false;
            Outline edge = row.gameObject.AddComponent<Outline>();
            edge.effectColor = ItemEdge5;
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            edge.useGraphicAlpha = false;
            Size(row.gameObject, 1380f, 90f);
            row.sizeDelta = new Vector2(1380f, 90f);
            B.TextAt(row, "Title", 16, 9, 600, 22, B.bold, 15, Words, TextAnchor.MiddleLeft, title);
            if (!string.IsNullOrEmpty(hint)) B.TextAt(row, "Hint", 16, 31, 700, 20, B.medium, 13, Muted, TextAnchor.UpperLeft, hint);
            return row;
        }

        // A row with a 48 x 26 switch (cyan with a dark knob on the right when on; grey with a light knob when off).
        private static void Switch6(Transform page, string name, string title, string hint, SerializedObject so, string property)
        {
            RectTransform row = Row6(page, name, title, hint);
            Text hintLabel = row.Find("Hint") != null ? row.Find("Hint").GetComponent<Text>() : null;
            if (hintLabel == null) hintLabel = B.TextAt(row, "Hint", 16, 31, 700, 20, B.medium, 13, Muted, TextAnchor.UpperLeft, hint);
            RectTransform pill = B.Rect(row, "Switch", 856, 17, 48, 26);
            Sprite pillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HudArt + "HudPill.png");
            Image fill = Shape(pill, Cyan, pillSprite != null ? pillSprite : rounded);
            if (pillSprite != null) fill.type = Image.Type.Sliced;
            fill.raycastTarget = true;
            Button button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            RectTransform knob = Rect("Knob", pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(16.5f, 0f), new Vector2(30f, 30f));
            Image knobImage = Shape(knob, KnobOn6, circle);
            Text word = Label(Rect("Word", pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(10f, 10f)), bold, 10, new Color(0f, 0f, 0f, 0f), TextAnchor.MiddleCenter, string.Empty);
            word.gameObject.SetActive(false); // the board's switch has no words

            SerializedProperty switchProperty = so.FindProperty(property);
            AssignSwitch(switchProperty, row, button, fill, knob, word, hintLabel, hint, hint);
            switchProperty.FindPropertyRelative("onFill").colorValue = Cyan;
            switchProperty.FindPropertyRelative("offFill").colorValue = SwitchOff6;
            switchProperty.FindPropertyRelative("onWordColor").colorValue = new Color(0f, 0f, 0f, 0f);
            switchProperty.FindPropertyRelative("offWordColor").colorValue = new Color(0f, 0f, 0f, 0f);
            switchProperty.FindPropertyRelative("knobOffset").floatValue = 16.5f;
            switchProperty.FindPropertyRelative("wordOffset").floatValue = 0f;
            switchProperty.FindPropertyRelative("knobImage").objectReferenceValue = knobImage;
            switchProperty.FindPropertyRelative("knobOnColor").colorValue = KnobOn6;
            switchProperty.FindPropertyRelative("knobOffColor").colorValue = Words;
        }

        // A row with ‹ value › on the right (Light, Quality).
        private static RectTransform Cycle6(Transform page, string name, string title, string hint, out Button previous,
            out Button next, out Text value)
        {
            RectTransform row = Row6(page, name, title, hint);
            RectTransform box = B.Box(row, "Picker", 704, 13, 200, 34, Dark5, ItemEdge5, 1f);
            previous = B.SmallButton(box, "Previous", 2, 2, 30, 30, "‹", new Color(0f, 0f, 0f, 0f), Cyan);
            next = B.SmallButton(box, "Next", 168, 2, 30, 30, "›", new Color(0f, 0f, 0f, 0f), Cyan);
            value = B.TextAt(box, "Value", 34, 0, 132, 34, B.bold, 13, Words, TextAnchor.MiddleCenter, "Arcade");
            return row;
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
