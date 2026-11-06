using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Privacy;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// PRIVACY-P3 (s143): the first private action flow on the real Trap Floor table, through TableWire bytes, with one
    /// host and three receiving clients (A, B and a spectator) in one process. Isolated integration test: no scene, no network.
    /// Also COLLAPSE-ONCE (the double-collapse regression).
    /// </summary>
    public sealed class PrivateDrawFlowTests
    {
        private static readonly byte[] Key = { 3, 1, 4, 1, 5, 9, 2, 6, 5, 3, 5, 8, 9, 7, 9, 3, 2, 3, 8, 4 };
        private const int Seed = 0x5EED4321;

        private sealed class Seats : IPrivateSeats
        {
            public TrapFloorTestTable T;
            public bool IsSeated(PlayerId player) => Array.IndexOf(T.Players, player) >= 0;
            public bool IsTurnOf(PlayerId player) => T.Turn.ActivePlayerId == player;
            public ContainerId DeckOf(PlayerId player) => T.SetupOf(player).ControllerDeckId;
            public ContainerId HandOf(PlayerId player) => T.SetupOf(player).HandContainerId;
            public bool IsOwnConsoleSlot(PlayerId player, ContainerId slot)
            {
                if (!IsSeated(player)) return false;
                foreach (ContainerId own in T.SetupOf(player).SideSlotContainerIds) if (own == slot) return true;
                return false;
            }
            public IReadOnlyList<PlayerId> Seated => T.Players;
        }

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

        private static bool Carries(byte[] haystack, Guid value) => Carries(haystack, value.ToByteArray());

        private sealed class Room
        {
            public TrapFloorTestTable T;
            public Seats S;
            public PrivateTableHost Host;
            public PlayerId A, B;
            public readonly Dictionary<PlayerId, PrivateClientModel> Clients = new Dictionary<PlayerId, PrivateClientModel>();
            public readonly List<byte[]> Sent = new List<byte[]>();

            public static Room Open()
            {
                Room r = new Room { T = TrapFloorTestTable.Create(3, Seed) };
                r.S = new Seats { T = r.T };
                r.Host = new PrivateTableHost(r.T.Match, r.S, r.T.Template.CardLabels, Key);
                r.A = r.T.Turn.ActivePlayerId;
                r.B = r.T.Players[Array.IndexOf(r.T.Players, r.A) == 0 ? 1 : 0];
                foreach (PlayerId p in r.Host.Recipients)
                {
                    byte[] package = r.Host.Join(p);
                    r.Sent.Add(package);
                    r.Clients[p] = PrivateClientModel.FromPackage(package);
                }

                return r;
            }

            public void Deliver(Dictionary<PlayerId, byte[]> updates)
            {
                foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
                {
                    Sent.Add(pair.Value);
                    Assert.IsTrue(Clients[pair.Key].Apply(pair.Value), "the client's table matches the host's check after the event (no correction)");
                }
            }
        }

        [Test]
        public void Draw_ThenPlaceFaceUp_EachClientLearnsOnlyWhatItMay()
        {
            Room room = Room.Open();
            ContainerId deck = room.S.DeckOf(room.A), hand = room.S.HandOf(room.A);
            TabletopObjectId top = room.T.Match.Containers[deck].GetObjectAt(room.T.Match.Containers[deck].TopIndex);
            ObjectDefinitionId identity = room.T.Match.Cards[top].BaseState.DefinitionId;

            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                room.Host.Draw(room.A, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = deck }, out Dictionary<PlayerId, byte[]> drawn));
            room.Deliver(drawn);

            Assert.IsTrue(room.Clients[room.A].Cards.TryGetValue(top, out ClientCardView mine), "A holds the card under its real id");
            Assert.AreEqual(identity, mine.Definition);
            Assert.AreEqual(hand, mine.Container);
            foreach (PlayerId other in new[] { room.B, RecipientSnapshotProjection.Spectator })
            {
                Assert.IsFalse(Carries(drawn[other], top.Value), "real id in another recipient's draw event");
                Assert.IsFalse(Carries(drawn[other], identity.Value), "identity in another recipient's draw event");
                Assert.IsFalse(room.Clients[other].Cards.ContainsKey(top));
                Assert.AreEqual(1, room.Clients[other].Counts[hand], "A's hand count is public");
                int backs = 0;
                foreach (ClientCardView c in room.Clients[other].Cards.Values) if (c.Container == hand && c.IsHidden) backs++;
                Assert.AreEqual(1, backs, "one back in A's hand");
            }

            Assert.AreEqual(room.T.Match.Containers[deck].Count, room.Clients[room.B].Counts[deck], "deck count is public");

            ContainerId slot = room.T.SetupOf(room.A).SideSlotContainerIds[0];
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                room.Host.Place(room.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = top, Destination = slot }, out Dictionary<PlayerId, byte[]> placed));
            room.Deliver(placed);
            foreach (PlayerId everyone in room.Host.Recipients)
            {
                Assert.IsTrue(room.Clients[everyone].Cards.TryGetValue(top, out ClientCardView seen), "the placed card is public now");
                Assert.AreEqual(identity, seen.Definition);
                Assert.AreEqual(slot, seen.Container);
                Assert.IsTrue(Carries(placed[everyone], identity.Value), "the reveal carries the identity");
            }

            foreach (byte[] message in room.Sent) Assert.IsFalse(Carries(message, BitConverter.GetBytes(Seed)), "no deal seed in any message");

            // Client memory, not only the wire: everything B's client holds after join + two events.
            RecipientSnapshotProjection.Classify(room.T.Match, room.B, out HashSet<TabletopObjectId> hiddenFromB, out _);
            byte[] memoryOfB = ConsoleCards.Application.Authority.TableWire.Write(new List<ClientCardView>(room.Clients[room.B].Cards.Values), false);
            HashSet<Guid> knownToB = new HashSet<Guid>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in room.T.Match.Cards)
                if (!hiddenFromB.Contains(pair.Key)) knownToB.Add(pair.Value.BaseState.DefinitionId.Value);
            foreach (TabletopObjectId h in hiddenFromB)
            {
                Assert.IsFalse(Carries(memoryOfB, h.Value), "B's client holds a hidden card's real id");
                Guid def = room.T.Match.Cards[h].BaseState.DefinitionId.Value;
                if (!knownToB.Contains(def)) Assert.IsFalse(Carries(memoryOfB, def), "B's client holds a hidden-only identity");
            }
        }

        [Test]
        public void Requests_AreValidatedOnTheHost()
        {
            Room room = Room.Open();
            ContainerId deckA = room.S.DeckOf(room.A), deckB = room.S.DeckOf(room.B);
            Guid once = Guid.NewGuid();

            Assert.AreEqual(PrivateRequestVerdict.NotSeated, room.Host.Draw(PlayerId.Empty, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = deckA }, out _));
            Assert.AreEqual(PrivateRequestVerdict.NotYourTurn, room.Host.Draw(room.B, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = deckB }, out _));
            Assert.AreEqual(PrivateRequestVerdict.WrongSeat, room.Host.Draw(room.A, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = room.S.HandOf(room.B) }, out _), "not the sender's draw deck (Trap Floor seats share one Controller deck)");
            Assert.AreEqual(PrivateRequestVerdict.Accepted, room.Host.Draw(room.A, new PrivateDrawRequest { RequestId = once, Deck = deckA }, out Dictionary<PlayerId, byte[]> u));
            room.Deliver(u);
            Assert.AreEqual(PrivateRequestVerdict.Duplicate, room.Host.Draw(room.A, new PrivateDrawRequest { RequestId = once, Deck = deckA }, out _), "a repeat runs once");
            Assert.AreEqual(1, room.T.Match.Containers[room.S.HandOf(room.A)].Count);

            ContainerId slotA = room.T.SetupOf(room.A).SideSlotContainerIds[0], slotB = room.T.SetupOf(room.B).SideSlotContainerIds[0];
            TabletopObjectId inHand = room.T.Match.Containers[room.S.HandOf(room.A)].GetObjectAt(0);
            Assert.AreEqual(PrivateRequestVerdict.UnknownReference,
                room.Host.Place(room.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = new TabletopObjectId(Guid.NewGuid()), Destination = slotA }, out _), "forged");
            TabletopObjectId bsBack = TabletopObjectId.Empty;
            foreach (ClientCardView c in room.Clients[room.B].Cards.Values) if (c.IsHidden && c.Container == room.S.HandOf(room.A)) bsBack = c.Reference;
            Assert.AreEqual(PrivateRequestVerdict.UnknownReference,
                room.Host.Place(room.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = bsBack, Destination = slotA }, out _), "B's reference used by A");
            TabletopObjectId hiddenDeckCard = room.T.Match.Containers[deckA].GetObjectAt(0);
            Assert.AreEqual(PrivateRequestVerdict.HiddenRealId,
                room.Host.Place(room.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = hiddenDeckCard, Destination = slotA }, out _), "a hidden card's real id");
            Assert.AreEqual(PrivateRequestVerdict.InvalidDestination,
                room.Host.Place(room.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = inHand, Destination = slotB }, out _), "another player's Console");

            TabletopObjectId tile = TabletopObjectId.Empty;
            foreach (TabletopObjectId id in room.T.Template.FloorCardIds.Values) if (room.T.Match.Cards[id].Face == CardFace.FaceDown) { tile = id; break; }
            TabletopObjectId staleHandle = TabletopObjectId.Empty;
            foreach (ClientCardView c in room.Clients[room.A].Cards.Values) if (c.IsHidden && c.Container.IsEmpty) { staleHandle = c.Reference; break; }
            foreach (TabletopObjectId id in room.T.Template.FloorCardIds.Values) room.T.Match.Cards[id].SetFace(CardFace.FaceUp); // every tile turned
            Assert.AreEqual(PrivateRequestVerdict.UnknownReference,
                room.Host.Place(room.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = staleHandle, Destination = slotA }, out _), "a stale handle");
            Assert.AreEqual(1, room.T.Match.Containers[room.S.HandOf(room.A)].Count, "no refused request changed the table");
        }

        [Test]
        public void Reconnect_GivesTheSameAuthorizedView_AndNothingMore()
        {
            Room room = Room.Open();
            ContainerId deck = room.S.DeckOf(room.A);
            room.Host.Draw(room.A, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = deck }, out Dictionary<PlayerId, byte[]> u);
            room.Deliver(u);
            foreach (PlayerId p in room.Host.Recipients)
            {
                PrivateClientModel rejoined = PrivateClientModel.FromPackage(room.Host.Join(p));
                Assert.AreEqual(room.Clients[p].Digest(), rejoined.Digest(), "rejoin rebuilds exactly the view the events built");
                Assert.AreEqual(0, room.Clients[p].Corrections);
            }
        }

        // ---------- COLLAPSE-ONCE ----------

        [Test]
        public void OneCollapsePerFloorTurn_SurvivesAnUndoOfEndFloorTurn()
        {
            TrapFloorFloorTurnLedger ledger = new TrapFloorFloorTurnLedger();
            const long roundOneFloorTurn = 1, roundTwoFloorTurn = 2;
            Assert.IsTrue(ledger.MayCollapse(roundOneFloorTurn, 0), "the floor turn has not collapsed yet");
            ledger.RecordCollapse(roundOneFloorTurn, 1); // tile (4,6) became a hole
            Assert.IsFalse(ledger.MayCollapse(roundOneFloorTurn, 1), "after Undo of End Floor Turn the same floor turn holds its hole: no second collapse");
            Assert.IsTrue(ledger.MayCollapse(roundTwoFloorTurn, 1), "the next floor turn collapses as usual");
            Assert.IsTrue(ledger.MayCollapse(roundOneFloorTurn, 0), "if the collapse itself is taken back (where allowed), the turn may collapse again");
            ledger.Clear();
            Assert.IsTrue(ledger.MayCollapse(roundOneFloorTurn, 1), "a new floor starts clean");
        }
    }
}
