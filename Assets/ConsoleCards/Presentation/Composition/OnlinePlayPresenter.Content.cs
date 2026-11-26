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

        private static bool StartsWithSaveMagic(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    byte[] head = new byte[4];
                    return stream.Read(head, 0, 4) == 4 && head[0] == (byte)'C' && head[1] == (byte)'C' && head[2] == (byte)'S' && head[3] == (byte)'1';
                }
            }
            catch (Exception)
            {
                return false;
            }
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
                // CONTENT-SAVE (s142 audit): table saves live in the profile's saves/ folder, not under games/, so none is
                // sent today; a save copied into games/ by hand would be. A saved table holds every hidden card and the seed.
                if (found[i].EndsWith(".ccsave", StringComparison.OrdinalIgnoreCase) || StartsWithSaveMagic(found[i])) continue;
                // CONTENT-1: card-editor source originals ("...-source.png") only let the card editor re-crop; a guest never needs them.
                if (Path.GetFileName(found[i]).IndexOf("-source.", StringComparison.OrdinalIgnoreCase) >= 0) continue;
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

        // CONTENT-1: the bundle goes to a joined computer in 64 KB parts, a few per frame and at most 4 not yet acknowledged,
        // so the roster, the table and the commands are not stuck behind one huge message (found live: 49 MB, 8+ minutes).
        private const byte MsgContentPart = 28; // host → guest, unordered: [id][int index][int count][int total length][bytes]
        private const byte MsgContentAck = 29; // guest → host, unordered: [id][int index]
        private const int ContentPartBytes = 64 * 1024;
        private const int ContentPartHeader = 13;
        private const int ContentPartsInFlight = 4;
        private const int ContentPartsPerFrame = 3;
        private const float ContentStallSeconds = 6f; // CONTENT-STALL
        private const long ContentBundleMargin = 1024L * 1024L; // the bundle's own headers (names, sizes) on top of MaxContentBytes

        private byte[] contentPartBuffer; // host: one buffer for every part sent (the session copies it)
        private byte[] contentIncoming; // joined computer: the bundle being assembled, with the leading message byte ReadContent expects
        private int contentIncomingParts;
        private int contentIncomingCount;
        private int contentIncomingStep;

        private static int ContentPartCount(int length) => (length + ContentPartBytes - 1) / ContentPartBytes;

        private static void PutInt(byte[] buffer, int at, int value)
        {
            buffer[at] = (byte)value;
            buffer[at + 1] = (byte)(value >> 8);
            buffer[at + 2] = (byte)(value >> 16);
            buffer[at + 3] = (byte)(value >> 24);
        }

        // Starts (or restarts, when the host's content changed) the transfer of the content to a joined computer that does
        // not have it yet. TickContent sends the parts.
        private void SendContent(Member member)
        {
            if (hostContentBundle == null || member == null || member.IsHost || !member.Connected || member.Connection < 0) return;
            if (member.ContentHash == hostContentHash) return;
            if (member.ContentBundle != null && member.ContentSending == hostContentHash) return; // already on its way
            member.ContentBundle = hostContentBundle;
            member.ContentSending = hostContentHash;
            member.ContentNext = 0;
            member.ContentAcked = -1;
            member.ContentProgressAt = Time.unscaledTime;
            Debug.Log($"[Online] Sending the room's content to {member.Name}: {ContentPartCount(hostContentBundle.Length)} parts, {hostContentBundle.Length / 1024} KB.");
        }

        private static void StopContentTransfer(Member member)
        {
            member.ContentBundle = null;
            member.ContentSending = string.Empty;
            member.ContentNext = 0;
            member.ContentAcked = -1;
        }

        // The host, every frame: the next parts for each computer that is getting the content (no allocation).
        private void TickContent()
        {
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (member.ContentBundle == null) continue;
                if (member.IsHost || !member.Connected || member.Connection < 0 || hostContentBundle == null)
                {
                    StopContentTransfer(member); // gone (a rejoin starts again from part 0), or the room's content was dropped
                    continue;
                }

                if (member.ContentSending != hostContentHash)
                {
                    StopContentTransfer(member); // the host's content changed meanwhile: start again with the new one
                    SendContent(member);
                    if (member.ContentBundle == null) continue;
                }

                int length = member.ContentBundle.Length;
                int count = ContentPartCount(length);
                // CONTENT-STALL (two-copy test, 2026-11-27): a guest sat in the lobby ("Waiting for the host") while the game ran:
                // the parts in flight were never confirmed and nothing sent them again (the joined computer takes parts only in
                // order, so one missing part stops the transfer for good). No progress for a while: send again from the first
                // part not confirmed.
                if (member.ContentNext > member.ContentAcked + 1 && Time.unscaledTime - member.ContentProgressAt > ContentStallSeconds)
                {
                    Debug.Log($"[Online] The content to {member.Name} stopped at part {member.ContentAcked + 1} of {count}: sending again from there.");
                    member.ContentNext = member.ContentAcked + 1;
                    member.ContentProgressAt = Time.unscaledTime;
                }

                int sent = 0;
                while (member.ContentNext < count && member.ContentNext - (member.ContentAcked + 1) < ContentPartsInFlight && sent++ < ContentPartsPerFrame)
                {
                    if (contentPartBuffer == null) contentPartBuffer = new byte[ContentPartHeader + ContentPartBytes];
                    int index = member.ContentNext++;
                    int offset = index * ContentPartBytes;
                    int chunk = Math.Min(ContentPartBytes, length - offset);
                    contentPartBuffer[0] = MsgContentPart;
                    PutInt(contentPartBuffer, 1, index);
                    PutInt(contentPartBuffer, 5, count);
                    PutInt(contentPartBuffer, 9, length);
                    Buffer.BlockCopy(member.ContentBundle, offset, contentPartBuffer, ContentPartHeader, chunk);
                    session.SendTo(member.Connection, contentPartBuffer, 0, ContentPartHeader + chunk);
                }
            }
        }

        // The host: a joined computer has a part. Acks outside the transfer (no transfer, an index never sent) are ignored.
        private void ReadContentAck(Member member, byte[] data)
        {
            if (member.ContentBundle == null || data.Length < 5) return;
            int count = ContentPartCount(member.ContentBundle.Length);
            int index = BitConverter.ToInt32(data, 1);
            if (index < 0 || index >= count) return;
            if (index >= member.ContentNext) member.ContentNext = index + 1; // CONTENT-STALL: a part confirmed again after a re-send
            if (index > member.ContentAcked)
            {
                // CONTENT-STALL diagnostics: every tenth of the transfer, how far it got (no names of files, no content).
                if ((index + 1) * 10 / count > (member.ContentAcked + 1) * 10 / count)
                    Debug.Log($"[Online] Content to {member.Name}: {(index + 1) * 100 / count}% confirmed.");
                member.ContentAcked = index;
                member.ContentProgressAt = Time.unscaledTime; // CONTENT-STALL
            }

            if (member.ContentAcked < count - 1) return;
            member.ContentHash = member.ContentSending; // only now does the computer count as having the content
            StopContentTransfer(member);
            Debug.Log($"[Online] Sending the room's content to {member.Name}: done.");
        }

        private void SendContentToRoom()
        {
            string before = hostContentHash;
            RefreshHostContent();
            for (int i = 0; i < members.Count; i++) SendContent(members[i]);
            // Audit C-3: the host's card, deck, picture or game edits made since the last table are used from this one.
            if (string.IsNullOrEmpty(before) || before == hostContentHash) return;
            const string Changed = "The host's cards, decks or pictures changed: this table uses them.";
            host.ShowNotice(Changed);
            for (int i = 0; i < members.Count; i++)
            {
                Member member = members[i];
                if (!member.IsHost && member.Connected && member.Connection >= 0) SendNotice(member, Changed);
            }
        }

        // ---------- a joined computer ----------

        // CONTENT-1: one part of the host's content. Part 0 starts (or restarts) the transfer; the others must follow in order.
        private void ReadContentPart(byte[] data)
        {
            if (data.Length <= ContentPartHeader) return;
            int index = BitConverter.ToInt32(data, 1);
            int count = BitConverter.ToInt32(data, 5);
            int total = BitConverter.ToInt32(data, 9);
            if (total <= 0 || total > MaxContentBytes + ContentBundleMargin || count != ContentPartCount(total) || index < 0 || index >= count) return;
            int chunk = data.Length - ContentPartHeader;
            if (chunk != (index == count - 1 ? total - index * ContentPartBytes : ContentPartBytes)) return;
            if (index == 0)
            {
                contentIncoming = new byte[total + 1];
                contentIncoming[0] = MsgContent;
                contentIncomingParts = 0;
                contentIncomingCount = count;
                contentIncomingStep = 0;
            }
            else if (contentIncoming == null || count != contentIncomingCount || total != contentIncoming.Length - 1 || index != contentIncomingParts)
            {
                // CONTENT-STALL: a part sent again that is already here (its confirmation was lost) is confirmed again, so the
                // host moves on.
                if (contentIncoming != null && count == contentIncomingCount && total == contentIncoming.Length - 1 && index < contentIncomingParts)
                {
                    BeginMessage(MsgContentAck);
                    writer.Write(contentIncomingParts - 1);
                    SendToHost();
                }

                return; // not part of the transfer in progress
            }

            Buffer.BlockCopy(data, ContentPartHeader, contentIncoming, 1 + index * ContentPartBytes, chunk);
            contentIncomingParts = index + 1;
            BeginMessage(MsgContentAck);
            writer.Write(index);
            SendToHost();
            if (contentIncomingParts < count)
            {
                int step = contentIncomingParts * 10 / count;
                if (step > contentIncomingStep)
                {
                    contentIncomingStep = step;
                    host.ShowMessage($"Getting the host's cards and pictures… {step * 10}%");
                }

                return;
            }

            byte[] bundle = contentIncoming;
            contentIncoming = null;
            Debug.Log($"[Online] The host's content arrived: {count} parts, {total / 1024} KB.");
            ReadContent(bundle);
        }

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
            contentIncoming = null; // CONTENT-1
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
