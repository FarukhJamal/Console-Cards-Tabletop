using System.Collections.Generic;
using System.IO;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Saving;
using UnityEditor;
using UnityEngine;

namespace ConsoleCards.Editor.Content
{
    /// <summary>
    /// Pack tools for authors (doc 24, A3): export a game's built-in art as a pack (a valid starting point for a
    /// new pack), import a pack folder into the last-used player profile (doc 28), and open the profiles folder.
    /// Players make their cards, decks and rules in the game, not here.
    /// </summary>
    public static class ContentPackTools
    {
        [MenuItem("Console Cards/Content/Export Built-in Art Pack...")]
        public static void ExportBuiltInArtPack()
        {
            GameDefinition game = SelectedOrFirstGame();
            if (game == null)
            {
                Debug.LogWarning("[Content] No Game Definition found to export.");
                return;
            }

            string target = EditorUtility.SaveFolderPanel($"Export {game.DisplayName} art pack to", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(target)) return;

            List<KeyValuePair<string, string>> pictures = CollectBuiltInPictures(game, out int skipped);
            string slug = ContentPackStore.SafeName(string.IsNullOrWhiteSpace(game.DisplayName) ? game.StableId : game.DisplayName, "game");
            string packFolder = Path.Combine(target, slug + "-built-in");
            ContentPackManifest manifest = new ContentPackManifest
            {
                packId = slug + "-built-in",
                gameStableId = game.StableId,
                displayName = $"{game.DisplayName} (built-in art)",
                author = "Console Cards",
                description = "The game's built-in pictures. Replace any file under art/ to make your own pack.",
            };
            ContentPackStore.WritePack(packFolder, manifest, pictures);
            EditorUtility.RevealInFinder(Path.Combine(packFolder, ContentPackManifest.FileName));
            Debug.Log($"[Content] Exported {pictures.Count} pictures to {packFolder}"
                + (skipped > 0 ? $" ({skipped} without an art key were left out; run Fill Missing Art Keys)." : "."));
        }

        [MenuItem("Console Cards/Content/Import Pack Folder...")]
        public static void ImportPackFolder()
        {
            string source = EditorUtility.OpenFolderPanel("Choose a pack folder (with pack.json)", string.Empty, string.Empty);
            if (string.IsNullOrEmpty(source)) return;
            if (!ContentPackStore.TryReadManifest(source, out ContentPackManifest manifest, out string error))
            {
                Debug.LogWarning($"[Content] {source} is not a pack: {error}.");
                return;
            }

            GameDefinition game = SelectedOrFirstGame();
            string gameId = !string.IsNullOrWhiteSpace(manifest.gameStableId)
                ? manifest.gameStableId
                : game != null ? game.StableId : string.Empty;
            if (string.IsNullOrWhiteSpace(gameId))
            {
                Debug.LogWarning("[Content] The pack names no game and no Game Definition was found.");
                return;
            }

            ProfileService profiles = ProfileService.CreateDefault();
            ContentPackStore store = ContentPackStore.ForProfile(profiles);
            if (store.TryImportPackFolder(gameId, source, out string packId, out error))
            {
                Debug.Log($"[Content] Imported pack '{packId}' into profile '{profiles.Active.displayName}'. "
                    + "In Play mode, open Table > Card art and pick it.");
            }
            else
            {
                Debug.LogWarning($"[Content] Import failed: {error}");
            }
        }

        [MenuItem("Console Cards/Content/Open Profiles Folder")]
        public static void OpenProfilesFolder()
        {
            ProfileStore store = ProfileStore.CreateDefault();
            Directory.CreateDirectory(store.RootFolder);
            EditorUtility.RevealInFinder(store.RootFolder);
        }

        private static List<KeyValuePair<string, string>> CollectBuiltInPictures(GameDefinition game, out int skipped)
        {
            List<KeyValuePair<string, string>> pictures = new List<KeyValuePair<string, string>>();
            HashSet<string> keys = new HashSet<string>();
            skipped = 0;
            IReadOnlyList<GameContentSet> sets = game.ContentSets;
            for (int s = 0; sets != null && s < sets.Count; s++)
            {
                IReadOnlyList<CardDefinition> cards = sets[s] != null ? sets[s].Cards : null;
                for (int i = 0; cards != null && i < cards.Count; i++)
                {
                    if (cards[i] == null) continue;
                    Add(cards[i].FrontArtworkReference, cards[i].FrontArtwork, pictures, keys, ref skipped);
                    Add(cards[i].BackArtworkReference, cards[i].BackArtwork, pictures, keys, ref skipped);
                }
            }

            IReadOnlyList<AvatarDefinition> avatars = game.Avatars;
            for (int i = 0; avatars != null && i < avatars.Count; i++)
            {
                if (avatars[i] != null) Add(avatars[i].ArtworkReference, avatars[i].Artwork, pictures, keys, ref skipped);
            }

            IReadOnlyList<Texture2D> backs = game.BoardCardBacks;
            IReadOnlyList<string> backKeys = game.BoardCardBackReferences;
            for (int i = 0; backs != null && i < backs.Count; i++)
            {
                Add(backKeys != null && i < backKeys.Count ? backKeys[i] : null, backs[i], pictures, keys, ref skipped);
            }

            return pictures;
        }

        private static void Add(string key, Texture2D texture, List<KeyValuePair<string, string>> pictures,
            HashSet<string> keys, ref int skipped)
        {
            if (texture == null) return;
            string normalized = ConsoleCards.Presentation.Art.ArtLibrary.NormalizeKey(key);
            if (normalized.Length == 0)
            {
                skipped++;
                return;
            }

            string path = Path.GetFullPath(AssetDatabase.GetAssetPath(texture));
            if (!File.Exists(path) || !keys.Add(normalized)) return;
            pictures.Add(new KeyValuePair<string, string>(normalized, path));
        }

        private static GameDefinition SelectedOrFirstGame()
        {
            if (Selection.activeObject is GameDefinition selected) return selected;
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(GameDefinition));
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<GameDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }
    }
}
