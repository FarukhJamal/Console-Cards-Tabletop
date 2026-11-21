using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Saving;
using UnityEngine;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>
    /// Doc 63 (owner, 2026-11-21): the one-time move of the cards and decks a player made inside a game (Trap Floor)
    /// into "Your cards" (<see cref="PlayerLibrary"/>). Moved: every new card of the game (a record with a GUID ID
    /// that is not one of the game's own cards), every deck that is not a stage (floor) deck, and their pictures
    /// (re-keyed from "trap-floor/player/…" to "your-cards/player/…"). Never touched: changes to the game's own
    /// cards and the stage decks; a picture they still use is copied, not moved. Made games whose deck was moved
    /// are pointed at the library. Safe to run again: what is already in the library is not copied twice.
    /// Sources: the profile's copy of the game, and in the Unity editor the project's GameData too (a build never
    /// changes its StreamingAssets).
    /// </summary>
    public static class PlayerLibraryMigration
    {
        private static bool ran;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Forget()
        {
            ran = false;
        }

        public static void RunOnce(ProfileService profiles, GameDefinition fromGame)
        {
            if (ran || profiles == null || fromGame == null || PlayerLibrary.IsLibrary(fromGame)) return;
            if (ContentPackStore.InSomeoneElsesRoom) return; // later, with this computer's own saves in use
            ran = true;
            try
            {
                Run(profiles, fromGame);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Your cards] The cards made in {fromGame.DisplayName} could not be moved into Your cards: {exception.Message}");
            }
        }

        private static void Run(ProfileService profiles, GameDefinition fromGame)
        {
            profiles.EnsureActive();
            ContentPackStore library = ContentPackStore.ForLibrary(profiles, true);
            string gameId = fromGame.StableId ?? string.Empty;
            if (gameId.Length == 0) return;
            HashSet<string> builtIn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int s = 0; s < fromGame.ContentSets.Count; s++)
            {
                GameContentSet set = fromGame.ContentSets[s];
                for (int c = 0; set != null && c < set.Cards.Count; c++)
                {
                    if (set.Cards[c] == null) continue;
                    builtIn.Add(set.Cards[c].StableId ?? string.Empty);
                }
            }

            List<ContentPackStore> sources = new List<ContentPackStore>(2);
            if (UnityEngine.Application.isEditor) sources.Add(ContentPackStore.GameData());
            sources.Add(new ContentPackStore(profiles.GamesFolder));

            PlayerCardsFile intoCards = library.LoadCards(PlayerLibrary.Id);
            PlayerDecksFile intoDecks = library.LoadDecks(PlayerLibrary.Id);
            ContentUploadIndex intoUploads = library.LoadUploads(PlayerLibrary.Id);
            int movedCards = 0;
            int movedDecks = 0;
            int movedPictures = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                MoveFrom(sources[i], gameId, GameContent.ArtFolderOf(fromGame), builtIn, fromGame, library,
                    intoCards, intoDecks, intoUploads, ref movedCards, ref movedDecks, ref movedPictures);
            }

            int repointed = RepointMadeGames(profiles, gameId, intoDecks);
            if (movedCards + movedDecks + movedPictures + repointed == 0) return;
            Debug.Log($"[Your cards] Moved {movedCards} card(s), {movedDecks} deck(s) and {movedPictures} picture(s) made in "
                + $"{fromGame.DisplayName} into Your cards; {repointed} made game(s) now use Your cards.");
        }

        private static void MoveFrom(
            ContentPackStore source,
            string gameId,
            string artFolder,
            HashSet<string> builtIn,
            GameDefinition fromGame,
            ContentPackStore library,
            PlayerCardsFile intoCards,
            PlayerDecksFile intoDecks,
            ContentUploadIndex intoUploads,
            ref int movedCards,
            ref int movedDecks,
            ref int movedPictures)
        {
            if (!Directory.Exists(source.GameFolder(gameId))) return;
            PlayerCardsFile cards = source.LoadCards(gameId);
            PlayerDecksFile decks = source.LoadDecks(gameId);
            List<PlayerCardRecord> cardsToMove = new List<PlayerCardRecord>();
            List<PlayerDeckRecord> decksToMove = new List<PlayerDeckRecord>();
            HashSet<string> owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // card IDs and "deck-<id>"
            for (int i = 0; i < cards.cards.Count; i++)
            {
                PlayerCardRecord record = cards.cards[i];
                string id = record != null ? (record.id ?? string.Empty).Trim() : string.Empty;
                if (!Guid.TryParse(id, out _) || builtIn.Contains(id)) continue;
                cardsToMove.Add(record);
                owners.Add(id);
            }

            for (int i = 0; i < decks.decks.Count; i++)
            {
                PlayerDeckRecord deck = decks.decks[i];
                if (deck == null || deck.stage > 0 || string.IsNullOrWhiteSpace(deck.id)) continue;
                decksToMove.Add(deck);
                owners.Add("deck-" + deck.id.Trim());
            }

            if (cardsToMove.Count == 0 && decksToMove.Count == 0) return;

            // Pictures the game's own card changes or its stage decks still use stay where they are (copied only).
            HashSet<string> stillUsed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < cards.changes.Count; i++)
            {
                CardChangeRecord change = cards.changes[i];
                if (change == null) continue;
                if (change.changeFrontArt) stillUsed.Add(ArtLibrary.NormalizeKey(change.frontArtKey));
                if (change.changeBackArt) stillUsed.Add(ArtLibrary.NormalizeKey(change.backArtKey));
            }

            for (int i = 0; i < decks.decks.Count; i++)
            {
                PlayerDeckRecord deck = decks.decks[i];
                if (deck == null || deck.stage <= 0) continue;
                stillUsed.Add(ArtLibrary.NormalizeKey(deck.backArtKey));
                for (int f = 0; deck.fronts != null && f < deck.fronts.Count; f++)
                {
                    if (deck.fronts[f] != null) stillUsed.Add(ArtLibrary.NormalizeKey(deck.fronts[f].frontArtKey));
                }
            }

            ContentUploadIndex fromUploads = source.LoadUploads(gameId);
            List<ContentArtFile> leaving = new List<ContentArtFile>();
            for (int i = 0; i < fromUploads.pictures.Count; i++)
            {
                ContentArtFile picture = fromUploads.pictures[i];
                if (picture == null || !TryRekey(picture.key, artFolder, owners, out string newKey)) continue;
                if (!HasUpload(intoUploads, newKey))
                {
                    string path = Path.Combine(source.UploadsFolder(gameId), picture.file ?? string.Empty);
                    if (!library.TryStoreUpload(PlayerLibrary.Id, newKey, path, out string fileName, out string error))
                    {
                        Debug.LogWarning($"[Your cards] A picture ({picture.file}) could not be moved into Your cards: {error}.");
                        continue;
                    }

                    intoUploads.pictures.Add(new ContentArtFile { key = newKey, file = fileName });
                    movedPictures++;
                }

                if (!stillUsed.Contains(ArtLibrary.NormalizeKey(picture.key))) leaving.Add(picture);
            }

            for (int i = 0; i < cardsToMove.Count; i++)
            {
                PlayerCardRecord record = cardsToMove[i];
                if (FindCard(intoCards, record.id) != null) continue; // already moved
                CardDefinition model = FindBuiltIn(fromGame, record.behavesLike);
                if (string.IsNullOrWhiteSpace(record.size) && model != null && model.SizeClass == CardSizeClass.Large)
                {
                    record.size = PlayerContent.LargeSize; // keeps the size it played at
                }

                record.behavesLike = string.Empty; // a plain card now
                record.frontArtKey = Rekeyed(record.frontArtKey, artFolder, owners);
                record.backArtKey = Rekeyed(record.backArtKey, artFolder, owners);
                intoCards.cards.Add(record);
                movedCards++;
            }

            for (int i = 0; i < decksToMove.Count; i++)
            {
                PlayerDeckRecord deck = decksToMove[i];
                if (FindDeck(intoDecks, deck.id) != null) continue;
                deck.backArtKey = Rekeyed(deck.backArtKey, artFolder, owners);
                intoDecks.decks.Add(deck);
                movedDecks++;
            }

            // The library is written first; the game's copy is changed only once that worked.
            bool saved = library.SaveCards(PlayerLibrary.Id, intoCards) & library.SaveDecks(PlayerLibrary.Id, intoDecks);
            library.SaveUploads(PlayerLibrary.Id, intoUploads);
            if (!saved)
            {
                Debug.LogWarning("[Your cards] Your cards could not be saved; the cards made in the game were left where they are.");
                return;
            }

            for (int i = 0; i < cardsToMove.Count; i++) cards.cards.Remove(cardsToMove[i]);
            for (int i = 0; i < decksToMove.Count; i++) decks.decks.Remove(decksToMove[i]);
            source.SaveCards(gameId, cards);
            source.SaveDecks(gameId, decks);
            for (int i = 0; i < leaving.Count; i++)
            {
                source.DeleteUpload(gameId, leaving[i].file);
                fromUploads.pictures.Remove(leaving[i]);
            }

            if (leaving.Count > 0) source.SaveUploads(gameId, fromUploads);
        }

        // A made game whose deck is now in the library (and not in the game any more) plays the library's deck.
        private static int RepointMadeGames(ProfileService profiles, string gameId, PlayerDecksFile libraryDecks)
        {
            if (libraryDecks.decks.Count == 0) return 0;
            PlayerGameStore store = PlayerGameStore.ForProfile(profiles);
            List<PlayerGameFile> games = store.List();
            int repointed = 0;
            for (int i = 0; i < games.Count; i++)
            {
                SavedGameDeck deck = games[i].deck;
                if (deck == null || !deck.IsSet || !string.Equals(deck.gameId, gameId, StringComparison.OrdinalIgnoreCase)) continue;
                if (FindDeck(libraryDecks, deck.deckId) == null) continue;
                deck.gameId = PlayerLibrary.Id;
                if (store.Save(games[i], out string error)) repointed++;
                else Debug.LogWarning($"[Your cards] A made game could not be pointed at Your cards: {error}.");
            }

            return repointed;
        }

        // "trap-floor/player/<moved id>/front" becomes "your-cards/player/<moved id>/front"; other keys are kept.
        private static bool TryRekey(string key, string artFolder, HashSet<string> owners, out string newKey)
        {
            newKey = string.Empty;
            string normalized = ArtLibrary.NormalizeKey(key);
            string prefix = (artFolder ?? string.Empty).ToLowerInvariant() + "/player/";
            if (normalized.Length <= prefix.Length || !normalized.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string rest = normalized.Substring(prefix.Length);
            int slash = rest.IndexOf('/');
            string owner = slash > 0 ? rest.Substring(0, slash) : rest;
            if (!owners.Contains(owner)) return false;
            newKey = PlayerLibrary.ArtFolder + "/player/" + rest;
            return true;
        }

        private static string Rekeyed(string key, string artFolder, HashSet<string> owners) =>
            TryRekey(key, artFolder, owners, out string newKey) ? newKey : key ?? string.Empty;

        private static bool HasUpload(ContentUploadIndex index, string key)
        {
            for (int i = 0; i < index.pictures.Count; i++)
            {
                if (index.pictures[i] != null && ArtLibrary.NormalizeKey(index.pictures[i].key) == key) return true;
            }

            return false;
        }

        private static PlayerCardRecord FindCard(PlayerCardsFile file, string id)
        {
            for (int i = 0; i < file.cards.Count; i++)
            {
                if (file.cards[i] != null && string.Equals(file.cards[i].id, id, StringComparison.OrdinalIgnoreCase)) return file.cards[i];
            }

            return null;
        }

        private static PlayerDeckRecord FindDeck(PlayerDecksFile file, string id)
        {
            for (int i = 0; i < file.decks.Count; i++)
            {
                if (file.decks[i] != null && string.Equals(file.decks[i].id, id, StringComparison.OrdinalIgnoreCase)) return file.decks[i];
            }

            return null;
        }

        private static CardDefinition FindBuiltIn(GameDefinition game, string id)
        {
            for (int s = 0; s < game.ContentSets.Count; s++)
            {
                GameContentSet set = game.ContentSets[s];
                for (int c = 0; set != null && c < set.Cards.Count; c++)
                {
                    if (set.Cards[c] != null && string.Equals(set.Cards[c].StableId, id, StringComparison.OrdinalIgnoreCase)) return set.Cards[c];
                }
            }

            return null;
        }
    }
}
