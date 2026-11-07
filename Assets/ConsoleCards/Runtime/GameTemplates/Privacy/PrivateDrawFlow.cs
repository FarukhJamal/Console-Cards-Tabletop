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
        RuleRefused,
        /// <summary>PRIVACY-P6B: the request was made against a table that has changed since (Undo names the revision it saw).</summary>
        StaleRevision,
        /// <summary>PRIVACY-P6B: Undo: the latest change on the host's table is another seat's (or a game step).</summary>
        NotYourChange,
        /// <summary>PRIVACY-P6B: Undo: nothing to take back.</summary>
        NothingToUndo
    }

    /// <summary>PRIVACY-P6A (s150): what a client does with an update of a given revision.</summary>
    public enum PrivateRevisionStep
    {
        /// <summary>The next one: apply it.</summary>
        Apply,
        /// <summary>This table already shows it (a repeat, or older than the package it took since): ignore it.</summary>
        AlreadyShown,
        /// <summary>One or more were missed: ask for this computer's own package (recovery).</summary>
        Gap
    }

    public static class PrivateRevisionGate
    {
        public static PrivateRevisionStep Next(long shown, long received) =>
            received <= shown ? PrivateRevisionStep.AlreadyShown
            : received == shown + 1 ? PrivateRevisionStep.Apply
            : PrivateRevisionStep.Gap;
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
        /// <summary>One card moving (P3–P6A form, kept for its readers). With <see cref="Changes"/> set, Changes is what the client applies.</summary>
        public RecipientCardEvent Event;
        /// <summary>PRIVACY-P6B: every change to this recipient's table since the last update (one card or many).</summary>
        public PrivateChangeSet Changes;
        public string Digest;
        /// <summary>PRIVACY-P6A: the host's authoritative revision after this change (one per accepted action, the same for every recipient).</summary>
        public long Revision;
    }

    /// <summary>
    /// PRIVACY-P6A (s150): the public turn as the host's own Trap Floor turn service holds it (round, phase, whose turn).
    /// Everyone may know it; it carries no card. Phase is the game's own phase value (the game's enum, as a number).
    /// </summary>
    public sealed class PrivateTurnSnapshot
    {
        public int Round;
        public int Phase;
        public Guid TurnOwner;
        public Guid ActivePlayer;
        public bool Ended;
    }

    /// <summary>PRIVACY-P6A: one accepted turn transition, sent to every recipient alike (it is public).</summary>
    public sealed class PrivateTurnUpdate
    {
        public long Revision;
        public PrivateTurnSnapshot Turn;
    }

    /// <summary>
    /// PRIVACY-P6A: "End the turn I believe it is" (round, phase, whose turn), as the legacy End Turn request carries it.
    /// It names no sender: the host takes the sender from the connection, and checks the turn with the game's own
    /// End Turn authority (TrapFloorEndTurnAuthority); this class only carries the bytes.
    /// </summary>
    public sealed class PrivateEndTurnRequest
    {
        public Guid RequestId;
        public int Round;
        public int Phase;
        public PlayerId TurnOwner;
        public bool Skipped;
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
        private MatchState match; // PRIVACY-P6B: rebound when the host's table object is replaced (an Undo step)
        private readonly IPrivateSeats seats;
        private readonly IReadOnlyDictionary<TabletopObjectId, string> labels;
        private RecipientHandleTable handles; // PRIVACY-P6A: replaced (all handles rotated) by RotateHandles
        private readonly HiddenCardRequestGate gate = new HiddenCardRequestGate();
        private readonly ContainerTransferService transfer = new ContainerTransferService();

        // PRIVACY-P6B: what each recipient's table holds, as last sent (its projection then), and the containers whose
        // order it could not see. Every change set is the difference to it, so nothing changed on the host's table is
        // missed, however it was changed.
        private readonly Dictionary<PlayerId, MatchState> lastSent = new Dictionary<PlayerId, MatchState>();
        private readonly Dictionary<PlayerId, HashSet<ContainerId>> lastHiddenOrder = new Dictionary<PlayerId, HashSet<ContainerId>>();
        private Dictionary<ContainerId, List<TabletopObjectId>> lastHostOrder;
        private string lastHudSignature;

        /// <summary>
        /// PRIVACY-P6B: runs an accepted Draw/Place/Return/Discard move on the authority. The presenter sets it to the
        /// table's own command door (TransferCardCommand, then FlipCardCommand when a face is asked for), so the game's
        /// own rules, Undo history and views follow; null runs the container transfer directly (tests).
        /// </summary>
        public Func<PlayerId, TabletopObjectId, ContainerState, ContainerState, CardFace?, bool> TransferExecutor { get; set; }

        /// <summary>PRIVACY-P6B: packages sent because a change could not be described as object changes (counted by the caller).</summary>
        public int FallbackCount { get; private set; }

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

        /// <summary>
        /// PRIVACY-P6A (s150): the authoritative revision. It goes up by one for every accepted action (every recipient's
        /// update of that action carries the same number), every accepted turn transition, and every handle rotation.
        /// A refused, duplicate or stale request leaves it unchanged. Packages carry the current value, so a client
        /// knows which updates it has already seen (an older one is ignored) and when it missed one (a gap: it asks for
        /// its own package).
        /// </summary>
        public long Revision { get; private set; }

        /// <summary>PRIVACY-P6A: the public turn sent with every package (null: none known yet).</summary>
        public PrivateTurnSnapshot Turn { get; private set; }

        /// <summary>First join, reconnect and late join alike: the recipient's own package. Never the full table.</summary>
        public byte[] Join(PlayerId recipient)
        {
            PlayerId who = seats.IsSeated(recipient) ? recipient : RecipientSnapshotProjection.Spectator;
            RecipientBootstrapPackage package = RecipientBootstrapPackage.Build(match, handles, who, labels, Layout);
            package.Revision = Revision;
            package.Turn = Turn;
            package.Hud = Hud;
            Remember(who, handles.Project(match, who).Restore());
            return TableWire.Write(package, false);
        }

        /// <summary>PRIVACY-P6B: the public status card every package and change set carries (null: none).</summary>
        public PrivateHudSnapshot Hud { get; private set; }

        /// <summary>The request gate, for operations validated outside this class: false for a request id already run.</summary>
        public bool AcceptRequest(Guid requestId) => gate.Accept(requestId);

        /// <summary>The host's full table this authority projects from.</summary>
        public MatchState Match => match;

        /// <summary>The reference a recipient holds a card by (its handle when hidden from it).</summary>
        public TabletopObjectId ReferenceFor(PlayerId recipient, TabletopObjectId real)
        {
            handles.Sync(match, recipient);
            return handles.ReferenceFor(recipient, real);
        }

        /// <summary>Reads a reference a recipient sent (its handle, or a real id it may know). Never another recipient's handle or a hidden real id.</summary>
        public ReferenceResolution Resolve(PlayerId recipient, TabletopObjectId reference, out TabletopObjectId real) =>
            handles.Resolve(match, recipient, reference, out real);

        /// <summary>
        /// PRIVACY-P6B: the host's table object was replaced by one that continues it (an Undo step restores a copy).
        /// Handles, revision and what each recipient was last sent are kept: the next change set is the difference.
        /// </summary>
        public void Rebind(MatchState replacement)
        {
            match = replacement ?? throw new ArgumentNullException(nameof(replacement));
        }

        /// <summary>
        /// PRIVACY-P6B: runs one accepted change on the authority (<paramref name="mutate"/> uses the table's own
        /// services), then gives every recipient the change set to its own table, one revision for all. A container
        /// whose remaining cards changed order (a shuffle) withdraws every handle of its cards first, so no reference
        /// survives to follow a card through it. Null when nothing changed (no revision).
        /// </summary>
        public Dictionary<PlayerId, byte[]> Apply(Action mutate, PrivateTurnSnapshot turn = null, PrivateHudSnapshot hud = null)
        {
            mutate?.Invoke();
            return Publish(turn, hud);
        }

        /// <summary>As Apply, with the HUD read after the change (so the change and its HUD travel in one revision).</summary>
        public Dictionary<PlayerId, byte[]> Apply(Action mutate, PrivateTurnSnapshot turn, Func<PrivateHudSnapshot> hudAfter)
        {
            mutate?.Invoke();
            return Publish(turn, hudAfter?.Invoke());
        }

        /// <summary>PRIVACY-P6B: whatever changed on the host's table since the last update, as change sets (null: nothing).</summary>
        /// <summary>PRIVACY-P6B: the public HUD after a change (read after a Draw/Place/Return/Discard so it rides in the same revision).</summary>
        public Func<PrivateHudSnapshot> HudSource;

        public Dictionary<PlayerId, byte[]> Sweep(PrivateHudSnapshot hud = null) => Publish(null, hud);

        /// <summary>Recipients whose update of the last publish said "send my package": the caller sends it (counted).</summary>
        public readonly List<PlayerId> NeedPackage = new List<PlayerId>();

        private Dictionary<PlayerId, byte[]> Publish(PrivateTurnSnapshot turn, PrivateHudSnapshot hud, TabletopObjectId movedCard = default, ContainerId movedFrom = default, Dictionary<PlayerId, TabletopObjectId> before = null, Dictionary<PlayerId, bool> knewBefore = null, Dictionary<PlayerId, bool> orderHidden = null, int fromIndex = -1)
        {
            NeedPackage.Clear();
            WithdrawReshuffled();
            if (hud != null)
            {
                string signature = hud.Signature();
                if (signature == lastHudSignature) hud = null;
                else { lastHudSignature = signature; Hud = hud; }
            }

            if (turn != null) Turn = turn;
            Dictionary<PlayerId, PrivateChangeSet> sets = new Dictionary<PlayerId, PrivateChangeSet>();
            Dictionary<PlayerId, MatchState> now = new Dictionary<PlayerId, MatchState>();
            bool any = turn != null || hud != null;
            HashSet<PlayerId> joined = new HashSet<PlayerId>(lastSent.Keys);
            foreach (PlayerId r in Recipients)
            {
                MatchState current = handles.Project(match, r).Restore();
                RecipientSnapshotProjection.Classify(match, r, out _, out HashSet<ContainerId> hiddenOrder);
                lastSent.TryGetValue(r, out MatchState previous);
                lastHiddenOrder.TryGetValue(r, out HashSet<ContainerId> previousHidden);
                PrivateChangeSet set = PrivateChangeSetBuilder.Build(match, previous, previousHidden ?? new HashSet<ContainerId>(), current, hiddenOrder);
                set.Turn = turn;
                set.Hud = hud;
                sets[r] = set;
                now[r] = current;
                lastHiddenOrder[r] = hiddenOrder;
                if (!set.IsEmpty) any = true;
            }

            if (!any) return null;
            Revision++;
            Dictionary<PlayerId, byte[]> updates = new Dictionary<PlayerId, byte[]>();
            foreach (PlayerId r in Recipients)
            {
                PrivateChangeSet set = sets[r];
                lastSent[r] = now[r];
                if (set.NeedsPackage)
                {
                    NeedPackage.Add(r);
                    if (joined.Contains(r)) FallbackCount++; // a recipient never sent a package is not a fallback
                }
                RecipientCardEvent ev = null;
                if (!movedCard.IsEmpty)
                {
                    // The one-card event (P3–P6A form) for its readers; the change set is what the client applies.
                    ev = RecipientEventProjection.CardMoved(match, handles, r, movedCard, movedFrom);
                    if (knewBefore[r] && !RecipientSnapshotProjection.MayKnowIdentity(match, match.Cards[movedCard], r))
                    {
                        ev.PreviousReference = TabletopObjectId.Empty;
                        ev.FromIndex = orderHidden[r] ? -1 : fromIndex;
                    }
                    else ev.PreviousReference = before[r];
                }

                string digest = PrivateClientModel.FromTable(now[r]).Digest();
                updates[r] = TableWire.Write(new PrivateUpdate { Event = ev, Changes = set, Digest = digest, Revision = Revision }, false);
            }

            return updates;
        }

        private void Remember(PlayerId recipient, MatchState table)
        {
            lastSent[recipient] = table;
            RecipientSnapshotProjection.Classify(match, recipient, out _, out HashSet<ContainerId> hiddenOrder);
            lastHiddenOrder[recipient] = hiddenOrder;
            if (lastHostOrder == null) lastHostOrder = HostOrder();
        }

        private Dictionary<ContainerId, List<TabletopObjectId>> HostOrder()
        {
            Dictionary<ContainerId, List<TabletopObjectId>> order = new Dictionary<ContainerId, List<TabletopObjectId>>();
            foreach (KeyValuePair<ContainerId, ContainerState> pair in match.Containers) order[pair.Key] = new List<TabletopObjectId>(pair.Value.ObjectIds);
            return order;
        }

        // A container whose cards that stayed in it are now in another order was shuffled (or reordered, or an Undo
        // put it back): every handle of its cards is withdrawn for every recipient, so the next ones say nothing.
        private void WithdrawReshuffled()
        {
            Dictionary<ContainerId, List<TabletopObjectId>> now = HostOrder();
            if (lastHostOrder != null)
            {
                foreach (KeyValuePair<ContainerId, List<TabletopObjectId>> pair in now)
                {
                    if (!lastHostOrder.TryGetValue(pair.Key, out List<TabletopObjectId> was)) continue;
                    HashSet<TabletopObjectId> stayed = new HashSet<TabletopObjectId>(was);
                    stayed.IntersectWith(pair.Value);
                    List<TabletopObjectId> a = was.FindAll(stayed.Contains), b = pair.Value.FindAll(stayed.Contains);
                    bool reordered = false;
                    for (int i = 0; i < a.Count && !reordered; i++) reordered = a[i] != b[i];
                    if (reordered) handles.Reshuffled(match, pair.Key);
                }
            }

            lastHostOrder = now;
        }

        /// <summary>
        /// PRIVACY-P6A: the host's turn service accepted a turn transition (the caller checked the request with the game's
        /// own End Turn authority and ran the game's own turn service). One revision; the same public bytes for everyone.
        /// </summary>
        public byte[] TurnChanged(PrivateTurnSnapshot turn)
        {
            Turn = turn ?? throw new ArgumentNullException(nameof(turn));
            Revision++;
            return TableWire.Write(new PrivateTurnUpdate { Revision = Revision, Turn = turn }, false);
        }

        /// <summary>Sets the turn a first package carries, without a transition (the deal).</summary>
        public void SetInitialTurn(PrivateTurnSnapshot turn) => Turn = turn;

        /// <summary>PRIVACY-P6B: sets the status card a first package carries, without an update.</summary>
        public void SetInitialHud(PrivateHudSnapshot hud)
        {
            Hud = hud;
            lastHudSignature = hud?.Signature();
        }

        /// <summary>PRIVACY-P6B: publishes what changed with the turn and the status card (null when nothing changed).</summary>
        public Dictionary<PlayerId, byte[]> PublishChanges(PrivateTurnSnapshot turn, PrivateHudSnapshot hud) => Publish(turn, hud);

        /// <summary>
        /// PRIVACY-P6A: every recipient's every handle is withdrawn and new ones are issued under a fresh key (a change the
        /// host could not describe as one card event, such as a shuffle made by the game's own rules). Old handles then
        /// reach nothing (Unknown), so no handle survives to correlate a card before and after. One revision; every
        /// client must take its own package again.
        /// </summary>
        public void RotateHandles(byte[] newSessionKey)
        {
            handles = new RecipientHandleTable(newSessionKey);
            Revision++;
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
            return Move(sender, real, deck, hand, null, out updates);
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
            return Move(sender, real, match.Containers[card.BaseState.ContainerId], slot, CardFace.FaceUp, out updates);
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
            return Move(sender, real, from, hand, null, out updates);
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
            return Move(sender, real, from, pile, null, out updates);
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

        private PrivateRequestVerdict Move(PlayerId sender, TabletopObjectId real, ContainerState from, ContainerState to, CardFace? face, out Dictionary<PlayerId, byte[]> updates)
        {
            updates = null;
            Dictionary<PlayerId, TabletopObjectId> before = new Dictionary<PlayerId, TabletopObjectId>();
            Dictionary<PlayerId, bool> knewBefore = new Dictionary<PlayerId, bool>();
            Dictionary<PlayerId, bool> orderHidden = new Dictionary<PlayerId, bool>();
            int fromIndex = from.IndexOf(real);
            foreach (PlayerId r in Recipients)
            {
                handles.Sync(match, r);
                before[r] = handles.ReferenceFor(r, real);
                knewBefore[r] = RecipientSnapshotProjection.MayKnowIdentity(match, match.Cards[real], r);
                RecipientSnapshotProjection.Classify(match, r, out _, out HashSet<ContainerId> hiddenOrder);
                orderHidden[r] = hiddenOrder.Contains(from.Id);
            }

            if (TransferExecutor != null)
            {
                // The table's own command door: the game's rules, Undo history and views follow the move.
                if (!TransferExecutor(sender, real, from, to, face)) return PrivateRequestVerdict.InvalidDestination;
            }
            else
            {
                if (!transfer.MoveBetweenContainers(match.Cards[real].BaseState, from, to).Succeeded) return PrivateRequestVerdict.InvalidDestination;
                if (face.HasValue) match.Cards[real].SetFace(face.Value);
                else if (to.ArrivalFace != ContainerArrivalFace.Unchanged)
                    // The container's own arrival face, as the table's draw applies it (a card arriving in a hand turns face up).
                    match.Cards[real].SetFace(to.ArrivalFace == ContainerArrivalFace.FaceDown ? CardFace.FaceDown : CardFace.FaceUp);
            }

            LastCard = real;
            LastFrom = from.Id;
            LastTo = to.Id;
            // One accepted action, one revision, the same number in every recipient's update. The change set carries
            // the move and anything the game's own rules did with it.
            updates = Publish(null, HudSource?.Invoke(), real, from.Id, before, knewBefore, orderHidden, fromIndex);
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
            // PRIVACY-P6A: a concealment names the old view by its place; this model keeps no order, so it cannot
            // follow one (the ordered client table, RecipientEventApplier, does).
            if (ev.PreviousReference.IsEmpty) { Corrections++; return false; }
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
        public static string Apply(MatchState table, PrivateUpdate update) => Apply(table, update, null);

        /// <summary>PRIVACY-P6B: a change set when the update has one (what changed is told in <paramref name="summary"/>), else the one-card event.</summary>
        public static string Apply(MatchState table, PrivateUpdate update, PrivateChangeSummary summary)
        {
            if (update?.Changes != null) return PrivateChangeSetApplier.Apply(table, update.Changes, update.Digest, summary);
            if (table == null) return "no table";
            RecipientCardEvent ev = update?.Event;
            if (ev == null) return "no event";
            if (ev.PreviousReference.IsEmpty)
            {
                // PRIVACY-P6A: a concealment. The card is the one this table holds at that public place; the event
                // carries no id for it. Filled in here (this computer's own reference), so the scene can follow.
                if (ev.FromIndex < 0 || ev.From.IsEmpty || !table.Containers.TryGetValue(ev.From, out ContainerState left) || ev.FromIndex >= left.Count)
                    return "the event names its card by a place this table cannot read";
                ev.PreviousReference = left.GetObjectAt(ev.FromIndex);
            }

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
        public const byte KindEndTurn = 5; // PRIVACY-P6A: the turn the sender believes it is (owner, round, phase, skipped)
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

        public static byte[] EndTurn(PrivateEndTurnRequest request)
        {
            byte[] bytes = new byte[Size];
            bytes[0] = KindEndTurn;
            request.RequestId.ToByteArray().CopyTo(bytes, 1);
            request.TurnOwner.Value.ToByteArray().CopyTo(bytes, 17);
            BitConverter.GetBytes(request.Round).CopyTo(bytes, 33);
            bytes[37] = (byte)request.Phase;
            bytes[38] = request.Skipped ? (byte)1 : (byte)0;
            return bytes;
        }

        /// <summary>PRIVACY-P6A: exactly one well-formed End Turn request (anything else in the unused bytes is refused).</summary>
        public static bool TryReadEndTurn(byte[] bytes, int offset, out PrivateEndTurnRequest request)
        {
            request = null;
            if (bytes == null || offset < 0 || bytes.Length - offset != Size || bytes[offset] != KindEndTurn) return false;
            Guid id = new Guid(Slice(bytes, offset + 1));
            Guid owner = new Guid(Slice(bytes, offset + 17));
            if (id == Guid.Empty || owner == Guid.Empty) return false;
            if (bytes[offset + 38] > 1) return false;
            for (int i = offset + 39; i < offset + Size; i++) if (bytes[i] != 0) return false;
            request = new PrivateEndTurnRequest
            {
                RequestId = id,
                TurnOwner = new PlayerId(owner),
                Round = BitConverter.ToInt32(bytes, offset + 33),
                Phase = bytes[offset + 37],
                Skipped = bytes[offset + 38] == 1
            };
            return true;
        }

        private static byte[] Slice(byte[] bytes, int at)
        {
            byte[] part = new byte[16];
            Array.Copy(bytes, at, part, 0, 16);
            return part;
        }
    }
}
