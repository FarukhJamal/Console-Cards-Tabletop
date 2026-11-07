using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.GameTemplates.Privacy;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// PRIVACY-P5 (s149): the private card lifecycle on the host's table — Draw, Place face up, Return to hand,
    /// Discard — with what each recipient's own table (built from its package, kept by its updates) knows after each
    /// step, and the requests the host refuses.
    /// </summary>
    public sealed class PrivateCardLifecycleTests
    {
        private static readonly byte[] Key = { 9, 9, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };
        private const int Seed = 0x5EED7777;

        private sealed class Seats : IPrivateSeats
        {
            public TrapFloorTestTable T;
            public bool AnyTime = true;  // Free / Reminders
            public int Limit = -1;       // Full: the game's hand limit
            public bool IsSeated(PlayerId player) => Array.IndexOf(T.Players, player) >= 0;
            public bool IsTurnOf(PlayerId player) => IsSeated(player) && (AnyTime || T.Turn.ActivePlayerId == player);
            public ContainerId DeckOf(PlayerId player) => T.SetupOf(player).ControllerDeckId;
            public ContainerId HandOf(PlayerId player) => T.SetupOf(player).HandContainerId;
            public bool IsOwnConsoleSlot(PlayerId player, ContainerId slot)
            {
                if (!IsSeated(player)) return false;
                foreach (ContainerId own in T.SetupOf(player).SideSlotContainerIds) if (own == slot) return true;
                return false;
            }
            public IReadOnlyList<PlayerId> Seated => T.Players;
            public int DrawLimit(PlayerId player) => Limit;
        }

        private sealed class Room
        {
            public TrapFloorTestTable T;
            public Seats S;
            public PrivateTableHost Host;
            public PlayerId A, B;
            public readonly Dictionary<PlayerId, MatchState> Tables = new Dictionary<PlayerId, MatchState>();

            public static Room Open()
            {
                Room r = new Room { T = TrapFloorTestTable.Create(3, Seed) };
                r.S = new Seats { T = r.T };
                r.Host = new PrivateTableHost(r.T.Match, r.S, r.T.Template.CardLabels, Key);
                r.A = r.T.Turn.ActivePlayerId;
                r.B = r.T.Players[Array.IndexOf(r.T.Players, r.A) == 0 ? 1 : 0];
                foreach (PlayerId p in r.Host.Recipients) r.Rejoin(p);
                return r;
            }

            public void Rejoin(PlayerId p)
            {
                byte[] package = Host.Join(p);
                Tables[p] = RecipientClientTable.Build((RecipientBootstrapPackage)TableWire.Read(package, 0, package.Length, false)).Table;
            }

            public void Deliver(Dictionary<PlayerId, byte[]> updates)
            {
                Assert.AreEqual(Tables.Count, updates.Count, "one update per recipient");
                foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
                    Assert.IsNull(RecipientEventApplier.Apply(Tables[pair.Key], RecipientEventApplier.Read(pair.Value)), "update applies with a matching digest");
            }

            public TabletopObjectId DrawFor(PlayerId p)
            {
                Assert.AreEqual(PrivateRequestVerdict.Accepted,
                    Host.Draw(p, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = T.SetupOf(p).ControllerDeckId }, out Dictionary<PlayerId, byte[]> u));
                Deliver(u);
                ContainerState hand = Tables[p].Containers[T.SetupOf(p).HandContainerId];
                return hand.GetObjectAt(hand.Count - 1);
            }

            public ContainerId Discard()
            {
                foreach (KeyValuePair<ContainerId, ContainerState> pair in T.Match.Containers)
                    if (Host.IsPublicDiscardPile(pair.Key, out _)) return pair.Key;
                Assert.Fail("Trap Floor has a public discard pile (the Ability discard)");
                return ContainerId.Empty;
            }
        }

        private static bool Hidden(MatchState table, TabletopObjectId id) =>
            table.Cards[id].BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition);

        private static bool Carries(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }

            return false;
        }

        private static PrivateCardRequest Req(TabletopObjectId card, ContainerId to = default) =>
            new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = card, Destination = to };

        [Test]
        public void ReturnAndDiscard_CodecRoundTrips_AndIsStrict()
        {
            Guid id = Guid.NewGuid(), card = Guid.NewGuid(), pile = Guid.NewGuid();
            byte[] ret = PrivateRequestCodec.Card(PrivateRequestCodec.KindReturn, new PrivateCardRequest { RequestId = id, Card = new TabletopObjectId(card) });
            Assert.IsTrue(PrivateRequestCodec.TryRead(ret, 0, out byte kind, out _, out _, out PrivateCardRequest r));
            Assert.AreEqual(PrivateRequestCodec.KindReturn, kind);
            Assert.AreEqual(card, r.Card.Value);
            byte[] dis = PrivateRequestCodec.Card(PrivateRequestCodec.KindDiscard, new PrivateCardRequest { RequestId = id, Card = new TabletopObjectId(card), Destination = new ContainerId(pile) });
            Assert.IsTrue(PrivateRequestCodec.TryRead(dis, 0, out kind, out _, out _, out PrivateCardRequest d));
            Assert.AreEqual(PrivateRequestCodec.KindDiscard, kind);
            Assert.AreEqual(pile, d.Destination.Value);

            byte[] returnWithDestination = (byte[])dis.Clone();
            returnWithDestination[0] = PrivateRequestCodec.KindReturn;
            Assert.IsFalse(PrivateRequestCodec.TryRead(returnWithDestination, 0, out _, out _, out _, out _), "a Return names no destination");
            byte[] discardWithout = (byte[])ret.Clone();
            discardWithout[0] = PrivateRequestCodec.KindDiscard;
            Assert.IsFalse(PrivateRequestCodec.TryRead(discardWithout, 0, out _, out _, out _, out _), "a Discard names its pile");
            byte[] noCard = (byte[])ret.Clone();
            for (int i = 17; i < 33; i++) noCard[i] = 0;
            Assert.IsFalse(PrivateRequestCodec.TryRead(noCard, 0, out _, out _, out _, out _), "no card");
            Assert.IsTrue(PrivateRequestCodec.TryRead(PrivateRequestCodec.Draw(new PrivateDrawRequest { RequestId = id, Deck = new ContainerId(pile) }), 0, out kind, out _, out _, out _));
            Assert.AreEqual(PrivateRequestCodec.KindDraw, kind, "the older kinds still read");
        }

        [Test]
        public void PlaceThenReturn_TheCardIsPrivateAgain_UnderAFreshReferenceForOthers()
        {
            Room r = Room.Open();
            TabletopObjectId card = r.DrawFor(r.A);
            ContainerId slot = r.T.SetupOf(r.A).SideSlotContainerIds[0];
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = card, Destination = slot }, out Dictionary<PlayerId, byte[]> placed));
            r.Deliver(placed);
            foreach (PlayerId p in r.Host.Recipients) Assert.IsTrue(r.Tables[p].Cards.ContainsKey(card), "public on the Console: everyone knows it by its real id");

            ContainerId handA = r.T.SetupOf(r.A).HandContainerId;
            int handBefore = r.T.Match.Containers[handA].Count;
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Return(r.A, Req(card), out Dictionary<PlayerId, byte[]> returned));
            r.Deliver(returned);
            Assert.AreEqual(handA, r.T.Match.Cards[card].BaseState.ContainerId);
            Assert.AreEqual(CardFace.FaceUp, r.T.Match.Cards[card].Face, "the hand's arrival face, as a draw");
            Assert.IsFalse(Hidden(r.Tables[r.A], card), "the owner still sees it");
            foreach (PlayerId other in new[] { r.B, RecipientSnapshotProjection.Spectator })
            {
                MatchState table = r.Tables[other];
                Assert.IsFalse(table.Cards.ContainsKey(card), "others no longer hold the card under its real id");
                Assert.AreEqual(handBefore + 1, table.Containers[handA].Count, "others see the count");
                foreach (TabletopObjectId id in table.Containers[handA].ObjectIds) Assert.IsTrue(Hidden(table, id), "others see backs only");
                Assert.AreEqual(0, table.Containers[slot].Count, "the slot is empty everywhere");
            }

            // The update bytes name no hidden card of B's table by its real id. PRIVACY-P6A: not even this card's: B's
            // update names B's old view by its public place (the slot it left), not by the real id B saw (P5 sent it).
            RecipientSnapshotProjection.Classify(r.T.Match, r.B, out HashSet<TabletopObjectId> hiddenFromB, out _);
            foreach (TabletopObjectId id in hiddenFromB)
                Assert.IsFalse(Carries(returned[r.B], id.Value.ToByteArray()), "no hidden real id in B's update");
            Assert.IsTrue(RecipientEventApplier.Read(returned[r.B]).Event.PreviousReference.IsEmpty, "B's update names the card by its old place only");

            // The old public reference does not reach the card any more for a recipient who may not identify it.
            Assert.AreEqual(PrivateRequestVerdict.HiddenRealId, r.Host.Discard(r.B, Req(card, r.Discard()), out _));
        }

        [Test]
        public void DiscardFromHand_FollowsThePilesFace_AndCountsStayConsistent()
        {
            Room r = Room.Open();
            TabletopObjectId card = r.DrawFor(r.A);
            ContainerId pile = r.Discard();
            ContainerId handA = r.T.SetupOf(r.A).HandContainerId;
            ContainerId deck = r.T.SetupOf(r.A).ControllerDeckId;
            int hand = r.T.Match.Containers[handA].Count, deckCount = r.T.Match.Containers[deck].Count, pileCount = r.T.Match.Containers[pile].Count;
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Discard(r.A, Req(card, pile), out Dictionary<PlayerId, byte[]> discarded));
            r.Deliver(discarded);
            // The pile's own rule decides the face (its arrival face); identity follows the face (doc 11 §12).
            ContainerArrivalFace arrival = r.T.Match.Containers[pile].ArrivalFace;
            bool faceUp = r.T.Match.Cards[card].Face == CardFace.FaceUp;
            TestContext.WriteLine($"Trap Floor public discard pile: arrival face {arrival}; the discarded card lies face {(faceUp ? "up" : "down")}.");
            if (arrival == ContainerArrivalFace.FaceUp) Assert.IsTrue(faceUp);
            if (arrival == ContainerArrivalFace.FaceDown) Assert.IsFalse(faceUp);
            foreach (PlayerId p in r.Host.Recipients)
            {
                MatchState table = r.Tables[p];
                if (faceUp)
                {
                    Assert.IsTrue(table.Cards.ContainsKey(card), "a face-up discard is public: everyone has its real id");
                    Assert.AreEqual(r.T.Match.Cards[card].BaseState.DefinitionId, table.Cards[card].BaseState.DefinitionId);
                }
                else
                {
                    Assert.IsFalse(table.Cards.ContainsKey(card), "a face-down discard is hidden from everyone, its owner too");
                }

                Assert.AreEqual(pileCount + 1, table.Containers[pile].Count);
                Assert.AreEqual(hand - 1, table.Containers[handA].Count);
                Assert.AreEqual(deckCount, table.Containers[deck].Count, "the deck is untouched");
            }
        }

        [Test]
        public void DiscardFromAFaceUpConsoleCard_FollowsThePilesFace()
        {
            Room r = Room.Open();
            TabletopObjectId card = r.DrawFor(r.A);
            ContainerId slot = r.T.SetupOf(r.A).SideSlotContainerIds[0];
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = card, Destination = slot }, out Dictionary<PlayerId, byte[]> placed));
            r.Deliver(placed);
            ContainerId pile = r.Discard();
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Discard(r.A, Req(card, pile), out Dictionary<PlayerId, byte[]> d));
            r.Deliver(d);
            bool faceUp = r.T.Match.Cards[card].Face == CardFace.FaceUp;
            foreach (PlayerId p in r.Host.Recipients)
            {
                Assert.AreEqual(0, r.Tables[p].Containers[slot].Count, "the Console slot is empty everywhere");
                Assert.AreEqual(faceUp, r.Tables[p].Cards.ContainsKey(card), "identity follows the face on the pile");
            }
        }

        [Test]
        public void DrawStillTellsOnlyTheDrawer_AfterOtherLifecycleSteps()
        {
            Room r = Room.Open();
            TabletopObjectId a = r.DrawFor(r.A);
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Discard(r.A, Req(a, r.Discard()), out Dictionary<PlayerId, byte[]> d));
            r.Deliver(d);
            TabletopObjectId b = r.DrawFor(r.B);
            Assert.IsFalse(Hidden(r.Tables[r.B], b));
            Assert.IsFalse(r.Tables[r.A].Cards.ContainsKey(b), "A never gets B's card id");
            Assert.IsFalse(r.Tables[RecipientSnapshotProjection.Spectator].Cards.ContainsKey(b));
        }

        [Test]
        public void ForgedBorrowedStaleCrossSeatAndDuplicate_AreRefused_AndChangeNothing()
        {
            Room r = Room.Open();
            TabletopObjectId mine = r.DrawFor(r.A);
            TabletopObjectId theirs = r.DrawFor(r.B);
            ContainerId pile = r.Discard();
            ContainerId slotA = r.T.SetupOf(r.A).SideSlotContainerIds[0];
            ContainerId slotB = r.T.SetupOf(r.B).SideSlotContainerIds[0];
            string before = RecipientTableDigest.Of(r.T.Match);

            Assert.AreEqual(PrivateRequestVerdict.UnknownReference, r.Host.Discard(r.A, Req(new TabletopObjectId(Guid.NewGuid()), pile), out _), "forged");
            ContainerState bHandOnA = r.Tables[r.A].Containers[r.T.SetupOf(r.B).HandContainerId];
            TabletopObjectId borrowedHandle = bHandOnA.GetObjectAt(bHandOnA.Count - 1);
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, r.Host.Discard(r.A, Req(borrowedHandle, pile), out _), "A's handle for a card in B's hand");
            Assert.AreEqual(PrivateRequestVerdict.HiddenRealId, r.Host.Discard(r.A, Req(theirs, pile), out _), "B's real id, which A may not know");
            Assert.AreNotEqual(PrivateRequestVerdict.Accepted, r.Host.Discard(r.B, Req(mine, pile), out _), "B with A's real id");
            Assert.AreEqual(PrivateRequestVerdict.InvalidDestination, r.Host.Discard(r.A, Req(mine, slotA), out _), "a Console slot is not a discard pile");
            Assert.AreEqual(PrivateRequestVerdict.InvalidDestination, r.Host.Discard(r.A, Req(mine, r.T.SetupOf(r.A).HandContainerId), out _), "a hand is not a discard pile");
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, r.Host.Return(r.A, Req(mine), out _), "Return takes a Console card, not a hand card");
            Assert.AreEqual(PrivateRequestVerdict.InvalidDestination, r.Host.Return(r.A, Req(mine, r.T.SetupOf(r.B).HandContainerId), out _), "not into another seat's hand");
            Assert.AreEqual(before, RecipientTableDigest.Of(r.T.Match), "no refused request changed the host's table");

            // B's card on B's Console: A cannot take it back to any hand, nor discard it.
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Place(r.B, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = theirs, Destination = slotB }, out Dictionary<PlayerId, byte[]> p));
            r.Deliver(p);
            before = RecipientTableDigest.Of(r.T.Match);
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, r.Host.Return(r.A, Req(theirs), out _), "cross-seat Return");
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, r.Host.Discard(r.A, Req(theirs, pile), out _), "cross-seat Discard");

            // Duplicate: the same request id runs once.
            PrivateCardRequest once = Req(mine, pile);
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Discard(r.A, once, out Dictionary<PlayerId, byte[]> d));
            r.Deliver(d);
            string afterOnce = RecipientTableDigest.Of(r.T.Match);
            Assert.AreEqual(PrivateRequestVerdict.Duplicate, r.Host.Discard(r.A, once, out _));
            Assert.AreEqual(afterOnce, RecipientTableDigest.Of(r.T.Match));
            Assert.AreNotEqual(PrivateRequestVerdict.Accepted, r.Host.Discard(r.A, Req(mine, pile), out _), "stale: that card is already on the pile");
            Assert.AreEqual(afterOnce, RecipientTableDigest.Of(r.T.Match));
        }

        [Test]
        public void FullModePolicy_TurnAndHandLimit_FreeModeAllowsAnyTime()
        {
            Room r = Room.Open();
            r.S.AnyTime = false; // Full
            Assert.AreEqual(PrivateRequestVerdict.NotYourTurn,
                r.Host.Draw(r.B, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = r.T.SetupOf(r.B).ControllerDeckId }, out _), "Full: not B's turn");
            int handA = r.T.Match.Containers[r.T.SetupOf(r.A).HandContainerId].Count;
            r.S.Limit = handA; // the hand already holds its maximum
            Assert.AreEqual(PrivateRequestVerdict.RuleRefused,
                r.Host.Draw(r.A, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = r.T.SetupOf(r.A).ControllerDeckId }, out _), "Full: the hand limit");
            r.S.Limit = handA + 1;
            r.DrawFor(r.A);

            r.S.AnyTime = true; // Free / Reminders
            r.S.Limit = -1;
            r.DrawFor(r.B); // out of turn, on a free table
        }

        [Test]
        public void Rejoin_AfterTheLifecycle_GivesTheSameTableTheUpdatesBuilt()
        {
            Room r = Room.Open();
            TabletopObjectId a = r.DrawFor(r.A);
            TabletopObjectId a2 = r.DrawFor(r.A);
            r.DrawFor(r.B);
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = a, Destination = r.T.SetupOf(r.A).SideSlotContainerIds[0] }, out Dictionary<PlayerId, byte[]> u1));
            r.Deliver(u1);
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Discard(r.A, Req(a2, r.Discard()), out Dictionary<PlayerId, byte[]> u2));
            r.Deliver(u2);
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Return(r.A, Req(a), out Dictionary<PlayerId, byte[]> u3));
            r.Deliver(u3);

            foreach (PlayerId p in new List<PlayerId>(r.Tables.Keys))
            {
                string kept = PrivateClientModel.FromTable(r.Tables[p]).Digest();
                r.Rejoin(p);
                Assert.AreEqual(kept, PrivateClientModel.FromTable(r.Tables[p]).Digest(), "a fresh package matches what the updates built (places, faces, counts, identities)");
            }
        }
    }
}
