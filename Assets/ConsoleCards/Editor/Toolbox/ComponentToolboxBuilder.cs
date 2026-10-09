using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.Toolbox
{
    /// <summary>
    /// Builds and syncs the authored Toolbox prefab from the component library (doc 22, C2a). Everything is
    /// made at edit time and saved as a prefab you can restyle; nothing is built at runtime.
    /// Build: makes the whole prefab (panel, header, tabs, placing card, open button and the templates).
    /// Sync: regenerates only the tabs, game chips and tiles from the library, by cloning the templates inside
    /// the prefab ("Templates"), so restyle a template and Sync to apply it everywhere.
    /// </summary>
    [InitializeOnLoad]
    public static class ComponentToolboxBuilder
    {
        public const string PrefabPath = "Assets/ConsoleCards/Content/Prefabs/Real/UI/ComponentToolbox.prefab";
        public const string UiCatalogPath = "Assets/ConsoleCards/Content/Prefabs/Real/UI/RealUiPrefabCatalog.asset";
        public const string LibraryPath = "Assets/ConsoleCards/Content/Catalogs/ComponentLibrary.asset";
        public const string ToolboxUiId = "platform.component-toolbox";
        private const string SpritePath = "Assets/ConsoleCards/Content/UI/Toolbox/Sprites/";
        private const string IconPath = "Assets/ConsoleCards/Content/UI/Toolbox/Icons/";
        private const string FontPath = "Assets/ConsoleCards/Content/UI/Fonts/";

        // Palette (from the approved mockup).

        // Layout, in Canvas reference units (1920 x 1080): the mockup at 1440 x 900 scaled by 1.2.
        private const float Margin = 29f;
        private const float PanelWidth = 564f;
        private const float Border = 5f;
        private const float HeaderHeight = 100f;
        private const float TabStripHeight = 62f;
        private const float FooterHeight = 52f;
        private const float SwitchRowHeight = 72f;
        private const float Pad = 19f;
        private const float Gap = 14f;
        private const float ChipRowHeight = 60f;
        private const float TileBaseHeight = 100f;
        private const float TileCounterHeight = 168f;
        private const float TileChoicesHeight = 215f;
        // Top of the open button and Placing card: below the session bar (86 high at the margin) and its shadow.
        private const float BelowSessionBar = Margin + 86f + 20f;

        private static Font titleFont;
        private static Font bodyFont;
        private static Font boldFont;

        static ComponentToolboxBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        [MenuItem("Console Cards/Toolbox/Build Toolbox Prefab")]
        public static void Build()
        {
            PrepareArt();
            ComponentLibrary library = LoadLibrary();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject root = new GameObject("ComponentToolbox", typeof(RectTransform));
            root.layer = 5;
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                ComponentToolboxView view = root.AddComponent<ComponentToolboxView>();
                BuildStatic(root.transform, view);
                Sync(root, library);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssignToUiCatalog();
            AssignCatalogIcons(library);
            Debug.Log($"[Toolbox] Built {PrefabPath} from the component library.");
        }

        [MenuItem("Console Cards/Toolbox/Sync Tiles from Library")]
        public static void SyncFromLibrary()
        {
            if (!File.Exists(PrefabPath))
            {
                Build();
                return;
            }

            PrepareArt();
            ComponentLibrary library = LoadLibrary();
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Sync(root, library);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssignToUiCatalog();
            AssignCatalogIcons(library);
            Debug.Log($"[Toolbox] Synced the tiles of {PrefabPath} with the component library.");
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(PrefabPath))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<ComponentLibrary>(LibraryPath) == null)
            {
                return;
            }

            Build();
        }

        // ---------- static parts (built once; restyle freely) ----------

        private static void BuildStatic(Transform root, ComponentToolboxView view)
        {
            // Open button (HUD), top left under the session bar (UI-1).
            RectTransform open = Rect("OpenButton", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -BelowSessionBar), new Vector2(250f, 62f));
            Image openShadow = Img(Rect("Shadow", open, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(6f, -6f), Vector2.zero),
                "PillOutlined", Ink);
            openShadow.raycastTarget = false;
            Image openFill = Img(Rect("Fill", open, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                "PillOutlined", Orange);
            Button openButton = open.gameObject.AddComponent<Button>();
            openButton.targetGraphic = openFill;
            Label(Rect("Label", open, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                titleFont, 24, Cream, TextAnchor.MiddleCenter, "+ TOOLBOX");

            // Panel group (panel and its offset shadow open and close together), on the left below the session bar.
            RectTransform group = Rect("PanelGroup", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform panelShadow = Rect("PanelShadow", group, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero);
            panelShadow.offsetMin = new Vector2(Margin + 10f, Margin - 10f);
            panelShadow.offsetMax = new Vector2(Margin + 10f + PanelWidth, -BelowSessionBar - 10f);
            Img(panelShadow, "PanelOutlined", Ink).raycastTarget = false;
            RectTransform panel = Rect("Panel", group, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, Vector2.zero);
            panel.offsetMin = new Vector2(Margin, Margin);
            panel.offsetMax = new Vector2(Margin + PanelWidth, -BelowSessionBar);
            Img(panel, "PanelOutlined", Cream);

            // Header.
            RectTransform header = TopBand("Header", panel, Border, HeaderHeight);
            Img(header, "TopRounded", Orange);
            Label(Rect("Title", header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(26f, -16f),
                new Vector2(-120f, 40f)), titleFont, 34, Cream, TextAnchor.UpperLeft, "TOOLBOX");
            Label(Rect("Subtitle", header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(26f, -58f),
                new Vector2(-120f, 26f)), bodyFont, 17, Cream, TextAnchor.UpperLeft, "Pick a piece, then click the table to place it");
            RectTransform close = Rect("CloseButton", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-22f, 0f), new Vector2(53f, 53f));
            Image closeFill = Img(close, "ButtonOutlined", Cream);
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeFill;
            Img(Rect("Icon", close, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(30f, 30f)), null, Ink, IconPath + "IconClose.png").raycastTarget = false;
            Img(TopBand("HeaderRule", panel, Border + HeaderHeight, Border), "Solid", Ink).raycastTarget = false;

            // Tab strip (tabs are generated by Sync).
            float stripTop = Border + HeaderHeight + Border;
            RectTransform strip = TopBand("TabStrip", panel, stripTop, TabStripHeight);
            Img(strip, "Solid", CreamDark).raycastTarget = false;
            Img(TopBand("TabRule", panel, stripTop + TabStripHeight, 4f), "Solid", Ink).raycastTarget = false;
            Rect("Tabs", strip, Vector2.zero, Vector2.one, new Vector2(0f, 0f), Vector2.zero, Vector2.zero);

            // Content (chips row and shelves are generated by Sync), scrollable.
            float contentTop = stripTop + TabStripHeight + 4f;
            RectTransform viewport = Rect("Viewport", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.offsetMin = new Vector2(Border, Border + FooterHeight);
            viewport.offsetMax = new Vector2(-Border, -contentTop);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 900f));
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            Rect("ChipRow", content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(Pad, -16f),
                new Vector2(-2f * Pad, 46f));
            Rect("Shelves", content, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Footer with the controls.
            RectTransform footer = Rect("Footer", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, Border), new Vector2(-2f * Border, FooterHeight));
            Img(footer, "BottomRounded", Ink).raycastTarget = false;
            Label(Rect("Controls", footer, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(-30f, 0f)),
                bodyFont, 17, Cream, TextAnchor.MiddleLeft,
                Key("Click") + " a piece    " + Key("Click") + " the table to place    " + Key("Wheel") + " rotate    "
                + Key("Right-click") + " cancel");

            // Footer Hand switch (Empty Table, doc 22 H-E): shown only when the session has a switchable Hand;
            // the view then raises the content's bottom edge above it.
            RectTransform handRow = Rect("HandSwitchRow", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, Border + FooterHeight), new Vector2(-2f * Border, SwitchRowHeight));
            Img(handRow, "Solid", CreamDark).raycastTarget = false;
            Img(Rect("Rule", handRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(0f, 4f)), "Solid", Ink).raycastTarget = false;
            Label(Rect("Title", handRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(22f, -12f),
                new Vector2(-160f, 26f)), boldFont, 19, Ink, TextAnchor.UpperLeft, "Hand");
            Text handHint = Label(Rect("Hint", handRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -40f), new Vector2(-160f, 24f)), bodyFont, 15, Muted, TextAnchor.UpperLeft,
                "Your private cards at the bottom of the screen");
            RectTransform handSwitch = Rect("Switch", handRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-22f, -2f), new Vector2(91f, 46f));
            Image handSwitchFill = Img(handSwitch, "PillOutlined", Go);
            Button handSwitchButton = handSwitch.gameObject.AddComponent<Button>();
            handSwitchButton.targetGraphic = handSwitchFill;
            RectTransform handKnob = Rect("Knob", handSwitch, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(22f, 0f), new Vector2(32f, 32f));
            Img(handKnob, "ButtonOutlined", Cream).raycastTarget = false;
            Text handWord = Label(Rect("Word", handSwitch, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-14f, 0f), new Vector2(46f, 30f)), titleFont, 15, Cream, TextAnchor.MiddleCenter, "ON");
            handRow.gameObject.SetActive(false);

            // Placing card (top left under the session bar, where the open button was) and placing controls (bottom centre).
            RectTransform placing = Rect("PlacingCard", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -BelowSessionBar), new Vector2(PanelWidth, 91f));
            Img(Rect("Shadow", placing, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(7f, -7f), Vector2.zero),
                "PanelOutlined", Ink).raycastTarget = false;
            Img(Rect("Fill", placing, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                "PanelOutlined", Cream).raycastTarget = false;
            RectTransform placingIconBox = Rect("IconBox", placing, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(22f, 0f), new Vector2(53f, 53f));
            Img(placingIconBox, "ButtonOutlined", Mustard).raycastTarget = false;
            Image placingIcon = Img(Rect("Icon", placingIconBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(34f, 34f)), null, Ink, IconPath + "IconDeck.png");
            placingIcon.raycastTarget = false;
            Label(Rect("Title", placing, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(91f, -18f),
                new Vector2(-110f, 26f)), titleFont, 19, Ink, TextAnchor.UpperLeft, "PLACING");
            Text placingSubtitle = Label(Rect("Subtitle", placing, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(91f, -46f), new Vector2(-110f, 28f)), bodyFont, 18, Ink, TextAnchor.UpperLeft, "Deck of 5 cards · rotation 0°");
            RectTransform placingControls = Rect("PlacingControls", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(640f, 56f));
            Img(placingControls, "ButtonOutlined", Ink).raycastTarget = false;
            Label(Rect("Controls", placingControls, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                bodyFont, 18, Cream, TextAnchor.MiddleCenter,
                Key("Left-click") + " place     " + Key("Wheel") + " rotate     " + Key("Right-click / Esc") + " cancel");

            BuildTemplates(root);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("openButton").objectReferenceValue = openButton;
            so.FindProperty("panel").objectReferenceValue = group.gameObject;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("placingCard").objectReferenceValue = placing.gameObject;
            so.FindProperty("placingIcon").objectReferenceValue = placingIcon;
            so.FindProperty("placingSubtitle").objectReferenceValue = placingSubtitle;
            so.FindProperty("placingControls").objectReferenceValue = placingControls.gameObject;
            so.FindProperty("tabFill").colorValue = TabIdle;
            so.FindProperty("tabText").colorValue = Muted;
            so.FindProperty("tabSelectedFill").colorValue = Cream;
            so.FindProperty("tabSelectedText").colorValue = Ink;
            so.FindProperty("chipFill").colorValue = Cream;
            so.FindProperty("chipText").colorValue = Ink;
            so.FindProperty("chipSelectedFill").colorValue = Teal;
            so.FindProperty("chipSelectedText").colorValue = Cream;
            SerializedProperty handSwitchProperty = so.FindProperty("handSwitch");
            handSwitchProperty.FindPropertyRelative("row").objectReferenceValue = handRow.gameObject;
            handSwitchProperty.FindPropertyRelative("button").objectReferenceValue = handSwitchButton;
            handSwitchProperty.FindPropertyRelative("fill").objectReferenceValue = handSwitchFill;
            handSwitchProperty.FindPropertyRelative("knob").objectReferenceValue = handKnob;
            handSwitchProperty.FindPropertyRelative("word").objectReferenceValue = handWord;
            handSwitchProperty.FindPropertyRelative("hint").objectReferenceValue = handHint;
            handSwitchProperty.FindPropertyRelative("onFill").colorValue = Go;
            handSwitchProperty.FindPropertyRelative("offFill").colorValue = TabIdle;
            handSwitchProperty.FindPropertyRelative("onWordColor").colorValue = Cream;
            handSwitchProperty.FindPropertyRelative("offWordColor").colorValue = Muted;
            handSwitchProperty.FindPropertyRelative("knobOffset").floatValue = 22f;
            handSwitchProperty.FindPropertyRelative("wordOffset").floatValue = 14f;
            handSwitchProperty.FindPropertyRelative("onHint").stringValue = "Your private cards at the bottom of the screen";
            handSwitchProperty.FindPropertyRelative("offHint").stringValue = "No hand: every card stays on the table";
            so.FindProperty("contentViewport").objectReferenceValue = viewport;
            so.FindProperty("viewportBottomWithSwitch").floatValue = Border + FooterHeight + SwitchRowHeight;
            so.FindProperty("viewportBottomWithoutSwitch").floatValue = Border + FooterHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Templates (inactive): Sync clones these, so restyling a template restyles every tile, tab or chip.
        private static void BuildTemplates(Transform root)
        {
            RectTransform templates = Rect("Templates", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            templates.gameObject.SetActive(false);

            // Tab.
            RectTransform tab = Rect("TabTemplate", templates, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                Vector2.zero, new Vector2(150f, 50f));
            Image tabFill = Img(tab, "TabOutlined", TabIdle);
            tab.gameObject.AddComponent<Button>().targetGraphic = tabFill;
            Label(Rect("Label", tab, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), Vector2.zero),
                boldFont, 17, Muted, TextAnchor.MiddleCenter, "TAB");

            // Game chip.
            RectTransform chip = Rect("ChipTemplate", templates, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(150f, 46f));
            Image chipFill = Img(chip, "PillOutlined", Cream);
            chip.gameObject.AddComponent<Button>().targetGraphic = chipFill;
            Label(Rect("Label", chip, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                boldFont, 17, Ink, TextAnchor.MiddleCenter, "Game");

            // Empty state.
            RectTransform empty = Rect("EmptyTemplate", templates, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(-2f * Pad, 220f));
            Img(empty, "FrameOnly", Rule).raycastTarget = false;
            Label(Rect("Title", empty, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f),
                new Vector2(-40f, 30f)), titleFont, 22, Ink, TextAnchor.UpperCenter, "EMPTY BOX");
            Label(Rect("Message", empty, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f),
                new Vector2(-80f, 80f)), bodyFont, 18, Muted, TextAnchor.UpperCenter, "No pieces in this box yet.");

            // Tile with every optional part; Sync removes the parts a tile does not use.
            RectTransform tile = Rect("TileTemplate", templates, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(251f, TileChoicesHeight));
            Image tileShadow = Img(Rect("Shadow", tile, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(5f, -5f), Vector2.zero),
                "TileOutlined", Ink);
            tileShadow.raycastTarget = false;
            Image tileFill = Img(Rect("Fill", tile, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                "TileOutlined", Cream);
            Button tileButton = tile.gameObject.AddComponent<Button>();
            tileButton.targetGraphic = tileFill;
            RectTransform iconBox = Rect("IconBox", tile, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(15f, -15f), new Vector2(67f, 67f));
            Img(iconBox, "BoxOutlined", Mustard).raycastTarget = false;
            Img(Rect("Icon", iconBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(40f, 40f)), null, Ink, IconPath + "IconCard.png").raycastTarget = false;
            Label(Rect("Name", tile, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(96f, -16f),
                new Vector2(-104f, 30f)), boldFont, 22, Ink, TextAnchor.UpperLeft, "Name");
            Label(Rect("Hint", tile, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(96f, -46f),
                new Vector2(-104f, 44f)), bodyFont, 15, Muted, TextAnchor.UpperLeft, "Hint");

            RectTransform counter = Rect("Counter", tile, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -TileBaseHeight), new Vector2(0f, TileCounterHeight - TileBaseHeight));
            DashRule(counter);
            Label(Rect("CounterHint", counter, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(15f, -6f),
                new Vector2(-170f, -12f)), bodyFont, 15, Muted, TextAnchor.MiddleLeft, "Places 1 loose card");
            SmallButton(counter, "Minus", "−", new Vector2(-114f, -6f));
            Label(Rect("Count", counter, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-75f, -6f),
                new Vector2(40f, 41f)), titleFont, 24, Ink, TextAnchor.MiddleCenter, "1");
            SmallButton(counter, "Plus", "+", new Vector2(-15f, -6f));

            RectTransform choices = Rect("Choices", tile, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -TileBaseHeight), new Vector2(0f, TileChoicesHeight - TileBaseHeight));
            DashRule(choices);
            RectTransform choiceChip = Rect("ChoiceTemplate", choices, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(15f, -14f), new Vector2(64f, 41f));
            Image choiceFill = Img(choiceChip, "ChipOutlined", Cream);
            choiceChip.gameObject.AddComponent<Button>().targetGraphic = choiceFill;
            Label(Rect("Label", choiceChip, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                boldFont, 17, Ink, TextAnchor.MiddleCenter, "d6");

            ToolboxEntryTile tileView = tile.gameObject.AddComponent<ToolboxEntryTile>();
            SerializedObject so = new SerializedObject(tileView);
            so.FindProperty("button").objectReferenceValue = tileButton;
            so.FindProperty("fill").objectReferenceValue = tileFill;
            so.FindProperty("shadow").objectReferenceValue = tileShadow;
            so.FindProperty("fillColor").colorValue = Cream;
            so.FindProperty("selectedFillColor").colorValue = Selected;
            so.FindProperty("shadowColor").colorValue = Ink;
            so.FindProperty("selectedShadowColor").colorValue = Orange;
            so.FindProperty("choiceFillColor").colorValue = Cream;
            so.FindProperty("choiceTextColor").colorValue = Ink;
            so.FindProperty("choiceSelectedFillColor").colorValue = Teal;
            so.FindProperty("choiceSelectedTextColor").colorValue = Cream;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- generated parts (Sync) ----------

        private static void Sync(GameObject root, ComponentLibrary library)
        {
            LoadFonts();
            library.Validate();
            ComponentToolboxView view = root.GetComponent<ComponentToolboxView>();
            Transform templates = Find(root.transform, "Templates");
            Transform panel = Find(root.transform, "PanelGroup/Panel");
            Transform tabsRoot = Find(panel, "TabStrip/Tabs");
            Transform content = Find(panel, "Viewport/Content");
            Transform chipRow = Find(content, "ChipRow");
            Transform shelvesRoot = Find(content, "Shelves");
            ClearChildren(tabsRoot);
            ClearChildren(chipRow);
            ClearChildren(shelvesRoot);

            List<ComponentCatalog> shelfCatalogs = new List<ComponentCatalog>();
            List<GameObject> shelfRoots = new List<GameObject>();
            List<GameObject> shelfEmpties = new List<GameObject>();
            List<List<ToolboxEntryTile>> shelfTiles = new List<List<ToolboxEntryTile>>();
            float tallest = 0f;

            void AddShelf(ComponentCatalog catalog, float top)
            {
                GameObject shelf = new GameObject($"Shelf_{catalog.name}", typeof(RectTransform));
                RectTransform shelfRect = shelf.GetComponent<RectTransform>();
                shelfRect.SetParent(shelvesRoot, false);
                Stretch(shelfRect);
                List<ToolboxEntryTile> tiles = new List<ToolboxEntryTile>();
                float height = BuildTiles(templates, shelfRect, catalog, top, tiles);
                GameObject empty = UnityEngine.Object.Instantiate(Find(templates, "EmptyTemplate").gameObject, shelfRect, false);
                empty.name = "Empty";
                empty.SetActive(tiles.Count == 0);
                RectTransform emptyRect = empty.GetComponent<RectTransform>();
                emptyRect.anchoredPosition = new Vector2(0f, -top);
                Find(empty.transform, "Message").GetComponent<Text>().text = $"No pieces in {catalog.DisplayName} yet.";
                tallest = Mathf.Max(tallest, Mathf.Max(height, top + 240f));
                shelfCatalogs.Add(catalog);
                shelfRoots.Add(shelf);
                shelfEmpties.Add(empty);
                shelfTiles.Add(tiles);
            }

            // Shelves: Base, Controller, each Game box (below the chip row), Environment.
            AddShelf(library.BaseBox, Pad);
            AddShelf(library.ControllerBox, Pad);
            List<(string label, int shelf)> chips = new List<(string, int)>();
            for (int i = 0; i < library.GameBoxes.Count; i++)
            {
                chips.Add((library.GameBoxes[i].DisplayName, shelfCatalogs.Count));
                AddShelf(library.GameBoxes[i], Pad + ChipRowHeight);
            }

            int environmentShelf = -1;
            if (library.Environment != null)
            {
                environmentShelf = shelfCatalogs.Count;
                AddShelf(library.Environment, Pad);
            }

            RectTransform contentRect = (RectTransform)content;
            contentRect.sizeDelta = new Vector2(0f, tallest + Pad);

            // Tabs.
            List<(string label, ComponentCatalogCategory category, int shelf, bool games)> tabs =
                new List<(string, ComponentCatalogCategory, int, bool)>
                {
                    ("BASE BOX", ComponentCatalogCategory.BaseBox, 0, false),
                    ("CONTROLLER BOX", ComponentCatalogCategory.ControllerBox, 1, false),
                    ("GAMES", ComponentCatalogCategory.GameBox, -1, true),
                };
            if (environmentShelf >= 0)
            {
                tabs.Add(("ENVIRONMENT", ComponentCatalogCategory.Environment, environmentShelf, false));
            }

            SerializedObject so = new SerializedObject(view);
            SerializedProperty tabArray = so.FindProperty("tabs");
            tabArray.arraySize = tabs.Count;
            float x = Pad;
            for (int i = 0; i < tabs.Count; i++)
            {
                GameObject tab = UnityEngine.Object.Instantiate(Find(templates, "TabTemplate").gameObject, tabsRoot, false);
                tab.name = $"Tab_{tabs[i].label}";
                Text label = Find(tab.transform, "Label").GetComponent<Text>();
                label.text = tabs[i].label;
                float width = TextWidth(label, tabs[i].label) + 34f;
                RectTransform tabRect = tab.GetComponent<RectTransform>();
                tabRect.anchoredPosition = new Vector2(x, 0f);
                tabRect.sizeDelta = new Vector2(width, tabRect.sizeDelta.y);
                x += width + 7f;
                SerializedProperty element = tabArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("category").enumValueIndex = (int)tabs[i].category;
                element.FindPropertyRelative("button").objectReferenceValue = tab.GetComponent<Button>();
                element.FindPropertyRelative("fill").objectReferenceValue = tab.GetComponent<Image>();
                element.FindPropertyRelative("label").objectReferenceValue = label;
                element.FindPropertyRelative("chipRow").objectReferenceValue = tabs[i].games ? chipRow.gameObject : null;
                element.FindPropertyRelative("shelfIndex").intValue = tabs[i].shelf;
            }

            // Game chips.
            SerializedProperty chipArray = so.FindProperty("gameChips");
            chipArray.arraySize = chips.Count;
            float chipX = 0f;
            for (int i = 0; i < chips.Count; i++)
            {
                GameObject chip = UnityEngine.Object.Instantiate(Find(templates, "ChipTemplate").gameObject, chipRow, false);
                chip.name = $"Chip_{chips[i].label}";
                Text label = Find(chip.transform, "Label").GetComponent<Text>();
                label.text = chips[i].label;
                float width = TextWidth(label, chips[i].label) + 34f;
                RectTransform chipRect = chip.GetComponent<RectTransform>();
                chipRect.anchoredPosition = new Vector2(chipX, 0f);
                chipRect.sizeDelta = new Vector2(width, chipRect.sizeDelta.y);
                chipX += width + 10f;
                SerializedProperty element = chipArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue = chip.GetComponent<Button>();
                element.FindPropertyRelative("fill").objectReferenceValue = chip.GetComponent<Image>();
                element.FindPropertyRelative("label").objectReferenceValue = label;
                element.FindPropertyRelative("shelfIndex").intValue = chips[i].shelf;
            }

            // Shelves.
            SerializedProperty shelfArray = so.FindProperty("shelves");
            shelfArray.arraySize = shelfCatalogs.Count;
            for (int i = 0; i < shelfCatalogs.Count; i++)
            {
                SerializedProperty element = shelfArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("catalog").objectReferenceValue = shelfCatalogs[i];
                element.FindPropertyRelative("root").objectReferenceValue = shelfRoots[i];
                element.FindPropertyRelative("emptyState").objectReferenceValue = shelfEmpties[i];
                SerializedProperty tiles = element.FindPropertyRelative("tiles");
                tiles.arraySize = shelfTiles[i].Count;
                for (int t = 0; t < shelfTiles[i].Count; t++)
                {
                    tiles.GetArrayElementAtIndex(t).objectReferenceValue = shelfTiles[i][t];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Two columns in Toolbox order; a row is as tall as its taller tile. Returns the bottom of the last row.
        private static float BuildTiles(
            Transform templates,
            RectTransform shelf,
            ComponentCatalog catalog,
            float top,
            List<ToolboxEntryTile> created)
        {
            List<ComponentCatalogEntry> entries = new List<ComponentCatalogEntry>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                if (catalog.Entries[i].ShowInToolbox)
                {
                    entries.Add(catalog.Entries[i]);
                }
            }

            entries.Sort((a, b) => a.ToolboxOrder.CompareTo(b.ToolboxOrder));
            float columnWidth = (PanelWidth - (2f * Border) - (2f * Pad) - Gap) * 0.5f;
            float y = top;
            for (int i = 0; i < entries.Count; i += 2)
            {
                float rowHeight = 0f;
                for (int c = 0; c < 2 && i + c < entries.Count; c++)
                {
                    float height = TileHeight(entries[i + c]);
                    rowHeight = Mathf.Max(rowHeight, height);
                    ToolboxEntryTile tile = BuildTile(templates, shelf, entries[i + c], height);
                    RectTransform rect = tile.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2(Pad + (c * (columnWidth + Gap)), -y);
                    rect.sizeDelta = new Vector2(columnWidth, height);
                    created.Add(tile);
                }

                y += rowHeight + Gap;
            }

            return y;
        }

        private static float TileHeight(ComponentCatalogEntry entry)
        {
            if (entry.Kind == ComponentCatalogKind.Card)
            {
                return TileCounterHeight;
            }

            return entry.Kind == ComponentCatalogKind.Die ? TileChoicesHeight : TileBaseHeight;
        }

        private static ToolboxEntryTile BuildTile(Transform templates, RectTransform shelf, ComponentCatalogEntry entry, float height)
        {
            GameObject tile = UnityEngine.Object.Instantiate(Find(templates, "TileTemplate").gameObject, shelf, false);
            tile.name = $"Tile_{entry.DisplayName}";
            Find(tile.transform, "Name").GetComponent<Text>().text = entry.DisplayName;
            Find(tile.transform, "Hint").GetComponent<Text>().text = entry.Description;
            Find(tile.transform, "IconBox").GetComponent<Image>().color = IconColour(entry.Kind);
            Image icon = Find(tile.transform, "IconBox/Icon").GetComponent<Image>();
            icon.sprite = entry.Icon != null ? entry.Icon : AssetDatabase.LoadAssetAtPath<Sprite>(IconPath + IconName(entry.Kind));

            ToolboxEntryTile view = tile.GetComponent<ToolboxEntryTile>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("entryId").stringValue = entry.Id;
            Transform counter = Find(tile.transform, "Counter");
            Transform choices = Find(tile.transform, "Choices");
            if (entry.Kind == ComponentCatalogKind.Card)
            {
                so.FindProperty("minusButton").objectReferenceValue = Find(counter, "Minus").GetComponent<Button>();
                so.FindProperty("plusButton").objectReferenceValue = Find(counter, "Plus").GetComponent<Button>();
                so.FindProperty("countLabel").objectReferenceValue = Find(counter, "Count").GetComponent<Text>();
                so.FindProperty("counterHint").objectReferenceValue = Find(counter, "CounterHint").GetComponent<Text>();
                so.FindProperty("singleFormat").stringValue = "Places 1 loose card";
                so.FindProperty("pluralFormat").stringValue = "Places a deck of {0}";
                so.FindProperty("minimumCount").intValue = 1;
                so.FindProperty("maximumCount").intValue = 99;
                so.FindProperty("defaultCount").intValue = 1;
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(counter.gameObject);
            }

            if (entry.Kind == ComponentCatalogKind.Die)
            {
                List<int> sizes = DieSizes(entry);
                Transform template = Find(choices, "ChoiceTemplate");
                SerializedProperty array = so.FindProperty("choices");
                array.arraySize = sizes.Count;
                for (int i = 0; i < sizes.Count; i++)
                {
                    GameObject chip = UnityEngine.Object.Instantiate(template.gameObject, choices, false);
                    chip.name = $"Choice_d{sizes[i]}";
                    Text label = Find(chip.transform, "Label").GetComponent<Text>();
                    label.text = $"d{sizes[i]}";
                    RectTransform rect = chip.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2(15f + ((i % 3) * 71f), -14f - ((i / 3) * 48f));
                    SerializedProperty element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("button").objectReferenceValue = chip.GetComponent<Button>();
                    element.FindPropertyRelative("fill").objectReferenceValue = chip.GetComponent<Image>();
                    element.FindPropertyRelative("label").objectReferenceValue = label;
                    element.FindPropertyRelative("value").intValue = sizes[i];
                }

                UnityEngine.Object.DestroyImmediate(template.gameObject);
                so.FindProperty("defaultChoiceIndex").intValue = Mathf.Max(0, sizes.IndexOf(6));
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(choices.gameObject);
                so.FindProperty("choices").arraySize = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static List<int> DieSizes(ComponentCatalogEntry entry)
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

        private static Color IconColour(ComponentCatalogKind kind)
        {
            switch (kind)
            {
                case ComponentCatalogKind.Pawn:
                case ComponentCatalogKind.Token:
                case ComponentCatalogKind.Die:
                    return Teal;
                case ComponentCatalogKind.Console:
                case ComponentCatalogKind.ControllerBox:
                    return Orange;
                default:
                    return Mustard;
            }
        }

        private static string IconName(ComponentCatalogKind kind)
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

        // ---------- assets ----------

        private static ComponentLibrary LoadLibrary()
        {
            ComponentLibrary library = AssetDatabase.LoadAssetAtPath<ComponentLibrary>(LibraryPath);
            if (library == null)
            {
                throw new InvalidOperationException($"No component library at {LibraryPath}.");
            }

            return library;
        }

        // Sprites need their 9-slice borders; set the importer once, then load the fonts.
        private static void PrepareArt()
        {
            ConfigureSprite("PanelOutlined", 26);
            ConfigureSprite("TileOutlined", 19);
            ConfigureSprite("BoxOutlined", 16);
            ConfigureSprite("ButtonOutlined", 14);
            ConfigureSprite("ChipOutlined", 12);
            ConfigureSprite("PillOutlined", 23);
            ConfigureSprite("FrameOnly", 18);
            ConfigureSprite("TopRounded", 20, 20, 20, 1);
            ConfigureSprite("BottomRounded", 20, 1, 20, 20);
            ConfigureSprite("TabOutlined", 14, 5, 14, 14);
            ConfigureSprite("Solid", 0);
            ConfigureSprite("Dash", 0);
            foreach (string name in new[] { "Card", "Deck", "Stack", "DiscardPile", "Pawn", "Token", "Die", "Console", "Close" })
            {
                ConfigureTexture(IconPath + "Icon" + name + ".png", Vector4.zero);
            }

            LoadFonts();
        }

        private static void ConfigureSprite(string name, float border)
        {
            ConfigureSprite(name, border, border, border, border);
        }

        // Border order as Unity's spriteBorder: left, bottom, right, top.
        private static void ConfigureSprite(string name, float left, float bottom, float right, float top)
        {
            ConfigureTexture(SpritePath + name + ".png", new Vector4(left, bottom, right, top));
        }

        private static void LoadFonts()
        {
            titleFont = RequireFont("Silkscreen-Bold.ttf");
            bodyFont = RequireFont("ChakraPetch-Medium.ttf");
            boldFont = RequireFont("ChakraPetch-Bold.ttf");
        }

        // Points the Real UI prefab catalog's Toolbox entry at the built prefab.
        private static void AssignToUiCatalog()
        {
            UiPrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<UiPrefabCatalog>(UiCatalogPath);
            ComponentToolboxView prefab = AssetDatabase.LoadAssetAtPath<ComponentToolboxView>(PrefabPath);
            if (catalog == null || prefab == null)
            {
                throw new InvalidOperationException($"Real UI prefab catalog or Toolbox prefab missing ({UiCatalogPath}).");
            }

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("id").stringValue == ToolboxUiId)
                {
                    entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(catalog);
                    AssetDatabase.SaveAssets();
                    return;
                }
            }

            throw new InvalidOperationException($"{UiCatalogPath} has no '{ToolboxUiId}' entry.");
        }

        // Gives catalog entries without an icon the Toolbox icon of their kind (the entry's UI face).
        private static void AssignCatalogIcons(ComponentLibrary library)
        {
            bool saved = false;
            IReadOnlyList<ComponentCatalog> catalogs = library.Catalogs;
            for (int c = 0; c < catalogs.Count; c++)
            {
                SerializedObject so = new SerializedObject(catalogs[c]);
                SerializedProperty entries = so.FindProperty("entries");
                bool changed = false;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    SerializedProperty icon = entry.FindPropertyRelative("icon");
                    if (icon.objectReferenceValue == null)
                    {
                        ComponentCatalogKind kind = (ComponentCatalogKind)entry.FindPropertyRelative("kind").intValue;
                        icon.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath + IconName(kind));
                        changed = true;
                    }
                }

                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(catalogs[c]);
                    saved = true;
                }
            }

            if (saved)
            {
                AssetDatabase.SaveAssets();
            }
        }

        // ---------- small helpers ----------

        private static void SmallButton(RectTransform parent, string name, string glyph, Vector2 rightOffset)
        {
            RectTransform rect = Rect(name, parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                rightOffset, new Vector2(41f, 41f));
            Image fill = Img(rect, "ButtonOutlined", Cream);
            rect.gameObject.AddComponent<Button>().targetGraphic = fill;
            Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero),
                boldFont, 24, Ink, TextAnchor.MiddleCenter, glyph);
        }

        private static void DashRule(RectTransform parent)
        {
            RectTransform rule = Rect("Rule", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(-30f, 3f));
            Image image = Img(rule, null, Rule, SpritePath + "Dash.png");
            image.type = Image.Type.Tiled;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        private static float TextWidth(Text label, string text)
        {
            TextGenerationSettings settings = label.GetGenerationSettings(new Vector2(2000f, 100f));
            float measured = label.cachedTextGeneratorForLayout.GetPreferredWidth(text, settings)
                / Mathf.Max(0.01f, label.pixelsPerUnit);
            // Fallback when the font has not generated yet: about 0.62 em per character.
            return Mathf.Max(measured, text.Length * label.fontSize * 0.62f * 0.8f);
        }

        private static Transform Find(Transform parent, string path)
        {
            Transform found = parent.Find(path);
            if (found == null)
            {
                throw new InvalidOperationException(
                    $"Toolbox prefab is missing '{path}' under '{parent.name}'. Rebuild it: Console Cards > Toolbox > Build Toolbox Prefab.");
            }

            return found;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

    }
}
