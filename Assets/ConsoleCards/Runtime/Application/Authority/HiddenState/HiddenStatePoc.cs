using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Core.Identifiers;

// Stage 3 proof of concept (owner decision A, 2026-11-05): recipient-specific hidden state and host-checked commands.
// NOT wired into live play. It is a small, separate model so the rules of "who may know what" and "who may change
// what" can be tested on their own before the real table (MatchState) is moved onto them. See
// claude/stabilization/10_NETWORK_AUTHORITY_MIGRATION.md.
namespace ConsoleCards.Application.Authority.HiddenState
{
    /// <summary>Who may see the faces of the cards in a pile.</summary>
    public enum PileVisibility
    {
        /// <summary>Everyone, spectators too (cards played face up on the table, a discard pile).</summary>
        Public = 0,
        /// <summary>Only the owning seat (a Hand). Others learn how many cards it holds, nothing more.</summary>
        OwnerOnly = 1,
        /// <summary>Nobody (a face-down deck). Everyone learns the count only: no faces, no ids, no order.</summary>
        Nobody = 2,
    }

    /// <summary>One card as the authority holds it. Only the authority ever has these.</summary>
    public sealed class AuthorityCard
    {
        public AuthorityCard(TabletopObjectId id, string definitionId)
        {
            if (id.IsEmpty) throw new ArgumentException("A card needs an id.", nameof(id));
            Id = id;
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
        }

        public TabletopObjectId Id { get; }
        public string DefinitionId { get; }
    }

    public sealed class AuthorityPile
    {
        internal readonly List<AuthorityCard> cards = new List<AuthorityCard>();

        public AuthorityPile(string key, PileVisibility visibility, PlayerId owner)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A pile needs a key.", nameof(key));
            if (visibility == PileVisibility.OwnerOnly && owner.IsEmpty)
                throw new ArgumentException("An owner-only pile needs an owner.", nameof(owner));
            Key = key;
            Visibility = visibility;
            Owner = owner;
        }

        public string Key { get; }
        public PileVisibility Visibility { get; }
        public PlayerId Owner { get; }
        public IReadOnlyList<AuthorityCard> Cards => cards;

