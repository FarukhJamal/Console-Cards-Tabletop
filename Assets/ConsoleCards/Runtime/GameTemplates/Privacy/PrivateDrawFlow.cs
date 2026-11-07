using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates.Privacy
{
    // PRIVACY-P3 (s143, doc 10 §47). The first private action flow, end to end on the authoritative MatchState:
    // join → draw → (owner sees the face, others a back) → place face up (everyone sees it). Messages are TableWire bytes.
    // Development/test only: NOTHING in the live game calls this (the live table still replicates commands).

    public enum PrivateRequestVerdict
    {
        Accepted,
        Duplicate,
        NotSeated,
        WrongSeat,
        NotYourTurn,
        UnknownReference,
        HiddenRealId,
        InvalidSource,
        InvalidDestination,
        EmptyDeck,
        /// <summary>PRIVACY-P5 (s149): Full only, the game's own limit says no (the hand already holds its maximum).</summary>
        RuleRefused
    }

    /// <summary>"Draw the top card of my deck into my hand." The deck and the hand are public containers.</summary>
    public sealed class PrivateDrawRequest
    {
        public Guid RequestId;
        public ContainerId Deck;
    }

    /// <summary>"Put this card of mine face up in this slot of my Console." The card is named by the sender's reference.</summary>
    public sealed class PrivatePlaceRequest
    {
        public Guid RequestId;
        public TabletopObjectId Card;
        public ContainerId Destination;
    }

    /// <summary>
    /// PRIVACY-P5 (s149): "Put this card of mine back in my hand" (Return: Destination empty, the host takes the sender's
    /// own hand) or "Put this card of mine on that public discard pile" (Discard). The card is named by the sender's
    /// own reference; the destination never decides whose card it is.
    /// </summary>
    public sealed class PrivateCardRequest
    {
        public Guid RequestId;
        public TabletopObjectId Card;
        public ContainerId Destination;
    }

    /// <summary>What one recipient is sent after an accepted action: the event and a digest of its own table.</summary>
    public sealed class PrivateUpdate
    {
        public RecipientCardEvent Event;
        public string Digest;
    }

    /// <summary>The seats the host knows (from the room, never from the request).</summary>
    public interface IPrivateSeats
    {
        bool IsSeated(PlayerId player);
        bool IsTurnOf(PlayerId player);
        ContainerId DeckOf(PlayerId player);
        ContainerId HandOf(PlayerId player);
        bool IsOwnConsoleSlot(PlayerId player, ContainerId slot);
        IReadOnlyList<PlayerId> Seated { get; }
        /// <summary>PRIVACY-P5: the most cards the player's hand may hold after a Draw, or -1 for no limit (Free, Reminders).</summary>
        int DrawLimit(PlayerId player);
    }

    /// <summary>Host side. Holds the only full table, the session key and the reference books.</summary>
    public sealed class PrivateTableHost
    {
        private readonly MatchState match;
        private readonly IPrivateSeats seats;
        private readonly IReadOnlyDictionary<TabletopObjectId, string> labels;
        private readonly RecipientHandleTable handles;
        private readonly HiddenCardRequestGate gate = new HiddenCardRequestGate();
        private readonly ContainerTransferService transfer = new ContainerTransferService();

        public PrivateTableHost(MatchState match, IPrivateSeats seats, IReadOnlyDictionary<TabletopObjectId, string> labels, byte[] sessionKey)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            this.seats = seats ?? throw new ArgumentNullException(nameof(seats));
            this.labels = labels;
            handles = new RecipientHandleTable(sessionKey);
        }

        /// <summary>Everyone the host sends to: every seat, and one spectator view.</summary>
        public IEnumerable<PlayerId> Recipients
        {
            get
            {
                foreach (PlayerId p in seats.Seated) yield return p;
                yield return RecipientSnapshotProjection.Spectator;
            }
        }

        /// <summary>PRIVACY-P5 (s149): the source and destination of the last accepted move (the host's own scene follows them).</summary>
        public ContainerId LastFrom { get; private set; }
        public ContainerId LastTo { get; private set; }
        public TabletopObjectId LastCard { get; private set; }

        /// <summary>PRIVACY-P4 (s148): the public table template sent with every package (null: none).</summary>
        public RecipientPublicLayout Layout { get; set; }

        /// <summary>First join, reconnect and late join alike: the recipient's own package. Never the full table.</summary>
        public byte[] Join(PlayerId recipient)
        {
            PlayerId who = seats.IsSeated(recipient) ? recipient : RecipientSnapshotProjection.Spectator;
            return TableWire.Write(RecipientBootstrapPackage.Build(match, handles, who, labels, Layout), false);
        }

        public PrivateRequestVerdict Draw(PlayerId sender, PrivateDrawRequest request, out Dictionary<PlayerId, byte[]> updates)
        {
            updates = null;
            PrivateRequestVerdict check = Common(sender, request?.RequestId ?? Guid.Empty, needsTurn: true);
            if (check != PrivateRequestVerdict.Accepted) return check;
            if (request.Deck != seats.DeckOf(sender)) return PrivateRequestVerdict.WrongSeat; // another player's deck
            if (!match.Containers.TryGetValue(request.Deck, out ContainerState deck) || deck.Kind != ContainerKind.Deck) return PrivateRequestVerdict.InvalidSource;
            if (deck.Count == 0) return PrivateRequestVerdict.EmptyDeck;
            ContainerState hand = match.Containers[seats.HandOf(sender)];
            int limit = seats.DrawLimit(sender);
            if (limit >= 0 && hand.Count >= limit) return PrivateRequestVerdict.RuleRefused; // Full: the game's hand limit
            TabletopObjectId real = deck.GetObjectAt(deck.TopIndex); // the host decides which card it is
            return Move(real, deck, hand, null, out updates);
        }

        public PrivateRequestVerdict Place(PlayerId sender, PrivatePlaceRequest request, out Dictionary<PlayerId, byte[]> updates)
        {
            updates = null;
            PrivateRequestVerdict check = Common(sender, request?.RequestId ?? Guid.Empty, needsTurn: true);
            if (check != PrivateRequestVerdict.Accepted) return check;
            ReferenceResolution resolution = handles.Resolve(match, sender, request.Card, out TabletopObjectId real);
            if (resolution == ReferenceResolution.Unknown) return PrivateRequestVerdict.UnknownReference;
            if (resolution == ReferenceResolution.HiddenRealId) return PrivateRequestVerdict.HiddenRealId;
            if (!match.Cards.TryGetValue(real, out CardInstanceState card) || card.BaseState.ContainerId != seats.HandOf(sender))
                return PrivateRequestVerdict.InvalidSource; // only a card in the sender's own hand
            if (!seats.IsOwnConsoleSlot(sender, request.Destination) || !match.Containers.TryGetValue(request.Destination, out ContainerState slot))
                return PrivateRequestVerdict.InvalidDestination;
            return Move(real, match.Containers[card.BaseState.ContainerId], slot, CardFace.FaceUp, out updates);
        }

        /// <summary>
        /// PRIVACY-P5 (s149): a card of the sender's own Console back into the sender's own hand. The hand's arrival face
        /// applies (as a draw); every other recipient then holds the card under a fresh handle, as a back.
        /// </summary>
        public PrivateRequestVerdict Return(PlayerId sender, PrivateCardRequest request, out Dictionary<PlayerId, byte[]> updates)
        {
            updates = null;
            PrivateRequestVerdict check = Common(sender, request?.RequestId ?? Guid.Empty, needsTurn: true);
            if (check != PrivateRequestVerdict.Accepted) return check;
            ContainerId handId = seats.HandOf(sender);
            if (!request.Destination.IsEmpty && request.Destination != handId) return PrivateRequestVerdict.InvalidDestination; // only one's own hand
            PrivateRequestVerdict source = ResolveOwnCard(sender, request.Card, allowHand: false, out TabletopObjectId real, out ContainerState from);
            if (source != PrivateRequestVerdict.Accepted) return source;
            if (!match.Containers.TryGetValue(handId, out ContainerState hand)) return PrivateRequestVerdict.InvalidDestination;
            return Move(real, from, hand, null, out updates);
        }

        /// <summary>
        /// PRIVACY-P5 (s149): a card of the sender's own hand or own Console onto a public discard pile (a discard pile
        /// that belongs to no seat and shows its cards to everyone). The pile's own arrival face applies.
        /// </summary>
        public PrivateRequestVerdict Discard(PlayerId sender, PrivateCardRequest request, out Dictionary<PlayerId, byte[]> updates)
        {
            updates = null;
            PrivateRequestVerdict check = Common(sender, request?.RequestId ?? Guid.Empty, needsTurn: true);
            if (check != PrivateRequestVerdict.Accepted) return check;
            PrivateRequestVerdict source = ResolveOwnCard(sender, request.Card, allowHand: true, out TabletopObjectId real, out ContainerState from);
            if (source != PrivateRequestVerdict.Accepted) return source;
            if (!IsPublicDiscardPile(request.Destination, out ContainerState pile)) return PrivateRequestVerdict.InvalidDestination;
            return Move(real, from, pile, null, out updates);
        }

        /// <summary>A discard pile no seat owns, whose cards everyone may see.</summary>
        public bool IsPublicDiscardPile(ContainerId id, out ContainerState pile)
        {
            return match.Containers.TryGetValue(id, out pile)
                && pile.Kind == ContainerKind.DiscardPile
                && pile.OwnerSeatId.IsEmpty
                && pile.Visibility == ObjectVisibility.Public;
        }

        // The sender's own card, named by the sender's own reference: in its own hand (when allowed) or in one of its
        // own Console slots. Another seat's card, a forged or withdrawn handle, or a hidden card's real id is refused.
        private PrivateRequestVerdict ResolveOwnCard(PlayerId sender, TabletopObjectId reference, bool allowHand, out TabletopObjectId real, out ContainerState from)
        {
            from = null;
            ReferenceResolution resolution = handles.Resolve(match, sender, reference, out real);
            if (resolution == ReferenceResolution.Unknown) return PrivateRequestVerdict.UnknownReference;
            if (resolution == ReferenceResolution.HiddenRealId) return PrivateRequestVerdict.HiddenRealId;
            if (!match.Cards.TryGetValue(real, out CardInstanceState card)) return PrivateRequestVerdict.InvalidSource;
            ContainerId at = card.BaseState.ContainerId;
            bool own = (allowHand && at == seats.HandOf(sender)) || (!at.IsEmpty && seats.IsOwnConsoleSlot(sender, at));
            if (!own || !match.Containers.TryGetValue(at, out from)) return PrivateRequestVerdict.InvalidSource;
            return PrivateRequestVerdict.Accepted;
        }

        private PrivateRequestVerdict Common(PlayerId sender, Guid requestId, bool needsTurn)
        {
            if (!seats.IsSeated(sender)) return PrivateRequestVerdict.NotSeated;
            if (needsTurn && !seats.IsTurnOf(sender)) return PrivateRequestVerdict.NotYourTurn;
            if (!gate.Accept(requestId)) return PrivateRequestVerdict.Duplicate;
            return PrivateRequestVerdict.Accepted;
        }

        private PrivateRequestVerdict Move(TabletopObjectId real, ContainerState from, ContainerState to, CardFace? face, out Dictionary<PlayerId, byte[]> updates)
        {
            updates = null;
            Dictionary<PlayerId, TabletopObjectId> before = new Dictionary<PlayerId, TabletopObjectId>();
            foreach (PlayerId r in Recipients)
            {
                handles.Sync(match, r);
                before[r] = handles.ReferenceFor(r, real);
            }

            if (!transfer.MoveBetweenContainers(match.Cards[real].BaseState, from, to).Succeeded) return PrivateRequestVerdict.InvalidDestination;
            if (face.HasValue) match.Cards[real].SetFace(face.Value);
            else if (to.ArrivalFace != ContainerArrivalFace.Unchanged)
                // The container's own arrival face, as the table's draw applies it (a card arriving in a hand turns face up).
                match.Cards[real].SetFace(to.ArrivalFace == ContainerArrivalFace.FaceDown ? CardFace.FaceDown : CardFace.FaceUp);

            LastCard = real;
            LastFrom = from.Id;
            LastTo = to.Id;
            updates = new Dictionary<PlayerId, byte[]>();
            foreach (PlayerId r in Recipients)
            {
                RecipientCardEvent ev = RecipientEventProjection.CardMoved(match, handles, r, real, from.Id);
                ev.PreviousReference = before[r];
                string digest = PrivateClientModel.FromTable(handles.Project(match, r).Restore()).Digest();
                updates[r] = TableWire.Write(new PrivateUpdate { Event = ev, Digest = digest }, false);
            }

            return PrivateRequestVerdict.Accepted;
        }
    }

    /// <summary>Client side: what it shows, built from its package and kept up to date by its events alone.</summary>
    public sealed class PrivateClientModel
    {
        public readonly Dictionary<TabletopObjectId, ClientCardView> Cards = new Dictionary<TabletopObjectId, ClientCardView>();
        public readonly Dictionary<ContainerId, int> Counts = new Dictionary<ContainerId, int>();
        public int Corrections { get; private set; }

        public static PrivateClientModel FromPackage(byte[] packageBytes)
        {
            RecipientBootstrapPackage package = (RecipientBootstrapPackage)TableWire.Read(packageBytes, 0, packageBytes.Length, false);
            RecipientClientTable table = RecipientClientTable.Build(package);
            PrivateClientModel model = new PrivateClientModel();
            foreach (ClientCardView card in table.Cards) model.Cards[card.Reference] = card;
            foreach (KeyValuePair<ContainerId, int> pair in table.Counts) model.Counts[pair.Key] = pair.Value;
            return model;
        }

        public static PrivateClientModel FromTable(MatchState table)
        {
            PrivateClientModel model = new PrivateClientModel();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in table.Cards)
                model.Cards[pair.Key] = new ClientCardView
                {
                    Reference = pair.Key,
                    IsHidden = pair.Value.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition),
                    Definition = pair.Value.BaseState.DefinitionId,
                    Face = pair.Value.Face,
                    Container = pair.Value.BaseState.ContainerId
                };
            foreach (KeyValuePair<ContainerId, ContainerState> pair in table.Containers) model.Counts[pair.Key] = pair.Value.Count;
            return model;
        }

        /// <summary>Applies one update. False when the client's table no longer matches the host's check (it then asks for its own package again).</summary>
        public bool Apply(byte[] updateBytes)
        {
            PrivateUpdate update = (PrivateUpdate)TableWire.Read(updateBytes, 0, updateBytes.Length, false);
            RecipientCardEvent ev = update.Event;
            if (!Cards.TryGetValue(ev.PreviousReference, out ClientCardView old)) { Corrections++; return false; }
            Cards.Remove(ev.PreviousReference);
            bool hidden = ev.Definition.Equals(RecipientSnapshotProjection.HiddenDefinition);
            Cards[ev.Reference] = new ClientCardView
            {
                Reference = ev.Reference,
                IsHidden = hidden,
                Definition = ev.Definition,
                Face = ev.FaceUp ? CardFace.FaceUp : CardFace.FaceDown,
                Container = ev.To,
                Label = hidden ? string.Empty : old.Label
            };
            if (!ev.From.IsEmpty && Counts.ContainsKey(ev.From)) Counts[ev.From]--;
            if (!ev.To.IsEmpty) Counts[ev.To] = ev.ToCount;
            if (Digest() == update.Digest) return true;
            Corrections++;
            return false;
        }

        /// <summary>Over references, identities, faces, places and counts: what this recipient may know.</summary>
        public string Digest()
        {
            List<string> lines = new List<string>();
            foreach (ClientCardView c in Cards.Values)
                lines.Add("c " + c.Reference.Value.ToString("N") + " " + c.Definition.Value.ToString("N") + " " + c.Face + " " + c.Container.Value.ToString("N"));
            foreach (KeyValuePair<ContainerId, int> pair in Counts) lines.Add("k " + pair.Key.Value.ToString("N") + " " + pair.Value);
            lines.Sort(string.CompareOrdinal);
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
        }
    }

    /// <summary>
    /// PRIVACY-P4 (s148): the client side of one update on the client's own table (the table its scene is built from).
    /// The card the event names by its previous reference is taken out and put back under its new reference, with the
    /// identity the host sent (the hidden definition for a back), in the event's container. Nothing else changes. The
    /// host's digest is then checked against the whole table: a mismatch means the client asks for its own package
    /// again (never a full table).
    /// </summary>
    public static class RecipientEventApplier
    {
        public static PrivateUpdate Read(byte[] updateBytes) =>
            (PrivateUpdate)TableWire.Read(updateBytes, 0, updateBytes.Length, false);

        /// <summary>Null when applied and the digest matches; otherwise what went wrong (the table may then be stale).</summary>
        public static string Apply(MatchState table, PrivateUpdate update)
        {
            if (table == null) return "no table";
            RecipientCardEvent ev = update?.Event;
            if (ev == null) return "no event";
            if (!table.Cards.TryGetValue(ev.PreviousReference, out CardInstanceState old)) return "the event names a card this table does not have";
            if (ev.Definition.IsEmpty || ev.Reference.IsEmpty) return "the event has no reference or identity";
            if (!ev.To.IsEmpty && !table.Containers.ContainsKey(ev.To)) return "the event names a container this table does not have";
            if (!ev.Reference.Equals(ev.PreviousReference) && table.ContainsObject(ev.Reference)) return "the new reference is already in use";

            TabletopObjectState was = old.BaseState;
            table.RemoveObject(ev.PreviousReference); // also takes it out of its container
            TabletopObjectState now = new TabletopObjectState(
                ev.Reference, ev.Definition, TabletopObjectKind.Card, was.Pose, ContainerId.Empty,
                was.OwnerPlayerId, was.Visibility, was.IsUserLocked);
            table.AddUncontainedCard(new CardInstanceState(now, ev.FaceUp ? CardFace.FaceUp : CardFace.FaceDown));
            if (!ev.To.IsEmpty && !new ContainerTransferService().PlaceIntoContainer(now, table.Containers[ev.To]).Succeeded)
                return "the card could not be put in its container";

            string digest = PrivateClientModel.FromTable(table).Digest();
            return digest == update.Digest ? null : "the table no longer matches the host's check";
        }
    }

    /// <summary>
    /// PRIVACY-P4 (s148): what a guest may ask the host, as plain bytes (never a serialized object, so a client cannot
    /// make the host build arbitrary types). It names the action and its public arguments only; never a seat or a
    /// player: the host takes the sender from the room connection.
    /// </summary>
    public static class PrivateRequestCodec
    {
        public const byte KindDraw = 1;
        public const byte KindPlace = 2;
        public const byte KindReturn = 3;  // PRIVACY-P5: card, no destination (the host takes the sender's own hand)
        public const byte KindDiscard = 4; // PRIVACY-P5: card, discard pile
        public const int Size = 1 + 16 + 16 + 16;

        public static byte[] Draw(PrivateDrawRequest request)
        {
            byte[] bytes = new byte[Size];
            bytes[0] = KindDraw;
            request.RequestId.ToByteArray().CopyTo(bytes, 1);
            request.Deck.Value.ToByteArray().CopyTo(bytes, 17);
            return bytes;
        }

        public static byte[] Place(PrivatePlaceRequest request)
        {
            byte[] bytes = new byte[Size];
            bytes[0] = KindPlace;
            request.RequestId.ToByteArray().CopyTo(bytes, 1);
            request.Card.Value.ToByteArray().CopyTo(bytes, 17);
            request.Destination.Value.ToByteArray().CopyTo(bytes, 33);
            return bytes;
        }

        public static byte[] Card(byte kind, PrivateCardRequest request)
        {
            if (kind != KindReturn && kind != KindDiscard) throw new ArgumentOutOfRangeException(nameof(kind));
            byte[] bytes = new byte[Size];
            bytes[0] = kind;
            request.RequestId.ToByteArray().CopyTo(bytes, 1);
            request.Card.Value.ToByteArray().CopyTo(bytes, 17);
            request.Destination.Value.ToByteArray().CopyTo(bytes, 33);
            return bytes;
        }

        /// <summary>PRIVACY-P5: also reads Return and Discard (as <paramref name="card"/>).</summary>
        public static bool TryRead(byte[] bytes, int offset, out byte kind, out PrivateDrawRequest draw, out PrivatePlaceRequest place, out PrivateCardRequest card)
        {
            card = null;
            if (TryRead(bytes, offset, out kind, out draw, out place)) return true;
            if (bytes == null || offset < 0 || bytes.Length - offset != Size) return false;
            kind = bytes[offset];
            Guid id = new Guid(Slice(bytes, offset + 1));
            Guid a = new Guid(Slice(bytes, offset + 17));
            Guid b = new Guid(Slice(bytes, offset + 33));
            if (id == Guid.Empty || a == Guid.Empty) return false;
            if (kind == KindReturn) { card = new PrivateCardRequest { RequestId = id, Card = new TabletopObjectId(a), Destination = new ContainerId(b) }; return b == Guid.Empty; }
            if (kind == KindDiscard) { card = new PrivateCardRequest { RequestId = id, Card = new TabletopObjectId(a), Destination = new ContainerId(b) }; return b != Guid.Empty; }
            return false;
        }

        /// <summary>False for anything that is not exactly one well-formed request.</summary>
        public static bool TryRead(byte[] bytes, int offset, out byte kind, out PrivateDrawRequest draw, out PrivatePlaceRequest place)
        {
            kind = 0;
            draw = null;
            place = null;
            if (bytes == null || offset < 0 || bytes.Length - offset != Size) return false;
            kind = bytes[offset];
            Guid id = new Guid(Slice(bytes, offset + 1));
            Guid a = new Guid(Slice(bytes, offset + 17));
            Guid b = new Guid(Slice(bytes, offset + 33));
            if (id == Guid.Empty) return false;
            if (kind == KindDraw) { draw = new PrivateDrawRequest { RequestId = id, Deck = new ContainerId(a) }; return b == Guid.Empty; }
            if (kind == KindPlace) { place = new PrivatePlaceRequest { RequestId = id, Card = new TabletopObjectId(a), Destination = new ContainerId(b) }; return true; }
            return false;
        }

        private static byte[] Slice(byte[] bytes, int at)
        {
            byte[] part = new byte[16];
            Array.Copy(bytes, at, part, 0, 16);
            return part;
        }
    }
}
