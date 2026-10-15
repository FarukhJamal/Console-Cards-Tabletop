using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Art;
using UnityEngine;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>One replaceable picture of the active game, for the Card Art panel.</summary>
    public sealed class ContentArtEntry
    {
        public ContentArtEntry(string key, string label, Texture2D builtIn)
        {
            Key = key;
            Label = label;
            BuiltIn = builtIn;
        }

        public string Key { get; }
        public string Label { get; }
        public Texture2D BuiltIn { get; }
    }

    /// <summary>
    /// The content system's single entry point (doc 24): it owns the art lookup (A2), knows the active game's
    /// replaceable pictures, activates game packs (A3) and keeps the player's own pictures (A4). The table only
    /// asks it for pictures by key; where a picture came from never reaches gameplay.
    /// </summary>
    public sealed class ContentArtService
    {
        private readonly List<ContentArtEntry> entries = new List<ContentArtEntry>();
        private readonly HashSet<string> entryKeys = new HashSet<string>(StringComparer.Ordinal);
        private List<ContentPackInfo> packs = new List<ContentPackInfo>();
        private ContentUploadIndex uploads = new ContentUploadIndex();
        private GameDefinition game;

        public ContentArtService(ContentPackStore store)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ArtLibrary Library { get; } = new ArtLibrary();
        public ContentPackStore Store { get; }
        public string GameId { get; private set; } = string.Empty;
        public IReadOnlyList<ContentArtEntry> Entries => entries;
        public IReadOnlyList<ContentPackInfo> Packs => packs;
        public string ActivePackId { get; private set; } = string.Empty;
        public bool HasGame => game != null;

        public string ActivePackName
        {
            get
            {
                ContentPackInfo active = FindPack(ActivePackId);
                return active != null ? active.DisplayName : "Built-in";
            }
        }

        /// <summary>The active pack, or null for built-in only.</summary>
        public ContentPackInfo ActivePack => FindPack(ActivePackId);

        /// <summary>
        /// The cards to play with (doc 27 L2-1): the active pack's cards with the player's own on top (same ID: the
        /// player's wins). Read from the profile each call; call it when a table starts, not every frame.
        /// </summary>
        public PlayerCardsFile LoadPlayerCards()
        {
            if (!HasGame) return new PlayerCardsFile();
            ContentPackInfo pack = ActivePack;
            return PlayerCardsFile.Merge(pack != null ? pack.Manifest.cards : null, Store.LoadCards(GameId));
        }

        /// <summary>The decks: the active pack's with the player's own on top, as <see cref="LoadPlayerCards"/>.</summary>
        public PlayerDecksFile LoadPlayerDecks()
        {
            if (!HasGame) return new PlayerDecksFile();
            ContentPackInfo pack = ActivePack;
            return PlayerDecksFile.Merge(pack != null ? pack.Manifest.decks : null, Store.LoadDecks(GameId));
        }

        /// <summary>
        /// Switches to a game: registers its built-in art, lists its pictures, and loads its uploads and active pack.
        /// Does nothing when the game is already in use.
        /// </summary>
        public void UseGame(GameDefinition definition)
        {
            if (ReferenceEquals(game, definition))
            {
                return;
            }

            game = definition;
            GameId = definition != null ? definition.StableId ?? string.Empty : string.Empty;
            Library.BeginBatch();
            try
            {
                SwitchGame(definition);
            }
            finally
            {
                Library.EndBatch();
            }
        }

        private void SwitchGame(GameDefinition definition)
        {
            Library.ClearLayer(ArtLayer.PlayerUpload);
            Library.ClearLayer(ArtLayer.GamePack);
            Library.ClearBuiltIn();
            entries.Clear();
            entryKeys.Clear();
            packs.Clear();
            ActivePackId = string.Empty;
            uploads = new ContentUploadIndex();
            if (definition == null)
            {
                return;
            }

            RegisterBuiltIn(definition);
            ReloadFromDisk();
        }

        /// <summary>Reads packs, the active pack and the uploads again (after files were added by hand).</summary>
        public void ReloadFromDisk()
        {
            if (game == null) return;
            Library.BeginBatch();
            try
            {
                packs = Store.ListPacks(GameId);
                uploads = Store.LoadUploads(GameId);
                LoadUploadLayer();
                string active = Store.LoadSettings(GameId).activePackId;
                ActivatePack(FindPack(active) != null ? active : string.Empty, out _);
            }
            finally
            {
                Library.EndBatch();
            }
        }

        /// <summary>Makes a pack active (empty id: built-in only) and remembers the choice for this game.</summary>
        public bool ActivatePack(string packId, out string message)
        {
            Library.BeginBatch();
            try
            {
                return ActivatePackInBatch(packId, out message);
            }
            finally
            {
                Library.EndBatch();
            }
        }

        private bool ActivatePackInBatch(string packId, out string message)
        {
            Library.ClearLayer(ArtLayer.GamePack);
            ActivePackId = string.Empty;
            ContentPackInfo pack = FindPack(packId);
            if (pack != null)
            {
                int loaded = 0;
                int failed = 0;
                for (int i = 0; i < pack.Manifest.art.Count; i++)
                {
                    ContentArtFile file = pack.Manifest.art[i];
                    if (LoadInto(ArtLayer.GamePack, pack.Folder, file, out string error))
                    {
                        loaded++;
                    }
                    else
                    {
                        failed++;
                        Debug.LogWarning($"[Content] Pack '{pack.DisplayName}': {file.key}: {error}");
                    }
                }

                ActivePackId = pack.Id;
                message = failed == 0
                    ? $"Art pack '{pack.DisplayName}' is on ({loaded} pictures)."
                    : $"Art pack '{pack.DisplayName}' is on ({loaded} pictures, {failed} could not be read).";
            }
            else
            {
                message = "Built-in art is on.";
            }

            if (game != null)
            {
                Store.SaveSettings(GameId, new ContentSettings { activePackId = ActivePackId });
            }

            return pack != null || string.IsNullOrEmpty(packId);
        }

        /// <summary>The pack after (step 1) or before (step -1) the active one; built-in sits between the ends.</summary>
        public string NeighbourPackId(int step)
        {
            int count = packs.Count + 1; // index 0 is built-in
            int current = 0;
            for (int i = 0; i < packs.Count; i++)
            {
                if (packs[i].Id == ActivePackId) current = i + 1;
            }

            int next = ((current + step) % count + count) % count;
            return next == 0 ? string.Empty : packs[next - 1].Id;
        }

        /// <summary>Uses the player's own picture for a key (copied into this game's uploads folder).</summary>
        public bool SetUpload(string key, string sourcePath, out string message)
        {
            string normalized = ArtLibrary.NormalizeKey(key);
            if (game == null || normalized.Length == 0)
            {
                message = "There is no picture to replace here.";
                return false;
            }

            if (!Store.TryStoreUpload(GameId, normalized, sourcePath, out string fileName, out string error))
            {
                message = $"That picture could not be used: {error}.";
                return false;
            }

            ContentArtFile entry = FindUpload(normalized);
            if (entry != null && entry.file != fileName)
            {
                Store.DeleteUpload(GameId, entry.file); // the same key saved earlier with another file type
            }

            if (entry == null)
            {
                entry = new ContentArtFile { key = normalized };
                uploads.pictures.Add(entry);
            }

            entry.file = fileName;
            if (!LoadInto(ArtLayer.PlayerUpload, Store.UploadsFolder(GameId), entry, out error))
            {
                uploads.pictures.Remove(entry);
                Store.DeleteUpload(GameId, fileName);
                Store.SaveUploads(GameId, uploads);
                message = $"That picture could not be read: {error}.";
                return false;
            }

            Store.SaveUploads(GameId, uploads);
            message = $"Your picture is now used for {LabelOf(normalized)}.";
            return true;
        }

        /// <summary>Removes the player's own picture for a key; the pack or built-in picture shows again.</summary>
        public bool RemoveUpload(string key, out string message)
        {
            string normalized = ArtLibrary.NormalizeKey(key);
            ContentArtFile entry = FindUpload(normalized);
            if (entry == null)
            {
                message = "There is no picture of yours to remove.";
                return false;
            }

            uploads.pictures.Remove(entry);
            Library.Remove(ArtLayer.PlayerUpload, normalized);
            Store.DeleteUpload(GameId, entry.file);
            Store.SaveUploads(GameId, uploads);
            message = $"{LabelOf(normalized)} uses its {(ActivePackId.Length > 0 ? "pack" : "built-in")} picture again.";
            return true;
        }

        public bool HasUpload(string key) => FindUpload(ArtLibrary.NormalizeKey(key)) != null;

        /// <summary>Where the picture for this key comes from now, in words for the panel.</summary>
        public string SourceText(string key)
        {
            if (!Library.TryResolve(key, out _, out ArtLayer layer)) return "No picture";
            switch (layer)
            {
                case ArtLayer.PlayerUpload: return "Your picture";
                case ArtLayer.GamePack: return $"Pack: {ActivePackName}";
                default: return "Built-in";
            }
        }

        public Texture2D Resolve(string key, Texture2D fallback) => Library.Resolve(key, fallback);

        public void Dispose()
        {
            Library.Dispose();
            game = null;
        }

        private void RegisterBuiltIn(GameDefinition definition)
        {
            IReadOnlyList<GameContentSet> sets = definition.ContentSets;
            for (int setIndex = 0; sets != null && setIndex < sets.Count; setIndex++)
            {
                IReadOnlyList<CardDefinition> cards = sets[setIndex] != null ? sets[setIndex].Cards : null;
                for (int i = 0; cards != null && i < cards.Count; i++)
                {
                    CardDefinition card = cards[i];
                    if (card == null) continue;
                    AddEntry(card.FrontArtworkReference, card.DisplayName, card.FrontArtwork);
                    AddEntry(card.BackArtworkReference, $"{card.DisplayName} (back)", card.BackArtwork);
                }
            }

            IReadOnlyList<AvatarDefinition> avatars = definition.Avatars;
            for (int i = 0; avatars != null && i < avatars.Count; i++)
            {
                AvatarDefinition avatar = avatars[i];
                if (avatar != null) AddEntry(avatar.ArtworkReference, $"{AvatarName(avatar)} (avatar)", avatar.Artwork);
            }

            IReadOnlyList<Texture2D> backs = definition.BoardCardBacks;
            IReadOnlyList<string> keys = definition.BoardCardBackReferences;
            for (int i = 0; keys != null && i < keys.Count; i++)
            {
                AddEntry(keys[i], $"Floor back, round {i + 1}", backs != null && i < backs.Count ? backs[i] : null);
            }
        }

        private void AddEntry(string key, string label, Texture2D builtIn)
        {
            string normalized = ArtLibrary.NormalizeKey(key);
            if (normalized.Length == 0) return;
            Library.RegisterBuiltIn(normalized, builtIn);
            if (entryKeys.Add(normalized))
            {
                entries.Add(new ContentArtEntry(normalized, label, builtIn));
            }
        }

        private void LoadUploadLayer()
        {
            Library.ClearLayer(ArtLayer.PlayerUpload);
            string folder = Store.UploadsFolder(GameId);
            for (int i = uploads.pictures.Count - 1; i >= 0; i--)
            {
                ContentArtFile file = uploads.pictures[i];
                if (!LoadInto(ArtLayer.PlayerUpload, folder, file, out string error))
                {
                    Debug.LogWarning($"[Content] Your picture for {file.key}: {error}");
                }
            }
        }

        private bool LoadInto(ArtLayer layer, string folder, ContentArtFile file, out string error)
        {
            string key = ArtLibrary.NormalizeKey(file != null ? file.key : null);
            if (key.Length == 0)
            {
                error = "the entry has no key";
                return false;
            }

            byte[] bytes = ContentPackStore.ReadPicture(folder, file.file, out error);
            if (bytes == null) return false;
            Texture2D texture = ArtLibrary.LoadTexture(bytes, key);
            if (texture == null)
            {
                error = $"'{file.file}' is not a PNG or JPG picture";
                return false;
            }

            Library.Set(layer, key, texture, true);
            return true;
        }

        private ContentPackInfo FindPack(string packId)
        {
            if (string.IsNullOrEmpty(packId)) return null;
            for (int i = 0; i < packs.Count; i++)
            {
                if (string.Equals(packs[i].Id, packId, StringComparison.Ordinal)) return packs[i];
            }

            return null;
        }

        private ContentArtFile FindUpload(string normalizedKey)
        {
            for (int i = 0; i < uploads.pictures.Count; i++)
            {
                if (ArtLibrary.NormalizeKey(uploads.pictures[i].key) == normalizedKey) return uploads.pictures[i];
            }

            return null;
        }

        private string LabelOf(string normalizedKey)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key == normalizedKey) return entries[i].Label;
            }

            return Path.GetFileNameWithoutExtension(normalizedKey);
        }

        private static string AvatarName(AvatarDefinition avatar)
        {
            string name = avatar.DisplayName;
            return string.IsNullOrEmpty(name) ? "Avatar" : name;
        }
    }
}
