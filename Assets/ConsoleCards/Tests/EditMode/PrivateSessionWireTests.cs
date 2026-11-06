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
    /// PRIVACY-P4 (s148): the pieces the networked private development session uses, checked without the network:
    /// the request codec (plain bytes from the guest), and the guest applying the host's updates to the table its
    /// scene is built from (each recipient's table, digest-checked against the host).
    /// </summary>
    public sealed class PrivateSessionWireTests
    {
        private static readonly byte[] Key = { 7, 7, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };
        private const int Seed = 0x5EED7777;

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

        private static RecipientPublicLayout LayoutOf(TrapFloorTestTable t)
        {
            RecipientPublicLayout layout = new RecipientPublicLayout();
            foreach (TrapFloorPlayerSetupDefinition player in t.Template.Players)
                layout.Seats.Add(new RecipientSeatLayout
                {
                    LayoutSeatIndex = player.LayoutSeatIndex, Seat = player.SeatId, Hand = player.HandContainerId,
                    ControllerDeck = player.ControllerDeckId, ActionArea = player.ActionAbilityAreaContainerId, Pawn = player.PawnId,
                });
            return layout;
        }

        private sealed class Room
        {
            public TrapFloorTestTable T;
            public PrivateTableHost Host;
            public PlayerId A, B;
            public readonly Dictionary<PlayerId, MatchState> Tables = new Dictionary<PlayerId, MatchState>();

            public static Room Open()
            {
                Room r = new Room { T = TrapFloorTestTable.Create(3, Seed) };
                r.Host = new PrivateTableHost(r.T.Match, new Seats { T = r.T }, r.T.Template.CardLabels, Key) { Layout = LayoutOf(r.T) };
                r.A = r.T.Turn.ActivePlayerId;
                r.B = r.T.Players[Array.IndexOf(r.T.Players, r.A) == 0 ? 1 : 0];
                foreach (PlayerId p in r.Host.Recipients)
                {
                    byte[] package = r.Host.Join(p);
                    RecipientBootstrapPackage received = (RecipientBootstrapPackage)TableWire.Read(package, 0, package.Length, false);
                    Assert.IsNotNull(received.Layout, "the package carries the public table template");
                    r.Tables[p] = RecipientClientTable.Build(received).Table;
                }

                return r;
            }

            public void Deliver(Dictionary<PlayerId, byte[]> updates)
            {
                foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
                    Assert.IsNull(RecipientEventApplier.Apply(Tables[pair.Key], RecipientEventApplier.Read(pair.Value)), "update applies with a matching digest");
            }

            public ContainerState Hand(PlayerId owner, PlayerId on) => Tables[on].Containers[T.SetupOf(owner).HandContainerId];
        }

        private static bool Hidden(MatchState table, TabletopObjectId id) =>
            table.Cards[id].BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition);

        [Test]
        public void RequestCodec_RoundTrips_AndRefusesAnythingElse()
        {
            Guid id = Guid.NewGuid(), deck = Guid.NewGuid(), card = Guid.NewGuid(), slot = Guid.NewGuid();
            byte[] draw = PrivateRequestCodec.Draw(new PrivateDrawRequest { RequestId = id, Deck = new ContainerId(deck) });
            byte[] message = new byte[draw.Length + 1];
            draw.CopyTo(message, 1);
            Assert.IsTrue(PrivateRequestCodec.TryRead(message, 1, out byte kind, out PrivateDrawRequest d, out _));
            Assert.AreEqual(PrivateRequestCodec.KindDraw, kind);
            Assert.AreEqual(id, d.RequestId);
            Assert.AreEqual(deck, d.Deck.Value);

            byte[] place = PrivateRequestCodec.Place(new PrivatePlaceRequest { RequestId = id, Card = new TabletopObjectId(card), Destination = new ContainerId(slot) });
            Assert.IsTrue(PrivateRequestCodec.TryRead(place, 0, out kind, out _, out PrivatePlaceRequest p));
            Assert.AreEqual(PrivateRequestCodec.KindPlace, kind);
            Assert.AreEqual(card, p.Card.Value);
            Assert.AreEqual(slot, p.Destination.Value);

            Assert.IsFalse(PrivateRequestCodec.TryRead(new byte[PrivateRequestCodec.Size + 1], 0, out _, out _, out _), "wrong length");
            byte[] noId = (byte[])place.Clone();
            for (int i = 1; i < 17; i++) noId[i] = 0;
            Assert.IsFalse(PrivateRequestCodec.TryRead(noId, 0, out _, out _, out _), "no request id");
            byte[] unknown = (byte[])place.Clone();
            unknown[0] = 9;
            Assert.IsFalse(PrivateRequestCodec.TryRead(unknown, 0, out _, out _, out _), "unknown kind");
            byte[] drawWithExtra = (byte[])place.Clone();
            drawWithExtra[0] = PrivateRequestCodec.KindDraw;
            Assert.IsFalse(PrivateRequestCodec.TryRead(drawWithExtra, 0, out _, out _, out _), "a draw names no destination");
        }

        [Test]
        public void DrawThenPlace_EachGuestTableFollowsItsOwnUpdates()
        {
            Room r = Room.Open();
            int handA = r.Hand(r.A, r.A).Count;
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Draw(r.A, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = r.T.SetupOf(r.A).ControllerDeckId }, out Dictionary<PlayerId, byte[]> drawn));
            r.Deliver(drawn);

            // A sees the drawn card's identity; B and the spectator see one more back in A's hand.
            ContainerState mine = r.Hand(r.A, r.A);
            Assert.AreEqual(handA + 1, mine.Count);
            TabletopObjectId newest = mine.GetObjectAt(mine.Count - 1);
            Assert.IsFalse(Hidden(r.Tables[r.A], newest), "the drawer sees its card");
            Assert.AreEqual(CardFace.FaceUp, r.T.Match.Cards[newest].Face, "a card arriving in a hand turns face up (the hand's arrival face)");
            Assert.AreEqual(CardFace.FaceUp, r.Tables[r.A].Cards[newest].Face);
            Assert.IsTrue(r.T.Match.Cards.ContainsKey(newest), "the drawer's reference is the real card");
            foreach (PlayerId other in new[] { r.B, RecipientSnapshotProjection.Spectator })
            {
                ContainerState theirs = r.Hand(r.A, other);
                Assert.AreEqual(handA + 1, theirs.Count, "others see the count");
                foreach (TabletopObjectId id in theirs.ObjectIds) Assert.IsTrue(Hidden(r.Tables[other], id), "others see backs only");
                Assert.IsFalse(r.Tables[other].Cards.ContainsKey(newest), "others never get the real id");
            }

            ContainerId slot = r.T.SetupOf(r.A).SideSlotContainerIds[0];
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = newest, Destination = slot }, out Dictionary<PlayerId, byte[]> placed));
            r.Deliver(placed);
            foreach (PlayerId p in r.Host.Recipients)
            {
                MatchState table = r.Tables[p];
                Assert.IsTrue(table.Cards.ContainsKey(newest), "the revealed card is public: everyone has its real id");
                Assert.IsFalse(Hidden(table, newest));
                Assert.AreEqual(slot, table.Cards[newest].BaseState.ContainerId);
                Assert.AreEqual(CardFace.FaceUp, table.Cards[newest].Face);
                Assert.AreEqual(r.T.Match.Cards[newest].BaseState.DefinitionId, table.Cards[newest].BaseState.DefinitionId, "the same identity for everyone");
            }
        }

        [Test]
        public void RefusedRequests_LeaveTheHostTableAsItWas()
        {
            Room r = Room.Open();
            ContainerId deck = r.T.SetupOf(r.A).ControllerDeckId;
            Guid repeated = Guid.NewGuid();
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Draw(r.A, new PrivateDrawRequest { RequestId = repeated, Deck = deck }, out Dictionary<PlayerId, byte[]> drawn));
            r.Deliver(drawn);
            string before = RecipientTableDigest.Of(r.T.Match);
            ContainerState mine = r.Hand(r.A, r.A);
            TabletopObjectId card = mine.GetObjectAt(mine.Count - 1);

            Assert.AreEqual(PrivateRequestVerdict.Duplicate, r.Host.Draw(r.A, new PrivateDrawRequest { RequestId = repeated, Deck = deck }, out _));
            Assert.AreEqual(PrivateRequestVerdict.UnknownReference,
                r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = new TabletopObjectId(Guid.NewGuid()), Destination = r.T.SetupOf(r.A).SideSlotContainerIds[0] }, out _));
            Assert.AreEqual(PrivateRequestVerdict.InvalidDestination,
                r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = card, Destination = r.T.SetupOf(r.B).SideSlotContainerIds[0] }, out _));
            ContainerState handOnB = r.Hand(r.A, r.B);
            TabletopObjectId handleOnB = handOnB.GetObjectAt(0);
            Assert.AreNotEqual(PrivateRequestVerdict.Accepted,
                r.Host.Place(r.B, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = handleOnB, Destination = r.T.SetupOf(r.B).SideSlotContainerIds[0] }, out _),
                "B cannot play a card of A's hand by B's handle for it");
            Assert.AreEqual(before, RecipientTableDigest.Of(r.T.Match), "no refused request changed the host's table");
        }

        [Test]
        public void TamperedOrStaleUpdate_IsRefused_SoTheGuestAsksForItsPackage()
        {
            Room r = Room.Open();
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Draw(r.A, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = r.T.SetupOf(r.A).ControllerDeckId }, out Dictionary<PlayerId, byte[]> drawn));
            PrivateUpdate update = RecipientEventApplier.Read(drawn[r.B]);
            update.Digest = "not the host's digest";
            Assert.IsNotNull(RecipientEventApplier.Apply(r.Tables[r.B], update), "a digest mismatch is reported");

            PrivateUpdate again = RecipientEventApplier.Read(drawn[r.A]);
            Assert.IsNull(RecipientEventApplier.Apply(r.Tables[r.A], again));
            Assert.IsNotNull(RecipientEventApplier.Apply(r.Tables[r.A], RecipientEventApplier.Read(drawn[r.A])), "the same update twice names a card that has moved on");
        }
    }
}
