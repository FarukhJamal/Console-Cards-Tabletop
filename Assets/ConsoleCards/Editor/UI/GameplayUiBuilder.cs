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
    /// merge panels, the quantity popup, card inspection, ability purchase and focused card choice (UI-1c); the rules
    /// card and its lines (doc 23, R1). Each prefab carries the existing view script with the same references, and the build points the
    /// Real UI prefab catalog's entries at the new prefabs. Nothing is built at runtime.
    /// </summary>
    public static class GameplayUiBuilder
    {
        public const string Folder = FinalUiFolder;
        public const string GuidePath = Folder + "InteractionGuide.prefab";
        public const string TrapFloorHudPath = Folder + "TrapFloorHud.prefab";
        public const string ActionRowPath = Folder + "ActionRow.prefab";
        public const string TabletopPopupPath = Folder + "TabletopPopup.prefab";
        public const string QuantityPopupPath = Folder + "QuantityPopup.prefab";
        public const string CardInspectPath = Folder + "CardInspect.prefab";
        public const string AbilityPurchasePath = Folder + "AbilityPurchase.prefab";
        public const string FocusedSelectionPath = Folder + "FocusedCardSelection.prefab";
        public const string FocusedOptionPath = Folder + "FocusedCardOption.prefab";
        public const string RulesCardPath = Folder + "RulesCard.prefab";
        public const string RulesHeadingPath = Folder + "RulesHeading.prefab";
        public const string RulesLinePath = Folder + "RulesLine.prefab";
        public const string FeedbackBannerPath = Folder + "FeedbackBanner.prefab";
        public const string FeedbackMessagesPath = Folder + "FeedbackMessages.prefab";
        public const string HotSeatHandoverPath = Folder + "HotSeatHandover.prefab";
        public const string ActivityLogPath = Folder + "ActivityLog.prefab";
        public const string ContentArtPanelPath = Folder + "CardArtPanel.prefab";
        public const string ContentArtRowPath = Folder + "CardArtRow.prefab";
        public const string RuleProposalPanelPath = Folder + "RuleProposalPanel.prefab";
        public const string HintStripPath = Folder + "HintStrip.prefab";
        public const string ProfilePanelPath = Folder + "ProfilePanel.prefab";
        public const string ProfileRowPath = Folder + "ProfileRow.prefab";
        private const string FeedbackAudioPath = "Assets/ConsoleCards/Content/Audio/Feedback/";
        private const string SessionIconPath = "Assets/ConsoleCards/Content/UI/Session/Icons/";

        // Layout, in canvas units at the 1920 x 1080 reference.
        private const float Margin = 29f;
        private const float ShadowOffset = 7f;
        private const float StatusCardWidth = 440f;
        private const float TurnStripWidth = 800f;
        private const float TurnStripCentreOffset = 55f;
        private const float GuideWidth = 580f;
        private const float StripHeight = 60f;
        private const float RowHeight = 50f;
        private const float MenuWidth = 400f;
        // Rules card: left column below the session bar, + TOOLBOX and the Placing card (which ends at 226).
        private const float RulesCardTop = 250f;
        private const float RulesCardWidth = 440f;
        private const float RulesBodyHeight = 430f;

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

        [MenuItem("Console Cards/UI/Create Missing Gameplay UI Prefabs (Final UI)")]
        public static void Build()
        {
            ConfigureTexture(ToolboxIconPath + "IconClose.png", Vector4.zero);
            ConfigureTexture(SessionIconPath + "IconChevron.png", Vector4.zero);
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
            Save<RulesCardLineView>(RulesHeadingPath, "RulesHeading", BuildRulesHeading, stretch: false);
            Save<RulesCardLineView>(RulesLinePath, "RulesLine", BuildRulesLine, stretch: false);
            Save<RulesCardView>(RulesCardPath, "RulesCard", BuildRulesCard, stretch: true);
            Save<FeedbackBannerView>(FeedbackBannerPath, "FeedbackBanner", BuildFeedbackBanner, stretch: true);
            Save<FeedbackMessageStackView>(FeedbackMessagesPath, "FeedbackMessages", BuildFeedbackMessages, stretch: true);
            Save<HotSeatHandoverView>(HotSeatHandoverPath, "HotSeatHandover", BuildHotSeatHandover, stretch: true);
            Save<ActivityLogView>(ActivityLogPath, "ActivityLog", BuildActivityLog, stretch: true);
            Save<ContentArtRowView>(ContentArtRowPath, "CardArtRow", BuildContentArtRow, stretch: false);
            Save<ContentArtPanelView>(ContentArtPanelPath, "CardArtPanel", BuildContentArtPanel, stretch: true);
            Save<RuleProposalPanelView>(RuleProposalPanelPath, "RuleProposalPanel", BuildRuleProposalPanel, stretch: true);
            Save<HintStripView>(HintStripPath, "HintStrip", BuildHintStrip, stretch: true);
            Save<ProfileRowView>(ProfileRowPath, "ProfileRow", BuildProfileRow, stretch: false);
            Save<ProfilePanelView>(ProfilePanelPath, "ProfilePanel", BuildProfilePanel, stretch: true);
            AddMissingRuleControls();
            AddMissingShopLabels();

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
            AssignCatalogEntry(PrototypeUiPrefabIds.RulesCard,
                AssetDatabase.LoadAssetAtPath<RulesCardView>(RulesCardPath), UiLayer.Hud, UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.RulesHeading,
                AssetDatabase.LoadAssetAtPath<RulesCardLineView>(RulesHeadingPath), UiLayer.Hud, UiRetention.Pooled);
            AssignCatalogEntry(PrototypeUiPrefabIds.RulesLine,
                AssetDatabase.LoadAssetAtPath<RulesCardLineView>(RulesLinePath), UiLayer.Hud, UiRetention.Pooled);
            AssignCatalogEntry(PrototypeUiPrefabIds.FeedbackBanner,
                AssetDatabase.LoadAssetAtPath<FeedbackBannerView>(FeedbackBannerPath), UiLayer.Notification,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.FeedbackMessages,
                AssetDatabase.LoadAssetAtPath<FeedbackMessageStackView>(FeedbackMessagesPath), UiLayer.Notification,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.HotSeatHandover,
                AssetDatabase.LoadAssetAtPath<HotSeatHandoverView>(HotSeatHandoverPath), UiLayer.Modal,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.ActivityLog,
                AssetDatabase.LoadAssetAtPath<ActivityLogView>(ActivityLogPath), UiLayer.Hud,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.ContentArtPanel,
                AssetDatabase.LoadAssetAtPath<ContentArtPanelView>(ContentArtPanelPath), UiLayer.Modal,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.ContentArtRow,
                AssetDatabase.LoadAssetAtPath<ContentArtRowView>(ContentArtRowPath), UiLayer.Modal,
                UiRetention.Pooled);
            AssignCatalogEntry(PrototypeUiPrefabIds.HintStrip,
                AssetDatabase.LoadAssetAtPath<HintStripView>(HintStripPath), UiLayer.Hud,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.RuleProposalPanel,
                AssetDatabase.LoadAssetAtPath<RuleProposalPanelView>(RuleProposalPanelPath), UiLayer.Modal,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.ProfilePanel,
                AssetDatabase.LoadAssetAtPath<ProfilePanelView>(ProfilePanelPath), UiLayer.Modal,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(PrototypeUiPrefabIds.ProfileRow,
                AssetDatabase.LoadAssetAtPath<ProfileRowView>(ProfileRowPath), UiLayer.Modal,
                UiRetention.Pooled);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gameplay UI] Created any missing Final UI gameplay prefabs and pointed the UI prefab catalog at them.");
        }

        private static void Save<T>(string path, string name, System.Action<RectTransform, T> build, bool stretch)
            where T : ReusableUiView
        {
            // Final UI prefabs belong to the designer: an existing one is never rebuilt or overwritten.
            if (File.Exists(path))
            {
                Debug.Log($"[Gameplay UI] Kept {path} (already exists; edit it in the Final UI folder).");
                return;
            }

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

        // ---------- player feedback (doc 25, F1): announcement banner and message stack ----------

        private static void BuildFeedbackBanner(RectTransform root, FeedbackBannerView view)
        {
            // Top centre, below the turn strip. Only the banner itself takes clicks (click to skip).
            RectTransform panel = Rect("Banner", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -122f), new Vector2(760f, 112f));
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            Image fill = Backdrop(panel, ShadowOffset);
            Button skip = panel.gameObject.AddComponent<Button>();
            skip.targetGraphic = fill;
            skip.transition = Selectable.Transition.None;
            RectTransform accentRect = Rect("Accent", panel, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(12f, 0f), new Vector2(18f, -26f));
            Image accent = Img(accentRect, "Solid", Orange);
            accent.raycastTarget = false;
            RectTransform titleRect = Rect("Title", panel, new Vector2(0f, 0.42f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            titleRect.offsetMin = new Vector2(48f, 0f);
            titleRect.offsetMax = new Vector2(-24f, -10f);
            Text title = Label(titleRect, titleFont, 30, Ink, TextAnchor.LowerLeft, "PLAYER 1 (BLUE) — YOUR TURN");
            RectTransform subtitleRect = Rect("Subtitle", panel, new Vector2(0f, 0f), new Vector2(1f, 0.42f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            subtitleRect.offsetMin = new Vector2(50f, 12f);
            subtitleRect.offsetMax = new Vector2(-24f, 0f);
            Text subtitle = Label(subtitleRect, bodyFont, 19, Muted, TextAnchor.UpperLeft, "ROUND 1");
            AudioSource audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("accent").objectReferenceValue = accent;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("subtitleLabel").objectReferenceValue = subtitle;
            so.FindProperty("skipButton").objectReferenceValue = skip;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("turnStartClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FeedbackAudioPath + "TurnPing.wav");
            so.FindProperty("trapClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FeedbackAudioPath + "Trap.wav");
            so.FindProperty("eliminatedClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FeedbackAudioPath + "Eliminated.wav");
            so.FindProperty("rewardClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FeedbackAudioPath + "Reward.wav");
            so.FindProperty("collapseClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FeedbackAudioPath + "Collapse.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Hot-seat handover (doc 25, H2): an opaque cover hides the table and every hand; a centred card names the
        // next player in their colour, lists what happened since their last turn, and shows their hand on confirm.
        private static void BuildHotSeatHandover(RectTransform root, HotSeatHandoverView view)
        {
            RectTransform coverRect = Rect("Cover", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Image cover = Img(coverRect, "Solid", new Color(0.05f, 0.06f, 0.06f, 0.97f));
            Vector2 centre = new Vector2(0.5f, 0.5f);
            RectTransform panel = ColumnCard(root, "HandoverPanel", centre, centre, 640f);
            BlockClicks(panel);
            Text title = CardHeader(panel, "PASS THE SCREEN", 22, 58f);
            RectTransform plate = Child("PlayerPlate", panel);
            Image accent = Img(plate, "Solid", Orange);
            accent.raycastTarget = false;
            Size(plate.gameObject, -1f, 18f);
            RectTransform body = Body(panel, 26, 14f);
            RectTransform playerRect = Child("Player", body);
            Text player = Label(playerRect, titleFont, 34, Orange, TextAnchor.MiddleCenter, "PLAYER 2 (RED)");
            Size(playerRect.gameObject, -1f, 54f);
            Text summary = Label(Child("Summary", body), bodyFont, 18, Ink, TextAnchor.UpperLeft,
                "Since your last turn:\n• Drew 2 to Player 2 (RED)'s hand");
            Text note = Label(Child("Note", body), bodyFont, 15, Muted, TextAnchor.MiddleCenter,
                "Only this player should look. Press Enter or the button when ready.");
            note.raycastTarget = false;
            RectTransform buttons = ButtonRow(body, TextAnchor.MiddleCenter);
            Button confirm = GoButton(buttons, "ConfirmButton", "I'm Player 2 — show my hand", 380f, out Text confirmLabel);
            AudioSource audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("cover").objectReferenceValue = cover;
            so.FindProperty("accentPlate").objectReferenceValue = accent;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("playerLabel").objectReferenceValue = player;
            so.FindProperty("summaryLabel").objectReferenceValue = summary;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.FindProperty("confirmLabel").objectReferenceValue = confirmLabel;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("handoverClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FeedbackAudioPath + "TurnPing.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // "What happened" log (doc 25, F4): a tab at the bottom right, above the controls strip, that opens a
        // scrolling list of the latest events (newest first). Only the tab and the open panel take clicks.
        private static void BuildActivityLog(RectTransform root, ActivityLogView view)
        {
            Vector2 bottomRight = new Vector2(1f, 0f);
            RectTransform tab = Rect("Tab", root, bottomRight, bottomRight, bottomRight,
                new Vector2(-Margin, 104f), new Vector2(240f, 46f));
            Image tabFill = Img(tab, "ButtonOutlined", Ink);
            Button toggle = tab.gameObject.AddComponent<Button>();
            toggle.targetGraphic = tabFill;
            Text tabLabel = Label(Rect("Label", tab, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-16f, 0f)), boldFont, 16, Cream, TextAnchor.MiddleCenter, "WHAT HAPPENED  (L)");

            RectTransform panel = Rect("Panel", root, bottomRight, bottomRight, bottomRight,
                new Vector2(-Margin, 160f), new Vector2(460f, 380f));
            Backdrop(panel, ShadowOffset);
            BlockClicks(panel);
            RectTransform header = Rect("Header", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -5f), new Vector2(-10f, 50f));
            Img(header, "TopRounded", Orange).raycastTarget = false;
            Label(Rect("Title", header, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(6f, 0f),
                new Vector2(-30f, 0f)), titleFont, 19, Cream, TextAnchor.MiddleLeft, "WHAT HAPPENED");
            RectTransform viewport = Rect("Viewport", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            viewport.offsetMin = new Vector2(14f, 14f);
            viewport.offsetMax = new Vector2(-14f, -62f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Img(viewport, "Solid", new Color(0f, 0f, 0f, 0f));
            RectTransform content = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            Column(content.gameObject, 2, 2, 2, 2, 0f);
            Fit(content.gameObject, false, true);
            Text body = Label(Child("Lines", content), bodyFont, 16, Ink, TextAnchor.UpperLeft, "Nothing yet.");
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            panel.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("toggleButton").objectReferenceValue = toggle;
            so.FindProperty("toggleLabel").objectReferenceValue = tabLabel;
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("scroll").objectReferenceValue = scroll;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Card Art panel (doc 24, A3/A4) ----------

        // Centred modal: the art pack chooser, the packs folder, and every replaceable picture with its source.
        private static void BuildContentArtPanel(RectTransform root, ContentArtPanelView view)
        {
            Button overlay = DismissOverlay(root);
            Vector2 centre = new Vector2(0.5f, 0.5f);
            RectTransform panel = ColumnCard(root, "CardArtPanel", centre, centre, 780f);
            BlockClicks(panel);
            CardHeader(panel, "CARD ART", 22, 58f);
            Button close = HeaderCloseButton(panel);
            RectTransform body = Body(panel, 20, 10f);
            Label(Child("PackTitle", body), boldFont, 16, Muted, TextAnchor.MiddleLeft, "ART PACK");
            RectTransform packRow = ButtonRow(body, TextAnchor.MiddleLeft);
            Button previous = PlainButton(packRow, "PreviousPackButton", "<", 56f, out _);
            RectTransform packLabelRect = Child("PackName", packRow);
            Text packLabel = Label(packLabelRect, boldFont, 18, Ink, TextAnchor.MiddleCenter, "Built-in");
            Size(packLabelRect.gameObject, 470f, 52f, 470f);
            Button next = PlainButton(packRow, "NextPackButton", ">", 56f, out _);
            RectTransform folderRow = ButtonRow(body, TextAnchor.MiddleLeft);
            Button openFolder = PlainButton(folderRow, "OpenFolderButton", "Open packs folder", 230f, out _);
            Button reload = PlainButton(folderRow, "ReloadButton", "Reload", 140f, out _);
            Label(Child("PicturesTitle", body), boldFont, 16, Muted, TextAnchor.MiddleLeft, "PICTURES");
            ScrollList(body, "PicturesViewport", 430f, out ScrollRect scroll, out RectTransform rows);
            Text status = Label(Child("Status", body), bodyFont, 15, Muted, TextAnchor.MiddleLeft,
                "Pictures you choose are saved in your profile.");

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("packLabel").objectReferenceValue = packLabel;
            so.FindProperty("previousPackButton").objectReferenceValue = previous;
            so.FindProperty("nextPackButton").objectReferenceValue = next;
            so.FindProperty("openFolderButton").objectReferenceValue = openFolder;
            so.FindProperty("reloadButton").objectReferenceValue = reload;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("rowsRoot").objectReferenceValue = rows;
            so.FindProperty("scroll").objectReferenceValue = scroll;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // One picture: thumbnail, name and source, then Choose picture and Reset.
        private static void BuildContentArtRow(RectTransform root, ContentArtRowView view)
        {
            root.sizeDelta = new Vector2(700f, 96f);
            Img(root, "ButtonOutlined", Cream).raycastTarget = false;
            Row(root.gameObject, 10, 12, 8, 8, 12f, TextAnchor.MiddleLeft);
            Size(root.gameObject, -1f, 96f);
            RectTransform thumbRect = Child("Thumbnail", root);
            RawImage thumbnail = thumbRect.gameObject.AddComponent<RawImage>();
            thumbnail.raycastTarget = false;
            Size(thumbRect.gameObject, 56f, 80f, 56f);
            RectTransform words = Child("Words", root);
            Column(words.gameObject, 0, 0, 4, 4, 2f, TextAnchor.MiddleLeft);
            LayoutElement wordsLayout = Size(words.gameObject, 300f, 80f, 200f);
            wordsLayout.flexibleWidth = 1f;
            Text name = Label(Child("Name", words), boldFont, 18, Ink, TextAnchor.MiddleLeft, "Shield");
            Text source = Label(Child("Source", words), bodyFont, 15, Muted, TextAnchor.MiddleLeft, "Built-in");
            Button choose = GoButton(root, "ChooseButton", "Choose picture...", 190f, out _);
            Button reset = PlainButton(root, "ResetButton", "Reset", 100f, out _);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("thumbnail").objectReferenceValue = thumbnail;
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("sourceLabel").objectReferenceValue = source;
            so.FindProperty("chooseButton").objectReferenceValue = choose;
            so.FindProperty("resetButton").objectReferenceValue = reset;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- player profiles (doc 28, P2) ----------

        // Centred modal: who is playing, every profile with Play as, and a name box to make a new profile or rename
        // this one. Switching profile starts a fresh table (the status line says so).
        private static void BuildProfilePanel(RectTransform root, ProfilePanelView view)
        {
            Button overlay = DismissOverlay(root);
            Vector2 centre = new Vector2(0.5f, 0.5f);
            RectTransform panel = ColumnCard(root, "ProfilePanel", centre, centre, 700f);
            BlockClicks(panel);
            CardHeader(panel, "PLAYER PROFILE", 22, 58f);
            Button close = HeaderCloseButton(panel);
            RectTransform body = Body(panel, 20, 10f);
            Label(Child("PlayingTitle", body), boldFont, 16, Muted, TextAnchor.MiddleLeft, "PLAYING AS");
            Text active = Label(Child("ActiveName", body), titleFont, 26, Ink, TextAnchor.MiddleLeft, "Player 1");
            Label(Child("ProfilesTitle", body), boldFont, 16, Muted, TextAnchor.MiddleLeft, "PROFILES ON THIS COMPUTER");
            ScrollList(body, "ProfilesViewport", 300f, out ScrollRect scroll, out RectTransform rows);
            Label(Child("NameTitle", body), boldFont, 16, Muted, TextAnchor.MiddleLeft, "NAME");
            InputField nameInput = TextInput(body, "NameInput", 52f, "Type a profile name...");
            nameInput.lineType = InputField.LineType.SingleLine;
            nameInput.characterLimit = 32;
            nameInput.textComponent.alignment = TextAnchor.MiddleLeft;
            ((Text)nameInput.placeholder).alignment = TextAnchor.MiddleLeft;
            RectTransform nameButtons = ButtonRow(body, TextAnchor.MiddleLeft);
            Button create = GoButton(nameButtons, "CreateButton", "New profile", 220f, out _);
            Button rename = PlainButton(nameButtons, "RenameButton", "Rename this profile", 250f, out _);
            Text status = Label(Child("Status", body), bodyFont, 15, Muted, TextAnchor.MiddleLeft,
                "Everything you make is saved in your profile.");
            RectTransform folderRow = ButtonRow(body, TextAnchor.MiddleLeft);
            Button openFolder = PlainButton(folderRow, "OpenFolderButton", "Open profiles folder", 250f, out _);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("activeNameLabel").objectReferenceValue = active;
            so.FindProperty("rowsRoot").objectReferenceValue = rows;
            so.FindProperty("scroll").objectReferenceValue = scroll;
            so.FindProperty("nameInput").objectReferenceValue = nameInput;
            so.FindProperty("createButton").objectReferenceValue = create;
            so.FindProperty("renameButton").objectReferenceValue = rename;
            so.FindProperty("openFolderButton").objectReferenceValue = openFolder;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // One profile: name and when it was last played, then Play as (or a PLAYING NOW mark for the active one).
        private static void BuildProfileRow(RectTransform root, ProfileRowView view)
        {
            root.sizeDelta = new Vector2(620f, 72f);
            Img(root, "ButtonOutlined", Cream).raycastTarget = false;
            Row(root.gameObject, 16, 12, 8, 8, 12f, TextAnchor.MiddleLeft);
            Size(root.gameObject, -1f, 72f);
            RectTransform words = Child("Words", root);
            Column(words.gameObject, 0, 0, 2, 2, 2f, TextAnchor.MiddleLeft);
            LayoutElement wordsLayout = Size(words.gameObject, 300f, 60f, 200f);
            wordsLayout.flexibleWidth = 1f;
            Text name = Label(Child("Name", words), boldFont, 19, Ink, TextAnchor.MiddleLeft, "Player 1");
            Text detail = Label(Child("Detail", words), bodyFont, 15, Muted, TextAnchor.MiddleLeft, "Last played today");
            RectTransform mark = Child("ActiveMark", root);
            Img(mark, "ButtonOutlined", Mustard).raycastTarget = false;
            Size(mark.gameObject, 150f, 42f, 150f);
            Label(Rect("Label", mark, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                titleFont, 14, Ink, TextAnchor.MiddleCenter, "PLAYING NOW");
            Button playAs = GoButton(root, "PlayAsButton", "Play as", 150f, out _);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("nameLabel").objectReferenceValue = name;
            so.FindProperty("detailLabel").objectReferenceValue = detail;
            so.FindProperty("playAsButton").objectReferenceValue = playAs;
            so.FindProperty("activeMark").objectReferenceValue = mark.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- hints (doc 23 §10) ----------

        // A cream card at the top centre, under the session bar: a mustard TIP badge, the hint and two buttons.
        private static void BuildHintStrip(RectTransform root, HintStripView view)
        {
            RectTransform card = Rect("Card", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -112f), new Vector2(820f, 96f));
            Backdrop(card, ShadowOffset);
            Row(card.gameObject, 14, 14, 10, 10, 14f, TextAnchor.MiddleLeft);
            card.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            RectTransform badge = Child("Badge", card);
            Img(badge, "ButtonOutlined", Mustard).raycastTarget = false;
            Size(badge.gameObject, 70f, 40f, 70f);
            Text kind = Label(Rect("Label", badge, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                titleFont, 15, Ink, TextAnchor.MiddleCenter, "TIP");

            Text text = Label(Child("Text", card), bodyFont, 16, Ink, TextAnchor.MiddleLeft, "A hint about the table.");
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 16;
            LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1f;
            textLayout.minHeight = 72f;

            Button later = TextButton(card, "LaterButton", "Later", boldFont, 15, Cream, Ink, 88f, 42f, out _);
            Button gotIt = TextButton(card, "GotItButton", "Got it", boldFont, 15, Go, Cream, 96f, 42f, out _);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("kindLabel").objectReferenceValue = kind;
            so.FindProperty("textLabel").objectReferenceValue = text;
            so.FindProperty("gotItButton").objectReferenceValue = gotIt;
            so.FindProperty("laterButton").objectReferenceValue = later;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- house rules (doc 23, R3) ----------

        // Centred modal: the rule now, the proposed change (a - value + stepper or a text box) and, once proposed,
        // one Agree button per player. "No" or Cancel keeps the rule.
        private static void BuildRuleProposalPanel(RectTransform root, RuleProposalPanelView view)
        {
            Button overlay = DismissOverlay(root);
            Vector2 centre = new Vector2(0.5f, 0.5f);
            RectTransform panel = ColumnCard(root, "RuleProposalPanel", centre, centre, 660f);
            BlockClicks(panel);
            Text title = CardHeader(panel, "CHANGE A RULE", 22, 58f);
            RectTransform body = Body(panel, 22, 10f);
            Label(Child("NowTitle", body), boldFont, 15, Muted, TextAnchor.MiddleLeft, "THE RULE NOW");
            Text current = Label(Child("Current", body), bodyFont, 17, Ink, TextAnchor.UpperLeft, "The rule as it is.");
            Label(Child("NewTitle", body), boldFont, 15, Muted, TextAnchor.MiddleLeft, "PROPOSED");
            Text value = Stepper(body, out Button decrement, out Button increment);
            LayoutElement valueLayout = value.GetComponent<LayoutElement>();
            if (valueLayout != null)
            {
                valueLayout.minWidth = 260f;
                valueLayout.preferredWidth = 260f;
            }

            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 14;
            value.resizeTextMaxSize = 32;
            GameObject stepRow = body.Find("Stepper").gameObject;
            InputField input = TextInput(body, "TextInput", 112f, "Type the new rule or value...");
            Text preview = Label(Child("Preview", body), bodyFont, 17, Ink, TextAnchor.UpperLeft, "The rule as proposed.");

            RectTransform agree = Child("AgreeRow", body);
            Column(agree.gameObject, 0, 0, 4, 0, 8f);
            Label(Child("AgreeTitle", agree), boldFont, 15, Muted, TextAnchor.MiddleLeft, "EVERY PLAYER AGREES WITH THEIR OWN BUTTON");
            Button[] agreeButtons = new Button[4];
            Text[] agreeLabels = new Text[4];
            for (int rowIndex = 0; rowIndex < 2; rowIndex++)
            {
                RectTransform row = ButtonRow(agree, TextAnchor.MiddleLeft);
                row.name = $"Voters{rowIndex + 1}";
                for (int column = 0; column < 2; column++)
                {
                    int i = (rowIndex * 2) + column;
                    agreeButtons[i] = GoButton(row, $"AgreeButton{i + 1}", $"Player {i + 1}: agree", 290f, out agreeLabels[i]);
                    SetDisabledTint(agreeButtons[i]);
                }
            }

            Text status = Label(Child("Status", body), bodyFont, 15, Muted, TextAnchor.MiddleLeft, string.Empty);
            RectTransform buttons = ButtonRow(body);
            Button reject = PlainButton(buttons, "RejectButton", "Cancel", 230f, out Text rejectLabel);
            Button propose = GoButton(buttons, "ProposeButton", "Propose to the table", 270f, out _);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("dismissOverlayButton").objectReferenceValue = overlay;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("currentLabel").objectReferenceValue = current;
            so.FindProperty("stepRow").objectReferenceValue = stepRow;
            so.FindProperty("decrementButton").objectReferenceValue = decrement;
            so.FindProperty("incrementButton").objectReferenceValue = increment;
            so.FindProperty("valueLabel").objectReferenceValue = value;
            so.FindProperty("textInput").objectReferenceValue = input;
            so.FindProperty("previewLabel").objectReferenceValue = preview;
            so.FindProperty("agreeRow").objectReferenceValue = agree.gameObject;
            AssignArray(so.FindProperty("agreeButtons"), agreeButtons);
            AssignArray(so.FindProperty("agreeLabels"), agreeLabels);
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("proposeButton").objectReferenceValue = propose;
            so.FindProperty("rejectButton").objectReferenceValue = reject;
            so.FindProperty("rejectLabel").objectReferenceValue = rejectLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InputField TextInput(Transform parent, string name, float height, string placeholderText)
        {
            RectTransform rect = Child(name, parent);
            Image fill = Img(rect, "ButtonOutlined", Cream);
            Size(rect.gameObject, -1f, height);
            InputField input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = fill;
            Text text = Label(Rect("Text", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-24f, -16f)), bodyFont, 17, Ink, TextAnchor.UpperLeft, string.Empty);
            text.supportRichText = false;
            Text placeholder = Label(Rect("Placeholder", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-24f, -16f)), bodyFont, 17, Muted, TextAnchor.UpperLeft, placeholderText);
            placeholder.fontStyle = FontStyle.Italic;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.MultiLineNewline;
            return input;
        }

        // Adds the R3 controls to rules prefabs made before R3 (a Change button on each rule line; the rule-set row
        // and the restart row on the card). Only missing controls are added; nothing existing is changed.
        private static void AddMissingRuleControls()
        {
            if (File.Exists(RulesLinePath))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(RulesLinePath);
                try
                {
                    SerializedObject so = new SerializedObject(root.GetComponent<RulesCardLineView>());
                    SerializedProperty change = so.FindProperty("changeButton");
                    if (change != null && change.objectReferenceValue == null)
                    {
                        Button button = TextButton(root.transform, "ChangeButton", "Change", boldFont, 13, Cream, Ink, 78f, 28f, out _);
                        button.gameObject.SetActive(false);
                        change.objectReferenceValue = button;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, RulesLinePath);
                        Debug.Log("[Gameplay UI] Added the Change button to the rules line.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (File.Exists(RulesCardPath))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(RulesCardPath);
                try
                {
                    SerializedObject so = new SerializedObject(root.GetComponent<RulesCardView>());
                    Transform card = root.transform.Find("Card");
                    Transform cardBody = card != null ? card.Find("Body") : null;
                    if (so.FindProperty("ruleSetRow") != null && so.FindProperty("ruleSetRow").objectReferenceValue == null
                        && card != null && cardBody != null)
                    {
                        int bodyIndex = cardBody.GetSiblingIndex();
                        RectTransform setRow = Child("RuleSetRow", card);
                        Img(setRow, "Solid", CreamDark).raycastTarget = false;
                        Row(setRow.gameObject, 16, 16, 6, 6, 10f, TextAnchor.MiddleLeft);
                        Size(setRow.gameObject, -1f, 48f);
                        Text setTitle = Label(Child("Title", setRow), boldFont, 15, Muted, TextAnchor.MiddleLeft, "RULE SET");
                        Size(setTitle.gameObject, 90f, 36f, 90f);
                        Button previous = TextButton(setRow, "PreviousSetButton", "<", boldFont, 18, Cream, Ink, 44f, 36f, out _);
                        Button next = TextButton(setRow, "NextSetButton", ">", boldFont, 18, Cream, Ink, 44f, 36f, out _);
                        Label(Child("Hint", setRow), bodyFont, 13, Muted, TextAnchor.MiddleLeft, "Switching asks the table.");
                        setRow.SetSiblingIndex(bodyIndex);
                        setRow.gameObject.SetActive(false);

                        RectTransform pending = Child("PendingRow", card);
                        Img(pending, "Solid", Mustard).raycastTarget = false;
                        Row(pending.gameObject, 16, 12, 6, 6, 10f, TextAnchor.MiddleLeft);
                        Size(pending.gameObject, -1f, 58f);
                        Text pendingText = Label(Child("Text", pending), bodyFont, 14, Ink, TextAnchor.MiddleLeft,
                            "Changed settings start when the game restarts.");
                        pendingText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                        Button restart = TextButton(pending, "RestartButton", "Restart", boldFont, 15, Go, Cream, 110f, 42f, out _);
                        pending.SetSiblingIndex(bodyIndex + 1);
                        pending.gameObject.SetActive(false);

                        so.FindProperty("ruleSetRow").objectReferenceValue = setRow.gameObject;
                        so.FindProperty("previousSetButton").objectReferenceValue = previous;
                        so.FindProperty("nextSetButton").objectReferenceValue = next;
                        so.FindProperty("pendingRow").objectReferenceValue = pending.gameObject;
                        so.FindProperty("pendingLabel").objectReferenceValue = pendingText;
                        so.FindProperty("restartButton").objectReferenceValue = restart;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, RulesCardPath);
                        Debug.Log("[Gameplay UI] Added the rule-set and restart rows to the rules card.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void BuildFeedbackMessages(RectTransform root, FeedbackMessageStackView view)
        {
            // Bottom centre, above the status strip; newest row at the bottom. Never takes clicks.
            const int RowCount = 4;
            RectTransform stack = Rect("Messages", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 150f), new Vector2(760f, 4f * 46f));
            VerticalLayoutGroup column = Column(stack.gameObject, 0, 0, 0, 0, 6f, TextAnchor.LowerCenter);
            column.reverseArrangement = true;
            GameObject[] rows = new GameObject[RowCount];
            Image[] fills = new Image[RowCount];
            Text[] labels = new Text[RowCount];
            CanvasGroup[] groups = new CanvasGroup[RowCount];
            for (int i = 0; i < RowCount; i++)
            {
                RectTransform row = Child($"Row{i}", stack);
                fills[i] = Img(row, "ButtonOutlined", Ink);
                fills[i].raycastTarget = false;
                groups[i] = row.gameObject.AddComponent<CanvasGroup>();
                groups[i].blocksRaycasts = false;
                HorizontalLayoutGroup rowLayout = Row(row.gameObject, 18, 18, 8, 8, 0f, TextAnchor.MiddleCenter);
                rowLayout.childForceExpandWidth = true;
                labels[i] = Label(Child("Text", row), boldFont, 17, Cream, TextAnchor.MiddleCenter, "Message");
                rows[i] = row.gameObject;
                row.gameObject.SetActive(false);
            }

            SerializedObject so = new SerializedObject(view);
            AssignArray(so.FindProperty("rows"), rows);
            AssignArray(so.FindProperty("rowFills"), fills);
            AssignArray(so.FindProperty("rowLabels"), labels);
            AssignArray(so.FindProperty("rowGroups"), groups);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
            // Fixed width in the gap between the session bar (ends near x 580) and the status card (starts at
            // x 1451): it never runs under either. Long progress text wraps and the strip grows downward.
            RectTransform strip = Rect("TurnStrip", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(TurnStripCentreOffset, -Margin), new Vector2(TurnStripWidth, 64f));
            Img(strip, "ButtonOutlined", Ink);
            Row(strip.gameObject, 22, 22, 10, 10, 22f, TextAnchor.MiddleLeft);
            Fit(strip.gameObject, false, true);
            Size(strip.gameObject, -1f, 64f);
            Text round = FitLabel(Child("Round", strip), titleFont, 17, Mustard, "ROUND 1");
            // Whose turn it is, by colour (UX-1): the pawn icon on a chip filled with that player's colour.
            RectTransform chip = Child("PlayerChip", strip);
            Image chipFill = Img(chip, "ChipOutlined", Cream);
            chipFill.raycastTarget = false;
            Size(chip.gameObject, 40f, 40f);
            Img(Rect("Icon", chip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(26f, 26f)), null, Ink, ToolboxIconPath + "IconPawn.png").raycastTarget = false;
            Text phase = FitLabel(Child("Phase", strip), boldFont, 20, Cream, "PLAYER 1 TURN");
            Text keys = Label(Child("Keys", strip), boldFont, 16, Cream, TextAnchor.MiddleLeft, "KEYS 0 / 1");
            keys.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

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
            so.FindProperty("playerChip").objectReferenceValue = chipFill;
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
            Text shopTitle = CardHeader(panel, "SHOP", 21, 58f);
            Button close = HeaderCloseButton(panel);
            RectTransform body = Child("Body", panel);
            Row(body.gameObject, 24, 24, 22, 24, 26f, TextAnchor.UpperLeft);

            RectTransform list = Child("List", body);
            Column(list.gameObject, 0, 0, 0, 0, 8f);
            Size(list.gameObject, 340f, -1f, 340f);
            Text listTitle = Label(Child("ListTitle", list), titleFont, 15, Muted, TextAnchor.MiddleLeft, "ABILITIES");
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
            so.FindProperty("titleLabel").objectReferenceValue = shopTitle;
            so.FindProperty("listTitleLabel").objectReferenceValue = listTitle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The Shop (doc 18 §15.4) names its header and list from code; an older AbilityPurchase prefab only gets
        // those two labels wired (nothing else in it changes).
        private static void AddMissingShopLabels()
        {
            if (!File.Exists(AbilityPurchasePath)) return;
            GameObject root = PrefabUtility.LoadPrefabContents(AbilityPurchasePath);
            try
            {
                PrototypeActionAbilityPurchasePopupView view = root.GetComponent<PrototypeActionAbilityPurchasePopupView>();
                SerializedObject so = view != null ? new SerializedObject(view) : null;
                SerializedProperty title = so?.FindProperty("titleLabel");
                SerializedProperty listTitle = so?.FindProperty("listTitleLabel");
                if (title == null || listTitle == null
                    || (title.objectReferenceValue != null && listTitle.objectReferenceValue != null))
                    return;
                Transform header = root.transform.Find("PurchasePanel/Header/Title");
                Transform list = root.transform.Find("PurchasePanel/Body/List/ListTitle");
                if (title.objectReferenceValue == null && header != null)
                    title.objectReferenceValue = header.GetComponent<Text>();
                if (listTitle.objectReferenceValue == null && list != null)
                    listTitle.objectReferenceValue = list.GetComponent<Text>();
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, AbilityPurchasePath);
                Debug.Log("[Gameplay UI] Wired the Shop title labels in the ability purchase panel.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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

        // ---------- rules card (doc 23, R1) ----------

        private static void BuildRulesCard(RectTransform root, RulesCardView view)
        {
            RectTransform card = Rect("Card", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -RulesCardTop), new Vector2(RulesCardWidth, 300f));
            Backdrop(card, ShadowOffset);
            Column(card.gameObject, 5, 5, 5, 5, 0f);
            Fit(card.gameObject, false, true);

            RectTransform header = Child("Header", card);
            Img(header, "TopRounded", Orange).raycastTarget = false;
            Size(header.gameObject, -1f, 56f);
            Label(Rect("Title", header, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(18f, 0f),
                new Vector2(110f, 0f)), titleFont, 22, Cream, TextAnchor.MiddleLeft, "RULES");
            Text setName = Label(Rect("SetName", header, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), new Vector2(128f, 0f),
                new Vector2(-312f, 0f)), boldFont, 16, Cream, TextAnchor.MiddleLeft, "Default");
            setName.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform badge = Rect("HouseRulesBadge", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-64f, 0f), new Vector2(124f, 28f));
            Img(badge, "ButtonOutlined", Mustard).raycastTarget = false;
            Label(Rect("Label", badge, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                titleFont, 12, Ink, TextAnchor.MiddleCenter, "HOUSE RULES");
            badge.gameObject.SetActive(false);
            RectTransform collapse = Rect("CollapseButton", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(42f, 42f));
            Image collapseFill = Img(collapse, "ButtonOutlined", Cream);
            Button collapseButton = collapse.gameObject.AddComponent<Button>();
            collapseButton.targetGraphic = collapseFill;
            RectTransform chevron = Rect("Chevron", collapse, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Img(chevron, null, Ink, SessionIconPath + "IconChevron.png").raycastTarget = false;
            RectTransform rule = Child("HeaderRule", card);
            Img(rule, "Solid", Ink).raycastTarget = false;
            Size(rule.gameObject, -1f, 5f);

            // Body: a scrolling list of pooled heading and rule lines.
            RectTransform body = Child("Body", card);
            Img(body, "BottomRounded", Cream);
            body.gameObject.AddComponent<RectMask2D>();
            Size(body.gameObject, -1f, RulesBodyHeight);
            RectTransform lines = Rect("Lines", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            Column(lines.gameObject, 22, 18, 14, 18, 8f);
            Fit(lines.gameObject, false, true);
            ScrollRect scroll = body.gameObject.AddComponent<ScrollRect>();
            scroll.content = lines;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("setNameLabel").objectReferenceValue = setName;
            so.FindProperty("houseRulesBadge").objectReferenceValue = badge.gameObject;
            so.FindProperty("collapseButton").objectReferenceValue = collapseButton;
            so.FindProperty("chevron").objectReferenceValue = chevron;
            so.FindProperty("body").objectReferenceValue = body.gameObject;
            so.FindProperty("linesRoot").objectReferenceValue = lines;
            so.FindProperty("scrollRect").objectReferenceValue = scroll;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRulesHeading(RectTransform root, RulesCardLineView view)
        {
            root.sizeDelta = new Vector2(380f, 30f);
            Size(root.gameObject, -1f, 30f);
            Text text = Label(Rect("Text", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -3f),
                Vector2.zero), titleFont, 15, Orange, TextAnchor.LowerLeft, "HEADING");

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("textLabel").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRulesLine(RectTransform root, RulesCardLineView view)
        {
            root.sizeDelta = new Vector2(380f, 24f);
            Row(root.gameObject, 0, 0, 0, 0, 10f, TextAnchor.UpperLeft);
            Text number = Label(Child("Number", root), titleFont, 16, Orange, TextAnchor.UpperLeft, "1");
            Size(number.gameObject, 24f, -1f, 24f);
            Text text = Label(Child("Text", root), bodyFont, 16, Ink, TextAnchor.UpperLeft, "Rule");
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            // Changed mark: a mustard bar at the line's left edge when it differs from the rules it is based on.
            RectTransform mark = Rect("ChangedMark", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f), new Vector2(6f, 0f));
            Img(mark, "Solid", Mustard).raycastTarget = false;
            mark.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            mark.gameObject.SetActive(false);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("numberLabel").objectReferenceValue = number;
            so.FindProperty("textLabel").objectReferenceValue = text;
            so.FindProperty("changedMark").objectReferenceValue = mark.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            // The R3 Change button is added by AddMissingRuleControls, for new and older prefabs alike.
        }
    }
}
