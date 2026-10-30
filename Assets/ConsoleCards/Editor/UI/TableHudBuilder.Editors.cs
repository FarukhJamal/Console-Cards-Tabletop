using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;
using B = ConsoleCards.Editor.UI.BoardScreensBuilder;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Boards 10 and 11 of the menu flow (owner, 2026-10-30: "cards plus art will be merged … a deck builder … I want
    /// those things to fix"), measured from the boards (1280 x 720, x1.5), in Final UI/Notices. Every editor has the
    /// same shape: the top bar (CREATE › NAME, its controls, UNDO), the list on the left, the big preview in the middle,
    /// the chosen thing's settings on the right, BACK bottom left and DONE bottom right.
    /// <list type="bullet">
    /// <item>CardStudio10 (+ rows): the Card studio (board 10), the card editor and the card pictures in one.</item>
    /// <item>DeckBuilder10 (+ card tile): the deck builder (board 11).</item>
    /// </list>
    /// The catalog points the game at them. Made once; after that they are kept (edit them freely).
    /// </summary>
    public static partial class TableHudBuilder
    {
        public const string StudioPath = Folder + "CardStudio10.prefab";
        public const string StudioRowPath = Folder + "CardStudioRow10.prefab";
        public const string StudioDeckRowPath = Folder + "CardStudioDeckRow10.prefab";
        public const string DecksPath = Folder + "DeckBuilder11.prefab";
        public const string DeckTilePath = Folder + "DeckBuilderTile11.prefab";

        private static readonly Color EdPage = Hex("101314");
        private static readonly Color EdBar = Hex("1c1f1f");
        private static readonly Color EdPane = Hex("222527");
        private static readonly Color EdDark = Hex("191c1d");
        private static readonly Color EdRow = Hex("292e2f");
        private static readonly Color EdRowEdge = Hex("424547");
        private static readonly Color EdFaint = Hex("6b7477");
        private static readonly Color EdSoft = Hex("b7c0c3");
        private static readonly Color EdTeal = Hex("1c3c3d");
        private static readonly Color EdTealEdge = Hex("01f7f9");
        private static readonly Color EdGreen = Hex("2fd46b");
        private static readonly Color EdWarnFill = Hex("3a2a12");
        private static readonly Color EdWarnEdge = Hex("ffb21e");
        private static readonly Color EdWarnWords = Hex("ffd9a0");
        private static readonly Color EdRed = Hex("ff7a6b");
        private static readonly Color EdTealWords = Hex("f4fefe");

        private static void BuildEditors()
        {
            B.Prepare();
            round = round != null ? round : HudSprite("HudRound", new Vector4(16f, 16f, 16f, 16f));
            frame = frame != null ? frame : HudSprite("HudFrame", new Vector4(16f, 16f, 16f, 16f));
            if (round == null) round = rounded;
            string[] paths = { StudioRowPath, StudioDeckRowPath, StudioPath, DeckTilePath, DecksPath };
            foreach (string path in paths) ForgetOlderBuild(path, EditorsLabel);
            Save<CardEditorRowView>(StudioRowPath, "CardStudioRow10", BuildStudioRow);
            Save<CardEditorDeckRowView>(StudioDeckRowPath, "CardStudioDeckRow10", BuildStudioDeckRow);
            Save<CardEditorPanelView>(StudioPath, "CardStudio10", BuildStudio);
            Save<DeckEditorCardRowView>(DeckTilePath, "DeckBuilderTile11", BuildDeckTile);
            Save<DeckEditorPanelView>(DecksPath, "DeckBuilder11", BuildDeckBuilder);
            foreach (string path in paths) MarkBuild(path, EditorsLabel);
            AssignCatalogEntry(UiPrefabIds.CardEditorRow, AssetDatabase.LoadAssetAtPath<CardEditorRowView>(StudioRowPath),
                UiLayer.Modal, UiRetention.Pooled);
            AssignCatalogEntry(UiPrefabIds.CardEditorDeckRow,
                AssetDatabase.LoadAssetAtPath<CardEditorDeckRowView>(StudioDeckRowPath), UiLayer.Modal, UiRetention.Pooled);
            AssignCatalogEntry(UiPrefabIds.CardEditorPanel, AssetDatabase.LoadAssetAtPath<CardEditorPanelView>(StudioPath),
                UiLayer.Modal, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(UiPrefabIds.DeckEditorCardRow,
                AssetDatabase.LoadAssetAtPath<DeckEditorCardRowView>(DeckTilePath), UiLayer.Modal, UiRetention.Pooled);
            AssignCatalogEntry(UiPrefabIds.DeckEditorPanel, AssetDatabase.LoadAssetAtPath<DeckEditorPanelView>(DecksPath),
                UiLayer.Modal, UiRetention.CachedSingleInstance);
        }

        private const string EditorsLabel = "BoardEditors4";

        // A prefab this builder made before its current version (no label) is made again once; one with the label is
        // kept (it may have been edited).
        private static void ForgetOlderBuild(string path, string label)
        {
            GameObject made = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (made != null && System.Array.IndexOf(AssetDatabase.GetLabels(made), label) < 0) AssetDatabase.DeleteAsset(path);
        }

        private static void MarkBuild(string path, string label)
        {
            GameObject made = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (made != null) AssetDatabase.SetLabels(made, new[] { label });
        }

        /// <summary>Turn Off: the editors before boards 10 and 11.</summary>
        private static void RestoreEditors()
        {
            Restore<CardEditorRowView>(UiPrefabIds.CardEditorRow, GameplayUiBuilder.CardEditorRowPath, UiRetention.Pooled);
            Restore<CardEditorDeckRowView>(UiPrefabIds.CardEditorDeckRow, GameplayUiBuilder.CardEditorDeckRowPath, UiRetention.Pooled);
            Restore<CardEditorPanelView>(UiPrefabIds.CardEditorPanel, GameplayUiBuilder.CardEditorPanelPath,
                UiRetention.CachedSingleInstance);
            Restore<DeckEditorCardRowView>(UiPrefabIds.DeckEditorCardRow, GameplayUiBuilder.DeckEditorCardRowPath, UiRetention.Pooled);
            Restore<DeckEditorPanelView>(UiPrefabIds.DeckEditorPanel, GameplayUiBuilder.DeckEditorPanelPath,
                UiRetention.CachedSingleInstance);
        }

        private static void Restore<T>(string id, string path, UiRetention retention) where T : ReusableUiView
        {
            T view = AssetDatabase.LoadAssetAtPath<T>(path);
            if (view != null) AssignCatalogEntry(id, view, UiLayer.Modal, retention);
        }

        // ---------- the Card studio (board 10) ----------

        private static void BuildStudio(RectTransform root, CardEditorPanelView view)
        {
            Button overlay = EditorPage(root);
            RectTransform header = EditorHeader(root, "CREATE ›", "CARD STUDIO", out Text title);
            Txt(header, "GameTitle", 726, 0, 50, 56, bold, 11, Muted, TextAnchor.MiddleRight, "GAME");
            Button previousGame = Btn(header, "PreviousGame", 784, 11, 34, 34, "‹", 13);
            Text game = Txt(header, "GameName", 822, 0, 136, 56, bold, 14, Words, TextAnchor.MiddleCenter, "TRAP FLOOR");
            game.resizeTextForBestFit = true;
            game.resizeTextMinSize = 12;
            game.resizeTextMaxSize = 21;
            Button nextGame = Btn(header, "NextGame", 962, 11, 34, 34, "›", 13);
            Line10(header, "Divider", 1010, 14, 1, 28, Edge);
            Button undo = Btn(header, "Undo", 1024, 11, 168, 34, "UNDO CHANGES", 12);

            // The cards.
            RectTransform left = Box10(root, "Cards", 24, 92, 270, 536, EdPane, Edge);
            InputField search = Field10(left, "Find", 12, 12, 246, 36, "Find a card…", 14, false);
            string[] filters = { "ALL 24", "YOURS", "NO PICTURE", "WRONG SHAPE" };
            float[] filterX = { 12, 80, 146, 12 };
            float[] filterY = { 56, 56, 56, 88 };
            float[] filterW = { 62, 60, 102, 110 };
            Button[] filterButtons = new Button[filters.Length];
            Image[] filterFills = new Image[filters.Length];
            Text[] filterLabels = new Text[filters.Length];
            for (int i = 0; i < filters.Length; i++)
            {
                RectTransform chip = Box10(left, "Filter" + i, filterX[i], filterY[i], filterW[i], 26, new Color(0f, 0f, 0f, 0f), Edge);
                filterFills[i] = chip.GetComponent<Image>();
                filterButtons[i] = chip.gameObject.AddComponent<Button>();
                filterButtons[i].targetGraphic = filterFills[i];
                filterLabels[i] = Txt(chip, "Label", 0, 0, filterW[i], 26, bold, 11, EdSoft, TextAnchor.MiddleCenter, filters[i]);
            }

            ScrollRect cardsScroll = Scroll10(left, "List", 12, 122, 246, 350, 8f, out RectTransform rows);
            Button newCard = Btn(left, "NewCard", 12, 480, 246, 44, "+ NEW CARD", 13, EdBar, Edge, Cyan);

            // Front and back.
            RectTransform middle = Box10(root, "Pictures", 310, 92, 540, 536, EdDark, Edge);
            RectTransform seg = Box10(middle, "Showing", 110, 14, 320, 36, EdBar, Edge);
            RectTransform segOn = Box10(seg, "FrontBack", 3, 3, 314, 30, EdTeal, null);
            Txt(segOn, "Label", 0, 0, 314, 30, bold, 11, Words, TextAnchor.MiddleCenter, "FRONT + BACK");
            RawImage front = CardSlot(middle, "Front", 28, 69, "FRONT", out Text blankName, out Text frontFile, out Button chooseFront,
                "REPLACE…", out Button clearFront, "REMOVE");
            RawImage back = CardSlot(middle, "Back", 284, 69, "BACK", out _, out Text backFile, out Button chooseBack, "REPLACE…",
                out Button clearBack, "USE DECK'S BACK");
            Text hint = Txt(middle, "Hint", 20, 478, 500, 40, medium, 12, Muted, TextAnchor.MiddleCenter,
                "Drop-in pictures: REPLACE… picks a PNG for either side. The dashed line is the safe area:\nkeep words inside it, the corners are rounded off on the table.");
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;

            // The chosen card's settings.
            RectTransform right = Box10(root, "Settings", 866, 92, 390, 536, EdPane, Edge);
            RectTransform tabs = Box10(right, "Tabs", 14, 14, 362, 36, EdBar, Edge);
            string[] tabNames = { "PICTURES", "GAME DATA", "IN DECKS" };
            Button[] tabButtons = new Button[3];
            Image[] tabFills = new Image[3];
            GameObject[] tabPages = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform tab = Box10(tabs, "Tab" + i, 3 + (i * 119), 3, 116, 30, EdTeal, null);
                tabFills[i] = tab.GetComponent<Image>();
                tabButtons[i] = tab.gameObject.AddComponent<Button>();
                tabButtons[i].targetGraphic = tabFills[i];
                Txt(tab, "Label", 0, 0, 116, 30, bold, 11, Words, TextAnchor.MiddleCenter, tabNames[i]);
                RectTransform page = Rect10(right, tabNames[i].Replace(" ", string.Empty), 14, 60, 362, 410);
                tabPages[i] = page.gameObject;
            }

            // PICTURES
            RectTransform pictures = (RectTransform)tabPages[0].transform;
            Txt(pictures, "SizeTitle", 0, 0, 362, 18, bold, 11, Muted, TextAnchor.MiddleLeft, "CARD SIZE");
            RectTransform sizeBox = Box10(pictures, "Size", 0, 26, 362, 132, EdRow, EdRowEdge);
            InfoRow(sizeBox, 0, "Shape", "5 : 7 portrait");
            InfoRow(sizeBox, 1, "Best size", "1500 × 2100 px");
            InfoRow(sizeBox, 2, "Smallest sharp size", "750 × 1050 px");
            Button template = Btn(sizeBox, "Template", 10, 84, 342, 36, "GET THE TEMPLATE PNG", 12);
            Txt(pictures, "ShapeTitle", 0, 172, 362, 18, bold, 11, Muted, TextAnchor.MiddleLeft, "WHEN A PICTURE IS THE WRONG SHAPE");
            Text never = Txt(pictures, "Never", 0, 194, 362, 40, medium, 12, Muted, TextAnchor.UpperLeft,
                "Pictures are never stretched: one of another shape is cropped to fill the card.");
            never.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform warning = Box10(pictures, "Warning", 0, 242, 362, 76, EdWarnFill, EdWarnEdge);
            Text warningWords = Txt(warning, "Label", 12, 8, 338, 60, medium, 12, EdWarnWords, TextAnchor.UpperLeft,
                "This picture is not the card's shape.");
            warningWords.horizontalOverflow = HorizontalWrapMode.Wrap;
            warning.gameObject.SetActive(false);

            // GAME DATA
            RectTransform data = (RectTransform)tabPages[1].transform;
            Text heading = Txt(data, "Heading", 0, 0, 230, 20, bold, 13, Words, TextAnchor.MiddleLeft, "GAME CARD");
            Text id = Txt(data, "Id", 230, 0, 132, 20, medium, 10, EdFaint, TextAnchor.MiddleRight, "ID");
            id.horizontalOverflow = HorizontalWrapMode.Wrap;
            Txt(data, "NameTitle", 0, 26, 362, 16, bold, 11, Muted, TextAnchor.MiddleLeft, "NAME (FOR LISTS, SEARCH AND THE LOG)");
            InputField name = Field10(data, "Name", 0, 44, 362, 34, "The card's name", 14, false);
            name.characterLimit = 40;
            Txt(data, "DescriptionTitle", 0, 86, 362, 16, bold, 11, Muted, TextAnchor.MiddleLeft, "WHAT IT DOES (NOT DRAWN ON THE CARD)");
            InputField description = Field10(data, "Description", 0, 104, 362, 58, "What the card does…", 13, true);
            Txt(data, "CostTitle", 0, 170, 230, 16, bold, 11, Muted, TextAnchor.MiddleLeft, "COST");
            InputField cost = Field10(data, "Cost", 0, 188, 230, 34, "Up + A/B/X/Y", 13, false);
            cost.characterLimit = 60;
            Txt(data, "CodeTitle", 240, 170, 122, 16, bold, 11, Muted, TextAnchor.MiddleLeft, "SHORT CODE");
            InputField code = Field10(data, "Code", 240, 188, 122, 34, "MY1", 13, false);
            code.characterLimit = 8;
            Txt(data, "WorksLikeTitle", 0, 230, 362, 16, bold, 11, Muted, TextAnchor.MiddleLeft, "WORKS LIKE");
            Button previousKind = Btn(data, "PreviousKind", 0, 248, 34, 34, "‹", 13);
            Text kind = Txt(data, "WorksLike", 40, 248, 282, 34, bold, 13, Words, TextAnchor.MiddleCenter, "Fall");
            kind.resizeTextForBestFit = true;
            kind.resizeTextMinSize = 12;
            kind.resizeTextMaxSize = 20;
            Button nextKind = Btn(data, "NextKind", 328, 248, 34, 34, "›", 13);
            // The old preview words: kept for the view, not shown (the picture is the card).
            RectTransform hiddenWords = Rect10(data, "PreviewWords", 0, 290, 362, 60);
            Text previewName = Txt(hiddenWords, "Name", 0, 0, 362, 20, bold, 12, Words, TextAnchor.UpperLeft, string.Empty);
            Text previewCost = Txt(hiddenWords, "Cost", 0, 20, 362, 20, bold, 12, Orange, TextAnchor.UpperLeft, string.Empty);
            Text previewDescription = Txt(hiddenWords, "Description", 0, 40, 362, 20, medium, 12, Words, TextAnchor.UpperLeft, string.Empty);
            hiddenWords.gameObject.SetActive(false);

            // IN DECKS
            RectTransform decks = (RectTransform)tabPages[2].transform;
            Text summary = Txt(decks, "Summary", 0, 0, 230, 34, bold, 12, Words, TextAnchor.MiddleLeft, "TRAP FLOOR DECK · 37 CARDS");
            summary.resizeTextForBestFit = true;
            summary.resizeTextMinSize = 10;
            summary.resizeTextMaxSize = 18;
            Button newDeck = Btn(decks, "NewDeck", 240, 0, 122, 34, "+ NEW DECK", 12, EdBar, Edge, Cyan);
            Scroll10(decks, "DeckList", 0, 44, 362, 360, 8f, out RectTransform deckRows);
            tabPages[1].SetActive(false);
            tabPages[2].SetActive(false);

            Button reset = Btn(right, "Reset", 14, 487, 250, 34, "RESET TO GAME'S", 12, EdBar, Edge, Words, out Text resetLabel);
            Button delete = Btn(right, "Delete", 272, 487, 104, 34, "DELETE", 12, EdBar, Edge, EdRed);

            // BACK | note | DONE (a hidden SAVE for the view: DONE saves and closes).
            EditorFooter(root, out Button close, out Button done, out Text status,
                "GAME DATA: the name (for lists, search and the log), what it works like and its cost. None of it is drawn on the card.");
            Button save = Btn(root, "Save", 0, 0, 10, 10, "SAVE", 10);
            save.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("previousGameButton").objectReferenceValue = previousGame;
            so.FindProperty("nextGameButton").objectReferenceValue = nextGame;
            so.FindProperty("gameLabel").objectReferenceValue = game;
            so.FindProperty("deckSummaryLabel").objectReferenceValue = summary;
            so.FindProperty("newCardButton").objectReferenceValue = newCard;
            so.FindProperty("rowsRoot").objectReferenceValue = rows;
            so.FindProperty("scroll").objectReferenceValue = cardsScroll;
            so.FindProperty("frontPreview").objectReferenceValue = front;
            so.FindProperty("frontBlankName").objectReferenceValue = blankName;
            so.FindProperty("backPreview").objectReferenceValue = back;
            so.FindProperty("previewName").objectReferenceValue = previewName;
            so.FindProperty("previewCost").objectReferenceValue = previewCost;
            so.FindProperty("previewDescription").objectReferenceValue = previewDescription;
            so.FindProperty("headingLabel").objectReferenceValue = heading;
            so.FindProperty("idLabel").objectReferenceValue = id;
            so.FindProperty("codeInput").objectReferenceValue = code;
            so.FindProperty("nameInput").objectReferenceValue = name;
            so.FindProperty("descriptionInput").objectReferenceValue = description;
            so.FindProperty("costInput").objectReferenceValue = cost;
            so.FindProperty("worksLikePrevious").objectReferenceValue = previousKind;
            so.FindProperty("worksLikeNext").objectReferenceValue = nextKind;
            so.FindProperty("worksLikeLabel").objectReferenceValue = kind;
            so.FindProperty("chooseFrontButton").objectReferenceValue = chooseFront;
            so.FindProperty("clearFrontButton").objectReferenceValue = clearFront;
            so.FindProperty("chooseBackButton").objectReferenceValue = chooseBack;
            so.FindProperty("clearBackButton").objectReferenceValue = clearBack;
            so.FindProperty("deckRowsRoot").objectReferenceValue = deckRows;
            so.FindProperty("newDeckButton").objectReferenceValue = newDeck;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("deleteButton").objectReferenceValue = delete;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.FindProperty("resetLabel").objectReferenceValue = resetLabel;
            so.FindProperty("saveButton").objectReferenceValue = save;
            so.FindProperty("searchInput").objectReferenceValue = search;
            AssignArray(so.FindProperty("filterButtons"), filterButtons);
            AssignArray(so.FindProperty("filterFills"), filterFills);
            AssignArray(so.FindProperty("filterLabels"), filterLabels);
            so.FindProperty("frontFileLabel").objectReferenceValue = frontFile;
            so.FindProperty("backFileLabel").objectReferenceValue = backFile;
            AssignArray(so.FindProperty("tabButtons"), tabButtons);
            AssignArray(so.FindProperty("tabFills"), tabFills);
            AssignArray(so.FindProperty("tabPages"), tabPages);
            so.FindProperty("tabOn").colorValue = EdTeal;
            so.FindProperty("tabOff").colorValue = new Color(0f, 0f, 0f, 0f);
            so.FindProperty("shapeWarning").objectReferenceValue = warning.gameObject;
            so.FindProperty("shapeWarningLabel").objectReferenceValue = warningWords;
            so.FindProperty("templateButton").objectReferenceValue = template;
            so.FindProperty("undoButton").objectReferenceValue = undo;
            so.FindProperty("doneButton").objectReferenceValue = done;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // One side of the card: its picture (200 x 280), the FRONT / BACK tag, the dashed safe area, the file line and
        // its two buttons.
        private static RawImage CardSlot(RectTransform parent, string name, float x, float y, string tag, out Text blankName,
            out Text file, out Button choose, string chooseWords, out Button clear, string clearWords)
        {
            RectTransform slot = Box10(parent, name, x, y, 200, 280, EdRow, EdRowEdge);
            RawImage picture = Rect10(slot, "Picture", 0, 0, 200, 280).gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            blankName = Txt(slot, "Blank", 10, 100, 180, 80, bold, 13, EdSoft, TextAnchor.MiddleCenter, name.ToUpperInvariant());
            blankName.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform safe = Box10(slot, "SafeArea", 10, 10, 180, 260, new Color(0f, 0f, 0f, 0f), new Color(0.024f, 0.937f, 0.941f, 0.55f));
            safe.GetComponent<Image>().raycastTarget = false;
            RectTransform chip = Box10(slot, "Tag", 8, 8, 46, 20, new Color(0.063f, 0.075f, 0.078f, 0.8f), null);
            Txt(chip, "Label", 0, 0, 46, 20, bold, 10, Words, TextAnchor.MiddleCenter, tag);
            file = Txt(parent, name + "File", x - 20, y + 288, 240, 20, medium, 12, Words, TextAnchor.MiddleCenter, "1500 × 2100");
            float clearWidth = clearWords.Length > 8 ? 150 : 83;
            float total = 100 + 6 + clearWidth;
            float start = x + 100 - (total * 0.5f);
            choose = Btn(parent, name + "Choose", start, y + 311, 100, 28, chooseWords, 12);
            clear = Btn(parent, name + "Clear", start + 106, y + 311, clearWidth, 28, clearWords, 12);
            return picture;
        }

        private static void InfoRow(RectTransform box, int index, string key, string value)
        {
            Txt(box, key.Replace(" ", string.Empty), 10, 8 + (index * 22), 200, 20, medium, 13, Muted, TextAnchor.MiddleLeft, key);
            Txt(box, key.Replace(" ", string.Empty) + "Value", 160, 8 + (index * 22), 192, 20, bold, 13, Words, TextAnchor.MiddleRight, value);
        }

        // One card in the studio's list: a 28 x 39 picture, the name and what it is; the chosen one teal with a cyan bar.
        private static void BuildStudioRow(RectTransform root, CardEditorRowView view)
        {
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(246f * P, 52f * P);
            Image fill = root.gameObject.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.color = EdRow;
            Outline[] edges = Edges(root, EdRowEdge);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            Size(root.gameObject, -1f, 52f * P);
            RectTransform bar = Rect("Bar", root, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(4f * P, 0f));
            Shape(bar, Cyan, null);
            bar.gameObject.SetActive(false);
            RawImage thumbnail = Rect10(root, "Thumbnail", 10, 6.5f, 28, 39).gameObject.AddComponent<RawImage>();
            thumbnail.raycastTarget = false;
            Text name = Txt(root, "Name", 48, 7, 190, 20, bold, 13, Words, TextAnchor.MiddleLeft, "Heavy Kick");
            Text detail = Txt(root, "Detail", 48, 27, 190, 18, medium, 11, Muted, TextAnchor.MiddleLeft, "front + back · yours");
            detail.horizontalOverflow = HorizontalWrapMode.Overflow;

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("selectButton").objectReferenceValue = button;
            so.FindProperty("selectedFill").objectReferenceValue = fill;
            so.FindProperty("thumbnail").objectReferenceValue = thumbnail;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("detailLabel").objectReferenceValue = detail;
            so.FindProperty("normalColor").colorValue = EdRow;
            so.FindProperty("selectedColor").colorValue = EdTeal;
            so.FindProperty("selectedBar").objectReferenceValue = bar.gameObject;
            AssignArray(so.FindProperty("edges"), edges);
            so.FindProperty("edgeColor").colorValue = EdRowEdge;
            so.FindProperty("selectedEdgeColor").colorValue = EdTealEdge;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // One deck the card can be in (IN DECKS): a tick, the deck's name and size, − copies +.
        private static void BuildStudioDeckRow(RectTransform root, CardEditorDeckRowView view)
        {
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(362f * P, 46f * P);
            Image fill = root.gameObject.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.color = EdRow;
            fill.raycastTarget = false;
            Edges(root, EdRowEdge);
            Size(root.gameObject, -1f, 46f * P);
            RectTransform tick = Box10(root, "Tick", 10, 12, 22, 22, Color.white, EdRowEdge);
            Image tickFill = tick.GetComponent<Image>();
            Button tickButton = tick.gameObject.AddComponent<Button>();
            tickButton.targetGraphic = tickFill;
            Text name = Txt(root, "Name", 42, 5, 200, 20, bold, 13, Words, TextAnchor.MiddleLeft, "Trap Floor deck");
            Text detail = Txt(root, "Detail", 42, 24, 200, 18, medium, 11, Muted, TextAnchor.MiddleLeft, "37 cards");
            Button fewer = Btn(root, "Decrement", 256, 11, 24, 24, "−", 13);
            Text copies = Txt(root, "Copies", 282, 11, 30, 24, bold, 13, Words, TextAnchor.MiddleCenter, "1");
            Button more = Btn(root, "Increment", 314, 11, 24, 24, "+", 13);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("tickButton").objectReferenceValue = tickButton;
            so.FindProperty("tickFill").objectReferenceValue = tickFill;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("detailLabel").objectReferenceValue = detail;
            so.FindProperty("decrementButton").objectReferenceValue = fewer;
            so.FindProperty("incrementButton").objectReferenceValue = more;
            so.FindProperty("copiesLabel").objectReferenceValue = copies;
            so.FindProperty("inColor").colorValue = Cyan;
            so.FindProperty("outColor").colorValue = EdBar;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- the deck builder (board 11) ----------

        private static void BuildDeckBuilder(RectTransform root, DeckEditorPanelView view)
        {
            Button overlay = EditorPage(root);
            RectTransform header = EditorHeader(root, "CREATE ›", "DECKS", out Text title);
            Txt(header, "GameTitle", 726, 0, 50, 56, bold, 11, Muted, TextAnchor.MiddleRight, "GAME");
            Button previousGame = Btn(header, "PreviousGame", 784, 11, 34, 34, "‹", 13);
            Text game = Txt(header, "GameName", 822, 0, 136, 56, bold, 14, Words, TextAnchor.MiddleCenter, "TRAP FLOOR");
            game.resizeTextForBestFit = true;
            game.resizeTextMinSize = 12;
            game.resizeTextMaxSize = 21;
            Button nextGame = Btn(header, "NextGame", 962, 11, 34, 34, "›", 13);
            Line10(header, "Divider", 1010, 14, 1, 28, Edge);
            Button undo = Btn(header, "Undo", 1024, 11, 168, 34, "UNDO CHANGES", 12);

            // YOUR DECKS
            RectTransform left = Box10(root, "Decks", 24, 92, 250, 536, EdPane, Edge);
            Txt(left, "Title", 12, 12, 226, 16, bold, 11, Muted, TextAnchor.MiddleLeft, "YOUR DECKS");
            ScrollRect deckScroll = Scroll10(left, "List", 12, 36, 226, 350, 8f, out RectTransform deckRows);
            Button delete = Btn(left, "Delete", 12, 394, 226, 34, "DELETE THIS DECK", 12, EdBar, Edge, EdRed);
            Button copy = Btn(left, "Copy", 12, 436, 226, 40, "COPY THIS DECK", 12);
            Button newDeck = Btn(left, "NewDeck", 12, 482, 226, 44, "+ NEW DECK", 13, EdBar, Edge, Cyan);

            // The deck.
            RectTransform middle = Box10(root, "Deck", 290, 92, 600, 536, EdDark, Edge);
            InputField deckName = Field10(middle, "Name", 14, 14, 240, 38, "Deck name", 14, false);
            deckName.textComponent.fontStyle = FontStyle.Bold;
            deckName.characterLimit = 40;
            Button rename = Btn(middle, "Rename", 262, 16, 84, 34, "RENAME", 12);
            Text size = Txt(middle, "Size", 380, 14, 206, 38, medium, 13, Muted, TextAnchor.MiddleRight, "Size 32");
            RectTransform bar = Box10(middle, "Bar", 14, 64, 572, 6, Hex("2b3033"), null);
            Box10(bar, "Fill", 0, 0, 572, 6, Cyan, null);
            bar.gameObject.SetActive(false); // the games give no size range yet (board: Size 32 / 30–40)
            ScrollRect cardScroll = Grid10(middle, "Cards", 14, 82, 572, 392, out RectTransform cardRows);
            Text summary = Txt(middle, "Summary", 14, 492, 380, 30, bold, 11, Muted, TextAnchor.MiddleLeft, "TRAP FLOOR DECK · 37 CARDS");
            Button filter = Btn(middle, "Filter", 0, 0, 10, 10, "Show", 10, EdBar, Edge, Words, out Text filterLabel);
            filter.gameObject.SetActive(false);
            Button shuffle = Btn(middle, "ShuffleTest", 400, 492, 186, 30, "SHUFFLE TEST · DRAW 5", 12);

            // ADD CARDS
            RectTransform right = Box10(root, "AddCards", 906, 92, 350, 536, EdPane, Edge);
            Txt(right, "Title", 12, 12, 326, 16, bold, 11, Muted, TextAnchor.MiddleLeft, "ADD CARDS");
            InputField find = Field10(right, "Find", 12, 36, 326, 38, "Find a card…", 14, false);
            Scroll10(right, "List", 12, 84, 326, 390, 8f, out RectTransform addRows);
            RectTransform template = Box10(addRows, "AddTemplate", 0, 0, 326, 46, EdRow, EdRowEdge);
            Size(template.gameObject, -1f, 46f * P);
            RawImage thumb = Rect10(template, "Thumb", 8, 8, 22, 30).gameObject.AddComponent<RawImage>();
            thumb.raycastTarget = false;
            Txt(template, "Name", 40, 0, 190, 46, medium, 13, Words, TextAnchor.MiddleLeft, "Fall");
            Txt(template, "Count", 222, 0, 56, 46, medium, 11, Muted, TextAnchor.MiddleRight, "8 in");
            Btn(template, "Add", 288, 9, 28, 28, "+", 13);
            template.gameObject.SetActive(false);
            Text addNote = Txt(right, "Note", 12, 486, 326, 40, medium, 11, EdFaint, TextAnchor.MiddleLeft,
                "Click + to add one copy; − on a card in the deck takes one out.");
            addNote.horizontalOverflow = HorizontalWrapMode.Wrap;

            EditorFooter(root, out Button close, out Button done, out Text status,
                "A deck outside its game's size still saves, with a warning when a game uses it.");
            Button save = Btn(root, "Save", 0, 0, 10, 10, "SAVE", 10);
            save.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("previousGameButton").objectReferenceValue = previousGame;
            so.FindProperty("nextGameButton").objectReferenceValue = nextGame;
            so.FindProperty("gameLabel").objectReferenceValue = game;
            so.FindProperty("deckRowsRoot").objectReferenceValue = deckRows;
            so.FindProperty("deckScroll").objectReferenceValue = deckScroll;
            so.FindProperty("deckNameInput").objectReferenceValue = deckName;
            so.FindProperty("newDeckButton").objectReferenceValue = newDeck;
            so.FindProperty("copyDeckButton").objectReferenceValue = copy;
            so.FindProperty("renameDeckButton").objectReferenceValue = rename;
            so.FindProperty("deleteDeckButton").objectReferenceValue = delete;
            so.FindProperty("summaryLabel").objectReferenceValue = summary;
            so.FindProperty("filterButton").objectReferenceValue = filter;
            so.FindProperty("filterLabel").objectReferenceValue = filterLabel;
            so.FindProperty("cardRowsRoot").objectReferenceValue = cardRows;
            so.FindProperty("cardScroll").objectReferenceValue = cardScroll;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("undoButton").objectReferenceValue = undo;
            so.FindProperty("saveButton").objectReferenceValue = save;
            so.FindProperty("addRowsRoot").objectReferenceValue = addRows;
            so.FindProperty("addRowTemplate").objectReferenceValue = template.gameObject;
            so.FindProperty("addSearch").objectReferenceValue = find;
            so.FindProperty("sizeLabel").objectReferenceValue = size;
            so.FindProperty("shuffleTestButton").objectReferenceValue = shuffle;
            so.FindProperty("doneButton").objectReferenceValue = done;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A card in the deck: its picture (84 x 116) with the name at the bottom, and − copies + under it.
        private static void BuildDeckTile(RectTransform root, DeckEditorCardRowView view)
        {
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(106f * P, 150f * P);
            RectTransform face = Box10(root, "Face", 11, 0, 84, 116, EdRow, null);
            Image fill = face.GetComponent<Image>();
            Shadow shadow = face.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Hex("15191a");
            shadow.effectDistance = new Vector2(4.5f, -4.5f);
            RawImage thumbnail = Rect10(face, "Picture", 0, 0, 84, 116).gameObject.AddComponent<RawImage>();
            thumbnail.raycastTarget = false;
            Text name = Txt(face, "Name", 6, 92, 72, 18, bold, 10, Words, TextAnchor.LowerLeft, "Fall");
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            Shadow nameShadow = name.gameObject.AddComponent<Shadow>();
            nameShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            nameShadow.effectDistance = new Vector2(0f, -1.5f);
            Text detail = Txt(root, "Detail", 0, 0, 1, 1, medium, 8, Muted, TextAnchor.MiddleLeft, string.Empty);
            detail.gameObject.SetActive(false);
            Button fewer = Btn(root, "Decrement", 15, 122, 24, 24, "−", 13);
            Text copies = Txt(root, "Copies", 41, 122, 26, 24, bold, 13, Words, TextAnchor.MiddleCenter, "8");
            Button more = Btn(root, "Increment", 69, 122, 24, 24, "+", 13);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("thumbnail").objectReferenceValue = thumbnail;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("detailLabel").objectReferenceValue = detail;
            so.FindProperty("decrementButton").objectReferenceValue = fewer;
            so.FindProperty("incrementButton").objectReferenceValue = more;
            so.FindProperty("copiesLabel").objectReferenceValue = copies;
            so.FindProperty("inDeckColor").colorValue = EdRow;
            so.FindProperty("outOfDeckColor").colorValue = EdRow;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A grid that scrolls: the deck's cards, five across.
        private static ScrollRect Grid10(Transform parent, string name, float x, float y, float width, float height,
            out RectTransform content)
        {
            RectTransform viewport = Rect10(parent, name, x, y, width, height);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                Vector2.zero);
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(106f * P, 150f * P);
            grid.spacing = new Vector2(10f * P, 14f * P);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperLeft;
            Fit(content.gameObject, false, true);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        // ---------- the shared parts of every editor ----------

        // The page: the board's background over the whole screen (it takes the clicks; it does not close the editor).
        private static Button EditorPage(RectTransform root)
        {
            Image page = root.gameObject.AddComponent<Image>();
            page.color = EdPage;
            page.raycastTarget = true;
            RectTransform dots = Rect("Dots", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image dotImage = dots.gameObject.AddComponent<Image>();
            dotImage.color = new Color(0.118f, 0.149f, 0.157f, 1f);
            dotImage.raycastTarget = false;
            dotImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ConsoleCards/Content/UI/Board/Dots.png");
            dotImage.type = Image.Type.Tiled;
            if (dotImage.sprite == null) dots.gameObject.SetActive(false);
            // The view needs a dismiss button; on the board the page itself does nothing when clicked.
            Button overlay = Rect("Catcher", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)
                .gameObject.AddComponent<Button>();
            overlay.transition = Selectable.Transition.None;
            overlay.interactable = false;
            return overlay;
        }

        // The top bar: the cap, CREATE ›, the editor's name, its controls (added by the caller) and the second cap.
        private static RectTransform EditorHeader(RectTransform root, string crumb, string name, out Text title)
        {
            RectTransform header = Box10(root, "Header", 24, 20, 1232, 56, EdBar, Edge);
            Cap(header, 18);
            Cap(header, 1204);
            Txt(header, "Crumb", 45, 0, 70, 56, medium, 13, Muted, TextAnchor.MiddleLeft, crumb);
            title = Txt(header, "Title", 120, 0, 300, 56, bold, 17, Words, TextAnchor.MiddleLeft, name);
            return header;
        }

        private static void Cap(RectTransform header, float x)
        {
            Image cap = Rect10(header, "Cap", x, 12, 10, 32).gameObject.AddComponent<Image>();
            cap.sprite = B.Stripes;
            cap.type = B.Stripes != null ? Image.Type.Tiled : Image.Type.Simple;
            cap.color = B.Stripes != null ? Color.white : Orange;
            cap.raycastTarget = false;
        }

        // BACK bottom left (grey dot), the note, DONE bottom right (teal, white dot).
        private static void EditorFooter(RectTransform root, out Button back, out Button done, out Text note, string words)
        {
            RectTransform backRect = Box10(root, "Back", 24, 650, 94, 46, EdBar, Edge);
            back = backRect.gameObject.AddComponent<Button>();
            back.targetGraphic = backRect.GetComponent<Image>();
            Dot10(backRect, 19, 19, EdFaint);
            Txt(backRect, "Label", 34, 0, 56, 46, bold, 13, Words, TextAnchor.MiddleLeft, "BACK");
            note = Txt(root, "Note", 132, 650, 1000, 46, medium, 12, EdFaint, TextAnchor.MiddleLeft, words);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform doneRect = Box10(root, "Done", 1161, 650, 95, 46, Teal, Cyan);
            done = doneRect.gameObject.AddComponent<Button>();
            done.targetGraphic = doneRect.GetComponent<Image>();
            Dot10(doneRect, 19, 19, Words);
            Txt(doneRect, "Label", 34, 0, 58, 46, bold, 13, EdTealWords, TextAnchor.MiddleLeft, "DONE");
        }

        private static void Dot10(RectTransform parent, float x, float y, Color colour)
        {
            Image dot = Rect10(parent, "Dot", x, y, 8, 8).gameObject.AddComponent<Image>();
            dot.sprite = circle;
            dot.color = colour;
            dot.raycastTarget = false;
        }

        // ---------- parts, in board pixels from the parent's top left (x1.5) ----------

        private static RectTransform Rect10(Transform parent, string name, float x, float y, float width, float height) =>
            TopLeft(name, (RectTransform)parent, x, y, width, height);

        private static RectTransform Box10(Transform parent, string name, float x, float y, float width, float height, Color fill,
            Color? edge)
        {
            RectTransform rect = Rect10(parent, name, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = fill;
            if (!edge.HasValue) return rect;
            if (fill.a < 0.99f && frame != null)
            {
                // An Outline copies the whole shape: on a see-through fill the edge is a frame picture instead.
                RectTransform edgeRect = Rect("Edge", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                    Vector2.zero);
                Image edgeImage = edgeRect.gameObject.AddComponent<Image>();
                edgeImage.sprite = frame;
                edgeImage.type = Image.Type.Sliced;
                edgeImage.color = edge.Value;
                edgeImage.raycastTarget = false;
                edgeImage.pixelsPerUnitMultiplier = 2f;
                return rect;
            }

            Edges(rect, edge.Value);
            return rect;
        }

        private static Outline[] Edges(RectTransform rect, Color edge)
        {
            Outline a = rect.gameObject.AddComponent<Outline>();
            a.effectColor = edge;
            a.effectDistance = new Vector2(1.5f, -1.5f);
            a.useGraphicAlpha = false;
            Outline b = rect.gameObject.AddComponent<Outline>();
            b.effectColor = edge;
            b.effectDistance = new Vector2(-1.5f, 1.5f);
            b.useGraphicAlpha = false;
            return new[] { a, b };
        }

        private static Text Txt(Transform parent, string name, float x, float y, float width, float height, Font font, int size,
            Color colour, TextAnchor anchor, string words)
        {
            Text text = Rect10(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = Mathf.RoundToInt(size * P);
            text.color = colour;
            text.alignment = anchor;
            text.text = words;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Line10(Transform parent, string name, float x, float y, float width, float height, Color colour)
        {
            Image image = Rect10(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
        }

        private static Button Btn(Transform parent, string name, float x, float y, float width, float height, string words, int size) =>
            Btn(parent, name, x, y, width, height, words, size, EdBar, Edge, Words, out _);

        private static Button Btn(Transform parent, string name, float x, float y, float width, float height, string words, int size,
            Color fill, Color edge, Color wordsColour) =>
            Btn(parent, name, x, y, width, height, words, size, fill, edge, wordsColour, out _);

        private static Button Btn(Transform parent, string name, float x, float y, float width, float height, string words, int size,
            Color fill, Color edge, Color wordsColour, out Text label)
        {
            RectTransform rect = Box10(parent, name, x, y, width, height, fill, edge);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            SetDisabledTint(button);
            label = Txt(rect, "Label", 0, 0, width, height, bold, size, wordsColour, TextAnchor.MiddleCenter, words);
            return button;
        }

        private static InputField Field10(Transform parent, string name, float x, float y, float width, float height, string hint,
            int size, bool lines)
        {
            RectTransform rect = Box10(parent, name, x, y, width, height, EdBar, EdRowEdge);
            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = rect.GetComponent<Image>();
            Text typed = Txt(rect, "Text", 10, lines ? 6 : 0, width - 20, lines ? height - 12 : height, medium, size, Words,
                lines ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, string.Empty);
            typed.supportRichText = false;
            typed.horizontalOverflow = lines ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            typed.verticalOverflow = lines ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            Text placeholder = Txt(rect, "Placeholder", 10, lines ? 6 : 0, width - 20, lines ? height - 12 : height, medium, size,
                EdFaint, lines ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, hint);
            input.textComponent = typed;
            input.placeholder = placeholder;
            input.lineType = lines ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            return input;
        }

        // A list that scrolls: the rows stack from the top with this gap between them.
        private static ScrollRect Scroll10(Transform parent, string name, float x, float y, float width, float height, float gap,
            out RectTransform content)
        {
            RectTransform viewport = Rect10(parent, name, x, y, width, height);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                Vector2.zero);
            VerticalLayoutGroup column = Column(content.gameObject, 0, 0, 0, 0, gap * P);
            column.childForceExpandWidth = true;
            Fit(content.gameObject, false, true);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }
    }
}
