using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Builds the authored gameplay UI prefabs in the Toolbox design language: the controls strip and help card,
    /// the Trap Floor turn strip and status card and the action row (UI-1b); the right-click menu, draw count and
    /// merge panels, the quantity popup, card inspection, ability purchase and focused card choice (UI-1c). Each prefab carries the existing view script with the same references, and the build points the
    /// Real UI prefab catalog's entries at the new prefabs. Nothing is built at runtime.
    /// </summary>
    public static class GameplayUiBuilder
    {
        public const string Folder = "Assets/ConsoleCards/Content/Prefabs/Real/UI/";
        public const string GuidePath = Folder + "InteractionGuide.prefab";
        public const string TrapFloorHudPath = Folder + "TrapFloorHud.prefab";
        public const string ActionRowPath = Folder + "ActionRow.prefab";
        public const string TabletopPopupPath = Folder + "TabletopPopup.prefab";
        public const string QuantityPopupPath = Folder + "QuantityPopup.prefab";
        public const string CardInspectPath = Folder + "CardInspect.prefab";
        public const string AbilityPurchasePath = Folder + "AbilityPurchase.prefab";
        public const string FocusedSelectionPath = Folder + "FocusedCardSelection.prefab";
        public const string FocusedOptionPath = Folder + "FocusedCardOption.prefab";

        // Layout, in canvas units at the 1920 x 1080 reference.
        private const float Margin = 29f;
        private const float ShadowOffset = 7f;
        private const float StatusCardWidth = 440f;
        private const float GuideWidth = 580f;
        private const float StripHeight = 60f;
        private const float RowHeight = 50f;
        private const float MenuWidth = 400f;

        private const string GuideText =
            "<color=#e0531f><b>SELECT AND MOVE</b></color>\n"
            + "Click a piece to select it; click the table to clear.\n"
            + "Drag a piece to move it. <b>Esc</b> cancels a drag.\n"
            + "<b>Right-click</b> a piece for its menu.\n\n"
            + "<color=#e0531f><b>CARDS</b></color>\n"
            + "Drag a card onto a deck, stack, discard pile or console slot to put it there.\n"
            + "Drag a card out of a pile to take it.\n"
            + "Drag hand cards left or right to reorder. <b>H</b> shows or hides the hand.\n"
            + "<b>F</b> flips the selected card.\n\n"
            + "<color=#e0531f><b>ROTATE</b></color>\n"
            + "<b>Mouse wheel</b> turns the selected piece, or the piece you are placing, 15°.\n\n"
            + "<color=#e0531f><b>CAMERA</b></color>\n"
            + "<b>Mouse wheel</b> with nothing selected zooms.\n"
            + "<b>WASD</b>, the <b>arrow keys</b> or a <b>middle-button drag</b> pans.\n"
            + "<b>1 2 3 4</b>: close, mid, board and top-down views.\n\n"
            + "<color=#e0531f><b>TABLE</b></color>\n"
            + "<b>Ctrl+Z</b> undo, <b>Ctrl+Y</b> redo. Toolbox adds pieces; the Table menu starts, resets or clears.";

        private static Font titleFont;
        private static Font bodyFont;
        private static Font boldFont;

        [MenuItem("Console Cards/UI/Build Gameplay UI Prefabs")]
        public static void Build()
        {
            ConfigureTexture(ToolboxIconPath + "IconClose.png", Vector4.zero);
            titleFont = RequireFont("Silkscreen-Bold.ttf");
            bodyFont = RequireFont("ChakraPetch-Medium.ttf");
            boldFont = RequireFont("ChakraPetch-Bold.ttf");
            Directory.CreateDirectory(Folder);

            Save<PrototypePopupActionRowView>(ActionRowPath, "ActionRow", BuildActionRow, stretch: false);
            Save<PrototypeInteractionGuide>(GuidePath, "InteractionGuide", BuildGuide, stretch: true);
            Save<PrototypeTrapFloorHudView>(TrapFloorHudPath, "TrapFloorHud", BuildTrapFloorHud, stretch: true);
            Save<PrototypeTabletopPopupView>(TabletopPopupPath, "TabletopPopup", BuildTabletopPopup, stretch: true);
            Save<PrototypeQuantityPopupView>(QuantityPopupPath, "QuantityPopup", BuildQuantityPopup, stretch: true);
            Save<PrototypeCardInspectView>(CardInspectPath, "CardInspect", BuildCardInspect, stretch: true);
            Save<PrototypeActionAbilityPurchasePopupView>(AbilityPurchasePath, "AbilityPurchase", BuildAbilityPurchase, stretch: true);
            Save<PrototypeFocusedCardSelectionView>(FocusedSelectionPath, "FocusedCardSelection", BuildFocusedSelection, stretch: true);
            Save<PrototypeFocusedCardOptionView>(FocusedOptionPath, "FocusedCardOption", BuildFocusedOption, stretch: false);

            AssignCatalogEntry(PrototypeUiPrefabIds.PopupActionRow,
                AssetDatabase.LoadAssetAtPath<PrototypePopupActionRowView>(ActionRowPath), UiLayer.Popup, UiRetention.Pooled);
            AssignCatalogEntry(PrototypeUiPrefabIds.InteractionGuide,
                AssetDatabase.LoadAssetAtPath<PrototypeInteractionGuide>(GuidePath), UiLayer.Hud, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.TrapFloorHud,
                AssetDatabase.LoadAssetAtPath<PrototypeTrapFloorHudView>(TrapFloorHudPath), UiLayer.Hud, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.TabletopPopup,
                AssetDatabase.LoadAssetAtPath<PrototypeTabletopPopupView>(TabletopPopupPath), UiLayer.Popup, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.QuantityPopup,
                AssetDatabase.LoadAssetAtPath<PrototypeQuantityPopupView>(QuantityPopupPath), UiLayer.Modal, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.CardInspect,
                AssetDatabase.LoadAssetAtPath<PrototypeCardInspectView>(CardInspectPath), UiLayer.Modal, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.ActionAbilityPurchase,
                AssetDatabase.LoadAssetAtPath<PrototypeActionAbilityPurchasePopupView>(AbilityPurchasePath), UiLayer.Modal,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.FocusedCardSelection,
                AssetDatabase.LoadAssetAtPath<PrototypeFocusedCardSelectionView>(FocusedSelectionPath), UiLayer.Modal,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.FocusedCardOption,
                AssetDatabase.LoadAssetAtPath<PrototypeFocusedCardOptionView>(FocusedOptionPath), UiLayer.Modal, UiRetention.Pooled);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gameplay UI] Built the gameplay UI prefabs and updated the Real UI prefab catalog.");
        }

        private static void Save<T>(string path, string name, System.Action<RectTransform, T> build, bool stretch)
            where T : ReusableUiView
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.layer = 5;
            try
            {
                RectTransform rect = root.GetComponent<RectTransform>();
                if (stretch)
                {
                    Stretch(rect);
                }

                T view = root.AddComponent<T>();
                build(rect, view);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---------- action row (Trap Floor actions, right-click menus) ----------

        private static void BuildActionRow(RectTransform root, PrototypePopupActionRowView view)
        {
            root.sizeDelta = new Vector2(380f, RowHeight);
            Image fill = Img(root, "ButtonOutlined", Cream);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.95f, 0.88f, 0.7f, 1f);
            colors.disabledColor = new Color(0.86f, 0.83f, 0.75f, 0.7f);
            button.colors = colors;
            Size(root.gameObject, -1f, RowHeight);
            Text label = Label(Rect("Label", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(4f, 0f),
                new Vector2(-40f, 0f)), boldFont, 18, Ink, TextAnchor.MiddleLeft, "Action");

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- controls strip and help card ----------

        private static void BuildGuide(RectTransform root, PrototypeInteractionGuide view)
        {
            // Controls strip, bottom right: the everyday controls and the Help button.
            RectTransform strip = Rect("ControlsStrip", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Margin, Margin), new Vector2(560f, StripHeight));
            Img(strip, "ButtonOutlined", Ink);
            Row(strip.gameObject, 20, 8, 8, 8, 18f);
            Fit(strip.gameObject, true, true);
            Size(strip.gameObject, -1f, StripHeight);
            FitLabel(Child("Controls", strip), bodyFont, 17, Cream,
                Key("Drag") + " move    " + Key("Wheel") + " rotate    " + Key("F") + " flip    " + Key("Right-click") + " menu");
            Button toggle = TextButton(strip, "HelpButton", "? Help", boldFont, 17, Mustard, Ink, 150f, 44f, out Text toggleLabel);

            // Help card above the strip (closed until Help is pressed).
            RectTransform card = Rect("HelpCard", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Margin, Margin + StripHeight + 20f), new Vector2(GuideWidth, 400f));
            Backdrop(card, ShadowOffset);
            Column(card.gameObject, 5, 5, 5, 5, 0f);
            Fit(card.gameObject, false, true);
            RectTransform header = Child("Header", card);
            Img(header, "TopRounded", Orange).raycastTarget = false;
            Size(header.gameObject, -1f, 58f);
            Text title = Label(Rect("Title", header, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f),
                new Vector2(-84f, 0f)), titleFont, 22, Cream, TextAnchor.MiddleLeft, "HOW TO PLAY");
            RectTransform close = Rect("CloseButton", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(42f, 42f));
            Image closeFill = Img(close, "ButtonOutlined", Cream);
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeFill;
            Img(Rect("Icon", close, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(22f, 22f)), null, Ink, ToolboxIconPath + "IconClose.png").raycastTarget = false;
            RectTransform headerRule = Child("HeaderRule", card);
            Img(headerRule, "Solid", Ink).raycastTarget = false;
            Size(headerRule.gameObject, -1f, 5f);
            RectTransform body = Child("Body", card);
            Column(body.gameObject, 22, 22, 16, 20, 0f);
            Text bodyLabel = Label(Child("Text", body), bodyFont, 17, Ink, TextAnchor.UpperLeft, GuideText);
            bodyLabel.lineSpacing = 1.1f;
            card.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("guidePanel").objectReferenceValue = card.gameObject;
            so.FindProperty("toggleButton").objectReferenceValue = toggle;
            so.FindProperty("toggleButtonLabel").objectReferenceValue = toggleLabel;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("bodyLabel").objectReferenceValue = bodyLabel;
            so.FindProperty("title").stringValue = "HOW TO PLAY";
            so.FindProperty("guideText").stringValue = GuideText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Trap Floor: turn strip (top centre) and status card (top right) ----------

        private static void BuildTrapFloorHud(RectTransform root, PrototypeTrapFloorHudView view)
        {
            RectTransform strip = Rect("TurnStrip", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -Margin), new Vector2(500f, 64f));
            Img(strip, "ButtonOutlined", Ink);
            Row(strip.gameObject, 24, 24, 10, 10, 28f, TextAnchor.MiddleCenter);
            Fit(strip.gameObject, true, true);
            Size(strip.gameObject, -1f, 64f);
            Text round = FitLabel(Child("Round", strip), titleFont, 18, Mustard, "ROUND 1");
            Text phase = FitLabel(Child("Phase", strip), boldFont, 21, Cream, "PLAYER 1 TURN");
            Text keys = FitLabel(Child("Keys", strip), boldFont, 19, Cream, "KEYS 0 / 1");

            RectTransform card = Rect("StatusCard", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Margin, -Margin), new Vector2(StatusCardWidth, 400f));
            Backdrop(card, ShadowOffset);
            Column(card.gameObject, 5, 5, 5, 5, 0f);
            Fit(card.gameObject, false, true);
            RectTransform header = Child("Header", card);
            Img(header, "TopRounded", Orange).raycastTarget = false;
            Size(header.gameObject, -1f, 54f);
            Label(Rect("Title", header, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f),
                new Vector2(-36f, 0f)), titleFont, 22, Cream, TextAnchor.MiddleLeft, "TRAP FLOOR");
            RectTransform rule = Child("HeaderRule", card);
            Img(rule, "Solid", Ink).raycastTarget = false;
            Size(rule.gameObject, -1f, 5f);

            RectTransform body = Child("Body", card);
            Column(body.gameObject, 18, 18, 14, 18, 10f);
            Text detail = Label(Child("Detail", body), bodyFont, 16, Ink, TextAnchor.UpperLeft, "Detail");
            Text counts = Label(Child("ContainerCounts", body), bodyFont, 15, Muted, TextAnchor.UpperLeft, "Counts");

            RectTransform floorfall = Child("Floorfall", body);
            Img(floorfall, "ButtonOutlined", CreamDark).raycastTarget = false;
            Column(floorfall.gameObject, 14, 14, 10, 12, 4f);
            FitLabel(Child("Title", floorfall), titleFont, 15, Orange, "FLOORFALL");
            Text dice = Label(Child("Dice", floorfall), boldFont, 17, Ink, TextAnchor.UpperLeft, "Dice");
            Text coordinate = Label(Child("Coordinate", floorfall), bodyFont, 16, Ink, TextAnchor.UpperLeft, "Coordinate");
            Text target = Label(Child("Target", floorfall), bodyFont, 16, Ink, TextAnchor.UpperLeft, "Target");

            RectTransform actionsTitle = Child("ActionsTitle", body);
            Label(actionsTitle, titleFont, 15, Muted, TextAnchor.LowerLeft, "ASSISTED ACTIONS");
            Size(actionsTitle.gameObject, -1f, 26f);
            RectTransform actions = Child("Actions", body);
            Column(actions.gameObject, 0, 0, 0, 0, 8f);
            Text help = Label(Child("ActionHelp", body), bodyFont, 14, Muted, TextAnchor.UpperLeft, "Help");

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("roundLabel").objectReferenceValue = round;
            so.FindProperty("phaseLabel").objectReferenceValue = phase;
            so.FindProperty("searchProgressLabel").objectReferenceValue = keys;
            so.FindProperty("detailLabel").objectReferenceValue = detail;
            so.FindProperty("containerCountsLabel").objectReferenceValue = counts;
            so.FindProperty("floorfallPanel").objectReferenceValue = floorfall.gameObject;
            so.FindProperty("floorfallDiceLabel").objectReferenceValue = dice;
            so.FindProperty("floorfallCoordinateLabel").objectReferenceValue = coordinate;
            so.FindProperty("floorfallTargetLabel").objectReferenceValue = target;
            so.FindProperty("actionsRoot").objectReferenceValue = actions;
            so.FindProperty("actionsTitle").objectReferenceValue = actionsTitle.gameObject;
            so.FindProperty("actionHelpLabel").objectReferenceValue = help;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- shared pieces for menus and modals (UI-1c) ----------

        // Orange title band for a column card; returns the title label.
        private static Text CardHeader(RectTransform card, string title, int fontSize, float height)
        {
            RectTransform header = Child("Header", card);
            Img(header, "TopRounded", Orange).raycastTarget = false;
            Size(header.gameObject, -1f, height);
            Text label = Label(Rect("Title", header, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f),
                new Vector2(-84f, 0f)), titleFont, fontSize, Cream, TextAnchor.MiddleLeft, title);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = fontSize;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform rule = Child("HeaderRule", card);
            Img(rule, "Solid", Ink).raycastTarget = false;
            Size(rule.gameObject, -1f, 5f);
            return label;
        }

        // Cream square button with the Toolbox close icon, at the right of a header band.
        private static Button HeaderCloseButton(RectTransform card)
        {
            Transform header = card.Find("Header");
            RectTransform close = Rect("CloseButton", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(42f, 42f));
            Image fill = Img(close, "ButtonOutlined", Cream);
            Button button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            Img(Rect("Icon", close, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(22f, 22f)), null, Ink, ToolboxIconPath + "IconClose.png").raycastTarget = false;
            return button;
        }

        // A card that sizes to its contents: shadow, cream fill, a column of children.
        private static RectTransform ColumnCard(Transform parent, string name, Vector2 anchor, Vector2 pivot, float width)
        {
            RectTransform card = Rect(name, parent, anchor, anchor, pivot, Vector2.zero, new Vector2(width, 300f));
            Backdrop(card, ShadowOffset);
            Column(card.gameObject, 5, 5, 5, 5, 0f);
            Fit(card.gameObject, false, true);
            return card;
        }

        private static RectTransform Body(RectTransform card, int padding, float spacing)
        {
            RectTransform body = Child("Body", card);
            Column(body.gameObject, padding, padding, padding - 4, padding, spacing);
            return body;
        }

        // Dim full-screen backdrop that closes the modal when clicked.
        private static Button DismissOverlay(RectTransform root)
        {
            RectTransform overlay = Rect("DismissOverlay", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Image image = Img(overlay, "Solid", BackdropDim);
            Button button = overlay.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        // Swallows clicks on a panel so they do not reach the dismiss handler behind it.
        private static void BlockClicks(RectTransform panel)
        {
            Button button = panel.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        // − count + stepper; returns the count label.
        private static Text Stepper(Transform parent, out Button decrement, out Button increment)
        {
            RectTransform row = Child("Stepper", parent);
            Row(row.gameObject, 0, 0, 0, 0, 14f, TextAnchor.MiddleCenter);
            Size(row.gameObject, -1f, 56f);
            decrement = TextButton(row, "DecrementButton", "-", boldFont, 28, Cream, Ink, 56f, 56f, out _);
            RectTransform countRect = Child("Count", row);
            Text count = Label(countRect, titleFont, 32, Ink, TextAnchor.MiddleCenter, "1");
            Size(countRect.gameObject, 90f, 56f, 90f);
            increment = TextButton(row, "IncrementButton", "+", boldFont, 28, Cream, Ink, 56f, 56f, out _);
            return count;
        }

        private static RectTransform ButtonRow(Transform parent, TextAnchor alignment = TextAnchor.MiddleRight)
        {
            RectTransform row = Child("Buttons", parent);
            Row(row.gameObject, 0, 0, 6, 0, 12f, alignment);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            return row;
        }

        private static Button GoButton(Transform parent, string name, string text, float width, out Text label) =>
            TextButton(parent, name, text, boldFont, 18, Go, Cream, width, 52f, out label);

        private static Button PlainButton(Transform parent, string name, string text, float width, out Text label) =>
            TextButton(parent, name, text, boldFont, 18, Cream, Ink, width, 52f, out label);

        private static RectTransform ScrollList(Transform parent, string name, float height, out ScrollRect scroll,
            out RectTransform content)
        {
            RectTransform viewport = Child(name, parent);
            Img(viewport, "ButtonOutlined", CreamDark);
            viewport.gameObject.AddComponent<RectMask2D>();
            Size(viewport.gameObject, -1f, height);
            content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            Column(content.gameObject, 10, 10, 10, 10, 8f);
            Fit(content.gameObject, false, true);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return viewport;
        }

        // ---------- right-click menu, draw count, merge destinations ----------

        private static void BuildTabletopPopup(RectTransform root, PrototypeTabletopPopupView view)
        {
            // Root: invisible full-screen click catcher (left click dismisses, right click re-targets) and bounds.
            Img(root, "Solid", new Color(0f, 0f, 0f, 0f));
            Vector2 corner = Vector2.zero;
            Vector2 topLeft = new Vector2(0f, 1f);

            RectTransform context = ColumnCard(root, "ContextMenuPanel", corner, topLeft, MenuWidth);
            BlockClicks(context);
            Text contextTitle = CardHeader(context, "MENU", 19, 52f);
            RectTransform contextBody = Body(context, 14, 8f);
            Text contextBodyLabel = Label(Child("Message", contextBody), bodyFont, 16, Ink, TextAnchor.UpperLeft, "Details");
            RectTransform contextActions = Child("Actions", contextBody);
            Column(contextActions.gameObject, 0, 0, 0, 0, 8f);
            context.gameObject.SetActive(false);

            RectTransform draw = ColumnCard(root, "DrawCountPanel", corner, topLeft, MenuWidth);
            BlockClicks(draw);
            CardHeader(draw, "DRAW CARDS", 19, 52f);
            RectTransform drawBody = Body(draw, 16, 10f);
            Text drawCount = Stepper(drawBody, out Button drawDecrement, out Button drawIncrement);
            Text drawAvailable = Label(Child("Available", drawBody), bodyFont, 15, Muted, TextAnchor.MiddleCenter, "Available: 0");
            RectTransform drawButtons = ButtonRow(drawBody);
            Button drawCancel = PlainButton(drawButtons, "CancelButton", "Cancel", 130f, out _);
            Button drawConfirm = GoButton(drawButtons, "DrawButton", "Draw", 130f, out _);
            draw.gameObject.SetActive(false);

            RectTransform merge = ColumnCard(root, "MergeDestinationPanel", corner, topLeft, MenuWidth);
            BlockClicks(merge);
            CardHeader(merge, "MERGE INTO", 19, 52f);
            RectTransform mergeBody = Body(merge, 14, 10f);
            RectTransform mergeViewport = ScrollList(mergeBody, "DestinationsViewport", 270f, out ScrollRect mergeScroll,
                out RectTransform mergeContent);
            RectTransform mergeButtons = ButtonRow(mergeBody, TextAnchor.MiddleLeft);
            Button mergeBack = PlainButton(mergeButtons, "BackButton", "Back", 130f, out _);
            merge.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("popupBounds").objectReferenceValue = root;
            so.FindProperty("contextPanel").objectReferenceValue = context;
            so.FindProperty("contextTitleLabel").objectReferenceValue = contextTitle;
            so.FindProperty("contextBodyLabel").objectReferenceValue = contextBodyLabel;
            so.FindProperty("contextActionsRoot").objectReferenceValue = contextActions;
            so.FindProperty("drawCountPanel").objectReferenceValue = draw;
            so.FindProperty("drawCountLabel").objectReferenceValue = drawCount;
            so.FindProperty("drawAvailableLabel").objectReferenceValue = drawAvailable;
            so.FindProperty("drawDecrementButton").objectReferenceValue = drawDecrement;
            so.FindProperty("drawIncrementButton").objectReferenceValue = drawIncrement;
            so.FindProperty("drawConfirmButton").objectReferenceValue = drawConfirm;
            so.FindProperty("drawCancelButton").objectReferenceValue = drawCancel;
            so.FindProperty("mergePanel").objectReferenceValue = merge;
            so.FindProperty("mergeViewport").objectReferenceValue = mergeViewport;
            so.FindProperty("mergeActionsRoot").objectReferenceValue = mergeContent;
            so.FindProperty("mergeScrollRect").objectReferenceValue = mergeScroll;
            so.FindProperty("mergeBackButton").objectReferenceValue = mergeBack;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- quantity popup ----------

        private static void BuildQuantityPopup(RectTransform root, PrototypeQuantityPopupView view)
        {
            Button overlay = DismissOverlay(root);
            Vector2 centre = new Vector2(0.5f, 0.5f);
            RectTransform panel = ColumnCard(root, "QuantityPanel", centre, centre, 540f);
            Text title = CardHeader(panel, "QUANTITY", 21, 58f);
            RectTransform body = Body(panel, 22, 12f);
            Text description = Label(Child("Description", body), bodyFont, 17, Ink, TextAnchor.UpperLeft, "Choose a quantity.");
            Text count = Stepper(body, out Button decrement, out Button increment);
            Text range = Label(Child("Range", body), bodyFont, 15, Muted, TextAnchor.MiddleCenter, "Allowed: 1 - 20");
            RectTransform buttons = ButtonRow(body);
            Button cancel = PlainButton(buttons, "CancelButton", "Cancel", 140f, out _);
            Button confirm = GoButton(buttons, "ConfirmButton", "Confirm", 180f, out Text confirmLabel);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("descriptionLabel").objectReferenceValue = description;
            so.FindProperty("countLabel").objectReferenceValue = count;
            so.FindProperty("rangeLabel").objectReferenceValue = range;
            so.FindProperty("decrementButton").objectReferenceValue = decrement;
            so.FindProperty("incrementButton").objectReferenceValue = increment;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.FindProperty("confirmButtonLabel").objectReferenceValue = confirmLabel;
            so.FindProperty("cancelButton").objectReferenceValue = cancel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- inspect a card ----------

        private static void BuildCardInspect(RectTransform root, PrototypeCardInspectView view)
        {
            Button overlay = DismissOverlay(root);
            Vector2 centre = new Vector2(0.5f, 0.5f);
            RectTransform panel = ColumnCard(root, "CardInspectPanel", centre, centre, 880f);
            CardHeader(panel, "INSPECT", 21, 58f);
            RectTransform body = Child("Body", panel);
            Row(body.gameObject, 26, 26, 24, 26, 30f, TextAnchor.UpperLeft);

            // The card face: surface tinted by the side, its title, artwork and text.
            RectTransform surfaceRect = Child("CardSurface", body);
            Image surface = Img(surfaceRect, "TileOutlined", Cream);
            surface.raycastTarget = false;
            Size(surfaceRect.gameObject, 400f, 560f, 400f);
            Text sideTitle = Label(Rect("SideTitle", surfaceRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(-48f, 40f)), boldFont, 26, Ink, TextAnchor.MiddleLeft, "Card");
            Image artwork = Img(Rect("Artwork", surfaceRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -74f), new Vector2(-48f, 280f)), null, Color.white);
            artwork.preserveAspect = true;
            artwork.raycastTarget = false;
            Text sideBody = Label(Rect("SideBody", surfaceRect, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(-48f, 160f)), bodyFont, 18, Ink, TextAnchor.UpperLeft, "Text");
            RectTransform icons = Rect("IconsRoot", surfaceRect, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(-48f, 40f));
            icons.gameObject.SetActive(false);

            // The column beside it: name, face, the side switch or the card's action, Close.
            RectTransform side = Child("Details", body);
            Column(side.gameObject, 0, 0, 4, 0, 14f);
            Size(side.gameObject, 360f, -1f, 360f);
            Text identity = Label(Child("CardName", side), boldFont, 26, Ink, TextAnchor.UpperLeft, "Card");
            Text faceState = Label(Child("FaceState", side), bodyFont, 17, Muted, TextAnchor.UpperLeft, "Face up");
            Button otherSide = PlainButton(side, "ViewOtherSideButton", "View back", 360f, out Text otherSideLabel);
            Button close = PlainButton(side, "CloseButton", "Close", 360f, out _);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("identityLabel").objectReferenceValue = identity;
            so.FindProperty("faceStateLabel").objectReferenceValue = faceState;
            so.FindProperty("cardSurface").objectReferenceValue = surface;
            so.FindProperty("artworkImage").objectReferenceValue = artwork;
            so.FindProperty("sideTitleLabel").objectReferenceValue = sideTitle;
            so.FindProperty("sideBodyLabel").objectReferenceValue = sideBody;
            so.FindProperty("iconsRoot").objectReferenceValue = icons;
            so.FindProperty("viewOtherSideButton").objectReferenceValue = otherSide;
            so.FindProperty("viewOtherSideButtonLabel").objectReferenceValue = otherSideLabel;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- buy an ability ----------

        private static void BuildAbilityPurchase(RectTransform root, PrototypeActionAbilityPurchasePopupView view)
        {
            Button overlay = DismissOverlay(root);
            Vector2 centre = new Vector2(0.5f, 0.5f);
            RectTransform panel = ColumnCard(root, "PurchasePanel", centre, centre, 1080f);
            CardHeader(panel, "BUY ABILITY", 21, 58f);
            Button close = HeaderCloseButton(panel);
            RectTransform body = Child("Body", panel);
            Row(body.gameObject, 24, 24, 22, 24, 26f, TextAnchor.UpperLeft);

            RectTransform list = Child("List", body);
            Column(list.gameObject, 0, 0, 0, 0, 8f);
            Size(list.gameObject, 340f, -1f, 340f);
            Label(Child("ListTitle", list), titleFont, 15, Muted, TextAnchor.MiddleLeft, "ABILITIES");
            ScrollList(list, "CatalogScroll", 470f, out ScrollRect catalogScroll, out RectTransform catalogRows);

            RectTransform detail = Child("Detail", body);
            Column(detail.gameObject, 0, 0, 0, 0, 14f);
            Size(detail.gameObject, 640f, -1f, 640f);
            Text cardName = Label(Child("CardName", detail), titleFont, 24, Ink, TextAnchor.MiddleLeft, "Card Name");
            RectTransform info = Child("Info", detail);
            Row(info.gameObject, 0, 0, 0, 0, 22f, TextAnchor.UpperLeft);
            RectTransform frame = Child("ArtworkFrame", info);
            Img(frame, "TileOutlined", Cream).raycastTarget = false;
            Size(frame.gameObject, 230f, 320f, 230f);
            RawImage artwork = Rect("Artwork", frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-24f, -24f)).gameObject.AddComponent<RawImage>();
            artwork.raycastTarget = false;
            RectTransform words = Child("Words", info);
            Column(words.gameObject, 0, 0, 0, 0, 12f);
            Size(words.gameObject, 388f, -1f, 388f);
            Text description = Label(Child("Description", words), bodyFont, 17, Ink, TextAnchor.UpperLeft, "Description");
            Text inputCost = Label(Child("InputCost", words), boldFont, 19, Ink, TextAnchor.UpperLeft, "Cost");
            Text affordability = Label(Child("Affordability", words), bodyFont, 16, Muted, TextAnchor.UpperLeft, "Can afford");
            Text status = Label(Child("Status", detail), boldFont, 16, Orange, TextAnchor.UpperLeft, string.Empty);
            RectTransform buttons = ButtonRow(detail);
            Button buy = GoButton(buttons, "BuyButton", "Buy", 200f, out Text buyLabel);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("catalogRowsRoot").objectReferenceValue = catalogRows;
            so.FindProperty("catalogScrollRect").objectReferenceValue = catalogScroll;
            so.FindProperty("cardNameLabel").objectReferenceValue = cardName;
            so.FindProperty("artworkImage").objectReferenceValue = artwork;
            so.FindProperty("descriptionLabel").objectReferenceValue = description;
            so.FindProperty("inputCostLabel").objectReferenceValue = inputCost;
            so.FindProperty("affordabilityLabel").objectReferenceValue = affordability;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("buyButton").objectReferenceValue = buy;
            so.FindProperty("buyButtonLabel").objectReferenceValue = buyLabel;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- choose cards / reveal a card ----------

        private static void BuildFocusedSelection(RectTransform root, PrototypeFocusedCardSelectionView view)
        {
            Button overlay = DismissOverlay(root);
            Vector2 centre = new Vector2(0.5f, 0.5f);
            // Fixed size: the view places the card area at 4-96 % across and 12-70 % up this panel.
            RectTransform panel = Rect("FocusedPanel", root, centre, centre, centre, Vector2.zero, new Vector2(1180f, 700f));
            Backdrop(panel, ShadowOffset);
            RectTransform header = TopBand("Header", panel, 5f, 58f);
            Img(header, "TopRounded", Orange).raycastTarget = false;
            Text title = Label(Rect("Title", header, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-48f, 0f)), titleFont, 21, Cream, TextAnchor.MiddleLeft, "CHOOSE");
            Img(TopBand("HeaderRule", panel, 63f, 5f), "Solid", Ink).raycastTarget = false;
            Text instruction = Label(Rect("Instruction", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -86f), new Vector2(-64f, 96f)), bodyFont, 19, Ink, TextAnchor.UpperLeft, "Instruction");

            RectTransform viewport = Rect("CardArea", panel, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.70f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform options = Rect("Options", viewport, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(620f, 260f));
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = options;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            Text status = Label(Rect("Status", panel, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(0f, 0f),
                new Vector2(32f, 22f), new Vector2(-32f, 52f)), boldFont, 17, Muted, TextAnchor.MiddleLeft, string.Empty);
            RectTransform buttons = Rect("Buttons", panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-32f, 22f), new Vector2(420f, 52f));
            Row(buttons.gameObject, 0, 0, 0, 0, 12f, TextAnchor.MiddleRight);
            Button cancel = PlainButton(buttons, "CancelButton", "Cancel", 150f, out Text cancelLabel);
            Button confirm = GoButton(buttons, "ConfirmButton", "Confirm", 200f, out Text confirmLabel);

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

        private static void BuildFocusedOption(RectTransform root, PrototypeFocusedCardOptionView view)
        {
            // The root is the card: the view sizes, fans and lifts it.
            root.sizeDelta = new Vector2(148f, 214f);
            Image surface = Img(root, "TileOutlined", Cream);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.None;
            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 148f;
            layout.preferredHeight = 214f;
            RawImage artwork = Rect("Artwork", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(-24f, 140f)).gameObject.AddComponent<RawImage>();
            artwork.raycastTarget = false;
            Text label = Label(Rect("Label", root, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(-20f, 52f)), boldFont, 15, Ink, TextAnchor.MiddleCenter, "Card");

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("cardTransform").objectReferenceValue = root;
            so.FindProperty("surface").objectReferenceValue = surface;
            so.FindProperty("artwork").objectReferenceValue = artwork;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("layoutElement").objectReferenceValue = layout;
            so.FindProperty("eligibleSurface").colorValue = Cream;
            so.FindProperty("selectedSurface").colorValue = Mustard;
            so.FindProperty("ineligibleSurface").colorValue = new Color(TabIdle.r, TabIdle.g, TabIdle.b, 0.92f);
            so.FindProperty("eligibleArtwork").colorValue = Color.white;
            so.FindProperty("ineligibleArtwork").colorValue = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            so.FindProperty("eligibleText").colorValue = Ink;
            so.FindProperty("ineligibleText").colorValue = Muted;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
