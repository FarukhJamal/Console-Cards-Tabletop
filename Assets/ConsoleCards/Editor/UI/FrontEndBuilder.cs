using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// UI-2 (owner, 2026-10-24: "the Milanote board shows the flow"): builds the front end prefab, the screens of the
    /// user journey with the owner's Figma exports: Main menu (Top_Console bar, Menu entries), Player profile (Player
    /// Profile frame, Profile_entry rows, Avatar frame), Game library (Game Entry rows), Game details, Game setup,
    /// Players and Review (step 1-2-3) and START GAME. Run by Console Cards > UI > Apply Kit Look (UI-1): the prefab is
    /// built once (then it is yours to adjust); each run only refreshes the background picture
    /// (Content/UI/Figma/Components/Background.png when it is there).
    /// </summary>
    public static class FrontEndBuilder
    {
        public const string PrefabPath = UiKit.FinalUiFolder + "FrontEnd.prefab";
        public const string BackgroundName = "Background";

        private static readonly Color White = UiKit.Hex("e8eced");
        private static readonly Color Muted = UiKit.Hex("9ba4a7");
        private static readonly Color Cyan = UiKit.Hex("06eff0");
        private static readonly Color Dark = UiKit.Hex("16191a");
        private static readonly Color Backdrop = UiKit.Hex("101314");
        private static readonly Color TitleShadow = new Color(0.55f, 0.2f, 0.05f, 0.9f);

        // Export -> 9-slice border (left, bottom, right, top) in export pixels.
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "Top_Console", new Vector4(70f, 0f, 70f, 0f) },
            { "Menu", new Vector4(70f, 0f, 70f, 0f) },
            { "Menu_Selected", new Vector4(70f, 0f, 70f, 0f) },
            { "Game Entry", new Vector4(70f, 0f, 70f, 0f) },
            { "Selected Game Entry", new Vector4(70f, 0f, 70f, 0f) },
            { "Avatar", new Vector4(62f, 66f, 62f, 66f) },
            { "Profile_entry", new Vector4(10f, 10f, 10f, 10f) },
            { "Selected_Profile_entry", new Vector4(10f, 10f, 10f, 10f) },
            { "Button_disabled", new Vector4(16f, 16f, 16f, 16f) },
            { "Square", new Vector4(16f, 16f, 16f, 16f) },
            { "Highlighter", new Vector4(6f, 0f, 6f, 0f) },
            { "editIcon", Vector4.zero },
            { "addIcon", Vector4.zero },
            { "playersIcon", Vector4.zero },
            { "OrangeAvatarImage", Vector4.zero },
            { BackgroundName, Vector4.zero },
        };

        private static Font bold;
        private static Font medium;

        public static void Prepare()
        {
            foreach (KeyValuePair<string, Vector4> sprite in Borders)
            {
                string path = KitFrames.FigmaPath + sprite.Key + ".png";
                if (File.Exists(path)) UiKit.ConfigureTexture(path, sprite.Value);
            }
        }

        /// <summary>Builds the prefab when it is missing, refreshes the background, points the catalog at it.</summary>
        public static int BuildOrRefresh()
        {
            if (Figma("Top_Console") == null || Figma("Menu") == null) return 0; // the exports are not in the project
            bold = UiKit.RequireFont("Oxanium-Bold.ttf");
            medium = UiKit.RequireFont("Oxanium-Medium.ttf");
            int changes = 0;
            if (!File.Exists(PrefabPath))
            {
                Build();
                changes++;
            }

            changes += RefreshBackground();
            FrontEndView view = AssetDatabase.LoadAssetAtPath<FrontEndView>(PrefabPath);
            if (view != null)
            {
                UiKit.AssignCatalogEntry(UiPrefabIds.FrontEnd, view, UiLayer.Modal, UiRetention.CachedSingleInstance);
            }

            return changes;
        }

        private static int RefreshBackground()
        {
            Sprite picture = Figma(BackgroundName);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform background = root.transform.Find("Background");
                Image image = background != null ? background.GetComponent<Image>() : null;
                if (image == null || image.sprite == picture) return 0;
                image.sprite = picture;
                image.color = picture != null ? Color.white : Backdrop;
                AspectRatioFitter fitter = background.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                {
                    fitter.aspectMode = picture != null
                        ? AspectRatioFitter.AspectMode.EnvelopeParent
                        : AspectRatioFitter.AspectMode.None;
                    if (picture != null) fitter.aspectRatio = picture.rect.width / picture.rect.height;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Build()
        {
            GameObject go = new GameObject("FrontEnd", typeof(RectTransform));
            go.layer = 5;
            try
            {
                RectTransform root = (RectTransform)go.transform;
                UiKit.Stretch(root);
                FrontEndView view = go.AddComponent<FrontEndView>();

                RectTransform background = Stretched("Background", root);
                Image backgroundImage = background.gameObject.AddComponent<Image>();
                backgroundImage.color = Backdrop;
                backgroundImage.raycastTarget = true; // the table behind does not take clicks
                AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.None;

                TopBar(root);
                RectTransform pages = Stretched("Pages", root);
                Stepper(pages);
                Home(pages);
                Create(pages);
                Profile(pages);
                Library(pages);
                Details(pages);
                Setup(pages);
                Players(pages);
                Review(pages);

                RectTransform status = Node("Status", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 14f), new Vector2(-200f, 30f));
                Words(status, medium, 15, Muted, TextAnchor.MiddleCenter, string.Empty);

                SerializedObject so = new SerializedObject(view);
                so.FindProperty("gameEntry").objectReferenceValue = Figma("Game Entry");
                so.FindProperty("selectedGameEntry").objectReferenceValue = Figma("Selected Game Entry");
                so.FindProperty("profileEntry").objectReferenceValue = Figma("Profile_entry");
                so.FindProperty("selectedProfileEntry").objectReferenceValue = Figma("Selected_Profile_entry");
                so.FindProperty("fallbackAvatar").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(KitFrames.FigmaPath + "OrangeAvatarImage.png");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- Screens ----------

        // CONSOLE CARDS | PLAY  CREATE  SANDBOX | GUIDE  TABLE  EXIT on the Top_Console bar.
        private static void TopBar(RectTransform root)
        {
            RectTransform bar = Node("TopBar", root, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(-56f, 87f));
            Plate(bar, Figma("Top_Console"), 130f / 87f);

            RectTransform brand = Node("Brand", bar, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(62f, 0f), new Vector2(360f, 0f));
            Row(brand, 12f, TextAnchor.MiddleLeft);
            Picture(Node("Dot", brand), Figma("cyan_on ellipse"), Color.white, 16f, 16f);
            Fit(Words(Node("Label", brand), bold, 22, White, TextAnchor.MiddleLeft, "CONSOLE CARDS"));

            RectTransform tabs = Node("Tabs", bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(520f, 0f));
            Row(tabs, 26f, TextAnchor.MiddleCenter);
            TabButton(tabs, "Play", "PLAY");
            Rule(tabs, "Rule1");
            TabButton(tabs, "Create", "CREATE");
            Rule(tabs, "Rule2");
            TabButton(tabs, "Sandbox", "SANDBOX");
            RectTransform highlight = Node("Highlight", tabs, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(44f, 6f));
            highlight.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Plate(highlight, Figma("Highlighter"), 1.5f).raycastTarget = false;

            RectTransform right = Node("Right", bar, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f),
                new Vector2(-62f, 0f), new Vector2(480f, 0f));
            Row(right, 30f, TextAnchor.MiddleRight);
            IconWord(right, "Guide", Figma("ShopIcon"), "GUIDE");
            IconWord(right, "Table", Figma("TableIcon"), "TABLE");
            IconWord(right, "Exit", Figma("RightArrowIcon"), "EXIT");
        }

        // Step 1 Game Setup, 2 Players, 3 Review (shown on the three setup screens).
        private static void Stepper(RectTransform pages)
        {
            RectTransform stepper = Node("Stepper", pages, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -124f), new Vector2(640f, 52f));
            Plate(stepper, Figma("Frame_move"), 1.5f);
            Row(stepper, 34f, TextAnchor.MiddleCenter);
            string[] labels = { "Game Setup", "Players", "Review" };
            for (int i = 0; i < 3; i++)
            {
                RectTransform step = Node("Step" + i, stepper);
                Row(step, 10f, TextAnchor.MiddleCenter);
                Fit(Words(Node("Number", step), bold, 20, White, TextAnchor.MiddleCenter, (i + 1).ToString()));
                Fit(Words(Node("Label", step), medium, 17, White, TextAnchor.MiddleLeft, labels[i]));
            }

            stepper.gameObject.SetActive(false);
        }

        private static void Home(RectTransform pages)
        {
            RectTransform page = Page(pages, "Home");
            BigTitle(page, "CONSOLE CARDS", "V I R T U A L   P L A T F O R M", 180f);
            Words(TopLeft("Tagline", page, 100f, 340f, 760f, 60f), medium, 18, White, TextAnchor.UpperLeft,
                "PLAY, CREATE, AND EXPLORE\nTABLETOP GAMES IN A DIGITAL SPACE.");
            RectTransform menu = MenuColumn(page, 430f);
            MenuEntry(menu, "Play", "PLAY", "Choose a game, set up a session, and start playing.", Derived("V5_Bullet"));
            MenuEntry(menu, "Create", "CREATE", "Design and customize your own games and content.", Figma("editIcon"));
            MenuEntry(menu, "Sandbox", "SANDBOX", "Open an empty table and experiment freely.", Figma("addIcon"));
        }

        private static void Create(RectTransform pages)
        {
            RectTransform page = Page(pages, "Create");
            BigTitle(page, "CREATE", "M A K E   T H E   G A M E S   Y O U R S", 180f);
            RectTransform menu = MenuColumn(page, 360f);
            MenuEntry(menu, "CardArt", "CARD ART", "Your own pictures on the cards.", Figma("editIcon"));
            MenuEntry(menu, "CardEditor", "CARD EDITOR", "Change the cards and add new ones.", Figma("editIcon"));
            MenuEntry(menu, "DeckEditor", "DECK EDITOR", "Build the decks you play with.", Figma("addIcon"));
            SecondaryButton(page, "Back", 96f, 880f, 300f, "BACK");
        }

        private static void Profile(RectTransform pages)
        {
            RectTransform page = Page(pages, "Profile");
            BigTitle(page, "CONSOLE CARDS", "V I R T U A L   P L A T F O R M", 150f);

            RectTransform panel = TopLeft("Panel", page, 96f, 290f, 620f, 560f);
            Plate(panel, Figma("Player Profile"), 2f);
            Words(TopLeft("Title", panel, 30f, 26f, 560f, 40f), bold, 30, White, TextAnchor.MiddleLeft, "PLAYER PROFILE");
            Words(TopLeft("Subtitle", panel, 30f, 66f, 560f, 26f), medium, 16, Muted, TextAnchor.MiddleLeft,
                "Set up your local player before choosing a game.");
            RectTransform rows = TopLeft("Rows", panel, 24f, 104f, 572f, 4f * 92f + 3f * 12f);
            Column(rows, 12f);
            for (int i = 0; i < FrontEndView.ProfileRowCount; i++)
            {
                RectTransform row = Node("Row" + i, rows);
                Size(row, -1f, 92f);
                Image plate = Plate(row, Figma("Profile_entry"), 1.5f);
                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = plate;
                RawImage avatar = TopLeft("Avatar", row, 16f, 16f, 60f, 60f).gameObject.AddComponent<RawImage>();
                avatar.color = Cyan;
                avatar.raycastTarget = false;
                RectTransform words = TopLeft("Words", row, 92f, 14f, 400f, 64f);
                Column(words, 2f);
                Words(Node("Small", words), bold, 13, Cyan, TextAnchor.MiddleLeft, "PLAYER 1");
                Words(Node("Name", words), bold, 22, White, TextAnchor.MiddleLeft, "ALEX");
                RectTransform edit = Node("Edit", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-22f, 0f), new Vector2(26f, 26f));
                Picture(edit, Figma("editIcon"), White, -1f, -1f);
            }

            RectTransform add = TopLeft("Add", panel, 24f, 104f + 4f * 104f, 572f, 30f);
            Words(add, bold, 15, Cyan, TextAnchor.MiddleLeft, "+  NEW OR RENAMED PROFILE");
            add.GetComponent<Text>().raycastTarget = true;
            Hover(add.gameObject.AddComponent<Button>(), add.GetComponent<Text>());

            RectTransform avatarPanel = TopLeft("Avatar", page, 740f, 290f, 360f, 560f);
            Plate(avatarPanel, Figma("Avatar"), 2f);
            Words(TopLeft("Name", avatarPanel, 20f, 24f, 320f, 36f), bold, 26, White, TextAnchor.MiddleCenter, "ALEX");
            RawImage picture = TopLeft("Picture", avatarPanel, 30f, 72f, 300f, 380f).gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            picture.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KitFrames.FigmaPath + "OrangeAvatarImage.png");
            Words(TopLeft("AvatarName", avatarPanel, 20f, 458f, 320f, 26f), medium, 16, Muted, TextAnchor.MiddleCenter, string.Empty);
            ArrowButton(TopLeft("Previous", avatarPanel, 40f, 498f, 36f, 36f), Figma("leftArrowIcon"));
            ArrowButton(TopLeft("Next", avatarPanel, 284f, 498f, 36f, 36f), Figma("RightArrowIcon"));
            ChipButton(TopLeft("Customize", avatarPanel, 112f, 494f, 136f, 44f), "CUSTOMIZE");

            SecondaryButton(page, "Back", 96f, 880f, 300f, "BACK");
            PrimaryButton(page, "Continue", 412f, 880f, 420f, "CONTINUE");
        }

        private static void Library(RectTransform pages)
        {
            RectTransform page = Page(pages, "Library");
            BigTitle(page, "GAME LIBRARY", "C H O O S E   A   G A M E   T O   P L A Y", 150f);

            RectTransform list = TopLeft("List", page, 96f, 300f, 640f, 560f);
            ScrollRect scroll = list.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Stretched("Viewport", list);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportHit = viewport.gameObject.AddComponent<Image>();
            viewportHit.color = Color.clear;
            RectTransform content = Node("Content", viewport, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            Column(content, 14f);
            ContentSizeFitter contentFit = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform template = Node("Template", content);
            Size(template, -1f, 180f);
            Image plate = Plate(template, Figma("Game Entry"), 355f / 180f);
            template.gameObject.AddComponent<Button>().targetGraphic = plate;
            RawImage thumb = TopLeft("Picture", template, 58f, 20f, 200f, 140f).gameObject.AddComponent<RawImage>();
            thumb.raycastTarget = false;
            RectTransform words = TopLeft("Words", template, 280f, 34f, 300f, 112f);
            Column(words, 6f);
            Words(Node("Name", words), bold, 26, White, TextAnchor.UpperLeft, "TRAP FLOOR");
            Words(Node("Detail", words), medium, 16, Muted, TextAnchor.UpperLeft, "2–4 players · 3 modes");

            RectTransform detail = TopLeft("Detail", page, 760f, 300f, 600f, 560f);
            Plate(detail, Figma("Player Profile"), 2f);
            Words(TopLeft("Name", detail, 30f, 24f, 540f, 40f), bold, 30, White, TextAnchor.MiddleLeft, "TRAP FLOOR");
            RawImage picture = TopLeft("Picture", detail, 30f, 76f, 540f, 300f).gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            Words(TopLeft("Text", detail, 30f, 392f, 540f, 110f), medium, 16, White, TextAnchor.UpperLeft, string.Empty);
            RectTransform players = TopLeft("PlayersRow", detail, 30f, 508f, 540f, 30f);
            Row(players, 10f, TextAnchor.MiddleLeft);
            Picture(Node("Icon", players), Figma("playersIcon"), Cyan, 22f, 22f);
            Fit(Words(Node("Players", players), bold, 16, Cyan, TextAnchor.MiddleLeft, "2–4 players"));

            SecondaryButton(page, "Back", 96f, 880f, 300f, "BACK");
            PrimaryButton(page, "Select", 412f, 880f, 948f, "SELECT TRAP FLOOR");
        }

        private static void Details(RectTransform pages)
        {
            RectTransform page = Page(pages, "Details");
            ChipButton(TopLeft("Back", page, 96f, 128f, 280f, 44f), "<  BACK TO LIBRARY");
            Words(TopLeft("Title", page, 96f, 186f, 1000f, 110f), bold, 96, White, TextAnchor.MiddleLeft, "TRAP FLOOR")
                .gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -6f);
            Words(TopLeft("Tagline", page, 100f, 298f, 800f, 30f), medium, 20, White, TextAnchor.MiddleLeft, string.Empty);
            Words(TopLeft("Text", page, 100f, 344f, 760f, 150f), medium, 18, White, TextAnchor.UpperLeft, string.Empty);
            RectTransform chips = TopLeft("Chips", page, 100f, 500f, 760f, 44f);
            Row(chips, 12f, TextAnchor.MiddleLeft);
            InfoChip(chips, "Players", Figma("playersIcon"));
            InfoChip(chips, "Modes", Figma("TableIcon"));
            RectTransform frame = TopLeft("PictureFrame", page, 96f, 566f, 780f, 300f);
            Plate(frame, Figma("Player Profile"), 2f);
            RawImage picture = TopLeft("Picture", page, 116f, 586f, 740f, 260f).gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;

            RectTransform box = Node("Box", page, Vector2.one, Vector2.one, Vector2.one, new Vector2(-60f, -150f),
                new Vector2(400f, 340f));
            Plate(box, Figma("Player Profile"), 2f);
            Words(TopLeft("Title", box, 28f, 24f, 340f, 34f), bold, 22, White, TextAnchor.MiddleLeft, "IN THE BOX");
            Words(TopLeft("Text", box, 28f, 70f, 344f, 240f), medium, 16, White, TextAnchor.UpperLeft, string.Empty);

            PrimaryButton(page, "Start", 96f, 890f, 780f, "START SETUP");
        }

        private static void Setup(RectTransform pages)
        {
            RectTransform page = Page(pages, "Setup");
            ChipButton(TopLeft("Back", page, 96f, 128f, 280f, 44f), "<  BACK TO DETAILS");
            StepTitle(page, "G A M E   C O N F I G U R A T I O N");
            RectTransform options = TopLeft("Options", page, 96f, 330f, 780f, 520f);
            Plate(options, Figma("Player Profile"), 2f);
            RectTransform column = TopLeft("Column", options, 26f, 26f, 728f, 468f);
            Column(column, 10f);
            OptionRow(options, column, "Mode", "GAME MODE", "Choose a mode for your session.");
            OptionRow(options, column, "Players", "PLAYER COUNT", "Number of local players.");
            OptionRow(options, column, "WinMode", "WIN MODE", "How the game is won.");
            OptionRow(options, column, "Turns", "TURNS", "Which way the turns go round.");
            OptionRow(options, column, "Deck", "DECK SET", "Choose the cards to use.");

            RectTransform summary = Node("Summary", page, Vector2.one, Vector2.one, Vector2.one, new Vector2(-60f, -150f),
                new Vector2(400f, 300f));
            Plate(summary, Figma("Player Profile"), 2f);
            Words(TopLeft("Title", summary, 28f, 24f, 340f, 34f), bold, 22, White, TextAnchor.MiddleLeft, "SESSION SUMMARY");
            Words(TopLeft("Text", summary, 28f, 70f, 344f, 210f), medium, 17, White, TextAnchor.UpperLeft, string.Empty);

            PrimaryButton(page, "Continue", 96f, 880f, 780f, "CONTINUE");
        }

        private static void Players(RectTransform pages)
        {
            RectTransform page = Page(pages, "Players");
            ChipButton(TopLeft("Back", page, 96f, 128f, 280f, 44f), "<  BACK TO SETUP");
            StepTitle(page, "P L A Y E R   S E T U P");
            Words(TopLeft("Text", page, 100f, 300f, 760f, 30f), medium, 17, White, TextAnchor.MiddleLeft,
                "Each seat plays in its colour. Player 1 is your profile.");
            RectTransform rows = TopLeft("Rows", page, 96f, 344f, 780f, 6f * 74f + 5f * 10f);
            Column(rows, 10f);
            for (int i = 0; i < FrontEndView.PlayerRowCount; i++)
            {
                RectTransform row = Node("Row" + i, rows);
                Size(row, -1f, 74f);
                Plate(row, Figma("Menu"), 213f / 74f).raycastTarget = false;
                RectTransform badge = TopLeft("Badge", row, 44f, 15f, 52f, 44f);
                Plate(badge, Figma("Square"), 2f).raycastTarget = false;
                Words(Stretched("Label", badge), bold, 17, Dark, TextAnchor.MiddleCenter, "P" + (i + 1));
                RawImage avatar = TopLeft("Avatar", row, 108f, 9f, 56f, 56f).gameObject.AddComponent<RawImage>();
                avatar.raycastTarget = false;
                RectTransform words = TopLeft("Words", row, 180f, 10f, 440f, 54f);
                Column(words, 2f);
                Words(Node("Name", words), bold, 20, White, TextAnchor.MiddleLeft, "Player " + (i + 1));
                Words(Node("Controller", words), medium, 14, Muted, TextAnchor.MiddleLeft, "Controller: BLUE");
                Picture(Node("Dot", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-60f, 0f), new Vector2(22f, 22f)), Derived("V5_DotPlain"), Color.white, -1f, -1f);
            }

            ChipButton(TopLeft("Add", page, 900f, 344f, 240f, 48f), "+  ADD PLAYER");
            ChipButton(TopLeft("Remove", page, 900f, 404f, 240f, 48f), "−  REMOVE PLAYER");

            RectTransform info = Node("Info", page, Vector2.one, Vector2.one, Vector2.one, new Vector2(-60f, -150f),
                new Vector2(400f, 220f));
            Plate(info, Figma("Player Profile"), 2f);
            Words(TopLeft("Title", info, 28f, 24f, 340f, 34f), bold, 22, White, TextAnchor.MiddleLeft, "PLAYER CONTROLS");
            Words(TopLeft("Text", info, 28f, 70f, 344f, 130f), medium, 16, White, TextAnchor.UpperLeft,
                "Each player uses a Console Controller to interact with cards and the table.");

            PrimaryButton(page, "Continue", 96f, 900f, 780f, "CONTINUE");
        }

        private static void Review(RectTransform pages)
        {
            RectTransform page = Page(pages, "Review");
            ChipButton(TopLeft("Back", page, 96f, 128f, 280f, 44f), "<  BACK TO PLAYERS");
            StepTitle(page, "R E V I E W   S E T U P");
            Words(TopLeft("Text", page, 100f, 300f, 760f, 30f), medium, 17, White, TextAnchor.MiddleLeft,
                "Check your game settings and players. You can go back and change anything.");

            RectTransform settings = TopLeft("Settings", page, 96f, 344f, 780f, 220f);
            Plate(settings, Figma("Player Profile"), 2f);
            Words(TopLeft("Title", settings, 28f, 22f, 500f, 34f), bold, 22, White, TextAnchor.MiddleLeft, "GAME SETTINGS");
            ChipButton(Node("Edit", settings, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24f, -18f),
                new Vector2(110f, 40f)), "EDIT  >");
            Words(TopLeft("Text", settings, 28f, 66f, 724f, 140f), medium, 17, White, TextAnchor.UpperLeft, string.Empty);

            RectTransform players = TopLeft("Players", page, 96f, 584f, 780f, 250f);
            Plate(players, Figma("Player Profile"), 2f);
            Words(TopLeft("Title", players, 28f, 22f, 500f, 34f), bold, 22, White, TextAnchor.MiddleLeft, "PLAYERS");
            ChipButton(Node("Edit", players, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24f, -18f),
                new Vector2(110f, 40f)), "EDIT  >");
            RectTransform tiles = TopLeft("Tiles", players, 28f, 66f, 724f, 164f);
            Row(tiles, 12f, TextAnchor.UpperLeft);
            for (int i = 0; i < FrontEndView.PlayerRowCount; i++)
            {
                RectTransform tile = Node("Tile" + i, tiles);
                Size(tile, 110f, 164f);
                RectTransform badge = TopLeft("Badge", tile, 0f, 0f, 40f, 24f);
                Plate(badge, Figma("Square"), 3f).raycastTarget = false;
                Words(Stretched("Label", badge), bold, 13, Dark, TextAnchor.MiddleCenter, "P" + (i + 1));
                RawImage avatar = TopLeft("Avatar", tile, 10f, 28f, 90f, 90f).gameObject.AddComponent<RawImage>();
                avatar.raycastTarget = false;
                Words(TopLeft("Name", tile, 0f, 122f, 110f, 22f), bold, 14, White, TextAnchor.MiddleCenter, "Player " + (i + 1));
                Words(TopLeft("Controller", tile, 0f, 142f, 110f, 20f), medium, 12, Muted, TextAnchor.MiddleCenter, "BLUE");
            }

            PrimaryButton(page, "Start", 96f, 856f, 780f, "START GAME");
        }

        // ---------- Pieces ----------

        private static RectTransform Page(RectTransform pages, string name)
        {
            RectTransform page = Stretched(name, pages);
            page.gameObject.SetActive(name == "Home");
            return page;
        }

        private static void BigTitle(RectTransform page, string title, string subtitle, float top)
        {
            Text big = Words(TopLeft("Title", page, 96f, top, 1100f, 110f), bold, 96, White, TextAnchor.MiddleLeft, title);
            big.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -6f);
            big.GetComponent<Shadow>().effectColor = TitleShadow;
            Words(TopLeft("Subtitle", page, 100f, top + 108f, 1000f, 30f), medium, 20, White, TextAnchor.MiddleLeft, subtitle);
        }

        private static void StepTitle(RectTransform page, string subtitle)
        {
            Text title = Words(TopLeft("Title", page, 96f, 190f, 1000f, 84f), bold, 72, White, TextAnchor.MiddleLeft, "TRAP FLOOR");
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -5f);
            title.GetComponent<Shadow>().effectColor = TitleShadow;
            Words(TopLeft("Subtitle", page, 100f, 268f, 1000f, 28f), medium, 18, White, TextAnchor.MiddleLeft, subtitle);
        }

        private static RectTransform MenuColumn(RectTransform page, float top)
        {
            RectTransform menu = TopLeft("Menu", page, 96f, top, 660f, 3f * 132f + 2f * 14f);
            Column(menu, 14f);
            return menu;
        }

        // A Menu entry: orange caps, a cyan icon box, title and line, a chevron; it lights (Menu_Selected) on hover.
        private static void MenuEntry(RectTransform menu, string name, string title, string line, Sprite icon)
        {
            RectTransform entry = Node(name, menu);
            Size(entry, -1f, 132f);
            Image plate = Plate(entry, Figma("Menu"), 213f / 132f);
            Button button = entry.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState states = button.spriteState;
            states.highlightedSprite = Figma("Menu_Selected");
            states.pressedSprite = Figma("Menu_Selected");
            button.spriteState = states;
            RectTransform box = TopLeft("Icon", entry, 62f, 34f, 64f, 64f);
            Plate(box, Figma("Square"), 2f).raycastTarget = false;
            Picture(Node("Glyph", box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(30f, 30f)), icon, Dark, -1f, -1f);
            Words(TopLeft("Title", entry, 150f, 30f, 420f, 38f), bold, 30, White, TextAnchor.MiddleLeft, title);
            Words(TopLeft("Line", entry, 150f, 70f, 420f, 34f), medium, 16, White, TextAnchor.UpperLeft, line);
            Picture(Node("Chevron", entry, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-62f, 0f), new Vector2(26f, 26f)), Figma("RightArrowIcon"), White, -1f, -1f);
        }

        // One setup row: title and hint at the left, "< value >" at the right.
        private static void OptionRow(RectTransform options, RectTransform column, string name, string title, string hint)
        {
            RectTransform row = Node(name, column);
            Size(row, -1f, 84f);
            Plate(row, Figma("Profile_entry"), 1.5f).raycastTarget = false;
            Words(TopLeft("Title", row, 22f, 14f, 340f, 28f), bold, 19, White, TextAnchor.MiddleLeft, title);
            Words(TopLeft("Hint", row, 22f, 44f, 340f, 24f), medium, 14, Muted, TextAnchor.MiddleLeft, hint);
            RectTransform picker = Node("Picker", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f), new Vector2(340f, 54f));
            Plate(picker, Figma("Frame_move"), 1.5f).raycastTarget = false;
            ArrowButton(Node("Previous", picker, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(12f, 0f), new Vector2(28f, 28f)), Figma("leftArrowIcon"));
            Text value = Words(Node("Value", picker, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-96f, -6f)), bold, 15, White, TextAnchor.MiddleCenter, "Easy");
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 11;
            value.resizeTextMaxSize = 16;
            ArrowButton(Node("Next", picker, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(28f, 28f)), Figma("RightArrowIcon"));
        }

        private static void InfoChip(RectTransform parent, string name, Sprite icon)
        {
            RectTransform chip = Node(name, parent);
            Plate(chip, Figma("Frame_move"), 1.5f).raycastTarget = false;
            HorizontalLayoutGroup row = Row(chip, 10f, TextAnchor.MiddleCenter);
            row.padding = new RectOffset(14, 16, 0, 0);
            Size(chip, -1f, 44f);
            Picture(Node("Icon", chip), icon, Cyan, 22f, 22f);
            Fit(Words(Node("Label", chip), bold, 16, White, TextAnchor.MiddleLeft, "2–4 players"));
        }

        private static void TabButton(RectTransform tabs, string name, string word)
        {
            RectTransform tab = Node(name, tabs);
            Text label = Words(Stretched("Label", tab), bold, 17, White, TextAnchor.MiddleCenter, word);
            label.raycastTarget = true;
            Size(tab, word.Length * 13f + 12f, 40f);
            Hover(tab.gameObject.AddComponent<Button>(), label);
        }

        private static void IconWord(RectTransform parent, string name, Sprite icon, string word)
        {
            RectTransform item = Node(name, parent);
            Row(item, 8f, TextAnchor.MiddleCenter);
            Picture(Node("Icon", item), icon, White, 20f, 20f);
            Text label = Fit(Words(Node("Label", item), bold, 15, White, TextAnchor.MiddleLeft, word));
            label.raycastTarget = true;
            Hover(item.gameObject.AddComponent<Button>(), label);
        }

        private static void Rule(RectTransform parent, string name)
        {
            Picture(Node(name, parent), Figma("Line"), Muted, 2f, 26f);
        }

        private static void PrimaryButton(RectTransform page, string name, float x, float y, float width, string word)
        {
            RectTransform button = TopLeft(name, page, x, y, width, 74f);
            Image plate = Plate(button, Figma("Button_Enabled"), 1.5f);
            Button press = button.gameObject.AddComponent<Button>();
            press.targetGraphic = plate;
            Row(button, 14f, TextAnchor.MiddleCenter);
            Picture(Node("Dot", button), Derived("V5_DotPlain"), Cyan, 18f, 18f);
            Fit(Words(Node("Label", button), bold, 24, White, TextAnchor.MiddleCenter, word));
        }

        private static void SecondaryButton(RectTransform page, string name, float x, float y, float width, string word)
        {
            RectTransform button = TopLeft(name, page, x, y, width, 74f);
            Image plate = Plate(button, Figma("Button_disabled"), 1.5f);
            Button press = button.gameObject.AddComponent<Button>();
            press.targetGraphic = plate;
            Row(button, 12f, TextAnchor.MiddleCenter);
            Picture(Node("Arrow", button), Figma("leftArrowIcon"), White, 20f, 20f);
            Fit(Words(Node("Label", button), bold, 22, White, TextAnchor.MiddleCenter, word));
        }

        private static void ChipButton(RectTransform rect, string word)
        {
            Image plate = Plate(rect, Figma("Frame_move"), 1.5f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            Words(Stretched("Label", rect), bold, 15, White, TextAnchor.MiddleCenter, word);
        }

        private static void ArrowButton(RectTransform rect, Sprite icon)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.color = White;
            Hover(rect.gameObject.AddComponent<Button>(), image);
        }

        private static void Hover(Button button, Graphic target)
        {
            button.targetGraphic = target;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Cyan;
            colors.pressedColor = new Color(Cyan.r * 0.8f, Cyan.g * 0.8f, Cyan.b * 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
            button.colors = colors;
        }

        // ---------- Primitives ----------

        private static RectTransform Node(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            RectTransform rect = Node(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform TopLeft(string name, Transform parent, float x, float y, float width, float height)
        {
            return Node(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y),
                new Vector2(width, height));
        }

        private static RectTransform Stretched(string name, Transform parent)
        {
            RectTransform rect = Node(name, parent);
            UiKit.Stretch(rect);
            return rect;
        }

        private static Image Plate(RectTransform rect, Sprite sprite, float exportScale)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = exportScale;
            image.color = Color.white;
            return image;
        }

        private static void Picture(RectTransform rect, Sprite sprite, Color color, float width, float height)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            if (width > 0f) Size(rect, width, height);
        }

        private static Text Words(RectTransform rect, Font font, int size, Color color, TextAnchor alignment, string text)
        {
            Text label = UiKit.Label(rect, font, size, color, alignment, text);
            return label;
        }

        private static Text Fit(Text text)
        {
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static HorizontalLayoutGroup Row(RectTransform rect, float spacing, TextAnchor alignment)
        {
            HorizontalLayoutGroup row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = alignment;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return row;
        }

        private static void Column(RectTransform rect, float spacing)
        {
            VerticalLayoutGroup column = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = spacing;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
        }

        private static void Size(RectTransform rect, float width, float height)
        {
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

        private static Sprite Figma(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(KitFrames.FigmaPath + name + ".png");

        private static Sprite Derived(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(V5Hud.DerivedPath + name + ".png");
    }
}
