using System.Collections.Generic;
using System.IO;
using System.Text;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ConsoleCards.Editor.UI.UiKit;

namespace ConsoleCards.Editor.Content
{
    /// <summary>
    /// Console Cards > Content > Create Stand-in Card Fronts (owner's choices 2026-10-20): draws a PNG front for every
    /// card definition that has no front art (no picture and no art key): the card's name at the top, a coloured type
    /// band (TRAP, ABILITY, FRIEND…), a simple symbol for its type in the middle and its rules text at the bottom, on
    /// a background in the type's colour with soft rays and rounded corners, like the real fronts. The pictures go to
    /// Content/Art/StandIn and are listed in Resources/StandInCardFronts; the game shows one only where a card has no
    /// art of its own. A picture that already exists is kept (edit it freely); delete it to have it drawn again.
    /// Editor only: the text is laid out with the project's fonts in a hidden scene and rendered to the picture.
    /// </summary>
    public static class StandInCardFrontBuilder
    {
        private const string Folder = "Assets/ConsoleCards/Content/Art/StandIn/";
        private const string ListPath = "Assets/ConsoleCards/Content/Resources/StandInCardFronts.asset";
        private const int Width = 750; // half the size of the real fronts (1500 × 2100), same shape
        private const int Height = 1050;
        private const int CornerRadius = 48;

        private struct TypeLook
        {
            public string Name;
            public Color Colour;
            public int Symbol; // 0 circle, 1 warning, 2 bolt, 3 star
        }

        [MenuItem("Console Cards/Content/Create Stand-in Card Fronts")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Font title = RequireFont("Silkscreen-Bold.ttf");
            Font body = RequireFont("ChakraPetch-Medium.ttf");
            Font bold = RequireFont("ChakraPetch-Bold.ttf");

            List<StandInCardFronts.Entry> entries = new List<StandInCardFronts.Entry>();
            int made = 0;
            int kept = 0;
            string[] guids = AssetDatabase.FindAssets("t:CardDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                CardDefinition card = AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (card == null
                    || card.FrontArtwork != null
                    || !string.IsNullOrWhiteSpace(card.FrontArtworkReference)
                    || string.IsNullOrWhiteSpace(card.StableId))
                {
                    continue;
                }

                string path = Folder + FileName(card) + ".png";
                if (File.Exists(path))
                {
                    kept++;
                }
                else
                {
                    File.WriteAllBytes(path, Render(card, title, body, bold));
                    AssetDatabase.ImportAsset(path);
                    made++;
                }

                ConfigureImport(path);
                Texture2D front = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (front != null) entries.Add(new StandInCardFronts.Entry { stableId = card.StableId, front = front });
            }

            StandInCardFronts list = AssetDatabase.LoadAssetAtPath<StandInCardFronts>(ListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<StandInCardFronts>();
                AssetDatabase.CreateAsset(list, ListPath);
            }

