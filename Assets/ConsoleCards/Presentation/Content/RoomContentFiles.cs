using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ConsoleCards.Presentation.Content
{
    /// <summary>
    /// ROOM-MEM (owner, 2026-11-27): a host's cards, decks, pictures and rules are for the session only. A computer that
    /// joins a room keeps what the host sends here, in memory, never on its disk; it is gone when the player leaves
    /// the room or closes the game. The files are keyed by their path under the host's games folder ('/' separators,
    /// case does not matter). <see cref="Root"/> is a virtual folder that is never made on disk: the stores read
    /// "files" under it through <see cref="ContentIO"/>.
    /// </summary>
    public static class RoomContentFiles
    {
        private const string RootName = "RoomContent-memory";

        private static readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private static string root;
        private static string rootFull;

        /// <summary>The room's virtual folder (ContentPackStore.RoomRoot while in a room). Never created on disk.</summary>
        public static string Root
        {
            get
            {
                if (root == null) root = Path.Combine(UnityEngine.Application.temporaryCachePath, RootName);
                return root;
            }
        }

        private static string RootFull
        {
            get
            {
                if (rootFull == null) rootFull = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return rootFull;
            }
        }

        public static int Count => files.Count;

        public static long TotalBytes
        {
            get
            {
                long total = 0;
                foreach (KeyValuePair<string, byte[]> pair in files) total += pair.Value.Length;
                return total;
            }
        }

        /// <summary>Replaces the room's files with these (relative path to bytes). The arrays are kept, not copied.</summary>
        public static void Set(Dictionary<string, byte[]> source)
        {
            files.Clear();
            if (source == null) return;
            foreach (KeyValuePair<string, byte[]> pair in source)
            {
                string key = NormaliseRelative(pair.Key);
                if (key.Length == 0 || pair.Value == null) continue;
                files[key] = pair.Value;
            }
        }

        public static void Clear() => files.Clear();

        /// <summary>The path as a key: '/' separators, no leading or trailing '/'.</summary>
        public static string NormaliseRelative(string relative) =>
            string.IsNullOrEmpty(relative) ? string.Empty : relative.Replace('\\', '/').Trim('/');

        /// <summary>True when the path is the room's folder or inside it (whether or not such a file is held).</summary>
        public static bool Owns(string path) => TryKey(path, out _);

        public static bool FileExists(string path) => TryKey(path, out string key) && key.Length > 0 && files.ContainsKey(key);

        public static bool DirectoryExists(string path)
        {
            if (!TryKey(path, out string key)) return false;
            if (key.Length == 0) return files.Count > 0;
            string prefix = key + "/";
            foreach (string held in files.Keys)
            {
                if (held.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        /// <summary>The held bytes (not a copy: do not change them). False when there is no such file.</summary>
        public static bool TryReadAllBytes(string path, out byte[] bytes)
        {
            bytes = null;
            return TryKey(path, out string key) && key.Length > 0 && files.TryGetValue(key, out bytes);
        }

        /// <summary>The file as text (UTF-8, a leading byte order mark dropped), as File.ReadAllText.</summary>
        public static string ReadAllText(string path)
        {
            if (!TryReadAllBytes(path, out byte[] bytes)) throw new FileNotFoundException("not in the host's content");
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            return Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
        }

        /// <summary>Full paths of the files in a folder (and below it when recursive), as Directory.GetFiles; sorted.</summary>
        public static string[] GetFiles(string directory, bool recursive, string pattern = "*")
        {
            List<string> found = new List<string>();
            if (!TryKey(directory, out string key)) return found.ToArray();
            string prefix = key.Length == 0 ? string.Empty : key + "/";
            foreach (string held in files.Keys)
            {
                if (!held.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string rest = held.Substring(prefix.Length);
                if (!recursive && rest.IndexOf('/') >= 0) continue;
                string name = rest.Substring(rest.LastIndexOf('/') + 1);
                if (!Matches(name, pattern)) continue;
                found.Add(Path.Combine(directory, rest.Replace('/', Path.DirectorySeparatorChar)));
            }

            found.Sort(StringComparer.Ordinal);
            return found.ToArray();
        }

        /// <summary>Full paths of the folders directly inside a folder, as Directory.GetDirectories; sorted.</summary>
        public static string[] GetDirectories(string directory)
        {
            List<string> found = new List<string>();
            if (!TryKey(directory, out string key)) return found.ToArray();
            string prefix = key.Length == 0 ? string.Empty : key + "/";
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string held in files.Keys)
            {
                if (!held.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string rest = held.Substring(prefix.Length);
                int slash = rest.IndexOf('/');
                if (slash <= 0) continue;
                string name = rest.Substring(0, slash);
                if (seen.Add(name)) found.Add(Path.Combine(directory, name));
            }

            found.Sort(StringComparer.Ordinal);
            return found.ToArray();
        }

        // "*", "*.*", "*.ext" or an exact name: all the content stores need.
        private static bool Matches(string name, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "*" || pattern == "*.*") return true;
            if (pattern[0] == '*' && pattern.IndexOf('*', 1) < 0 && pattern.IndexOf('?') < 0)
                return name.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
            return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
        }

        // The path under Root as a key ("" for Root itself); false when the path is somewhere else.
        private static bool TryKey(string path, out string key)
        {
            key = null;
            if (string.IsNullOrEmpty(path)) return false;
            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return false;
            }

            string top = RootFull;
            if (full.Length < top.Length || string.Compare(full, 0, top, 0, top.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
            if (full.Length == top.Length)
            {
                key = string.Empty;
                return true;
            }

            char next = full[top.Length];
            if (next != Path.DirectorySeparatorChar && next != Path.AltDirectorySeparatorChar) return false;
            key = NormaliseRelative(full.Substring(top.Length + 1));
            return true;
        }
    }

    /// <summary>
    /// ROOM-MEM (owner, 2026-11-27): File and Directory calls for the content stores. A path under
    /// <see cref="RoomContentFiles.Root"/> is read from the room's memory; any other path from the disk as before. A
    /// write, copy or delete under the room's folder is refused (<see cref="Refuse"/>): the host's content is never
    /// kept here.
    /// </summary>
    public static class ContentIO
    {
        /// <summary>What the player is told when something would keep the host's content.</summary>
        public const string ReadOnlyMessage = "The host's cards stay with the host.";

        public static bool IsRoomPath(string path) => RoomContentFiles.Owns(path);

        /// <summary>True (with one console warning) when <paramref name="path"/> is the room's; the caller then does nothing.</summary>
        public static bool Refuse(string path)
        {
            if (!RoomContentFiles.Owns(path)) return false;
            Debug.LogWarning("[Content] The host's content is read-only on this computer.");
            return true;
        }

        public static bool FileExists(string path) =>
            RoomContentFiles.Owns(path) ? RoomContentFiles.FileExists(path) : File.Exists(path);

        public static bool DirectoryExists(string path) =>
            RoomContentFiles.Owns(path) ? RoomContentFiles.DirectoryExists(path) : Directory.Exists(path);

        /// <summary>The file's size; false when there is no such file.</summary>
        public static bool TryGetLength(string path, out long length)
        {
            length = 0;
            if (RoomContentFiles.Owns(path))
            {
                if (!RoomContentFiles.TryReadAllBytes(path, out byte[] held)) return false;
                length = held.Length;
                return true;
            }

            FileInfo info = new FileInfo(path);
            if (!info.Exists) return false;
            length = info.Length;
            return true;
        }

        public static byte[] ReadAllBytes(string path)
        {
            if (!RoomContentFiles.Owns(path)) return File.ReadAllBytes(path);
            if (!RoomContentFiles.TryReadAllBytes(path, out byte[] bytes)) throw new FileNotFoundException("not in the host's content");
            return bytes;
        }

        public static string ReadAllText(string path) =>
            RoomContentFiles.Owns(path) ? RoomContentFiles.ReadAllText(path) : File.ReadAllText(path);

        public static string[] GetFiles(string directory, string pattern, SearchOption option) =>
            RoomContentFiles.Owns(directory)
                ? RoomContentFiles.GetFiles(directory, option == SearchOption.AllDirectories, pattern)
                : Directory.GetFiles(directory, pattern, option);

        public static string[] GetDirectories(string directory) =>
            RoomContentFiles.Owns(directory) ? RoomContentFiles.GetDirectories(directory) : Directory.GetDirectories(directory);
    }
}
