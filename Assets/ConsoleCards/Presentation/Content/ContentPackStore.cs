using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>
    /// Where content lives on this computer (doc 24): one folder per game under the persistent data folder,
    /// holding packs/&lt;pack&gt;/pack.json (A3), uploads/ with the player's own pictures (A4) and settings.json.
    /// Plain files only; every path read from a manifest is kept inside its own folder.
    /// </summary>
    public sealed class ContentPackStore
    {
        public const long MaximumPictureBytes = 16L * 1024L * 1024L;
        private const string PacksFolderName = "packs";
        private const string UploadsFolderName = "uploads";

        public ContentPackStore(string rootFolder)
        {
            RootFolder = rootFolder ?? throw new ArgumentNullException(nameof(rootFolder));
        }

        /// <summary>The default store: &lt;persistent data&gt;/Content.</summary>
        public static ContentPackStore CreateDefault() =>
            new ContentPackStore(Path.Combine(UnityEngine.Application.persistentDataPath, "Content"));

        public string RootFolder { get; }

        public string GameFolder(string gameId) => Path.Combine(RootFolder, SafeName(gameId, "game"));

        public string PacksFolder(string gameId) => Path.Combine(GameFolder(gameId), PacksFolderName);

        public string UploadsFolder(string gameId) => Path.Combine(GameFolder(gameId), UploadsFolderName);

        /// <summary>Every readable pack for this game, sorted by name. Broken packs are skipped with a warning.</summary>
        public List<ContentPackInfo> ListPacks(string gameId)
        {
            List<ContentPackInfo> packs = new List<ContentPackInfo>();
            string folder = PacksFolder(gameId);
            if (!Directory.Exists(folder))
            {
                return packs;
            }

            foreach (string packFolder in Directory.GetDirectories(folder))
            {
                if (TryReadManifest(packFolder, out ContentPackManifest manifest, out string error))
                {
                    if (!string.IsNullOrEmpty(manifest.gameStableId)
                        && !string.Equals(manifest.gameStableId, gameId, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning($"[Content] {packFolder} is for another game ({manifest.gameStableId}); skipped.");
                        continue;
                    }

                    packs.Add(new ContentPackInfo(packFolder, manifest));
                }
                else
                {
                    Debug.LogWarning($"[Content] {packFolder}: {error}");
                }
            }

            packs.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            return packs;
        }

        public static bool TryReadManifest(string packFolder, out ContentPackManifest manifest, out string error)
        {
            manifest = null;
            string path = Path.Combine(packFolder, ContentPackManifest.FileName);
            if (!File.Exists(path))
            {
                error = $"no {ContentPackManifest.FileName}";
                return false;
            }

            try
            {
                manifest = JsonUtility.FromJson<ContentPackManifest>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                error = $"{ContentPackManifest.FileName} could not be read ({exception.Message})";
                return false;
            }

            if (manifest == null)
            {
                error = $"{ContentPackManifest.FileName} is empty";
                return false;
            }

            if (manifest.formatVersion > ContentPackManifest.CurrentFormatVersion)
            {
                error = $"made for a newer version (format {manifest.formatVersion})";
                return false;
            }

            if (string.IsNullOrWhiteSpace(manifest.packId))
            {
                manifest.packId = Path.GetFileName(packFolder);
            }

            if (manifest.art == null)
            {
                manifest.art = new List<ContentArtFile>();
            }

            error = string.Empty;
            return true;
        }

        public ContentSettings LoadSettings(string gameId) =>
            ReadJson(Path.Combine(GameFolder(gameId), ContentSettings.FileName), new ContentSettings());

        public void SaveSettings(string gameId, ContentSettings settings) =>
            WriteJson(Path.Combine(GameFolder(gameId), ContentSettings.FileName), settings);

        public ContentUploadIndex LoadUploads(string gameId)
        {
            ContentUploadIndex index = ReadJson(
                Path.Combine(UploadsFolder(gameId), ContentUploadIndex.FileName),
                new ContentUploadIndex());
            if (index.pictures == null) index.pictures = new List<ContentArtFile>();
            return index;
        }

        public void SaveUploads(string gameId, ContentUploadIndex index) =>
            WriteJson(Path.Combine(UploadsFolder(gameId), ContentUploadIndex.FileName), index);

        /// <summary>Copies a picture into this game's uploads folder under a name made from the key.</summary>
        public bool TryStoreUpload(string gameId, string key, string sourcePath, out string fileName, out string error)
        {
            fileName = string.Empty;
            string extension = Path.GetExtension(sourcePath ?? string.Empty).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            {
                error = "only PNG or JPG pictures can be used";
                return false;
            }

            try
            {
                FileInfo source = new FileInfo(sourcePath);
                if (!source.Exists)
                {
                    error = "the picture was not found";
                    return false;
                }

                if (source.Length > MaximumPictureBytes)
                {
                    error = "the picture is larger than 16 MB";
                    return false;
                }

                string folder = UploadsFolder(gameId);
                Directory.CreateDirectory(folder);
                fileName = SafeName(key, "picture") + extension;
                File.Copy(sourcePath, Path.Combine(folder, fileName), true);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void DeleteUpload(string gameId, string fileName)
        {
            if (!TryResolveInside(UploadsFolder(gameId), fileName, out string path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Content] Could not delete {path}: {exception.Message}");
            }
        }

        /// <summary>Reads a picture file that must lie inside <paramref name="folder"/>; null with a reason otherwise.</summary>
        public static byte[] ReadPicture(string folder, string relativeFile, out string error)
        {
            if (!TryResolveInside(folder, relativeFile, out string path))
            {
                error = $"'{relativeFile}' is outside its folder";
                return null;
            }

            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists)
                {
                    error = $"'{relativeFile}' is missing";
                    return null;
                }

                if (info.Length > MaximumPictureBytes)
                {
                    error = $"'{relativeFile}' is larger than 16 MB";
                    return null;
                }

                error = string.Empty;
                return File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return null;
            }
        }

        /// <summary>Writes a pack folder: pack.json plus each picture copied to art/&lt;key&gt;.</summary>
        public static void WritePack(
            string packFolder,
            ContentPackManifest manifest,
            IReadOnlyList<KeyValuePair<string, string>> keyToSourceFile)
        {
            Directory.CreateDirectory(packFolder);
            manifest.art = new List<ContentArtFile>(keyToSourceFile.Count);
            for (int i = 0; i < keyToSourceFile.Count; i++)
            {
                string key = keyToSourceFile[i].Key;
                string relative = "art/" + key;
                string destination = Path.Combine(packFolder, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(keyToSourceFile[i].Value, destination, true);
                manifest.art.Add(new ContentArtFile { key = key, file = relative });
            }

            File.WriteAllText(Path.Combine(packFolder, ContentPackManifest.FileName), JsonUtility.ToJson(manifest, true));
        }

        /// <summary>Copies a pack folder into this game's packs. False with a reason when it is not a pack.</summary>
        public bool TryImportPackFolder(string gameId, string sourceFolder, out string packId, out string error)
        {
            packId = string.Empty;
            if (!TryReadManifest(sourceFolder, out ContentPackManifest manifest, out error))
            {
                return false;
            }

            try
            {
                packId = manifest.packId;
                string destination = Path.Combine(PacksFolder(gameId), SafeName(manifest.packId, "pack"));
                CopyFolder(sourceFolder, destination);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>Lower case letters, digits, '.', '-' and '_' only; '/' becomes "__".</summary>
        public static string SafeName(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            char[] buffer = new char[value.Length * 2];
            int length = 0;
            string lower = value.Trim().ToLowerInvariant();
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                if (c == '/' || c == '\\')
                {
                    buffer[length++] = '_';
                    buffer[length++] = '_';
                }
                else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_')
                {
                    buffer[length++] = c;
                }
                else
                {
                    buffer[length++] = '-';
                }
            }

            string result = new string(buffer, 0, length).Trim('.');
            return result.Length == 0 ? fallback : result;
        }

        private static bool TryResolveInside(string folder, string relative, out string path)
        {
            path = string.Empty;
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) return false;
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
            path = full;
            return true;
        }

        private static void CopyFolder(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }

            foreach (string folder in Directory.GetDirectories(source))
            {
                CopyFolder(folder, Path.Combine(destination, Path.GetFileName(folder)));
            }
        }

        private static T ReadJson<T>(string path, T fallback) where T : class
        {
            try
            {
                if (!File.Exists(path)) return fallback;
                T value = JsonUtility.FromJson<T>(File.ReadAllText(path));
                return value ?? fallback;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Content] {path} could not be read: {exception.Message}");
                return fallback;
            }
        }

        private static void WriteJson(string path, object value)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(value, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Content] {path} could not be saved: {exception.Message}");
            }
        }
    }
}
