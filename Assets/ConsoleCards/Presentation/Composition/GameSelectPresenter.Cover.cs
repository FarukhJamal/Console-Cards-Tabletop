using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Saving;
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

        // ---------- doc 63 G1 (owner, 2026-11-21): the cover of a game you made ----------

        // A made game's cover, by game ID: the picture read from the profile (owned here), else the card whose front
        // stands in for it. Worked out again only when the game is saved again (its stamp), never every refresh.
        private sealed class MadeCover
        {
            public string Stamp = string.Empty;
            public Texture2D Picture;
            public ObjectDefinitionId Card;
            public GameDefinition CardGame; // the game's own deck: its first card
        }

        private readonly Dictionary<string, MadeCover> madeCovers = new Dictionary<string, MadeCover>(StringComparer.Ordinal);

        /// <summary>The picture for a game list entry: a made game's cover, else the game's own picture.</summary>
        internal Texture2D EntryPicture(GameSelectEntry entry)
        {
            if (entry == null) return null;
            return entry.PlayerGame != null ? MadeGamePicture(entry.PlayerGame) : GamePicture(entry.Game);
        }

        /// <summary>A made game's cover picture, else the front of its deck's first card; null when it has neither.</summary>
        internal Texture2D MadeGamePicture(PlayerGameFile game)
        {
            if (game == null) return null;
            string id = game.id ?? string.Empty;
            string stamp = (game.savedUtc ?? string.Empty) + "|" + (game.coverPicture ?? string.Empty);
            if (!madeCovers.TryGetValue(id, out MadeCover cover) || cover.Stamp != stamp)
            {
                if (cover != null && cover.Picture != null) UnityEngine.Object.Destroy(cover.Picture);
                cover = new MadeCover { Stamp = stamp, Picture = LoadMadeCover(game.coverPicture) };
                if (cover.Picture == null) FindDeckFront(game.deck, cover);
                madeCovers[id] = cover;
            }

            if (cover.Picture != null) return cover.Picture;
            if (!cover.Card.IsEmpty && host.TryGetCardLook(cover.Card, out CardDefinition card)) return host.FrontArt(card);
            return cover.CardGame != null ? FirstCardFront(cover.CardGame) : null;
        }

        /// <summary>The cover picture file of a made game, read from the profile (in someone else's room: the host's too).</summary>
        internal Texture2D LoadMadeCover(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return null;
            ProfileService profiles = host.EnsureProfile();
            Texture2D found = LoadMadeCoverFrom(ContentPackStore.ForLibrary(profiles, true), file);
            if (found == null && ContentPackStore.InSomeoneElsesRoom) found = LoadMadeCoverFrom(ContentPackStore.ForLibrary(profiles, false), file);
            return found;
        }

        private static Texture2D LoadMadeCoverFrom(ContentPackStore store, string file)
        {
            string folder = store.UploadsFolder(PlayerLibrary.Id);
            if (!File.Exists(Path.Combine(folder, file))) return null;
            byte[] bytes = ContentPackStore.ReadPicture(folder, file, out string error);
            if (bytes == null)
            {
                Debug.LogWarning($"[Your games] A game's cover picture could not be read: {error}");
                return null;
            }

            return ArtLibrary.LoadTexture(bytes, "your-game-cover");
        }

        // The first card of a made game's deck: a deck of yours (or one of its parts), or a game's own deck.
        private void FindDeckFront(SavedGameDeck deck, MadeCover cover)
        {
            if (deck == null || !deck.IsSet) return;
            try
            {
                GameDefinition game = FindGameById(deck.gameId);
                if (game != null && string.Equals(deck.deckId, SavedGameDeck.GameDeck, StringComparison.OrdinalIgnoreCase))
                {
                    cover.CardGame = game;
                    return;
                }

                if (game != null && FirstDeckCard(game, deck.deckId, out cover.Card)) return;
                // Doc 63: a deck moved from its game into "Your cards" is found there.
                GameDefinition library = host.LibraryGame;
                if (library != null && !PlayerLibrary.IsLibrary(deck.gameId)) FirstDeckCard(library, deck.deckId, out cover.Card);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Your games] The cover card could not be found: {exception.Message}");
            }
        }

        private bool FirstDeckCard(GameDefinition game, string deckId, out ObjectDefinitionId card)
        {
            card = default;
            bool library = PlayerLibrary.IsLibrary(game);
            ContentArtServiceScope scope = library ? default : ArtFor(game);
            try
            {
                PlayerDecksFile decks = library
                    ? ContentPackStore.ForLibrary(host.EnsureProfile(), false).LoadDecks(PlayerLibrary.Id)
                    : scope.Art.Store.LoadDecks(scope.Art.GameId);
                for (int d = 0; decks != null && decks.decks != null && d < decks.decks.Count; d++)
                {
                    PlayerDeckRecord record = decks.decks[d];
                    if (record == null) continue;
                    if (string.Equals(record.id, deckId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (FirstCardIn(record.cards, out card)) return true;
                        for (int p = 0; record.HasParts && p < record.parts.Count; p++)
                        {
                            if (record.parts[p] != null && FirstCardIn(record.parts[p].cards, out card)) return true;
                        }

                        return false;
                    }

                    PlayerDeckPart part = record.HasParts ? record.FindPart(deckId) : null;
                    if (part != null) return FirstCardIn(part.cards, out card);
                }
            }
            finally
            {
                scope.Dispose();
            }

            return false;
        }

        private static bool FirstCardIn(List<PlayerDeckEntry> entries, out ObjectDefinitionId card)
        {
            card = default;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                if (entries[i] == null || entries[i].count <= 0 || !Guid.TryParse(entries[i].cardId, out Guid id)) continue;
                card = new ObjectDefinitionId(id);
                return true;
            }

            return false;
        }

        private GameDefinition FindGameById(string gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId)) return null;
            GameDefinition library = host.LibraryGame;
            if (library != null && string.Equals(library.StableId, gameId, StringComparison.OrdinalIgnoreCase)) return library;
            IReadOnlyList<IGameCartridge> cartridges = host.Cartridges.CodeCartridges;
            for (int i = 0; i < cartridges.Count; i++)
            {
                GameDefinition game = cartridges[i].Game;
                if (game != null && string.Equals(game.StableId, gameId, StringComparison.OrdinalIgnoreCase)) return game;
            }

            IReadOnlyList<ComponentCatalog> boxes = host.componentLibrary != null ? host.componentLibrary.GameBoxes : null;
            for (int i = 0; boxes != null && i < boxes.Count; i++)
            {
                GameDefinition game = boxes[i] != null ? boxes[i].Game : null;
                if (game != null && string.Equals(game.StableId, gameId, StringComparison.OrdinalIgnoreCase)) return game;
            }

            return null;
        }

        // A game's first card front (its controller-input cards are skipped), as GamePicture finds it.
        internal Texture2D FirstCardFront(GameDefinition game)
        {
            for (int s = 0; game != null && s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                for (int c = 0; set != null && c < set.Cards.Count; c++)
                {
                    CardDefinition card = set.Cards[c];
                    if (card == null || card.RepresentedControllerInput != null) continue;
                    return host.UsesTableArt(game) ? host.FrontArt(card) : card.FrontArtwork;
                }
            }

            return null;
        }
    }
}
