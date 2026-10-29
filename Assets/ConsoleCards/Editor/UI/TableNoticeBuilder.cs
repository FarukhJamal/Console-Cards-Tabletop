using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// POP-2 (owner approved "Popups v2", 2026-10-29; doc 38): builds the table notices in the kit look, colour B
    /// (orange for where you are in the game, teal for the main button, yellow for warnings): the choice chip beside a
    /// tile, the yellow warning line with UNDO, and the floor-end chip. They live in their own folder,
    /// Final UI/Notices, and are yours to edit: an existing prefab is never rebuilt (delete it to get the built
    /// default back). Apply Kit Look does not touch this folder. The owner expects to change the look again, so every
    /// size and colour sits on the prefab (and its view's colour fields), not in code.
    /// </summary>
    public static class TableNoticeBuilder
    {
        public const string Folder = FinalUiFolder + "Notices/";
        public const string ChoiceChipPath = Folder + "TableChoiceChip.prefab";
        public const string WarningPath = Folder + "TableWarning.prefab";
        public const string MilestonePath = Folder + "TableMilestone.prefab";

        // The kit (doc 36 §1) and colour B (doc 38).
        private static readonly Color PanelFill = new Color(0.078f, 0.094f, 0.098f, 0.94f);
        private static readonly Color Words = Hex("e8eced");
        private static readonly Color SoftWords = Hex("c9d0d2");
        private static readonly Color MutedWords = Hex("9ba4a7");
        private static readonly Color Cyan = Hex("06eff0");
        private static readonly Color Primary = Hex("138a8c");
        private static readonly Color Secondary = Hex("222527");
        private static readonly Color Line = Hex("3c4143");
        private static readonly Color Yellow = Hex("ffc93b");
        private static readonly Color YellowFill = new Color(0.110f, 0.094f, 0.039f, 0.94f);
        private static readonly Color YellowEdge = Hex("8a6d12");
        private static readonly Color YellowWords = Hex("f3e7b8");
        private static readonly Color Green = Hex("2fd46b");
        private static readonly Color RingBack = Hex("2b3033");

        private static Font bold;
        private static Font medium;
        private static Sprite rounded;
        private static Sprite circle;

        [MenuItem("Console Cards/UI/Create Missing Table Notices (POP-2)")]
        public static void Build()
        {
            bold = RequireFont("Oxanium-Bold.ttf");
            medium = RequireFont("Oxanium-Medium.ttf");
            rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Directory.CreateDirectory(Folder);

            Save<TableChoiceChipView>(ChoiceChipPath, "TableChoiceChip", BuildChoiceChip);
            Save<TableWarningView>(WarningPath, "TableWarning", BuildWarning);
            Save<TableMilestoneView>(MilestonePath, "TableMilestone", BuildMilestone);

            AssignCatalogEntry(UiPrefabIds.TableChoiceChip,
                AssetDatabase.LoadAssetAtPath<TableChoiceChipView>(ChoiceChipPath), UiLayer.Hud,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(UiPrefabIds.TableWarning,
                AssetDatabase.LoadAssetAtPath<TableWarningView>(WarningPath), UiLayer.Notification,
                UiRetention.CachedSingleInstance);
            AssignCatalogEntry(UiPrefabIds.TableMilestone,
                AssetDatabase.LoadAssetAtPath<TableMilestoneView>(MilestonePath), UiLayer.Hud,
                UiRetention.CachedSingleInstance);
            AssetDatabase.SaveAssets();
            Debug.Log("[POP-2] Created any missing table notices (Final UI/Notices) and pointed the UI prefab catalog at them.");
            TableHudBuilder.Build(); // HUD-B: the colour-B HUD, the rulebook, the Shop drawer and the payment strip
        }

        private static void Save<T>(string path, string name, System.Action<RectTransform, T> build)
            where T : ReusableUiView
        {
            if (File.Exists(path))
            {
                Debug.Log($"[POP-2] Kept {path} (already exists; edit it in Final UI/Notices).");
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

        // ---------- the choice chip beside a tile ----------

        private static void BuildChoiceChip(RectTransform root, TableChoiceChipView view)
        {
            // The dotted leader from the tile to the lifted card (drawn as a thin line; recolour or swap its sprite).
            RectTransform leader = Rect("Leader", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(3f, 60f));
            Image leaderImage = leader.gameObject.AddComponent<Image>();
            leaderImage.color = Cyan;
            leaderImage.raycastTarget = false;

            // The card, lifted off its tile and glowing in the tone.
            RectTransform card = Rect("LiftedCard", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 168f));
            RectTransform glowRect = Rect("Glow", card, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(8f, 8f));
            Image glow = Rounded(glowRect, Cyan);
            glow.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -12f);
            RectTransform pictureRect = Rect("Picture", card, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            RawImage picture = pictureRect.gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;

            // The chip: tag and name, one short text, the way out and the main button.
            RectTransform chip = Rect("Chip", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(360f, 140f));
            Image border = Plate(chip, Cyan, PanelFill, out _);
            VerticalLayoutGroup column = Column(chip.gameObject, 16, 16, 14, 14, 10);
            column.childForceExpandWidth = true;
            Fit(chip.gameObject, false, true);

            Text dock = FitLabel(Child("Parked", chip), bold, 13, MutedWords, "1 CHOICE OPEN · MOVE THE CAMERA TO SEE IT");
            dock.horizontalOverflow = HorizontalWrapMode.Wrap;
            dock.gameObject.SetActive(false);

            RectTransform header = Child("Header", chip);
            Row(header.gameObject, 0, 0, 0, 0, 10);
            Image tagFill = Tag(header, "ABILITY · PRIZE", Cyan, Hex("102021"), out Text tagLabel);
            Text title = FitLabel(Child("Name", header), bold, 22, Words, "Rush");
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            Size(title.gameObject, -1f, 30f).flexibleWidth = 1f;

            Text body = Label(Child("Body", chip), medium, 16, SoftWords, TextAnchor.UpperLeft,
                "Take it? Cost 2: pay it from your Hand through your Console.");

            RectTransform buttons = Child("Buttons", chip);
            Row(buttons.gameObject, 0, 0, 2, 0, 10);
            Button secondary = KitButton(buttons, "Secondary", "LEAVE", Secondary, SoftWords, 1f, out Text secondaryLabel);
            Button primary = KitButton(buttons, "Primary", "TAKE IT", Primary, Hex("f4fefe"), 1.4f, out Text primaryLabel);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("chip").objectReferenceValue = chip;
            so.FindProperty("chipBorder").objectReferenceValue = border;
            so.FindProperty("card").objectReferenceValue = card;
            so.FindProperty("cardGlow").objectReferenceValue = glow;
            so.FindProperty("cardPicture").objectReferenceValue = picture;
            so.FindProperty("leader").objectReferenceValue = leader;
            so.FindProperty("leaderImage").objectReferenceValue = leaderImage;
            so.FindProperty("tagFill").objectReferenceValue = tagFill;
            so.FindProperty("tagLabel").objectReferenceValue = tagLabel;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("bodyLabel").objectReferenceValue = body;
            so.FindProperty("dockLabel").objectReferenceValue = dock.gameObject;
            so.FindProperty("primaryButton").objectReferenceValue = primary;
            so.FindProperty("primaryLabel").objectReferenceValue = primaryLabel;
            so.FindProperty("secondaryButton").objectReferenceValue = secondary;
            so.FindProperty("secondaryLabel").objectReferenceValue = secondaryLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- the yellow warning line ----------

        private static void BuildWarning(RectTransform root, TableWarningView view)
        {
            RectTransform panel = Rect("Warning", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(640f, 44f));
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            Plate(panel, YellowEdge, YellowFill, out _);
            HorizontalLayoutGroup row = Row(panel.gameObject, 0, 6, 6, 6, 12);
            row.childAlignment = TextAnchor.MiddleLeft;
            Fit(panel.gameObject, true, true);

            RectTransform bar = Child("Bar", panel);
            Image barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = Yellow;
            barImage.raycastTarget = false;
            Size(bar.gameObject, 6f, 32f, 6f);

            RectTransform icon = Child("Icon", panel);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = circle;
            iconImage.color = Yellow;
            iconImage.raycastTarget = false;
            Size(icon.gameObject, 22f, 22f, 22f);
            Label(Rect("Mark", icon, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                bold, 15, Hex("1a1406"), TextAnchor.MiddleCenter, "!");

            Text message = Label(Child("Message", panel), medium, 16, YellowWords, TextAnchor.MiddleLeft,
                "<b><color=#FFC93B>Round over</color></b>  ·  roll the floor dice first");
            LayoutElement messageSize = Size(message.gameObject, -1f, -1f);
            messageSize.preferredWidth = 520f;

            Button fix = OutlineButton(panel, "Undo", "UNDO", Yellow, YellowFill, 74f, 28f, out Text fixLabel);

            RectTransform closeRect = Child("Close", panel);
            Text closeMark = Label(closeRect, bold, 20, MutedWords, TextAnchor.MiddleCenter, "×");
            closeMark.raycastTarget = true;
            Button close = closeRect.gameObject.AddComponent<Button>();
            close.targetGraphic = closeMark;
            Size(closeRect.gameObject, 26f, 28f, 26f);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("messageLabel").objectReferenceValue = message;
            so.FindProperty("fixButton").objectReferenceValue = fix;
            so.FindProperty("fixLabel").objectReferenceValue = fixLabel;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- the floor-end chip ----------

        private static void BuildMilestone(RectTransform root, TableMilestoneView view)
        {
            RectTransform panel = Rect("Milestone", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -96f), new Vector2(620f, 72f));
            Image border = Plate(panel, Green, PanelFill, out _);
            HorizontalLayoutGroup row = Row(panel.gameObject, 0, 10, 10, 10, 14);
            row.childAlignment = TextAnchor.MiddleLeft;
            Fit(panel.gameObject, true, true);

            RectTransform bar = Child("ToneBar", panel);
            Image toneBar = bar.gameObject.AddComponent<Image>();
            toneBar.color = Green;
            toneBar.raycastTarget = false;
            Size(bar.gameObject, 8f, 52f, 8f);

            Image tagFill = Tag(panel, "FLOOR 1 WON", Green, Hex("0e2416"), out Text tagLabel);

            RectTransform words = Child("Words", panel);
            Column(words.gameObject, 0, 0, 0, 0, 2);
            Text title = FitLabel(Child("Title", words), bold, 20, Words, "Floor 2 of 4");
            Text detail = FitLabel(Child("Detail", words), medium, 14, MutedWords, "5×5, dealt fresh");
            Text hint = FitLabel(Child("Hint", words), medium, 12, MutedWords, "Esc stops the countdown");

            RectTransform ring = Child("Countdown", panel);
            Size(ring.gameObject, 40f, 40f, 40f);
            Image ringBack = ring.gameObject.AddComponent<Image>();
            ringBack.sprite = circle;
            ringBack.color = RingBack;
            ringBack.raycastTarget = false;
            RectTransform fillRect = Rect("Fill", ring, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            Image ringFill = fillRect.gameObject.AddComponent<Image>();
            ringFill.sprite = circle;
            ringFill.color = Cyan;
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = false;
            ringFill.raycastTarget = false;
            RectTransform innerRect = Rect("Inner", ring, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            Image inner = innerRect.gameObject.AddComponent<Image>();
            inner.sprite = circle;
            inner.color = Hex("1c1f1f");
            inner.raycastTarget = false;
            Text count = Label(Rect("Seconds", ring, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero), bold, 15, Words, TextAnchor.MiddleCenter, "5");

            RectTransform notYetRect = Child("NotYet", panel);
            Text notYetLabel = FitLabel(notYetRect, bold, 13, MutedWords, "NOT YET");
            notYetLabel.raycastTarget = true;
            Button notYet = notYetRect.gameObject.AddComponent<Button>();
            notYet.targetGraphic = notYetLabel;

            Button primary = KitButton(panel, "Primary", "START", Primary, Hex("f4fefe"), 0f, out Text primaryLabel);
            Size(primary.gameObject, 110f, 44f, 110f);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("toneBar").objectReferenceValue = toneBar;
            so.FindProperty("panelBorder").objectReferenceValue = border;
            so.FindProperty("tagFill").objectReferenceValue = tagFill;
            so.FindProperty("tagLabel").objectReferenceValue = tagLabel;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("detailLabel").objectReferenceValue = detail;
            so.FindProperty("countdownRoot").objectReferenceValue = ring.gameObject;
            so.FindProperty("countdownRing").objectReferenceValue = ringFill;
            so.FindProperty("countdownLabel").objectReferenceValue = count;
            so.FindProperty("hintLabel").objectReferenceValue = hint;
            so.FindProperty("primaryButton").objectReferenceValue = primary;
            so.FindProperty("primaryLabel").objectReferenceValue = primaryLabel;
            so.FindProperty("secondaryButton").objectReferenceValue = notYet;
            so.FindProperty("secondaryLabel").objectReferenceValue = notYetLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- parts ----------

        private static Image Rounded(RectTransform rect, Color colour)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        // A panel's edge and fill, behind its contents and outside its layout; the fill takes the clicks.
        private static Image Plate(RectTransform panel, Color edge, Color fill, out Image fillImage)
        {
            RectTransform edgeRect = Rect("Edge", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(3f, 3f));
            Image edgeImage = Rounded(edgeRect, edge);
            edgeRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            edgeRect.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -10f);
            RectTransform fillRect = Rect("Fill", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            fillImage = Rounded(fillRect, fill);
            fillImage.raycastTarget = true;
            fillRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return edgeImage;
        }

        private static Image Tag(Transform parent, string text, Color fill, Color words, out Text label)
        {
            RectTransform rect = Child("Tag", parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = fill;
            image.raycastTarget = false;
            HorizontalLayoutGroup row = Row(rect.gameObject, 9, 9, 4, 4, 0, TextAnchor.MiddleCenter);
            row.childForceExpandHeight = false;
            label = FitLabel(Child("Label", rect), bold, 13, words, text);
            label.alignment = TextAnchor.MiddleCenter;
            Size(rect.gameObject, -1f, 26f);
            return image;
        }

        private static Button KitButton(Transform parent, string name, string text, Color fill, Color words,
            float flexible, out Text label)
        {
            RectTransform rect = Child(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = fill;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            LayoutElement size = Size(rect.gameObject, -1f, 44f);
            size.flexibleWidth = flexible;
            label = Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-12f, 0f)), bold, 16, words, TextAnchor.MiddleCenter, text);
            return button;
        }

        // A small outlined button (the fix inside a warning): a thin coloured edge, the panel's fill, coloured words.
        private static Button OutlineButton(Transform parent, string name, string text, Color edge, Color fill,
            float width, float height, out Text label)
        {
            RectTransform rect = Child(name, parent);
            Image edgeImage = rect.gameObject.AddComponent<Image>();
            edgeImage.sprite = rounded;
            edgeImage.type = Image.Type.Sliced;
            edgeImage.color = edge;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = edgeImage;
            Size(rect.gameObject, width, height, width);
            RectTransform inside = Rect("Inside", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-3f, -3f));
            Rounded(inside, new Color(fill.r, fill.g, fill.b, 1f));
            label = Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero), bold, 13, edge, TextAnchor.MiddleCenter, text);
            return button;
        }
    }
}
