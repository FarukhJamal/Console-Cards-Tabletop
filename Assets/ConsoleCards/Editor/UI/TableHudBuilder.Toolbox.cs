using System.Collections.Generic;
using System.IO;
using ConsoleCards.Editor.Toolbox;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// TOOL-B to design board 8 "Toolbox open" (owner, 2026-10-30: "everything the same as the reference"), measured
    /// from the board (1280 x 720, x1.5), in Final UI/Notices/TableToolbox:
    /// <list type="bullet">
    /// <item>TOOLBOX, Find a piece… and CLOSE × on top.</item>
    /// <item>Down the left the kinds of piece with their counts: DICE, RECENT (the last pieces you placed), CARDS &amp;
    /// DECKS, PAWNS &amp; TOKENS, BOARDS, CONSOLES, PILES &amp; HANDS, GAME BOXES (and OTHER and TABLE when the library
    /// has such pieces); at the bottom TABLE TOOLS: SELECT, DRAW, PING, MEASURE.</item>
    /// <item>The pieces as square tiles, four to a row: each die size is its own tile (d4 … d20), then Dice set…
    /// (all six at once); a card has its count.</item>
    /// <item>Under the tiles what the piece under the pointer is; while placing, a chip beside the pointer
    /// ("d6  click to place · Esc stops").</item>
    /// </list>
    /// The same ComponentToolboxView, so placing works as before. A Toolbox made before board 8 is replaced once (it is
    /// the builder's own); the tabs and tiles are made again from the component library on every run, like Console
    /// Cards > Toolbox > Sync Tiles from Library.
    /// </summary>
    public static partial class TableHudBuilder
    {
        public const string ToolboxPath = Folder + "TableToolbox.prefab";
        private const string ToolboxIcons = "Assets/ConsoleCards/Content/UI/Toolbox/Icons/";
        private const string ToolboxLabel = "ToolboxBoard8";

        private const float NavWidth = 285f;
        private const float Cell = 177f;
        private const float CellGap = 15f;
        private const float TileHeight = 168f;
        private const float CounterHeight = 58f;
        private const float ChipRowHeightB = 58f;

        private static readonly Color TileFill = Hex("292e2f");
        private static readonly Color TileSelected = Hex("1c3c3d");
        private static readonly Color TileEdge = Hex("424547");
        private static readonly Color TileEdgeHover = Hex("01f7f9");
        private static readonly Color Well8 = Hex("1c1f1f");
        private static readonly Color CatWords = Hex("b7c0c3");
        private static readonly Color Faint8 = Hex("6b7477");

        private struct ToolboxKindTab
        {
            public string Label;
            public ComponentCatalogKind[] Kinds;
        }

        // In board 8's order (RECENT goes in after DICE; GAME BOXES after these; then OTHER and TABLE, when any).
        private static readonly ToolboxKindTab[] KindTabs =
        {
            new ToolboxKindTab { Label = "DICE", Kinds = new[] { ComponentCatalogKind.Die } },
            new ToolboxKindTab { Label = "CARDS & DECKS", Kinds = new[] { ComponentCatalogKind.Card, ComponentCatalogKind.Deck } },
            new ToolboxKindTab { Label = "PAWNS & TOKENS", Kinds = new[] { ComponentCatalogKind.Pawn, ComponentCatalogKind.Token } },
            new ToolboxKindTab
            {
                Label = "BOARDS",
                Kinds = new[] { ComponentCatalogKind.GameBoard, ComponentCatalogKind.ControllerMappingBoard },
            },
            new ToolboxKindTab
            {
                Label = "CONSOLES",
                Kinds = new[] { ComponentCatalogKind.Console, ComponentCatalogKind.ControllerBox },
            },
            new ToolboxKindTab
            {
                Label = "PILES & HANDS",
                Kinds = new[] { ComponentCatalogKind.Stack, ComponentCatalogKind.DiscardPile, ComponentCatalogKind.Hand },
            },
            new ToolboxKindTab { Label = "OTHER", Kinds = null }, // every kind not in a tab above (Other, and any new kind)
        };

        private static readonly string[] DieNames =
        {
            "", "", "", "", "four-sided die", "", "six-sided die", "", "eight-sided die", "", "ten-sided die", "",
            "twelve-sided die", "", "", "", "", "", "", "", "twenty-sided die",
        };

        // One tile to make: its entry, and for a size tile its value and words.
        private struct TileSpec
        {
            public ComponentCatalogEntry Entry;
            public int FixedValue;
            public string Name;
            public string DetailTitle;
            public string IconFile;
        }

        /// <summary>Makes the board-8 Toolbox when it is missing (or older) and syncs its tiles with the library.</summary>
        private static void BuildToolboxB()
        {
            ComponentLibrary library = AssetDatabase.LoadAssetAtPath<ComponentLibrary>(ComponentToolboxBuilder.LibraryPath);
            if (library == null)
            {
                Debug.LogWarning($"[TOOL-B] No component library at {ComponentToolboxBuilder.LibraryPath}; the old Toolbox stays.");
                return;
            }

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ToolboxPath);
            if (existing != null && System.Array.IndexOf(AssetDatabase.GetLabels(existing), ToolboxLabel) < 0)
            {
                AssetDatabase.DeleteAsset(ToolboxPath); // made before board 8 (by this builder): built again
            }

            foreach (string icon in new[] { "ToolD4", "ToolD6", "ToolD8", "ToolD10", "ToolD12", "ToolD20" })
            {
                HudSprite(icon, Vector4.zero);
            }

            HudSprite("HudDashFrame", new Vector4(12f, 12f, 12f, 12f));
            if (!File.Exists(ToolboxPath))
            {
                GameObject made = new GameObject("TableToolbox", typeof(RectTransform));
                made.layer = 5;
                try
                {
                    Stretch(made.GetComponent<RectTransform>());
                    ComponentToolboxView view = made.AddComponent<ComponentToolboxView>();
                    BuildToolboxPanel(made.GetComponent<RectTransform>(), view);
                    SyncToolboxB(made, library);
                    PrefabUtility.SaveAsPrefabAsset(made, ToolboxPath);
                }
                finally
                {
                    Object.DestroyImmediate(made);
                }

                GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(ToolboxPath);
                if (saved != null) AssetDatabase.SetLabels(saved, new[] { ToolboxLabel });
                Debug.Log($"[TOOL-B] Made the board-8 Toolbox ({ToolboxPath}).");
            }
            else
            {
                GameObject root = PrefabUtility.LoadPrefabContents(ToolboxPath);
                try
                {
                    if (root.GetComponent<ComponentToolboxView>() == null || Find(root.transform, "PanelGroup/Panel/Body") == null)
                    {
                        Debug.LogWarning($"[TOOL-B] {ToolboxPath} is not a colour-B Toolbox; its tiles were not synced.");
                        return;
                    }

                    SyncToolboxB(root, library);
                    PrefabUtility.SaveAsPrefabAsset(root, ToolboxPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            SetToolboxPrefab(AssetDatabase.LoadAssetAtPath<ComponentToolboxView>(ToolboxPath));
        }

        /// <summary>Points the Toolbox entry of the UI catalog at this prefab (TOOL-B, or the old one at Turn Off).</summary>
        private static void SetToolboxPrefab(ComponentToolboxView prefab)
        {
            UiPrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<UiPrefabCatalog>(RealUiCatalogPath);
            if (catalog == null || prefab == null) return;
            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("id").stringValue != ComponentToolboxBuilder.ToolboxUiId) continue;
                entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
                return;
            }
        }

        // ---------- the panel (made once) ----------

        private static void BuildToolboxPanel(RectTransform root, ComponentToolboxView view)
        {
            // The old launcher; the HUD has its own Toolbox button, so it stays hidden (the view needs one).
            RectTransform open = Rect("OpenButton", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(30f, 30f), new Vector2(200f, 56f));
            Image openFill = Shape(open, Orange, rounded);
            openFill.raycastTarget = true;
            Button openButton = open.gameObject.AddComponent<Button>();
            openButton.targetGraphic = openFill;
            Label(Rect("Label", open, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), bold, 20,
                OrangeInk, TextAnchor.MiddleCenter, "+ TOOLBOX");
            open.gameObject.SetActive(false);

            RectTransform group = Rect("PanelGroup", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            RectTransform panel = Rect("Panel", group, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -144f), new Vector2(1110f, 906f));
            Plate(panel, Orange, Hex("222527"));

            // Header: TOOLBOX, Find a piece…, CLOSE ×.
            RectTransform header = Rect("Header", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(-48f, 60f));
            Row(header.gameObject, 0, 0, 0, 0, 18).childAlignment = TextAnchor.MiddleLeft;
            Text title = FitLabel(Child("Title", header), bold, 26, Words, "TOOLBOX");
            InputField search = SearchField(header);
            LayoutElement searchSize = Size(search.gameObject, -1f, 60f, 200f);
            searchSize.preferredWidth = -1f;
            searchSize.flexibleWidth = 1f;
            search.textComponent.fontSize = 21;
            Text placeholder = search.placeholder as Text;
            if (placeholder != null)
            {
                placeholder.text = "Find a piece…";
                placeholder.fontSize = 21;
            }

            Button close = Button8(header, "CloseButton", "CLOSE  ×", 150f);
            _ = title;

            // Body: the kinds on the left, the pieces on the right.
            RectTransform body = Rect("Body", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            body.offsetMin = new Vector2(24f, 24f);
            body.offsetMax = new Vector2(-24f, -105f);

            RectTransform nav = Rect("Nav", body, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                new Vector2(NavWidth, 0f));
            RectTransform tabs = Rect("Tabs", nav, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(0f, 600f));
            VerticalLayoutGroup tabColumn = Column(tabs.gameObject, 0, 0, 0, 0, 6);
            tabColumn.childForceExpandHeight = false;

            // TABLE TOOLS at the bottom of the kinds.
            RectTransform tools = Rect("TableTools", nav, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 117f));
            string[] toolNames = { "SELECT", "DRAW", "PING", "MEASURE" };
            TableToolKind[] toolKinds = { TableToolKind.Select, TableToolKind.Draw, TableToolKind.Ping, TableToolKind.Measure };
            Button[] toolButtons = new Button[toolNames.Length];
            for (int i = 0; i < toolNames.Length; i++)
            {
                float x = (i % 2) * ((NavWidth + 9f) * 0.5f);
                float y = i < 2 ? 63f : 0f;
                RectTransform at = Rect("Tool_" + toolNames[i], tools, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, y),
                    new Vector2((NavWidth - 9f) * 0.5f, 54f));
                toolButtons[i] = Button8At(at, toolNames[i]);
            }

            Label(Rect("TableToolsTitle", nav, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 123f), new Vector2(0f, 24f)), bold, 16, Muted, TextAnchor.LowerLeft, "TABLE TOOLS");

            RectTransform right = Rect("Right", body, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            right.offsetMin = new Vector2(NavWidth + 21f, 0f);
            right.offsetMax = Vector2.zero;

            RectTransform viewport = Rect("Viewport", right, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 351f));
            viewport.gameObject.AddComponent<RectMask2D>();
            CatchScroll(viewport);
            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 600f));
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            RectTransform chipRow = Rect("ChipRow", content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, ChipRowHeightB - 10f));
            Rect("Shelves", content, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // What the piece under the pointer is (the view puts it right under the tiles).
            RectTransform detail = Rect("Detail", right, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -372f), new Vector2(0f, 135f));
            Image detailFill = Shape(detail, Well8, rounded);
            Edge8(detail, Edge);
            _ = detailFill;
            Text detailText = Label(Rect("Text", detail, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-42f, -36f)), medium, 20, CatWords, TextAnchor.UpperLeft,
                "Pick a piece, then click the table to place it; keep clicking for more. Right-click or Esc stops.");
            detailText.supportRichText = true;
            detailText.lineSpacing = 1.1f;

            // The Hand switch (an Empty Table), above TABLE TOOLS.
            RectTransform handRow = Rect("HandRow", nav, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 162f), new Vector2(0f, 76f));
            Shape(handRow, RowFill, rounded);
            Label(Rect("Title", handRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f),
                new Vector2(-110f, 26f)), bold, 17, Words, TextAnchor.UpperLeft, "Hand");
            Text handHint = Label(Rect("Hint", handRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -38f), new Vector2(-110f, 30f)), medium, 13, Muted, TextAnchor.UpperLeft, "Cards you draw");
            RectTransform pill = Rect("Switch", handRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(86f, 40f));
            Image pillFill = Shape(pill, Teal, rounded);
            pillFill.raycastTarget = true;
            Button pillButton = pill.gameObject.AddComponent<Button>();
            pillButton.targetGraphic = pillFill;
            RectTransform knob = Rect("Knob", pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(20f, 0f), new Vector2(30f, 30f));
            Shape(knob, Words, circle);
            Text word = Label(Rect("Word", pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-13f, 0f), new Vector2(44f, 28f)), bold, 14, Words, TextAnchor.MiddleCenter, "ON");

            // While placing: a chip beside the pointer (the view moves it there).
            RectTransform placing = Rect("PlacingCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f, 42f));
            Image placingFill = Shape(placing, Hex("101314"), rounded);
            _ = placingFill;
            Edge8(placing, Cyan);
            Row(placing.gameObject, 15, 15, 0, 0, 0f).childAlignment = TextAnchor.MiddleLeft;
            Fit(placing.gameObject, true, false);
            Text placingSubtitle = FitLabel(Child("Subtitle", placing), bold, 18, Words,
                "<color=#06EFF0>d6</color>  click to place · Esc stops");
            placingSubtitle.raycastTarget = false;
            placing.gameObject.SetActive(false);
            // The old keys bar is not on board 8: an empty stand-in (the view shows and hides it).
            RectTransform placingControls = Rect("PlacingControls", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                Vector2.zero);
            placingControls.gameObject.SetActive(false);

            group.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("openButton").objectReferenceValue = openButton;
            so.FindProperty("panel").objectReferenceValue = group.gameObject;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("placingCard").objectReferenceValue = placing.gameObject;
            so.FindProperty("placingIcon").objectReferenceValue = null;
            so.FindProperty("placingSubtitle").objectReferenceValue = placingSubtitle;
            so.FindProperty("placingControls").objectReferenceValue = placingControls.gameObject;
            so.FindProperty("placingFormat").stringValue = "<color=#06EFF0>{0}</color>  click to place · Esc stops";
            so.FindProperty("placingFollowsPointer").boolValue = true;
            so.FindProperty("placingPointerOffset").vector2Value = new Vector2(40f, 0f);
            so.FindProperty("contentViewport").objectReferenceValue = viewport;
            so.FindProperty("viewportBottomWithSwitch").floatValue = 0f;
            so.FindProperty("viewportBottomWithoutSwitch").floatValue = 0f;
            so.FindProperty("detailBox").objectReferenceValue = detail;
            so.FindProperty("viewportMaxHeight").floatValue = 534f;
            so.FindProperty("detailGap").floatValue = 21f;
            so.FindProperty("tabFill").colorValue = new Color(0f, 0f, 0f, 0f);
            so.FindProperty("tabText").colorValue = CatWords;
            so.FindProperty("tabSelectedFill").colorValue = TileSelected;
            so.FindProperty("tabSelectedText").colorValue = Words;
            so.FindProperty("chipFill").colorValue = Button2;
            so.FindProperty("chipText").colorValue = Soft;
            so.FindProperty("chipSelectedFill").colorValue = Orange;
            so.FindProperty("chipSelectedText").colorValue = OrangeInk;
            so.FindProperty("searchField").objectReferenceValue = search;
            so.FindProperty("detailLabel").objectReferenceValue = detailText;
            so.FindProperty("detailHow").stringValue =
                "Click the table to place one; keep clicking for more. Right-click or Esc stops.";
            so.FindProperty("detailIdle").stringValue =
                "Pick a piece, then click the table to place it; keep clicking for more. Right-click or Esc stops.";
            so.FindProperty("flowTiles").boolValue = true;
            so.FindProperty("flowCell").floatValue = Cell;
            so.FindProperty("flowGap").floatValue = CellGap;
            so.FindProperty("flowColumns").intValue = 4;
            so.FindProperty("flowTop").floatValue = 0f;
            SerializedProperty toolButtonArray = so.FindProperty("tableToolButtons");
            SerializedProperty toolKindArray = so.FindProperty("tableToolKinds");
            toolButtonArray.arraySize = toolButtons.Length;
            toolKindArray.arraySize = toolKinds.Length;
            for (int i = 0; i < toolButtons.Length; i++)
            {
                toolButtonArray.GetArrayElementAtIndex(i).objectReferenceValue = toolButtons[i];
                toolKindArray.GetArrayElementAtIndex(i).enumValueIndex = (int)toolKinds[i];
            }

            SerializedProperty hand = so.FindProperty("handSwitch");
            AssignSwitch(hand, handRow, pillButton, pillFill, knob, word, handHint, "Cards you draw go to your hand",
                "Off: cards you draw stay on the table");
            hand.FindPropertyRelative("onFill").colorValue = Teal;
            hand.FindPropertyRelative("offFill").colorValue = SwitchOff;
            hand.FindPropertyRelative("onWordColor").colorValue = Words;
            hand.FindPropertyRelative("offWordColor").colorValue = Muted;
            hand.FindPropertyRelative("knobOffset").floatValue = 20f;
            hand.FindPropertyRelative("wordOffset").floatValue = 13f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A board-8 button (.ibtn): dark, with a grey edge, its words in capitals.
        private static Button Button8(Transform parent, string name, string words, float width)
        {
            RectTransform rect = Child(name, parent);
            Size(rect.gameObject, width, 54f, width);
            return Button8At(rect, words);
        }

        private static Button Button8At(RectTransform rect, string words)
        {
            Image fill = Shape(rect, Well8, rounded);
            fill.raycastTarget = true;
            Edge8(rect, Edge);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1.6f, 2.1f, 2.2f, 1f); // towards the teal of a hovered tile
            button.colors = colours;
            Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), bold, 18,
                Words, TextAnchor.MiddleCenter, words);
            return button;
        }

        // A 1.5-unit edge drawn on top of the panel (the panel's fill stays solid under it).
        private static Image Edge8(RectTransform rect, Color colour)
        {
            Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HudArt + "HudFrame.png");
            RectTransform edge = Rect("Edge", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image image = edge.gameObject.AddComponent<Image>();
            image.sprite = frameSprite;
            image.type = frameSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = frameSprite != null ? colour : new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
            edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return image;
        }

        // ---------- tabs and tiles (made again on every run) ----------

        private static void SyncToolboxB(GameObject root, ComponentLibrary library)
        {
            library.Validate();
            ComponentToolboxView view = root.GetComponent<ComponentToolboxView>();
            Transform tabsRoot = Find(root.transform, "PanelGroup/Panel/Body/Nav/Tabs");
            RectTransform content = (RectTransform)Find(root.transform, "PanelGroup/Panel/Body/Right/Viewport/Content");
            Transform chipRow = Find(content, "ChipRow");
            Transform shelvesRoot = Find(content, "Shelves");
            Clear(tabsRoot);
            Clear(chipRow);
            Clear(shelvesRoot);

            List<(string label, int count, int shelf, bool games)> tabs = new List<(string, int, int, bool)>();
            List<(ComponentCatalog catalog, GameObject root, GameObject empty, List<ToolboxEntryTile> tiles)> shelves =
                new List<(ComponentCatalog, GameObject, GameObject, List<ToolboxEntryTile>)>();
            List<TileSpec> everyTile = new List<TileSpec>();
            float tallest = 0f;

            // The kinds of piece from the Base and Controller boxes; then each game box (under its chip); then the table.
            for (int k = 0; k < KindTabs.Length; k++)
            {
                List<ComponentCatalogEntry> entries = new List<ComponentCatalogEntry>();
                ComponentCatalogKind[] kinds = KindTabs[k].Kinds ?? UnlistedKinds();
                CollectKinds(library.BaseBox, kinds, entries);
                CollectKinds(library.ControllerBox, kinds, entries);
                if (entries.Count == 0) continue;
                List<TileSpec> specs = Specs(entries, out int pieces);
                everyTile.AddRange(specs);
                tabs.Add((KindTabs[k].Label, pieces, shelves.Count, false));
                shelves.Add(Shelf(shelvesRoot, library.BaseBox, KindTabs[k].Label, specs, 0f, false, ref tallest));
            }

            List<(string label, int shelf)> chips = new List<(string, int)>();
            int gamePieces = 0;
            for (int g = 0; g < library.GameBoxes.Count; g++)
            {
                ComponentCatalog box = library.GameBoxes[g];
                List<ComponentCatalogEntry> entries = new List<ComponentCatalogEntry>();
                CollectKinds(box, null, entries);
                List<TileSpec> specs = Specs(entries, out int pieces);
                everyTile.AddRange(specs);
                gamePieces += pieces;
                chips.Add((box.DisplayName, shelves.Count));
                shelves.Add(Shelf(shelvesRoot, box, box.DisplayName, specs, ChipRowHeightB, false, ref tallest));
            }

            // GAME BOXES goes before OTHER (board 8 ends its list with it).
            int otherTab = tabs.FindIndex(t => t.label == "OTHER");
            if (chips.Count > 0)
            {
                (string, int, int, bool) games = ("GAME BOXES", gamePieces, -1, true);
                if (otherTab >= 0) tabs.Insert(otherTab, games);
                else tabs.Add(games);
            }

            if (library.Environment != null)
            {
                List<ComponentCatalogEntry> entries = new List<ComponentCatalogEntry>();
                CollectKinds(library.Environment, null, entries);
                if (entries.Count > 0)
                {
                    List<TileSpec> specs = Specs(entries, out int pieces);
                    everyTile.AddRange(specs);
                    tabs.Add(("TABLE", pieces, shelves.Count, false));
                    shelves.Add(Shelf(shelvesRoot, library.Environment, "Table", specs, 0f, false, ref tallest));
                }
            }

            // RECENT, second: a copy of every tile, each shown while its piece is among the last ones placed.
            int recentShelf = shelves.Count;
            shelves.Add(Shelf(shelvesRoot, library.BaseBox, "Recent", everyTile, 0f, true, ref tallest));
            tabs.Insert(Mathf.Min(1, tabs.Count), ("RECENT", 0, recentShelf, false));

            content.sizeDelta = new Vector2(0f, tallest + 20f);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("recentShelfIndex").intValue = recentShelf;
            SerializedProperty tabArray = so.FindProperty("tabs");
            tabArray.arraySize = tabs.Count;
            for (int i = 0; i < tabs.Count; i++)
            {
                RectTransform tab = Child($"Tab_{tabs[i].label}", tabsRoot);
                Image fill = Shape(tab, new Color(0f, 0f, 0f, 0f), rounded);
                fill.raycastTarget = true;
                Outline chosenEdge = tab.gameObject.AddComponent<Outline>(); // shows with the chosen tab's fill only
                chosenEdge.effectColor = TileEdgeHover;
                chosenEdge.effectDistance = new Vector2(1.5f, -1.5f);
                chosenEdge.useGraphicAlpha = true;
                Outline chosenEdge2 = tab.gameObject.AddComponent<Outline>();
                chosenEdge2.effectColor = TileEdgeHover;
                chosenEdge2.effectDistance = new Vector2(-1.5f, 1.5f);
                chosenEdge2.useGraphicAlpha = true;
                Button button = tab.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                Size(tab.gameObject, -1f, 60f);
                Text label = Label(Rect("Label", tab, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f),
                    new Vector2(-36f, 0f)), bold, 20, CatWords, TextAnchor.MiddleLeft, tabs[i].label);
                Label(Rect("Count", tab, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f),
                    new Vector2(50f, 0f)), medium, 16, Faint8, TextAnchor.MiddleRight, tabs[i].count.ToString());
                SerializedProperty element = tabArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("category").enumValueIndex = tabs[i].games
                    ? (int)ComponentCatalogCategory.GameBox
                    : (int)ComponentCatalogCategory.BaseBox;
                element.FindPropertyRelative("button").objectReferenceValue = button;
                element.FindPropertyRelative("fill").objectReferenceValue = fill;
                element.FindPropertyRelative("label").objectReferenceValue = label;
                element.FindPropertyRelative("chipRow").objectReferenceValue = tabs[i].games ? chipRow.gameObject : null;
                element.FindPropertyRelative("shelfIndex").intValue = tabs[i].shelf;
            }

            SerializedProperty chipArray = so.FindProperty("gameChips");
            chipArray.arraySize = chips.Count;
            float chipX = 0f;
            for (int i = 0; i < chips.Count; i++)
            {
                RectTransform chip = Rect($"Chip_{chips[i].label}", chipRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(chipX, 0f), new Vector2(0f, 42f));
                Image fill = Shape(chip, Button2, rounded);
                fill.raycastTarget = true;
                Button button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                Text label = Label(Rect("Label", chip, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                    Vector2.zero), bold, 15, Soft, TextAnchor.MiddleCenter, chips[i].label.ToUpperInvariant());
                float width = label.preferredWidth + 36f;
                chip.sizeDelta = new Vector2(width, 42f);
                chipX += width + 8f;
                SerializedProperty element = chipArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue = button;
                element.FindPropertyRelative("fill").objectReferenceValue = fill;
                element.FindPropertyRelative("label").objectReferenceValue = label;
                element.FindPropertyRelative("shelfIndex").intValue = chips[i].shelf;
            }

            SerializedProperty shelfArray = so.FindProperty("shelves");
            shelfArray.arraySize = shelves.Count;
            for (int i = 0; i < shelves.Count; i++)
            {
                SerializedProperty element = shelfArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("catalog").objectReferenceValue = shelves[i].catalog;
                element.FindPropertyRelative("root").objectReferenceValue = shelves[i].root;
                element.FindPropertyRelative("emptyState").objectReferenceValue = shelves[i].empty;
                SerializedProperty tiles = element.FindPropertyRelative("tiles");
                tiles.arraySize = shelves[i].tiles.Count;
                for (int t = 0; t < shelves[i].tiles.Count; t++)
                {
                    tiles.GetArrayElementAtIndex(t).objectReferenceValue = shelves[i].tiles[t];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The tiles for these entries: a die is a tile per size and a Dice set… tile; anything else is one tile.
        // `pieces` counts the tiles a player would call pieces (not Dice set…).
        private static List<TileSpec> Specs(List<ComponentCatalogEntry> entries, out int pieces)
        {
            List<TileSpec> specs = new List<TileSpec>();
            pieces = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                ComponentCatalogEntry entry = entries[i];
                List<int> sizes = entry.Kind == ComponentCatalogKind.Die ? DieSizesB(entry) : new List<int>();
                if (sizes.Count == 0)
                {
                    specs.Add(new TileSpec { Entry = entry, Name = entry.DisplayName });
                    pieces++;
                    continue;
                }

                for (int s = 0; s < sizes.Count; s++)
                {
                    int side = sizes[s];
                    string words = side < DieNames.Length && DieNames[side].Length > 0 ? DieNames[side] : $"{side}-sided die";
                    specs.Add(new TileSpec
                    {
                        Entry = entry,
                        FixedValue = side,
                        Name = $"d{side}",
                        DetailTitle = $"d{side} · {words}",
                        IconFile = $"ToolD{side}",
                    });
                    pieces++;
                }

                if (sizes.Count > 1)
                {
                    specs.Add(new TileSpec
                    {
                        Entry = entry,
                        FixedValue = -1,
                        Name = "Dice set…",
                        DetailTitle = "Dice set · d4, d6, d8, d10, d12 and d20 side by side",
                    });
                }
            }

            return specs;
        }

        // The kinds no tab names, for OTHER: so a kind added later still gets its tile.
        private static ComponentCatalogKind[] UnlistedKinds()
        {
            List<ComponentCatalogKind> rest = new List<ComponentCatalogKind>();
            foreach (ComponentCatalogKind kind in System.Enum.GetValues(typeof(ComponentCatalogKind)))
            {
                bool listed = false;
                for (int k = 0; k < KindTabs.Length && !listed; k++)
                {
                    listed = KindTabs[k].Kinds != null && System.Array.IndexOf(KindTabs[k].Kinds, kind) >= 0;
                }

                if (!listed) rest.Add(kind);
            }

            return rest.ToArray();
        }

        // The Toolbox entries of a catalog (of these kinds; null: all), in Toolbox order.
        private static void CollectKinds(ComponentCatalog catalog, ComponentCatalogKind[] kinds, List<ComponentCatalogEntry> into)
        {
            if (catalog == null) return;
            int start = into.Count;
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ComponentCatalogEntry entry = catalog.Entries[i];
                if (!entry.ShowInToolbox) continue;
                if (kinds != null && System.Array.IndexOf(kinds, entry.Kind) < 0) continue;
                into.Add(entry);
            }

            into.Sort(start, into.Count - start, Comparer<ComponentCatalogEntry>.Create((a, b) => a.ToolboxOrder.CompareTo(b.ToolboxOrder)));
        }

        private static (ComponentCatalog, GameObject, GameObject, List<ToolboxEntryTile>) Shelf(Transform shelvesRoot,
            ComponentCatalog catalog, string name, List<TileSpec> specs, float top, bool recentCopies, ref float tallest)
        {
            RectTransform shelf = Rect($"Shelf_{name}", shelvesRoot, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(0f, 10f));
            List<ToolboxEntryTile> tiles = new List<ToolboxEntryTile>();
            int column = 0;
            float y = 0f;
            float rowHeight = 0f;
            for (int i = 0; i < specs.Count; i++)
            {
                ToolboxEntryTile tile = TileB(shelf, specs[i], recentCopies, out int span, out float height);
                if (column + span > 4)
                {
                    column = 0;
                    y += rowHeight + CellGap;
                    rowHeight = 0f;
                }

                ((RectTransform)tile.transform).anchoredPosition = new Vector2(column * (Cell + CellGap), -y);
                rowHeight = Mathf.Max(rowHeight, height);
                column += span;
                tiles.Add(tile);
            }

            tallest = Mathf.Max(tallest, recentCopies ? (2f * TileHeight) + CellGap : top + y + rowHeight);
            RectTransform empty = Rect("Empty", shelf, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 120f));
            Label(empty, medium, 18, Muted, TextAnchor.MiddleCenter,
                recentCopies ? "The pieces you place show here." : $"No pieces in {name} match.");
            empty.gameObject.SetActive(tiles.Count == 0 || recentCopies);
            return (catalog, shelf.gameObject, empty.gameObject, tiles);
        }

        // One piece as a square tile: its picture and name (board 8); a card also has its count (two cells).
        private static ToolboxEntryTile TileB(RectTransform shelf, TileSpec spec, bool recentCopy, out int span,
            out float height)
        {
            ComponentCatalogEntry entry = spec.Entry;
            bool counter = entry.Kind == ComponentCatalogKind.Card;
            bool diceSet = spec.FixedValue < 0;
            span = counter ? 2 : 1;
            height = TileHeight + (counter ? CounterHeight : 0f);
            float width = (span * Cell) + ((span - 1) * CellGap);

            RectTransform rect = Rect($"Tile_{spec.Name}", shelf, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), Vector2.zero, new Vector2(width, height));
            Sprite dash = diceSet ? AssetDatabase.LoadAssetAtPath<Sprite>(HudArt + "HudDashFrame.png") : null;
            Image edge = Shape(Rect("Edge", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(3f, 3f)), TileEdge, dash != null ? dash : rounded);
            if (dash != null) edge.type = Image.Type.Sliced;
            Image fill = Shape(Rect("Fill", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                dash != null ? new Vector2(-3f, -3f) : Vector2.zero), TileFill, rounded);
            fill.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None; // the tile's own colours show hover and choice

            // The picture: the board's die shapes, the + of Dice set…, or the kind's icon.
            float pictureBottom = counter ? CounterHeight + 51f : 51f;
            RectTransform picture = Rect("Icon", rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, pictureBottom), new Vector2(60f, 60f));
            if (diceSet)
            {
                Label(picture, bold, 39, Cyan, TextAnchor.MiddleCenter, "+");
            }
            else
            {
                Sprite shape = spec.IconFile != null ? AssetDatabase.LoadAssetAtPath<Sprite>(HudArt + spec.IconFile + ".png") : null;
                Image icon = Shape(picture, shape != null ? Color.white : IconColourB(entry.Kind), null);
                icon.sprite = shape != null ? shape
                    : entry.Icon != null ? entry.Icon : AssetDatabase.LoadAssetAtPath<Sprite>(ToolboxIcons + IconFileB(entry.Kind));
                icon.preserveAspect = true;
            }

            Text nameLabel = Label(Rect("Name", rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, counter ? CounterHeight + 15f : 15f), new Vector2(-18f, 27f)), bold, 18,
                diceSet ? Muted : Words, TextAnchor.LowerCenter, spec.Name);
            nameLabel.resizeTextForBestFit = true;
            nameLabel.resizeTextMinSize = 12;
            nameLabel.resizeTextMaxSize = 18;

            ToolboxEntryTile tile = rect.gameObject.AddComponent<ToolboxEntryTile>();
            ToolboxTileHover hover = rect.gameObject.AddComponent<ToolboxTileHover>();
            SerializedObject hoverSo = new SerializedObject(hover);
            hoverSo.FindProperty("highlight").boolValue = true;
            hoverSo.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject so = new SerializedObject(tile);
            so.FindProperty("entryId").stringValue = entry.Id;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("shadow").objectReferenceValue = edge;
            so.FindProperty("fillColor").colorValue = TileFill;
            so.FindProperty("selectedFillColor").colorValue = TileSelected;
            so.FindProperty("shadowColor").colorValue = TileEdge;
            so.FindProperty("selectedShadowColor").colorValue = TileEdgeHover;
            so.FindProperty("choiceFillColor").colorValue = Button2;
            so.FindProperty("choiceTextColor").colorValue = Soft;
            so.FindProperty("choiceSelectedFillColor").colorValue = Cyan;
            so.FindProperty("choiceSelectedTextColor").colorValue = Ink;
            so.FindProperty("fixedValue").intValue = spec.FixedValue;
            so.FindProperty("detailTitle").stringValue = spec.DetailTitle ?? string.Empty;
            so.FindProperty("recentCopy").boolValue = recentCopy;
            so.FindProperty("choices").arraySize = 0;

            if (counter)
            {
                RectTransform row = Rect("Counter", rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    Vector2.zero, new Vector2(0f, CounterHeight));
                Shape(Rect("Rule", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                    new Vector2(-24f, 2f)), Edge, null);
                Text counterHint = Label(Rect("CounterHint", row, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f),
                    new Vector2(14f, 0f), new Vector2(-160f, 0f)), medium, 15, Soft, TextAnchor.MiddleLeft, "Places 1 loose card");
                Button minus = StepButton(row, "Minus", "−", -110f);
                Text count = Label(Rect("Count", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-72f, 0f), new Vector2(36f, 36f)), bold, 20, Words, TextAnchor.MiddleCenter, "1");
                Button plus = StepButton(row, "Plus", "+", -12f);
                so.FindProperty("minusButton").objectReferenceValue = minus;
                so.FindProperty("plusButton").objectReferenceValue = plus;
                so.FindProperty("countLabel").objectReferenceValue = count;
                so.FindProperty("counterHint").objectReferenceValue = counterHint;
                so.FindProperty("singleFormat").stringValue = "Places 1 loose card";
                so.FindProperty("pluralFormat").stringValue = "Places a deck of {0}";
                so.FindProperty("minimumCount").intValue = 1;
                so.FindProperty("maximumCount").intValue = 99;
                so.FindProperty("defaultCount").intValue = 1;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            if (recentCopy) rect.gameObject.SetActive(false); // shown by the view once its piece is recent
            return tile;
        }

        private static Button StepButton(RectTransform row, string name, string glyph, float right)
        {
            RectTransform rect = Rect(name, row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(right, 0f), new Vector2(36f, 36f));
            Image fill = Shape(rect, Button2, rounded);
            fill.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            Label(Rect("Glyph", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), bold, 20,
                Words, TextAnchor.MiddleCenter, glyph);
            return button;
        }

        private static List<int> DieSizesB(ComponentCatalogEntry entry)
        {
            List<int> sizes = new List<int>();
            for (int i = 0; i < entry.Definitions.Count; i++)
            {
                if (entry.Definitions[i].Role == ComponentDefinitionLink.ShapeRole
                    && entry.Definitions[i].Definition is PhysicalDieDefinition die
                    && !sizes.Contains(die.SideCount))
                {
                    sizes.Add(die.SideCount);
                }
            }

            sizes.Sort();
            return sizes;
        }

        private static Color IconColourB(ComponentCatalogKind kind)
        {
            switch (kind)
            {
                case ComponentCatalogKind.Pawn:
                case ComponentCatalogKind.Token:
                case ComponentCatalogKind.Die:
                    return Cyan;
                case ComponentCatalogKind.Console:
                case ComponentCatalogKind.ControllerBox:
                    return Orange;
                default:
                    return Hex("f2a81d");
            }
        }

        private static string IconFileB(ComponentCatalogKind kind)
        {
            switch (kind)
            {
                case ComponentCatalogKind.Deck: return "IconDeck.png";
                case ComponentCatalogKind.Stack: return "IconStack.png";
                case ComponentCatalogKind.DiscardPile: return "IconDiscardPile.png";
                case ComponentCatalogKind.Pawn: return "IconPawn.png";
                case ComponentCatalogKind.Token: return "IconToken.png";
                case ComponentCatalogKind.Die: return "IconDie.png";
                case ComponentCatalogKind.Console: return "IconConsole.png";
                default: return "IconCard.png";
            }
        }

        private static Transform Find(Transform parent, string path)
        {
            return parent == null ? null : parent.Find(path);
        }

        private static void Clear(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }
    }
}
