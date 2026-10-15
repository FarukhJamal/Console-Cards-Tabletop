using System;
using System.IO;
using UnityEngine;

namespace ConsoleCards.Presentation.Saving
{
    /// <summary>
    /// The one place that reads and writes save files (doc 28). Every store (profiles, cards, decks, rules, pictures,
    /// hints) goes through here, so saving behaves the same everywhere: JSON made by Unity's JsonUtility, and a write
    /// goes to a temporary file first and then replaces the old file, so a crash while saving never leaves half a file.
    /// Never call these every frame; saving happens when a player changes something.
    /// </summary>
    public static class SaveFiles
    {
        private const string TemporarySuffix = ".saving";

        /// <summary>
        /// The saved object, or null when there is no file yet (no warning) or it cannot be read (a warning names the
        /// file; the file is left as it is).
        /// </summary>
        public static T ReadJson<T>(string path, string area) where T : class
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{area}] {path} could not be read: {exception.Message}");
                return null;
            }
        }

        /// <summary>Saves <paramref name="value"/> as JSON, safely (see the class summary). False with a reason on failure.</summary>
        public static bool TryWriteJson(string path, object value, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(path) || value == null)
            {
                error = "nothing to save";
                return false;
            }

            string temporary = path + TemporarySuffix;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temporary, JsonUtility.ToJson(value, true));
                if (!File.Exists(path))
                {
                    File.Move(temporary, path);
                    return true;
                }

                try
                {
                    File.Replace(temporary, path, null);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(temporary, path, true);
                    File.Delete(temporary);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch (Exception)
                {
                    // The temporary file is harmless; the next save replaces it.
                }

                return false;
            }
        }

        /// <summary>As <see cref="TryWriteJson"/>, with a console warning on failure.</summary>
        public static bool WriteJson(string path, object value, string area)
        {
            if (TryWriteJson(path, value, out string error)) return true;
            Debug.LogWarning($"[{area}] {path} could not be saved: {error}");
            return false;
        }

        /// <summary>Copies a file into the saves (for example a picture the player chose). False with a reason.</summary>
        public static bool TryCopyFile(string source, string destination, out string error)
        {
            error = string.Empty;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, true);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>Copies a whole folder, keeping files already at the destination (used once, to bring old saves in).</summary>
        public static void CopyFolderKeepingExisting(string source, string destination)
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                if (!File.Exists(target)) File.Copy(file, target);
            }

            foreach (string folder in Directory.GetDirectories(source))
            {
                CopyFolderKeepingExisting(folder, Path.Combine(destination, Path.GetFileName(folder)));
            }
        }

        /// <summary>True when files can be written in <paramref name="folder"/> (it is made if missing).</summary>
        public static bool CanWriteIn(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string probe = Path.Combine(folder, ".write-check" + TemporarySuffix);
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A name safe for a file or folder: lower case letters, digits, '.', '-' and '_'; '/' becomes "__".</summary>
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
    }
}
