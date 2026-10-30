using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Owner, 2026-10-30: "everything the same as the reference". Builds the front-end screens exactly as the menu-flow
    /// design boards (doc 38; the "Console Cards Menu Flow" canvas), measured from the boards at 1280 x 720 and scaled
    /// by 1.5 to the 1920 x 1080 canvas. Each screen keeps its FE_ prefab (and its place in FrontEnd.prefab); the
    /// screen as it was is copied to FrontEnd/Before boards/ first. A screen already built from its board is kept.
    /// <para>Console Cards &gt; UI &gt; Build Board Screens. Note: Apply Kit Look Again rebuilds the old screens.</para>
    /// </summary>
    public static partial class BoardScreensBuilder
    {
        private const string BackupFolder = FrontEndBuilder.ScreensFolder + "Before boards/";
        private const string BoardLabel = "BoardScreen";
        private const string StripePath = "Assets/ConsoleCards/Content/UI/Board/Stripes.png";
        private const float S = 1.5f; // board pixels to canvas units

        // The boards' colours.
        internal static readonly Color Words = Hex("e8eced");
        internal static readonly Color Soft = Hex("b7c0c3");
        internal static readonly Color Muted = Hex("9ba4a7");
        internal static readonly Color Faint = Hex("6b7477");
        internal static readonly Color Panel = Hex("222527");
        internal static readonly Color Row = Hex("2a2f31");
        internal static readonly Color Well = Hex("1c1f1f");
        internal static readonly Color Edge = Hex("3c4143");
        internal static readonly Color Cyan = Hex("06eff0");
        internal static readonly Color TealFill = Hex("1c3c3d");
        internal static readonly Color Orange = Hex("f2762e");
        internal static readonly Color Ink = Hex("0b1416");

        internal static Font bold;
        internal static Font medium;
        private static Sprite rounded;
        private static Sprite stripes;

        [MenuItem("Console Cards/UI/Build Board Screens (CREATE)")]
        public static void BuildMenu()
        {
            int built = Build();
            Debug.Log(built > 0
                ? $"[Boards] Built {built} screen(s) from the design boards (the old ones are in {BackupFolder})."
                : "[Boards] Every board screen is already built.");
        }

        /// <summary>Builds the screens not yet built from their boards; returns how many.</summary>
        public static int Build()
        {
            Prepare();
            int built = 0;
            if (Rebuild("FE_Create", 1, BuildCreate)) built++;
            if (Rebuild("FE_TopBar", 1, BuildTopBar)) built++; // boards 1–3 (BoardScreensBuilder.Flow.cs)
            if (Rebuild("FE_MainMenu", 2, BuildHome)) built++; // 2: BRAND-1, the logo as the title
            if (Rebuild("FE_Library", 1, BuildLibrary)) built++;
            if (Rebuild("FE_Details", 1, BuildSheet)) built++;
            if (Rebuild("FE_Lobby", 1, BuildLobbyBoard)) built++; // board 2b (BoardScreensBuilder.Online.cs)
            if (Rebuild("FE_Room", 1, BuildRoomBoard)) built++;
            BoardBackground();
            MenuPicture(); // BRAND-1: the main menu's picture (BoardScreensBuilder.Brand.cs)
            if (built > 0) AssetDatabase.SaveAssets();
            return built;
        }

        // Empties the screen prefab (keeping its root, so FrontEnd.prefab still holds it) and builds it again.
        private static bool Rebuild(string file, int version, System.Action<RectTransform> build)
        {
            string path = FrontEndBuilder.ScreensFolder + file + ".prefab";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[Boards] {path} is missing: run Console Cards > UI > Apply Kit Look first.");
                return false;
            }

            string label = BoardLabel + version;
            if (System.Array.IndexOf(AssetDatabase.GetLabels(asset), label) >= 0) return false;
            if (!AssetDatabase.IsValidFolder(BackupFolder.TrimEnd('/')))
            {
                AssetDatabase.CreateFolder(FrontEndBuilder.ScreensFolder.TrimEnd('/'), "Before boards");
            }

            string backup = BackupFolder + file + ".prefab";
            if (!File.Exists(backup)) AssetDatabase.CopyAsset(path, backup);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                }

                foreach (Component component in root.GetComponents<Component>())
                {
                    if (component is Graphic || component is LayoutGroup || component is ContentSizeFitter)
                    {
                        Object.DestroyImmediate(component);
                    }
                }

                RectTransform rect = (RectTransform)root.transform;
                UiKit.Stretch(rect);
                build(rect);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            System.Collections.Generic.List<string> labels =
                new System.Collections.Generic.List<string>(AssetDatabase.GetLabels(asset));
            labels.RemoveAll(l => l.StartsWith(BoardLabel, System.StringComparison.Ordinal));
            labels.Add(label);
            AssetDatabase.SetLabels(asset, labels.ToArray());
            return true;
        }

        // ---------- CREATE (board 4) ----------

        private static void BuildCreate(RectTransform page)
        {
            Text kicker = TextAt(page, "Kicker", 48, 100, 400, 20, medium, 13, Muted, TextAnchor.MiddleLeft, "CREATE");
            kicker.fontStyle = FontStyle.Bold;
            TextAt(page, "Title", 48, 118, 700, 40, bold, 30, Words, TextAnchor.MiddleLeft, "Make it yours");

            // MAKE A GAME
            RectTransform make = Box(page, "MakeGame", 48, 172, 720, 168, TealFill, Cyan, 2f);
            Button makeButton = make.gameObject.AddComponent<Button>();
            makeButton.targetGraphic = make.GetComponent<Image>();
            Stripe(make, 22, 18, 10, 132);
            RectTransform icon = Box(make, "Icon", 54, 52, 64, 64, Cyan, null, 0f);
            Glyph(icon, "Plus", Ink, 30);
            TextAt(make, "Title", 140, 46, 520, 34, bold, 24, Words, TextAnchor.MiddleLeft, "MAKE A GAME");
            Text body = TextAt(make, "Text", 140, 80, 520, 44, medium, 14, Soft, TextAnchor.UpperLeft,
                "Lay out an empty table with any pieces, then save it as a game in four steps: Deck · Cards · Deal · Save.");
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            TextAt(make, "Chevron", 670, 70, 30, 30, bold, 22, Cyan, TextAnchor.MiddleCenter, "›");

            // The editors.
            RectTransform tiles = Rect(page, "Tiles", 48, 364, 720, 150);
            Tile(tiles, "CardStudio", 0, "Cards", "CARD STUDIO", "Cards, their words, icons and pictures");
            Tile(tiles, "Decks", 245, "Cards", "DECKS", "Build, copy and count decks");
            Tile(tiles, "Table", 491, "Diamond", "TABLE", "Shape, size and edge");

            // YOUR GAMES
            RectTransform games = Box(page, "Games", 800, 172, 432, 432, Panel, Edge, 1f);
            Text heading = TextAt(games, "Heading", 21, 22, 140, 18, bold, 13, Soft, TextAnchor.MiddleLeft, "YOUR GAMES");
            heading.fontStyle = FontStyle.Bold;
            Line(games, "Rule", 133, 31, 248, 1, Edge);
            TextAt(games, "Marks", 390, 22, 24, 18, bold, 15, Orange, TextAnchor.MiddleRight, "///");
            RectTransform rows = Rect(games, "Rows", 20, 50, 392, 330);
            for (int i = 0; i < FrontEndView.MyGameRowCount; i++) GameRow(rows, i);
            Text empty = TextAt(games, "Empty", 21, 60, 390, 60, medium, 13, Muted, TextAnchor.UpperLeft,
                "Nothing made yet. MAKE A GAME lays out a table and saves it as a game of yours.");
            empty.horizontalOverflow = HorizontalWrapMode.Wrap;
            TextAt(games, "More", 21, 340, 390, 18, medium, 12, Muted, TextAnchor.MiddleLeft, string.Empty);
            Text foot = TextAt(games, "Note", 21, 368, 390, 44, medium, 12, Muted, TextAnchor.UpperLeft,
                "EDIT starts the game's table with the save steps open. The ⋯ menu has Copy, Rename and Delete (asks twice).");
            foot.horizontalOverflow = HorizontalWrapMode.Wrap;

            // BACK
            RectTransform footer = Rect(page, "Footer", 48, 642, 720, 46);
            RectTransform back = Box(footer, "Back", 0, 0, 94, 46, Panel, Edge, 1f);
            Button backButton = back.gameObject.AddComponent<Button>();
            backButton.targetGraphic = back.GetComponent<Image>();
            Box(back, "Dot", 19, 19, 8, 8, Muted, null, 0f);
            TextAt(back, "Label", 34, 0, 56, 46, bold, 13, Words, TextAnchor.MiddleLeft, "BACK");
            TextAt(footer, "Note", 109, 0, 560, 46, medium, 13, Muted, TextAnchor.MiddleLeft,
                "Each editor opens over this screen and comes back here when it closes.");
        }

        private static void Tile(RectTransform parent, string name, float x, string icon, string title, string text)
        {
            RectTransform tile = Box(parent, name, x, 0, 229, 150, Panel, Edge, 1f);
            Button button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tile.GetComponent<Image>();
            RectTransform box = Box(tile, "Icon", 19, 19, 42, 42, Well, Edge, 1f);
            Glyph(box, icon, Cyan, 22);
            TextAt(tile, "Title", 19, 74, 200, 22, bold, 17, Words, TextAnchor.MiddleLeft, title);
            Text line = TextAt(tile, "Text", 19, 100, 195, 36, medium, 13, Muted, TextAnchor.UpperLeft, text);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // A game you made: its name and detail, PLAY, EDIT, ⋯, the ⋯ menu (Copy, Rename, Delete) and a rename field.
        private static void GameRow(RectTransform rows, int index)
        {
            RectTransform row = Box(rows, "Row" + index, 0, index * 66, 390, 56, Row, Edge, 1f);
            TextAt(row, "Name", 14, 8, 200, 22, bold, 14, Words, TextAnchor.MiddleLeft, "[YOUR GAME NAME]");
            TextAt(row, "Detail", 14, 30, 200, 18, medium, 11, Muted, TextAnchor.MiddleLeft, "2–4 players · changed today");
            SmallButton(row, "Play", 213, 11, 57, 33, "PLAY", Cyan, Cyan);
            SmallButton(row, "Edit", 280, 11, 53, 33, "EDIT", Edge, Words);
            SmallButton(row, "More", 343, 11, 35, 33, "⋯", Edge, Words);

            RectTransform menu = Box(row, "Menu", 213, 11, 165, 33, Well, Edge, 1f);
            SmallButton(menu, "Copy", 2, 2, 50, 29, "COPY", Well, Words);
            SmallButton(menu, "Rename", 54, 2, 60, 29, "RENAME", Well, Words);
            SmallButton(menu, "Delete", 116, 2, 47, 29, "DELETE", Well, Orange);
            menu.gameObject.SetActive(false);

            RectTransform field = Box(row, "RenameField", 10, 6, 196, 26, Well, Cyan, 1f);
            InputField input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = field.GetComponent<Image>();
            Text typed = TextAt(field, "Text", 8, 0, 182, 26, bold, 13, Words, TextAnchor.MiddleLeft, string.Empty);
            typed.supportRichText = false;
            input.textComponent = typed;
            input.characterLimit = 40;
            input.lineType = InputField.LineType.SingleLine;
            field.gameObject.SetActive(false);
            row.gameObject.SetActive(false);
        }

        // ---------- parts, in board pixels from the parent's top left ----------

        internal static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x * S, -y * S);
            rect.sizeDelta = new Vector2(width * S, height * S);
            return rect;
        }

        // A rounded panel with an optional edge (the edge is an Outline, so the fill keeps the clicks).
        internal static RectTransform Box(Transform parent, string name, float x, float y, float width, float height,
            Color fill, Color? edge, float edgeWidth)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = fill;
            image.raycastTarget = true;
            if (edge.HasValue && edgeWidth > 0f)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = edge.Value;
                outline.effectDistance = new Vector2(edgeWidth * S, -edgeWidth * S);
                outline.useGraphicAlpha = false;
                Outline inner = rect.gameObject.AddComponent<Outline>();
                inner.effectColor = edge.Value;
                inner.effectDistance = new Vector2(-edgeWidth * S, edgeWidth * S);
                inner.useGraphicAlpha = false;
            }

            return rect;
        }

        internal static Text TextAt(Transform parent, string name, float x, float y, float width, float height, Font font,
            int size, Color colour, TextAnchor anchor, string words)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = Mathf.RoundToInt(size * S);
            text.color = colour;
            text.alignment = anchor;
            text.text = words;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        internal static void Line(Transform parent, string name, float x, float y, float width, float height, Color colour)
        {
            Image image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
        }

        internal static Button SmallButton(Transform parent, string name, float x, float y, float width, float height,
            string words, Color edge, Color colour)
        {
            RectTransform rect = Box(parent, name, x, y, width, height, Well, edge, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            TextAt(rect, "Label", 0, 0, width, height, bold, 11, colour, TextAnchor.MiddleCenter, words);
            return button;
        }

        // The orange striped bar on the left of a board card.
        internal static void Stripe(Transform parent, float x, float y, float width, float height)
        {
            Image image = Rect(parent, "Stripe", x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = stripes;
            image.type = stripes != null ? Image.Type.Tiled : Image.Type.Simple;
            image.color = stripes != null ? Color.white : Orange;
            image.raycastTarget = false;
        }

        // A kit icon (Content/UI/Figma/Kit/V5_I_*.png) in the middle of its box, or a letter when it is missing.
        internal static void Glyph(RectTransform box, string icon, Color colour, float size)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(V5Hud.DerivedPath + "V5_I_" + icon + ".png");
            RectTransform rect = (RectTransform)new GameObject("Glyph", typeof(RectTransform)).transform;
            rect.gameObject.layer = 5;
            rect.SetParent(box, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size * S, size * S);
            if (sprite != null)
            {
                Image image = rect.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.color = colour;
                image.preserveAspect = true;
                image.raycastTarget = false;
                return;
            }

            Text text = rect.gameObject.AddComponent<Text>();
            text.font = bold;
            text.fontSize = Mathf.RoundToInt(size * S * 0.8f);
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = icon == "Plus" ? "+" : icon.Substring(0, 1);
            text.raycastTarget = false;
        }

        /// <summary>The fonts and pictures the board parts use (run before building with them).</summary>
        internal static void Prepare()
        {
            bold = UiKit.RequireFont("Oxanium-Bold.ttf");
            medium = UiKit.RequireFont("Oxanium-Medium.ttf");
            rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            stripes = StripeSprite();
            check = CheckSprite();
        }

        internal static Sprite circle;
        internal static Sprite check;

        /// <summary>The orange striped picture (made by Prepare).</summary>
        internal static Sprite Stripes => stripes;
        private const string CheckPath = "Assets/ConsoleCards/Content/UI/Board/Check.png";

        // A white tick on clear, for the done steps (Oxanium has no ✓).
        private static Sprite CheckSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(CheckPath);
            if (existing != null) return existing;
            if (File.Exists(CheckPath)) return ImportAsSprite(CheckPath); // made, but not imported as a picture yet
            Directory.CreateDirectory(Path.GetDirectoryName(CheckPath));
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) texture.SetPixel(x, y, clear);
            }

            // Two strokes, 3 px thick: (7,16) to (13,10), then (13,10) to (25,22) (y up).
            for (float t = 0f; t <= 1f; t += 0.01f)
            {
                Dot(texture, Mathf.Lerp(7f, 13f, t), Mathf.Lerp(16f, 10f, t));
                Dot(texture, Mathf.Lerp(13f, 25f, t), Mathf.Lerp(10f, 22f, t));
            }

            File.WriteAllBytes(CheckPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return ImportAsSprite(CheckPath);
        }

        private static Sprite ImportAsSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Dot(Texture2D texture, float cx, float cy)
        {
            for (int y = -2; y <= 2; y++)
            {
                for (int x = -2; x <= 2; x++)
                {
                    if ((x * x) + (y * y) > 4) continue;
                    int px = Mathf.RoundToInt(cx) + x;
                    int py = Mathf.RoundToInt(cy) + y;
                    if (px >= 0 && py >= 0 && px < texture.width && py < texture.height) texture.SetPixel(px, py, Color.white);
                }
            }
        }

        // Orange and dark diagonal stripes, made once as a small tiling picture.
        private static Sprite StripeSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(StripePath);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(StripePath));
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color dark = Hex("7a3a14");
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, ((x + y) % size) < size / 2 ? Orange : dark);
                }
            }

            File.WriteAllBytes(StripePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(StripePath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(StripePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(StripePath);
        }

        private static Color Hex(string hex) => UiKit.Hex(hex);
    }
}
