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
        EmptyDeck
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

        /// <summary>First join, reconnect and late join alike: the recipient's own package. Never the full table.</summary>
        public byte[] Join(PlayerId recipient)
        {
            PlayerId who = seats.IsSeated(recipient) ? recipient : RecipientSnapshotProjection.Spectator;
            return TableWire.Write(RecipientBootstrapPackage.Build(match, handles, who, labels), false);
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
}
