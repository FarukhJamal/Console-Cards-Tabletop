using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// UI-1 (owner, 2026-10-23; doc 36): restyles the Final UI prefabs in place in the designer's UI Kit look:
    /// Oxanium for every label, the kit's dark panels, rows and buttons with a thin light border, and the kit's
    /// colours. Layout and every hand edit stay; only fonts, sprites and colours that still match the old palette
    /// change, so running it again changes nothing. It also moves the notifications to the V5 layout (Log and
    /// toasts bottom left, the banner under the hint bar). Run it again after a builder makes a missing prefab.
    /// </summary>
    public static class KitLookApplier
    {
        public const string KitSpritePath = "Assets/ConsoleCards/Content/UI/Kit/";

        // Kit sprites: white border, grey fill (the share of the tint the fill shows), and 9-slice border.
        private static readonly (string Name, float Fill, int Border)[] KitSprites =
        {
            ("KitPanel", 0.57f, 13),
            ("KitRow", 0.65f, 9),
            ("KitButton", 0.47f, 11),
            ("KitChip", 0.65f, 9),
            ("KitPill", 0.65f, 23),
            ("KitAccent", 1f, 11),
        };

        // Old outlined sprite -> kit sprite of the same role.
        private static readonly Dictionary<string, string> SpriteRoles = new Dictionary<string, string>
        {
            { "PanelOutlined", "KitPanel" },
            { "TileOutlined", "KitRow" },
            { "BoxOutlined", "KitRow" },
            { "TabOutlined", "KitRow" },
            { "ButtonOutlined", "KitButton" },
            { "ChipOutlined", "KitChip" },
            { "PillOutlined", "KitPill" },
        };

        // The kit (doc 36 §1).
        private static readonly Color KitText = Hex("e8eced");
        private static readonly Color KitMuted = Hex("9ba4a7");
        private static readonly Color KitCyan = Hex("06eff0");
        private static readonly Color KitAmber = Hex("f2a81d");
        private static readonly Color KitOrange = Hex("e0531f");
        private static readonly Color KitPrimary = Hex("138a8c");
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.5f);

        // Old palette (UiKit before UI-1) -> the fill each role shows in the kit.
        private static readonly (Color Old, Color Fill, bool Accent)[] FillMap =
        {
            (Hex("fdf8e6"), Hex("222527"), false), // Cream: panel
            (Hex("f4ecd2"), Hex("292e2f"), false), // CreamDark: row
            (Hex("e2d7b4"), Hex("2e3335"), false), // TabIdle
            (Hex("1d1d1f"), Hex("16191a"), false), // Ink
            (Hex("4a4a4a"), Hex("3a3f41"), false), // Muted
            (Hex("c9bf9e"), Hex("424547"), false), // Rule
            (Hex("fff3c4"), Hex("1c3c3d"), false), // Selected: highlight row
            (Hex("e0531f"), KitOrange, true), // Orange
            (Hex("f2a81d"), KitAmber, true), // Mustard
            (Hex("2fa58a"), KitCyan, true), // Teal
            (Hex("1f7a66"), KitPrimary, true), // Go: primary button
        };

        // Old palette -> text colour on the kit's dark panels.
        private static readonly (Color Old, Color Text)[] TextMap =
        {
            (Hex("1d1d1f"), KitText), // Ink
            (Hex("4a4a4a"), KitMuted), // Muted
            (Hex("fdf8e6"), KitText), // Cream (on dark buttons)
            (Hex("f4ecd2"), Hex("c9d0d2")), // CreamDark
            (Hex("c9bf9e"), Hex("6b7275")), // Rule
            (Hex("e0531f"), Hex("ff7b2e")), // Orange
            (Hex("f2a81d"), Hex("f5b731")), // Mustard
            (Hex("2fa58a"), KitCyan), // Teal
            (Hex("1f7a66"), KitCyan), // Go
        };

        private sealed class Report
        {
            public int Prefabs;
            public int Fonts;
            public int Sprites;
            public int Colors;
            public int Moves;
            public int Frames;
            public int V5;

            public void AddFrame() => Frames++;

            public void AddV5() => V5++;

            public int Changes => Fonts + Sprites + Colors + Moves + Frames + V5;
        }

        [MenuItem("Console Cards/UI/Apply Kit Look (UI-1)")]
        public static void ApplyAll()
        {
            PrepareKitSprites();
            KitFrames.Prepare();
            V5Hud.Prepare();
            Dictionary<Font, Font> fonts = FontMap();
            Report report = new Report();
            string[] paths = Directory.GetFiles(UiKit.FinalUiFolder, "*.prefab", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.Ordinal);
            foreach (string file in paths)
            {
                ApplyToPrefab(file.Replace('\\', '/'), fonts, report);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UI-1] Kit look: {report.Prefabs} prefabs changed; {report.Fonts} labels to Oxanium, "
                + $"{report.Sprites} panel sprites, {report.Colors} colours, {report.Moves} moved to the V5 layout, "
                + $"{report.Frames} Figma frames, {report.V5} table HUD prefabs laid out as the Figma V5 screen.");
        }

        private static void ApplyToPrefab(string path, Dictionary<Font, Font> fonts, Report report)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int before = report.Changes;
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                {
                    Font kitFont = KitFontFor(text.font, fonts);
                    if (kitFont != null && text.font != kitFont)
                    {
                        text.font = kitFont;
                        report.Fonts++;
                    }

                    // Dark words on an amber, orange or cyan chip stay dark (the kit's warning chip).
                    if (OnAccent(text) && SameRgb(text.color, Hex("1d1d1f")))
                    {
                        text.color = new Color(0.086f, 0.098f, 0.102f, text.color.a);
                        report.Colors++;
                    }
                    else if (TryMapText(text.color, out Color textColor))
                    {
                        text.color = textColor;
                        report.Colors++;
                    }
                }

                HashSet<Image> tinted = RuntimeTintedImages(root);
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    RestyleImage(image, tinted.Contains(image), report);
                }

                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour is Graphic || behaviour is Selectable) continue;
                    string ns = behaviour.GetType().Namespace ?? string.Empty;
                    if (!ns.StartsWith("ConsoleCards", StringComparison.Ordinal)) continue;
                    RemapSerializedColors(behaviour, report);
                }

                MoveToV5Layout(root, report);
                KitFrames.Apply(root, report.AddFrame);
                V5Hud.Apply(root, report.AddV5);
                if (report.Changes != before)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.Prefabs++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RestyleImage(Image image, bool tintedAtRunTime, Report report)
        {
            Sprite sprite = image.sprite;
            string spritePath = sprite != null ? AssetDatabase.GetAssetPath(sprite) : string.Empty;
            bool kitSprite = spritePath.StartsWith(UiKit.SpritePath, StringComparison.Ordinal)
                || spritePath.StartsWith(KitSpritePath, StringComparison.Ordinal);
            bool icon = spritePath.Contains("/Icons/");
            Color color = image.color;

            if (icon)
            {
                // Icons drawn in ink on the old cream buttons turn light.
                if (TryMapText(color, out Color iconColor))
                {
                    image.color = iconColor;
                    report.Colors++;
                }

                return;
            }

            if (sprite != null && !kitSprite)
            {
                return; // pictures and artwork keep their colours
            }

            bool shadow = image.gameObject.name == "Shadow";
            if (sprite != null && SpriteRoles.TryGetValue(sprite.name, out string role))
            {
                if (shadow)
                {
                    image.sprite = KitSprite(role);
                    image.color = ShadowColor;
                    report.Sprites++;
                    return;
                }

                if (tintedAtRunTime)
                {
                    // A view colours this one while playing: the flat accent shape shows its colour exactly.
                    image.sprite = KitSprite("KitAccent");
                    if (TryMapFill(color, out Color exact, out _)) image.color = new Color(exact.r, exact.g, exact.b, color.a);
                }
                else if (TryMapFill(color, out Color fill, out bool accent))
                {
                    string kit = accent ? "KitAccent" : role;
                    image.sprite = KitSprite(kit);
                    image.color = accent ? fill : TintFor(kit, fill, color.a);
                }
                else
                {
                    // A colour set elsewhere (a state colour): keep it exactly on the accent shape.
                    image.sprite = KitSprite("KitAccent");
                }

                image.type = Image.Type.Sliced;
                report.Sprites++;
                return;
            }

            if (shadow && SameRgb(color, Hex("1d1d1f")))
            {
                image.color = ShadowColor;
                report.Colors++;
                return;
            }

            if (TryMapFill(color, out Color plain, out _))
            {
                image.color = new Color(plain.r, plain.g, plain.b, color.a);
                report.Colors++;
            }
        }

        private static void RemapSerializedColors(MonoBehaviour behaviour, Report report)
        {
            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.Color) continue;
                string name = property.name.ToLowerInvariant();
                if (name.Contains("alert")) continue; // dark words on amber and orange rows stay dark
                bool isText = name.Contains("text") || name.Contains("label") || name.Contains("ink")
                    || name.Contains("font") || name.Contains("icon");
                Color old = property.colorValue;
                Color mapped;
                if (isText ? TryMapText(old, out mapped) : TryMapFillColor(old, out mapped))
                {
                    property.colorValue = new Color(mapped.r, mapped.g, mapped.b, old.a);
                    changed = true;
                    report.Colors++;
                }
            }

            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // V5 (doc 36 §2): the Log and the toasts bottom left, the banner under the hint bar.
        private static void MoveToV5Layout(GameObject root, Report report)
        {
            switch (root.name)
            {
                case "ActivityLog":
                    Place(root, "Tab", new Vector2(29f, 104f), new Vector2(240f, 46f), report);
                    Place(root, "Panel", new Vector2(29f, 160f), new Vector2(460f, 300f), report);
                    break;
                case "FeedbackMessages":
                    Place(root, "Messages", new Vector2(29f, 160f), new Vector2(460f, 184f), report);
                    break;
                case "FeedbackBanner":
                    RectTransform banner = Child(root, "Banner");
                    if (banner != null && Mathf.Abs(banner.anchoredPosition.y - (-122f)) < 0.5f)
                    {
                        banner.anchoredPosition = new Vector2(banner.anchoredPosition.x, -224f);
                        report.Moves++;
                    }

                    break;
            }
        }

        // Bottom-left corner placement; only from the built default (bottom right / bottom centre).
        private static void Place(GameObject root, string child, Vector2 position, Vector2 size, Report report)
        {
            RectTransform rect = Child(root, child);
            if (rect == null || rect.anchorMin == Vector2.zero && rect.pivot == Vector2.zero) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            report.Moves++;
        }

        private static RectTransform Child(GameObject root, string name)
        {
            Transform found = root.transform.Find(name);
            return found as RectTransform;
        }

        private static Color TintFor(string kit, Color fill, float alpha)
        {
            float share = 1f;
            foreach ((string name, float shareOfTint, int _) in KitSprites)
            {
                if (name == kit) share = shareOfTint;
            }

            return new Color(
                Mathf.Clamp01(fill.r / share),
                Mathf.Clamp01(fill.g / share),
                Mathf.Clamp01(fill.b / share),
                alpha);
        }

        private static bool TryMapFill(Color old, out Color fill, out bool accent)
        {
            foreach ((Color from, Color to, bool isAccent) in FillMap)
            {
                if (SameRgb(old, from))
                {
                    fill = to;
                    accent = isAccent;
                    return true;
                }
            }

            fill = old;
            accent = false;
            return false;
        }

        private static bool TryMapFillColor(Color old, out Color fill)
        {
            return TryMapFill(old, out fill, out _);
        }

        private static bool TryMapText(Color old, out Color text)
        {
            foreach ((Color from, Color to) in TextMap)
            {
                if (SameRgb(old, from))
                {
                    text = new Color(to.r, to.g, to.b, old.a);
                    return true;
                }
            }

            text = old;
            return false;
        }

        private static bool SameRgb(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.012f && Mathf.Abs(a.g - b.g) < 0.012f && Mathf.Abs(a.b - b.b) < 0.012f;
        }

        private static Dictionary<Font, Font> FontMap()
        {
            Font bold = UiKit.RequireFont("Oxanium-Bold.ttf");
            Font medium = UiKit.RequireFont("Oxanium-Medium.ttf");
            Dictionary<Font, Font> map = new Dictionary<Font, Font>
            {
                { UiKit.RequireFont("Silkscreen-Bold.ttf"), bold },
                { UiKit.RequireFont("ChakraPetch-Bold.ttf"), bold },
                { UiKit.RequireFont("ChakraPetch-Medium.ttf"), medium },
            };
            return map;
        }

        // Oxanium for a label: by the old kit font, or for Unity's built-in Arial (GameTemplateRow).
        private static Font KitFontFor(Font font, Dictionary<Font, Font> fonts)
        {
            if (font == null) return null;
            if (fonts.TryGetValue(font, out Font mapped)) return mapped;
            return font.name == "Arial" || font.name == "LegacyRuntime"
                ? UiKit.RequireFont("Oxanium-Medium.ttf")
                : null;
        }

        // The nearest picture behind a label (its own object or a parent) is an accent fill.
        private static bool OnAccent(Text text)
        {
            for (Transform at = text.transform.parent; at != null; at = at.parent)
            {
                Image back = at.GetComponent<Image>();
                if (back == null || !back.enabled) continue;
                Color c = back.color;
                return SameRgb(c, Hex("e0531f")) || SameRgb(c, Hex("f2a81d")) || SameRgb(c, Hex("2fa58a"))
                    || SameRgb(c, KitAmber) || SameRgb(c, KitCyan);
            }

            return false;
        }

        private static void PrepareKitSprites()
        {
            foreach ((string name, float _, int border) in KitSprites)
            {
                UiKit.ConfigureTexture(KitSpritePath + name + ".png", new Vector4(border, border, border, border));
            }
        }

        // Images a view of this prefab colours while playing (it has colour fields and refers to them).
        private static HashSet<Image> RuntimeTintedImages(GameObject root)
        {
            HashSet<Image> tinted = new HashSet<Image>();
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour is Graphic || behaviour is Selectable) continue;
                string ns = behaviour.GetType().Namespace ?? string.Empty;
                if (!ns.StartsWith("ConsoleCards", StringComparison.Ordinal)) continue;
                SerializedObject serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();
                bool hasColors = false;
                List<Image> referenced = new List<Image>();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.Color) hasColors = true;
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue is Image image)
                    {
                        referenced.Add(image);
                    }
                }

                if (hasColors) tinted.UnionWith(referenced);
            }

            return tinted;
        }

        private static Sprite KitSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitSpritePath + name + ".png");
            if (sprite == null)
            {
                throw new InvalidOperationException($"UI-1 kit sprite missing: {KitSpritePath + name}.png.");
            }

            return sprite;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }
    }
}
