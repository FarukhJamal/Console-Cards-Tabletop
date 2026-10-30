using System;
using System.Collections.Generic;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;
using static ConsoleCards.Presentation.Composition.TabletopComposition;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// COVER-1 (owner, 2026-10-31: "every game should have their title image ... we need that option to update that"):
    /// the game's title picture in the Library, on the game sheet and on Played Last. CHANGE PICTURE on the game sheet
    /// saves a picture under the game's cover key: in the Unity editor into the game itself (GAMEDATA-1), in a build
    /// into the player's profile. Without one the game's own Cover Picture is used, then its first card's front.
    /// </summary>
    internal sealed partial class GameSelectPresenter
    {
        // Each game's title picture read from the saves, by game ID (null: none saved). Read once, not every refresh.
        private readonly Dictionary<string, Texture2D> coverCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private GameDefinition coverGame; // the game the sheet shows

        private void FillCoverBoard(GameSelectModel model, GameSelectEntry chosen)
        {
            coverGame = chosen != null && chosen.PlayerGame == null ? chosen.Game : null;
            model.CanChangeGamePicture = coverGame != null && PictureFilePicker.IsAvailable;
            gameSelectActions.ChangeGamePicture = ChangeGamePictureFromFrontEnd;
        }

        // The saved title picture, else the game's own; null when the game has neither.
        private Texture2D CoverOf(GameDefinition game)
        {
            string id = game.StableId ?? string.Empty;
            if (!coverCache.TryGetValue(id, out Texture2D cover))
            {
                cover = LoadCover(game);
                coverCache[id] = cover;
            }

            return cover != null ? cover : game.CoverPicture;
        }

        private Texture2D LoadCover(GameDefinition game)
        {
            string key = ArtLibrary.NormalizeKey(ContentArtService.CoverKeyOf(game));
            ContentPackStore yours = ContentPackStore.ForCards(host.EnsureProfile());
            Texture2D found = LoadCoverFrom(yours, game.StableId, key);
            if (found == null && !yours.IsGameData) found = LoadCoverFrom(ContentPackStore.GameData(), game.StableId, key);
            return found;
        }

        private static Texture2D LoadCoverFrom(ContentPackStore store, string gameId, string key)
        {
            ContentUploadIndex index = store.LoadUploads(gameId);
            for (int i = 0; i < index.pictures.Count; i++)
            {
                ContentArtFile file = index.pictures[i];
                if (file == null || ArtLibrary.NormalizeKey(file.key) != key) continue;
                byte[] bytes = ContentPackStore.ReadPicture(store.UploadsFolder(gameId), file.file, out string error);
                if (bytes == null) Debug.LogWarning($"[Content] The title picture for {key}: {error}");
                return bytes != null ? ArtLibrary.LoadTexture(bytes, key) : null;
            }

            return null;
        }

        private void ForgetCover(GameDefinition game)
        {
            string id = game.StableId ?? string.Empty;
            if (coverCache.TryGetValue(id, out Texture2D old) && old != null) UnityEngine.Object.Destroy(old);
            coverCache.Remove(id);
        }

        private void ChangeGamePictureFromFrontEnd()
        {
            GameDefinition game = coverGame;
            if (game == null) return;
            if (!PictureFilePicker.TryPick($"Choose the title picture for {game.DisplayName}", out string path))
            {
                ShowGameSelect(string.Empty);
                return;
            }

            string message;
            ContentArtServiceScope scope = ArtFor(game);
            try
            {
                scope.Art.SetUpload(ContentArtService.CoverKeyOf(game), path, out message);
            }
            finally
            {
                scope.Dispose();
            }

            ForgetCover(game);
            ShowGameSelect(message);
        }
    }
}
