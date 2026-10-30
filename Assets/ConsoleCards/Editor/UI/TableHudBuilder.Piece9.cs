using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Board 9 "Piece menu + card inspect" (owner, 2026-10-30: "piece inspect and card inspect is different"), measured
    /// from the board (1280 x 720, x1.5), in Final UI/Notices:
    /// <list type="bullet">
    /// <item>ActionRow9: a menu row (#E8ECED words, the key on the right in a small outlined chip; under the pointer a
    /// #1C3C3D fill with a cyan bar on the left; Delete red).</item>
    /// <item>TabletopPopup9: the piece's menu (270 wide, #222527, the piece's name and line over a rule, then the actions
    /// in PLAY · ARRANGE · TABLE), Draw how many? (orange edge, − n +, 1 3 5 7, CANCEL | DRAW, "Into your hand · Enter
    /// draws") and Merge into.</item>
    /// <item>CardInspect9: Inspect at the right edge (340 wide), the table still in view: the card shown whole with its
    /// FRONT / BACK tag, the card's facts as rows, the side switch or the card's action, ×.</item>
    /// </list>
    /// The catalog points the game at them. Made once; after that they are kept (edit them freely).
    /// </summary>
    public static partial class TableHudBuilder
    {
        public const string ActionRow9Path = Folder + "ActionRow9.prefab";
        public const string Popup9Path = Folder + "TabletopPopup9.prefab";
        public const string Inspect9Path = Folder + "CardInspect9.prefab";
        private const float P = 1.5f; // board pixels to the canvas

        private static readonly Color MenuFill = Hex("222527");
        private static readonly Color MenuEdge = Hex("3c4143");
        private static readonly Color ChipEdge = Hex("424547");
        private static readonly Color HoverTeal = Hex("1c3c3d");
        private static readonly Color GroupWords = Hex("6b7477");
        private static readonly Color ButtonWell = Hex("1c1f1f");

        private static void BuildPieceMenus()
        {
            frame = frame != null ? frame : HudSprite("HudFrame", new Vector4(16f, 16f, 16f, 16f));
            round = round != null ? round : HudSprite("HudRound", new Vector4(16f, 16f, 16f, 16f));
            if (round == null) round = rounded;
            Save<PopupActionRowView>(ActionRow9Path, "ActionRow9", BuildActionRow9);
            Save<TabletopPopupView>(Popup9Path, "TabletopPopup9", BuildPopup9);
            Save<CardInspectView>(Inspect9Path, "CardInspect9", BuildInspect9);
            AssignCatalogEntry(UiPrefabIds.PopupActionRow, AssetDatabase.LoadAssetAtPath<PopupActionRowView>(ActionRow9Path),
                UiLayer.Popup, UiRetention.Pooled);
            AssignCatalogEntry(UiPrefabIds.TabletopPopup, AssetDatabase.LoadAssetAtPath<TabletopPopupView>(Popup9Path),
                UiLayer.Popup, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(UiPrefabIds.CardInspect, AssetDatabase.LoadAssetAtPath<CardInspectView>(Inspect9Path),
                UiLayer.Modal, UiRetention.CachedSingleInstance);
        }

        /// <summary>Turn Off: the menus before board 9.</summary>
        private static void RestorePieceMenus()
        {
            PopupActionRowView row = AssetDatabase.LoadAssetAtPath<PopupActionRowView>(GameplayUiBuilder.ActionRowPath);
            if (row != null) AssignCatalogEntry(UiPrefabIds.PopupActionRow, row, UiLayer.Popup, UiRetention.Pooled);
            TabletopPopupView popup = AssetDatabase.LoadAssetAtPath<TabletopPopupView>(GameplayUiBuilder.TabletopPopupPath);
            if (popup != null) AssignCatalogEntry(UiPrefabIds.TabletopPopup, popup, UiLayer.Popup, UiRetention.CachedSingleInstance);
            CardInspectView inspect = AssetDatabase.LoadAssetAtPath<CardInspectView>(GameplayUiBuilder.CardInspectPath);
            if (inspect != null) AssignCatalogEntry(UiPrefabIds.CardInspect, inspect, UiLayer.Modal, UiRetention.CachedSingleInstance);
        }

        // ---------- a menu row ----------

        private static void BuildActionRow9(RectTransform root, PopupActionRowView view)
        {
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(258f * P, 36f * P);
            Image fill = root.gameObject.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.color = HoverTeal;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = Color.white;
            colours.pressedColor = Color.white;
            colours.selectedColor = new Color(1f, 1f, 1f, 0f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0f);
            colours.fadeDuration = 0.05f;
            button.colors = colours;
            Size(root.gameObject, -1f, 36f * P);

            RectTransform bar = Rect("Bar", root, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(3f * P, 0f));
            Shape(bar, Cyan, null);
            bar.gameObject.SetActive(false);

            Text label = Label(Rect("Label", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f),
                new Vector2(-12f * P * 2f - 48f, 0f)), medium, 21, Words, TextAnchor.MiddleLeft, "Draw…");
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            RectTransform chip = Rect("Key", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f * P, 0f), new Vector2(46f, 26f));
            Image chipFill = Shape(chip, new Color(0f, 0f, 0f, 0f), rounded);
            Outline edge = chip.gameObject.AddComponent<Outline>();
            edge.effectColor = ChipEdge;
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            edge.useGraphicAlpha = false;
            Outline edge2 = chip.gameObject.AddComponent<Outline>();
            edge2.effectColor = ChipEdge;
            edge2.effectDistance = new Vector2(-1.5f, 1.5f);
            edge2.useGraphicAlpha = false;
            chipFill.raycastTarget = false;
            Row(chip.gameObject, 9, 9, 0, 0, 0f, TextAnchor.MiddleCenter);
            Fit(chip.gameObject, true, false);
            Text key = FitLabel(Child("Label", chip), bold, 16, Muted, "D");
            key.alignment = TextAnchor.MiddleCenter;
            chip.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("keyChip").objectReferenceValue = chip.gameObject;
            so.FindProperty("keyLabel").objectReferenceValue = key;
            so.FindProperty("hoverBar").objectReferenceValue = bar.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- the piece's menu, Draw how many, Merge into ----------

        private static void BuildPopup9(RectTransform root, TabletopPopupView view)
        {
            Image catcher = root.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);

            // The menu.
            RectTransform context = MenuCard(root, "ContextMenuPanel", 270f, 6f, MenuEdge, 1.5f);
            RectTransform head = Child("Head", context);
            Column(head.gameObject, 18, 18, 15, 12, 2f);
            Text title = Label(Child("Title", head), bold, 22, Words, TextAnchor.UpperLeft, "DECK");
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text body = Label(Child("Body", head), medium, 18, Muted, TextAnchor.UpperLeft, "32 cards · face down");
            RectTransform rule = Child("Rule", context);
            Shape(rule, MenuEdge, null);
            Size(rule.gameObject, -1f, 1.5f);
            RectTransform actions = Child("Actions", context);
            VerticalLayoutGroup actionsColumn = Column(actions.gameObject, 0, 0, 3, 0, 0f);
            actionsColumn.childForceExpandWidth = true;
            // A group's name (PLAY, ARRANGE, TABLE): #6B7477, 18 in like the rows' words; the view copies it.
            RectTransform groupRow = Child("GroupTemplate", context);
            Row(groupRow.gameObject, 18, 18, 12, 3, 0f, TextAnchor.LowerLeft);
            Size(groupRow.gameObject, -1f, 33f);
            FitLabel(Child("Label", groupRow), bold, 15, GroupWords, "PLAY");
            groupRow.gameObject.SetActive(false);
            context.gameObject.SetActive(false);

            // Draw how many?
            RectTransform draw = MenuCard(root, "DrawCountPanel", 250f, 16f, Orange, 3f);
            Text drawTitle = Label(Child("Title", draw), bold, 22, Words, TextAnchor.MiddleLeft, "Draw how many?");
            Size(drawTitle.gameObject, -1f, 30f);
            RectTransform stepper = Child("Stepper", draw);
            Row(stepper.gameObject, 0, 0, 24, 24, 21f, TextAnchor.MiddleCenter);
            Button decrement = SquareButton(stepper, "Decrement", "−", 60f, 60f, ButtonWell, MenuEdge, 22);
            Text count = Label(Child("Count", stepper), bold, 45, Words, TextAnchor.MiddleCenter, "3");
            Size(count.gameObject, 60f, 60f, 60f);
            Button increment = SquareButton(stepper, "Increment", "+", 60f, 60f, ButtonWell, MenuEdge, 22);
            RectTransform quick = Child("Quick", draw);
            Row(quick.gameObject, 0, 0, 0, 21, 9f, TextAnchor.MiddleCenter);
            Button[] quickButtons = new Button[4];
            string[] quickLabels = { "1", "3", "5", "7" };
            for (int i = 0; i < quickButtons.Length; i++)
            {
                quickButtons[i] = SquareButton(quick, "Quick" + quickLabels[i], quickLabels[i], 45f, 45f, ButtonWell, MenuEdge, 19);
            }

            RectTransform buttons = Child("Buttons", draw);
            HorizontalLayoutGroup buttonRow = Row(buttons.gameObject, 0, 0, 0, 0, 12f, TextAnchor.MiddleCenter);
            buttonRow.childForceExpandWidth = true;
            Button cancel = SquareButton(buttons, "CancelButton", "CANCEL", -1f, 60f, ButtonWell, MenuEdge, 19);
            Button confirm = SquareButton(buttons, "DrawButton", "DRAW", -1f, 60f, Teal, Cyan, 19);
            Text available = Label(Child("Available", draw), medium, 16, Muted, TextAnchor.MiddleCenter,
                "Into your hand · Enter draws");
            Size(available.gameObject, -1f, 30f);
            draw.gameObject.SetActive(false);

            // Merge into.
            RectTransform merge = MenuCard(root, "MergeDestinationPanel", 270f, 6f, MenuEdge, 1.5f);
            Text mergeTitle = Label(Child("Title", merge), bold, 22, Words, TextAnchor.MiddleLeft, "   MERGE INTO");
            Size(mergeTitle.gameObject, -1f, 45f);
            RectTransform viewport = Child("DestinationsViewport", merge);
            Size(viewport.gameObject, -1f, 330f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportCatcher = viewport.gameObject.AddComponent<Image>();
            viewportCatcher.color = new Color(0f, 0f, 0f, 0f);
            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            Column(content.gameObject, 0, 0, 0, 0, 0f).childForceExpandWidth = true;
            Fit(content.gameObject, false, true);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            Button back = SquareButton(merge, "BackButton", "BACK", -1f, 54f, ButtonWell, MenuEdge, 18);
            merge.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("popupBounds").objectReferenceValue = root;
            so.FindProperty("contextPanel").objectReferenceValue = context;
            so.FindProperty("contextTitleLabel").objectReferenceValue = title;
            so.FindProperty("contextBodyLabel").objectReferenceValue = body;
            so.FindProperty("contextActionsRoot").objectReferenceValue = actions;
            so.FindProperty("drawCountPanel").objectReferenceValue = draw;
            so.FindProperty("drawCountLabel").objectReferenceValue = count;
            so.FindProperty("drawAvailableLabel").objectReferenceValue = available;
            so.FindProperty("drawDecrementButton").objectReferenceValue = decrement;
            so.FindProperty("drawIncrementButton").objectReferenceValue = increment;
            so.FindProperty("drawConfirmButton").objectReferenceValue = confirm;
            so.FindProperty("drawCancelButton").objectReferenceValue = cancel;
            so.FindProperty("mergePanel").objectReferenceValue = merge;
            so.FindProperty("mergeViewport").objectReferenceValue = viewport;
            so.FindProperty("mergeActionsRoot").objectReferenceValue = content;
            so.FindProperty("mergeScrollRect").objectReferenceValue = scroll;
            so.FindProperty("mergeBackButton").objectReferenceValue = back;
            so.FindProperty("groupTemplate").objectReferenceValue = groupRow.gameObject;
            SerializedProperty quickProperty = so.FindProperty("drawQuickButtons");
            quickProperty.arraySize = quickButtons.Length;
            for (int i = 0; i < quickButtons.Length; i++) quickProperty.GetArrayElementAtIndex(i).objectReferenceValue = quickButtons[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // A board menu card: #222527, rounded, its edge, a soft shadow, a column of its parts, as tall as they need.
        private static RectTransform MenuCard(RectTransform root, string name, float width, float padding, Color edge, float edgeWidth)
        {
            RectTransform card = Rect(name, root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero,
                new Vector2(width * P, 0f));
            Image fill = card.gameObject.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.color = MenuFill;
            fill.raycastTarget = true;
            Swallow(card);
            Outline a = card.gameObject.AddComponent<Outline>();
            a.effectColor = edge;
            a.effectDistance = new Vector2(edgeWidth, -edgeWidth);
            a.useGraphicAlpha = false;
            Outline b = card.gameObject.AddComponent<Outline>();
            b.effectColor = edge;
            b.effectDistance = new Vector2(-edgeWidth, edgeWidth);
            b.useGraphicAlpha = false;
            Shadow shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -18f);
            int pad = Mathf.RoundToInt(padding * P);
            VerticalLayoutGroup column = Column(card.gameObject, pad, pad, pad, pad, 0f);
            column.childForceExpandWidth = true;
            Fit(card.gameObject, false, true);
            return card;
        }

        private static Button SquareButton(Transform parent, string name, string words, float width, float height, Color fill,
            Color edge, int size)
        {
            RectTransform rect = Child(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = fill;
            Outline a = rect.gameObject.AddComponent<Outline>();
            a.effectColor = edge;
            a.effectDistance = new Vector2(1.5f, -1.5f);
            a.useGraphicAlpha = false;
            Outline b = rect.gameObject.AddComponent<Outline>();
            b.effectColor = edge;
            b.effectDistance = new Vector2(-1.5f, 1.5f);
            b.useGraphicAlpha = false;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            SetDisabledTint(button);
            LayoutElement layout = Size(rect.gameObject, width, height, width >= 0f ? width : -1f);
            if (width < 0f) layout.flexibleWidth = 1f;
            Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), bold, size,
                Words, TextAnchor.MiddleCenter, words);
            return button;
        }

        // ---------- inspect ----------

        private static void BuildInspect9(RectTransform root, CardInspectView view)
        {
            // A clear catcher behind the panel: a click on the table beside it closes it.
            RectTransform overlayRect = Rect("DismissOverlay", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Image catcher = overlayRect.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            Button overlay = overlayRect.gameObject.AddComponent<Button>();
            overlay.targetGraphic = catcher;
            overlay.transition = Selectable.Transition.None;

            RectTransform panel = Rect("CardInspectPanel", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-40f * P, 0f), new Vector2(340f * P, 640f * P));
            Image fill = panel.gameObject.AddComponent<Image>();
            fill.sprite = rounded;
            fill.type = Image.Type.Sliced;
            fill.color = MenuFill;
            fill.raycastTarget = true;
            Swallow(panel);
            Outline a = panel.gameObject.AddComponent<Outline>();
            a.effectColor = MenuEdge;
            a.effectDistance = new Vector2(1.5f, -1.5f);
            Outline b = panel.gameObject.AddComponent<Outline>();
            b.effectColor = MenuEdge;
            b.effectDistance = new Vector2(-1.5f, 1.5f);

            Text kicker = Label(TopLeft("Kicker", panel, 18f, 18f, 200f, 30f), bold, 16, Muted, TextAnchor.MiddleLeft, "INSPECT");
            Text identity = Label(TopLeft("CardName", panel, 120f, 18f, 140f, 30f), bold, 16, Muted, TextAnchor.MiddleLeft, "Card");
            identity.gameObject.SetActive(false);
            Button close = SquareButtonAt(panel, "CloseButton", "×", 292f - 2f, 18f, 32f, 30f, 22);

            // The card, whole.
            RectTransform cardRect = TopLeft("CardSurface", panel, 55f, 62f, 230f, 322f);
            Image surface = cardRect.gameObject.AddComponent<Image>();
            surface.sprite = round;
            surface.type = Image.Type.Sliced;
            surface.color = Hex("2b6b8f");
            surface.raycastTarget = false;
            Shadow cardShadow = cardRect.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            cardShadow.effectDistance = new Vector2(0f, -15f);
            Image artwork = Rect("Artwork", cardRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero)
                .gameObject.AddComponent<Image>();
            artwork.preserveAspect = true;
            artwork.raycastTarget = false;
            Text sideTitle = Label(Rect("SideTitle", cardRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-36f, -36f)), bold, 24, Words, TextAnchor.MiddleCenter, "[FRONT PNG]");
            Text sideBody = Label(Rect("SideBody", cardRect, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 18f), new Vector2(-36f, 60f)), medium, 15, Words, TextAnchor.LowerCenter, string.Empty);
            RectTransform icons = Rect("IconsRoot", cardRect, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 30f));
            icons.gameObject.SetActive(false);
            RectTransform tag = Rect("Tag", cardRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-12f, 12f), new Vector2(0f, 30f));
            Shape(tag, new Color(0.063f, 0.075f, 0.078f, 0.8f), rounded);
            Row(tag.gameObject, 9, 9, 0, 0, 0f, TextAnchor.MiddleCenter);
            Fit(tag.gameObject, true, false);
            Text tagLabel = FitLabel(Child("Label", tag), bold, 15, Words, "FRONT");

            Text faceState = Label(TopLeft("FaceState", panel, 18f, 394f, 304f, 34f), medium, 16, Muted, TextAnchor.UpperCenter,
                "Face up on the table.");

            // The facts and the note.
            RectTransform details = TopLeft("Details", panel, 18f, 432f, 304f, 128f);
            details.gameObject.AddComponent<RectMask2D>();
            VerticalLayoutGroup detailsColumn = Column(details.gameObject, 0, 0, 0, 0, 12f);
            detailsColumn.childForceExpandWidth = true;
            RectTransform facts = Child("Facts", details);
            Column(facts.gameObject, 0, 0, 0, 0, 9f).childForceExpandWidth = true;
            RectTransform fact = Child("FactTemplate", facts);
            Size(fact.gameObject, -1f, 30f);
            Label(Rect("Key", fact, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), medium, 19,
                Muted, TextAnchor.UpperLeft, "Card");
            Text value = Label(Rect("Value", fact, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(45f, 0f),
                new Vector2(-90f, 0f)), bold, 19, Words, TextAnchor.UpperRight, "Heavy Kick");
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform factRule = Rect("Rule", fact, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(0f, 1.5f));
            Shape(factRule, MenuEdge, null);
            fact.gameObject.SetActive(false);
            Text noteLabel = Label(Child("Note", details), medium, 16, Hex("6b7477"), TextAnchor.UpperLeft, string.Empty);

            // The side switch or the card's own action.
            RectTransform otherRect = TopLeft("ViewOtherSideButton", panel, 18f, 572f, 304f, 40f);
            Image otherFill = otherRect.gameObject.AddComponent<Image>();
            otherFill.sprite = rounded;
            otherFill.type = Image.Type.Sliced;
            otherFill.color = ButtonWell;
            Outline oa = otherRect.gameObject.AddComponent<Outline>();
            oa.effectColor = MenuEdge;
            oa.effectDistance = new Vector2(1.5f, -1.5f);
            Outline ob = otherRect.gameObject.AddComponent<Outline>();
            ob.effectColor = MenuEdge;
            ob.effectDistance = new Vector2(-1.5f, 1.5f);
            Button other = otherRect.gameObject.AddComponent<Button>();
            other.targetGraphic = otherFill;
            SetDisabledTint(other);
            Text otherLabel = Label(Rect("Label", otherRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero), bold, 19, Words, TextAnchor.MiddleCenter, "FLIP");

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
            so.FindProperty("viewOtherSideButton").objectReferenceValue = other;
            so.FindProperty("viewOtherSideButtonLabel").objectReferenceValue = otherLabel;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("factsRoot").objectReferenceValue = facts;
            so.FindProperty("factTemplate").objectReferenceValue = fact.gameObject;
            so.FindProperty("noteLabel").objectReferenceValue = noteLabel;
            so.FindProperty("sideTag").objectReferenceValue = tagLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            kicker.text = "INSPECT";
        }

        // A click on a panel stops there (it does not reach the close-on-click behind it).
        private static void Swallow(RectTransform panel)
        {
            Button button = panel.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        // A rect at board pixels from the parent's top left.
        private static RectTransform TopLeft(string name, RectTransform parent, float x, float y, float width, float height) =>
            Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x * P, -y * P),
                new Vector2(width * P, height * P));

        private static Button SquareButtonAt(RectTransform parent, string name, string words, float x, float y, float width,
            float height, int size)
        {
            RectTransform rect = TopLeft(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = ButtonWell;
            Outline a = rect.gameObject.AddComponent<Outline>();
            a.effectColor = MenuEdge;
            a.effectDistance = new Vector2(1.5f, -1.5f);
            Outline b = rect.gameObject.AddComponent<Outline>();
            b.effectColor = MenuEdge;
            b.effectDistance = new Vector2(-1.5f, 1.5f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), bold, size,
                Words, TextAnchor.MiddleCenter, words);
            return button;
        }
    }
}
