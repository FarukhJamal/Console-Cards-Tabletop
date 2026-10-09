using System;
using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Builds the authored session bar prefab (UI-1): the game chip, Undo, Redo, the hover tip and the Table
    /// menu, in the Toolbox design language (same sprites, fonts and colours). Everything is made at edit time
    /// and saved as a prefab you can restyle; nothing is built at runtime. The build also adds the prefab to
    /// the Real UI prefab catalog as platform.session-bar (HUD layer, cached).
    /// </summary>
    [InitializeOnLoad]
    public static class SessionUiBuilder
    {
        public const string PrefabPath = "Assets/ConsoleCards/Content/Prefabs/Real/UI/SessionBar.prefab";
        private const string IconPath = "Assets/ConsoleCards/Content/UI/Session/Icons/";

        // Layout, in canvas units at the 1920 x 1080 reference (about 1.25 x the approved boards).
        private const float Margin = 29f;
        private const float Border = 5f;
        private const float BarPad = 10f;
        private const float ButtonSize = 56f;
        private const float BarGap = 12f;
        private const float ShadowOffset = 7f;
        // The Toolbox panel opens below the bar (UI-1b), so the bar keeps its corner while the panel is open.
        private const float BarLeftBesidePanel = Margin;
        private const float MenuWidth = 400f;
        private const float MenuHeaderHeight = 58f;
        private const float MenuRowHeight = 60f;
        private const float MenuRowRule = 3f;

        private static Font titleFont;
        private static Font bodyFont;
        private static Font boldFont;

        static SessionUiBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        [MenuItem("Console Cards/UI/Build Session Bar Prefab")]
        public static void Build()
        {
            PrepareArt();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject root = new GameObject("SessionBar", typeof(RectTransform));
            root.layer = 5;
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                SessionBarView view = root.AddComponent<SessionBarView>();
                BuildContents(root.transform, view);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssignCatalogEntry(PrototypeUiPrefabIds.SessionBar, AssetDatabase.LoadAssetAtPath<SessionBarView>(PrefabPath),
                UiLayer.Hud, UiRetention.CachedSingleInstance);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Session UI] Built {PrefabPath}.");
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(PrefabPath))
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<UiPrefabCatalog>(RealUiCatalogPath) == null
                || AssetImporter.GetAtPath(IconPath + "IconUndo.png") == null)
            {
                return;
            }

            Build();
        }

        private static void BuildContents(Transform root, SessionBarView view)
        {
            // Outside-click blocker for the Table menu (first, so it sits under the bar and the menu).
            RectTransform blocker = Rect("MenuBlocker", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Image blockerImage = Img(blocker, "Solid", new Color(0f, 0f, 0f, 0f));
            Button blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.targetGraphic = blockerImage;
            blockerButton.transition = Selectable.Transition.None;
            blocker.gameObject.SetActive(false);

            // Bar: sized by its contents; shadow and fill ignore the layout.
            RectTransform bar = Rect("Bar", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -Margin), new Vector2(400f, ButtonSize + 2f * (Border + BarPad)));
            HorizontalLayoutGroup barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            int inset = Mathf.RoundToInt(Border + BarPad);
            barLayout.padding = new RectOffset(inset, inset, inset, inset);
            barLayout.spacing = BarGap;
            barLayout.childAlignment = TextAnchor.MiddleLeft;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = false;
            ContentSizeFitter barFit = bar.gameObject.AddComponent<ContentSizeFitter>();
            barFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            barFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Backdrop(bar, ShadowOffset);

            // Game chip: card glyph (games only), title, mode line.
            RectTransform chip = Rect("Chip", bar, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image chipFill = Img(chip, "ButtonOutlined", Orange);
            chipFill.raycastTarget = false;
            HorizontalLayoutGroup chipLayout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset(12, 18, 4, 4);
            chipLayout.spacing = 12f;
            chipLayout.childAlignment = TextAnchor.MiddleLeft;
            chipLayout.childControlWidth = true;
            chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = false;
            chipLayout.childForceExpandHeight = false;
            Size(chip.gameObject, -1f, ButtonSize, -1f);
            RectTransform glyph = Rect("Glyph", chip, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Img(glyph, "ButtonOutlined", Cream).raycastTarget = false;
            Size(glyph.gameObject, 30f, 38f, 30f);
            RectTransform words = Rect("Words", chip, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            VerticalLayoutGroup wordsLayout = words.gameObject.AddComponent<VerticalLayoutGroup>();
            wordsLayout.spacing = 0f;
            wordsLayout.childAlignment = TextAnchor.MiddleLeft;
            wordsLayout.childControlWidth = true;
            wordsLayout.childControlHeight = false;
            wordsLayout.childForceExpandWidth = false;
            wordsLayout.childForceExpandHeight = false;
            // Fixed line heights (the column does not size them), so title and mode line always fit the chip.
            Text chipTitle = FitLabel(Rect("Title", words, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(0f, 24f)), titleFont, 19, Cream, "TRAP FLOOR");
            Text chipSubtitle = FitLabel(Rect("Subtitle", words, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(0f, 20f)), boldFont, 15, Cream, "Easy");

            // Undo and Redo.
            Button undoButton = IconButton(bar, "UndoButton", "IconUndo.png", out Image undoIcon, out SessionBarHoverTarget undoHover);
            Button redoButton = IconButton(bar, "RedoButton", "IconRedo.png", out Image redoIcon, out SessionBarHoverTarget redoHover);

            // Table button: menu icon and word.
            RectTransform table = Rect("TableButton", bar, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image tableFill = Img(table, "ButtonOutlined", Cream);
            Button tableButton = table.gameObject.AddComponent<Button>();
            tableButton.targetGraphic = tableFill;
            HorizontalLayoutGroup tableLayout = table.gameObject.AddComponent<HorizontalLayoutGroup>();
            tableLayout.padding = new RectOffset(16, 20, 0, 0);
            tableLayout.spacing = 10f;
            tableLayout.childAlignment = TextAnchor.MiddleLeft;
            tableLayout.childControlWidth = true;
            tableLayout.childControlHeight = true;
            tableLayout.childForceExpandWidth = false;
            tableLayout.childForceExpandHeight = false;
            Size(table.gameObject, -1f, ButtonSize, -1f);
            RectTransform tableIcon = Rect("Icon", table, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Img(tableIcon, null, Ink, IconPath + "IconMenu.png").raycastTarget = false;
            Size(tableIcon.gameObject, 26f, 26f, 26f);
            FitLabel(Rect("Label", table, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                boldFont, 19, Ink, "Table");

            // Hover tip (dark pill under the hovered button).
            RectTransform tip = Rect("Tip", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -Margin - 100f), new Vector2(120f, 36f));
            Img(tip, "ButtonOutlined", Ink).raycastTarget = false;
            HorizontalLayoutGroup tipLayout = tip.gameObject.AddComponent<HorizontalLayoutGroup>();
            tipLayout.padding = new RectOffset(14, 14, 7, 7);
            tipLayout.childAlignment = TextAnchor.MiddleLeft;
            tipLayout.childControlWidth = true;
            tipLayout.childControlHeight = true;
            tipLayout.childForceExpandWidth = false;
            tipLayout.childForceExpandHeight = false;
            ContentSizeFitter tipFit = tip.gameObject.AddComponent<ContentSizeFitter>();
            tipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Text tipLabel = FitLabel(Rect("Label", tip, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                bodyFont, 16, Cream, "Undo");
            tip.gameObject.SetActive(false);

            // Table menu (placed under the Table button when it opens).
            float menuHeight = Border + MenuHeaderHeight + Border + 3f * MenuRowHeight + 2f * MenuRowRule + Border;
            RectTransform menu = Rect("TableMenu", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -Margin - 100f), new Vector2(MenuWidth, menuHeight));
            Backdrop(menu, ShadowOffset + 1f);
            RectTransform header = TopBand("Header", menu, Border, MenuHeaderHeight);
            Img(header, "TopRounded", Orange).raycastTarget = false;
            Label(Rect("Title", header, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(9f, 0f), new Vector2(-36f, 0f)),
                titleFont, 22, Cream, TextAnchor.MiddleLeft, "TABLE");
            Img(TopBand("HeaderRule", menu, Border + MenuHeaderHeight, Border), "Solid", Ink).raycastTarget = false;
            float rowTop = Border + MenuHeaderHeight + Border;
            Button newTableButton = MenuRow(menu, "NewTableRow", rowTop, "New table…", false);
            Img(TopBand("Rule1", menu, rowTop + MenuRowHeight, MenuRowRule), "Solid", TabIdle).raycastTarget = false;
            rowTop += MenuRowHeight + MenuRowRule;
            Button resetButton = MenuRow(menu, "ResetRow", rowTop, "Reset to start", false);
            Img(TopBand("Rule2", menu, rowTop + MenuRowHeight, MenuRowRule), "Solid", TabIdle).raycastTarget = false;
            rowTop += MenuRowHeight + MenuRowRule;
            Button clearTableButton = MenuRow(menu, "ClearTableRow", rowTop, "Clear table", true);
            menu.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("bar").objectReferenceValue = bar;
            so.FindProperty("barLeft").floatValue = Margin;
            so.FindProperty("barLeftBesidePanel").floatValue = BarLeftBesidePanel;
            so.FindProperty("chipFill").objectReferenceValue = chipFill;
            so.FindProperty("chipGlyph").objectReferenceValue = glyph.gameObject;
            so.FindProperty("chipTitle").objectReferenceValue = chipTitle;
            so.FindProperty("chipSubtitle").objectReferenceValue = chipSubtitle;
            so.FindProperty("gameChipColor").colorValue = Orange;
            so.FindProperty("tableChipColor").colorValue = Ink;
            so.FindProperty("undoButton").objectReferenceValue = undoButton;
            so.FindProperty("undoIcon").objectReferenceValue = undoIcon;
            so.FindProperty("undoHover").objectReferenceValue = undoHover;
            so.FindProperty("redoButton").objectReferenceValue = redoButton;
            so.FindProperty("redoIcon").objectReferenceValue = redoIcon;
            so.FindProperty("redoHover").objectReferenceValue = redoHover;
            so.FindProperty("tableButton").objectReferenceValue = tableButton;
            so.FindProperty("iconColor").colorValue = Ink;
            so.FindProperty("disabledIconAlpha").floatValue = 0.3f;
            so.FindProperty("tip").objectReferenceValue = tip;
            so.FindProperty("tipLabel").objectReferenceValue = tipLabel;
            so.FindProperty("tipGap").floatValue = 22f;
            so.FindProperty("menuBlocker").objectReferenceValue = blockerButton;
            so.FindProperty("menu").objectReferenceValue = menu;
            so.FindProperty("menuGap").floatValue = 22f;
            so.FindProperty("newTableButton").objectReferenceValue = newTableButton;
            so.FindProperty("resetButton").objectReferenceValue = resetButton;
            so.FindProperty("clearTableButton").objectReferenceValue = clearTableButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button IconButton(
            RectTransform parent,
            string name,
            string icon,
            out Image iconImage,
            out SessionBarHoverTarget hover)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image fill = Img(rect, "ButtonOutlined", Cream);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            Size(rect.gameObject, ButtonSize, ButtonSize, ButtonSize);
            iconImage = Img(Rect("Icon", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(28f, 28f)), null, Ink, IconPath + icon);
            iconImage.raycastTarget = false;
            hover = rect.gameObject.AddComponent<SessionBarHoverTarget>();
            return button;
        }

        private static Button MenuRow(RectTransform menu, string name, float top, string text, bool last)
        {
            RectTransform row = TopBand(name, menu, top, MenuRowHeight);
            Image fill = Img(row, last ? "BottomRounded" : "Solid", Cream);
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.98f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.95f, 0.9f, 0.76f, 1f);
            button.colors = colors;
            Label(Rect("Label", row, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(9f, 0f), new Vector2(-36f, 0f)),
                boldFont, 20, Ink, TextAnchor.MiddleLeft, text);
            return button;
        }

        private static void PrepareArt()
        {
            foreach (string name in new[] { "Undo", "Redo", "Menu" })
            {
                ConfigureTexture(IconPath + "Icon" + name + ".png", Vector4.zero);
            }

            titleFont = RequireFont("Silkscreen-Bold.ttf");
            bodyFont = RequireFont("ChakraPetch-Medium.ttf");
            boldFont = RequireFont("ChakraPetch-Bold.ttf");
        }
    }
}
