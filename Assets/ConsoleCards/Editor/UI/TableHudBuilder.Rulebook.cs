using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// BOOK-1 (owner, 2026-10-30: the rulebook as a book on the table, "like Red Dead Redemption"): makes the book in
    /// Resources/Table/TableRulebook.prefab with its three materials (cover, paper, spine). Plain box shapes, so it is
    /// easy to swap for a modelled book later: keep the TableRulebookObject and point its fields at the new parts.
    /// The open book is design board "Rulebook" (cream pages, numbered steps, section tabs, the bar under the book).
    /// A book made before that board is replaced once (it is the builder's own); after that it is kept.
    /// </summary>
    public static partial class TableHudBuilder
    {
        public const string BookFolder = "Assets/ConsoleCards/Content/Resources/Table/";
        public const string BookPath = BookFolder + "TableRulebook.prefab";

        // The closed book, in its own units: page width (x), depth (z), cover and page-block thickness.
        private const float BookWidth = 1.2f;
        private const float BookDepth = 1.6f;
        private const float CoverThickness = 0.03f;
        private const float PagesThickness = 0.1f;
        private const float CanvasUnit = 0.001f; // one canvas pixel in book units
        private const string BookLabel = "RulebookBoard9c";
        private static readonly Color BookEyebrow = Hex("c0501c");
        private static readonly Color HouseFill = Hex("e2d8c0");
        private static readonly Color TabIdle = Hex("d6cfbd");
        private static readonly Color TabHouse = Hex("f4c9a6");
        private static readonly Color BarFill = Hex("15191a");
        private static readonly Color FieldFill = Hex("0f1213");

        private static void BuildRulebookObject()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BookPath);
            if (existing != null && System.Array.IndexOf(AssetDatabase.GetLabels(existing), BookLabel) < 0)
            {
                AssetDatabase.DeleteAsset(BookPath); // made before the Rulebook board (by this builder): built again
            }

            if (File.Exists(BookPath))
            {
                Debug.Log($"[BOOK-1] Kept {BookPath} (already exists; edit it, or delete it to get the built book back).");
                return;
            }

            Directory.CreateDirectory(BookFolder);
            round = round != null ? round : HudSprite("HudRound", new Vector4(16f, 16f, 16f, 16f));
            frame = frame != null ? frame : HudSprite("HudFrame", new Vector4(16f, 16f, 16f, 16f));
            if (round == null) round = rounded;
            Sprite ribbon = HudSprite("HudRibbon", Vector4.zero);
            Material cover = BookMaterial("TableRulebookCover", DeepTeal, 0.35f);
            Material paper = BookMaterial("TableRulebookPaper", Paper, 0.1f);
            Material spine = BookMaterial("TableRulebookSpine", Orange, 0.3f);

            GameObject root = new GameObject("TableRulebook");
            try
            {
                float height = (2f * CoverThickness) + PagesThickness;
                float top = CoverThickness + PagesThickness;
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, height * 0.5f, 0f);
                collider.size = new Vector3(BookWidth + 0.06f, height, BookDepth + 0.04f);

                Box("BackCover", root.transform, new Vector3(0f, CoverThickness * 0.5f, 0f),
                    new Vector3(BookWidth + 0.04f, CoverThickness, BookDepth + 0.04f), cover);
                Box("Pages", root.transform, new Vector3(0f, CoverThickness + (PagesThickness * 0.5f), 0f),
                    new Vector3(BookWidth - 0.02f, PagesThickness, BookDepth - 0.04f), paper);
                Box("Spine", root.transform, new Vector3(-(BookWidth * 0.5f) - 0.02f, height * 0.5f, 0f),
                    new Vector3(0.04f, height, BookDepth + 0.04f), spine);

                // The front cover swings open on the spine; the left page is the paper on its inside.
                Transform hinge = new GameObject("CoverHinge").transform;
                hinge.SetParent(root.transform, false);
                hinge.localPosition = new Vector3(-BookWidth * 0.5f, top, 0f);
                Box("FrontCover", hinge, new Vector3((BookWidth * 0.5f) + 0.02f, CoverThickness * 0.5f, 0f),
                    new Vector3(BookWidth + 0.04f, CoverThickness, BookDepth + 0.04f), cover);
                Box("Band", hinge, new Vector3((BookWidth * 0.5f) + 0.02f, CoverThickness + 0.001f, -BookDepth * 0.3f),
                    new Vector3(BookWidth + 0.04f, 0.002f, 0.08f), spine);
                Box("LeftPaper", hinge, new Vector3(BookWidth * 0.5f, -0.0015f, 0f),
                    new Vector3(BookWidth - 0.04f, 0.003f, BookDepth - 0.06f), paper);
                Quaternion faceUp = Quaternion.Euler(90f, 0f, 0f);
                Quaternion insideCover = Quaternion.Inverse(Quaternion.AngleAxis(180f, Vector3.forward)) * faceUp;

                RectTransform coverCanvas = WorldCanvas("CoverText", hinge,
                    new Vector3((BookWidth * 0.5f) + 0.02f, CoverThickness + 0.003f, 0.1f), faceUp,
                    new Vector2(1000f, 900f));
                Text coverTitle = Label(Rect("Title", coverCanvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(-80f, -80f)), bold, 120, Paper, TextAnchor.MiddleCenter, "RULEBOOK");
                coverTitle.supportRichText = true;

                RectTransform leftCanvas = WorldCanvas("LeftPage", hinge, new Vector3(BookWidth * 0.5f, -0.004f, 0f),
                    insideCover, PageSize());
                PageSide leftSide = PageText(leftCanvas, false, null);

                RectTransform rightCanvas = WorldCanvas("RightPage", root.transform, new Vector3(0f, top + 0.002f, 0f), faceUp,
                    PageSize());
                PageSide rightSide = PageText(rightCanvas, true, ribbon);

                // A blank page that turns over the spine while the words change.
                Transform flipHinge = new GameObject("FlipHinge").transform;
                flipHinge.SetParent(root.transform, false);
                flipHinge.localPosition = new Vector3(-BookWidth * 0.5f, top + 0.005f, 0f);
                GameObject flipPage = Box("FlipPage", flipHinge, new Vector3(BookWidth * 0.5f, 0f, 0f),
                    new Vector3(BookWidth - 0.04f, 0.002f, BookDepth - 0.06f), paper);
                flipPage.SetActive(false);

                ReadingParts parts = BuildReadingBar(root.transform);
                PinnedParts pin = BuildPinned(root.transform);

                TableRulebookObject book = root.AddComponent<TableRulebookObject>();
                SerializedObject so = new SerializedObject(book);
                so.FindProperty("coverHinge").objectReferenceValue = hinge;
                so.FindProperty("flipHinge").objectReferenceValue = flipHinge;
                so.FindProperty("flipPage").objectReferenceValue = flipPage;
                so.FindProperty("clickCollider").objectReferenceValue = collider;
                so.FindProperty("coverTitle").objectReferenceValue = coverTitle;
                so.FindProperty("openCentre").vector3Value = new Vector3(-BookWidth * 0.5f, top, 0f);
                so.FindProperty("openWidth").floatValue = (2f * BookWidth) + 0.08f;
                so.FindProperty("openHeight").floatValue = BookDepth + 0.04f;
                AssignSide(so, "left", leftSide);
                AssignSide(so, "right", rightSide);
                so.FindProperty("readingCanvas").objectReferenceValue = parts.Reading;
                so.FindProperty("previousButton").objectReferenceValue = parts.Previous;
                so.FindProperty("nextButton").objectReferenceValue = parts.Next;
                so.FindProperty("previousHalf").objectReferenceValue = parts.PreviousHalf;
                so.FindProperty("nextHalf").objectReferenceValue = parts.NextHalf;
                so.FindProperty("closeButton").objectReferenceValue = parts.Close;
                so.FindProperty("moreButton").objectReferenceValue = parts.Search;
                so.FindProperty("bookmarkButton").objectReferenceValue = parts.Bookmark;
                so.FindProperty("bookmarkLabel").objectReferenceValue = parts.BookmarkLabel;
                so.FindProperty("pinButton").objectReferenceValue = parts.Pin;
                so.FindProperty("pinLabel").objectReferenceValue = parts.PinLabel;
                so.FindProperty("pageCountLabel").objectReferenceValue = parts.Count;
                so.FindProperty("tabColumn").objectReferenceValue = parts.Tabs;
                AssignArray(so.FindProperty("tabButtons"), parts.TabButtons);
                AssignArray(so.FindProperty("tabLabels"), parts.TabLabels);
                AssignArray(so.FindProperty("tabFills"), parts.TabFills);
                so.FindProperty("tabIdle").colorValue = TabIdle;
                so.FindProperty("tabOn").colorValue = Cyan;
                so.FindProperty("tabHouse").colorValue = TabHouse;
                so.FindProperty("pinnedCanvas").objectReferenceValue = pin.Canvas;
                so.FindProperty("pinnedEyebrow").objectReferenceValue = pin.Eyebrow;
                so.FindProperty("pinnedHeading").objectReferenceValue = pin.Heading;
                so.FindProperty("pinnedBody").objectReferenceValue = pin.Body;
                so.FindProperty("unpinButton").objectReferenceValue = pin.Unpin;
                so.FindProperty("readingFill").floatValue = 0.76f; // the board: the open book is about 80% of the screen tall
                so.FindProperty("readingTilt").floatValue = 4f;
                so.ApplyModifiedPropertiesWithoutUndo();
                parts.Reading.SetActive(false);
                pin.Canvas.SetActive(false);
                GameObject made = PrefabUtility.SaveAsPrefabAsset(root, BookPath);
                if (made != null) AssetDatabase.SetLabels(made, new[] { BookLabel });
                Debug.Log($"[BOOK-1] Made the table rulebook ({BookPath}).");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Vector2 PageSize() =>
            new Vector2((BookWidth - 0.1f) / CanvasUnit, (BookDepth - 0.12f) / CanvasUnit);

        private static Material BookMaterial(string name, Color colour, float smoothness)
        {
            string path = BookFolder + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            material.color = colour;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // A box part of the book: the cube mesh without its own collider (the root's collider takes the clicks).
        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<BoxCollider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }

        // A canvas lying on the book: one canvas pixel is CanvasUnit book units.
        private static RectTransform WorldCanvas(string name, Transform parent, Vector3 position, Quaternion rotation,
            Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;
            rect.sizeDelta = size;
            rect.localPosition = position;
            rect.localRotation = rotation;
            rect.localScale = Vector3.one * CanvasUnit;
            return rect;
        }

        private sealed class PageSide
        {
            public Text Eyebrow;
            public Text Heading;
            public Text Intro;
            public RectTransform Steps;
            public GameObject RowTemplate;
            public Text Number;
            public GameObject Ribbon;
        }

        private static void AssignSide(SerializedObject so, string name, PageSide side)
        {
            so.FindProperty(name + ".Eyebrow").objectReferenceValue = side.Eyebrow;
            so.FindProperty(name + ".Heading").objectReferenceValue = side.Heading;
            so.FindProperty(name + ".Intro").objectReferenceValue = side.Intro;
            so.FindProperty(name + ".Steps").objectReferenceValue = side.Steps;
            so.FindProperty(name + ".RowTemplate").objectReferenceValue = side.RowTemplate;
            so.FindProperty(name + ".Number").objectReferenceValue = side.Number;
            so.FindProperty(name + ".Ribbon").objectReferenceValue = side.Ribbon;
        }

        private static void AssignArray<T>(SerializedProperty property, T[] items) where T : Object
        {
            property.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // A page as on the board (a page is 440 board pixels wide, 2.5 canvas pixels each): "GAME · RULEBOOK" in
        // orange, the title, a line under it, then the steps (a dark numbered disc, the rule with its first words in
        // bold, a rule under each; a changed rule: a tinted box with an orange edge and HOUSE RULE AT THIS TABLE);
        // the page number in the bottom corner. The right page carries the bookmark ribbon.
        private static PageSide PageText(RectTransform page, bool rightSide, Sprite ribbon)
        {
            PageSide side = new PageSide();
            RectTransform content = Rect("Content", page, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            Column(content.gameObject, 90, 90, 90, 150, 22f);
            side.Steps = content;
            side.Eyebrow = Label(Child("Eyebrow", content), bold, 27, BookEyebrow, TextAnchor.UpperLeft, "TRAP FLOOR · RULEBOOK");
            side.Heading = Label(Child("Heading", content), bold, 74, Ink, TextAnchor.UpperLeft, "Your turn");
            side.Heading.lineSpacing = 0.9f;
            side.Intro = Label(Child("Intro", content), medium, 32, InkSoft, TextAnchor.UpperLeft, "Three steps, in order.");
            side.Intro.lineSpacing = 1.15f;

            RectTransform row = Child("RowTemplate", content);
            Image back = Shape(row, HouseFill, null);
            back.enabled = false;
            Row(row.gameObject, 28, 25, 28, 28, 30f, TextAnchor.UpperLeft);
            RectTransform bar = Rect("Bar", row, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(9f, 0f));
            Shape(bar, Orange, null);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RectTransform line = Rect("Line", row, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(0f, 3f));
            Shape(line, PaperLine, null);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RectTransform badge = Child("Badge", row);
            Shape(badge, Ink, circle);
            Size(badge.gameObject, 64f, 64f, 64f);
            Label(Rect("Number", badge, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), bold,
                30, Paper, TextAnchor.MiddleCenter, "1");
            RectTransform words = Child("Words", row);
            Column(words.gameObject, 0, 0, 6, 0, 8f);
            Size(words.gameObject, -1f, -1f).flexibleWidth = 1f;
            Label(Child("Tag", words), bold, 26, BookEyebrow, TextAnchor.UpperLeft, "HOUSE RULE AT THIS TABLE");
            Text body = Label(Child("Body", words), medium, 36, Ink, TextAnchor.UpperLeft, "<b>Draw.</b> Draw 5 cards.");
            body.lineSpacing = 1.1f;
            side.RowTemplate = row.gameObject;

            side.Number = Label(Rect("Number", page, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 60f), new Vector2(-180f, 44f)), medium, 28, InkSoft,
                rightSide ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, "4");
            if (rightSide)
            {
                RectTransform mark = Rect("Ribbon", page, Vector2.one, Vector2.one, new Vector2(1f, 1f), new Vector2(-95f, 40f),
                    new Vector2(55f, 175f));
                Shape(mark, Hex("f26d2b"), ribbon);
                mark.gameObject.SetActive(false);
                side.Ribbon = mark.gameObject;
            }

            return side;
        }

        private sealed class ReadingParts
        {
            public GameObject Reading;
            public Button Previous;
            public Button Next;
            public Button PreviousHalf;
            public Button NextHalf;
            public Button Close;
            public Button Search;
            public Button Bookmark;
            public Text BookmarkLabel;
            public Button Pin;
            public Text PinLabel;
            public Text Count;
            public RectTransform Tabs;
            public Button[] TabButtons;
            public Text[] TabLabels;
            public Image[] TabFills;
        }

        private sealed class PinnedParts
        {
            public GameObject Canvas;
            public Text Eyebrow;
            public Text Heading;
            public Text Body;
            public Button Unpin;
        }

        private static RectTransform ScreenCanvas(Transform root, string name, int order)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root, false);
            go.layer = 5;
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go.GetComponent<RectTransform>();
        }

        // While reading (the board, x1.5): the screen halves turn the pages; the section tabs on the book's right edge
        // (placed there each frame); under the book ‹ › Pages 4–5 of 12 on the left, Search the rules…, BOOKMARK,
        // PIN PAGE and CLOSE (R) on the right.
        private static ReadingParts BuildReadingBar(Transform root)
        {
            ReadingParts parts = new ReadingParts();
            RectTransform rect = ScreenCanvas(root, "Reading", 500);
            parts.Reading = rect.gameObject;

            parts.PreviousHalf = Half(rect, "PreviousHalf", 0f, 0.5f);
            parts.NextHalf = Half(rect, "NextHalf", 0.5f, 1f);

            const int TabCount = 8;
            parts.Tabs = Rect("Tabs", rect, Vector2.zero, Vector2.zero, new Vector2(0f, 1f), Vector2.zero,
                new Vector2(54f, TabCount * 88f));
            VerticalLayoutGroup tabLayout = Column(parts.Tabs.gameObject, 0, 0, 0, 0, 4f);
            tabLayout.childForceExpandWidth = true;
            parts.TabButtons = new Button[TabCount];
            parts.TabLabels = new Text[TabCount];
            parts.TabFills = new Image[TabCount];
            for (int i = 0; i < TabCount; i++)
            {
                RectTransform tab = Child($"Tab{i + 1}", parts.Tabs);
                Image fill = Shape7(tab, TabIdle, round);
                fill.raycastTarget = true;
                Size(tab.gameObject, 54f, 84f);
                Button button = tab.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                ColorBlock colours = button.colors;
                colours.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
                button.colors = colours;
                RectTransform labelRect = Centred("Label", tab, 80f, 50f);
                labelRect.localRotation = Quaternion.Euler(0f, 0f, -90f);
                Text label = Label(labelRect, bold, 15, Ink, TextAnchor.MiddleCenter, "SECTION");
                label.lineSpacing = 0.85f;
                parts.TabButtons[i] = button;
                parts.TabLabels[i] = label;
                parts.TabFills[i] = fill;
            }

            RectTransform bar = Rect("Bar", rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 36f), new Vector2(1350f, 54f));
            Row(bar.gameObject, 0, 0, 0, 0, 12f);
            parts.Previous = BarButton(bar, "Previous", "‹", 48f, 51f, Edge, out _);
            parts.Next = BarButton(bar, "Next", "›", 48f, 51f, Edge, out _);
            parts.Count = FitLabel(Child("Pages", bar), medium, 19, Soft, "Pages 4–5 of 12");
            Size(Child("Spring", bar).gameObject, -1f, -1f).flexibleWidth = 1f;

            RectTransform search = Child("Search", bar);
            Image searchFill = Fill(search, FieldFill, round, Edge, 1.5f);
            Button searchButton = search.gameObject.AddComponent<Button>();
            searchButton.targetGraphic = searchFill;
            Size(search.gameObject, 330f, 51f, 330f);
            Label(Rect("Placeholder", search, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-34f, 0f)), medium, 19, Muted, TextAnchor.MiddleLeft, "Search the rules…");
            parts.Search = searchButton;
            parts.Bookmark = BarButton(bar, "Bookmark", "BOOKMARK", 189f, 51f, Edge, out parts.BookmarkLabel);
            parts.Pin = BarButton(bar, "Pin", "PIN PAGE", 132f, 51f, Edge, out parts.PinLabel);

            RectTransform close = Child("Close", bar);
            Image closeFill = Fill(close, BarFill, round, Cyan, 1.5f);
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeFill;
            Row(close.gameObject, 18, 9, 0, 0, 12f, TextAnchor.MiddleCenter);
            Size(close.gameObject, 144f, 54f, 144f);
            FitLabel(Child("Label", close), bold, 18, Words, "CLOSE").alignment = TextAnchor.MiddleCenter;
            RectTransform chip = Child("Key", close);
            Fill(chip, KeyFill, round, KeyEdge, 1.5f).raycastTarget = false;
            Size(chip.gameObject, 30f, 30f, 30f);
            Label(Rect("Label", chip, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), bold, 15,
                Words, TextAnchor.MiddleCenter, "R");
            parts.Close = closeButton;
            return parts;
        }

        private static Button BarButton(RectTransform bar, string name, string text, float width, float height, Color edge,
            out Text label)
        {
            RectTransform rect = Child(name, bar);
            Image fill = Fill(rect, BarFill, round, edge, 1.5f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            SetDisabledTint(button);
            Size(rect.gameObject, width, height, width);
            label = Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                bold, text.Length > 1 ? 18 : 24, Words, TextAnchor.MiddleCenter, text);
            return button;
        }

        // PIN PAGE: a paper card at the screen's lower left (over the Rulebook and Toolbox buttons) with the page's title
        // and steps, × takes it away.
        private static PinnedParts BuildPinned(Transform root)
        {
            PinnedParts parts = new PinnedParts();
            RectTransform rect = ScreenCanvas(root, "Pinned", 15);
            parts.Canvas = rect.gameObject;
            RectTransform card = Rect("Card", rect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(36f, 140f),
                new Vector2(420f, 200f));
            Fill(card, Paper, round, DeepTeal, 4f).raycastTarget = true;
            Column(card.gameObject, 24, 18, 14, 22, 6f);
            Fit(card.gameObject, false, true);
            RectTransform top = Child("Top", card);
            Row(top.gameObject, 0, 0, 0, 0, 10f);
            parts.Eyebrow = FitLabel(Child("Eyebrow", top), bold, 16, BookEyebrow, "PINNED · PAGE 4");
            Size(parts.Eyebrow.gameObject, -1f, -1f).flexibleWidth = 1f;
            parts.Unpin = BarButton(top, "Unpin", "×", 34f, 34f, Edge, out Text cross);
            cross.fontSize = 22;
            parts.Heading = Label(Child("Heading", card), bold, 28, Ink, TextAnchor.UpperLeft, "Your turn");
            parts.Body = Label(Child("Body", card), medium, 18, Ink, TextAnchor.UpperLeft, "<b>1</b>  Draw 5 cards.");
            parts.Body.lineSpacing = 1.15f;
            return parts;
        }

        private static Button Half(RectTransform parent, string name, float from, float to)
        {
            RectTransform rect = Rect(name, parent, new Vector2(from, 0f), new Vector2(to, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.001f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }
    }
}
