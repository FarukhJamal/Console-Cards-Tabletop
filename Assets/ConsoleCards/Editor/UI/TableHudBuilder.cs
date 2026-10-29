using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// HUD-B (owner, 2026-10-29: "minimise the UI so we look at the table", colour B approved; doc 40): builds the
    /// table HUD in colour B, the rulebook, the Shop as a drawer and the payment as a strip at the bottom, in
    /// Final UI/Notices. They are yours to edit: an existing prefab is never rebuilt (delete it to get the built
    /// default back). Apply Kit Look does not touch this folder. Every size and colour sits on the prefab and its view.
    /// <para>Console Cards > UI > Table HUD B: Turn Off points the game back at the V5 HUD and the old Shop and payment
    /// popups (the prefabs are kept); Create Missing Table HUD (HUD-B) turns it on again.</para>
    /// </summary>
    public static partial class TableHudBuilder
    {
        public const string Folder = TableNoticeBuilder.Folder;
        public const string HudPath = Folder + "TableHud.prefab";
        public const string RulebookPath = Folder + "TableRulebook.prefab";
        public const string ShopDrawerPath = Folder + "ShopDrawer.prefab";
        public const string PaymentStripPath = Folder + "PaymentStrip.prefab";

        private const string ToolIcons = "Assets/ConsoleCards/Content/UI/ToolBar/Icons/";
        private const string SessionIcons = "Assets/ConsoleCards/Content/UI/Session/Icons/";
        private const string V5Icons = "Assets/ConsoleCards/Content/UI/Figma/Kit/";

        // The kit (doc 36 §1) and colour B (doc 38).
        private static readonly Color Panel = new Color(0.078f, 0.094f, 0.098f, 0.92f);
        private static readonly Color Edge = Hex("3c4143");
        private static readonly Color Words = Hex("e8eced");
        private static readonly Color Soft = Hex("c9d0d2");
        private static readonly Color Muted = Hex("9ba4a7");
        private static readonly Color Cyan = Hex("06eff0");
        private static readonly Color Teal = Hex("138a8c");
        private static readonly Color DarkTeal = Hex("0a3537");
        private static readonly Color DeepTeal = Hex("0f3a3b");
        private static readonly Color Orange = Hex("f2762e");
        private static readonly Color OrangeDark = Hex("b9531c");
        private static readonly Color OrangeInk = Hex("1a1206");
        private static readonly Color Button2 = Hex("222527");
        private static readonly Color Paper = Hex("ece6d6");
        private static readonly Color PaperLine = Hex("d2cab5");
        private static readonly Color PaperTab = Hex("d6cfbd");
        private static readonly Color Ink = Hex("1a1d1e");
        private static readonly Color InkSoft = Hex("4b5357");

        private static Font bold;
        private static Font medium;
        private static Sprite rounded;
        private static Sprite circle;

        // Turn Off is remembered on this computer, so later runs of the UI builders keep the V5 HUD.
        private const string OffPreference = "ConsoleCards.HudB.Off";

        [MenuItem("Console Cards/UI/Create Missing Table HUD (HUD-B)")]
        public static void BuildAndTurnOn()
        {
            EditorPrefs.SetBool(OffPreference, false);
            Build();
        }

        /// <summary>Makes the missing HUD-B prefabs and turns HUD-B on, unless it was turned off on this computer.</summary>
        public static void Build()
        {
            AddMissingHandTrayRow(); // Settings › Show hand works with either HUD
            if (EditorPrefs.GetBool(OffPreference, false))
            {
                Debug.Log("[HUD-B] Off on this computer (Table HUD B: Turn Off); Create Missing Table HUD turns it on again.");
                return;
            }

            bold = RequireFont("Oxanium-Bold.ttf");
            medium = RequireFont("Oxanium-Medium.ttf");
            rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Directory.CreateDirectory(Folder);

            BuildHudBoard(); // HUD-B to board 7B (TableHudBuilder.Hud7.cs; the older BuildHud is kept for reference)
            Save<TableRulebookView>(RulebookPath, "TableRulebook", BuildRulebook);
            Save<ActionAbilityPurchasePopupView>(ShopDrawerPath, "ShopDrawer", BuildShopDrawer);
            Save<FocusedCardSelectionView>(PaymentStripPath, "PaymentStrip", BuildPaymentStrip);
            BuildMenus(); // MENU-1: the Esc menu and Settings
            BuildRulebookObject(); // BOOK-1: the rulebook as a book on the table
            BuildToolboxB(); // TOOL-B: the Toolbox in colour B (board 8)
            BuildMakeGame(); // MAKE-B: Save table as a game in colour B (board 13)

            AssignCatalogEntry(UiPrefabIds.TableHud, AssetDatabase.LoadAssetAtPath<TableHudView>(HudPath),
                UiLayer.Hud, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(UiPrefabIds.TableRulebook, AssetDatabase.LoadAssetAtPath<TableRulebookView>(RulebookPath),
                UiLayer.Modal, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(UiPrefabIds.ActionAbilityPurchase,
                AssetDatabase.LoadAssetAtPath<ActionAbilityPurchasePopupView>(ShopDrawerPath), UiLayer.Modal,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(UiPrefabIds.FocusedCardSelection,
                AssetDatabase.LoadAssetAtPath<FocusedCardSelectionView>(PaymentStripPath), UiLayer.Modal,
                UiRetention.CachedSingleInstance);
            AssetDatabase.SaveAssets();
            Debug.Log("[HUD-B] Created any missing table HUD prefabs (Final UI/Notices) and turned the colour-B HUD on.");
        }

        // Owner, 2026-10-30: Show / Hide Hand moved from the turn actions into Settings. The new row is a copy of the
        // Hands on table row next to it (so it keeps that row's look), retitled; nothing else in the prefab changes.
        private static void AddMissingHandTrayRow()
        {
            if (!File.Exists(SessionUiBuilder.PrefabPath)) return;
            GameObject root = PrefabUtility.LoadPrefabContents(SessionUiBuilder.PrefabPath);
            try
            {
                SessionBarView view = root.GetComponent<SessionBarView>();
                if (view == null) return;
                SerializedObject so = new SerializedObject(view);
                SerializedProperty target = so.FindProperty("handTraySwitch");
                SerializedProperty source = so.FindProperty("handsOnTableSwitch");
                if (target == null || source == null) return;
                if (target.FindPropertyRelative("row").objectReferenceValue != null) return;
                GameObject sourceRow = source.FindPropertyRelative("row").objectReferenceValue as GameObject;
                if (sourceRow == null)
                {
                    Debug.LogWarning("[HUD-B] SessionBar has no Hands on table row to copy; Settings gets no Show hand row.");
                    return;
                }

                GameObject row = Object.Instantiate(sourceRow, sourceRow.transform.parent, false);
                row.name = "HandTrayRow";
                row.transform.SetSiblingIndex(sourceRow.transform.GetSiblingIndex() + 1);
                target.FindPropertyRelative("row").objectReferenceValue = row;
                foreach (string field in new[] { "button", "fill", "knob", "word", "hint" })
                {
                    Component from = source.FindPropertyRelative(field).objectReferenceValue as Component;
                    target.FindPropertyRelative(field).objectReferenceValue = Twin(from, sourceRow.transform, row.transform);
                }

                foreach (string field in new[] { "onFill", "offFill", "onWordColor", "offWordColor" })
                {
                    target.FindPropertyRelative(field).colorValue = source.FindPropertyRelative(field).colorValue;
                }

                foreach (string field in new[] { "knobOffset", "wordOffset" })
                {
                    target.FindPropertyRelative(field).floatValue = source.FindPropertyRelative(field).floatValue;
                }

                target.FindPropertyRelative("onHint").stringValue = "Your hand is up (H)";
                target.FindPropertyRelative("offHint").stringValue = "Hand tucked away (H)";
                Object word = target.FindPropertyRelative("word").objectReferenceValue;
                Object hint = target.FindPropertyRelative("hint").objectReferenceValue;
                foreach (Text text in row.GetComponentsInChildren<Text>(true))
                {
                    if (text != word && text != hint && text.text == "Hands on table") text.text = "Show hand";
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, SessionUiBuilder.PrefabPath);
                Debug.Log("[HUD-B] Added the Show hand switch to Settings (" + SessionUiBuilder.PrefabPath + ").");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The component in the copy that sits where `from` sits in the original row.
        private static Component Twin(Component from, Transform original, Transform copy)
        {
            if (from == null) return null;
            string path = from.transform == original ? string.Empty : AnimationUtility.CalculateTransformPath(from.transform, original);
            Transform at = path.Length == 0 ? copy : copy.Find(path);
            return at != null ? at.GetComponent(from.GetType()) : null;
        }

        [MenuItem("Console Cards/UI/Table HUD B: Turn Off (back to the V5 HUD)")]
        public static void TurnOff()
        {
            EditorPrefs.SetBool(OffPreference, true);
            UiPrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<UiPrefabCatalog>(RealUiCatalogPath);
            if (catalog == null) return;
            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("entries");
            for (int i = entries.arraySize - 1; i >= 0; i--)
            {
                string id = entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
                if (id == UiPrefabIds.TableHud || id == UiPrefabIds.TableRulebook)
                {
                    entries.DeleteArrayElementAtIndex(i);
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            ActionAbilityPurchasePopupView shop =
                AssetDatabase.LoadAssetAtPath<ActionAbilityPurchasePopupView>(GameplayUiBuilder.AbilityPurchasePath);
            if (shop != null)
            {
                AssignCatalogEntry(UiPrefabIds.ActionAbilityPurchase, shop, UiLayer.Modal, UiRetention.CachedSingleInstance);
            }

            SetToolboxPrefab(AssetDatabase.LoadAssetAtPath<ConsoleCards.Presentation.UI.Toolbox.ComponentToolboxView>(
                ConsoleCards.Editor.Toolbox.ComponentToolboxBuilder.PrefabPath)); // TOOL-B: the old Toolbox again
            GameSavePanelView makeGame = AssetDatabase.LoadAssetAtPath<GameSavePanelView>(GameplayUiBuilder.GameSavePanelPath);
            if (makeGame != null)
            {
                AssignCatalogEntry(UiPrefabIds.GameSavePanel, makeGame, UiLayer.Modal, UiRetention.CachedSingleInstance);
            }

            FocusedCardSelectionView selection =
                AssetDatabase.LoadAssetAtPath<FocusedCardSelectionView>(GameplayUiBuilder.FocusedSelectionPath);
            if (selection != null)
            {
                AssignCatalogEntry(UiPrefabIds.FocusedCardSelection, selection, UiLayer.Modal, UiRetention.CachedSingleInstance);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[HUD-B] Turned off: the V5 HUD and the old Shop and payment popups are back. The HUD-B prefabs are kept.");
        }

        private static void Save<T>(string path, string name, System.Action<RectTransform, T> build)
            where T : ReusableUiView
        {
            if (File.Exists(path))
            {
                Debug.Log($"[HUD-B] Kept {path} (already exists; edit it in Final UI/Notices).");
                return;
            }

            GameObject root = new GameObject(name, typeof(RectTransform));
            root.layer = 5;
            try
            {
                RectTransform rect = root.GetComponent<RectTransform>();
                Stretch(rect);
                T view = root.AddComponent<T>();
                build(rect, view);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- the HUD ----------

        private static void BuildHud(RectTransform root, TableHudView view)
        {
            // Top left: the logo (it opens the Table menu) and the status strip beside it.
            RectTransform logoRect = Rect("Logo", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -14f), new Vector2(84f, 84f));
            Image logoRing = Shape(logoRect, Orange, rounded);
            logoRing.raycastTarget = true;
            Button logo = logoRect.gameObject.AddComponent<Button>();
            logo.targetGraphic = logoRing;
            Shape(Rect("Inner", logoRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f)),
                DeepTeal, rounded);
            Shape(Rect("Mark", logoRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24f, -24f)),
                Cyan, rounded);
            Label(Rect("Letters", logoRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                bold, 22, DeepTeal, TextAnchor.MiddleCenter, "CC");
            Tip(logoRect, "Menu", false);

            RectTransform strip = Rect("Status", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(96f, -26f), new Vector2(520f, 60f));
            Plate(strip, Edge, Panel);
            HorizontalLayoutGroup stripRow = Row(strip.gameObject, 22, 10, 8, 8, 14);
            stripRow.childAlignment = TextAnchor.MiddleLeft;
            Fit(strip.gameObject, true, false);

            RectTransform roundGroup = Child("Round", strip);
            Row(roundGroup.gameObject, 0, 0, 0, 0, 8).childAlignment = TextAnchor.MiddleLeft;
            Text roundWord = FitLabel(Child("Word", roundGroup), bold, 17, Orange, "ROUND");
            Text roundNumber = FitLabel(Child("Number", roundGroup), bold, 32, Orange, "3");
            Text turn = FitLabel(Child("Turn", strip), bold, 17, Words, "YOUR TURN");
            RectTransform pipsRow = Child("Pips", strip);
            Row(pipsRow.gameObject, 4, 4, 0, 0, 10).childAlignment = TextAnchor.MiddleCenter;
            Image[] pips = new Image[6];
            for (int i = 0; i < pips.Length; i++)
            {
                RectTransform pip = Child("Pip" + i, pipsRow);
                pips[i] = Shape(pip, Words, circle);
                Size(pip.gameObject, 14f, 14f, 14f);
            }

            Text game = FitLabel(Child("Game", strip), bold, 18, Words, "EMPTY TABLE");
            RectTransform cap = Child("Cap", strip);
            Shape(cap, Orange, rounded);
            Size(cap.gameObject, 10f, 40f, 10f);

            // The hint bar: teal with a dark-teal tag (orange at the end of a round).
            RectTransform hint = Rect("Hint", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -108f), new Vector2(560f, 42f));
            Image hintEdge = Plate(hint, Cyan, Teal, out Image hintFill);
            _ = hintEdge;
            HorizontalLayoutGroup hintRow = Row(hint.gameObject, 5, 6, 5, 5, 10);
            hintRow.childAlignment = TextAnchor.MiddleLeft;
            Fit(hint.gameObject, true, true);
            Image hintTagFill = Tag(hint, "DRAW PHASE", DarkTeal, Cyan, out Text hintTag);
            Text hintText = Label(Child("Text", hint), medium, 16, Hex("f4fefe"), TextAnchor.MiddleLeft,
                "Draw 5 cards from the Controller deck");
            Size(hintText.gameObject, -1f, -1f).preferredWidth = 620f;
            Button gotIt = SmallButton(hint, "GotIt", "GOT IT", DarkTeal, Cyan, 74f, 28f, out _);
            Button hintClose = SmallButton(hint, "Close", "×", new Color(0f, 0f, 0f, 0f), Hex("bff6f6"), 28f, 28f, out _);

            // The rail, right edge.
            RectTransform rail = Rect("Rail", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -18f), new Vector2(66f, 700f));
            Plate(rail, Edge, Panel);
            VerticalLayoutGroup railColumn = Column(rail.gameObject, 7, 7, 7, 7, 6, TextAnchor.UpperCenter);
            railColumn.childForceExpandWidth = false;
            Fit(rail.gameObject, false, true);
            TableHudView.RailTool[] tools =
            {
                RailTool(rail, TableToolKind.Grab, ToolIcons + "ToolGrab.png", "G", "Move pieces"),
                RailTool(rail, TableToolKind.Select, ToolIcons + "ToolSelect.png", "S", "Select · Ctrl"),
                RailTool(rail, TableToolKind.Ping, ToolIcons + "ToolPing.png", "P", "Ping"),
                RailTool(rail, TableToolKind.Flick, ToolIcons + "ToolFlick.png", "F", "Flick · Shift"),
                RailTool(rail, TableToolKind.Draw, ToolIcons + "ToolDraw.png", "D", "Draw"),
            };
            RailRule(rail, "Rule1");
            Button undo = RailButton(rail, "Undo", SessionIcons + "IconUndo.png", "<", "Undo · Ctrl Z", out Image undoIcon);
            Button redo = RailButton(rail, "Redo", SessionIcons + "IconRedo.png", ">", "Redo · Ctrl Y", out Image redoIcon);
            RailRule(rail, "Rule2");
            Button shop = RailButton(rail, "Shop", SessionIcons + "IconShop.png", "$", "Shop", out _);
            Button log = RailButton(rail, "Log", V5Icons + "V5_I_Bars.png", "L", "What happened", out _);
            Button help = RailButton(rail, "Help", null, "?", "Help · keys", out _);
            Button settings = RailButton(rail, "Settings", V5Icons + "V5_I_Gear.png", "*", "Settings", out _);
            Button handStyle = RailButton(rail, "HandStyle", ToolIcons + "ToolFan.png", "H", "Hand: fan / row", out _);
            Button menu = RailButton(rail, "Menu", SessionIcons + "IconMenu.png", "=", "Table menu", out _);

            // Bottom left: the Rulebook and Toolbox buttons and the turn player's chip.
            RectTransform corner = Rect("BottomLeft", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(20f, 20f),
                new Vector2(600f, 68f));
            Row(corner.gameObject, 0, 0, 0, 0, 10).childAlignment = TextAnchor.LowerLeft;
            Fit(corner.gameObject, true, true);
            Button rulebook = CornerButton(corner, "Rulebook", V5Icons + "V5_I_Book.png", "R", Cyan, "Rulebook");
            Button toolbox = CornerButton(corner, "Toolbox", V5Icons + "V5_I_Plus.png", "+", Orange, "Toolbox");
            RectTransform chip = Child("Player", corner);
            Plate(chip, Edge, Panel);
            Row(chip.gameObject, 12, 14, 6, 6, 10).childAlignment = TextAnchor.MiddleLeft;
            Size(chip.gameObject, -1f, 40f);
            RectTransform dotRect = Child("Dot", chip);
            Image dot = Shape(dotRect, Hex("ff3b3b"), circle);
            Size(dotRect.gameObject, 14f, 14f, 14f);
            Text playerText = FitLabel(Child("Text", chip), medium, 15, Words, "RED PLAYER  ·  Keys 1 / 3");

            // Bottom right: the turn's other actions as small chips, END TURN under them.
            RectTransform turnArea = Rect("TurnActions", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-20f, 20f), new Vector2(260f, 200f));
            VerticalLayoutGroup turnColumn = Column(turnArea.gameObject, 0, 0, 0, 0, 8, TextAnchor.LowerRight);
            turnColumn.childForceExpandWidth = false;
            Fit(turnArea.gameObject, true, true);
            RectTransform actionsRoot = Child("Others", turnArea);
            VerticalLayoutGroup othersColumn = Column(actionsRoot.gameObject, 0, 0, 0, 0, 6, TextAnchor.LowerRight);
            othersColumn.childForceExpandWidth = false;
            Button template = ActionChip(actionsRoot);
            Button primary = PillButton(turnArea, "EndTurn", "END TURN", Orange, OrangeInk, 230f, 56f, out Text primaryLabel);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("logoButton").objectReferenceValue = logo;
            so.FindProperty("roundGroup").objectReferenceValue = roundGroup.gameObject;
            so.FindProperty("roundWord").objectReferenceValue = roundWord;
            so.FindProperty("roundNumber").objectReferenceValue = roundNumber;
            so.FindProperty("turnLabel").objectReferenceValue = turn;
            SerializedProperty pipsProperty = so.FindProperty("pips");
            pipsProperty.arraySize = pips.Length;
            for (int i = 0; i < pips.Length; i++) pipsProperty.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            so.FindProperty("gameLabel").objectReferenceValue = game;
            so.FindProperty("hintBar").objectReferenceValue = hint.gameObject;
            so.FindProperty("hintFill").objectReferenceValue = hintFill;
            so.FindProperty("hintTagFill").objectReferenceValue = hintTagFill;
            so.FindProperty("hintTag").objectReferenceValue = hintTag;
            so.FindProperty("hintText").objectReferenceValue = hintText;
            so.FindProperty("hintGotIt").objectReferenceValue = gotIt;
            so.FindProperty("hintClose").objectReferenceValue = hintClose;
            SerializedProperty toolsProperty = so.FindProperty("tools");
            toolsProperty.arraySize = tools.Length;
            for (int i = 0; i < tools.Length; i++)
            {
                SerializedProperty tool = toolsProperty.GetArrayElementAtIndex(i);
                tool.FindPropertyRelative("kind").enumValueIndex = (int)tools[i].kind;
                tool.FindPropertyRelative("button").objectReferenceValue = tools[i].button;
                tool.FindPropertyRelative("fill").objectReferenceValue = tools[i].fill;
                tool.FindPropertyRelative("icon").objectReferenceValue = tools[i].icon;
            }

            so.FindProperty("undoButton").objectReferenceValue = undo;
            so.FindProperty("undoIcon").objectReferenceValue = undoIcon;
            so.FindProperty("redoButton").objectReferenceValue = redo;
            so.FindProperty("redoIcon").objectReferenceValue = redoIcon;
            so.FindProperty("shopButton").objectReferenceValue = shop;
            so.FindProperty("logButton").objectReferenceValue = log;
            so.FindProperty("helpButton").objectReferenceValue = help;
            so.FindProperty("settingsButton").objectReferenceValue = settings;
            so.FindProperty("menuButton").objectReferenceValue = menu;
            so.FindProperty("handStyleButton").objectReferenceValue = handStyle;
            so.FindProperty("rulebookButton").objectReferenceValue = rulebook;
            so.FindProperty("toolboxButton").objectReferenceValue = toolbox;
            so.FindProperty("playerChip").objectReferenceValue = chip.gameObject;
            so.FindProperty("playerDot").objectReferenceValue = dot;
            so.FindProperty("playerText").objectReferenceValue = playerText;
            so.FindProperty("actionsRoot").objectReferenceValue = actionsRoot;
            so.FindProperty("actionTemplate").objectReferenceValue = template;
            so.FindProperty("primaryButton").objectReferenceValue = primary;
            so.FindProperty("primaryLabel").objectReferenceValue = primaryLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TableHudView.RailTool RailTool(RectTransform rail, TableToolKind kind, string iconPath, string letter,
            string tip)
        {
            Button button = RailButton(rail, kind.ToString(), iconPath, letter, tip, out Image icon, out Image fill);
            return new TableHudView.RailTool { kind = kind, button = button, fill = fill, icon = icon };
        }

        private static Button RailButton(RectTransform rail, string name, string iconPath, string letter, string tip,
            out Image icon)
        {
            return RailButton(rail, name, iconPath, letter, tip, out icon, out _);
        }

        // A rail button: the chosen fill (orange, set by the view), a teal hover, the icon (or a letter when the
        // icon picture is missing) and its tip to the left.
        private static Button RailButton(RectTransform rail, string name, string iconPath, string letter, string tip,
            out Image icon, out Image fill)
        {
            RectTransform rect = Child(name, rail);
            Size(rect.gameObject, 52f, 52f, 52f);
            fill = Shape(Rect("Fill", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                new Color(0f, 0f, 0f, 0f), rounded);
            Image hover = Shape(Rect("Hover", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero), Cyan, rounded);
            hover.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hover;
            ColorBlock colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = new Color(1f, 1f, 1f, 0.28f);
            colours.pressedColor = new Color(1f, 1f, 1f, 0.45f);
            colours.selectedColor = new Color(1f, 1f, 1f, 0f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0f);
            button.colors = colours;
            Sprite picture = iconPath != null ? AssetDatabase.LoadAssetAtPath<Sprite>(iconPath) : null;
            RectTransform iconRect = Rect("Icon", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            if (picture != null)
            {
                icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = picture;
                icon.preserveAspect = true;
                icon.color = Soft;
                icon.raycastTarget = false;
            }
            else
            {
                icon = null;
                Label(iconRect, bold, 22, Soft, TextAnchor.MiddleCenter, letter);
            }

            Tip(rect, tip, true);
            return button;
        }

        private static void RailRule(RectTransform rail, string name)
        {
            RectTransform rule = Child(name, rail);
            Image line = rule.gameObject.AddComponent<Image>();
            line.color = Edge;
            line.raycastTarget = false;
            Size(rule.gameObject, 30f, 2f, 30f);
        }

        // A tip beside a button, shown while the pointer is over it (HudHoverTip).
        private static void Tip(RectTransform button, string text, bool toTheLeft)
        {
            RectTransform tipRect = Rect("Tip", button, toTheLeft ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f),
                toTheLeft ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f), toTheLeft ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
                new Vector2(toTheLeft ? -10f : 10f, 0f), new Vector2(160f, 32f));
            Plate(tipRect, Cyan, Hex("101314"), out Image tipFill);
            tipFill.raycastTarget = false; // the pointer passes through the tip, so it never sticks
            Row(tipRect.gameObject, 10, 10, 4, 4, 0).childAlignment = TextAnchor.MiddleCenter;
            Fit(tipRect.gameObject, true, false);
            Text label = FitLabel(Child("Text", tipRect), bold, 14, Words, text);
            label.alignment = TextAnchor.MiddleCenter;
            tipRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tipRect.gameObject.SetActive(false);
            HudHoverTip hover = button.gameObject.AddComponent<HudHoverTip>();
            SerializedObject so = new SerializedObject(hover);
            so.FindProperty("tip").objectReferenceValue = tipRect.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CornerButton(RectTransform parent, string name, string iconPath, string letter, Color tone,
            string tip)
        {
            RectTransform rect = Child(name, parent);
            Size(rect.gameObject, 64f, 64f, 64f);
            Image edge = Plate(rect, tone, Panel, out Image fill);
            _ = edge;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            Sprite picture = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            RectTransform iconRect = Rect("Icon", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            if (picture != null)
            {
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = picture;
                icon.preserveAspect = true;
                icon.color = tone;
                icon.raycastTarget = false;
            }
            else
            {
                Label(iconRect, bold, 26, tone, TextAnchor.MiddleCenter, letter);
            }

            Tip(rect, tip, false);
            return button;
        }

        private static Button ActionChip(RectTransform parent)
        {
            RectTransform rect = Child("ActionTemplate", parent);
            Image edge = Plate(rect, Edge, Button2, out Image fill);
            _ = edge;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            Row(rect.gameObject, 16, 16, 6, 6, 0).childAlignment = TextAnchor.MiddleCenter;
            Size(rect.gameObject, -1f, 38f);
            Text label = FitLabel(Child("Label", rect), bold, 14, Soft, "TAKE IT");
            label.alignment = TextAnchor.MiddleCenter;
            rect.gameObject.SetActive(false);
            return button;
        }

        // ---------- the rulebook ----------

        private static void BuildRulebook(RectTransform root, TableRulebookView view)
        {
            RectTransform backdropRect = Rect("Backdrop", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            Image dim = backdropRect.gameObject.AddComponent<Image>();
            dim.color = new Color(0.024f, 0.047f, 0.051f, 0.6f);
            Button backdrop = backdropRect.gameObject.AddComponent<Button>();
            backdrop.targetGraphic = dim;
            backdrop.transition = Selectable.Transition.None;

            RectTransform book = Rect("Book", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-40f, 40f), new Vector2(1340f, 840f));
            Image cover = Shape(book, DeepTeal, rounded);
            cover.raycastTarget = true; // clicks on the book do not close it
            cover.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -18f);

            Page(book, "LeftPage", 0f, true, out Text title, out Text leftHeading, out RectTransform leftLines,
                out Text leftNumber);
            Page(book, "RightPage", 0.5f, false, out _, out Text rightHeading, out RectTransform rightLines,
                out Text rightNumber);
            RulebookLineView template = RuleLine(leftLines);

            // Tabs down the right edge of the book.
            RectTransform tabsRoot = Rect("Tabs", book, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-6f, -40f), new Vector2(190f, 700f));
            VerticalLayoutGroup tabColumn = Column(tabsRoot.gameObject, 0, 0, 0, 0, 8);
            tabColumn.childForceExpandWidth = true;
            RectTransform tabRect = Child("TabTemplate", tabsRoot);
            Image tabFill = Shape(tabRect, PaperTab, rounded);
            tabFill.raycastTarget = true;
            Button tab = tabRect.gameObject.AddComponent<Button>();
            tab.targetGraphic = tabFill;
            Size(tabRect.gameObject, -1f, 54f);
            Label(Rect("Label", tabRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(6f, 0f),
                new Vector2(-28f, 0f)), bold, 14, InkSoft, TextAnchor.MiddleCenter, "YOUR TURN");
            tabRect.gameObject.SetActive(false);

            // The bookmark ribbon.
            RectTransform ribbon = Rect("Bookmark", book, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-120f, 12f), new Vector2(30f, 96f));
            Shape(ribbon, Orange, null);

            // The footer under the book.
            RectTransform footer = Rect("Footer", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(-40f, -394f), new Vector2(1340f, 54f));
            HorizontalLayoutGroup footerRow = Row(footer.gameObject, 0, 0, 0, 0, 10);
            footerRow.childAlignment = TextAnchor.MiddleLeft;
            Button previous = SmallButton(footer, "Previous", "‹", Panel, Words, 52f, 48f, out _);
            Button next = SmallButton(footer, "Next", "›", Panel, Words, 52f, 48f, out _);
            Text pageCount = FitLabel(Child("Pages", footer), medium, 16, Soft, "Pages 1–2 of 7");
            RectTransform ruleSet = Child("RuleSet", footer);
            Row(ruleSet.gameObject, 0, 0, 0, 0, 6).childAlignment = TextAnchor.MiddleLeft;
            Button previousSet = SmallButton(ruleSet, "PreviousSet", "‹", Panel, Cyan, 40f, 40f, out _);
            Text setName = FitLabel(Child("Name", ruleSet), bold, 15, Words, "Default rules");
            Button nextSet = SmallButton(ruleSet, "NextSet", "›", Panel, Cyan, 40f, 40f, out _);
            RectTransform pending = Child("Pending", footer);
            Row(pending.gameObject, 0, 0, 0, 0, 8).childAlignment = TextAnchor.MiddleLeft;
            Text pendingText = FitLabel(Child("Text", pending), medium, 14, Orange, "Changes start on restart");
            Button restart = SmallButton(pending, "Restart", "RESTART", OrangeDark, Words, 110f, 40f, out _);
            Size(Child("Spring", footer).gameObject, -1f, -1f).flexibleWidth = 1f;
            InputField search = SearchField(footer);
            Button close = SmallButton(footer, "Close", "CLOSE", Panel, Cyan, 110f, 48f, out _);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("backdropButton").objectReferenceValue = backdrop;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("leftHeading").objectReferenceValue = leftHeading;
            so.FindProperty("leftLines").objectReferenceValue = leftLines;
            so.FindProperty("leftPageNumber").objectReferenceValue = leftNumber;
            so.FindProperty("rightHeading").objectReferenceValue = rightHeading;
            so.FindProperty("rightLines").objectReferenceValue = rightLines;
            so.FindProperty("rightPageNumber").objectReferenceValue = rightNumber;
            so.FindProperty("lineTemplate").objectReferenceValue = template;
            so.FindProperty("tabsRoot").objectReferenceValue = tabsRoot;
            so.FindProperty("tabTemplate").objectReferenceValue = tab;
            so.FindProperty("previousButton").objectReferenceValue = previous;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("pageCountLabel").objectReferenceValue = pageCount;
            so.FindProperty("searchField").objectReferenceValue = search;
            so.FindProperty("ruleSetRow").objectReferenceValue = ruleSet.gameObject;
            so.FindProperty("previousSetButton").objectReferenceValue = previousSet;
            so.FindProperty("nextSetButton").objectReferenceValue = nextSet;
            so.FindProperty("ruleSetLabel").objectReferenceValue = setName;
            so.FindProperty("pendingRow").objectReferenceValue = pending.gameObject;
            so.FindProperty("pendingLabel").objectReferenceValue = pendingText;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // One page: paper, an optional small title, the heading, a scrolling list of rules, the page number.
        private static void Page(RectTransform book, string name, float left, bool withTitle, out Text title, out Text heading,
            out RectTransform lines, out Text number)
        {
            RectTransform page = Rect(name, book, new Vector2(left, 0f), new Vector2(left + 0.5f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            page.offsetMin = new Vector2(left == 0f ? 14f : 0f, 14f);
            page.offsetMax = new Vector2(left == 0f ? 0f : -14f, -14f);
            Image paper = Shape(page, Paper, null);
            paper.raycastTarget = true;
            title = Label(Rect("Title", page, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(44f, -34f),
                new Vector2(-88f, 22f)), bold, 14, OrangeDark, TextAnchor.UpperLeft, "RULEBOOK · TRAP FLOOR");
            title.gameObject.SetActive(withTitle);
            heading = Label(Rect("Heading", page, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(44f, withTitle ? -62f : -40f), new Vector2(-88f, 52f)), bold, 38, Ink, TextAnchor.UpperLeft,
                "Your turn");

            RectTransform viewport = Rect("Lines", page, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            viewport.offsetMin = new Vector2(44f, 64f);
            viewport.offsetMax = new Vector2(-44f, withTitle ? -128f : -106f);
            viewport.gameObject.AddComponent<RectMask2D>();
            CatchScroll(viewport);
            lines = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                Vector2.zero);
            VerticalLayoutGroup column = Column(lines.gameObject, 0, 0, 0, 0, 2);
            column.childForceExpandWidth = true;
            Fit(lines.gameObject, false, true);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = lines;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            number = Label(Rect("Number", page, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(-88f, 24f)), medium, 14, InkSoft,
                left == 0f ? TextAnchor.LowerLeft : TextAnchor.LowerRight, "4");
        }

        private static RulebookLineView RuleLine(RectTransform parent)
        {
            RectTransform row = Child("RuleTemplate", parent);
            HorizontalLayoutGroup layout = Row(row.gameObject, 0, 0, 10, 10, 14, TextAnchor.UpperLeft);
            layout.childForceExpandHeight = false;
            Image rule = Shape(Rect("Rule", row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(0f, 1f)), PaperLine, null);
            rule.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform numberRect = Child("Number", row);
            Shape(numberRect, Ink, circle);
            Size(numberRect.gameObject, 30f, 30f, 30f);
            Text number = Label(Rect("Text", numberRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero), bold, 14, Paper, TextAnchor.MiddleCenter, "1");
            _ = number;
            Text words = Label(Child("Words", row), medium, 17, Ink, TextAnchor.UpperLeft,
                "Move up to 2 tiles in one straight line.");
            Size(words.gameObject, -1f, -1f).flexibleWidth = 1f;

            RectTransform house = Child("House", row);
            Shape(house, Orange, rounded);
            Row(house.gameObject, 8, 8, 3, 3, 0).childAlignment = TextAnchor.MiddleCenter;
            Size(house.gameObject, -1f, 24f);
            FitLabel(Child("Label", house), bold, 12, OrangeInk, "HOUSE");

            Button change = SmallButton(row, "Change", "CHANGE", new Color(0f, 0f, 0f, 0f), InkSoft, 86f, 26f, out _);

            RulebookLineView view = row.gameObject.AddComponent<RulebookLineView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("numberLabel").objectReferenceValue = numberRect.GetComponentInChildren<Text>();
            so.FindProperty("textLabel").objectReferenceValue = words;
            so.FindProperty("houseMark").objectReferenceValue = house.gameObject;
            so.FindProperty("changeButton").objectReferenceValue = change;
            so.ApplyModifiedPropertiesWithoutUndo();
            row.gameObject.SetActive(false);
            return view;
        }

        private static InputField SearchField(RectTransform parent)
        {
            RectTransform rect = Child("Search", parent);
            Size(rect.gameObject, 280f, 48f, 280f);
            Image edge = Plate(rect, Edge, Panel, out Image fill);
            _ = edge;
            InputField field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = fill;
            Text text = Label(Rect("Text", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-28f, 0f)), medium, 16, Words, TextAnchor.MiddleLeft, string.Empty);
            text.supportRichText = false;
            Text placeholder = Label(Rect("Placeholder", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-28f, 0f)), medium, 16, Muted, TextAnchor.MiddleLeft, "Search the rules…");
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        // ---------- the Shop as a drawer ----------

        private static void BuildShopDrawer(RectTransform root, ActionAbilityPurchasePopupView view)
        {
            // A see-through overlay: the table stays in view beside the drawer; a click beside it closes the Shop.
            Button overlay = InvisibleButton(root, "Overlay");

            RectTransform panel = Rect("Drawer", root, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-96f, 0f), new Vector2(460f, -40f));
            Plate(panel, Orange, Panel);
            VerticalLayoutGroup column = Column(panel.gameObject, 0, 0, 0, 16, 12);
            column.childForceExpandWidth = true;

            RectTransform header = Child("Header", panel);
            Row(header.gameObject, 0, 10, 0, 0, 12).childAlignment = TextAnchor.MiddleLeft;
            Size(header.gameObject, -1f, 56f);
            RectTransform cap = Child("Cap", header);
            Shape(cap, Orange, null);
            Size(cap.gameObject, 10f, 56f, 10f);
            Text title = FitLabel(Child("Title", header), bold, 22, Words, "SHOP");
            Size(Child("Spring", header).gameObject, -1f, -1f).flexibleWidth = 1f;
            Button close = SmallButton(header, "Close", "×", Button2, Words, 40f, 40f, out _);

            RectTransform body = Child("Body", panel);
            Column(body.gameObject, 16, 16, 0, 0, 10).childForceExpandWidth = true;
            Size(body.gameObject, -1f, -1f).flexibleHeight = 1f;
            Text listTitle = FitLabel(Child("ListTitle", body), bold, 13, Muted, "ABILITIES");

            RectTransform viewport = Child("Catalog", body);
            Size(viewport.gameObject, -1f, 300f).flexibleHeight = 1f;
            viewport.gameObject.AddComponent<RectMask2D>();
            CatchScroll(viewport);
            RectTransform rows = Rect("Rows", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            Column(rows.gameObject, 0, 0, 0, 0, 6).childForceExpandWidth = true;
            Fit(rows.gameObject, false, true);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = rows;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            RectTransform detail = Child("Detail", body);
            Plate(detail, Cyan, Hex("1c1f1f"));
            Row(detail.gameObject, 12, 12, 12, 12, 14).childAlignment = TextAnchor.UpperLeft;
            RectTransform frame = Child("Art", detail);
            Shape(frame, Button2, rounded);
            Size(frame.gameObject, 120f, 168f, 120f);
            RawImage artwork = Rect("Artwork", frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-6f, -6f)).gameObject.AddComponent<RawImage>();
            artwork.raycastTarget = false;
            RectTransform words = Child("Words", detail);
            Column(words.gameObject, 0, 0, 0, 0, 6).childForceExpandWidth = true;
            Size(words.gameObject, -1f, -1f).flexibleWidth = 1f;
            Text cardName = Label(Child("Name", words), bold, 20, Words, TextAnchor.UpperLeft, "Rush");
            Text description = Label(Child("Description", words), medium, 14, Soft, TextAnchor.UpperLeft, "What it does.");
            Text cost = Label(Child("Cost", words), bold, 15, Orange, TextAnchor.UpperLeft, "Cost 2");
            Text afford = Label(Child("Afford", words), medium, 14, Hex("2fd46b"), TextAnchor.UpperLeft, "You have 4");

            Text status = Label(Child("Status", body), bold, 14, Orange, TextAnchor.UpperLeft, string.Empty);
            Button buy = PillButton(body, "Buy", "BUY", Teal, Hex("f4fefe"), -1f, 50f, out Text buyLabel);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("catalogRowsRoot").objectReferenceValue = rows;
            so.FindProperty("catalogScrollRect").objectReferenceValue = scroll;
            so.FindProperty("cardNameLabel").objectReferenceValue = cardName;
            so.FindProperty("artworkImage").objectReferenceValue = artwork;
            so.FindProperty("descriptionLabel").objectReferenceValue = description;
            so.FindProperty("inputCostLabel").objectReferenceValue = cost;
            so.FindProperty("affordabilityLabel").objectReferenceValue = afford;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("buyButton").objectReferenceValue = buy;
            so.FindProperty("buyButtonLabel").objectReferenceValue = buyLabel;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("listTitleLabel").objectReferenceValue = listTitle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- the payment as a strip at the bottom ----------

        private static void BuildPaymentStrip(RectTransform root, FocusedCardSelectionView view)
        {
            Button overlay = InvisibleButton(root, "Overlay");
            RectTransform panel = Rect("Strip", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(1240f, 430f));
            Plate(panel, Orange, Panel);

            RectTransform header = Rect("Header", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 64f));
            RectTransform cap = Rect("Cap", header, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(10f, 0f));
            Shape(cap, Orange, null);
            Text title = Label(Rect("Title", header, new Vector2(0f, 0f), new Vector2(0.4f, 1f), new Vector2(0f, 0.5f),
                new Vector2(30f, 0f), new Vector2(-30f, 0f)), bold, 20, Orange, TextAnchor.MiddleLeft, "PAY FOR RUSH");
            Text instruction = Label(Rect("Instruction", header, new Vector2(0.4f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(-30f, 0f)), medium, 16, Words, TextAnchor.MiddleLeft,
                "Choose 2 Controller cards");

            RectTransform viewport = Rect("CardArea", panel, new Vector2(0.02f, 0.17f), new Vector2(0.98f, 0.84f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform options = Rect("Options", viewport, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(620f, 260f));
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = options;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            Text status = Label(Rect("Status", panel, Vector2.zero, new Vector2(0.55f, 0f), new Vector2(0f, 0f),
                new Vector2(30f, 16f), new Vector2(-30f, 44f)), bold, 16, Muted, TextAnchor.MiddleLeft, string.Empty);
            RectTransform buttons = Rect("Buttons", panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 14f), new Vector2(460f, 50f));
            Row(buttons.gameObject, 0, 0, 0, 0, 10).childAlignment = TextAnchor.MiddleRight;
            Button cancel = PillButton(buttons, "Cancel", "CANCEL", Button2, Soft, 170f, 50f, out Text cancelLabel);
            Button confirm = PillButton(buttons, "Confirm", "CONFIRM PAYMENT", Teal, Hex("f4fefe"), 250f, 50f, out Text confirmLabel);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("optionsRoot").objectReferenceValue = options;
            so.FindProperty("optionsScrollRect").objectReferenceValue = scroll;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("instructionLabel").objectReferenceValue = instruction;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.FindProperty("confirmButtonLabel").objectReferenceValue = confirmLabel;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.FindProperty("cancelButtonLabel").objectReferenceValue = cancelLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- parts ----------

        // A see-through graphic so a ScrollRect's viewport gets the wheel and drags.
        private static void CatchScroll(RectTransform viewport)
        {
            Image catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
        }

        private static Image Shape(RectTransform rect, Color colour, Sprite sprite)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite == rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static Image Plate(RectTransform panel, Color edge, Color fill) => Plate(panel, edge, fill, out _);

        // A panel's edge and fill, behind its contents and outside its layout; the fill takes the clicks.
        private static Image Plate(RectTransform panel, Color edge, Color fill, out Image fillImage)
        {
            RectTransform edgeRect = Rect("Edge", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(3f, 3f));
            Image edgeImage = Shape(edgeRect, edge, rounded);
            edgeRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            edgeRect.SetAsFirstSibling();
            RectTransform fillRect = Rect("Fill", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            fillImage = Shape(fillRect, fill, rounded);
            fillImage.raycastTarget = true;
            fillRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            fillRect.SetSiblingIndex(1);
            return edgeImage;
        }

        private static Image Tag(Transform parent, string text, Color fill, Color words, out Text label)
        {
            RectTransform rect = Child("Tag", parent);
            Image image = Shape(rect, fill, rounded);
            Row(rect.gameObject, 10, 10, 4, 4, 0, TextAnchor.MiddleCenter);
            label = FitLabel(Child("Label", rect), bold, 13, words, text);
            label.alignment = TextAnchor.MiddleCenter;
            Size(rect.gameObject, -1f, 30f);
            return image;
        }

        private static Button SmallButton(Transform parent, string name, string text, Color fill, Color words,
            float width, float height, out Text label)
        {
            RectTransform rect = Child(name, parent);
            Image image = Shape(rect, fill, rounded);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Size(rect.gameObject, width, height, width);
            label = Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                bold, height >= 40f ? 18 : 12, words, TextAnchor.MiddleCenter, text);
            return button;
        }

        private static Button PillButton(Transform parent, string name, string text, Color fill, Color words, float width,
            float height, out Text label)
        {
            RectTransform rect = Child(name, parent);
            Image image = Shape(rect, fill, rounded);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            LayoutElement size = Size(rect.gameObject, width, height, width >= 0f ? width : -1f);
            if (width < 0f) size.flexibleWidth = 1f;
            label = Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-16f, 0f)), bold, height >= 54f ? 21 : 17, words, TextAnchor.MiddleCenter, text);
            return button;
        }

        // A see-through overlay over the whole screen: the table stays in view, a click beside the panel closes it
        // (the view's own dismiss, which cancels a payment), and nothing else is pressed while it is open.
        private static Button InvisibleButton(RectTransform root, string name)
        {
            RectTransform rect = Rect(name, root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.001f);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }
    }
}
