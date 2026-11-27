using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Saving;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// MP-4 (owner, 2026-10-27): the host's content is the room's. The host sends the game saves of the table being played
    /// (that game's cards, decks, pictures, packs, house rules, saved layouts and settings, and the decks and cards of
    /// "Your cards" the table uses; TABLE-ONLY, owner 2026-11-27) to each joined computer, and again when they change.
    /// A joined computer keeps them in memory for the room only (ROOM-MEM: <see cref="RoomContentFiles"/>, read through
    /// <see cref="ContentPackStore.RoomRoot"/>), never on its disk and never in its own profile, and reads them instead of its
    /// own while it is in the room. A table from the host says which content it was dealt with; a joined computer waits
    /// for that content before it deals the same table. Leaving the room goes back to the profile's own saves.
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
        private TableContentNeeds tableNeeds; // host: what the table about to be dealt uses (set by the game screen before START)
        private string hostContentKey = string.Empty; // host: the table the bundle was made for
        private string contentStatus = string.Empty; // joined computer: "Getting the host's cards… 40%", for the room screen
        private int contentPercent;

        // ---------- the host ----------

        // TABLE-ONLY (owner, 2026-11-27): the game screen says what the table it is about to start uses (the host's
        // current table is still the old one at that moment). Only that goes to the room.
        internal void NoteTableContent(TableContentNeeds needs)
        {
            tableNeeds = needs;
        }

        private TableContentNeeds CurrentNeeds()
        {
            if (tableNeeds != null) return tableNeeds;
            try
            {
                return TableContentNeeds.ForGame(host.TableGame); // in the lobby: the game the host has now
            }
            catch (Exception)
            {
                return new TableContentNeeds();
            }
        }

        // One file of the bundle: read from a file (Source) or made here (Bytes).
        private sealed class ContentItem
        {
            public string Relative;
            public string Source;
            public byte[] Bytes;
            public long Length;
            public long Ticks;
        }

        // The host's content as it is now: its fingerprint (the table's game, file names, sizes and times) and, when that
        // changed, the bundle sent to the room. Called when a computer joins and when a game starts, not every frame.
        private void RefreshHostContent()
        {
            string root;
            try
            {
                // A computer that took over hosting (MP-5) goes on with the room's content it already has (held in memory).
                root = !string.IsNullOrEmpty(ContentPackStore.RoomRoot) ? ContentPackStore.RoomRoot : host.EnsureProfile().GamesFolder;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] Your game saves could not be found: {exception.Message}");
                return;
            }

            TableContentNeeds needs = CurrentNeeds();
            string key = needs.Key();
            List<ContentItem> items = ContentFiles(root, needs, out long total);
            string fingerprint = Fingerprint(key, items);
            if (fingerprint == hostContentFiles && hostContentBundle != null) return;
            byte[] body;
            using (MemoryStream stream = new MemoryStream((int)Math.Min(total + 1024, int.MaxValue)))
            using (BinaryWriter bundle = new BinaryWriter(stream, Encoding.UTF8))
            {
                bundle.Write(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    byte[] bytes;
                    try
                    {
                        bytes = items[i].Bytes ?? ContentIO.ReadAllBytes(items[i].Source);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[Online] A file could not be read to share ({exception.GetType().Name}).");
                        bytes = Array.Empty<byte>();
                    }

                    bundle.Write(items[i].Relative);
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
            hostContentKey = key;
            hostContentHash = hash;
            Debug.Log($"[Online] Your content for the room: {needs.GameIds.Count} game folder(s){(needs.UsesLibrary ? " and your library" : string.Empty)}, "
                + $"{items.Count} files, {hostContentBundle.Length / 1024} KB.");
        }

        private static bool StartsWithSaveMagic(string path)
        {
            try
            {
                if (ContentIO.IsRoomPath(path))
                {
                    byte[] held = ContentIO.ReadAllBytes(path);
                    return held.Length >= 4 && held[0] == (byte)'C' && held[1] == (byte)'C' && held[2] == (byte)'S' && held[3] == (byte)'1';
                }

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

        // TABLE-ONLY (owner, 2026-11-27): the files of this table only: the folder of each game it uses and, from "Your
        // cards", the cards, decks and pictures it uses. A computer that took over hosting sends what it holds (the
        // room's, already only the table's). Within the room's limits.
        private static List<ContentItem> ContentFiles(string root, TableContentNeeds needs, out long total)
        {
            List<ContentItem> items = new List<ContentItem>();
            total = 0;
            if (string.IsNullOrEmpty(root) || !ContentIO.DirectoryExists(root)) return items;
            int skipped = 0;
            if (ContentIO.IsRoomPath(root))
            {
                AddFolder(items, root, root, ref total, ref skipped);
            }
            else
            {
                foreach (string gameId in needs.GameIds)
                {
                    AddFolder(items, root, Path.Combine(root, ContentPackStore.SafeName(gameId, "game")), ref total, ref skipped);
                }

                AddLibrary(items, root, needs, ref total, ref skipped);
            }

            if (skipped > 0) Debug.LogWarning($"[Online] {skipped} of your files are too big to share with the room and were left out.");
            return items;
        }

        // Every file under one folder (relative to the games folder, with '/'), pictures as their share copy.
        private static void AddFolder(List<ContentItem> items, string root, string folder, ref long total, ref int skipped)
        {
            if (!ContentIO.DirectoryExists(folder)) return;
            string[] found;
            try
            {
                found = ContentIO.GetFiles(folder, "*", SearchOption.AllDirectories);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] Your game saves could not be listed: {exception.Message}");
                return;
            }

            Array.Sort(found, StringComparer.Ordinal);
            for (int i = 0; i < found.Length; i++)
            {
                string name = Path.GetFileName(found[i]);
                if (ContentShareCopy.IsShareFile(name)) continue; // SHARE-COPY: the host's own copies are sent in place of the pictures
                // CONTENT-SAVE (s142 audit): table saves live in the profile's saves/ folder, not under games/, so none is
                // sent today; a save copied into games/ by hand would be. A saved table holds every hidden card and the seed.
                if (found[i].EndsWith(".ccsave", StringComparison.OrdinalIgnoreCase) || StartsWithSaveMagic(found[i])) continue;
                // CONTENT-1: card-editor source originals ("...-source.png") only let the card editor re-crop; a guest never needs them.
                if (name.IndexOf("-source.", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string relative = RelativeTo(root, found[i]);
                bool picture = ContentShareCopy.IsPicture(name)
                    && (relative.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase) >= 0 || relative.IndexOf("/packs/", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!TryItem(relative, picture ? ContentShareCopy.Prepare(found[i]) : found[i], out ContentItem item)) continue;
                TryAdd(items, item, ref total, ref skipped);
            }
        }

        // TABLE-ONLY: from "Your cards" only the decks (or the cards) the table uses, and the pictures those cards use.
        // The cards.json and decks.json sent are cut down to those records; uploads.json to those pictures.
        private static void AddLibrary(List<ContentItem> items, string root, TableContentNeeds needs, ref long total, ref int skipped)
        {
            if (!needs.UsesLibrary) return;
            ContentPackStore store = new ContentPackStore(root);
            string id = PlayerLibrary.Id;
            PlayerCardsFile cards = store.LoadCards(id);
            PlayerDecksFile decks = store.LoadDecks(id);
            HashSet<string> wanted = new HashSet<string>(needs.LibraryCardIds, StringComparer.OrdinalIgnoreCase);
            List<PlayerDeckRecord> keptDecks = new List<PlayerDeckRecord>();
            for (int d = 0; decks.decks != null && d < decks.decks.Count; d++)
            {
                PlayerDeckRecord deck = decks.decks[d];
                if (deck == null) continue;
                bool used = needs.LibraryDeckIds.Contains(deck.id ?? string.Empty);
                for (int p = 0; !used && deck.parts != null && p < deck.parts.Count; p++)
                {
                    used = deck.parts[p] != null && needs.LibraryDeckIds.Contains(deck.parts[p].id ?? string.Empty);
                }

                if (!used) continue;
                keptDecks.Add(deck);
                AddDeckCards(wanted, deck);
            }

            List<PlayerCardRecord> keptCards = new List<PlayerCardRecord>();
            for (int c = 0; cards.cards != null && c < cards.cards.Count; c++)
            {
                PlayerCardRecord card = cards.cards[c];
                if (card != null && (needs.AllLibraryCards || wanted.Contains(card.id ?? string.Empty))) keptCards.Add(card);
            }

            List<CardChangeRecord> keptChanges = new List<CardChangeRecord>();
            for (int c = 0; cards.changes != null && c < cards.changes.Count; c++)
            {
                CardChangeRecord change = cards.changes[c];
                if (change != null && (needs.AllLibraryCards || wanted.Contains(change.cardId ?? string.Empty))) keptChanges.Add(change);
            }

            if (keptCards.Count == 0 && keptChanges.Count == 0 && keptDecks.Count == 0) return;
            string cardsPath = store.CardsPath(id);
            string decksPath = store.DecksPath(id);
            AddJson(items, root, cardsPath, JsonUtility.ToJson(CutCards(cards, keptCards, keptChanges)), ref total, ref skipped);
            AddJson(items, root, decksPath, JsonUtility.ToJson(CutDecks(decks, keptDecks)), ref total, ref skipped);

            // The pictures those cards and decks name (a card with no picture of its own has the library's default key).
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            keys.Add(ArtLibrary.NormalizeKey(PlayerLibrary.ArtFolder + "/plain/front"));
            for (int c = 0; c < keptCards.Count; c++)
            {
                keys.Add(ArtLibrary.NormalizeKey(keptCards[c].frontArtKey));
                keys.Add(ArtLibrary.NormalizeKey(keptCards[c].backArtKey));
                keys.Add(ArtLibrary.NormalizeKey(PlayerContent.DefaultArtKey(PlayerLibrary.ArtFolder, keptCards[c].id, true)));
                keys.Add(ArtLibrary.NormalizeKey(PlayerContent.DefaultArtKey(PlayerLibrary.ArtFolder, keptCards[c].id, false)));
            }

            for (int c = 0; c < keptChanges.Count; c++)
            {
                keys.Add(ArtLibrary.NormalizeKey(keptChanges[c].frontArtKey));
                keys.Add(ArtLibrary.NormalizeKey(keptChanges[c].backArtKey));
            }

            for (int d = 0; d < keptDecks.Count; d++)
            {
                keys.Add(ArtLibrary.NormalizeKey(keptDecks[d].backArtKey));
                for (int f = 0; keptDecks[d].fronts != null && f < keptDecks[d].fronts.Count; f++)
                {
                    if (keptDecks[d].fronts[f] != null) keys.Add(ArtLibrary.NormalizeKey(keptDecks[d].fronts[f].frontArtKey));
                }
            }

            ContentUploadIndex index = store.LoadUploads(id);
            ContentUploadIndex kept = new ContentUploadIndex();
            string uploads = store.UploadsFolder(id);
            for (int u = 0; index.pictures != null && u < index.pictures.Count; u++)
            {
                ContentArtFile picture = index.pictures[u];
                if (picture == null || string.IsNullOrEmpty(picture.file) || !keys.Contains(ArtLibrary.NormalizeKey(picture.key))) continue;
                if (Path.IsPathRooted(picture.file) || picture.file.IndexOf("..", StringComparison.Ordinal) >= 0) continue;
                if (picture.file.IndexOf("-source.", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string path = Path.Combine(uploads, picture.file);
                if (!TryItem(RelativeTo(root, path), ContentShareCopy.Prepare(path), out ContentItem item)) continue;
                if (TryAdd(items, item, ref total, ref skipped)) kept.pictures.Add(picture);
            }

            if (kept.pictures.Count > 0) AddJson(items, root, Path.Combine(uploads, ContentUploadIndex.FileName), JsonUtility.ToJson(kept), ref total, ref skipped);
        }

        private static void AddDeckCards(HashSet<string> ids, PlayerDeckRecord deck)
        {
            for (int i = 0; deck.cards != null && i < deck.cards.Count; i++)
            {
                if (deck.cards[i] != null && !string.IsNullOrEmpty(deck.cards[i].cardId)) ids.Add(deck.cards[i].cardId);
            }

            for (int p = 0; deck.parts != null && p < deck.parts.Count; p++)
            {
                PlayerDeckPart part = deck.parts[p];
                for (int i = 0; part != null && part.cards != null && i < part.cards.Count; i++)
                {
                    if (part.cards[i] != null && !string.IsNullOrEmpty(part.cards[i].cardId)) ids.Add(part.cards[i].cardId);
                }
            }

            for (int f = 0; deck.fronts != null && f < deck.fronts.Count; f++)
            {
                if (deck.fronts[f] != null && !string.IsNullOrEmpty(deck.fronts[f].cardId)) ids.Add(deck.fronts[f].cardId);
            }
        }

        // The file's own fields are kept (a copy by JSON); only the records are cut down.
        private static PlayerCardsFile CutCards(PlayerCardsFile from, List<PlayerCardRecord> cards, List<CardChangeRecord> changes)
        {
            PlayerCardsFile copy = JsonUtility.FromJson<PlayerCardsFile>(JsonUtility.ToJson(from));
            copy.cards = cards;
            copy.changes = changes;
            return copy;
        }

        private static PlayerDecksFile CutDecks(PlayerDecksFile from, List<PlayerDeckRecord> decks)
        {
            PlayerDecksFile copy = JsonUtility.FromJson<PlayerDecksFile>(JsonUtility.ToJson(from));
            copy.decks = decks;
            return copy;
        }

        // A JSON file made here; its time is the saved file's, so the fingerprint follows a change to the original.
        private static void AddJson(List<ContentItem> items, string root, string path, string json, ref long total, ref int skipped)
        {
            long ticks = 0;
            try
            {
                if (!ContentIO.IsRoomPath(path)) ticks = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
            }
            catch (Exception)
            {
                ticks = 0;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            TryAdd(items, new ContentItem { Relative = RelativeTo(root, path), Bytes = bytes, Length = bytes.Length, Ticks = ticks }, ref total, ref skipped);
        }

        private static bool TryItem(string relative, string source, out ContentItem item)
        {
            item = null;
            long ticks = 0;
            try
            {
                if (!ContentIO.TryGetLength(source, out long length)) return false;
                if (!ContentIO.IsRoomPath(source)) ticks = File.GetLastWriteTimeUtc(source).Ticks;
                item = new ContentItem { Relative = relative, Source = source, Length = length, Ticks = ticks };
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryAdd(List<ContentItem> items, ContentItem item, ref long total, ref int skipped)
        {
            if (item.Length > MaxContentFileBytes || total + item.Length > MaxContentBytes || items.Count >= MaxContentFiles)
            {
                skipped++;
                return false;
            }

            items.Add(item);
            total += item.Length;
            return true;
        }

        private static string RelativeTo(string root, string path) =>
            path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');

        // The table's game and the file names, sizes and times: a change to either makes a new bundle.
        private static string Fingerprint(string key, List<ContentItem> items)
        {
            StringBuilder text = new StringBuilder((items.Count * 64) + key.Length + 8);
            text.Append(key).Append('\n');
            for (int i = 0; i < items.Count; i++)
            {
                text.Append(items[i].Relative).Append('|').Append(items[i].Length).Append('|').Append(items[i].Ticks).Append('\n');
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
            string beforeKey = hostContentKey;
            RefreshHostContent();
            for (int i = 0; i < members.Count; i++) SendContent(members[i]);
            // Audit C-3: the host's card, deck, picture or game edits made since the last table are used from this one.
            // TABLE-ONLY: another game's content is not "the host's cards changed"; only the same table's content is.
            if (string.IsNullOrEmpty(before) || before == hostContentHash || beforeKey != hostContentKey) return;
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
                if (count > 1) SetContentProgress(0); // LOBBY-PROGRESS
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
                    SetContentProgress(step * 10);
                }

                return;
            }

            byte[] bundle = contentIncoming;
            contentIncoming = null;
            Debug.Log($"[Online] The host's content arrived: {count} parts, {total / 1024} KB.");
            ReadContent(bundle);
        }

        // LOBBY-PROGRESS (owner, 2026-11-27): the room screen and the table say how much of the host's cards has come,
        // every tenth (the part counts are small; this is not per frame).
        private void SetContentProgress(int percent)
        {
            contentPercent = percent;
            contentStatus = $"Getting the host's cards… {percent}%";
            host.ShowMessage(contentStatus);
            RefreshFrontEnd();
        }

        // The host's content arrived: kept in memory for the room (never on this computer's disk), read from now on, and a
        // table that waited for it is dealt.
        private void ReadContent(byte[] data)
        {
            string hash;
            Dictionary<string, byte[]> received = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            using (BinaryReader reader = new BinaryReader(new MemoryStream(data, 1, data.Length - 1, false), Encoding.UTF8))
            {
                int format = reader.ReadInt32();
                if (format != ContentFormat) throw new InvalidDataException("the host's content is in another format");
                hash = reader.ReadString();
                int count = reader.ReadInt32();
                if (count < 0 || count > MaxContentFiles) throw new InvalidDataException("too many files");
                long total = 0;
                for (int i = 0; i < count; i++)
                {
                    string relative = reader.ReadString();
                    int length = reader.ReadInt32();
                    if (length < 0 || length > MaxContentFileBytes || (total += length) > MaxContentBytes)
                        throw new InvalidDataException("a file is too big");
                    byte[] bytes = reader.ReadBytes(length);
                    if (bytes.Length != length) throw new InvalidDataException("the content ended early");
                    string key = RoomContentFiles.NormaliseRelative(relative);
                    if (key.Length == 0 || Path.IsPathRooted(relative) || HasParentStep(key))
                        continue; // never outside the room's folder
                    received[key] = bytes;
                }
            }

            RoomContentFiles.Set(received);
            ContentPackStore.RoomRoot = RoomContentFiles.Root;
            roomContentHash = hash;
            host.ReloadContentStores();
            Debug.Log($"[Online] The host's content is in use for this room: {RoomContentFiles.Count} files, {RoomContentFiles.TotalBytes / 1024} KB (kept in memory only).");
            contentPercent = 100;
            contentStatus = "The host's cards are ready."; // LOBBY-PROGRESS
            host.ShowMessage(contentStatus);
            if (waitingForContent != null && waitingForContent.Setup != null && waitingForContent.Setup.ContentHash == roomContentHash)
            {
                OnlineTablePacket packet = waitingForContent;
                waitingForContent = null;
                if (!sharedTable) StartSharing(false);
                ApplyPacket(packet);
            }

            RefreshFrontEnd();
        }

        // True when a relative path has a "." or ".." step.
        private static bool HasParentStep(string key)
        {
            string[] steps = key.Split('/');
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] == ".." || steps[i] == ".") return true;
            }

            return false;
        }

        // False (and the table kept for later) when a table from the host was dealt with content this computer does
        // not have yet.
        private bool HasContentFor(OnlineTablePacket packet)
        {
            string wanted = packet.Setup.ContentHash;
            if (hosting || string.IsNullOrEmpty(wanted) || wanted == roomContentHash) return true;
            waitingForContent = packet;
            host.ShowMessage(contentPercent > 0 && contentPercent < 100 ? $"Getting the host's cards… {contentPercent}%" : "Getting the host's cards…");
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
            hostContentKey = string.Empty;
            tableNeeds = null;
            contentStatus = string.Empty;
            contentPercent = 0;
            RoomContentFiles.Clear(); // ROOM-MEM (owner, 2026-11-27): the host's files go from this computer's memory
            if (string.IsNullOrEmpty(ContentPackStore.RoomRoot) && roomContentHash.Length == 0) return;
            ContentPackStore.RoomRoot = null;
            roomContentHash = string.Empty;
            host.ReloadContentStores();
            if (host.TableGame != null) host.RulesCardPanel.ReloadHouseRules(string.Empty);
        }

        // ROOM-MEM (owner, 2026-11-27): older versions kept the host's files in this cache folder; a crash could have left
        // them there. Removed once, at start. Nothing is written there now.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RemoveLegacyRoomFolder()
        {
            string old = Path.Combine(UnityEngine.Application.temporaryCachePath, RoomContentFolder);
            bool existed = Directory.Exists(old);
            ClearRoomFolder(old);
            if (existed && !Directory.Exists(old)) Debug.Log("[Online] Removed an old copy of a host's content from this computer's cache.");
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

    /// <summary>
    /// TABLE-ONLY (owner, 2026-11-27): what the table about to be dealt uses, so the host sends only that: the folder of
    /// each game it uses, and from "Your cards" the decks (and any cards) it uses. Made by the game screen at START.
    /// </summary>
    internal sealed class TableContentNeeds
    {
        public readonly HashSet<string> GameIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> LibraryDeckIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> LibraryCardIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool AllLibraryCards; // a made game that plays with the whole library ("All your cards")

        public bool UsesLibrary => AllLibraryCards || LibraryDeckIds.Count > 0 || LibraryCardIds.Count > 0;

        /// <summary>A game's own table: its folder of saves.</summary>
        public static TableContentNeeds ForGame(GameDefinition game)
        {
            TableContentNeeds needs = new TableContentNeeds();
            needs.AddGameId(game != null ? game.StableId : null);
            return needs;
        }

        /// <summary>An Empty Table started with a deck of yours (a deck of "Your cards" or of a game; deckId may be a part's).</summary>
        public static TableContentNeeds ForDeck(GameDefinition tableGame, GameDefinition deckGame, string deckId)
        {
            TableContentNeeds needs = ForGame(tableGame);
            if (deckGame != null && !string.IsNullOrEmpty(deckId)) needs.AddDeck(deckGame.StableId, deckId);
            return needs;
        }

        /// <summary>A game the host made: its deck and the cards laid out on its tables.</summary>
        public static TableContentNeeds ForPlayerGame(PlayerGameFile game, GameDefinition tableGame)
        {
            TableContentNeeds needs = ForGame(tableGame);
            if (game == null) return needs;
            if (game.deck != null && game.deck.IsSet) needs.AddDeck(game.deck.gameId, game.deck.deckId);
            needs.AddTable(game.table);
            for (int i = 0; game.layouts != null && i < game.layouts.Count; i++)
            {
                if (game.layouts[i] != null) needs.AddTable(game.layouts[i].table);
            }

            return needs;
        }

        /// <summary>The table's needs as text, for the bundle's fingerprint.</summary>
        public string Key()
        {
            StringBuilder text = new StringBuilder(64);
            Append(text, "g", GameIds);
            Append(text, "d", LibraryDeckIds);
            Append(text, "c", LibraryCardIds);
            if (AllLibraryCards) text.Append("all;");
            return text.ToString();
        }

        private void AddGameId(string id)
        {
            if (!string.IsNullOrWhiteSpace(id) && !PlayerLibrary.IsLibrary(id)) GameIds.Add(id);
        }

        // A deck of a game: that game's folder. A deck of "Your cards": that deck (the pseudo deck "game" is every card).
        private void AddDeck(string gameId, string deckId)
        {
            if (PlayerLibrary.IsLibrary(gameId))
            {
                if (string.Equals(deckId, SavedGameDeck.GameDeck, StringComparison.OrdinalIgnoreCase)) AllLibraryCards = true;
                else if (!string.IsNullOrWhiteSpace(deckId)) LibraryDeckIds.Add(deckId);
            }
            else
            {
                AddGameId(gameId);
            }
        }

        // Cards laid out on a made game's table: any of them may be one of "Your cards" (only those found there are sent).
        private void AddTable(SavedTable table)
        {
            if (table == null) return;
            for (int i = 0; table.pieces != null && i < table.pieces.Count; i++)
            {
                SavedPiece piece = table.pieces[i];
                if (piece != null && piece.kind == SavedPiece.Card && !string.IsNullOrEmpty(piece.definitionId)) LibraryCardIds.Add(piece.definitionId);
            }

            for (int i = 0; table.piles != null && i < table.piles.Count; i++)
            {
                SavedPile pile = table.piles[i];
                for (int c = 0; pile != null && pile.cards != null && c < pile.cards.Count; c++)
                {
                    if (!string.IsNullOrEmpty(pile.cards[c])) LibraryCardIds.Add(pile.cards[c]);
                }
            }
        }

        private static void Append(StringBuilder text, string tag, HashSet<string> ids)
        {
            List<string> sorted = new List<string>(ids);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            text.Append(tag).Append(':');
            for (int i = 0; i < sorted.Count; i++) text.Append(sorted[i].ToLowerInvariant()).Append(',');
            text.Append(';');
        }
    }
}
