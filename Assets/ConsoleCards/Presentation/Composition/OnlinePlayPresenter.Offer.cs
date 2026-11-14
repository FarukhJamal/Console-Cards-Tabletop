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
                new PopupActionOption("Add them to my cards", true, () => AnswerOffer(true)),
                new PopupActionOption("Refuse", true, () => AnswerOffer(false)),
            };
            host.runtimeUi.ShowContextMenu(
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                "CARDS FROM " + member.Name.ToUpperInvariant(),
                $"{member.Name} offers {what} for this game. Added, they join your own cards (yours win where they clash) and "
                    + $"everyone plays them from the next table. No answer in {OfferSeconds:0} s refuses.",
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
                ContentPackStore mine = ContentPackStore.ForOwnEdits(host.EnsureProfile());
                PlayerCardsFile cards = PlayerCardsFile.Merge(offer.Cards, mine.LoadCards(offer.GameId)); // the host's win
                PlayerDecksFile decks = PlayerDecksFile.Merge(offer.Decks, mine.LoadDecks(offer.GameId));
                ContentUploadIndex index = mine.LoadUploads(offer.GameId);
                int added = 0;
                for (int i = 0; i < offer.Pictures.Count; i++)
                {
                    (string key, string file, byte[] bytes) = offer.Pictures[i];
                    string extension = Path.GetExtension(file ?? string.Empty).ToLowerInvariant();
                    if (bytes == null || bytes.Length == 0 || (extension != ".png" && extension != ".jpg" && extension != ".jpeg")) continue;
                    if (index.pictures.Exists(p => p != null && string.Equals(p.key, key, StringComparison.OrdinalIgnoreCase))) continue;
                    string name = ContentPackStore.SafeName(key, "picture") + extension;
                    string folder = mine.UploadsFolder(offer.GameId);
                    Directory.CreateDirectory(folder);
                    File.WriteAllBytes(Path.Combine(folder, name), bytes);
                    index.pictures.Add(new ContentArtFile { key = key, file = name });
                    added++;
                }

                bool saved = mine.SaveCards(offer.GameId, cards) & mine.SaveDecks(offer.GameId, decks);
                mine.SaveUploads(offer.GameId, index);
                string said = saved
                    ? $"{offer.From.Name}'s cards were added to the host's cards. Everyone plays them from the next table."
                    : $"{offer.From.Name}'s cards could not all be saved on the host's computer.";
                host.ShowNotice(said);
                for (int i = 0; i < members.Count; i++)
                {
                    Member member = members[i];
                    if (!member.IsHost && member.Connected && member.Connection >= 0) SendNotice(member, said);
                }

                Debug.Log($"[Online] Took {offer.From.Name}'s content into the host's saves for {offer.GameId}: {cards.cards.Count} card(s) "
                    + $"and {decks.decks.Count} deck(s) in all, {added} new picture(s).");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] {offer.From.Name}'s cards could not be added: {exception.Message}");
                host.ShowNotice($"{offer.From.Name}'s cards could not be added (the log says why).");
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
