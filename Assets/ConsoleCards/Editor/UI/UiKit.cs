using System;
using ConsoleCards.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleCards.Editor.UI
{
    /// <summary>
    /// Shared drawing kit for the UI prefab builders (Toolbox, session bar, gameplay UI): one palette, one set
    /// of sprites and fonts, and the same small helpers, so every authored UI prefab speaks one design language.
    /// Builders import it with <c>using static</c>.
    /// </summary>
    public static class UiKit
    {
        public const string SpritePath = "Assets/ConsoleCards/Content/UI/Toolbox/Sprites/";
        public const string ToolboxIconPath = "Assets/ConsoleCards/Content/UI/Toolbox/Icons/";
        public const string FontPath = "Assets/ConsoleCards/Content/UI/Fonts/";
        public const string RealUiCatalogPath = "Assets/ConsoleCards/Content/Prefabs/Real/UI/RealUiPrefabCatalog.asset";
        public const float KitBorder = 5f;

        public static readonly Color Cream = Hex("fdf8e6");
        public static readonly Color CreamDark = Hex("f4ecd2");
        public static readonly Color TabIdle = Hex("e2d7b4");
        public static readonly Color Ink = Hex("1d1d1f");
        public static readonly Color Muted = Hex("4a4a4a");
        public static readonly Color Orange = Hex("e0531f");
        public static readonly Color Mustard = Hex("f2a81d");
        public static readonly Color Teal = Hex("2fa58a");
        public static readonly Color Go = Hex("1f7a66");
        public static readonly Color Selected = Hex("fff3c4");
        public static readonly Color Rule = Hex("c9bf9e");
        public static readonly Color BackdropDim = new Color(0.04f, 0.06f, 0.055f, 0.55f);

        public static RectTransform Rect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        /// <summary>A child for a layout group (the group sets its size).</summary>
        public static RectTransform Child(string name, Transform parent)
        {
            return Rect(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        public static RectTransform TopBand(string name, Transform parent, float top, float height, float border = KitBorder)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            rect.offsetMin = new Vector2(border, -top - height);
            rect.offsetMax = new Vector2(-border, -top);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static Image Img(RectTransform rect, string sprite, Color color, string spriteAssetPath = null)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            string path = spriteAssetPath ?? (sprite != null ? SpritePath + sprite + ".png" : null);
            image.sprite = path != null ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : null;
            image.color = color;
            image.type = sprite != null && sprite != "Solid" ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 1f;
            image.preserveAspect = spriteAssetPath != null;
            return image;
        }

        public static Text Label(RectTransform rect, Font font, int size, Color color, TextAnchor alignment, string text)
        {
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.text = text;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        /// <summary>A one-line label inside a layout group, sized to its text.</summary>
        public static Text FitLabel(RectTransform rect, Font font, int size, Color color, string text)
        {
            Text label = Label(rect, font, size, color, TextAnchor.MiddleLeft, text);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

        public static LayoutElement Size(GameObject go, float width, float height, float minWidth = -1f)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = go.AddComponent<LayoutElement>();
            }

            if (width >= 0f)
            {
                element.preferredWidth = width;
            }

            if (minWidth >= 0f)
            {
                element.minWidth = minWidth;
            }

            if (height >= 0f)
            {
                element.minHeight = height;
                element.preferredHeight = height;
            }

            return element;
        }

        public static HorizontalLayoutGroup Row(GameObject go, int left, int right, int top, int bottom, float spacing,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static VerticalLayoutGroup Column(GameObject go, int left, int right, int top, int bottom, float spacing,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static ContentSizeFitter Fit(GameObject go, bool width, bool height)
        {
            ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = width ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = height ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return fitter;
        }

        /// <summary>Offset ink shadow and a fill behind a panel's contents; neither takes part in layout.</summary>
        public static Image Backdrop(RectTransform panel, float shadowOffset, string sprite = "PanelOutlined", Color? fill = null)
        {
            RectTransform shadow = Rect("Shadow", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(shadowOffset, -shadowOffset), Vector2.zero);
            Img(shadow, sprite, Ink).raycastTarget = false;
            shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            RectTransform fillRect = Rect("Fill", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image fillImage = Img(fillRect, sprite, fill ?? Cream);
            fillRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return fillImage;
        }

        /// <summary>An outlined button with a centred label (the fill is the button's target graphic).</summary>
        public static Button TextButton(Transform parent, string name, string text, Font font, int fontSize, Color fill,
            Color textColor, float width, float height, out Text label)
        {
            RectTransform rect = Child(name, parent);
            Image image = Img(rect, "ButtonOutlined", fill);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            SetDisabledTint(button);
            Size(rect.gameObject, width, height, width);
            label = Label(Rect("Label", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(-16f, 0f)), font, fontSize, textColor, TextAnchor.MiddleCenter, text);
            return button;
        }

        public static void SetDisabledTint(Selectable selectable)
        {
            ColorBlock colors = selectable.colors;
            colors.disabledColor = new Color(0.85f, 0.82f, 0.74f, 0.75f);
            selectable.colors = colors;
        }

        /// <summary>
        /// An ON/OFF switch row in the Toolbox Hand switch style: title, hint line and a pill switch at the right.
        /// Assign the parts to a ToolboxSwitch with <see cref="AssignSwitch"/>.
        /// </summary>
        public static RectTransform SwitchRow(Transform parent, string name, string title, string hint, Font titleFont,
            Font hintFont, Font wordFont, float height, out Button button, out Image fill, out RectTransform knob,
            out Text word, out Text hintLabel)
        {
            RectTransform row = Child(name, parent);
            Img(row, "Solid", CreamDark).raycastTarget = false;
            Size(row.gameObject, -1f, height);
            Img(Rect("Rule", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(0f, 3f)), "Solid", TabIdle).raycastTarget = false;
            Label(Rect("Title", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f),
                new Vector2(-140f, 26f)), titleFont, 19, Ink, TextAnchor.UpperLeft, title);
            hintLabel = Label(Rect("Hint", row, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -38f), new Vector2(-140f, 24f)), hintFont, 15, Muted, TextAnchor.UpperLeft, hint);
            RectTransform pill = Rect("Switch", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-18f, -1f), new Vector2(91f, 46f));
            fill = Img(pill, "PillOutlined", Go);
            button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            knob = Rect("Knob", pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(22f, 0f), new Vector2(32f, 32f));
            Img(knob, "ButtonOutlined", Cream).raycastTarget = false;
            word = Label(Rect("Word", pill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-14f, 0f), new Vector2(46f, 30f)), wordFont, 15, Cream, TextAnchor.MiddleCenter, "ON");
            return row;
        }

        /// <summary>Fills a serialized ToolboxSwitch with the parts of a <see cref="SwitchRow"/>.</summary>
        public static void AssignSwitch(SerializedProperty property, RectTransform row, Button button, Image fill,
            RectTransform knob, Text word, Text hint, string onHint, string offHint)
        {
            property.FindPropertyRelative("row").objectReferenceValue = row.gameObject;
            property.FindPropertyRelative("button").objectReferenceValue = button;
            property.FindPropertyRelative("fill").objectReferenceValue = fill;
            property.FindPropertyRelative("knob").objectReferenceValue = knob;
            property.FindPropertyRelative("word").objectReferenceValue = word;
            property.FindPropertyRelative("hint").objectReferenceValue = hint;
            property.FindPropertyRelative("onFill").colorValue = Go;
            property.FindPropertyRelative("offFill").colorValue = TabIdle;
            property.FindPropertyRelative("onWordColor").colorValue = Cream;
            property.FindPropertyRelative("offWordColor").colorValue = Muted;
            property.FindPropertyRelative("knobOffset").floatValue = 22f;
            property.FindPropertyRelative("wordOffset").floatValue = 14f;
            property.FindPropertyRelative("onHint").stringValue = onHint;
            property.FindPropertyRelative("offHint").stringValue = offHint;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        public static string Key(string key)
        {
            return $"<b><color=#{ColorUtility.ToHtmlStringRGB(Mustard)}>{key}</color></b>";
        }

        public static Font RequireFont(string file)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath + file);
            if (font == null)
            {
                throw new InvalidOperationException($"UI font missing: {FontPath + file}.");
            }

            return font;
        }

        /// <summary>Imports a PNG as an uncompressed single sprite with the given 9-slice border.</summary>
        public static void ConfigureTexture(string path, Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"UI art missing: {path}.");
            }

            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.spriteBorder != border
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency
                || importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = path.EndsWith("Dash.png", StringComparison.Ordinal)
                ? TextureWrapMode.Repeat
                : TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        /// <summary>Points (or adds) a Real UI prefab catalog entry at a built prefab. Call AssetDatabase.SaveAssets after.</summary>
        public static void AssignCatalogEntry(string id, ReusableUiView prefab, UiLayer layer, UiRetention retention)
        {
            UiPrefabCatalog catalog = AssetDatabase.LoadAssetAtPath<UiPrefabCatalog>(RealUiCatalogPath);
            if (catalog == null || prefab == null)
            {
                throw new InvalidOperationException($"Real UI prefab catalog or the '{id}' prefab is missing.");
            }

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty entries = so.FindProperty("entries");
            SerializedProperty entry = null;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty candidate = entries.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("id").stringValue == id)
                {
                    entry = candidate;
                    break;
                }
            }

            if (entry == null)
            {
                entries.InsertArrayElementAtIndex(entries.arraySize);
                entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("id").stringValue = id;
            }

            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("layer").enumValueIndex = (int)layer;
            entry.FindPropertyRelative("retention").enumValueIndex = (int)retention;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
    }
}
