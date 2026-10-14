using System;
using System.Collections.Generic;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>
    /// A game pack (doc 24, A3): a folder with this manifest as pack.json and its pictures beside it. Version 1
    /// carries art: each entry puts one picture file under one art key. Serialised with Unity's JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class ContentPackManifest
    {
        public const string FileName = "pack.json";
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public string packId = string.Empty;
        public string gameStableId = string.Empty;
        public string displayName = string.Empty;
        public string author = string.Empty;
        public string description = string.Empty;
        public List<ContentArtFile> art = new List<ContentArtFile>();
    }

    /// <summary>One picture: an art key and its file, relative to the pack (or uploads) folder.</summary>
    [Serializable]
    public sealed class ContentArtFile
    {
        public string key = string.Empty;
        public string file = string.Empty;
    }

    /// <summary>The player's own pictures for one game (doc 24, A4), kept as uploads/uploads.json.</summary>
    [Serializable]
    public sealed class ContentUploadIndex
    {
        public const string FileName = "uploads.json";

        public List<ContentArtFile> pictures = new List<ContentArtFile>();
    }

    /// <summary>Per-game content choices on this computer: which pack is active (empty: built-in only).</summary>
    [Serializable]
    public sealed class ContentSettings
    {
        public const string FileName = "settings.json";

        public string activePackId = string.Empty;
    }

    /// <summary>A pack found in the store, ready to list and activate.</summary>
    public sealed class ContentPackInfo
    {
        public ContentPackInfo(string folder, ContentPackManifest manifest)
        {
            Folder = folder;
            Manifest = manifest;
        }

        public string Folder { get; }
        public ContentPackManifest Manifest { get; }
        public string Id => Manifest.packId;
        public string DisplayName => string.IsNullOrWhiteSpace(Manifest.displayName) ? Manifest.packId : Manifest.displayName;
    }
}
