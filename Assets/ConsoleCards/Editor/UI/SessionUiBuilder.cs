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
    /// menu (with the Rules card and Hints switches and Help, doc 23 R1), in the Toolbox design language (same sprites, fonts and colours). Everything is made at edit time
    /// and saved as a prefab you can restyle; nothing is built at runtime. The build also adds the prefab to
    /// the Real UI prefab catalog as platform.session-bar (HUD layer, cached).
    /// </summary>
    [InitializeOnLoad]
    public static class SessionUiBuilder
    {
        public const string PrefabPath = FinalUiFolder + "SessionBar.prefab";
        private const string IconPath = "Assets/ConsoleCards/Content/UI/Session/Icons/";

        // Layout, in canvas units at the 1920 x 1080 reference (about 1.25 x the approved boards).
        private const float Margin = 29f;
        private const float Border = 5f;
        private const float BarPad = 10f;
        private const float ButtonSize = 56f;
        private const float BarGap = 12f;
        private const float ShadowOffset = 7f;
        // Distance from a bar button's bottom edge to just below the bar and its shadow (tips and the menu sit here).
        private const float BelowBar = 15f + 7f + 10f;
        private const float MenuWidth = 400f;
        private const float MenuHeaderHeight = 58f;
        private const float MenuRowHeight = 60f;
        private const float MenuRowRule = 3f;
        private const float SwitchRowHeight = 72f;
        private const string RulesOnHint = "Shown in the left column";
        private const string HintsOnHint = "Controls strip and how-to lines";
        private const string HotSeatOnHint = "The screen passes to whoever's turn it is";
        private const string HotSeatOffHint = "Off; this screen keeps its seat";

        private static Font titleFont;
        private static Font bodyFont;
        private static Font boldFont;

        static SessionUiBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        [MenuItem("Console Cards/UI/Create Missing Session Bar Prefab (Final UI)")]
        public static void Build()
        {
            // Final UI prefabs belong to the designer: never overwrite an existing one.
            if (File.Exists(PrefabPath))
            {
                AssignCatalogEntry(UiPrefabIds.SessionBar, AssetDatabase.LoadAssetAtPath<SessionBarView>(PrefabPath),
                    UiLayer.Hud, UiRetention.CachedSingleInstance);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Session UI] Kept {PrefabPath} (already exists; edit it in the Final UI folder).");
                return;
            }

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

            AssignCatalogEntry(UiPrefabIds.SessionBar, AssetDatabase.LoadAssetAtPath<SessionBarView>(PrefabPath),
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
            Button undoButton = IconButton(bar, "UndoButton", "IconUndo.png", "Undo", out Image undoIcon,
                out SessionBarHoverTarget undoHover, out GameObject undoTip, out Text undoTipLabel);
            Button redoButton = IconButton(bar, "RedoButton", "IconRedo.png", "Redo", out Image redoIcon,
                out SessionBarHoverTarget redoHover, out GameObject redoTip, out Text redoTipLabel);

            // Shop entry (doc 18 §15.4): shop icon and word, shown only while a game's Shop is open.
            Button shopButton = ShopEntry(bar);

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

            // Table menu: placed in the prefab under the bar's right end (the Table button), outside the bar's
            // layout; a column sized to its rows (doc 23, R1). It swallows clicks so they do not reach the bar.
            RectTransform menu = Rect("TableMenu", bar, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-(Border + BarPad), -(ShadowOffset + 10f)), new Vector2(MenuWidth, 400f));
            menu.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Backdrop(menu, ShadowOffset + 1f);
            Button menuClickBlock = menu.gameObject.AddComponent<Button>();
            menuClickBlock.transition = Selectable.Transition.None;
            Navigation menuNavigation = menuClickBlock.navigation;
            menuNavigation.mode = Navigation.Mode.None;
            menuClickBlock.navigation = menuNavigation;
            Column(menu.gameObject, (int)Border, (int)Border, (int)Border, (int)Border, 0f);
            Fit(menu.gameObject, false, true);
            RectTransform header = Child("Header", menu);
            Img(header, "TopRounded", Orange).raycastTarget = false;
            Size(header.gameObject, -1f, MenuHeaderHeight);
            Label(Rect("Title", header, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(9f, 0f), new Vector2(-36f, 0f)),
                titleFont, 22, Cream, TextAnchor.MiddleLeft, "TABLE");
            Separator(menu, "HeaderRule", Ink, Border);
            Button newTableButton = MenuRow(menu, "NewTableRow", "New table…", false);
            Separator(menu, "Rule1", TabIdle, MenuRowRule);
            Button resetButton = MenuRow(menu, "ResetRow", "Reset to start", false);
            Separator(menu, "Rule2", TabIdle, MenuRowRule);
            Button clearTableButton = MenuRow(menu, "ClearTableRow", "Clear table", false);
            Separator(menu, "SettingsRule", Ink, MenuRowRule);
            RectTransform rulesRow = SwitchRow(menu, "RulesCardRow", "Rules card", RulesOnHint, boldFont, bodyFont, titleFont,
                SwitchRowHeight, out Button rulesButton, out Image rulesFill, out RectTransform rulesKnob, out Text rulesWord,
                out Text rulesHint);
            RectTransform hintsRow = SwitchRow(menu, "HintsRow", "Hints", HintsOnHint, boldFont, bodyFont, titleFont,
                SwitchRowHeight, out Button hintsButton, out Image hintsFill, out RectTransform hintsKnob, out Text hintsWord,
                out Text hintsHint);
            AddPlayRows(menu, view, -1);
            AddTipsRow(menu, view, hintsRow.GetSiblingIndex() + 1);
            Separator(menu, "Rule3", TabIdle, MenuRowRule);
            Button helpButton = MenuRow(menu, "HelpRow", "Help", true);
            menu.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("bar").objectReferenceValue = bar;
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
            so.FindProperty("shopButton").objectReferenceValue = shopButton;
            so.FindProperty("iconColor").colorValue = Ink;
            so.FindProperty("disabledIconAlpha").floatValue = 0.3f;
            so.FindProperty("undoTip").objectReferenceValue = undoTip;
            so.FindProperty("undoTipLabel").objectReferenceValue = undoTipLabel;
            so.FindProperty("redoTip").objectReferenceValue = redoTip;
            so.FindProperty("redoTipLabel").objectReferenceValue = redoTipLabel;
            so.FindProperty("menuBlocker").objectReferenceValue = blockerButton;
            so.FindProperty("menu").objectReferenceValue = menu;
            so.FindProperty("newTableButton").objectReferenceValue = newTableButton;
            so.FindProperty("resetButton").objectReferenceValue = resetButton;
            so.FindProperty("clearTableButton").objectReferenceValue = clearTableButton;
            AssignSwitch(so.FindProperty("rulesSwitch"), rulesRow, rulesButton, rulesFill, rulesKnob, rulesWord, rulesHint,
                RulesOnHint, "Hidden; switch on to read the rules");
            AssignSwitch(so.FindProperty("hintsSwitch"), hintsRow, hintsButton, hintsFill, hintsKnob, hintsWord, hintsHint,
                HintsOnHint, "Off; the Table menu still has Help");
            so.FindProperty("helpButton").objectReferenceValue = helpButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Adds the play and view rows (Seat, Hot-seat, Hands on table, Feedback, Card art, Card editor, Deck editor, Profile, Save table as a game, Table shape, Table edge, Table tools; doc 18 §15.11, doc 25 F3,
        /// doc 28) and
        /// the Shop entry on the bar (doc 18 §15.4) to an existing SessionBar prefab when they are missing. Rows that already exist and everything else in the
        /// prefab are kept as they are.
        /// </summary>
        [MenuItem("Console Cards/UI/Add Missing Table Menu Rows (Final UI)")]
        public static void AddMissingTableMenuRows()
        {
            if (!File.Exists(PrefabPath))
            {
                Build();
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                SessionBarView view = root.GetComponent<SessionBarView>();
                SerializedObject so = view != null ? new SerializedObject(view) : null;
                RectTransform menu = so?.FindProperty("menu").objectReferenceValue as RectTransform;
                if (view == null || menu == null)
                {
                    Debug.LogWarning($"[Session UI] {PrefabPath} has no SessionBarView with a Table menu; nothing added.");
                    return;
                }

                PrepareArt();
                Transform before = menu.Find("Rule3");
                int index = before != null ? before.GetSiblingIndex() : menu.childCount;
                int added = AddPlayRows(menu, view, index);
                Transform hintsRow = menu.Find("HintsRow");
                added += AddTipsRow(menu, view, hintsRow != null ? hintsRow.GetSiblingIndex() + 1 : -1);
                added += AddShopEntry(view);
                if (added == 0)
                {
                    Debug.Log($"[Session UI] {PrefabPath} already has every Table menu row.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[Session UI] Added {added} missing item(s) (Table menu rows, Shop entry) to {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Adds whichever of the Seat row and the Hot-seat, Hands on table and Feedback switches the view lacks,
        // in that order, at siblingIndex (-1: at the end). Returns how many rows were added.
        private static int AddPlayRows(RectTransform menu, SessionBarView view, int siblingIndex)
        {
            SerializedObject so = new SerializedObject(view);
            int next = siblingIndex < 0 ? menu.childCount : siblingIndex;
            int added = 0;
            if (so.FindProperty("seatButton").objectReferenceValue == null)
            {
                RectTransform rule = Child("PlayRule", menu);
                Img(rule, "Solid", Ink).raycastTarget = false;
                Size(rule.gameObject, -1f, MenuRowRule);
                rule.SetSiblingIndex(next++);
                Button seatButton = MenuRow(menu, "SeatRow", "Seat: Player 1 (BLUE)", false);
                SetDisabledTint(seatButton);
                seatButton.transform.SetSiblingIndex(next++);
                so.FindProperty("seatButton").objectReferenceValue = seatButton;
                so.FindProperty("seatLabel").objectReferenceValue = seatButton.transform.Find("Label").GetComponent<Text>();
                added++;
            }
            else
            {
                next = AfterRow(menu, so.FindProperty("seatButton").objectReferenceValue as Component, next);
            }

            next = AddSwitchIfMissing(menu, so, "hotSeatSwitch", "HotSeatRow", "Hot-seat", HotSeatOnHint, HotSeatOffHint,
                next, ref added);
            next = AddSwitchIfMissing(menu, so, "handsOnTableSwitch", "HandsOnTableRow", "Hands on table",
                "Your hand lies at your table edge", "Off; your hand is on screen", next, ref added);
            next = AddSwitchIfMissing(menu, so, "fullFeedbackSwitch", "FeedbackRow", "Full feedback",
                "Banners, sounds and camera nudges", "Minimal: messages and board markers", next, ref added);
            if (so.FindProperty("cardArtButton").objectReferenceValue == null)
            {
                RectTransform rule = Child("CardArtRule", menu);
                Img(rule, "Solid", TabIdle).raycastTarget = false;
                Size(rule.gameObject, -1f, MenuRowRule);
                rule.SetSiblingIndex(next++);
                Button cardArt = MenuRow(menu, "CardArtRow", "Card art…", false);
                cardArt.transform.SetSiblingIndex(next);
                so.FindProperty("cardArtButton").objectReferenceValue = cardArt;
                added++;
            }

            // The Card editor row (L2-2) goes right under Card art.
            if (so.FindProperty("cardEditorButton").objectReferenceValue == null)
            {
                Component cardArtRow = so.FindProperty("cardArtButton").objectReferenceValue as Component;
                Button cardEditor = MenuRow(menu, "CardEditorRow", "Card editor…", false);
                cardEditor.transform.SetSiblingIndex(AfterRow(menu, cardArtRow, menu.childCount - 1));
                so.FindProperty("cardEditorButton").objectReferenceValue = cardEditor;
                added++;
            }

            // The Deck editor row (L2-3) goes right under Card editor.
            if (so.FindProperty("deckEditorButton").objectReferenceValue == null)
            {
                Component cardEditorRow = so.FindProperty("cardEditorButton").objectReferenceValue as Component;
                Button deckEditor = MenuRow(menu, "DeckEditorRow", "Deck editor…", false);
                deckEditor.transform.SetSiblingIndex(AfterRow(menu, cardEditorRow, menu.childCount - 1));
                so.FindProperty("deckEditorButton").objectReferenceValue = deckEditor;
                added++;
            }

            // The Profile row (doc 28, P2) goes under Deck editor (or Card editor, or Card art).
            if (so.FindProperty("profileButton").objectReferenceValue == null)
            {
                Component above = so.FindProperty("deckEditorButton").objectReferenceValue as Component;
                if (above == null) above = so.FindProperty("cardEditorButton").objectReferenceValue as Component;
                if (above == null) above = so.FindProperty("cardArtButton").objectReferenceValue as Component;
                Button profile = MenuRow(menu, "ProfileRow", "Profile: Player 1…", false);
                profile.transform.SetSiblingIndex(AfterRow(menu, above, menu.childCount - 1));
                so.FindProperty("profileButton").objectReferenceValue = profile;
                so.FindProperty("profileLabel").objectReferenceValue = profile.transform.Find("Label").GetComponent<Text>();
                added++;
            }

            // The Save table as a game row (doc 29) goes under Profile.
            if (so.FindProperty("saveGameButton").objectReferenceValue == null)
            {
                Component profileRow = so.FindProperty("profileButton").objectReferenceValue as Component;
                Button saveGame = MenuRow(menu, "SaveGameRow", "Save table as a game…", false);
                saveGame.transform.SetSiblingIndex(AfterRow(menu, profileRow, menu.childCount - 1));
                so.FindProperty("saveGameButton").objectReferenceValue = saveGame;
                added++;
            }

            // The Table shape row (T1) goes under Save table as a game (or Profile).
            if (so.FindProperty("tableShapeButton").objectReferenceValue == null)
            {
                Component above = so.FindProperty("saveGameButton").objectReferenceValue as Component;
                if (above == null) above = so.FindProperty("profileButton").objectReferenceValue as Component;
                Button tableShape = MenuRow(menu, "TableShapeRow", "Table: Classic", false);
                tableShape.transform.SetSiblingIndex(AfterRow(menu, above, menu.childCount - 1));
                so.FindProperty("tableShapeButton").objectReferenceValue = tableShape;
                so.FindProperty("tableShapeLabel").objectReferenceValue = tableShape.transform.Find("Label").GetComponent<Text>();
                added++;
            }

            // The Table edge row (T2) goes under Table shape.
            if (so.FindProperty("tableEdgeButton").objectReferenceValue == null)
            {
                Component above = so.FindProperty("tableShapeButton").objectReferenceValue as Component;
                Button tableEdge = MenuRow(menu, "TableEdgeRow", "Edge: Lip", false);
                tableEdge.transform.SetSiblingIndex(AfterRow(menu, above, menu.childCount - 1));
                so.FindProperty("tableEdgeButton").objectReferenceValue = tableEdge;
                so.FindProperty("tableEdgeLabel").objectReferenceValue = tableEdge.transform.Find("Label").GetComponent<Text>();
                added++;
            }

            // The Table tools row (T3) goes under Table edge.
            if (so.FindProperty("tableToolsButton").objectReferenceValue == null)
            {
                Component above = so.FindProperty("tableEdgeButton").objectReferenceValue as Component;
                Button tableTools = MenuRow(menu, "TableToolsRow", "Table tools…", false);
                tableTools.transform.SetSiblingIndex(AfterRow(menu, above, menu.childCount - 1));
                so.FindProperty("tableToolsButton").objectReferenceValue = tableTools;
                added++;
            }

            // The Arrange table row (LAYOUT-1) goes under Table tools.
            SerializedProperty arrangeTable = so.FindProperty("arrangeTableButton");
            if (arrangeTable != null && arrangeTable.objectReferenceValue == null)
            {
                Component above = so.FindProperty("tableToolsButton").objectReferenceValue as Component;
                Button arrange = MenuRow(menu, "ArrangeTableRow", "Arrange table…", false);
                arrange.transform.SetSiblingIndex(AfterRow(menu, above, menu.childCount - 1));
                arrangeTable.objectReferenceValue = arrange;
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        // The Shop entry (doc 18 §15.4) goes on the bar just before the Table button. Returns 1 when added.
        private static int AddShopEntry(SessionBarView view)
        {
            SerializedObject so = new SerializedObject(view);
            SerializedProperty shop = so.FindProperty("shopButton");
            RectTransform bar = so.FindProperty("bar").objectReferenceValue as RectTransform;
            if (shop == null || shop.objectReferenceValue != null || bar == null) return 0;
            Button shopButton = ShopEntry(bar);
            Transform table = bar.Find("TableButton");
            if (table != null) shopButton.transform.SetSiblingIndex(table.GetSiblingIndex());
            shop.objectReferenceValue = shopButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        // Cream pill with the shop icon and the word Shop, like the Table button; hidden until a Shop is open.
        private static Button ShopEntry(RectTransform bar)
        {
            RectTransform shop = Rect("ShopButton", bar, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image fill = Img(shop, "ButtonOutlined", Cream);
            Button button = shop.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            HorizontalLayoutGroup layout = shop.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 20, 0, 0);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            Size(shop.gameObject, -1f, ButtonSize, -1f);
            RectTransform icon = Rect("Icon", shop, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Img(icon, null, Ink, IconPath + "IconShop.png").raycastTarget = false;
            Size(icon.gameObject, 28f, 26f, 28f);
            FitLabel(Rect("Label", shop, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                boldFont, 19, Ink, "Shop");
            shop.gameObject.SetActive(false);
            return button;
        }

        // The Tips switch (doc 23 §10) goes right under the Hints switch it belongs to.
        private static int AddTipsRow(RectTransform menu, SessionBarView view, int siblingIndex)
        {
            SerializedObject so = new SerializedObject(view);
            int added = 0;
            AddSwitchIfMissing(menu, so, "tipsSwitch", "TipsRow", "Tips", "Teach the table and the game",
                "Off; only reminders show", siblingIndex < 0 ? menu.childCount : siblingIndex, ref added);
            so.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }

        private static int AddSwitchIfMissing(RectTransform menu, SerializedObject so, string property, string name,
            string title, string onHint, string offHint, int next, ref int added)
        {
            SerializedProperty switchProperty = so.FindProperty(property);
            GameObject existing = switchProperty.FindPropertyRelative("row").objectReferenceValue as GameObject;
            if (existing != null)
            {
                return AfterRow(menu, existing.transform, next);
            }

            RectTransform row = SwitchRow(menu, name, title, offHint, boldFont, bodyFont, titleFont, SwitchRowHeight,
                out Button button, out Image fill, out RectTransform knob, out Text word, out Text hint);
            row.SetSiblingIndex(next);
            AssignSwitch(switchProperty, row, button, fill, knob, word, hint, onHint, offHint);
            added++;
            return next + 1;
        }

        // The index just after an existing row that sits directly in the menu (or the given index otherwise).
        private static int AfterRow(RectTransform menu, Component row, int fallback)
        {
            return row != null && row.transform.parent == menu ? row.transform.GetSiblingIndex() + 1 : fallback;
        }

        private static Button IconButton(
            RectTransform parent,
            string name,
            string icon,
            string tipText,
            out Image iconImage,
            out SessionBarHoverTarget hover,
            out GameObject tip,
            out Text tipLabel)
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

            // Hover tip: a dark pill placed in the prefab just under this button, sized to its text.
            RectTransform tipRect = Rect("Tip", rect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, -BelowBar), new Vector2(120f, 36f));
            Img(tipRect, "ButtonOutlined", Ink).raycastTarget = false;
            Row(tipRect.gameObject, 14, 14, 7, 7, 0f);
            Fit(tipRect.gameObject, true, true);
            tipLabel = FitLabel(Child("Label", tipRect), bodyFont, 16, Cream, tipText);
            tip = tipRect.gameObject;
            tip.SetActive(false);
            return button;
        }

        private static Button MenuRow(RectTransform menu, string name, string text, bool last)
        {
            RectTransform row = Child(name, menu);
            Size(row.gameObject, -1f, MenuRowHeight);
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

        private static void Separator(RectTransform menu, string name, Color color, float height)
        {
            RectTransform rule = Child(name, menu);
            Img(rule, "Solid", color).raycastTarget = false;
            Size(rule.gameObject, -1f, height);
        }

        private static void PrepareArt()
        {
            foreach (string name in new[] { "Undo", "Redo", "Menu", "Shop" })
            {
                ConfigureTexture(IconPath + "Icon" + name + ".png", Vector4.zero);
            }

            titleFont = RequireFont("Silkscreen-Bold.ttf");
            bodyFont = RequireFont("ChakraPetch-Medium.ttf");
            boldFont = RequireFont("ChakraPetch-Bold.ttf");
        }
    }
}
