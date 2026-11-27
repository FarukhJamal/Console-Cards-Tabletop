using System;
using System.Collections.Generic;
using System.IO;
using ConsoleCards.Definitions;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Audit C-1 / C-4 (owner, 2026-11-14: "if someone from a guest wants to give their cards … they will upload the card,
    /// and host will approve them, and then they can be used"). The room stays host-authoritative: a guest offers its own
    /// cards, decks and pictures for the table's game (from its own saves, never the room's copy); the host is asked;
    /// accepted, they are added to the host's own saves for that game (the host's own cards and decks win where the ids
    /// are the same) and every computer gets them with the next table. Nothing changes the game being played.
    /// Message: Offer (guest → host, unordered): game id, cards JSON, decks JSON, pictures (key, file name, bytes).
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        private const byte MsgOffer = 27;
        private const long MaxOfferBytes = 16L * 1024L * 1024L;
        private const float OfferSeconds = 60f;

        private sealed class ContentOffer
        {
            public Member From;
            public string GameId;
            public PlayerCardsFile Cards;
            public PlayerDecksFile Decks;
            public List<(string Key, string File, byte[] Bytes)> Pictures;
            public float Until;
        }

        private ContentOffer pendingOffer;
        // SESSION-OFFER (owner, 2026-11-27: "Make offered cards session only"): cards the host took from guests in this room.
        // They are put into the room's content (and the host's in-memory copy of it), never written to the host's disk.
        private readonly List<ContentOffer> acceptedOffers = new List<ContentOffer>();
        private int acceptedOfferStamp;
        private float lastOfferSentAt = -100f;

        /// <summary>A guest in a normal room with a game on can offer its own content to the host.</summary>
        internal bool CanOfferContent => !hosting && sharedTable && !privateGuest && InRoom && welcomed;

        /// <summary>A guest: sends its own cards, decks and pictures for the table's game to the host, to be approved.</summary>
        internal void OfferOwnContent()
        {
            if (!CanOfferContent) return;
            if (Time.unscaledTime - lastOfferSentAt < 5f)
            {
                host.ShowNotice("Your cards were just sent to the host; wait for their answer.");
                return;
            }

            host.EnsureContentArt();
            string gameId = host.contentArt != null ? host.contentArt.GameId : string.Empty;
            if (string.IsNullOrEmpty(gameId))
            {
                host.ShowNotice("This table's game has no cards to offer.");
                return;
            }

            ContentPackStore own = ContentPackStore.ForOwnEdits(host.EnsureProfile());
            PlayerCardsFile cards = own.LoadCards(gameId);
            PlayerDecksFile decks = own.LoadDecks(gameId);
            ContentUploadIndex uploads = own.LoadUploads(gameId);
            if (cards.IsEmpty && decks.IsEmpty)
            {
                host.ShowNotice("You have no cards or decks of your own for this game yet (make them in the card editor).");
                return;
            }

            BeginMessage(MsgOffer);
            writer.Write(gameId);
            writer.Write(JsonUtility.ToJson(cards));
            writer.Write(JsonUtility.ToJson(decks));
            long total = 0;
            List<(string Key, string File, byte[] Bytes)> pictures = new List<(string, string, byte[])>();
            for (int i = 0; uploads.pictures != null && i < uploads.pictures.Count; i++)
            {
                ContentArtFile picture = uploads.pictures[i];
                if (picture == null || string.IsNullOrEmpty(picture.key) || string.IsNullOrEmpty(picture.file)) continue;
                byte[] bytes = ContentPackStore.ReadPicture(own.UploadsFolder(gameId), picture.file, out _);
                if (bytes == null || total + bytes.Length > MaxOfferBytes) continue;
                total += bytes.Length;
                pictures.Add((picture.key, Path.GetFileName(picture.file), bytes));
            }

            writer.Write(pictures.Count);
            for (int i = 0; i < pictures.Count; i++)
            {
                writer.Write(pictures[i].Key);
                writer.Write(pictures[i].File);
                writer.Write(pictures[i].Bytes.Length);
                writer.Write(pictures[i].Bytes);
            }

            SendToHost();
            lastOfferSentAt = Time.unscaledTime;
            host.ShowNotice($"Sent to the host: {cards.cards.Count} card(s), {decks.decks.Count} deck(s), {pictures.Count} picture(s). They are used here once the host accepts them, from the next table.");
            Debug.Log($"[Online] Offered the host {cards.cards.Count} card(s), {cards.changes.Count} card change(s), {decks.decks.Count} deck(s), {pictures.Count} picture(s) ({total / 1024} KB) for {gameId}.");
        }

        // The host: a guest's offer, asked about.
        private void HostOffer(Member member, byte[] data)
        {
            if (IsWatcher(member)) return;
            ContentOffer offer;
            try
            {
                BinaryReader reader = Reader(data);
                reader.ReadByte();
                offer = new ContentOffer
                {
                    From = member,
                    GameId = reader.ReadString(),
                    Cards = JsonUtility.FromJson<PlayerCardsFile>(reader.ReadString())?.Normalised(),
                    Decks = JsonUtility.FromJson<PlayerDecksFile>(reader.ReadString())?.Normalised(),
                    Pictures = new List<(string, string, byte[])>(),
                    Until = Time.unscaledTime + OfferSeconds,
                };
                int count = reader.ReadInt32();
                if (count < 0 || count > 4000) return;
                long total = 0;
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    string file = reader.ReadString();
                    int length = reader.ReadInt32();
                    if (length < 0 || total + length > MaxOfferBytes) return;
                    total += length;
                    offer.Pictures.Add((key, file, reader.ReadBytes(length)));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] {member.Name}'s offered cards could not be read: {exception.Message}");
                return;
            }

            if (offer.Cards == null || offer.Decks == null || string.IsNullOrEmpty(offer.GameId)) return;
            if (pendingOffer != null)
            {
                SendNotice(member, "The host is answering another player's cards; send yours again in a moment.");
                return;
            }

            pendingOffer = offer;
            string what = $"{offer.Cards.cards.Count} card(s), {offer.Cards.changes.Count} change(s) to the game's cards, "
                + $"{offer.Decks.decks.Count} deck(s) and {offer.Pictures.Count} picture(s)";
            host.ShowNotice($"{member.Name} offers {what}.");
            if (host.runtimeUi == null) return;
            List<PopupActionOption> actions = new List<PopupActionOption>(2)
            {
                new PopupActionOption("Use them in this room", true, () => AnswerOffer(true)),
                new PopupActionOption("Refuse", true, () => AnswerOffer(false)),
            };
            host.runtimeUi.ShowContextMenu(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                "CARDS FROM " + member.Name.ToUpperInvariant(),
                $"{member.Name} offers {what} for this game. Taken, they join your cards for this session only (yours win where "
                    + $"they clash; nothing is saved on your computer) and everyone plays them from the next table. No answer in {OfferSeconds:0} s refuses.",
                actions,
                host.ContextMenus.CloseContextMenu,
                host.ContextMenus.DismissPopupFromSecondary);
        }

        private void AnswerOffer(bool accept)
        {
            host.ContextMenus.CloseContextMenu();
            ContentOffer offer = pendingOffer;
            pendingOffer = null;
            if (offer == null) return;
            if (!accept)
            {
                SendNotice(offer.From, "The host did not take your cards this time.");
                Debug.Log($"[Online] The host refused {offer.From.Name}'s cards.");
                return;
            }

            try
            {
                // SESSION-OFFER: kept in memory for this room only; RefreshHostContent merges it into what the room plays.
                acceptedOffers.Add(offer);
                acceptedOfferStamp++;
                SendContentToRoom();
                string said = $"{offer.From.Name}'s cards are in this room's cards for this session (not saved on the host's computer). "
                    + "Everyone plays them from the next table.";
                host.ShowNotice(said);
                for (int i = 0; i < members.Count; i++)
                {
                    Member member = members[i];
                    if (!member.IsHost && member.Connected && member.Connection >= 0) SendNotice(member, said);
                }

                Debug.Log($"[Online] Took {offer.From.Name}'s content for this session only ({offer.Cards?.cards?.Count ?? 0} card(s), "
                    + $"{offer.Decks?.decks?.Count ?? 0} deck(s), {offer.Pictures?.Count ?? 0} picture(s)); nothing was written to disk.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] {offer.From.Name}'s cards could not be added: {exception.Message}");
                host.ShowNotice($"{offer.From.Name}'s cards could not be added (the log says why).");
            }
        }

        // SESSION-OFFER: the taken offers for the table's games joined to the host's cards, decks and picture list (the host's
        // records win), as items of the room's bundle. Only when the content is made, never per frame.
        private void AddAcceptedOffers(string root, TableContentNeeds needs, List<ContentItem> items, ref long total)
        {
            ContentPackStore disk = new ContentPackStore(root);
            int skipped = 0;
            foreach (string gameId in needs.GameIds)
            {
                PlayerCardsFile cards = null;
                PlayerDecksFile decks = null;
                ContentUploadIndex index = null;
                string folder = ContentPackStore.SafeName(gameId, "game");
                for (int o = 0; o < acceptedOffers.Count; o++)
                {
                    ContentOffer offer = acceptedOffers[o];
                    if (offer == null || !string.Equals(offer.GameId, gameId, StringComparison.OrdinalIgnoreCase)) continue;
                    cards = PlayerCardsFile.Merge(offer.Cards, cards ?? disk.LoadCards(gameId)); // the host's (and earlier) win
                    decks = PlayerDecksFile.Merge(offer.Decks, decks ?? disk.LoadDecks(gameId));
                    index = index ?? disk.LoadUploads(gameId);
                    for (int i = 0; offer.Pictures != null && i < offer.Pictures.Count; i++)
                    {
                        (string key, string file, byte[] bytes) = offer.Pictures[i];
                        string extension = Path.GetExtension(file ?? string.Empty).ToLowerInvariant();
                        if (bytes == null || bytes.Length == 0 || (extension != ".png" && extension != ".jpg" && extension != ".jpeg")) continue;
                        if (index.pictures.Exists(p => p != null && string.Equals(p.key, key, StringComparison.OrdinalIgnoreCase))) continue;
                        string name = ContentPackStore.SafeName(key, "picture") + extension;
                        string relative = folder + "/uploads/" + name;
                        if (items.Exists(item => string.Equals(item.Relative, relative, StringComparison.OrdinalIgnoreCase))) continue;
                        if (!TryAdd(items, new ContentItem { Relative = relative, Bytes = bytes, Length = bytes.Length }, ref total, ref skipped)) continue;
                        index.pictures.Add(new ContentArtFile { key = key, file = name });
                    }
                }

                if (cards == null) continue;
                cards.formatVersion = PlayerCardsFile.CurrentFormatVersion;
                decks.formatVersion = PlayerDecksFile.CurrentFormatVersion;
                if (string.IsNullOrEmpty(cards.gameStableId)) cards.gameStableId = gameId;
                if (string.IsNullOrEmpty(decks.gameStableId)) decks.gameStableId = gameId;
                SetJsonItem(items, folder + "/" + PlayerCardsFile.FileName, cards.Normalised(), ref total);
                SetJsonItem(items, folder + "/" + PlayerDecksFile.FileName, decks.Normalised(), ref total);
                SetJsonItem(items, folder + "/uploads/" + ContentUploadIndex.FileName, index, ref total);
            }

            if (skipped > 0) Debug.LogWarning($"[Online] {skipped} offered picture(s) are too big to share with the room and were left out.");
        }

        private static void SetJsonItem(List<ContentItem> items, string relative, object value, ref long total)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(value, true));
            int at = items.FindIndex(item => string.Equals(item.Relative, relative, StringComparison.OrdinalIgnoreCase));
            ContentItem made = new ContentItem { Relative = relative, Bytes = bytes, Length = bytes.Length };
            if (at >= 0)
            {
                total += bytes.Length - items[at].Length;
                items[at] = made;
            }
            else
            {
                items.Add(made);
                total += bytes.Length;
            }
        }

        private void TickOffer(float now)
        {
            if (pendingOffer == null || now < pendingOffer.Until) return;
            ContentOffer offer = pendingOffer;
            pendingOffer = null;
            host.ContextMenus.CloseContextMenu();
            SendNotice(offer.From, "The host did not answer about your cards; they were not added.");
            host.ShowNotice($"{offer.From.Name}'s cards were not answered and were not added.");
        }
    }
}
