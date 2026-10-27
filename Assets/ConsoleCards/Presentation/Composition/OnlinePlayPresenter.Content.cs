using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ConsoleCards.Presentation.Content;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// MP-4 (owner, 2026-10-27): the host's content is the room's. The host sends its game saves (every game's cards,
    /// decks, pictures, packs, house rules, saved layouts and settings: the profile's games/ folder) to each joined
    /// computer once, and again when they change; a joined computer keeps them in a temporary folder for the room
    /// (<see cref="ContentPackStore.RoomRoot"/>), never in its own profile, and reads them instead of its own while it is
    /// in the room. A table from the host says which content it was dealt with; a joined computer waits for that
    /// content before it deals the same table. Leaving the room goes back to the profile's own saves.
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private const int ContentFormat = 1;
        private const long MaxContentBytes = 48L * 1024L * 1024L;
        private const long MaxContentFileBytes = 16L * 1024L * 1024L;
        private const int MaxContentFiles = 4000;
        private const string RoomContentFolder = "RoomContent";

        private string hostContentHash = string.Empty; // host: the content its tables are dealt with (from its bytes)
        private string hostContentFiles = string.Empty; // host: names, sizes and times of the files last bundled
        private byte[] hostContentBundle;
        private string roomContentHash = string.Empty; // joined computer: the host content it has
        private OnlineTablePacket waitingForContent;

        // ---------- the host ----------

        // The host's content as it is now: its fingerprint (file names, sizes and times) and, when that changed, the
        // bundle sent to the room. Called when a computer joins and when a game starts, not every frame.
        private void RefreshHostContent()
        {
            string root;
            try
            {
                // A computer that took over hosting (MP-5) goes on with the room's content it already has.
                root = !string.IsNullOrEmpty(ContentPackStore.RoomRoot) ? ContentPackStore.RoomRoot : host.EnsureProfile().GamesFolder;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] Your game saves could not be found: {exception.Message}");
                return;
            }

            List<string> files = ContentFiles(root, out long total);
            string fingerprint = Fingerprint(root, files);
            if (fingerprint == hostContentFiles && hostContentBundle != null) return;
            byte[] body;
            using (MemoryStream stream = new MemoryStream((int)Math.Min(total + 1024, int.MaxValue)))
            using (BinaryWriter bundle = new BinaryWriter(stream, Encoding.UTF8))
            {
                bundle.Write(files.Count);
                for (int i = 0; i < files.Count; i++)
                {
                    byte[] bytes;
                    try
                    {
                        bytes = File.ReadAllBytes(Path.Combine(root, files[i]));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[Online] {files[i]} could not be read to share: {exception.Message}");
                        bytes = Array.Empty<byte>();
                    }

                    bundle.Write(files[i]);
                    bundle.Write(bytes.Length);
                    bundle.Write(bytes);
                }

                bundle.Flush();
                body = stream.ToArray();
            }

            // The content's name is made from its bytes, so a computer that took over hosting (with the same files)
            // has the same name and nobody gets the content twice.
            string hash = Hash(body);
            using (MemoryStream stream = new MemoryStream(body.Length + 64))
            using (BinaryWriter bundle = new BinaryWriter(stream, Encoding.UTF8))
            {
                bundle.Write(ContentFormat);
                bundle.Write(hash);
                bundle.Write(body);
                bundle.Flush();
                hostContentBundle = stream.ToArray();
            }

            hostContentFiles = fingerprint;
            hostContentHash = hash;
            Debug.Log($"[Online] Your content for the room: {files.Count} files, {hostContentBundle.Length / 1024} KB.");
        }

        // Every file under the game saves (relative, with '/'), largest last, within the room's limits.
        private static List<string> ContentFiles(string root, out long total)
        {
            List<string> files = new List<string>();
            total = 0;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return files;
            string[] found;
            try
            {
                found = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] Your game saves could not be listed: {exception.Message}");
                return files;
            }

            Array.Sort(found, StringComparer.Ordinal);
            int skipped = 0;
            for (int i = 0; i < found.Length; i++)
            {
                long size;
                try
                {
                    size = new FileInfo(found[i]).Length;
                }
                catch (Exception)
                {
                    continue;
                }

                if (size > MaxContentFileBytes || total + size > MaxContentBytes || files.Count >= MaxContentFiles)
                {
                    skipped++;
                    continue;
                }

                files.Add(found[i].Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/'));
                total += size;
            }

            if (skipped > 0) Debug.LogWarning($"[Online] {skipped} of your files are too big to share with the room and were left out.");
            return files;
        }

        private static string Fingerprint(string root, List<string> files)
        {
            StringBuilder text = new StringBuilder(files.Count * 64);
            for (int i = 0; i < files.Count; i++)
            {
                FileInfo info = new FileInfo(Path.Combine(root, files[i]));
                text.Append(files[i]).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
            }

            return Hash(Encoding.UTF8.GetBytes(text.ToString()));
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                StringBuilder hex = new StringBuilder(digest.Length * 2);
                for (int i = 0; i < digest.Length; i++) hex.Append(digest[i].ToString("x2"));
                return hex.ToString();
            }
        }

        // Sends the content to a joined computer that does not have it yet (a large message, sent once per change).
        private void SendContent(Member member)
        {
            if (hostContentBundle == null || member == null || member.IsHost || !member.Connected || member.Connection < 0) return;
            if (member.ContentHash == hostContentHash) return;
            byte[] message = new byte[hostContentBundle.Length + 1];
            message[0] = MsgContent;
            Buffer.BlockCopy(hostContentBundle, 0, message, 1, hostContentBundle.Length);
            session.SendTo(member.Connection, message, 0, message.Length);
            member.ContentHash = hostContentHash;
        }

        private void SendContentToRoom()
        {
            RefreshHostContent();
            for (int i = 0; i < members.Count; i++) SendContent(members[i]);
        }

        // ---------- a joined computer ----------

        // The host's content arrived: written to the room's folder, read from now on, and a table that waited for it
        // is dealt.
        private void ReadContent(byte[] data)
        {
            string root = Path.Combine(UnityEngine.Application.temporaryCachePath, RoomContentFolder);
            string hash;
            using (BinaryReader reader = new BinaryReader(new MemoryStream(data, 1, data.Length - 1, false), Encoding.UTF8))
            {
                int format = reader.ReadInt32();
                if (format != ContentFormat) throw new InvalidDataException("the host's content is in another format");
                hash = reader.ReadString();
                int count = reader.ReadInt32();
                if (count < 0 || count > MaxContentFiles) throw new InvalidDataException("too many files");
                ClearRoomFolder(root);
                Directory.CreateDirectory(root);
                string fullRoot = Path.GetFullPath(root);
                long total = 0;
                for (int i = 0; i < count; i++)
                {
                    string relative = reader.ReadString();
                    int length = reader.ReadInt32();
                    if (length < 0 || length > MaxContentFileBytes || (total += length) > MaxContentBytes)
                        throw new InvalidDataException("a file is too big");
                    byte[] bytes = reader.ReadBytes(length);
                    string path = Path.GetFullPath(Path.Combine(fullRoot, relative));
                    if (relative.Length == 0 || Path.IsPathRooted(relative) || !path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                        continue; // never outside the room's folder
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, bytes);
                }
            }

            ContentPackStore.RoomRoot = root;
            roomContentHash = hash;
            host.ReloadContentStores();
            Debug.Log($"[Online] The host's cards, decks, pictures and rules are in use for this room.");
            if (waitingForContent != null && waitingForContent.Setup != null && waitingForContent.Setup.ContentHash == roomContentHash)
            {
                OnlineTablePacket packet = waitingForContent;
                waitingForContent = null;
                if (!sharedTable) StartSharing(false);
                ApplyPacket(packet);
            }
        }

        // False (and the table kept for later) when a table from the host was dealt with content this computer does
        // not have yet.
        private bool HasContentFor(OnlineTablePacket packet)
        {
            string wanted = packet.Setup.ContentHash;
            if (hosting || string.IsNullOrEmpty(wanted) || wanted == roomContentHash) return true;
            waitingForContent = packet;
            host.ShowMessage("Getting the host's cards and pictures…");
            return false;
        }

        // Back to the profile's own saves (left the room, or the scene is closing).
        private void ForgetRoomContent()
        {
            waitingForContent = null;
            hostContentBundle = null;
            hostContentHash = string.Empty;
            hostContentFiles = string.Empty;
            if (string.IsNullOrEmpty(ContentPackStore.RoomRoot) && roomContentHash.Length == 0) return;
            string root = ContentPackStore.RoomRoot;
            ContentPackStore.RoomRoot = null;
            roomContentHash = string.Empty;
            host.ReloadContentStores();
            if (host.TableGame != null) host.RulesCardPanel.ReloadHouseRules(string.Empty);
            if (!string.IsNullOrEmpty(root)) ClearRoomFolder(root);
        }

        private static void ClearRoomFolder(string root)
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] The room's copy of the host's content could not be removed: {exception.Message}");
            }
        }
    }
}
