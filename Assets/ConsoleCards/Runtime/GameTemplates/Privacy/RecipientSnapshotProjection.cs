using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates.Privacy
{
    /// <summary>
    /// PRIVACY-PROJECTION (doc 11 v2, slice P1). Builds the table snapshot one recipient may receive from the host's
    /// full table. NOT wired to the network: the live game still replicates every command to every computer, and a
    /// filtered snapshot is useless while that parallel path carries the same secrets (doc 10 §39). This is the pure,
    /// tested building block the recipient-filtering release will send instead of the full SessionUndoSnapshot.
    /// </summary>
    public static class RecipientSnapshotProjection
    {
        /// <summary>The definition every hidden object carries in a projection: a card back, nothing more.</summary>
        public static readonly ObjectDefinitionId HiddenDefinition =
            new ObjectDefinitionId(new Guid("c0c0cafe-0000-4000-8000-00000000b4c4"));

        /// <summary>A spectator: no seat, so no hand, no Console and no private card.</summary>
        public static PlayerId Spectator => PlayerId.Empty;

        /// <summary>
        /// The recipient's projection. <paramref name="sessionKey"/> is the host's secret for this session (never sent):
        /// hidden objects get ids derived from it, so the deterministic template ids (which encode which card is which)
        /// never reach another computer while the card is hidden.
        /// </summary>
        public static GameTemplateInitialSnapshot Project(MatchState match, PlayerId recipient, byte[] sessionKey)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (sessionKey == null || sessionKey.Length < 16) throw new ArgumentException("a session key of 16+ bytes is required", nameof(sessionKey));

            HashSet<TabletopObjectId> hidden = new HashSet<TabletopObjectId>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in match.Cards)
                if (!MayKnowIdentity(match, pair.Value, recipient)) hidden.Add(pair.Key);

            HashSet<ContainerId> hiddenOrder = new HashSet<ContainerId>();
            foreach (KeyValuePair<ContainerId, ContainerState> pair in match.Containers)
                if (OrderIsHidden(match, pair.Value, recipient, hidden)) hiddenOrder.Add(pair.Key);

            using (HMACSHA256 mac = new HMACSHA256(sessionKey))
            {
                byte[] who = recipient.Value.ToByteArray();
                TabletopObjectId Alias(TabletopObjectId id)
                {
                    byte[] input = new byte[32];
                    Buffer.BlockCopy(who, 0, input, 0, 16);
                    Buffer.BlockCopy(id.Value.ToByteArray(), 0, input, 16, 16);
                    byte[] hash = mac.ComputeHash(input);
                    byte[] guid = new byte[16];
                    Buffer.BlockCopy(hash, 0, guid, 0, 16);
                    return new TabletopObjectId(new Guid(guid));
                }

                return GameTemplateInitialSnapshot.Capture(match).ProjectFor(hidden, Alias, hiddenOrder, HiddenDefinition);
            }
        }

        /// <summary>A fresh random session key for the host (kept in memory on the host only).</summary>
        public static byte[] NewSessionKey()
        {
            byte[] key = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(key);
            return key;
        }

        /// <summary>
        /// Whether a recipient may learn a card's identity. Pawns, tokens and dice are always public.
        /// Own hand and own Console: known. A deck: unknown unless the card lies face up. AuthorityOnly: never.
        /// Owner/seat-private objects: only their owner. Anything else: known when it lies face up.
        /// </summary>
        public static bool MayKnowIdentity(MatchState match, CardInstanceState card, PlayerId recipient)
        {
            TabletopObjectState state = card.BaseState;
            bool isOwner = !recipient.IsEmpty && state.OwnerPlayerId == recipient;
            switch (state.Visibility)
            {
                case ObjectVisibility.AuthorityOnly: return false;
                case ObjectVisibility.OwnerOnly:
                case ObjectVisibility.SeatOnly:
                case ObjectVisibility.HiddenIdentityWithPublicBack:
                    if (!isOwner) return false;
                    break;
            }

            if (!state.ContainerId.IsEmpty && match.Containers.TryGetValue(state.ContainerId, out ContainerState container))
            {
                bool seatIsMine = SeatBelongsTo(match, container.OwnerSeatId, recipient);
                if (container.Visibility == ObjectVisibility.AuthorityOnly) return false;
                if (container.Kind == ContainerKind.Deck) return card.Face == CardFace.FaceUp;
                if (container.Kind == ContainerKind.Hand) return seatIsMine;
                if ((container.Visibility == ObjectVisibility.OwnerOnly || container.Visibility == ObjectVisibility.SeatOnly
                     || container.Visibility == ObjectVisibility.HiddenIdentityWithPublicBack) && !seatIsMine) return false;
                if (seatIsMine) return true; // own Console slot or own private pile
            }

            return card.Face == CardFace.FaceUp;
        }

        private static bool OrderIsHidden(MatchState match, ContainerState container, PlayerId recipient, HashSet<TabletopObjectId> hidden)
        {
            if (container.Kind == ContainerKind.Deck) return true;
            if (container.Kind == ContainerKind.Hand && !SeatBelongsTo(match, container.OwnerSeatId, recipient)) return true;
            int hiddenCount = 0;
            foreach (TabletopObjectId id in container.ObjectIds)
                if (hidden.Contains(id)) hiddenCount++;
            return hiddenCount > 1; // two or more hidden cards: their order alone would tell them apart
        }

        private static bool SeatBelongsTo(MatchState match, SeatId seatId, PlayerId recipient)
        {
            if (recipient.IsEmpty || seatId.IsEmpty) return false;
            return match.Seats.TryGetValue(seatId, out SeatState seat) && seat.OccupantPlayerId == recipient;
        }
    }
}
