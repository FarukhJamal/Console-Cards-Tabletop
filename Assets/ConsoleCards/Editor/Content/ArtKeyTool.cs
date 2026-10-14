using System.Collections.Generic;
using System.IO;
using System.Text;
using ConsoleCards.Definitions;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Content
{
    /// <summary>
    /// Fills missing art keys (doc 24, A2). A card front or back, an avatar or a board card back that has a texture
    /// but no key gets one from the texture's place under an Art folder: Art/TrapFloor/Fronts/shield.png becomes
    /// trap-floor/fronts/shield.png. Keys that are already set are never changed. Duplicate keys (one key, two
    /// different pictures) and keys that differ from their file's place are reported.
    /// </summary>
    public static class ArtKeyTool
    {
        private const string ArtFolderMarker = "/Art/";

        [MenuItem("Console Cards/Content/Fill Missing Art Keys")]
        public static void FillMissingArtKeys()
        {
            Dictionary<string, string> texturesByKey = new Dictionary<string, string>();
            StringBuilder report = new StringBuilder();
            int filled = 0;
            int checkedCount = 0;

            foreach (CardDefinition card in LoadAll<CardDefinition>())
            {
                SerializedObject so = new SerializedObject(card);
                filled += Fill(so, "frontArtwork", "frontArtworkReference", card.name, texturesByKey, report, ref checkedCount);
                filled += Fill(so, "backArtwork", "backArtworkReference", card.name, texturesByKey, report, ref checkedCount);
                Save(so, card);
            }

            foreach (AvatarDefinition avatar in LoadAll<AvatarDefinition>())
            {
                SerializedObject so = new SerializedObject(avatar);
                filled += Fill(so, "artwork", "artworkReference", avatar.name, texturesByKey, report, ref checkedCount);
                Save(so, avatar);
            }

            foreach (GameDefinition game in LoadAll<GameDefinition>())
            {
                SerializedObject so = new SerializedObject(game);
                SerializedProperty backs = so.FindProperty("boardCardBacks");
                SerializedProperty keys = so.FindProperty("boardCardBackReferences");
                if (backs == null || keys == null) continue;
                if (keys.arraySize < backs.arraySize) keys.arraySize = backs.arraySize;
                for (int i = 0; i < backs.arraySize; i++)
                {
                    filled += FillPair(backs.GetArrayElementAtIndex(i), keys.GetArrayElementAtIndex(i),
                        $"{game.name} board back {i + 1}", texturesByKey, report, ref checkedCount);
                }

                Save(so, game);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Art keys] Checked {checkedCount} pictures; filled {filled} missing key(s)."
                + (report.Length > 0 ? "\n" + report : " No problems found."));
        }

        /// <summary>The key for a texture asset path: folders under Art in kebab case, then the file name.</summary>
        public static string DeriveKey(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return string.Empty;
            string path = assetPath.Replace('\\', '/');
            int marker = path.IndexOf(ArtFolderMarker, System.StringComparison.Ordinal);
            string relative = marker >= 0 ? path.Substring(marker + ArtFolderMarker.Length) : Path.GetFileName(path);
            string[] parts = relative.Split('/');
            StringBuilder key = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) key.Append('/');
                key.Append(i < parts.Length - 1 ? Kebab(parts[i]) : parts[i].ToLowerInvariant());
            }

            return key.ToString();
        }

        private static int Fill(SerializedObject so, string textureField, string keyField, string owner,
            Dictionary<string, string> texturesByKey, StringBuilder report, ref int checkedCount)
        {
            SerializedProperty texture = so.FindProperty(textureField);
            SerializedProperty key = so.FindProperty(keyField);
            return texture == null || key == null
                ? 0
                : FillPair(texture, key, $"{owner}.{textureField}", texturesByKey, report, ref checkedCount);
        }

        private static int FillPair(SerializedProperty texture, SerializedProperty key, string owner,
            Dictionary<string, string> texturesByKey, StringBuilder report, ref int checkedCount)
        {
            Texture2D picture = texture.objectReferenceValue as Texture2D;
            if (picture == null) return 0;
            checkedCount++;
            string path = AssetDatabase.GetAssetPath(picture);
            string derived = DeriveKey(path);
            int filled = 0;
            string current = key.stringValue == null ? string.Empty : key.stringValue.Trim();
            if (current.Length == 0)
            {
                key.stringValue = derived;
                current = derived;
                filled = 1;
            }
            else if (!string.Equals(current.ToLowerInvariant().Replace('\\', '/'), derived, System.StringComparison.Ordinal))
            {
                report.AppendLine($"Note: {owner} uses key '{current}', but its file suggests '{derived}'.");
            }

            string normalized = current.ToLowerInvariant().Replace('\\', '/').TrimStart('/');
            if (texturesByKey.TryGetValue(normalized, out string otherPath) && otherPath != path)
            {
                report.AppendLine($"Duplicate: key '{normalized}' is used for {otherPath} and {path} ({owner}).");
            }
            else
            {
                texturesByKey[normalized] = path;
            }

            return filled;
        }

        private static string Kebab(string folder)
        {
            StringBuilder result = new StringBuilder(folder.Length + 4);
            for (int i = 0; i < folder.Length; i++)
            {
                char c = folder[i];
                if (char.IsUpper(c) && i > 0 && (char.IsLower(folder[i - 1]) || char.IsDigit(folder[i - 1])))
                {
                    result.Append('-');
                }

                result.Append(c == ' ' || c == '_' ? '-' : char.ToLowerInvariant(c));
            }

            return result.ToString();
        }

        private static void Save(SerializedObject so, Object target)
        {
            if (so.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(target);
            }
        }

        private static IEnumerable<T> LoadAll<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            for (int i = 0; i < guids.Length; i++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null) yield return asset;
            }
        }
    }
}
