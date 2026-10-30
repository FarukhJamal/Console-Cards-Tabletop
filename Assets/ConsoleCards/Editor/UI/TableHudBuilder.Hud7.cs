using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;
using B = ConsoleCards.Editor.UI.BoardScreensBuilder;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// HUD-B to design board 7B "The table HUD · colour B" (owner, 2026-10-30: "everything the same as the reference"),
    /// measured from the board (1280 x 720, x1.5):
    /// <list type="bullet">
    /// <item>Top left: the hex logo (the game menu) over the strip: ROUND n | whose turn, the turn pips (the turn
    /// player's with a ring) and the striped cap.</item>
    /// <item>Under it the one-line hint: DRAW PHASE, the step, 1/3 (a press shows the next step) and ×.</item>
    /// <item>Right edge, the rail: Select and move (V), Move the camera (Space drag), Ping (P), Measure (M) | Undo,
    /// Redo (and the Shop while its rule is on) | the game menu. Log (L), Help and Settings are in the game menu;
    /// their buttons are kept, hidden.</item>
    /// <item>Bottom left: the Rulebook (R) and Toolbox (B) with their key chips; bottom middle, above the hand, the
    /// turn player's chip (its edge in their colour, the state as a yellow tag); bottom right END TURN (Enter).</item>
    /// </list>
    /// The pictures (Content/UI/Board/Hud) are the board's own icons. A HUD made before board 7B is replaced once
    /// (it is the builder's own); after that it is kept (edit it freely).
    /// </summary>
    public static partial class TableHudBuilder
    {
        private const string HudLabel = "HudBoard7B4";
        private const string HudArt = "Assets/ConsoleCards/Content/UI/Board/Hud/";
        private const float K = 1.5f; // board pixels to the 1920 x 1080 canvas

        private static readonly Color Glass = new Color(0.078f, 0.094f, 0.098f, 0.82f);
        private static readonly Color GlassStrong = new Color(0.078f, 0.094f, 0.098f, 0.9f);
        private static readonly Color HoverFill = Hex("1c3c3d");
        private static readonly Color HoverEdge = Hex("01f7f9");
        private static readonly Color KeyFill = Hex("2b3033");
        private static readonly Color KeyEdge = Hex("424547");
        private static readonly Color HintWords = Hex("f4fefe");
        private static readonly Color HintSoft = Hex("bff6f6");
        private static readonly Color StatusYellow = Hex("ffb21e");
        private static readonly Color EndTurnEdge = Hex("ffb07a");

        private static Sprite hex;
        private static Sprite pill;
        private static Sprite ring;
        private static Sprite round;
        private static Sprite frame;
        private static Sprite pillFrame;

        private static readonly string[] HudIcons =
        {
            "HudSelect", "HudPan", "HudPing", "HudMeasure", "HudUndo", "HudRedo", "HudMenu", "HudBook", "HudToolbox",
        };

        private static void BuildHudBoard()
        {
            TableHudView existing = AssetDatabase.LoadAssetAtPath<TableHudView>(HudPath);
            if (existing != null && System.Array.IndexOf(AssetDatabase.GetLabels(existing.gameObject), HudLabel) < 0)
            {
                AssetDatabase.DeleteAsset(HudPath); // made before board 7B (by this builder): built again
            }

            if (File.Exists(HudPath)) return;
            B.Prepare();
            hex = HudSprite("HudHex", Vector4.zero);
            pill = HudSprite("HudPill", new Vector4(31f, 31f, 31f, 31f));
            ring = HudSprite("HudRing", Vector4.zero);
            round = HudSprite("HudRound", new Vector4(16f, 16f, 16f, 16f));
            frame = HudSprite("HudFrame", new Vector4(16f, 16f, 16f, 16f));
            pillFrame = HudSprite("HudPillFrame", new Vector4(31f, 31f, 31f, 31f));
            if (round == null) round = rounded;
            foreach (string icon in HudIcons) HudSprite(icon, Vector4.zero);
            Save<TableHudView>(HudPath, "TableHud", BuildHud7);
            GameObject made = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (made != null) AssetDatabase.SetLabels(made, new[] { HudLabel });
        }

        // News at the top centre (boards 7B, 14v2): a dark pill with a coloured dot and one line; it fades by itself.
        private static void BuildNews7(RectTransform root, SerializedObject so)
        {
            RectTransform news = Rect("News", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -14f * K), new Vector2(200f, 30f * K));
            Fill(news, GlassStrong, pill != null ? pill : round, Edge, 1.5f).raycastTarget = false;
            HorizontalLayoutGroup row = Row(news.gameObject, 18, 18, 0, 0, 12f, TextAnchor.MiddleCenter);
            row.childAlignment = TextAnchor.MiddleCenter;
            Fit(news.gameObject, true, false);
            RectTransform dot = Child("Dot", news);
            Image dotImage = Shape(dot, Hex("2f7bff"), circle);
            Size(dot.gameObject, 12f, 12f, 12f);
            Text label = FitLabel(Child("Label", news), medium, 19, Words, "Blue found a Key · 2 of 3");
            CanvasGroup group = news.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            news.gameObject.SetActive(false);
            so.FindProperty("newsGroup").objectReferenceValue = group;
            so.FindProperty("newsLabel").objectReferenceValue = label;
            so.FindProperty("newsDot").objectReferenceValue = dotImage;
        }

        private static Sprite HudSprite(string name, Vector4 border)
        {
            string path = HudArt + name + ".png";
            if (!File.Exists(path)) return null;
            if (AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) == null) return null;
            ConfigureTexture(path, border);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BuildHud7(RectTransform root, TableHudView view)
        {
            // ---------- top left: the strip, then the hex logo over its left end ----------
            RectTransform strip = Rect("Status", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(52f * K, -18f * K), new Vector2(300f, 40f * K));
            Fill(strip, GlassStrong, round, Edge, 1.5f);
            Row(strip.gameObject, 45, 15, 0, 0, 18f).childAlignment = TextAnchor.MiddleLeft;
            Fit(strip.gameObject, true, false);

            RectTransform roundGroup = Child("Round", strip);
            Row(roundGroup.gameObject, 0, 0, 0, 0, 9f).childAlignment = TextAnchor.MiddleLeft;
            Text roundWord = FitLabel(Child("Word", roundGroup), bold, 18, Orange, "ROUND");
            Text roundNumber = FitLabel(Child("Number", roundGroup), bold, 33, Orange, "3");
            RectTransform divider = Child("Divider", strip);
            Shape(divider, Edge, null);
            Size(divider.gameObject, 2f, 30f, 2f);
            Text turn = FitLabel(Child("Turn", strip), bold, 18, Words, "YOUR TURN");
            RectTransform pipsRow = Child("Pips", strip);
            Row(pipsRow.gameObject, 4, 4, 0, 0, 9f).childAlignment = TextAnchor.MiddleCenter;
            Image[] pips = new Image[6];
            for (int i = 0; i < pips.Length; i++)
            {
                RectTransform pip = Child("Pip" + i, pipsRow);
                pips[i] = Shape(pip, Words, circle);
                Size(pip.gameObject, 15f, 15f, 15f);
                if (ring != null)
                {
                    // The turn player's ring (shown by the view): a gap, then a ring in their colour.
                    RectTransform pipRing = Rect("Ring", pip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
                    Shape(pipRing, Words, ring);
                    pipRing.gameObject.SetActive(false);
                }
            }

            Text game = FitLabel(Child("Game", strip), bold, 18, Words, "EMPTY TABLE");
            RectTransform cap = Child("Cap", strip);
            Image capImage = cap.gameObject.AddComponent<Image>();
            capImage.sprite = B.Stripes;
            capImage.type = B.Stripes != null ? Image.Type.Tiled : Image.Type.Simple;
            capImage.color = B.Stripes != null ? Color.white : Orange;
            capImage.raycastTarget = false;
            Size(cap.gameObject, 12f, 39f, 12f);

            RectTransform logoRect = Rect("Logo", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f * K, -10f * K), new Vector2(62f * K, 56f * K));
            Image logoOuter = Shape(logoRect, Orange, hex != null ? hex : rounded);
            logoOuter.raycastTarget = true;
            Button logo = logoRect.gameObject.AddComponent<Button>();
            logo.targetGraphic = logoOuter;
            Shape(Centred("Inner", logoRect, 54f * K, 48f * K), DeepTeal, hex != null ? hex : rounded);
            Shape(Centred("Mark", logoRect, 40f * K, 36f * K), Cyan, hex != null ? hex : rounded);
            Label(Centred("Letters", logoRect, 40f * K, 36f * K), bold, 20, DeepTeal, TextAnchor.MiddleCenter, "CC");
            Tip(logoRect, "Game menu · Esc", false);

            // ---------- the one-line hint ----------
            RectTransform hint = Rect("Hint", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f * K, -74f * K), new Vector2(560f, 26f * K));
            Image hintFill = Fill(hint, Teal, round, Cyan, 1.5f);
            hintFill.raycastTarget = true;
            Button hintNext = hint.gameObject.AddComponent<Button>();
            hintNext.targetGraphic = hintFill;
            hintNext.transition = Selectable.Transition.None;
            Row(hint.gameObject, 5, 6, 0, 0, 12f).childAlignment = TextAnchor.MiddleLeft;
            Fit(hint.gameObject, true, false);
            RectTransform tagRect = Child("Tag", hint);
            Image hintTagFill = Shape7(tagRect, DarkTeal, round);
            Row(tagRect.gameObject, 11, 11, 0, 0, 0f, TextAnchor.MiddleCenter);
            Size(tagRect.gameObject, -1f, 30f);
            Text hintTag = FitLabel(Child("Label", tagRect), bold, 15, Cyan, "DRAW PHASE");
            hintTag.alignment = TextAnchor.MiddleCenter;
            Text hintText = FitLabel(Child("Text", hint), medium, 18, HintWords, "Draw 5 cards from the Controller deck");
            Text hintCount = FitLabel(Child("Count", hint), medium, 16, HintSoft, "1/3");
            Button gotIt = SmallButton(hint, "GotIt", "GOT IT", DarkTeal, Cyan, 74f, 28f, out _);
            Button hintClose = SmallButton(hint, "Close", "×", new Color(0f, 0f, 0f, 0f), HintSoft, 30f, 30f, out Text closeX);
            closeX.fontSize = 24;
            Tip((RectTransform)hintClose.transform, "Hide the hint", false);

            // ---------- the rail ----------
            RectTransform rail = Rect("Rail", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-14f * K, -14f * K), new Vector2(72f, 400f));
            Fill(rail, Glass, round, Edge, 1.5f);
            VerticalLayoutGroup railColumn = Column(rail.gameObject, 8, 8, 8, 8, 6f, TextAnchor.UpperCenter);
            railColumn.childForceExpandWidth = false;
            Fit(rail.gameObject, true, true);
            TableHudView.RailTool[] tools =
            {
                Rail7Tool(rail, TableToolKind.Grab, "HudSelect", "V", "Select and move · V"),
                Rail7Tool(rail, TableToolKind.Pan, "HudPan", "+", "Move the camera · Space drag"),
                Rail7Tool(rail, TableToolKind.Ping, "HudPing", "P", "Ping · P"),
                Rail7Tool(rail, TableToolKind.Measure, "HudMeasure", "M", "Measure · M"),
            };
            Rail7Rule(rail, "Rule1");
            Button undo = Rail7Button(rail, "Undo", "HudUndo", "<", "Undo · Ctrl Z", out Image undoIcon, out _);
            Button redo = Rail7Button(rail, "Redo", "HudRedo", ">", "Redo · Ctrl Y", out Image redoIcon, out _);
            Button shop = Rail7Button(rail, "Shop", null, "$", "Shop", out _, out _);
            Rail7Rule(rail, "Rule2");
            Button menu = Rail7Button(rail, "Menu", "HudMenu", "=", "Game menu · Esc", out _, out _);

            // Not on board 7B (in the game menu, or a key): kept for the view, hidden.
            RectTransform hidden = Rect("NotOnBoard", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(72f, 72f));
            hidden.gameObject.SetActive(false);
            Button log = Rail7Button(hidden, "Log", null, "L", "What happened · L", out _, out _);
            Button help = Rail7Button(hidden, "Help", null, "?", "Help", out _, out _);
            Button settings = Rail7Button(hidden, "Settings", null, "*", "Settings", out _, out _);
            Button handStyle = Rail7Button(hidden, "HandStyle", null, "H", "Hand: fan / row", out _, out _);

            // ---------- bottom left: the Rulebook and the Toolbox ----------
            Button rulebook = Corner7(root, "Rulebook", 16f * K, "HudBook", Cyan, "R", "Rulebook · R");
            Button toolbox = Corner7(root, "Toolbox", 72f * K, "HudToolbox", Orange, "B", "Toolbox · B");

            // ---------- top right, beside the rail: the turn player's chip (owner, 2026-10-30: "move it to the right
            // side alongside the right toolbar"; it grows to the left) ----------
            RectTransform chip = Rect("Player", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-((14f * K) + 72f + 12f), -14f * K), new Vector2(300f, 26f * K));
            Fill(chip, new Color(0.078f, 0.094f, 0.098f, 0.85f), pill != null ? pill : round, Hex("ff3b3b"), 1.5f);
            Graphic playerEdge = chip.Find("Edge")?.GetComponent<Graphic>();
            Row(chip.gameObject, 15, 9, 0, 0, 12f).childAlignment = TextAnchor.MiddleLeft;
            Fit(chip.gameObject, true, false);
            RectTransform dotRect = Child("Dot", chip);
            Image dot = Shape(dotRect, Hex("ff3b3b"), circle);
            Size(dotRect.gameObject, 15f, 15f, 15f);
            Text playerText = FitLabel(Child("Text", chip), medium, 18, Words,
                "<b>YOU</b>  <color=#9BA4A7>· Keys</color>  <b>1/3</b>");
            RectTransform statusTag = Child("State", chip);
            Shape7(statusTag, StatusYellow, round);
            Row(statusTag.gameObject, 9, 9, 0, 0, 0f, TextAnchor.MiddleCenter);
            Size(statusTag.gameObject, -1f, 27f);
            Text statusText = FitLabel(Child("Label", statusTag), bold, 15, OrangeInk, "OVERCHARGED +1");
            statusText.alignment = TextAnchor.MiddleCenter;
            statusTag.gameObject.SetActive(false);

            // ---------- bottom right: the turn's other actions over END TURN (Enter) ----------
            RectTransform turnArea = Rect("TurnActions", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-16f * K, 16f * K), new Vector2(260f, 200f));
            VerticalLayoutGroup turnColumn = Column(turnArea.gameObject, 0, 0, 0, 0, 9f, TextAnchor.LowerRight);
            turnColumn.childForceExpandWidth = false;
            Fit(turnArea.gameObject, true, true);
            RectTransform actionsRoot = Child("Others", turnArea);
            VerticalLayoutGroup othersColumn = Column(actionsRoot.gameObject, 0, 0, 0, 0, 6f, TextAnchor.LowerRight);
            othersColumn.childForceExpandWidth = false;
            Button template = Action7Chip(actionsRoot);
            Button primary = EndTurn7(turnArea, out Text primaryLabel);

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("logoButton").objectReferenceValue = logo;
            so.FindProperty("roundGroup").objectReferenceValue = roundGroup.gameObject;
            so.FindProperty("roundWord").objectReferenceValue = roundWord;
            so.FindProperty("roundNumber").objectReferenceValue = roundNumber;
            so.FindProperty("turnLabel").objectReferenceValue = turn;
            SerializedProperty pipsProperty = so.FindProperty("pips");
            pipsProperty.arraySize = pips.Length;
            for (int i = 0; i < pips.Length; i++) pipsProperty.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
            so.FindProperty("activePipScale").floatValue = 1.3f;
            so.FindProperty("gameLabel").objectReferenceValue = game;
            so.FindProperty("hintBar").objectReferenceValue = hint.gameObject;
            so.FindProperty("hintFill").objectReferenceValue = hintFill;
            so.FindProperty("hintTagFill").objectReferenceValue = hintTagFill;
            so.FindProperty("hintTag").objectReferenceValue = hintTag;
            so.FindProperty("hintText").objectReferenceValue = hintText;
            so.FindProperty("hintGotIt").objectReferenceValue = gotIt;
            so.FindProperty("hintClose").objectReferenceValue = hintClose;
            so.FindProperty("hintCount").objectReferenceValue = hintCount;
            so.FindProperty("hintNext").objectReferenceValue = hintNext;
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
            so.FindProperty("playerEdge").objectReferenceValue = playerEdge;
            so.FindProperty("playerStatusTag").objectReferenceValue = statusTag.gameObject;
            so.FindProperty("playerStatusText").objectReferenceValue = statusText;
            BuildNews7(root, so);
            so.FindProperty("actionsRoot").objectReferenceValue = actionsRoot;
            so.FindProperty("actionTemplate").objectReferenceValue = template;
            so.FindProperty("primaryButton").objectReferenceValue = primary;
            so.FindProperty("primaryLabel").objectReferenceValue = primaryLabel;
            so.FindProperty("railIdleIcon").colorValue = Soft;
            so.FindProperty("railActiveIcon").colorValue = OrangeInk;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- board 7B parts ----------

        // The object's own fill (it takes the clicks) and, when given, its edge: a frame picture on top for a
        // see-through fill (an Outline would show through it), two Outlines under a solid one.
        private static Image Fill(RectTransform rect, Color colour, Sprite sprite, Color? edge, float width)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero || sprite == rounded ? Image.Type.Sliced : Image.Type.Simple;
            image.color = colour;
            image.raycastTarget = true;
            Sprite edgeSprite = sprite == pill && pill != null ? pillFrame : frame;
            if (edge.HasValue && colour.a < 0.99f && edgeSprite != null)
            {
                RectTransform edgeRect = Rect("Edge", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                    Vector2.zero);
                Image edgeImage = edgeRect.gameObject.AddComponent<Image>();
                edgeImage.sprite = edgeSprite;
                edgeImage.type = Image.Type.Sliced;
                edgeImage.color = edge.Value;
                edgeImage.raycastTarget = false;
                edgeRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            else if (edge.HasValue)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = edge.Value;
                outline.effectDistance = new Vector2(width, -width);
                outline.useGraphicAlpha = false;
                Outline other = rect.gameObject.AddComponent<Outline>();
                other.effectColor = edge.Value;
                other.effectDistance = new Vector2(-width, width);
                other.useGraphicAlpha = false;
            }

            return image;
        }

        // Shape, sliced for the 9-sliced board pictures too.
        private static Image Shape7(RectTransform rect, Color colour, Sprite sprite)
        {
            Image image = Shape(rect, colour, sprite);
            if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            return image;
        }

        private static RectTransform Centred(string name, RectTransform parent, float width, float height) =>
            Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(width, height));

        private static TableHudView.RailTool Rail7Tool(RectTransform rail, TableToolKind kind, string icon, string letter,
            string tip)
        {
            Button button = Rail7Button(rail, kind.ToString(), icon, letter, tip, out Image image, out Image fill);
            return new TableHudView.RailTool { kind = kind, button = button, fill = fill, icon = image };
        }

        // A 54 x 54 rail button: the chosen fill (orange, set by the view), the hover (dark teal with a cyan edge),
        // the board's icon (or a letter when the picture is missing) and its tip to the left.
        private static Button Rail7Button(RectTransform rail, string name, string icon, string letter, string tip,
            out Image iconImage, out Image fill)
        {
            RectTransform rect = Child(name, rail);
            Size(rect.gameObject, 54f, 54f, 54f);
            fill = Shape7(Rect("Fill", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero),
                new Color(0f, 0f, 0f, 0f), round);
            RectTransform hoverRect = Rect("Hover", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero);
            Image hover = Shape7(hoverRect, HoverFill, round);
            hover.raycastTarget = true;
            Outline hoverEdge = hoverRect.gameObject.AddComponent<Outline>();
            hoverEdge.effectColor = HoverEdge;
            hoverEdge.effectDistance = new Vector2(1.5f, -1.5f);
            hoverEdge.useGraphicAlpha = true; // the edge shows with the hover only
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hover;
            ColorBlock colours = button.colors;
            colours.normalColor = new Color(1f, 1f, 1f, 0f);
            colours.highlightedColor = Color.white;
            colours.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colours.selectedColor = new Color(1f, 1f, 1f, 0f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0f);
            button.colors = colours;
            Sprite picture = icon != null ? AssetDatabase.LoadAssetAtPath<Sprite>(HudArt + icon + ".png") : null;
            if (picture == null && name == "Shop") picture = AssetDatabase.LoadAssetAtPath<Sprite>(SessionIcons + "IconShop.png");
            RectTransform iconRect = Centred("Icon", rect, 27f, 27f);
            if (picture != null)
            {
                iconImage = iconRect.gameObject.AddComponent<Image>();
                iconImage.sprite = picture;
                iconImage.preserveAspect = true;
                iconImage.color = Soft;
                iconImage.raycastTarget = false;
            }
            else
            {
                iconImage = null;
                Label(iconRect, bold, 22, Soft, TextAnchor.MiddleCenter, letter);
            }

            Tip(rect, tip, true);
            return button;
        }

        private static void Rail7Rule(RectTransform rail, string name)
        {
            RectTransform rule = Child(name, rail);
            Image line = rule.gameObject.AddComponent<Image>();
            line.color = Edge;
            line.raycastTarget = false;
            Size(rule.gameObject, 33f, 2f, 33f);
        }

        // A 72 x 72 glass button at the bottom left, its edge and icon in its colour, its key in a chip on its corner.
        private static Button Corner7(RectTransform root, string name, float left, string icon, Color tone, string key,
            string tip)
        {
            RectTransform rect = Rect(name, root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(left, 16f * K),
                new Vector2(48f * K, 48f * K));
            Image fill = Fill(rect, Glass, round, tone, 1.5f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.8f, 1f, 1f, 1f);
            button.colors = colours;
            Sprite picture = AssetDatabase.LoadAssetAtPath<Sprite>(HudArt + icon + ".png");
            RectTransform iconRect = Centred("Icon", rect, 36f, 36f);
            if (picture != null)
            {
                Image image = iconRect.gameObject.AddComponent<Image>();
                image.sprite = picture;
                image.preserveAspect = true;
                image.color = tone;
                image.raycastTarget = false;
            }
            else
            {
                Label(iconRect, bold, 26, tone, TextAnchor.MiddleCenter, key);
            }

            RectTransform chip = Rect("Key", rect, Vector2.one, Vector2.one, Vector2.one, new Vector2(9f, 9f),
                new Vector2(27f, 27f));
            Image chipFill = Fill(chip, KeyFill, round, KeyEdge, 1.5f);
            chipFill.raycastTarget = false;
            Row(chip.gameObject, 8, 8, 0, 0, 0f, TextAnchor.MiddleCenter);
            Fit(chip.gameObject, true, false);
            FitLabel(Child("Label", chip), bold, 15, Words, key).alignment = TextAnchor.MiddleCenter;
            Tip(rect, tip, false);
            return button;
        }

        private static Button Action7Chip(RectTransform parent)
        {
            RectTransform rect = Child("ActionTemplate", parent);
            Image fill = Fill(rect, GlassStrong, pill != null ? pill : round, Edge, 1.5f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            Row(rect.gameObject, 18, 18, 0, 0, 0f, TextAnchor.MiddleCenter);
            Size(rect.gameObject, -1f, 42f);
            Text label = FitLabel(Child("Label", rect), bold, 15, Soft, "TAKE IT");
            label.alignment = TextAnchor.MiddleCenter;
            rect.gameObject.SetActive(false);
            return button;
        }

        // END TURN: an orange pill with its ENTER key chip.
        private static Button EndTurn7(RectTransform parent, out Text label)
        {
            RectTransform rect = Child("EndTurn", parent);
            Image fill = Fill(rect, Orange, pill != null ? pill : rounded, EndTurnEdge, 1.5f);
            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(0f, -9f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            SetDisabledTint(button);
            Row(rect.gameObject, 24, 12, 0, 0, 15f, TextAnchor.MiddleCenter);
            Size(rect.gameObject, -1f, 66f);
            label = FitLabel(Child("Label", rect), bold, 21, OrangeInk, "END TURN");
            RectTransform key = Child("Key", rect);
            Fill(key, OrangeInk, pill != null ? pill : rounded, null, 0f).raycastTarget = false;
            Row(key.gameObject, 12, 12, 0, 0, 0f, TextAnchor.MiddleCenter);
            Size(key.gameObject, -1f, 39f);
            FitLabel(Child("Label", key), bold, 15, Orange, "ENTER").alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