            SerializedObject so = new SerializedObject(list);
            SerializedProperty array = so.FindProperty("entries");
            array.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("stableId").stringValue = entries[i].stableId;
                entry.FindPropertyRelative("front").objectReferenceValue = entries[i].front;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Stand-in fronts] {made} drawn, {kept} kept; {entries.Count} cards without art now have a stand-in front "
                + $"({Folder}).");
        }

        // "crane-trap-54460015" (the name, then the start of the stable id so two cards never share a file).
        private static string FileName(CardDefinition card)
        {
            StringBuilder name = new StringBuilder();
            string display = string.IsNullOrWhiteSpace(card.DisplayName) ? "card" : card.DisplayName.ToLowerInvariant();
            foreach (char c in display)
            {
                if (char.IsLetterOrDigit(c)) name.Append(c);
                else if (name.Length > 0 && name[name.Length - 1] != '-') name.Append('-');
            }

            string id = card.StableId.Replace("-", string.Empty);
            return name.ToString().Trim('-') + "-" + id.Substring(Mathf.Max(0, id.Length - 8));
        }

        private static TypeLook LookOf(string category)
        {
            string key = (category ?? string.Empty).Trim().ToLowerInvariant();
            switch (key)
            {
                case "trap":
                    return new TypeLook { Name = "TRAP", Colour = new Color(0.42f, 0.2f, 0.78f), Symbol = 1 };
                case "ability":
                    return new TypeLook { Name = "ABILITY", Colour = new Color(0.1f, 0.55f, 0.48f), Symbol = 2 };
                case "friend":
                    return new TypeLook { Name = "FRIEND", Colour = new Color(0.9f, 0.5f, 0.12f), Symbol = 3 };
                case "controllerinput":
                    return new TypeLook { Name = "CONTROLLER", Colour = new Color(0.2f, 0.35f, 0.72f), Symbol = 0 };
                default:
                    bool named = key.Length > 0 && !char.IsDigit(key[0]);
                    return new TypeLook
                    {
                        Name = named ? category.Trim().ToUpperInvariant() : "CARD",
                        Colour = new Color(0.28f, 0.3f, 0.38f),
                        Symbol = 0,
                    };
            }
        }

        // ---------- Drawing one front ----------

        private static byte[] Render(CardDefinition card, Font title, Font body, Font bold)
        {
            TypeLook look = LookOf(card.Category);
            Texture2D background = Background(look.Colour);
            Texture2D symbol = Symbol(look.Symbol);
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            Texture2D read = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                GameObject cameraObject = new GameObject("Stand-in Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = Height * 0.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.targetTexture = target;
                camera.cullingMask = ~0;

                GameObject canvasObject = new GameObject("Stand-in Canvas", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                canvasObject.layer = 5;
                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                RectTransform root = canvasObject.GetComponent<RectTransform>();
                root.sizeDelta = new Vector2(Width, Height);
                root.position = Vector3.zero;

                RawImage backgroundImage = Child<RawImage>(root, "Background", 0f, 0f, 1f, 1f, Vector2.zero);
                backgroundImage.texture = background;

                // Name at the top: big, white, dark outline (like the real fronts).
                Text name = Child<Text>(root, "Name", 0.06f, 0.82f, 0.94f, 0.97f, Vector2.zero);
                Style(name, title, 64, Color.white, TextAnchor.MiddleCenter, (card.DisplayName ?? string.Empty).ToUpperInvariant());
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 24;
                name.resizeTextMaxSize = 72;
                Outline outline = name.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.08f, 0.06f, 0.12f, 1f);
                outline.effectDistance = new Vector2(4f, -4f);

                // The type band.
                Text band = Child<Text>(root, "Type", 0f, 0.745f, 1f, 0.805f, Vector2.zero);
                Style(band, bold, 34, new Color(0.08f, 0.06f, 0.12f, 1f), TextAnchor.MiddleCenter, look.Name);

                // The symbol in the middle.
                RawImage symbolImage = Child<RawImage>(root, "Symbol", 0.27f, 0.36f, 0.73f, 0.72f, Vector2.zero);
                symbolImage.texture = symbol;
                AspectRatioFitter fitter = symbolImage.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = 1f;

                // The rules text at the bottom, on the light panel the background draws.
                Text rules = Child<Text>(root, "Rules", 0.1f, 0.05f, 0.9f, 0.31f, Vector2.zero);
                Style(rules, body, 34, new Color(0.1f, 0.08f, 0.14f, 1f), TextAnchor.MiddleCenter, card.Description ?? string.Empty);
                rules.resizeTextForBestFit = true;
                rules.resizeTextMinSize = 18;
                rules.resizeTextMaxSize = 36;

                Canvas.ForceUpdateCanvases();
                RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                read.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RoundCorners(read);
                return read.EncodeToPNG();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(read);
                Object.DestroyImmediate(background);
                Object.DestroyImmediate(symbol);
            }
        }

        private static T Child<T>(RectTransform parent, string name, float minX, float minY, float maxX, float maxY, Vector2 offset)
            where T : Graphic
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = offset;
            rect.offsetMax = offset;
            return go.AddComponent<T>();
        }

        private static void Style(Text text, Font font, int size, Color colour, TextAnchor alignment, string words)
        {
            text.font = font;
            text.fontSize = size;
            text.color = colour;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.text = words;
        }

        // The type's colour with soft rays from the symbol, a pale band under the type name and a light panel under
        // the rules text.
        private static Texture2D Background(Color colour)
        {
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[Width * Height];
            Color light = Color.Lerp(colour, Color.white, 0.16f);
            Color dark = Color.Lerp(colour, Color.black, 0.12f);
            Color band = new Color(0.98f, 0.92f, 0.62f, 1f);
            Color panel = new Color(0.99f, 0.97f, 0.9f, 0.94f);
            Vector2 centre = new Vector2(Width * 0.5f, Height * 0.54f);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float angle = Mathf.Atan2(y - centre.y, x - centre.x);
                    float ray = Mathf.Sin(angle * 16f);
                    Color pixel = Color.Lerp(dark, light, Mathf.SmoothStep(0f, 1f, (ray * 2.5f) + 0.5f));
                    float v = y / (float)Height;
                    if (v >= 0.745f && v <= 0.805f) pixel = band;
                    if (InsideRoundedRect(x, y, Width * 0.07f, Height * 0.035f, Width * 0.93f, Height * 0.325f, 28f))
                    {
                        pixel = Color.Lerp(pixel, panel, panel.a);
                    }

                    pixels[(y * Width) + x] = pixel;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static bool InsideRoundedRect(float x, float y, float minX, float minY, float maxX, float maxY, float radius)
        {
            if (x < minX || x > maxX || y < minY || y > maxY) return false;
            float cx = Mathf.Clamp(x, minX + radius, maxX - radius);
            float cy = Mathf.Clamp(y, minY + radius, maxY - radius);
            float dx = x - cx;
            float dy = y - cy;
            return (dx * dx) + (dy * dy) <= radius * radius;
        }

        // Clear corners, as the real fronts have.
        private static void RoundCorners(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, CornerRadius, Width - CornerRadius);
                    float cy = Mathf.Clamp(y + 0.5f, CornerRadius, Height - CornerRadius);
                    float distance = Mathf.Sqrt(((x + 0.5f - cx) * (x + 0.5f - cx)) + ((y + 0.5f - cy) * (y + 0.5f - cy)));
                    float alpha = Mathf.Clamp01(CornerRadius - distance + 0.5f);
                    int index = (y * Width) + x;
                    pixels[index].a = (byte)Mathf.RoundToInt(alpha * 255f);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }

        // ---------- Symbols: a white-and-yellow shape with a dark outline ----------

        private const int SymbolSize = 512;

        private static Texture2D Symbol(int kind)
        {
            Vector2[] shape;
            switch (kind)
            {
                case 1: // warning triangle
                    shape = new[] { new Vector2(0.5f, 0.92f), new Vector2(0.94f, 0.12f), new Vector2(0.06f, 0.12f) };
                    break;
                case 2: // lightning bolt
                    shape = new[]
                    {
                        new Vector2(0.6f, 0.95f), new Vector2(0.22f, 0.45f), new Vector2(0.47f, 0.45f),
                        new Vector2(0.38f, 0.05f), new Vector2(0.8f, 0.58f), new Vector2(0.54f, 0.58f),
                    };
                    break;
                case 3: // star
                    shape = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = (Mathf.PI * 0.5f) + (i * Mathf.PI / 5f);
                        float radius = i % 2 == 0 ? 0.46f : 0.2f;
                        shape[i] = new Vector2(0.5f + (radius * Mathf.Cos(angle)), 0.48f + (radius * Mathf.Sin(angle)));
                    }

                    break;
                default: // circle (many sides)
                    shape = new Vector2[48];
                    for (int i = 0; i < 48; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 48f;
                        shape[i] = new Vector2(0.5f + (0.4f * Mathf.Cos(angle)), 0.5f + (0.4f * Mathf.Sin(angle)));
                    }

                    break;
            }

            Color fill = new Color(1f, 0.85f, 0.25f, 1f);
            Color edge = new Color(0.08f, 0.06f, 0.12f, 1f);
            float edgeWidth = SymbolSize * 0.035f;
            Texture2D texture = new Texture2D(SymbolSize, SymbolSize, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[SymbolSize * SymbolSize];
            for (int y = 0; y < SymbolSize; y++)
            {
                for (int x = 0; x < SymbolSize; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / SymbolSize, (y + 0.5f) / SymbolSize);
                    float distance = DistanceToEdges(shape, p) * SymbolSize; // pixels to the nearest edge
                    bool inside = Inside(shape, p);
                    float alpha = inside ? 1f : Mathf.Clamp01(edgeWidth - distance + 0.5f);
                    Color colour = inside && distance > edgeWidth ? fill : edge;
                    if (inside && distance > edgeWidth && distance < edgeWidth + 1f) colour = Color.Lerp(edge, fill, distance - edgeWidth);
                    if (kind == 1 && InsideExclamation(p)) colour = edge; // the "!" in the triangle
                    colour.a = alpha;
                    pixels[(y * SymbolSize) + x] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static bool InsideExclamation(Vector2 p)
        {
            bool bar = p.x > 0.455f && p.x < 0.545f && p.y > 0.4f && p.y < 0.72f;
            Vector2 dot = p - new Vector2(0.5f, 0.28f);
            return bar || dot.sqrMagnitude < 0.05f * 0.05f;
        }

        private static bool Inside(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y) / (b.y - a.y)) + a.x) inside = !inside;
            }

            return inside;
        }

        private static float DistanceToEdges(Vector2[] polygon, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[j];
                Vector2 b = polygon[i];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + (ab * t) - p).magnitude);
            }

            return best;
        }

        private static void ConfigureImport(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Default && importer.alphaIsTransparency && importer.maxTextureSize == 1024)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}
