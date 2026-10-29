using System.IO;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Owner, 2026-10-28: builds the title intro (Final/UI/TitleIntro.prefab, <see cref="TitleIntroView"/>) from the
    /// pictures in Content/UI/Intro/ and points the UI catalog at it. Like the other builders it only makes the
    /// prefab when it is missing; "Build Title Intro Again" overwrites it (and your edits to it).
    ///
    /// The art: Intro_Room.png (the whole scene, required), and when you have them Intro_Console.png and
    /// Intro_Cartridge.png: the console and the cartridge cut out on transparent pictures of the same size as the
    /// room, so they line up by themselves and can move on their own (parallax, the cartridge going in).
    /// The places below (console, slot, prompt) are fractions of the picture, measured on the concept art; move
    /// the Hotspot, Focus and Prompt objects in the prefab if new art puts the console somewhere else.
    /// </summary>
    public static class TitleIntroBuilder
    {
        public const string PrefabPath = UiKit.FinalUiFolder + "TitleIntro.prefab";
        public const string ArtPath = "Assets/ConsoleCards/Content/UI/Intro/";
        private const string FigmaPath = "Assets/ConsoleCards/Content/UI/Figma/Components/";

        // Measured on the concept picture (fractions from the bottom left).
        private static readonly Vector2 HotspotMin = new Vector2(0.392f, 0.453f);
        private static readonly Vector2 HotspotMax = new Vector2(0.589f, 0.649f);
        private static readonly Vector2 SlotPoint = new Vector2(0.4916f, 0.58f); // where the cartridge goes in
        private static readonly Vector2 PromptPoint = new Vector2(0.5f, 0.71f);

        private static readonly Color Cyan = UiKit.Hex("06EFF0");
        private static readonly Color Words = UiKit.Hex("E8ECED");
        private static readonly Color Muted = UiKit.Hex("9BA4A7");
        private static readonly Color KitOrange = UiKit.Hex("F2762E");

        [MenuItem("Console Cards/UI/Build Title Intro")]
        public static void BuildMenu() => Run(false);

        [MenuItem("Console Cards/UI/Build Title Intro Again (overwrites your intro edits)")]
        public static void BuildAgainMenu() => Run(true);

        private static void Run(bool again)
        {
            AssetDatabase.Refresh();
            if (!File.Exists(ArtPath + "Intro_Room.png"))
            {
                EditorUtility.DisplayDialog("Title intro", "Put the room picture at " + ArtPath + "Intro_Room.png first.", "OK");
                return;
            }

            ImportArt();
            bool exists = File.Exists(PrefabPath);
            if (!exists || again) Build();
            TitleIntroView view = AssetDatabase.LoadAssetAtPath<TitleIntroView>(PrefabPath);
            UiKit.AssignCatalogEntry(UiPrefabIds.TitleIntro, view, UiLayer.Notification, UiRetention.CachedSingleInstance);
            AssetDatabase.SaveAssets();
            Debug.Log(exists && !again
                ? "Title intro: the prefab was already there and is kept; the catalog points at it."
                : "Title intro: built " + PrefabPath + " and added it to the UI catalog.");
            Selection.activeObject = view;
        }

        private static void ImportArt()
        {
            Picture("Intro_Room.png", TextureWrapMode.Clamp, FilterMode.Bilinear, 2048);
            Picture("Intro_Console.png", TextureWrapMode.Clamp, FilterMode.Bilinear, 2048);
            Picture("Intro_Cartridge.png", TextureWrapMode.Clamp, FilterMode.Bilinear, 2048);
            Picture("Intro_Glow.png", TextureWrapMode.Clamp, FilterMode.Bilinear, 512);
            Picture("Intro_Vignette.png", TextureWrapMode.Clamp, FilterMode.Bilinear, 1024);
            Picture("Intro_Streaks.png", TextureWrapMode.Clamp, FilterMode.Bilinear, 1024);
            Picture("Intro_Mote.png", TextureWrapMode.Clamp, FilterMode.Bilinear, 32);
            Picture("Intro_Scanlines.png", TextureWrapMode.Repeat, FilterMode.Point, 32);
        }

        private static void Picture(string file, TextureWrapMode wrap, FilterMode filter, int maxSize)
        {
            string path = ArtPath + file;
            if (!File.Exists(path)) return;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency
                || importer.wrapMode != wrap
                || importer.filterMode != filter
                || importer.maxTextureSize != maxSize
                || importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.filterMode = filter;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static Sprite Art(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + file);

        private static void Build()
        {
            Font bold = UiKit.RequireFont("Oxanium-Bold.ttf");
            Font medium = UiKit.RequireFont("Oxanium-Medium.ttf");
            Sprite room = Art("Intro_Room.png");
            Sprite console = Art("Intro_Console.png");
            Sprite cartridge = Art("Intro_Cartridge.png");
            float aspect = room != null ? room.rect.width / room.rect.height : 16f / 9f;

            GameObject go = new GameObject("TitleIntro", typeof(RectTransform));
            go.layer = 5;
            try
            {
                RectTransform root = (RectTransform)go.transform;
                UiKit.Stretch(root);
                Canvas canvas = go.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32000; // over the front end, the HUD and every popup
                go.AddComponent<GraphicRaycaster>();
                AudioSource audio = go.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0f;
                TitleIntroView view = go.AddComponent<TitleIntroView>();

                Image catcher = Full("Catcher", root, null, Color.clear);
                catcher.raycastTarget = true;

                // The "camera": everything in Stage is scaled and moved for the dive.
                RectTransform stage = Stretched("Stage", root);
                RectTransform frame = UiKit.Rect("Frame", stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1920f / aspect));
                AspectRatioFitter fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = aspect;
                frame.localScale = Vector3.one * 1.03f; // a little extra picture at the edges for the parallax

                RectTransform roomLayer = Full("Room", frame, room, Color.white).rectTransform;
                Image glow = Placed("Glow", frame, SlotPoint, new Vector2(560f, 210f), Art("Intro_Glow.png"), Cyan);
                RectTransform consoleLayer = console != null ? Full("Console", frame, console, Color.white).rectTransform : null;
                RectTransform cartridgeLayer = cartridge != null ? Full("Cartridge", frame, cartridge, Color.white).rectTransform : null;

                RectTransform hotspot = UiKit.Rect("Hotspot", frame, HotspotMin, HotspotMax, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);
                Image hotspotImage = hotspot.gameObject.AddComponent<Image>();
                hotspotImage.color = Color.clear;
                Target(hotspot.gameObject, view);

                RectTransform focus = UiKit.Rect("Focus", frame, SlotPoint, SlotPoint, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);

                // ENTER over the console, in the kit's words: orange end caps, cyan when the console is hovered.
                RectTransform promptRect = UiKit.Rect("Prompt", frame, PromptPoint, PromptPoint, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(460f, 120f));
                CanvasGroup prompt = promptRect.gameObject.AddComponent<CanvasGroup>();
                Image promptHit = promptRect.gameObject.AddComponent<Image>();
                promptHit.color = Color.clear;
                Target(promptRect.gameObject, view);
                RectTransform lift = Stretched("Lift", promptRect);
                Text label = UiKit.Label(UiKit.Rect("Label", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(320f, 64f)), bold, 46, Words,
                    TextAnchor.MiddleCenter, "E N T E R");
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                Shadow shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
                shadow.effectDistance = new Vector2(0f, -3f);
                Text left = UiKit.Label(UiKit.Rect("CapLeft", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(1f, 0.5f), new Vector2(-170f, 14f), new Vector2(60f, 40f)), bold, 22, KitOrange,
                    TextAnchor.MiddleRight, "///");
                left.horizontalOverflow = HorizontalWrapMode.Overflow;
                Text right = UiKit.Label(UiKit.Rect("CapRight", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(170f, 14f), new Vector2(60f, 40f)), bold, 22, KitOrange,
                    TextAnchor.MiddleLeft, "///");
                right.horizontalOverflow = HorizontalWrapMode.Overflow;
                RectTransform line = UiKit.Rect("Line", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(240f, 3f));
                Image lineImage = line.gameObject.AddComponent<Image>();
                lineImage.color = Cyan;
                lineImage.raycastTarget = false;
                Text sub = UiKit.Label(UiKit.Rect("Sub", lift, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(0f, -44f), new Vector2(400f, 26f)), medium, 15, Muted,
                    TextAnchor.MiddleCenter, "INSERT CARTRIDGE");
                sub.horizontalOverflow = HorizontalWrapMode.Overflow;

                // Dust in the lamp light (inside the stage, so it rushes past in the dive).
                RectTransform motes = Stretched("Motes", stage);
                Sprite mote = Art("Intro_Mote.png");
                var random = new System.Random(7);
                for (int i = 0; i < 26; i++)
                {
                    float size = 3f + (float)random.NextDouble() * 7f;
                    RectTransform dot = UiKit.Rect("Mote" + i, motes, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                    Image image = dot.gameObject.AddComponent<Image>();
                    image.sprite = mote;
                    image.color = new Color(1f, 0.86f, 0.7f, 0f);
                    image.raycastTarget = false;
                }

                Image vignette = Full("Vignette", root, Art("Intro_Vignette.png"), new Color(0f, 0f, 0f, 0.85f));
                // Square and wider than the screen, so it still covers it at its smallest (0.9) in the dive.
                Image streaks = Placed("Streaks", root, new Vector2(0.5f, 0.5f), new Vector2(2600f, 2600f),
                    Art("Intro_Streaks.png"), new Color(0.75f, 1f, 1f, 0f));

                // Corners: the game's name, the hint and EXIT.
                RectTransform hudRect = Stretched("Hud", root);
                CanvasGroup hud = hudRect.gameObject.AddComponent<CanvasGroup>();
                RectTransform brand = UiKit.Rect("Brand", hudRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(56f, -44f), new Vector2(520f, 40f));
                Image dotOn = UiKit.Rect("Dot", brand, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    Vector2.zero, new Vector2(14f, 14f)).gameObject.AddComponent<Image>();
                dotOn.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FigmaPath + "cyan_on ellipse.png");
                dotOn.color = dotOn.sprite != null ? Color.white : Cyan;
                dotOn.raycastTarget = false;
                Text name = UiKit.Label(UiKit.Rect("Name", brand, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
                    new Vector2(26f, 0f), new Vector2(-26f, 0f)), bold, 24, Words, TextAnchor.MiddleLeft,
                    "CONSOLE CARDS <color=#" + ColorUtility.ToHtmlStringRGB(KitOrange) + ">///</color>");
                name.horizontalOverflow = HorizontalWrapMode.Overflow;

                Text hint = UiKit.Label(UiKit.Rect("Hint", hudRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(900f, 30f)), medium, 16, Muted,
                    TextAnchor.MiddleCenter, "CLICK THE CONSOLE   ·   ENTER   ·   A / START");
                hint.horizontalOverflow = HorizontalWrapMode.Overflow;

                RectTransform exitRect = UiKit.Rect("Exit", hudRect, new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 0f), new Vector2(-48f, 36f), new Vector2(150f, 52f));
                Image exitPlate = exitRect.gameObject.AddComponent<Image>();
                exitPlate.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FigmaPath + "Button_disabled.png");
                exitPlate.type = exitPlate.sprite != null && exitPlate.sprite.border != Vector4.zero
                    ? Image.Type.Sliced : Image.Type.Simple;
                exitPlate.color = exitPlate.sprite != null ? Color.white : UiKit.Hex("1C1F1F");
                Button exit = exitRect.gameObject.AddComponent<Button>();
                exit.targetGraphic = exitPlate;
                UiKit.Label(Stretched("Label", exitRect), bold, 18, Words, TextAnchor.MiddleCenter, "EXIT");

                // The power-on: two black halves, the bright line between them, the light, the tube's lines.
                RectTransform shutterTop = Full("ShutterTop", root, null, Color.black).rectTransform;
                RectTransform shutterBottom = Full("ShutterBottom", root, null, Color.black).rectTransform;
                Image seam = Placed("Seam", root, new Vector2(0.5f, 0.5f), new Vector2(2400f, 80f), Art("Intro_Glow.png"),
                    new Color(0.85f, 1f, 1f, 0f));
                Image flash = Full("Flash", root, null, new Color(0.88f, 1f, 1f, 0f));
                Image scanlines = Full("Scanlines", root, Art("Intro_Scanlines.png"), new Color(0f, 0f, 0f, 0f));
                scanlines.type = Image.Type.Tiled;
                scanlines.pixelsPerUnitMultiplier = 1f;
                Image fade = Full("Fade", root, null, Color.black);

                SerializedObject so = new SerializedObject(view);
                Set(so, "stage", stage);
                Set(so, "frame", frame);
                Set(so, "roomLayer", roomLayer);
                Set(so, "consoleLayer", consoleLayer);
                Set(so, "cartridgeLayer", cartridgeLayer);
                Set(so, "glow", glow);
                Set(so, "focus", focus);
                Set(so, "motes", motes);
                Set(so, "hud", hud);
                Set(so, "prompt", prompt);
                Set(so, "promptLift", lift);
                Set(so, "promptLabel", label);
                Set(so, "promptLine", line);
                Set(so, "vignette", vignette);
                Set(so, "streaks", streaks);
                Set(so, "flash", flash);
                Set(so, "shutterTop", shutterTop);
                Set(so, "shutterBottom", shutterBottom);
                Set(so, "seam", seam);
                Set(so, "scanlines", scanlines);
                Set(so, "catcher", catcher);
                Set(so, "fade", fade);
                Set(so, "exitButton", exit);
                Set(so, "audioSource", audio);
                Set(so, "hoverClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ConsoleCards/Content/Resources/CardSounds/CardTap.wav"));
                Set(so, "insertClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ConsoleCards/Content/Resources/TableSounds/Chip1.wav"));
                Set(so, "powerClip", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ConsoleCards/Content/Resources/UiSounds/TablePing.wav"));
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void Target(GameObject go, TitleIntroView view)
        {
            TitleIntroTarget target = go.AddComponent<TitleIntroTarget>();
            SerializedObject so = new SerializedObject(target);
            so.FindProperty("view").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("Title intro: TitleIntroView has no field '" + field + "'.");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static RectTransform Stretched(string name, Transform parent)
        {
            RectTransform rect = UiKit.Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            UiKit.Stretch(rect);
            return rect;
        }

        private static Image Full(string name, Transform parent, Sprite sprite, Color color)
        {
            Image image = Stretched(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image Placed(string name, Transform parent, Vector2 anchor, Vector2 size, Sprite sprite, Color color)
        {
            RectTransform rect = UiKit.Rect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
