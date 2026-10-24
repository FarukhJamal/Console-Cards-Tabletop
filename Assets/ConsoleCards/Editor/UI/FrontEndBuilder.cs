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
    /// UI-2 (owner, 2026-10-24: the Milanote user journey; second pass "notice the difference in placement, font size,
    /// asset usage"): builds the front end prefab as the board's screens, measured from the mock screens (1920x1080):
    /// the brand pill and the Top_Console bar (GUIDE, SETTINGS, TABLE, EXIT with icons); Main menu (Menu entries with
    /// glyphs, lit only under the pointer); Player profile (orange frame, the active profile expanded in a cyan frame with its name box
    /// and CUSTOMIZE AVATAR, the other profiles as Menu rows, the Avatar frame with arrows and page dots); Game library
    /// (Game Entry rows, the chosen one in cyan, detail panel with picture, text and a stats strip); Game details (back
    /// chip, title, tagline, stats pill, picture panel, IN THE BOX, START SETUP); Game setup, Players and Review (step
    /// 1-2-3 pill, game thumbnail, Menu rows with orange pickers, summary panels) and START GAME.
    /// Each screen is its own prefab in Final/UI/FrontEnd/ (FE_TopBar, FE_Stepper, FE_MainMenu, FE_Create,
    /// FE_Profile, FE_Library, FE_Details, FE_Setup, FE_Players, FE_Review), nested in FrontEnd.prefab.
    /// Run by Console Cards > UI > Apply Kit Look (UI-1): a screen is built only when its prefab is missing (delete it
    /// to build it again from the design); each run refreshes the background picture
    /// (Content/UI/Figma/Components/Background.png). "Apply Kit Look Again" rebuilds every screen.
    /// </summary>
    public static class FrontEndBuilder
    {
        public const string PrefabPath = UiKit.FinalUiFolder + "FrontEnd.prefab";
        public const string BackgroundName = "Background";

        // Owner, 2026-10-25: each screen is its own prefab (FrontEnd/FE_*.prefab), nested in FrontEnd.prefab, so its
        // anchors can be edited. Screens are built only when missing (delete one to build it again from the design);
        // FrontEnd.prefab made before the split (version below this) is put together again once.
        private const int Version = 5;
        public const string ScreensFolder = UiKit.FinalUiFolder + "FrontEnd/";

        // Screen prefab file, the object's name in FrontEnd, where it sits, whether it starts shown.
        private static readonly (string File, string Name, bool InPages, bool Shown)[] Screens =
        {
            ("FE_TopBar", "TopBar", false, true),
            ("FE_Stepper", "Stepper", true, false),
            ("FE_MainMenu", "Home", true, true),
            ("FE_Create", "Create", true, false),
            ("FE_Profile", "Profile", true, false),
            ("FE_Library", "Library", true, false),
            ("FE_Details", "Details", true, false),
            ("FE_Setup", "Setup", true, false),
            ("FE_Players", "Players", true, false),
            ("FE_Review", "Review", true, false),
        };
        private const string WhitePlate = "Assets/ConsoleCards/Content/UI/Kit/KitAccent.png";

        private static readonly Color White = UiKit.Hex("e8eced");
        private static readonly Color Muted = UiKit.Hex("9ba4a7");
        private static readonly Color Cyan = UiKit.Hex("06eff0");
        private static readonly Color Dark = UiKit.Hex("16191a");
        private static readonly Color Orange = UiKit.Hex("ff7b2e");
        private static readonly Color Pale = UiKit.Hex("c9ced0");
        private static readonly Color Gold = UiKit.Hex("f2b632");
        private static readonly Color Backdrop = UiKit.Hex("101314");
        private static readonly Color TitleShadow = new Color(0.05f, 0.06f, 0.07f, 0.95f);

        // Export or derived sprite -> 9-slice border (left, bottom, right, top) in its pixels.
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
            { "page elipse", Vector4.zero },
            { "Current_pageEllipse", Vector4.zero },
            { BackgroundName, Vector4.zero },
        };

        private static readonly Dictionary<string, Vector4> DerivedBorders = new Dictionary<string, Vector4>
        {
            { "V5_CyanFrame", new Vector4(70f, 0f, 70f, 0f) },
            { "V5_CyanEntry", new Vector4(70f, 0f, 70f, 0f) },
            { "V5_CyanPanel", new Vector4(62f, 66f, 62f, 66f) },
            { "V5_OrangePill", new Vector4(20f, 20f, 20f, 20f) },
            { "V5_CyanPill", new Vector4(20f, 20f, 20f, 20f) },
            { "V5_OrangeLine", new Vector4(16f, 16f, 16f, 16f) },
            { "V5_CyanLine", new Vector4(16f, 16f, 16f, 16f) },
            { "V5_GreyPill", new Vector4(20f, 20f, 20f, 20f) },
            { "V5_GreyLine", new Vector4(16f, 16f, 16f, 16f) },
        };

        private static readonly string[] Icons =
        {
            "Play", "Cube", "Book", "Gear", "Door", "Person", "Lock", "Clock", "Pad", "Dots", "Bars", "Cards", "Key", "Pawn",
            "Diamond", "Plus", "Ring", "Disc", "Mode", "Tile", "Console",
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

            foreach (KeyValuePair<string, Vector4> sprite in DerivedBorders)
            {
                string path = V5Hud.DerivedPath + sprite.Key + ".png";
                if (File.Exists(path)) UiKit.ConfigureTexture(path, sprite.Value);
            }

            foreach (string icon in Icons)
            {
                string path = V5Hud.DerivedPath + "V5_I_" + icon + ".png";
                if (File.Exists(path)) UiKit.ConfigureTexture(path, Vector4.zero);
            }
        }

        /// <summary>Builds the prefab when it is missing or older, refreshes the background, points the catalog at it.</summary>
        public static int BuildOrRefresh(bool again = false)
        {
            if (Figma("Top_Console") == null || Figma("Menu") == null || Derived("V5_CyanFrame") == null) return 0;
            bold = UiKit.RequireFont("Oxanium-Bold.ttf");
            medium = UiKit.RequireFont("Oxanium-Medium.ttf");
            int changes = 0;
            int screens = BuildScreens(again);
            changes += screens;
            changes += UpgradeScreens();
            FrontEndView existing = File.Exists(PrefabPath) ? AssetDatabase.LoadAssetAtPath<FrontEndView>(PrefabPath) : null;
            if (again || screens > 0 || existing == null || existing.BuiltVersion < Version)
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

        // Each screen as its own prefab asset; an existing one is kept (it may have the owner's edits).
        private static int BuildScreens(bool again)
        {
            if (!AssetDatabase.IsValidFolder(ScreensFolder.TrimEnd('/')))
            {
                AssetDatabase.CreateFolder(UiKit.FinalUiFolder.TrimEnd('/'), "FrontEnd");
            }

            int built = 0;
            foreach (var (file, name, _, _) in Screens)
            {
                string path = ScreensFolder + file + ".prefab";
                if (!again && File.Exists(path)) continue;

                GameObject holder = new GameObject("Holder", typeof(RectTransform));
                holder.layer = 5;
                try
                {
                    RectTransform rect = (RectTransform)holder.transform;
                    UiKit.Stretch(rect);
                    BuildScreen(name, rect);
                    Transform screen = holder.transform.Find(name);
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(screen.gameObject, path);
                    if (Designs.TryGetValue(file, out int design)) AssetDatabase.SetLabels(saved, new[] { DesignLabel + design });
                    built++;
                }
                finally
                {
                    Object.DestroyImmediate(holder);
                }
            }

            return built;
        }

        // Screens whose design changed after they may already have been built (owner, 2026-10-25: Game details as in
        // the reference). An older prefab is not rebuilt (it holds the owner's edits): only the new parts are added.
        private static readonly Dictionary<string, int> Designs = new Dictionary<string, int> { { "FE_Details", 2 } };
        private const string DesignLabel = "FrontEndDesign";

        private static bool HasDesign(GameObject asset, int design)
        {
            return Array.IndexOf(AssetDatabase.GetLabels(asset), DesignLabel + design) >= 0;
        }

        private static void MarkDesign(string path, int design)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return;
            List<string> labels = new List<string>(AssetDatabase.GetLabels(asset));
            labels.RemoveAll(label => label.StartsWith(DesignLabel, StringComparison.Ordinal));
            labels.Add(DesignLabel + design);
            AssetDatabase.SetLabels(asset, labels.ToArray());
        }

        // Owner, 2026-10-25: "I don't want to overwrite my changes". A screen made with an older design gets only the
        // new parts added; everything already in it (positions, sizes, anchors, texts, sprites) is left as it is.
        private static int UpgradeScreens()
        {
            int upgraded = 0;
            foreach (KeyValuePair<string, int> design in Designs)
            {
                string path = ScreensFolder + design.Key + ".prefab";
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || HasDesign(asset, design.Value)) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (design.Key == "FE_Details") UpgradeDetails((RectTransform)root.transform);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                MarkDesign(path, design.Value);
                Debug.Log($"Console Cards: added the new parts to {path} (your edits are kept).");
                upgraded++;
            }

            return upgraded;
        }

        // Game details design 2: grey info pill, picture strip with arrows, feature tiles, IN THE BOX rows and icons.
        private static void UpgradeDetails(RectTransform page)
        {
            Transform stats = page.Find("Stats");
            Image pill = stats != null ? stats.GetComponent<Image>() : null;
            if (pill != null && pill.sprite == Derived("V5_OrangePill")) pill.sprite = Derived("V5_GreyPill");

            RectTransform media = page.Find("Media") as RectTransform;
            if (media != null && media.Find("Thumb0") == null)
            {
                Transform picture = media.Find("Picture");
                if (picture != null) picture.gameObject.SetActive(false);
                Rect area = media.rect;
                MediaParts(media, area.width > 1f ? area.width / 830f : 1f, area.height > 1f ? area.height / 296f : 1f);
            }

            RectTransform box = page.Find("Box") as RectTransform;
            if (box != null && box.Find("Row0") == null)
            {
                for (int i = 0; i < FrontEndView.BoxRowCount; i++)
                {
                    RectTransform row = Node("Row" + i, box, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                        new Vector2(0f, -(62f + i * 49f)), new Vector2(-52f, 40f));
                    BoxRowParts(row);
                }

                IconSet(box);
                // Six rows need this much room; a box the owner made taller keeps its height.
                float needed = 62f + FrontEndView.BoxRowCount * 49f + 12f;
                if (Mathf.Approximately(box.anchorMin.y, box.anchorMax.y) && box.sizeDelta.y < needed)
                {
                    box.sizeDelta = new Vector2(box.sizeDelta.x, needed);
                }
            }
        }

        // The picture strip and feature tiles, measured on an 830x296 panel (scaled to the panel's own size).
        private static void MediaParts(RectTransform media, float sx, float sy)
        {
            GalleryArrow(TopLeft("Previous", media, 18f * sx, 72f * sy, 24f, 24f), Figma("leftArrowIcon"));
            GalleryArrow(TopLeft("Next", media, 788f * sx, 72f * sy, 24f, 24f), Figma("RightArrowIcon"));
            for (int i = 0; i < FrontEndView.GalleryThumbCount; i++)
            {
                Thumb(media, "Thumb" + i, (50f + i * 252f) * sx, 15f * sy, 232f * sx, 138f * sy);
            }

            for (int i = 0; i < FrontEndView.FeatureTileCount; i++)
            {
                FeatureTile(media, "Feature" + i, (17f + i * 270f) * sx, 168f * sy, 257f * sx, 107f * sy);
            }
        }

        private static void BuildScreen(string name, RectTransform holder)
        {
            switch (name)
            {
                case "TopBar": TopBar(holder); break;
                case "Stepper": Stepper(holder); break;
                case "Home": Home(holder); break;
                case "Create": Create(holder); break;
                case "Profile": Profile(holder); break;
                case "Library": Library(holder); break;
                case "Details": Details(holder); break;
                case "Setup": Setup(holder); break;
                case "Players": Players(holder); break;
                case "Review": Review(holder); break;
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
                background.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.None;

                RectTransform pages = null;
                foreach (var (file, name, inPages, shown) in Screens)
                {
                    if (inPages && pages == null) pages = Stretched("Pages", root);
                    GameObject screen = AssetDatabase.LoadAssetAtPath<GameObject>(ScreensFolder + file + ".prefab");
                    if (screen == null) throw new InvalidOperationException($"The front end screen {file} could not be built.");
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(screen, inPages ? pages : root);
                    instance.name = name;
                    instance.SetActive(shown);
                }

                RectTransform status = Node("Status", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 8f), new Vector2(-200f, 24f));
                Words(status, medium, 14, Muted, TextAnchor.MiddleRight, string.Empty);

                SerializedObject so = new SerializedObject(view);
                so.FindProperty("gameEntry").objectReferenceValue = Figma("Game Entry");
                so.FindProperty("selectedGameEntry").objectReferenceValue = Derived("V5_CyanEntry");
                so.FindProperty("profileEntry").objectReferenceValue = Figma("Menu");
                so.FindProperty("selectedProfileEntry").objectReferenceValue = Derived("V5_CyanFrame");
                so.FindProperty("ringSprite").objectReferenceValue = Derived("V5_I_Ring");
                so.FindProperty("discSprite").objectReferenceValue = Derived("V5_I_Disc");
                so.FindProperty("builtVersion").intValue = Version;
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

        // ---------- Top bar ----------

        // The brand pill (orange caps) over the start of the Top_Console bar; tabs in the middle; icons at the right.
        private static void TopBar(RectTransform root)
        {
            RectTransform top = Stretched("TopBar", root);
            RectTransform bar = Node("Bar", top, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                new Vector2(173f, -30f), new Vector2(-454f, 72f));
            Plate(bar, Figma("Top_Console"), 130f / 72f).raycastTarget = false;

            RectTransform brand = TopLeft("Brand", top, 56f, 30f, 380f, 72f);
            Plate(brand, Figma("Menu"), 213f / 72f).raycastTarget = false;
            HorizontalLayoutGroup brandRow = Row(brand, 14f, TextAnchor.MiddleCenter);
            brandRow.padding = new RectOffset(10, 10, 0, 0);
            Picture(Node("Dot", brand), Derived("V5_I_Disc"), Cyan, 12f, 12f);
            Fit(Words(Node("Label", brand), bold, 27, White, TextAnchor.MiddleLeft, "CONSOLE CARDS"));

            RectTransform tabs = Node("Tabs", top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-12f, -30f), new Vector2(560f, 72f));
            Row(tabs, 34f, TextAnchor.MiddleCenter);
            TabButton(tabs, "Play", "PLAY");
            Rule(tabs, "Rule1");
            TabButton(tabs, "Create", "CREATE");
            Rule(tabs, "Rule2");
            TabButton(tabs, "Sandbox", "SANDBOX");
            RectTransform highlight = Node("Highlight", tabs, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 6f), new Vector2(46f, 5f));
            highlight.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Plate(highlight, Figma("Highlighter"), 1.5f).raycastTarget = false;

            RectTransform right = Node("Right", top, Vector2.one, Vector2.one, Vector2.one, new Vector2(-96f, -30f),
                new Vector2(520f, 72f));
            Row(right, 26f, TextAnchor.MiddleRight);
            IconWord(right, "Guide", Derived("V5_I_Book"), "GUIDE");
            IconWord(right, "Settings", Derived("V5_I_Gear"), "SETTINGS");
            IconWord(right, "Table", Figma("TableIcon"), "TABLE");
            IconWord(right, "Exit", Derived("V5_I_Door"), "EXIT");
        }

        // 1 Game Setup, 2 Players, 3 Review: a dark pill with circled numbers (the current one filled cyan).
        private static void Stepper(RectTransform pages)
        {
            RectTransform stepper = TopLeft("Stepper", pages, 727f, 152f, 697f, 53f);
            Plate(stepper, Figma("Frame_move"), 1.5f);
            Row(stepper, 70f, TextAnchor.MiddleCenter);
            string[] labels = { "Game Setup", "Players", "Review" };
            for (int i = 0; i < 3; i++)
            {
                RectTransform step = Node("Step" + i, stepper);
                Row(step, 10f, TextAnchor.MiddleCenter);
                RectTransform circle = Node("Circle", step);
                Picture(circle, Derived("V5_I_Ring"), White, 30f, 30f);
                Words(Stretched("Number", circle), bold, 15, White, TextAnchor.MiddleCenter, (i + 1).ToString());
                Fit(Words(Node("Label", step), medium, 15, White, TextAnchor.MiddleLeft, labels[i]));
            }

        }

        // ---------- Screens ----------

        private static void Home(RectTransform pages)
        {
            RectTransform page = Page(pages, "Home");
            PageTitle(page, "CONSOLE CARDS", "V I R T U A L   P L A T F O R M", 118f, 205f, 100);
            Words(TopLeft("Tagline", page, 122f, 372f, 700f, 54f), medium, 16, White, TextAnchor.UpperLeft,
                "P L A Y ,  C R E A T E ,  A N D  E X P L O R E\nT A B L E T O P  G A M E S  I N  A  D I G I T A L  S P A C E .");
            RectTransform menu = TopLeft("Menu", page, 79f, 461f, 681f, 448f);
            MenuEntry(menu, "Play", 0f, "PLAY", "Choose a game, set up a session,\nand start playing.", "V5_I_Play", true);
            MenuEntry(menu, "Create", 157f, "CREATE", "Design and customize your\nown games and content.", "editIcon", false);
            MenuEntry(menu, "Sandbox", 312f, "SANDBOX", "Open an empty table and\nexperiment freely.", "V5_I_Cube", false);
        }

        private static void Create(RectTransform pages)
        {
            RectTransform page = Page(pages, "Create");
            PageTitle(page, "CREATE", "M A K E   T H E   G A M E S   Y O U R S", 118f, 205f, 100);
            RectTransform menu = TopLeft("Menu", page, 79f, 400f, 681f, 448f);
            MenuEntry(menu, "CardArt", 0f, "CARD ART", "Your own pictures\non the cards.", "editIcon", true);
            MenuEntry(menu, "CardEditor", 157f, "CARD EDITOR", "Change the cards\nand add new ones.", "V5_I_Cards", false);
            MenuEntry(menu, "DeckEditor", 312f, "DECK EDITOR", "Build the decks\nyou play with.", "V5_I_Cards", false);
            BackButton(page, 79f, 900f, "BACK");
        }

        private static void Profile(RectTransform pages)
        {
            RectTransform page = Page(pages, "Profile");
            PageTitle(page, "CONSOLE CARDS", "V I R T U A L   P L A T F O R M", 105f, 165f, 92);

            RectTransform panel = TopLeft("Panel", page, 68f, 303f, 652f, 617f);
            Plate(panel, Figma("Player Profile"), 2f);
            Words(TopLeft("Title", panel, 44f, 26f, 560f, 44f), bold, 36, White, TextAnchor.MiddleLeft, "PLAYER PROFILE");
            Words(TopLeft("Subtitle", panel, 44f, 72f, 560f, 24f), medium, 15, White, TextAnchor.MiddleLeft,
                "Set up your local player before choosing a game.");

            // The profile playing now: avatar, PLAYER 1, the name box (opens the profile panel), CUSTOMIZE AVATAR.
            RectTransform active = TopLeft("Active", panel, 20f, 128f, 612f, 158f);
            Image activePlate = Plate(active, Derived("V5_CyanFrame"), 213f / 158f);
            active.gameObject.AddComponent<Button>().targetGraphic = activePlate;
            Framed(active, "Avatar", 36f, 22f, 106f, 113f, Derived("V5_CyanLine"));
            Words(TopLeft("Small", active, 164f, 14f, 300f, 20f), bold, 12, Cyan, TextAnchor.MiddleLeft, "PLAYER 1");
            RectTransform nameBox = TopLeft("NameBox", active, 160f, 38f, 410f, 46f);
            Image nameBoxPlate = Plate(nameBox, Figma("Frame_move"), 1.5f);
            nameBox.gameObject.AddComponent<Button>().targetGraphic = nameBoxPlate;
            Words(TopLeft("Name", nameBox, 18f, 0f, 330f, 46f), bold, 20, White, TextAnchor.MiddleLeft, "ALEX");
            Picture(Node("Edit", nameBox, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-14f, 0f), new Vector2(20f, 20f)), Figma("editIcon"), Cyan, -1f, -1f);
            RectTransform customize = TopLeft("Customize", active, 160f, 94f, 410f, 42f);
            Image customizePlate = Plate(customize, Figma("Frame_move"), 1.5f);
            customize.gameObject.AddComponent<Button>().targetGraphic = customizePlate;
            Picture(TopLeft("Icon", customize, 16f, 12f, 18f, 18f), Derived("V5_I_Person"), Muted, -1f, -1f);
            Words(TopLeft("Label", customize, 46f, 0f, 320f, 42f), bold, 12, White, TextAnchor.MiddleLeft, "CUSTOMIZE AVATAR");
            Picture(Node("Chevron", customize, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-14f, 0f), new Vector2(16f, 16f)), Figma("RightArrowIcon"), White, -1f, -1f);

            for (int i = 1; i < FrontEndView.ProfileRowCount; i++)
            {
                RectTransform row = TopLeft("Row" + i, panel, 20f, 296f + (i - 1) * 109f, 612f, 95f);
                Image plate = Plate(row, Figma("Menu"), 213f / 95f);
                row.gameObject.AddComponent<Button>().targetGraphic = plate;
                Framed(row, "Avatar", 40f, 10f, 76f, 76f, null);
                Words(TopLeft("Small", row, 136f, 8f, 300f, 20f), bold, 11, Muted, TextAnchor.MiddleLeft, "PLAYER " + (i + 1));
                RectTransform box = TopLeft("NameBox", row, 132f, 32f, 438f, 48f);
                Plate(box, Figma("Frame_move"), 1.5f).raycastTarget = false;
                Words(TopLeft("Name", box, 18f, 0f, 400f, 48f), bold, 18, White, TextAnchor.MiddleLeft, "GUEST " + (i + 1));
            }

            RectTransform add = TopLeft("Add", panel, 20f, 296f, 612f, 95f);
            Image addPlate = Plate(add, Figma("Menu"), 213f / 95f);
            add.gameObject.AddComponent<Button>().targetGraphic = addPlate;
            RectTransform addBox = TopLeft("Box", add, 40f, 16f, 64f, 64f);
            Plate(addBox, Figma("Frame_move"), 1.5f).raycastTarget = false;
            Picture(Node("Icon", addBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(26f, 26f)), Derived("V5_I_Plus"), Muted, -1f, -1f);
            Words(TopLeft("Label", add, 136f, 20f, 420f, 30f), bold, 18, White, TextAnchor.MiddleLeft, "NEW PROFILE");
            Words(TopLeft("Hint", add, 136f, 50f, 420f, 22f), medium, 13, Muted, TextAnchor.MiddleLeft,
                "Make or rename a profile.");

            // The avatar card: the name between two diamonds, the picture in an orange frame, arrows and page dots.
            RectTransform avatar = TopLeft("Avatar", page, 738f, 303f, 371f, 576f);
            Plate(avatar, Figma("Avatar"), 2f);
            Picture(TopLeft("DiamondLeft", avatar, 52f, 22f, 16f, 16f), Derived("V5_I_Diamond"), Cyan, -1f, -1f);
            Picture(TopLeft("DiamondRight", avatar, 303f, 22f, 16f, 16f), Derived("V5_I_Diamond"), Cyan, -1f, -1f);
            Words(TopLeft("Name", avatar, 70f, 12f, 231f, 36f), bold, 22, White, TextAnchor.MiddleCenter, "ALEX");
            Framed(avatar, "Picture", 31f, 66f, 309f, 414f, Derived("V5_OrangeLine"));
            Words(TopLeft("AvatarName", avatar, 31f, 446f, 309f, 28f), medium, 14, Muted, TextAnchor.MiddleCenter, string.Empty);
            ArrowButton(TopLeft("Previous", avatar, 40f, 506f, 30f, 30f), Figma("leftArrowIcon"));
            ArrowButton(TopLeft("Next", avatar, 301f, 506f, 30f, 30f), Figma("RightArrowIcon"));
            RectTransform dots = TopLeft("Dots", avatar, 135f, 512f, 100f, 18f);
            Row(dots, 10f, TextAnchor.MiddleCenter);
            for (int i = 0; i < 4; i++)
            {
                Picture(Node("Dot" + i, dots), Figma(i == 0 ? "Current_pageEllipse" : "page elipse"), i == 0 ? Cyan : Muted, 8f, 8f);
            }

            BackButton(page, 68f, 965f, "BACK");
            Cta(page, "Continue", 368f, 965f, 462f, 88f, Derived("V5_CyanFrame"), "CONTINUE", null, true);
        }

        private static void Library(RectTransform pages)
        {
            RectTransform page = Page(pages, "Library");
            PageTitle(page, "GAME LIBRARY", "C H O O S E   A   G A M E   T O   P L A Y", 103f, 167f, 92);

            RectTransform list = TopLeft("List", page, 59f, 300f, 645f, 650f);
            ScrollRect scroll = list.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Stretched("Viewport", list);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            RectTransform content = Node("Content", viewport, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            Column(content, 14f).padding = new RectOffset(0, 4, 8, 8);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform template = Node("Template", content);
            Size(template, -1f, 200f);
            Image plate = Plate(template, Figma("Game Entry"), 355f / 200f);
            template.gameObject.AddComponent<Button>().targetGraphic = plate;
            Framed(template, "Picture", 34f, 16f, 262f, 168f, null);
            Words(TopLeft("Name", template, 316f, 24f, 290f, 40f), bold, 30, White, TextAnchor.MiddleLeft, "TRAP FLOOR");
            Words(TopLeft("Detail", template, 316f, 66f, 290f, 60f), medium, 16, White, TextAnchor.UpperLeft, string.Empty);
            RectTransform players = TopLeft("PlayersRow", template, 316f, 146f, 290f, 26f);
            Row(players, 10f, TextAnchor.MiddleLeft);
            Picture(Node("Icon", players), Figma("playersIcon"), Cyan, 22f, 22f);
            Fit(Words(Node("Players", players), medium, 16, White, TextAnchor.MiddleLeft, "2 - 4 Players"));

            RectTransform detail = TopLeft("Detail", page, 718f, 296f, 555f, 651f);
            Plate(detail, Figma("Player Profile"), 2f);
            Words(TopLeft("Name", detail, 32f, 22f, 440f, 44f), bold, 34, White, TextAnchor.MiddleLeft, "TRAP FLOOR");
            Picture(TopLeft("Dots", detail, 495f, 32f, 26f, 26f), Derived("V5_I_Dots"), Muted, -1f, -1f);
            Framed(detail, "Picture", 30f, 83f, 495f, 333f, Derived("V5_CyanLine"));
            Words(TopLeft("Text", detail, 32f, 430f, 491f, 76f), medium, 15, White, TextAnchor.UpperLeft, string.Empty);
            StatsStrip(detail, 30f, 520f, 495f, 92f);

            BackButton(page, 59f, 965f, "BACK");
            Cta(page, "Select", 374f, 965f, 640f, 88f, Derived("V5_CyanFrame"), "SELECT TRAP FLOOR", null, true);
        }

        private static void Details(RectTransform pages)
        {
            RectTransform page = Page(pages, "Details");
            BackChip(page, "<  BACK TO LIBRARY", 98f, 141f, 216f);
            Text title = Words(TopLeft("Title", page, 102f, 210f, 900f, 90f), bold, 86, White, TextAnchor.MiddleLeft, "TRAP FLOOR");
            Shade(title);
            Words(TopLeft("Tagline", page, 106f, 300f, 800f, 26f), medium, 19, White, TextAnchor.MiddleLeft, string.Empty);
            Words(TopLeft("Text", page, 102f, 338f, 580f, 100f), medium, 17, White, TextAnchor.UpperLeft, string.Empty);
            // Owner, 2026-10-25: the info pill has no orange highlight in the reference (a plain grey outline).
            StatsPill(page, 91f, 451f, 559f, 46f, Derived("V5_GreyPill"));

            // The reference: a strip of three pictures with arrows, then three feature tiles.
            RectTransform media = TopLeft("Media", page, 71f, 518f, 830f, 296f);
            Plate(media, Figma("Player Profile"), 2f);
            MediaParts(media, 1f, 1f);

            // IN THE BOX: one row per kind of component; the box grows with its rows.
            RectTransform box = TopLeft("Box", page, 1533f, 126f, 326f, 324f);
            Plate(box, Figma("Player Profile"), 2.5f);
            VerticalLayoutGroup lines = Column(box, 9f);
            lines.padding = new RectOffset(26, 26, 18, 22);
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Size(Words(Node("Title", box), bold, 16, White, TextAnchor.MiddleLeft, "IN THE BOX").rectTransform, -1f, 30f);
            for (int i = 0; i < FrontEndView.BoxRowCount; i++) BoxRow(box, "Row" + i);
            Words(Node("Text", box), medium, 15, White, TextAnchor.UpperLeft, string.Empty).gameObject.SetActive(false);
            IconSet(box);

            Cta(page, "Start", 71f, 838f, 826f, 124f, Figma("Menu_Selected"), "START SETUP", "Configure your session.", false);
        }

        private static void Setup(RectTransform pages)
        {
            RectTransform page = Page(pages, "Setup");
            BackChip(page, "<  BACK TO DETAILS", 75f, 152f, 212f);
            StepTitle(page, "G A M E   C O N F I G U R A T I O N");
            RectTransform options = TopLeft("Options", page, 73f, 338f, 798f, 515f);
            Plate(options, Figma("Player Profile"), 2f);
            RectTransform column = TopLeft("Column", options, 14f, 14f, 770f, 487f);
            Column(column, 0f);
            OptionRow(column, "Mode", "V5_I_Mode", "GAME MODE", "Choose a mode for your session.");
            OptionRow(column, "Players", "playersIcon", "PLAYER COUNT", "Number of local players.");
            OptionRow(column, "WinMode", "V5_I_Bars", "WIN MODE", "How the game is won.");
            OptionRow(column, "Turns", "V5_I_Clock", "TURNS", "Which way the turns go round.");
            OptionRow(column, "Deck", "V5_I_Cards", "DECK SET", "Choose the card set to use.");

            RectTransform summary = TopLeft("Summary", page, 1537f, 144f, 334f, 257f);
            Plate(summary, Figma("Player Profile"), 2.5f);
            Words(TopLeft("Title", summary, 26f, 18f, 280f, 26f), bold, 16, White, TextAnchor.MiddleLeft, "SESSION SUMMARY");
            RectTransform rows = TopLeft("Rows", summary, 26f, 56f, 282f, 180f);
            Column(rows, 6f);
            string[] icons = { "V5_I_Mode", "playersIcon", "V5_I_Bars", "V5_I_Clock", "V5_I_Cards" };
            string[] labels = { "Mode", "Players", "Win mode", "Turns", "Deck Set" };
            for (int i = 0; i < 5; i++) SummaryRow(rows, "Row" + i, icons[i], labels[i]);

            Cta(page, "Continue", 73f, 878f, 795f, 122f, Figma("Menu_Selected"), "CONTINUE", "Set up your players.", false);
        }

        private static void Players(RectTransform pages)
        {
            RectTransform page = Page(pages, "Players");
            BackChip(page, "<  BACK TO CONFIG", 75f, 152f, 212f);
            StepTitle(page, "P L A Y E R   S E T U P");
            Words(TopLeft("Text", page, 76f, 308f, 760f, 44f), medium, 15, White, TextAnchor.UpperLeft,
                "Each seat plays in its colour. Player 1 is your profile;\nthe other seats are local players at this table.");

            RectTransform rows = TopLeft("Rows", page, 73f, 362f, 737f, 520f);
            Column(rows, 8f);
            for (int i = 0; i < FrontEndView.PlayerRowCount; i++)
            {
                RectTransform row = Node("Row" + i, rows);
                Size(row, -1f, 92f);
                Plate(row, Figma("Menu"), 213f / 92f).raycastTarget = false;
                RectTransform badge = TopLeft("Badge", row, 30f, 30f, 46f, 32f);
                Plate(badge, AssetDatabase.LoadAssetAtPath<Sprite>(WhitePlate), 1.5f).raycastTarget = false;
                Words(Stretched("Label", badge), bold, 15, Dark, TextAnchor.MiddleCenter, "P" + (i + 1));
                Framed(row, "Avatar", 90f, 8f, 76f, 76f, null);
                RectTransform box = TopLeft("NameBox", row, 184f, 12f, 330f, 40f);
                Plate(box, Figma("Frame_move"), 1.5f).raycastTarget = false;
                Words(TopLeft("Name", box, 14f, 0f, 280f, 40f), bold, 17, White, TextAnchor.MiddleLeft, "Player " + (i + 1));
                Picture(Node("Edit", box, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-12f, 0f), new Vector2(16f, 16f)), Figma("editIcon"), Muted, -1f, -1f);
                Picture(TopLeft("Dot", row, 188f, 62f, 12f, 12f), Derived("V5_I_Disc"), Color.white, -1f, -1f);
                Words(TopLeft("Controller", row, 206f, 56f, 300f, 24f), medium, 13, Muted, TextAnchor.MiddleLeft,
                    "Controller: BLUE");
                RectTransform pawn = TopLeft("PawnBox", row, 538f, 8f, 168f, 76f);
                Plate(pawn, Figma("Frame_move"), 1.5f).raycastTarget = false;
                Picture(TopLeft("Pawn", pawn, 66f, 12f, 36f, 52f), Derived("V5_I_Pawn"), Color.white, -1f, -1f);
            }

            RectTransform addRow = Node("AddRow", rows);
            Size(addRow, -1f, 84f);
            Plate(addRow, Figma("Menu"), 213f / 84f).raycastTarget = false;
            RectTransform plusBox = TopLeft("Box", addRow, 30f, 20f, 44f, 44f);
            Plate(plusBox, Figma("Frame_move"), 1.5f).raycastTarget = false;
            Picture(Node("Icon", plusBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(20f, 20f)), Derived("V5_I_Plus"), Muted, -1f, -1f);
            Words(TopLeft("Title", addRow, 94f, 18f, 300f, 26f), bold, 17, White, TextAnchor.MiddleLeft, "ADD PLAYER");
            Words(TopLeft("Hint", addRow, 94f, 44f, 300f, 22f), medium, 13, Muted, TextAnchor.MiddleLeft, "Or remove the last seat.");
            SquareButton(addRow, "Remove", 498f, 14f, "V5_I_Plus", true);
            SquareButton(addRow, "Add", 590f, 14f, "V5_I_Plus", false);

            RectTransform info = TopLeft("Info", page, 1534f, 129f, 332f, 204f);
            Plate(info, Figma("Player Profile"), 2.5f);
            Words(TopLeft("Title", info, 26f, 18f, 280f, 26f), bold, 16, White, TextAnchor.MiddleLeft, "PLAYER CONTROLS");
            RectTransform padBox = TopLeft("PadBox", info, 26f, 62f, 120f, 110f);
            Plate(padBox, AssetDatabase.LoadAssetAtPath<Sprite>(WhitePlate), 1f).color = UiKit.Hex("e0602a");
            Picture(Node("Icon", padBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(76f, 76f)), Derived("V5_I_Pad"), Dark, -1f, -1f);
            Words(TopLeft("Text", info, 160f, 62f, 150f, 120f), medium, 14, White, TextAnchor.UpperLeft,
                "Each player uses a Console Controller to interact with cards and the table.");

            Cta(page, "Continue", 73f, 904f, 737f, 114f, Derived("V5_CyanFrame"), "CONTINUE", "Review your setup.", false);
        }

        private static void Review(RectTransform pages)
        {
            RectTransform page = Page(pages, "Review");
            BackChip(page, "<  BACK TO PLAYERS", 75f, 152f, 212f);
            StepTitle(page, "R E V I E W   S E T U P");
            Words(TopLeft("Text", page, 76f, 312f, 760f, 44f), medium, 15, White, TextAnchor.UpperLeft,
                "Check your game settings and players before starting.\nYou can go back and make changes anytime.");

            RectTransform settings = TopLeft("Settings", page, 61f, 370f, 728f, 160f);
            Plate(settings, Figma("Player Profile"), 2.5f);
            PanelHeader(settings, "GAME SETTINGS");
            RectTransform grid = TopLeft("Grid", settings, 30f, 60f, 668f, 84f);
            GridLayoutGroup cells = grid.gameObject.AddComponent<GridLayoutGroup>();
            cells.cellSize = new Vector2(330f, 26f);
            cells.spacing = new Vector2(8f, 4f);
            string[] icons = { "V5_I_Mode", "playersIcon", "V5_I_Bars", "V5_I_Cards", "V5_I_Clock" };
            string[] labels = { "Mode", "Players", "Win mode", "Deck Set", "Turns" };
            for (int i = 0; i < 5; i++) SummaryRow(grid, "Cell" + i, icons[i], labels[i]);

            RectTransform players = TopLeft("Players", page, 61f, 548f, 728f, 252f);
            Plate(players, Figma("Player Profile"), 2.5f);
            PanelHeader(players, "PLAYERS");
            RectTransform tiles = TopLeft("Tiles", players, 30f, 58f, 668f, 180f);
            Row(tiles, 14f, TextAnchor.UpperLeft);
            for (int i = 0; i < FrontEndView.PlayerRowCount; i++)
            {
                RectTransform tile = Node("Tile" + i, tiles);
                Size(tile, 100f, 180f);
                Framed(tile, "Avatar", 0f, 0f, 100f, 112f, Derived("V5_CyanLine"));
                RectTransform badge = TopLeft("Badge", tile, 4f, 4f, 30f, 20f);
                Plate(badge, AssetDatabase.LoadAssetAtPath<Sprite>(WhitePlate), 2f).raycastTarget = false;
                Words(Stretched("Label", badge), bold, 11, Dark, TextAnchor.MiddleCenter, "P" + (i + 1));
                Words(TopLeft("Name", tile, 0f, 118f, 100f, 22f), bold, 14, White, TextAnchor.MiddleCenter, "Player " + (i + 1));
                Picture(TopLeft("Dot", tile, 16f, 148f, 14f, 18f), Derived("V5_I_Pawn"), Color.white, -1f, -1f);
                Words(TopLeft("Controller", tile, 34f, 146f, 66f, 22f), medium, 11, Muted, TextAnchor.MiddleLeft, "BLUE");
            }

            RectTransform parts = TopLeft("Components", page, 61f, 818f, 728f, 98f);
            Plate(parts, Figma("Player Profile"), 2.5f);
            PanelHeader(parts, "COMPONENTS IN TABLE", false);
            Words(TopLeft("Text", parts, 30f, 52f, 668f, 36f), medium, 14, White, TextAnchor.MiddleLeft, string.Empty);

            Cta(page, "Start", 61f, 940f, 728f, 104f, Derived("V5_CyanFrame"), "START GAME", "Load the game and begin playing.", false);
        }

        // ---------- Pieces ----------

        private static RectTransform Page(RectTransform pages, string name)
        {
            return Stretched(name, pages); // shown or hidden by FrontEnd (and the view)
        }

        private static void PageTitle(RectTransform page, string title, string subtitle, float x, float y, int size)
        {
            Text big = Words(TopLeft("Title", page, x, y, 1100f, size + 16f), bold, size, White, TextAnchor.MiddleLeft, title);
            Shade(big);
            Words(TopLeft("Subtitle", page, x + 6f, y + size + 4f, 1000f, 26f), medium, 19, White, TextAnchor.MiddleLeft, subtitle);
        }

        // The setup steps: the game's picture in a small frame, the game name, the step line.
        private static void StepTitle(RectTransform page, string subtitle)
        {
            Framed(page, "Thumb", 75f, 215f, 152f, 96f, Derived("V5_OrangeLine"));
            Text title = Words(TopLeft("Title", page, 257f, 210f, 900f, 70f), bold, 66, White, TextAnchor.MiddleLeft, "TRAP FLOOR");
            Shade(title);
            Words(TopLeft("Subtitle", page, 260f, 278f, 900f, 24f), medium, 17, White, TextAnchor.MiddleLeft, subtitle);
        }

        private static void Shade(Text text)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(3f, -5f);
            shadow.effectColor = TitleShadow;
        }

        // A Menu entry (orange caps): glyph, title and two lines, a chevron; lit (Menu_Selected) on hover.
        private static void MenuEntry(RectTransform menu, string name, float y, string title, string line, string icon, bool lit)
        {
            RectTransform entry = TopLeft(name, menu, 0f, y, 681f, 136f);
            // Owner, 2026-10-25: plain until the pointer is over it (the board shows the hover state).
            Image plate = Plate(entry, Figma("Menu"), 213f / 136f);
            Button button = entry.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState states = button.spriteState;
            states.highlightedSprite = Figma("Menu_Selected");
            states.pressedSprite = Figma("Menu_Selected");
            button.spriteState = states;
            Sprite glyph = icon.StartsWith("V5_", StringComparison.Ordinal) ? Derived(icon) : Figma(icon);
            Picture(TopLeft("Glyph", entry, 74f, 42f, 52f, 52f), glyph, White, -1f, -1f);
            Words(TopLeft("Title", entry, 172f, 24f, 420f, 42f), bold, 34, White, TextAnchor.MiddleLeft, title);
            Words(TopLeft("Line", entry, 172f, 68f, 420f, 46f), medium, 15, White, TextAnchor.UpperLeft, line);
            Picture(Node("Chevron", entry, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-54f, 0f), new Vector2(22f, 22f)), Figma("RightArrowIcon"), White, -1f, -1f);
        }

        // One setup row (Menu, orange caps): icon, title and hint, "< value >" in an orange pill.
        private static void OptionRow(RectTransform column, string name, string icon, string title, string hint)
        {
            RectTransform row = Node(name, column);
            Size(row, -1f, 97f);
            Plate(row, Figma("Menu"), 213f / 97f).raycastTarget = false;
            Sprite glyph = icon.StartsWith("V5_", StringComparison.Ordinal) ? Derived(icon) : Figma(icon);
            Picture(TopLeft("Icon", row, 50f, 32f, 32f, 32f), glyph, White, -1f, -1f);
            Words(TopLeft("Title", row, 112f, 22f, 330f, 26f), bold, 18, White, TextAnchor.MiddleLeft, title);
            Words(TopLeft("Hint", row, 112f, 50f, 330f, 22f), medium, 13, Muted, TextAnchor.MiddleLeft, hint);
            RectTransform picker = TopLeft("Picker", row, 492f, 24f, 248f, 48f);
            Plate(picker, Derived("V5_OrangePill"), 1.5f).raycastTarget = false;
            ArrowButton(Node("Previous", picker, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(10f, 0f), new Vector2(24f, 24f)), Figma("leftArrowIcon"));
            Text value = Words(Node("Value", picker, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-76f, -4f)), bold, 15, White, TextAnchor.MiddleCenter, "Standard");
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 10;
            value.resizeTextMaxSize = 15;
            ArrowButton(Node("Next", picker, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(24f, 24f)), Figma("RightArrowIcon"));
        }

        private static void SummaryRow(RectTransform parent, string name, string icon, string label)
        {
            RectTransform row = Node(name, parent);
            Size(row, -1f, 28f);
            Sprite glyph = icon.StartsWith("V5_", StringComparison.Ordinal) ? Derived(icon) : Figma(icon);
            Picture(TopLeft("Icon", row, 0f, 4f, 20f, 20f), glyph, White, -1f, -1f);
            Words(TopLeft("Label", row, 32f, 0f, 120f, 28f), medium, 13, Muted, TextAnchor.MiddleLeft, label);
            Text value = Words(Node("Value", row, new Vector2(0f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero,
                new Vector2(-150f, 0f)), bold, 13, White, TextAnchor.MiddleRight, string.Empty);
            value.horizontalOverflow = HorizontalWrapMode.Wrap;
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 9;
            value.resizeTextMaxSize = 13;
        }

        // Three cells in an outlined strip: icon, a value and a small label (players, mode, deck).
        private static void StatsStrip(RectTransform parent, float x, float y, float width, float height)
        {
            RectTransform strip = TopLeft("Stats", parent, x, y, width, height);
            Plate(strip, Derived("V5_OrangePill"), 1.5f).raycastTarget = false;
            Row(strip, 0f, TextAnchor.MiddleLeft).childForceExpandWidth = true;
            string[] icons = { "playersIcon", "V5_I_Clock", "V5_I_Pad" };
            for (int i = 0; i < 3; i++)
            {
                RectTransform cell = Node("Cell" + i, strip);
                Row(cell, 10f, TextAnchor.MiddleCenter);
                Sprite glyph = icons[i].StartsWith("V5_", StringComparison.Ordinal) ? Derived(icons[i]) : Figma(icons[i]);
                Picture(Node("Icon", cell), glyph, Cyan, 30f, 30f);
                RectTransform words = Node("Words", cell);
                Column(words, 0f).childAlignment = TextAnchor.MiddleLeft;
                Words(Node("Value", words), bold, 18, White, TextAnchor.MiddleLeft, "2 - 4");
                Words(Node("Label", words), medium, 11, Muted, TextAnchor.MiddleLeft, "PLAYERS");
            }
        }

        // Three parts in an orange pill, split by rules: players, mode, deck (Game details).
        private static void StatsPill(RectTransform page, float x, float y, float width, float height, Sprite outline = null)
        {
            RectTransform pill = TopLeft("Stats", page, x, y, width, height);
            Plate(pill, outline != null ? outline : Derived("V5_OrangePill"), 1.5f).raycastTarget = false;
            HorizontalLayoutGroup row = Row(pill, 18f, TextAnchor.MiddleLeft);
            row.padding = new RectOffset(16, 16, 0, 0);
            string[] icons = { "playersIcon", "V5_I_Clock", "V5_I_Cards" };
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) Rule(pill, "Rule" + i);
                RectTransform cell = Node("Cell" + i, pill);
                Row(cell, 8f, TextAnchor.MiddleLeft);
                Sprite glyph = icons[i].StartsWith("V5_", StringComparison.Ordinal) ? Derived(icons[i]) : Figma(icons[i]);
                Picture(Node("Icon", cell), glyph, White, 20f, 20f);
                Fit(Words(Node("Label", cell), bold, 14, White, TextAnchor.MiddleLeft, "1 - 4 Players"));
            }
        }

        // A picture in the Game details strip: the picture (cropped to fill), a grey outline, the cyan one when chosen.
        private static void Thumb(RectTransform parent, string name, float x, float y, float width, float height)
        {
            RectTransform thumb = TopLeft(name, parent, x, y, width, height);
            Image hit = thumb.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.35f);
            thumb.gameObject.AddComponent<Button>().targetGraphic = hit;
            RectTransform mask = Stretched("Mask", thumb);
            mask.offsetMin = new Vector2(3f, 3f);
            mask.offsetMax = new Vector2(-3f, -3f);
            mask.gameObject.AddComponent<RectMask2D>();
            RectTransform picture = Node("Picture", mask, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(width, height));
            RawImage image = picture.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0.08f);
            AspectRatioFitter fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = width / height;
            Plate(Stretched("Frame", thumb), Derived("V5_GreyLine"), 1.5f).raycastTarget = false;
            RectTransform chosen = Stretched("Selected", thumb);
            chosen.offsetMin = new Vector2(-2f, -2f);
            chosen.offsetMax = new Vector2(2f, 2f);
            Plate(chosen, Derived("V5_CyanLine"), 1.5f).raycastTarget = false;
            chosen.gameObject.SetActive(false);
        }

        // An orange chevron that turns cyan on hover.
        private static void GalleryArrow(RectTransform rect, Sprite icon)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            Button button = rect.gameObject.AddComponent<Button>();
            Hover(button, image);
            ColorBlock colors = button.colors;
            colors.normalColor = Orange;
            colors.selectedColor = Orange;
            button.colors = colors;
        }

        // A feature tile: thin orange outline, an icon, a title and a short line.
        private static void FeatureTile(RectTransform parent, string name, float x, float y, float width, float height)
        {
            RectTransform tile = TopLeft(name, parent, x, y, width, height);
            Image plate = Plate(tile, Derived("V5_OrangePill"), 1.5f);
            plate.color = new Color(0.62f, 0.62f, 0.62f, 1f);
            plate.raycastTarget = false;
            Picture(Node("Icon", tile, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f),
                new Vector2(54f, 54f)), Derived("V5_I_Tile"), Pale, -1f, -1f);
            Words(TopLeft("Title", tile, 92f, 24f, width - 104f, 24f), bold, 14, White, TextAnchor.MiddleLeft, "Feature");
            Text line = Words(TopLeft("Line", tile, 92f, 50f, width - 104f, 40f), medium, 12, Muted, TextAnchor.UpperLeft,
                string.Empty);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // An IN THE BOX row: icon, name, "x 20" at the right.
        private static void BoxRow(RectTransform box, string name)
        {
            RectTransform row = Node(name, box);
            Size(row, -1f, 40f);
            BoxRowParts(row);
        }

        private static void BoxRowParts(RectTransform row)
        {
            Picture(Node("Icon", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f),
                new Vector2(30f, 30f)), Derived("V5_I_Tile"), Pale, -1f, -1f);
            Words(Node("Label", row, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(64f, 0f),
                new Vector2(-124f, 0f)), medium, 15, White, TextAnchor.MiddleLeft, "Component");
            Words(Node("Count", row, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero,
                new Vector2(60f, 0f)), medium, 15, White, TextAnchor.MiddleRight, "x 1");
        }

        // The pictures the rows and tiles use, by name (hidden; swap a sprite or colour here to change an icon).
        private static void IconSet(RectTransform parent)
        {
            RectTransform set = Node("IconSet", parent);
            set.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            string[] names = { "Tiles", "Cards", "Abilities", "Keys", "Pawns", "Console" };
            string[] icons = { "V5_I_Tile", "V5_I_Cards", "V5_I_Diamond", "V5_I_Key", "V5_I_Pawn", "V5_I_Console" };
            for (int i = 0; i < names.Length; i++)
            {
                Picture(Node(names[i], set), Derived(icons[i]), names[i] == "Keys" ? Gold : Pale, -1f, -1f);
            }

            set.gameObject.SetActive(false);
        }

        private static void PanelHeader(RectTransform panel, string title, bool edit = true)
        {
            Picture(TopLeft("Bullet", panel, 30f, 24f, 10f, 10f), Derived("V5_I_Disc"), Cyan, -1f, -1f);
            Words(TopLeft("Title", panel, 48f, 14f, 400f, 30f), bold, 17, White, TextAnchor.MiddleLeft, title);
            if (!edit) return;
            RectTransform chip = Node("Edit", panel, Vector2.one, Vector2.one, Vector2.one, new Vector2(-30f, -14f),
                new Vector2(110f, 36f));
            Image plate = Plate(chip, Derived("V5_OrangePill"), 1.5f);
            chip.gameObject.AddComponent<Button>().targetGraphic = plate;
            Words(Stretched("Label", chip), bold, 13, White, TextAnchor.MiddleCenter, "EDIT  >");
        }

        // A big action: Menu_Selected (orange caps) or the cyan frame; play glyph, title, a line, chevron.
        private static void Cta(RectTransform page, string name, float x, float y, float width, float height, Sprite sprite,
            string title, string line, bool chevron)
        {
            RectTransform button = TopLeft(name, page, x, y, width, height);
            Image plate = Plate(button, Figma("Menu"), 213f / height);
            Button press = button.gameObject.AddComponent<Button>();
            press.targetGraphic = plate;
            // Owner, 2026-10-25: plain until hovered; the teal or cyan look is the hover and press state.
            press.transition = Selectable.Transition.SpriteSwap;
            SpriteState states = press.spriteState;
            states.highlightedSprite = sprite;
            states.pressedSprite = sprite;
            press.spriteState = states;
            float glyph = Mathf.Min(48f, height * 0.42f);
            Picture(Node("Play", button, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(58f, 0f), new Vector2(glyph, glyph)), Derived("V5_I_Play"), Cyan, -1f, -1f);
            float left = 58f + glyph + 36f;
            if (string.IsNullOrEmpty(line))
            {
                Words(Node("Label", button, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(left * 0.5f - 30f, 0f),
                    new Vector2(-left - 60f, 0f)), bold, 30, White, TextAnchor.MiddleCenter, title);
            }
            else
            {
                Words(Node("Label", button, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f),
                    new Vector2(left, 0f), new Vector2(-left - 60f, 40f)), bold, 30, White, TextAnchor.LowerLeft, title);
                Words(Node("Line", button, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f),
                    new Vector2(left, -4f), new Vector2(-left - 60f, 26f)), medium, 15, White, TextAnchor.UpperLeft, line);
            }

            if (chevron)
            {
                Picture(Node("Chevron", button, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-40f, 0f), new Vector2(20f, 20f)), Figma("RightArrowIcon"), White, -1f, -1f);
            }
        }

        // BACK: a short Menu bar with "<  BACK".
        private static void BackButton(RectTransform page, float x, float y, string word)
        {
            RectTransform button = TopLeft("Back", page, x, y, 281f, 88f);
            Image plate = Plate(button, Figma("Menu"), 213f / 88f);
            button.gameObject.AddComponent<Button>().targetGraphic = plate;
            Row(button, 16f, TextAnchor.MiddleCenter);
            Picture(Node("Arrow", button), Figma("leftArrowIcon"), White, 18f, 18f);
            Fit(Words(Node("Label", button), bold, 20, White, TextAnchor.MiddleCenter, word));
        }

        private static void BackChip(RectTransform page, string word, float x, float y, float width)
        {
            RectTransform chip = TopLeft("Back", page, x, y, width, 41f);
            Image plate = Plate(chip, Derived("V5_OrangePill"), 1.5f);
            chip.gameObject.AddComponent<Button>().targetGraphic = plate;
            Words(Stretched("Label", chip), bold, 12, White, TextAnchor.MiddleCenter, word);
        }

        private static void SquareButton(RectTransform parent, string name, float x, float y, string icon, bool minus)
        {
            RectTransform button = TopLeft(name, parent, x, y, 76f, 56f);
            Image plate = Plate(button, Figma("Button_disabled"), 1.5f);
            Button press = button.gameObject.AddComponent<Button>();
            press.targetGraphic = plate;
            if (minus)
            {
                Words(Stretched("Label", button), bold, 30, White, TextAnchor.MiddleCenter, "−");
            }
            else
            {
                Picture(Node("Icon", button, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(22f, 22f)), Derived(icon), White, -1f, -1f);
            }
        }

        // A picture (RawImage) with an optional line frame drawn over its edge.
        private static void Framed(RectTransform parent, string name, float x, float y, float width, float height, Sprite frame)
        {
            RectTransform holder = TopLeft(name, parent, x, y, width, height);
            RawImage picture = holder.gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            picture.color = new Color(1f, 1f, 1f, 0.08f);
            if (frame == null) return;
            RectTransform line = Stretched("Frame", holder);
            line.offsetMin = new Vector2(-4f, -4f);
            line.offsetMax = new Vector2(4f, 4f);
            Image image = line.gameObject.AddComponent<Image>();
            image.sprite = frame;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.5f;
            image.raycastTarget = false;
        }

        private static void TabButton(RectTransform tabs, string name, string word)
        {
            RectTransform tab = Node(name, tabs);
            Text label = Words(Stretched("Label", tab), bold, 17, White, TextAnchor.MiddleCenter, word);
            label.raycastTarget = true;
            Size(tab, word.Length * 13f + 14f, 40f);
            Hover(tab.gameObject.AddComponent<Button>(), label);
        }

        private static void IconWord(RectTransform parent, string name, Sprite icon, string word)
        {
            RectTransform item = Node(name, parent);
            Row(item, 8f, TextAnchor.MiddleCenter);
            Picture(Node("Icon", item), icon, White, 18f, 18f);
            Text label = Fit(Words(Node("Label", item), bold, 13, White, TextAnchor.MiddleLeft, word));
            label.raycastTarget = true;
            Hover(item.gameObject.AddComponent<Button>(), label);
        }

        private static void Rule(RectTransform parent, string name)
        {
            Picture(Node(name, parent), Figma("Line"), Muted, 2f, 24f);
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
            return UiKit.Label(rect, font, size, color, alignment, text);
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

        private static VerticalLayoutGroup Column(RectTransform rect, float spacing)
        {
            VerticalLayoutGroup column = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = spacing;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            return column;
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
