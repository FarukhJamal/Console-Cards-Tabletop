using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates.Privacy
{
    // PRIVACY-P2 (s142, doc 11 §13, doc 10 §42). The isolated, development-only seed-free path. NOTHING here is called by
    // the live game: the normal online table still deals from the shared seed and replays every command (doc 10 §39).

    /// <summary>How the host read a reference a client sent.</summary>
    public enum ReferenceResolution
    {
        /// <summary>A card this recipient may identify (its real id), or a hidden card under this recipient's current handle.</summary>
        Resolved,
        /// <summary>Not a reference this recipient was given: forged, another recipient's handle, or a handle withdrawn by a reveal or a shuffle.</summary>
        Unknown,
        /// <summary>The real id of a card hidden from this recipient: refused, so real ids cannot be probed.</summary>
        HiddenRealId
    }

    /// <summary>
    /// Host-only. The opaque references (handles) each recipient sees for the cards hidden from it.
    /// A handle is HMAC(session key, recipient, counter): it names nothing about the card. A card keeps its handle while it
    /// stays hidden and does not move through a shuffle; a reveal or a shuffle withdraws it, and the card gets a fresh handle
    /// the next time it is hidden, so a handle seen before a reveal cannot be followed afterwards. Handles are issued in an
    /// order that depends only on public information (container, public position), so two tables that differ only in
    /// hidden identities or hidden order give the same handles.
    /// </summary>
    public sealed class RecipientHandleTable
    {
        private sealed class Book
        {
            public long Counter;
            public readonly Dictionary<TabletopObjectId, TabletopObjectId> RealToHandle = new Dictionary<TabletopObjectId, TabletopObjectId>();
            public readonly Dictionary<TabletopObjectId, TabletopObjectId> HandleToReal = new Dictionary<TabletopObjectId, TabletopObjectId>();
        }

        private readonly byte[] key;
        private readonly Dictionary<PlayerId, Book> books = new Dictionary<PlayerId, Book>();

        public RecipientHandleTable(byte[] sessionKey)
        {
            if (sessionKey == null || sessionKey.Length < 16) throw new ArgumentException("a session key of 16+ bytes is required", nameof(sessionKey));
            key = (byte[])sessionKey.Clone();
        }

        /// <summary>Brings the recipient's handles up to date with the table: withdraws handles of cards it may now identify, issues new ones.</summary>
        public void Sync(MatchState match, PlayerId recipient)
        {
            RecipientSnapshotProjection.Classify(match, recipient, out HashSet<TabletopObjectId> hidden, out HashSet<ContainerId> hiddenOrder);
            Book book = BookOf(recipient);
            List<TabletopObjectId> withdraw = new List<TabletopObjectId>();
            foreach (KeyValuePair<TabletopObjectId, TabletopObjectId> pair in book.RealToHandle)
                if (!hidden.Contains(pair.Key)) withdraw.Add(pair.Key);
            foreach (TabletopObjectId real in withdraw) Withdraw(book, real);

            List<KeyValuePair<string, TabletopObjectId>> needed = new List<KeyValuePair<string, TabletopObjectId>>();
            foreach (TabletopObjectId real in hidden)
                if (!book.RealToHandle.ContainsKey(real)) needed.Add(new KeyValuePair<string, TabletopObjectId>(PublicPlace(match, real, hiddenOrder), real));
            // Public order only. Ties (cards whose public place is the same) receive the same set of handles either way.
            needed.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            foreach (KeyValuePair<string, TabletopObjectId> item in needed)
            {
                TabletopObjectId handle = NewHandle(recipient, ++book.Counter);
                book.RealToHandle[item.Value] = handle;
                book.HandleToReal[handle] = item.Value;
            }
        }

        /// <summary>A shuffle of this container: every handle of its cards is withdrawn for every recipient.</summary>
        public void Reshuffled(MatchState match, ContainerId containerId)
        {
            if (!match.Containers.TryGetValue(containerId, out ContainerState container)) return;
            foreach (Book book in books.Values)
                foreach (TabletopObjectId real in container.ObjectIds) Withdraw(book, real);
        }

        /// <summary>The recipient's projection with its handles (Sync first).</summary>
        public GameTemplateInitialSnapshot Project(MatchState match, PlayerId recipient)
        {
            Sync(match, recipient);
            RecipientSnapshotProjection.Classify(match, recipient, out HashSet<TabletopObjectId> hidden, out HashSet<ContainerId> hiddenOrder);
            Book book = BookOf(recipient);
            return GameTemplateInitialSnapshot.Capture(match).ProjectFor(hidden, id => book.RealToHandle[id], hiddenOrder, RecipientSnapshotProjection.HiddenDefinition);
        }

        /// <summary>The reference this recipient knows a card by (its handle when hidden, else its real id).</summary>
        public TabletopObjectId ReferenceFor(PlayerId recipient, TabletopObjectId real)
        {
            return BookOf(recipient).RealToHandle.TryGetValue(real, out TabletopObjectId handle) ? handle : real;
        }

        /// <summary>Reads a reference a client sent (Sync first). Never accepts another recipient's handle or a hidden card's real id.</summary>
        public ReferenceResolution Resolve(MatchState match, PlayerId recipient, TabletopObjectId reference, out TabletopObjectId real)
        {
            Sync(match, recipient);
            Book book = BookOf(recipient);
            if (book.HandleToReal.TryGetValue(reference, out real)) return ReferenceResolution.Resolved;
            if (match.Cards.TryGetValue(reference, out CardInstanceState card))
            {
                real = reference;
                if (RecipientSnapshotProjection.MayKnowIdentity(match, card, recipient)) return ReferenceResolution.Resolved;
                real = TabletopObjectId.Empty;
                return ReferenceResolution.HiddenRealId;
            }

            if (match.ContainsObject(reference)) { real = reference; return ReferenceResolution.Resolved; } // pawns, dice, tokens
            real = TabletopObjectId.Empty;
            return ReferenceResolution.Unknown;
        }

        private Book BookOf(PlayerId recipient)
        {
            if (!books.TryGetValue(recipient, out Book book)) books[recipient] = book = new Book();
            return book;
        }

        private static void Withdraw(Book book, TabletopObjectId real)
        {
            if (!book.RealToHandle.TryGetValue(real, out TabletopObjectId handle)) return;
            book.RealToHandle.Remove(real);
            book.HandleToReal.Remove(handle);
        }

        private TabletopObjectId NewHandle(PlayerId recipient, long counter)
        {
            byte[] input = new byte[24];
            Buffer.BlockCopy(recipient.Value.ToByteArray(), 0, input, 0, 16);
            Buffer.BlockCopy(BitConverter.GetBytes(counter), 0, input, 16, 8);
            using (HMACSHA256 mac = new HMACSHA256(key))
            {
                byte[] hash = mac.ComputeHash(input);
                byte[] guid = new byte[16];
                Buffer.BlockCopy(hash, 0, guid, 0, 16);
                return new TabletopObjectId(new Guid(guid));
            }
        }

        // Container and position only; the index inside a container whose order is hidden is left out.
        private static string PublicPlace(MatchState match, TabletopObjectId real, HashSet<ContainerId> hiddenOrder)
        {
            TabletopObjectState state = match.GetObject(real);
            if (state.ContainerId.IsEmpty) return "~|" + state.Pose;
            string index = hiddenOrder.Contains(state.ContainerId) ? string.Empty : match.Containers[state.ContainerId].IndexOf(real).ToString("D6");
            return state.ContainerId.Value.ToString("N") + "|" + index + "|" + state.Pose;
        }
    }

    /// <summary>Host-only: a hidden-card request is run once, whatever the network repeats.</summary>
    public sealed class HiddenCardRequestGate
    {
        private readonly HashSet<Guid> seen = new HashSet<Guid>();
        private readonly Queue<Guid> order = new Queue<Guid>();
        public const int Capacity = 4096;

        public bool Accept(Guid requestId)
        {
            if (requestId == Guid.Empty || seen.Contains(requestId)) return false;
            seen.Add(requestId);
            order.Enqueue(requestId);
            if (order.Count > Capacity) seen.Remove(order.Dequeue());
            return true;
        }
    }

    /// <summary>One card moving, as one recipient may be told about it (the event replaces the replayed command).</summary>
    public sealed class RecipientCardEvent
    {
        public TabletopObjectId Reference;
        public ContainerId From;
        public ContainerId To;
        public ObjectDefinitionId Definition;
        public bool FaceUp;
        public int ToCount;
    }

    public static class RecipientEventProjection
    {
        /// <summary>Built AFTER the host applied the move. The face travels only to a recipient that may now identify the card.</summary>
        public static RecipientCardEvent CardMoved(MatchState after, RecipientHandleTable handles, PlayerId recipient, TabletopObjectId real, ContainerId from)
        {
            handles.Sync(after, recipient);
            CardInstanceState card = after.Cards[real];
            bool known = RecipientSnapshotProjection.MayKnowIdentity(after, card, recipient);
            ContainerId to = card.BaseState.ContainerId;
            return new RecipientCardEvent
            {
                Reference = handles.ReferenceFor(recipient, real),
                From = from,
                To = to,
                Definition = known ? card.BaseState.DefinitionId : RecipientSnapshotProjection.HiddenDefinition,
                FaceUp = card.Face == CardFace.FaceUp,
                ToCount = to.IsEmpty ? 0 : after.Containers[to].Count
            };
        }
    }

    /// <summary>A public text label (card name) for one reference.</summary>
    public sealed class RecipientLabel
    {
        public TabletopObjectId Reference;
        public string Text;
    }

    /// <summary>
    /// What the host sends a joining computer on the seed-free path: the projection and the labels of the cards it may
    /// identify. No seed, no command history, no label of a hidden card.
    /// </summary>
    public sealed class RecipientBootstrapPackage
    {
        public GameTemplateInitialSnapshot Table;
        public List<RecipientLabel> Labels;
        public string Digest;

        public static RecipientBootstrapPackage Build(MatchState match, RecipientHandleTable handles, PlayerId recipient, IReadOnlyDictionary<TabletopObjectId, string> labels)
        {
            GameTemplateInitialSnapshot table = handles.Project(match, recipient);
            RecipientSnapshotProjection.Classify(match, recipient, out HashSet<TabletopObjectId> hidden, out _);
            List<RecipientLabel> visible = new List<RecipientLabel>();
            if (labels != null)
            {
                List<TabletopObjectId> ids = new List<TabletopObjectId>(labels.Keys);
                ids.Sort((a, b) => a.Value.CompareTo(b.Value));
                foreach (TabletopObjectId id in ids)
                    if (!hidden.Contains(id) && match.ContainsObject(id)) visible.Add(new RecipientLabel { Reference = id, Text = labels[id] });
            }

            return new RecipientBootstrapPackage { Table = table, Labels = visible, Digest = RecipientTableDigest.Of(table.Restore()) };
        }
    }

    /// <summary>
    /// A consistency check over what one recipient may know: the host computes it over the recipient's projection, the
    /// client over its own table. Equal tables give equal digests; hidden facts never enter it.
    /// </summary>
    public static class RecipientTableDigest
    {
        public static string Of(MatchState table)
        {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in table.Cards)
                lines.Add("c " + pair.Key.Value.ToString("N") + " " + pair.Value.BaseState.DefinitionId.Value.ToString("N") + " " + pair.Value.Face
                    + " " + pair.Value.BaseState.ContainerId.Value.ToString("N"));
            foreach (KeyValuePair<ContainerId, ContainerState> pair in table.Containers)
            {
                StringBuilder order = new StringBuilder("k " + pair.Key.Value.ToString("N"));
                foreach (TabletopObjectId id in pair.Value.ObjectIds) order.Append(' ').Append(id.Value.ToString("N"));
                lines.Add(order.ToString());
            }

            lines.Sort(string.CompareOrdinal);
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
        }
    }

    /// <summary>One object as the seed-free client presents it.</summary>
    public sealed class ClientCardView
    {
        public TabletopObjectId Reference;
        public bool IsHidden;
        public ObjectDefinitionId Definition;
        public CardFace Face;
        public ContainerId Container;
        public string Label;
    }

    /// <summary>
    /// The client side of the seed-free path: builds its table and presentation model from the package alone. It takes
    /// no seed, no template and no random source, so it cannot deal anything; a hidden card is a back, and a card that is
    /// face up with no identity is an error, never a reason to look elsewhere.
    /// </summary>
    public sealed class RecipientClientTable
    {
        public MatchState Table { get; private set; }
        public IReadOnlyList<ClientCardView> Cards { get; private set; }
        public IReadOnlyDictionary<ContainerId, int> Counts { get; private set; }

        public static RecipientClientTable Build(RecipientBootstrapPackage package)
        {
            if (package == null || package.Table == null) throw new ArgumentNullException(nameof(package));
            MatchState table = package.Table.Restore();
            if (RecipientTableDigest.Of(table) != package.Digest)
                throw new InvalidOperationException("The table does not match the host's check: ask the host for the table again.");

            Dictionary<TabletopObjectId, string> labels = new Dictionary<TabletopObjectId, string>();
            if (package.Labels != null) foreach (RecipientLabel label in package.Labels) labels[label.Reference] = label.Text;

            List<ClientCardView> cards = new List<ClientCardView>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in table.Cards)
            {
                bool hidden = pair.Value.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition);
                if (hidden && pair.Value.Face == CardFace.FaceUp && !IsInHand(table, pair.Value))
                    throw new InvalidOperationException($"Card {pair.Key} lies face up but its identity was not sent: the client does not guess.");
                cards.Add(new ClientCardView
                {
                    Reference = pair.Key,
                    IsHidden = hidden,
                    Definition = pair.Value.BaseState.DefinitionId,
                    Face = pair.Value.Face,
                    Container = pair.Value.BaseState.ContainerId,
                    Label = hidden ? string.Empty : (labels.TryGetValue(pair.Key, out string text) ? text : string.Empty)
                });
            }

            Dictionary<ContainerId, int> counts = new Dictionary<ContainerId, int>();
            foreach (KeyValuePair<ContainerId, ContainerState> pair in table.Containers) counts[pair.Key] = pair.Value.Count;
            return new RecipientClientTable { Table = table, Cards = cards, Counts = counts };
        }

        // Another player's hand arrives face up (cards turn face up in any hand) but is drawn as backs.
        private static bool IsInHand(MatchState table, CardInstanceState card)
        {
            return !card.BaseState.ContainerId.IsEmpty
                && table.Containers.TryGetValue(card.BaseState.ContainerId, out ContainerState container)
                && container.Kind == ContainerKind.Hand;
        }
    }
}
