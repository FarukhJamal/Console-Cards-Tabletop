using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Boards 1–3 of the menu flow (owner, 2026-10-30: "everything the same as the reference"): the top bar, Home, the
    /// Library and the Game sheet (FE_Details), measured from the boards (1280 x 720, x1.5). The names are the ones
    /// FrontEndView looks for (FrontEndView.Board.cs); the old Game setup steps stay in the front end, unused by them.
    /// </summary>
    public static partial class BoardScreensBuilder
    {
        private const string HudArtPath = "Assets/ConsoleCards/Content/UI/Board/Hud/";
        private static readonly Color Page = Hex("101314");
        private static readonly Color Dots = Hex("1e2628");
        private static readonly Color Item = Hex("2a2f31");
        private static readonly Color ItemEdge = Hex("424547");
        private static readonly Color TealButton = Hex("138a8c");
        private static readonly Color DarkTeal = Hex("0a3537");

        // ---------- the top bar ----------

        private static void BuildTopBar(RectTransform top)
        {
            RectTransform bar = Box(top, "Bar", 24, 20, 1232, 56, Well, Edge, 1f);
            bar.GetComponent<Image>().raycastTarget = false;
            Stripe(top, 42, 32, 10, 32);
            Stripe(top, 1228, 32, 10, 32);
            TextAt(top, "Brand", 80, 20, 220, 56, bold, 17, Words, TextAnchor.MiddleLeft, "CONSOLE CARDS");

            RectTransform tabs = Rect(top, "Tabs", 290, 20, 280, 56);
            TabB(tabs, "Play", 0, 46, "PLAY");
            TabB(tabs, "Create", 67, 64, "CREATE");
            TabB(tabs, "Sandbox", 154, 80, "SANDBOX");
            RectTransform highlight = Rect(tabs, "Highlight", 0, 42, 46, 2);
            Image line = highlight.gameObject.AddComponent<Image>();
            line.color = Cyan;
            line.raycastTarget = false;

            RectTransform right = Rect(top, "Right", 836, 20, 400, 56);
            RectTransform profile = Box(right, "Profile", 6, 8, 108, 40, Hex("101314"), Edge, 1f);
            Button profileButton = profile.gameObject.AddComponent<Button>();
            profileButton.targetGraphic = profile.GetComponent<Image>();
            Image dot = Rect(profile, "Dot", 8, 7, 26, 26).gameObject.AddComponent<Image>();
            dot.sprite = circle;
            dot.color = Cyan;
            dot.raycastTarget = false;
            TextAt(profile, "Small", 42, 5, 64, 14, medium, 11, Muted, TextAnchor.MiddleLeft, "Playing as");
            TextAt(profile, "Name", 42, 19, 64, 18, bold, 13, Words, TextAnchor.MiddleLeft, "PLAYER 1");
            WordButton(right, "Guide", 144, 52, "GUIDE");
            WordButton(right, "Settings", 222, 76, "SETTINGS");
            WordButton(right, "Exit", 327, 36, "EXIT");

            Text crumb = TextAt(top, "Crumb", 900, 20, 300, 56, bold, 13, Muted, TextAnchor.MiddleRight, "PLAY  ›  TRAP FLOOR");
            crumb.gameObject.SetActive(false);
        }

        private static void TabB(RectTransform tabs, string name, float x, float width, string words)
        {
            RectTransform tab = Rect(tabs, name, x, 0, width, 56);
            Image catcher = tab.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            Button button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = catcher;
            button.transition = Selectable.Transition.None;
            TextAt(tab, "Label", 0, 0, width, 56, bold, 14, Words, TextAnchor.MiddleCenter, words);
        }

        private static void WordButton(RectTransform parent, string name, float x, float width, string words)
        {
            RectTransform rect = Rect(parent, name, x, 0, width, 56);
            Image catcher = rect.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = catcher;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.024f, 0.937f, 0.941f, 0.12f);
            button.colors = colours;
            TextAt(rect, "Label", 0, 0, width, 56, bold, 14, Words, TextAnchor.MiddleCenter, words);
        }

        // ---------- Home (board 1) ----------

        private static void BuildHome(RectTransform page)
        {
            TextAt(page, "Title", 72, 120, 900, 76, bold, 52, Words, TextAnchor.MiddleLeft, "CONSOLE CARDS");
            TextAt(page, "Subtitle", 72, 197, 900, 24, medium, 18, Soft, TextAnchor.MiddleLeft,
                "A tabletop for card and board games. Pick up the pieces and play.");

            RectTransform menu = Rect(page, "Menu", 72, 252, 520, 340);
            MenuCard(menu, "Play", 0, true, "Play", "PLAY", "Pick a game and start. Here or online.");
            MenuCard(menu, "Create", 118, false, "Plus", "CREATE", "Make cards, decks and whole games of your own.");
            MenuCard(menu, "Sandbox", 236, false, "Cube", "SANDBOX", "An empty table and every piece. No rules.");

            RectTransform card = Box(page, "Continue", 738, 252, 470, 336, Panel, Edge, 1f);
            Text heading = TextAt(card, "Heading", 24, 22, 200, 20, bold, 13, Muted, TextAnchor.MiddleLeft, "CONTINUE");
            _ = heading;
            Line(card, "Rule", 120, 32, 292, 1, Edge);
            TextAt(card, "Marks", 418, 22, 28, 20, bold, 15, Orange, TextAnchor.MiddleRight, "///");
            Box(card, "Cover", 24, 58, 132, 96, Cyan, null, 0f).GetComponent<Image>().raycastTarget = false;
            RawImage picture = Rect(card, "Picture", 24, 58, 132, 96).gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            picture.color = new Color(0f, 0f, 0f, 0f);
            TextAt(card, "Name", 174, 58, 280, 30, bold, 22, Words, TextAnchor.MiddleLeft, "TRAP FLOOR");
            TextAt(card, "Line", 174, 91, 280, 20, medium, 14, Muted, TextAnchor.MiddleLeft, "4 players · Hard");
            TextAt(card, "SubLine", 174, 112, 280, 20, medium, 14, Muted, TextAnchor.MiddleLeft, "The table as you left it");
            DotButton(card, "Continue", 24, 174, 126, 42, TealButton, Cyan, Words, "CONTINUE");
            DotButton(card, "PlayAgain", 162, 174, 138, 42, Well, Edge, Faint, "PLAY AGAIN");
            Line(card, "Rule2", 24, 236, 422, 1, Edge);
            Text note = TextAt(card, "Note", 24, 250, 422, 64, medium, 13, Muted, TextAnchor.UpperLeft, string.Empty);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.lineSpacing = 1.15f;
        }

        // A Home card: the striped edge, the icon box, the title and line, ›.
        private static void MenuCard(RectTransform menu, string name, float y, bool first, string icon, string title,
            string text)
        {
            RectTransform card = Box(menu, name, 0, y, 520, 104, first ? TealFill : Panel, first ? Cyan : Edge, 1f);
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            Stripe(card, 22, 10, 10, 84);
            RectTransform box = Box(card, "Icon", 52, 28, 48, 48, first ? Cyan : Well, first ? (Color?)null : Edge, 1f);
            Glyph(box, icon, first ? Ink : Cyan, 22);
            TextAt(card, "Title", 120, 26, 340, 28, bold, 22, Words, TextAnchor.MiddleLeft, title);
            TextAt(card, "Text", 120, 56, 360, 20, medium, 14, Soft, TextAnchor.MiddleLeft, text);
            TextAt(card, "Chevron", 474, 37, 24, 30, bold, 22, first ? Cyan : Muted, TextAnchor.MiddleCenter, "›");
        }

        // A board button with a dot before its words.
        private static Button DotButton(RectTransform parent, string name, float x, float y, float width, float height,
            Color fill, Color edge, Color dot, string words)
        {
            RectTransform rect = Box(parent, name, x, y, width, height, fill, edge, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            Image mark = Rect(rect, "Dot", 16, (height - 8f) * 0.5f, 8, 8).gameObject.AddComponent<Image>();
            mark.sprite = circle;
            mark.color = dot;
            mark.raycastTarget = false;
            TextAt(rect, "Label", 32, 0, width - 36, height, bold, 13, Words, TextAnchor.MiddleLeft, words);
            return button;
        }

        // ---------- the Library (board 2) ----------

        private static void BuildLibrary(RectTransform page)
        {
            TextAt(page, "Kicker", 72, 104, 200, 18, bold, 13, Muted, TextAnchor.MiddleLeft, "PLAY");
            TextAt(page, "Title", 72, 122, 700, 40, bold, 32, Words, TextAnchor.MiddleLeft, "Choose a game");

            RectTransform filters = Rect(page, "Filters", 72, 176, 330, 32);
            Chip(filters, "All", 0, 55, "ALL");
            Chip(filters, "Ours", 63, 126, "OUR GAMES");
            Chip(filters, "Yours", 197, 128, "YOUR GAMES");

            RectTransform where = Box(page, "Where", 921, 118, 287, 44, Well, Edge, 1f);
            RectTransform local = Box(where, "Local", 4, 4, 186, 36, TealFill, Cyan, 1f);
            TextAt(local, "Label", 0, 0, 186, 36, bold, 13, Words, TextAnchor.MiddleCenter, "ON THIS COMPUTER");
            RectTransform online = Rect(where, "Online", 196, 4, 86, 36);
            Image onlineCatcher = online.gameObject.AddComponent<Image>();
            onlineCatcher.color = new Color(0f, 0f, 0f, 0f);
            Button onlineButton = online.gameObject.AddComponent<Button>();
            onlineButton.targetGraphic = onlineCatcher;
            TextAt(online, "Label", 0, 0, 86, 36, bold, 13, Muted, TextAnchor.MiddleCenter, "ONLINE");

            RectTransform list = Rect(page, "List", 72, 228, 1140, 400);
            RectTransform viewport = Rect(list, "Viewport", 0, 0, 1140, 400);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            RectTransform content = Rect(viewport, "Content", 0, 0, 1136, 328);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            LibraryTile(content);

            Text empty = TextAt(page, "Empty", 72, 300, 700, 30, medium, 16, Muted, TextAnchor.MiddleLeft,
                "No games here yet: CREATE › MAKE A GAME makes one of yours.");
            empty.gameObject.SetActive(false);

            BackB(page, 72, 646, 94, 46);
            TextAt(page, "Hint", 182, 646, 700, 46, medium, 13, Muted, TextAnchor.MiddleLeft,
                "One click on a game opens its sheet. Double-click starts it with the last settings.");
        }

        private static void Chip(RectTransform parent, string name, float x, float width, string words)
        {
            RectTransform rect = Rect(parent, name, x, 0, width, 32);
            Sprite pill = AssetDatabase.LoadAssetAtPath<Sprite>(HudArtPath + "HudPill.png");
            Image fill = rect.gameObject.AddComponent<Image>();
            fill.sprite = pill != null ? pill : rounded;
            fill.type = Image.Type.Sliced;
            fill.color = new Color(0f, 0f, 0f, 0f);
            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(HudArtPath + "HudPillFrame.png");
            if (frame != null)
            {
                Image edge = Rect(rect, "Edge", 0, 0, width, 32).gameObject.AddComponent<Image>();
                edge.sprite = frame;
                edge.type = Image.Type.Sliced;
                edge.color = Edge;
                edge.raycastTarget = false;
            }

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            TextAt(rect, "Label", 0, 0, width, 32, bold, 13, Soft, TextAnchor.MiddleCenter, words);
        }

        // The Library's tile (copied for each game): the picture or a colour block, the tag, the name and line, and
        // PLAYED LAST · CHOOSE › on the game chosen last. Its two Outlines are its edge (cyan when chosen).
        private static void LibraryTile(RectTransform content)
        {
            RectTransform tile = Rect(content, "Template", 0, 0, 268, 328);
            Image fill = tile.gameObject.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.color = Panel;
            Outline edge = tile.gameObject.AddComponent<Outline>();
            edge.effectColor = Edge;
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            edge.useGraphicAlpha = false;
            Outline edge2 = tile.gameObject.AddComponent<Outline>();
            edge2.effectColor = Edge;
            edge2.effectDistance = new Vector2(-1.5f, 1.5f);
            edge2.useGraphicAlpha = false;
            Button button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            button.colors = colours;

            Image cover = Rect(tile, "Cover", 1, 1, 266, 168).gameObject.AddComponent<Image>();
            cover.color = Cyan;
            cover.raycastTarget = false;
            RawImage picture = Rect(tile, "Picture", 1, 1, 266, 168).gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            picture.color = new Color(0f, 0f, 0f, 0f);
            Sprite hex = AssetDatabase.LoadAssetAtPath<Sprite>(HudArtPath + "HudHex.png");
            RectTransform mark = Rect(tile, "Mark", 116, 68, 36, 33);
            Image outer = mark.gameObject.AddComponent<Image>();
            outer.sprite = hex;
            outer.color = Cyan;
            outer.raycastTarget = false;
            Image inner = Rect(mark, "Inner", 2.5f, 2.3f, 31, 28.4f).gameObject.AddComponent<Image>();
            inner.sprite = hex;
            inner.color = TealFill;
            inner.raycastTarget = false;
            mark.gameObject.SetActive(false);

            RectTransform tag = Box(tile, "Tag", 14, 13, 98, 22, DarkTeal, null, 0f);
            tag.GetComponent<Image>().raycastTarget = false;
            TextAt(tag, "Label", 0, 0, 98, 22, bold, 11, Cyan, TextAnchor.MiddleCenter, "2–4 PLAYERS");
            TextAt(tile, "Name", 20, 182, 240, 28, bold, 20, Words, TextAnchor.MiddleLeft, "TRAP FLOOR");
            Text line = TextAt(tile, "Detail", 20, 214, 232, 60, medium, 14, Soft, TextAnchor.UpperLeft, string.Empty);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.lineSpacing = 1.15f;
            Text footer = TextAt(tile, "Footer", 20, 294, 240, 18, bold, 12, Cyan, TextAnchor.MiddleLeft,
                "PLAYED LAST  ·  CHOOSE  ›");
            footer.gameObject.SetActive(false);
            tile.gameObject.SetActive(false);
        }

        private static void BackB(RectTransform page, float x, float y, float width, float height)
        {
            RectTransform back = Box(page, "Back", x, y, width, height, Panel, Edge, 1f);
            Button button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = back.GetComponent<Image>();
            Image dot = Rect(back, "Dot", 19, (height - 8f) * 0.5f, 8, 8).gameObject.AddComponent<Image>();
            dot.sprite = circle;
            dot.color = Muted;
            dot.raycastTarget = false;
            TextAt(back, "Label", 34, 0, width - 36, height, bold, 13, Words, TextAnchor.MiddleLeft, "BACK");
        }

        // ---------- the Game sheet (board 3, FE_Details) ----------

        private static void BuildSheet(RectTransform page)
        {
            RectTransform sheet = Box(page, "Sheet", 48, 100, 520, 508, Panel, Edge, 1f);
            Box(sheet, "Cover", 24, 25, 150, 150, Cyan, null, 0f).GetComponent<Image>().raycastTarget = false;
            RawImage picture = Rect(sheet, "Picture", 24, 25, 150, 150).gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            picture.color = new Color(0f, 0f, 0f, 0f);
            RectTransform tag = Box(sheet, "Tag", 192, 25, 98, 22, Cyan, null, 0f);
            TextAt(tag, "Label", 0, 0, 98, 22, bold, 11, Ink, TextAnchor.MiddleCenter, "2–4 PLAYERS");
            TextAt(sheet, "Name", 192, 54, 310, 40, bold, 30, Words, TextAnchor.MiddleLeft, "TRAP FLOOR");
            Text tagline = TextAt(sheet, "Tagline", 192, 98, 300, 60, medium, 14, Soft, TextAnchor.UpperLeft, string.Empty);
            tagline.horizontalOverflow = HorizontalWrapMode.Wrap;
            tagline.lineSpacing = 1.15f;

            TextAt(sheet, "HowHeading", 24, 200, 150, 18, bold, 13, Words, TextAnchor.MiddleLeft, "HOW TO PLAY");
            Line(sheet, "Rule", 142, 209, 328, 1, Edge);
            TextAt(sheet, "Marks", 474, 200, 24, 18, bold, 15, Orange, TextAnchor.MiddleRight, "///");
            RectTransform how = Rect(sheet, "How", 24, 233, 470, 200);
            Sprite play = AssetDatabase.LoadAssetAtPath<Sprite>(HudArtPath + "HudPlay.png");
            for (int i = 0; i < Presentation.UI.FrontEndView.HowToPlayRows; i++)
            {
                RectTransform row = Box(how, "Row" + i, 0, i * 52, 470, 44, Item, ItemEdge, 1f);
                row.GetComponent<Image>().raycastTarget = false;
                Image arrow = Rect(row, "Mark", 16, 17, 7, 10).gameObject.AddComponent<Image>();
                arrow.sprite = play;
                arrow.color = play != null ? Orange : new Color(0f, 0f, 0f, 0f);
                arrow.raycastTarget = false;
                Text text = TextAt(row, "Label", 34, 0, 426, 44, medium, 14, Words, TextAnchor.MiddleLeft, string.Empty);
                text.supportRichText = true;
            }

            Text howEmpty = TextAt(sheet, "HowEmpty", 24, 233, 470, 44, medium, 14, Muted, TextAnchor.MiddleLeft,
                "This game has no written rules yet.");
            howEmpty.gameObject.SetActive(false);
            TextAt(sheet, "Note", 24, 448, 470, 20, medium, 13, Muted, TextAnchor.MiddleLeft,
                "The full rules stay on the table's Rules card while you play.");

            // SETUP: a column, so rows that do not apply leave no gap.
            RectTransform setup = Box(page, "Setup", 600, 100, 632, 508, Panel, Orange, 2f);
            VerticalLayoutGroup column = setup.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(36, 36, 36, 36);
            column.spacing = 12f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            RectTransform heading = Flow(setup, "Heading", 18);
            TextAt(heading, "Label", 0, 0, 580, 18, bold, 13, Words, TextAnchor.MiddleLeft,
                "SETUP  <color=#9BA4A7>· ready to start, change anything you like</color>").supportRichText = true;

            RectTransform players = Flow(setup, "Players", 58);
            AddBox(players, TealFill, Cyan);
            TextAt(players, "Title", 14, 0, 80, 58, bold, 14, Words, TextAnchor.MiddleLeft, "Players");
            TextAt(players, "Hint", 78, 0, 200, 58, medium, 13, Muted, TextAnchor.MiddleLeft, "on this computer");
            SmallButton(players, "Minus", 456, 13, 32, 32, "−", Edge, Cyan);
            TextAt(players, "Count", 494, 13, 40, 32, bold, 18, Words, TextAnchor.MiddleCenter, "4");
            SmallButton(players, "Plus", 540, 13, 32, 32, "+", Edge, Cyan);

            RectTransform seats = Flow(setup, "Seats", 56);
            GridLayoutGroup grid = seats.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(139f * S, 56f * S);
            grid.spacing = new Vector2(8f * S, 8f * S);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            Object.DestroyImmediate(seats.GetComponent<LayoutElement>()); // the grid's own height (one row per four seats)
            for (int i = 0; i < Presentation.UI.FrontEndView.BoardSeatCount; i++)
            {
                RectTransform seat = Rect(seats, "Seat" + i, 0, 0, 139, 56);
                AddBox(seat, Item, Edge);
                Button button = seat.gameObject.AddComponent<Button>();
                button.targetGraphic = seat.GetComponent<Image>();
                Image dot = Rect(seat, "Dot", 14, 15, 9, 9).gameObject.AddComponent<Image>();
                dot.sprite = circle;
                dot.color = Cyan;
                dot.raycastTarget = false;
                TextAt(seat, "Colour", 28, 11, 100, 14, medium, 11, Muted, TextAnchor.MiddleLeft, "BLUE");
                TextAt(seat, "Name", 14, 29, 120, 20, bold, 14, Words, TextAnchor.MiddleLeft, "Player 1");
            }

            ChoiceRow(setup, "Difficulty", "Difficulty");
            ChoiceRow(setup, "WinMode", "Win mode");
            RectTransform more = Flow(setup, "More", 46);
            Sprite dash = AssetDatabase.LoadAssetAtPath<Sprite>(HudArtPath + "HudDashFrame.png");
            Image moreFill = AddBox(more, Well, null);
            if (dash != null)
            {
                Image dashed = Rect(more, "Edge", 0, 0, 580, 46).gameObject.AddComponent<Image>();
                dashed.sprite = dash;
                dashed.type = Image.Type.Sliced;
                dashed.color = ItemEdge;
                dashed.raycastTarget = false;
                dashed.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                RectTransform dashedRect = dashed.rectTransform;
                dashedRect.anchorMin = Vector2.zero;
                dashedRect.anchorMax = Vector2.one;
                dashedRect.offsetMin = Vector2.zero;
                dashedRect.offsetMax = Vector2.zero;
            }

            Button moreButton = more.gameObject.AddComponent<Button>();
            moreButton.targetGraphic = moreFill;
            RectTransform chevron = Rect(more, "Chevron", 16, 18, 8, 10);
            chevron.pivot = new Vector2(0.5f, 0.5f);
            chevron.anchoredPosition = new Vector2(20f * S, -23f * S);
            Image chevronImage = chevron.gameObject.AddComponent<Image>();
            chevronImage.sprite = play;
            chevronImage.color = play != null ? Cyan : new Color(0f, 0f, 0f, 0f);
            chevronImage.raycastTarget = false;
            TextAt(more, "Title", 34, 0, 130, 46, bold, 16, Words, TextAnchor.MiddleLeft, "More options");
            TextAt(more, "Hint", 150, 0, 400, 46, medium, 13, Soft, TextAnchor.MiddleLeft, "Turns · Deck");
            ChoiceRow(setup, "Turns", "Turns");
            ChoiceRow(setup, "Deck", "Deck");
            RectTransform note = Flow(setup, "Note", 20);
            TextAt(note, "Label", 0, 0, 580, 20, medium, 13, Muted, TextAnchor.MiddleLeft,
                "Click a seat to change its colour.");

            BackB(page, 48, 634, 102, 50);
            TextAt(page, "Summary", 640, 634, 416, 50, medium, 14, Muted, TextAnchor.MiddleRight,
                "Trap Floor · 4 players · Hard · Regular");
            RectTransform start = Box(page, "Start", 1071, 630, 161, 58, TealButton, Cyan, 1f);
            Button startButton = start.gameObject.AddComponent<Button>();
            startButton.targetGraphic = start.GetComponent<Image>();
            Image startDot = Rect(start, "Dot", 40, 25, 8, 8).gameObject.AddComponent<Image>();
            startDot.sprite = circle;
            startDot.color = Words;
            startDot.raycastTarget = false;
            TextAt(start, "Label", 56, 0, 90, 58, bold, 18, Words, TextAnchor.MiddleLeft, "START");
        }

        // A row of SETUP's column (its height in board pixels; the parts inside are placed from its top left).
        private static RectTransform Flow(RectTransform column, string name, float height)
        {
            RectTransform row = Rect(column, name, 0, 0, 580, height);
            LayoutElement size = row.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = height * S;
            size.minHeight = height * S;
            return row;
        }

        private static Image AddBox(RectTransform rect, Color fill, Color? edge)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = fill;
            if (edge.HasValue)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = edge.Value;
                outline.effectDistance = new Vector2(S, -S);
                outline.useGraphicAlpha = false;
                Outline other = rect.gameObject.AddComponent<Outline>();
                other.effectColor = edge.Value;
                other.effectDistance = new Vector2(-S, S);
                other.useGraphicAlpha = false;
            }

            return image;
        }

        // Difficulty, Win mode, Turns, Deck: the name on the left, ‹ value › on the right.
        private static void ChoiceRow(RectTransform column, string name, string title)
        {
            RectTransform row = Flow(column, name, 48);
            AddBox(row, Item, Edge);
            TextAt(row, "Title", 14, 0, 300, 48, medium, 14, Words, TextAnchor.MiddleLeft, title);
            SmallButton(row, "Previous", 400, 9, 32, 30, "‹", Edge, Cyan);
            TextAt(row, "Value", 436, 0, 92, 48, bold, 14, Words, TextAnchor.MiddleCenter, "Regular");
            SmallButton(row, "Next", 532, 9, 32, 30, "›", Edge, Cyan);
        }

        // The front end's background as on the boards: near black with a faint dot grid.
        private static void BoardBackground()
        {
            string path = FrontEndBuilder.PrefabPath;
            if (!System.IO.File.Exists(path)) return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform background = root.transform.Find("Background");
                if (background == null || background.Find("Dots") != null) return;
                Image image = background.GetComponent<Image>();
                if (image != null) image.color = Page;
                Sprite dots = AssetDatabase.LoadAssetAtPath<Sprite>(HudArtPath + "HudDots.png");
                if (dots != null)
                {
                    RectTransform rect = (RectTransform)new GameObject("Dots", typeof(RectTransform)).transform;
                    rect.gameObject.layer = 5;
                    rect.SetParent(background, false);
                    UiKit.Stretch(rect);
                    Image tiled = rect.gameObject.AddComponent<Image>();
                    tiled.sprite = dots;
                    tiled.type = Image.Type.Tiled;
                    tiled.color = Dots;
                    tiled.raycastTarget = false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
