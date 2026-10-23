using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Maintenance
{
    /// <summary>
    /// One-off tidy (owner, 2026-10-23: "why are boards separate in Resources when every art is in ConsoleCards/Content/Art?"):
    /// moves the Trap Floor playmat picture from Assets/Resources/Boards to Content/Art/Boards through the
    /// AssetDatabase, so it keeps its GUID and the Trap Floor game definition still points at it. Removes the empty
    /// Resources/Boards folder. Does nothing once the picture has moved; this file can be deleted afterwards.
    /// </summary>
    [InitializeOnLoad]
    internal static class MoveBoardArtIntoContent
    {
        private const string OldFolder = "Assets/Resources/Boards";
        private const string OldPath = OldFolder + "/TrapFloorPlaymat.png";
        private const string ArtFolder = "Assets/ConsoleCards/Content/Art";
        private const string NewFolder = ArtFolder + "/Boards";
        private const string NewPath = NewFolder + "/TrapFloorPlaymat.png";

        static MoveBoardArtIntoContent()
        {
            EditorApplication.delayCall += Move;
        }

        private static void Move()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(OldPath) == null) return;
            if (!AssetDatabase.IsValidFolder(NewFolder)) AssetDatabase.CreateFolder(ArtFolder, "Boards");
            if (AssetDatabase.LoadAssetAtPath<Object>(NewPath) != null)
            {
                Debug.LogWarning($"[Board art] {NewPath} already exists; {OldPath} was left where it is.");
                return;
            }

            string error = AssetDatabase.MoveAsset(OldPath, NewPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"[Board art] Could not move {OldPath}: {error}");
                return;
            }

            if (AssetDatabase.IsValidFolder(OldFolder) && AssetDatabase.FindAssets(string.Empty, new[] { OldFolder }).Length == 0)
            {
                AssetDatabase.DeleteAsset(OldFolder);
            }

            AssetDatabase.ImportAsset(NewPath, ImportAssetOptions.ForceUpdate); // the card art import rule applies there
            Debug.Log($"[Board art] Moved the playmat picture to {NewPath}.");
        }
    }
}