        /// <summary>Set-up only (dealing the table); play goes through <see cref="AuthorityRequestProcessor"/>.</summary>
        public void Add(AuthorityCard card) => cards.Add(card ?? throw new ArgumentNullException(nameof(card)));
    }

    /// <summary>
    /// The whole table as the authority (a dedicated server, or the host) holds it. Clients never receive this object;
    /// they receive <see cref="RecipientView"/>s built from it.
    /// </summary>
    public sealed class AuthorityTable
    {
        private readonly Dictionary<string, AuthorityPile> piles = new Dictionary<string, AuthorityPile>(StringComparer.Ordinal);
        private readonly List<PlayerId> seats = new List<PlayerId>();

        public IReadOnlyList<PlayerId> Seats => seats;
        public PlayerId TurnPlayer { get; set; }
        public long Revision { get; private set; }

        public void AddSeat(PlayerId player)
        {
            if (player.IsEmpty) throw new ArgumentException("A seat needs a player.", nameof(player));
            if (!seats.Contains(player)) seats.Add(player);
        }

        public bool IsSeated(PlayerId player) => !player.IsEmpty && seats.Contains(player);

        public AuthorityPile AddPile(string key, PileVisibility visibility, PlayerId owner = default)
        {
            AuthorityPile pile = new AuthorityPile(key, visibility, owner);
            piles.Add(key, pile);
            return pile;
        }

        public bool TryGetPile(string key, out AuthorityPile pile) => piles.TryGetValue(key ?? string.Empty, out pile);

        public IEnumerable<AuthorityPile> Piles => piles.Values;

        public static string HandKey(PlayerId player) => "hand:" + player.Value.ToString("N");

        internal void Touch() => Revision++;
    }

    /// <summary>Who a view is built for. A spectator has no seat.</summary>
    public readonly struct Recipient
    {
        private Recipient(PlayerId player) { Player = player; }
        public PlayerId Player { get; }
        public bool IsSpectator => Player.IsEmpty;
        public static Recipient Seat(PlayerId player)
        {
            if (player.IsEmpty) throw new ArgumentException("Use Recipient.Spectator for a viewer without a seat.", nameof(player));
            return new Recipient(player);
        }

        public static Recipient Spectator => new Recipient(PlayerId.Empty);
    }

    /// <summary>One card as a recipient may see it: always with its face, because hidden cards are not listed at all.</summary>
    public readonly struct CardView
    {
        public CardView(TabletopObjectId id, string definitionId)
        {
            Id = id;
            DefinitionId = definitionId;
        }

        public TabletopObjectId Id { get; }
        public string DefinitionId { get; }
    }

    public sealed class PileView
    {
        public PileView(string key, int count, IReadOnlyList<CardView> visibleCards)
        {
            Key = key;
            Count = count;
            VisibleCards = visibleCards;
        }

        public string Key { get; }
        /// <summary>How many cards the pile holds. Always known (as at a real table).</summary>
        public int Count { get; }
        /// <summary>The cards this recipient may see, in order; empty for a pile it may not look into.</summary>
        public IReadOnlyList<CardView> VisibleCards { get; }
    }

    /// <summary>Everything one recipient is sent. Built fresh for a first snapshot, a resync and a reconnect alike.</summary>
    public sealed class RecipientView
    {
        public RecipientView(long revision, PlayerId turnPlayer, IReadOnlyList<PileView> piles)
        {
            Revision = revision;
            TurnPlayer = turnPlayer;
            Piles = piles;
        }

        public long Revision { get; }
        public PlayerId TurnPlayer { get; }
        public IReadOnlyList<PileView> Piles { get; }

        /// <summary>A flat text form of every field, as a stand-in for the bytes that would go on the network.</summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append("rev=").Append(Revision).Append(";turn=").Append(TurnPlayer.Value.ToString("N"));
            for (int i = 0; i < Piles.Count; i++)
            {
                PileView pile = Piles[i];
                text.Append(";pile=").Append(pile.Key).Append(",count=").Append(pile.Count);
                for (int c = 0; c < pile.VisibleCards.Count; c++)
                {
                    text.Append(",card=").Append(pile.VisibleCards[c].Id.Value.ToString("N"))
                        .Append(':').Append(pile.VisibleCards[c].DefinitionId);
                }
            }

            return text.ToString();
        }
    }

    public static class RecipientViewBuilder
    {
        /// <summary>
        /// The only way table contents leave the authority. A pure function of (table, recipient): the first snapshot,
        /// a resync and a reconnect for the same seat give the same view, and a seat switch gives the new seat's view.
        /// </summary>
        public static RecipientView Build(AuthorityTable table, Recipient recipient)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            List<PileView> views = new List<PileView>();
            foreach (AuthorityPile pile in table.Piles)
            {
                bool mayLook = pile.Visibility == PileVisibility.Public
                    || (pile.Visibility == PileVisibility.OwnerOnly && !recipient.IsSpectator && pile.Owner == recipient.Player);
                List<CardView> cards = new List<CardView>();
                if (mayLook)
                {
                    for (int i = 0; i < pile.cards.Count; i++) cards.Add(new CardView(pile.cards[i].Id, pile.cards[i].DefinitionId));
                }

                views.Add(new PileView(pile.Key, pile.cards.Count, cards));
            }

            views.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key)); // no hint from insertion order
            return new RecipientView(table.Revision, table.TurnPlayer, views);
        }
    }

    // ---------- requests: what a client may ask for (never a state replacement) ----------

    public abstract class TableRequest
    {
    }

    /// <summary>Draw from a face-down pile into the requester's own Hand.</summary>
    public sealed class DrawRequest : TableRequest
    {
        public DrawRequest(string fromPile, int count)
        {
            FromPile = fromPile;
            Count = count;
        }

        public string FromPile { get; }
        public int Count { get; }
    }

    /// <summary>Play a card from the requester's own Hand into a public pile.</summary>
    public sealed class PlayCardRequest : TableRequest
    {
        public PlayCardRequest(TabletopObjectId card, string toPile)
        {
            Card = card;
            ToPile = toPile;
        }

        public TabletopObjectId Card { get; }
        public string ToPile { get; }
    }

    /// <summary>What today's clients effectively send (a whole table). Kept only so the processor can prove it refuses it.</summary>
    public sealed class ReplaceTableRequest : TableRequest
    {
    }

    public enum TableRequestError
    {
        None = 0,
        NotSeated = 1,
        NotYourTurn = 2,
        UnknownPile = 3,
        NotEnoughCards = 4,
        NotInYourHand = 5,
        NotAPublicPile = 6,
        StateReplacementRefused = 7,
        BadRequest = 8,
    }

    public readonly struct TableRequestResult
    {
        public TableRequestResult(TableRequestError error, long revision)
        {
            Error = error;
            Revision = revision;
        }

        public bool Accepted => Error == TableRequestError.None;
        public TableRequestError Error { get; }
        public long Revision { get; }
    }

    /// <summary>
    /// The authority's door: requests are checked against the table and the policy, then applied. Clients never write
    /// the table. Manual (Free) play keeps its freedom through the policy (no turn check), not by trusting clients.
    /// </summary>
    public sealed class AuthorityRequestProcessor
    {
        private readonly AuthorityTable table;

        public AuthorityRequestProcessor(AuthorityTable table)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
        }

        /// <summary>Off in Free / Manual play: anyone seated may act at any time, as at a real table.</summary>
        public bool RequireTurn { get; set; } = true;

        public TableRequestResult Submit(PlayerId requester, TableRequest request)
        {
            if (request is ReplaceTableRequest) return Refuse(TableRequestError.StateReplacementRefused);
            if (!table.IsSeated(requester)) return Refuse(TableRequestError.NotSeated); // spectators change nothing
            if (RequireTurn && requester != table.TurnPlayer) return Refuse(TableRequestError.NotYourTurn);
            switch (request)
            {
                case DrawRequest draw: return Draw(requester, draw);
                case PlayCardRequest play: return Play(requester, play);
                default: return Refuse(TableRequestError.BadRequest);
            }
        }

        private TableRequestResult Draw(PlayerId requester, DrawRequest draw)
        {
            if (draw.Count < 1) return Refuse(TableRequestError.BadRequest);
            if (!table.TryGetPile(draw.FromPile, out AuthorityPile from)) return Refuse(TableRequestError.UnknownPile);
            if (!table.TryGetPile(AuthorityTable.HandKey(requester), out AuthorityPile hand)) return Refuse(TableRequestError.UnknownPile);
            if (from.cards.Count < draw.Count) return Refuse(TableRequestError.NotEnoughCards);
            for (int i = 0; i < draw.Count; i++)
            {
                AuthorityCard top = from.cards[from.cards.Count - 1];
                from.cards.RemoveAt(from.cards.Count - 1);
                hand.cards.Add(top);
            }

            table.Touch();
            return new TableRequestResult(TableRequestError.None, table.Revision);
        }

        private TableRequestResult Play(PlayerId requester, PlayCardRequest play)
        {
            if (!table.TryGetPile(AuthorityTable.HandKey(requester), out AuthorityPile hand)) return Refuse(TableRequestError.UnknownPile);
            if (!table.TryGetPile(play.ToPile, out AuthorityPile to)) return Refuse(TableRequestError.UnknownPile);
            if (to.Visibility != PileVisibility.Public) return Refuse(TableRequestError.NotAPublicPile);
            int index = hand.cards.FindIndex(c => c.Id == play.Card);
            if (index < 0) return Refuse(TableRequestError.NotInYourHand); // a guessed or another player's card id does nothing
            AuthorityCard card = hand.cards[index];
            hand.cards.RemoveAt(index);
            to.cards.Add(card);
            table.Touch();
            return new TableRequestResult(TableRequestError.None, table.Revision);
        }

        private TableRequestResult Refuse(TableRequestError error) => new TableRequestResult(error, table.Revision);
    }

    /// <summary>
    /// Seat claims on (re)connect. The secret never leaves the authority and the joining client; the roster only ever
    /// carries the public id, which cannot claim a seat (the SEC-1 rule, as a testable unit).
    /// </summary>
    public sealed class SeatClaims
    {
        private readonly Dictionary<string, PlayerId> bySecret = new Dictionary<string, PlayerId>(StringComparer.Ordinal);

        public void Register(PlayerId player, string secret)
        {
            if (player.IsEmpty) throw new ArgumentException("A claim needs a player.", nameof(player));
            if (string.IsNullOrEmpty(secret) || secret.Length < 16) throw new ArgumentException("The secret is too short.", nameof(secret));
            bySecret[secret] = player;
        }

        /// <summary>The seat for a presented secret, or a spectator when it matches none.</summary>
        public Recipient Claim(string presented)
        {
            return presented != null && bySecret.TryGetValue(presented, out PlayerId player)
                ? Recipient.Seat(player)
                : Recipient.Spectator;
        }
    }
}
