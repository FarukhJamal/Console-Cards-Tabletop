using System.IO;
using ConsoleCards.Definitions;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Content
{
    /// <summary>
    /// Makes the platform's default Card Frame Style (FRAME-1, doc 31 PL-FRAMES) in Resources/CardFrames, from the
    /// stand-in pictures in Content/Art/CardFrames, with three frames: Dark, Orange and No frame. Like the other
    /// builders it only makes what is missing and never overwrites: change the asset in the Inspector, or replace the
    /// pictures (same names and layout; white or grey so the colours tint them) to restyle every card.
    /// </summary>
    public static class CardFrameBuilder
    {
        private const string ArtPath = "Assets/ConsoleCards/Content/Art/CardFrames/";
        private const string StylePath = "Assets/ConsoleCards/Content/Resources/CardFrames/DefaultCardFrameStyle.asset";
        private const string IconAtlasPath = "Assets/ConsoleCards/Content/Resources/CardIcons/CardIconAtlas.png";

        [MenuItem("Console Cards/Cards/Create Missing Card Frame Style")]
        public static void Build()
        {
            Texture2D frame = PrepareTexture(ArtPath + "CardFrame.png");
            Texture2D sideTab = PrepareTexture(ArtPath + "CardTabSide.png");
            Texture2D cornerTab = PrepareTexture(ArtPath + "CardTabCorner.png");
            PrepareTexture(IconAtlasPath);

            if (AssetDatabase.LoadAssetAtPath<CardFrameStyle>(StylePath) != null)
            {
                Debug.Log($"[Card frames] {StylePath} already exists; nothing was changed.");
                CardFrameStyle.ForgetDefault();
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(StylePath));
            CardFrameStyle style = ScriptableObject.CreateInstance<CardFrameStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
            SerializedObject so = new SerializedObject(style);
            so.FindProperty("stableId").stringValue = "console-cards.frames.default";
            so.FindProperty("displayName").stringValue = "Console Cards frames";
            so.FindProperty("frameTexture").objectReferenceValue = frame;
            so.FindProperty("sideTabTexture").objectReferenceValue = sideTab;
            so.FindProperty("cornerTabTexture").objectReferenceValue = cornerTab;
            SerializedProperty variants = so.FindProperty("variants");
            variants.arraySize = 3;
            SetVariant(variants.GetArrayElementAtIndex(0), "dark", "Dark", true,
                new Color(0.24f, 0.2f, 0.19f, 1f), new Color(0.45f, 0.45f, 0.48f, 1f));
            SetVariant(variants.GetArrayElementAtIndex(1), "orange", "Orange", true,
                new Color(0.93f, 0.42f, 0.05f, 1f), new Color(0.93f, 0.42f, 0.05f, 1f));
            SetVariant(variants.GetArrayElementAtIndex(2), "none", "No frame", false, Color.white, Color.white);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            CardFrameStyle.ForgetDefault();
            Debug.Log($"[Card frames] Made {StylePath}. Games without their own Card Frame Style use it.");
        }

        private static void SetVariant(SerializedProperty variant, string id, string name, bool showFrame, Color frame, Color tab)
        {
            variant.FindPropertyRelative("id").stringValue = id;
            variant.FindPropertyRelative("displayName").stringValue = name;
            variant.FindPropertyRelative("showFrame").boolValue = showFrame;
            variant.FindPropertyRelative("frameColor").colorValue = frame;
            variant.FindPropertyRelative("tabColor").colorValue = tab;
            variant.FindPropertyRelative("frameTexture").objectReferenceValue = null;
        }

        // Clear edges (no repeat), see-through, mipmaps; the picture is loaded after its settings are right.
        private static Texture2D PrepareTexture(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Card frames] Missing picture {path}.");
                return null;
            }

            if (importer.textureType != TextureImporterType.Default
                || !importer.alphaIsTransparency
                || importer.wrapMode != TextureWrapMode.Clamp
                || !importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
