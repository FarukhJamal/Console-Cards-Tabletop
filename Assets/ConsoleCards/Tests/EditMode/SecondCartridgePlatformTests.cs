using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.GameTemplates.Privacy;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P7 (s157), platform evidence: a second, deliberately simple cartridge ("Race to the Line": a shared face-down
    /// deck, a hand and a one-slot Console per seat, a public discard pile, one pawn per seat, strict turns) runs on the
    /// shared private-session platform: the generic host authority, recipient projection, handles, change sets, the
    /// operation policy and the client applier. It brings only its own table and its own seat adapter (about twenty
    /// lines); no Trap Floor type is referenced and no Trap Floor networking code is copied. In-process (unit) only.
    /// </summary>
    public sealed class SecondCartridgePlatformTests
    {
        private static readonly byte[] Key = { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 11, 12, 13, 14, 15, 16 };
        private const int DeckSize = 6;

        // The cartridge's own seat adapter: the only piece of the platform a cartridge must write.
        private sealed class RaceSeats : IPrivateSeats
        {
            public RaceTable T;
            public bool IsSeated(PlayerId player) => Array.IndexOf(T.Players, player) >= 0;
            public bool IsTurnOf(PlayerId player) => IsSeated(player) && T.Players[T.TurnIndex] == player;
            public int DrawLimit(PlayerId player) => 3;
            public ContainerId DeckOf(PlayerId player) => T.Deck;
            public ContainerId HandOf(PlayerId player) => IsSeated(player) ? T.Hands[Array.IndexOf(T.Players, player)] : ContainerId.Empty;
            public bool IsOwnConsoleSlot(PlayerId player, ContainerId slot) => IsSeated(player) && T.Slots[Array.IndexOf(T.Players, player)] == slot;
            public IReadOnlyList<PlayerId> Seated => T.Players;
        }

        // The cartridge's own table, built directly from the platform's domain types (no Trap Floor template).
        private sealed class RaceTable
        {
            public MatchState Match;
            public PlayerId[] Players;
            public SeatId[] SeatIds;
            public ContainerId[] Hands, Slots;
            public ContainerId Deck, Discard;
            public TabletopObjectId[] Pawns;
            public readonly Dictionary<TabletopObjectId, string> Labels = new Dictionary<TabletopObjectId, string>();
            public int TurnIndex;

            private static Guid G(int a, int b) => new Guid(a, (short)b, 0x5eed, new byte[] { 7, 7, 7, 7, 7, 7, 7, (byte)b });

            public static RaceTable Create()
            {
                RaceTable t = new RaceTable();
                t.Players = new[] { new PlayerId(G(1, 1)), new PlayerId(G(1, 2)) };
                t.SeatIds = new[] { new SeatId(G(2, 1)), new SeatId(G(2, 2)) };
                t.Hands = new[] { new ContainerId(G(3, 1)), new ContainerId(G(3, 2)) };
                t.Slots = new[] { new ContainerId(G(4, 1)), new ContainerId(G(4, 2)) };
                t.Deck = new ContainerId(G(5, 1));
                t.Discard = new ContainerId(G(5, 2));
                List<ContainerState> containers = new List<ContainerState>
                {
                    new ContainerState(t.Deck, ContainerKind.Deck, SeatId.Empty, ObjectVisibility.Public, 0),
                    new ContainerState(t.Discard, ContainerKind.DiscardPile, SeatId.Empty, ObjectVisibility.Public, 0, ContainerArrivalFace.FaceUp),
                };
                List<SeatState> seats = new List<SeatState>();
                for (int i = 0; i < 2; i++)
                {
                    containers.Add(new ContainerState(t.Hands[i], ContainerKind.Hand, t.SeatIds[i], ObjectVisibility.Public, 0, ContainerArrivalFace.FaceUp));
                    containers.Add(new ContainerState(t.Slots[i], ContainerKind.ConsoleSlot, t.SeatIds[i], ObjectVisibility.Public, 1));
                    seats.Add(new SeatState(t.SeatIds[i], new TabletopPose(new TableCoordinate(i * 10d, 0d), 0, 0, 0), t.Hands[i],
                        new ConsoleState(t.SeatIds[i], new[] { t.Slots[i] }), t.Players[i], SeatStatus.Occupied));
                }

                ObjectDefinitionId pawnDefinition = new ObjectDefinitionId(G(6, 99));
                List<CardInstanceState> cards = new List<CardInstanceState>();
                ContainerState deck = containers[0];
                ContainerTransferService place = new ContainerTransferService();
                for (int i = 0; i < DeckSize; i++)
                {
                    TabletopObjectId id = new TabletopObjectId(G(7, i + 1));
                    TabletopObjectState state = new TabletopObjectState(id, new ObjectDefinitionId(G(6, i + 1)), TabletopObjectKind.Card,
                        new TabletopPose(new TableCoordinate(0d, 5d), 0, 0, 0), ContainerId.Empty, PlayerId.Empty, ObjectVisibility.Public, false);
                    Assert.IsTrue(place.PlaceIntoContainer(state, deck).Succeeded);
                    cards.Add(new CardInstanceState(state, CardFace.FaceDown));
                    t.Labels[id] = "race-secret-" + i;
                }

                t.Pawns = new TabletopObjectId[2];
                List<PawnState> pawns = new List<PawnState>();
                for (int i = 0; i < 2; i++)
                {
                    t.Pawns[i] = new TabletopObjectId(G(8, i + 1));
                    pawns.Add(new PawnState(new TabletopObjectState(t.Pawns[i], pawnDefinition, TabletopObjectKind.Pawn,
                        new TabletopPose(new TableCoordinate(i, 0d), 0, 0, 0), ContainerId.Empty, t.Players[i], ObjectVisibility.Public, false)));
                }

                t.Match = new MatchState(new MatchId(G(9, 1)), default, 0, cards, pawns, Array.Empty<TokenState>(), containers, seats);
                return t;
            }
        }

        private sealed class Room
        {
            public RaceTable T;
            public RaceSeats S;
            public PrivateTableHost Host;
            public PlayerId A, B;
            public readonly Dictionary<PlayerId, MatchState> Tables = new Dictionary<PlayerId, MatchState>();
            public readonly Dictionary<PlayerId, long> Shown = new Dictionary<PlayerId, long>();
            public readonly List<byte[]> Sent = new List<byte[]>();

            public static Room Open()
            {
                Room r = new Room { T = RaceTable.Create() };
                r.S = new RaceSeats { T = r.T };
                r.Host = new PrivateTableHost(r.T.Match, r.S, r.T.Labels, Key);
                r.A = r.T.Players[0];
                r.B = r.T.Players[1];
                foreach (PlayerId p in r.Host.Recipients)
                {
                    byte[] bytes = r.Host.Join(p);
                    r.Sent.Add(bytes);
                    RecipientBootstrapPackage package = (RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false);
                    r.Tables[p] = RecipientClientTable.Build(package).Table;
                    r.Shown[p] = package.Revision;
                }

                return r;
            }

            public void Deliver(Dictionary<PlayerId, byte[]> updates)
            {
                Assert.IsNotNull(updates, "the change was published");
                foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
                {
                    Sent.Add(pair.Value);
                    PrivateUpdate update = RecipientEventApplier.Read(pair.Value);
                    Assert.AreEqual(PrivateRevisionStep.Apply, PrivateRevisionGate.Next(Shown[pair.Key], update.Revision));
                    Assert.IsNull(RecipientEventApplier.Apply(Tables[pair.Key], update, new PrivateChangeSummary()), "applies with a matching digest");
                    Shown[pair.Key] = update.Revision;
                }
            }

            public void AssertEveryoneMatchesTheHost()
            {
                foreach (PlayerId p in Host.Recipients)
                {
                    byte[] bytes = Host.Join(p);
                    MatchState fresh = RecipientClientTable.Build((RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false)).Table;
                    Assert.AreEqual(PrivateClientModel.FromTable(fresh).Digest(), PrivateClientModel.FromTable(Tables[p]).Digest(), $"recipient {p} matches the host");
                }
            }
        }

        [Test]
        public void SecondCartridge_DealsDrawsPlacesDiscardsAndMovesPawns_OnTheSharedPrivatePlatform()
        {
            Room r = Room.Open();
            int handA = 0, handB = 1;

            // Initial deal: the deck's identities and order go to nobody.
            foreach (PlayerId p in r.Host.Recipients)
                foreach (CardInstanceState card in r.Tables[p].Cards.Values)
                    Assert.IsTrue(card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition), "a face-down deck card is hidden from every seat");

            // A draws (its turn): A learns the card, B sees only a back.
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Draw(r.A, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = r.T.Deck }, out Dictionary<PlayerId, byte[]> drew));
            r.Deliver(drew);
            TabletopObjectId drawn = r.T.Match.Containers[r.T.Hands[handA]].GetObjectAt(0);
            Assert.AreEqual(1, r.Tables[r.A].Containers[r.T.Hands[handA]].Count);
            Assert.AreEqual(1, r.Tables[r.B].Containers[r.T.Hands[handA]].Count);
            TabletopObjectId refA = r.Tables[r.A].Containers[r.T.Hands[handA]].GetObjectAt(0);
            TabletopObjectId refB = r.Tables[r.B].Containers[r.T.Hands[handA]].GetObjectAt(0);
            Assert.IsFalse(r.Tables[r.A].Cards[refA].BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition), "the drawer knows its card");
            Assert.IsTrue(r.Tables[r.B].Cards[refB].BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition), "the other seat does not");
            Assert.AreNotEqual(drawn, refB, "the other seat holds a handle, never the real id");

            // Turn and seat rules (the cartridge's adapter): B may not draw on A's turn; B may not use A's slot.
            Assert.AreEqual(PrivateRequestVerdict.NotYourTurn, r.Host.Draw(r.B, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = r.T.Deck }, out _));
            Assert.AreEqual(PrivateRequestVerdict.InvalidDestination,
                r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = refA, Destination = r.T.Slots[handB] }, out _), "not into another seat's Console");

            // A places it face up into its own Console slot: now public to everyone.
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = refA, Destination = r.T.Slots[handA] }, out Dictionary<PlayerId, byte[]> placed));
            r.Deliver(placed);
            foreach (PlayerId p in r.Host.Recipients)
            {
                Assert.AreEqual(1, r.Tables[p].Containers[r.T.Slots[handA]].Count, $"{p}: the card is in A's Console");
                TabletopObjectId seen = r.Tables[p].Containers[r.T.Slots[handA]].GetObjectAt(0);
                Assert.IsFalse(r.Tables[p].Cards[seen].BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition), $"{p}: face up is public");
            }

            // A discards it onto the public pile through the generic Discard request.
            TabletopObjectId inSlot = r.Tables[r.A].Containers[r.T.Slots[handA]].GetObjectAt(0);
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Discard(r.A, new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = inSlot, Destination = r.T.Discard }, out Dictionary<PlayerId, byte[]> discarded));
            r.Deliver(discarded);
            foreach (PlayerId p in r.Host.Recipients) Assert.AreEqual(1, r.Tables[p].Containers[r.T.Discard].Count);

            // A's pawn moves by the generic piece operation (policy check, then the host's table), replicated to both.
            PrivateOperation move = new PrivateOperation { Kind = PrivateOperationKind.MovePiece, RequestId = Guid.NewGuid(), Card = r.T.Pawns[handA], HasPose = true };
            Assert.AreEqual(PrivateRequestVerdict.Accepted, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.A, move, out PrivateResolvedOperation resolved));
            PhysicalObjectState rest = new PhysicalObjectState(new PhysicalVector3(3f, 0.1f, 1f), new PhysicalRotation(0f, 0f, 0f, 1f), default, default, PhysicalObjectMode.Sleeping, r.A);
            r.Deliver(r.Host.Apply(() => r.T.Match.GetObject(resolved.RealCard).SetPhysicalState(rest)));
            foreach (PlayerId p in r.Host.Recipients) Assert.AreEqual(3f, r.Tables[p].GetObject(r.T.Pawns[handA]).PhysicalState.Position.X, 1e-4f, $"{p}: the pawn moved");
            PrivateOperation notMine = new PrivateOperation { Kind = PrivateOperationKind.MovePiece, RequestId = Guid.NewGuid(), Card = r.T.Pawns[handA], HasPose = true };
            Assert.AreEqual(PrivateRequestVerdict.NotYourTurn, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.B, notMine, out _), "B on A's turn");

            // The turn passes (the cartridge's own rule); B now draws, and the same privacy holds the other way round.
            r.T.TurnIndex = 1;
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Draw(r.B, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = r.T.Deck }, out Dictionary<PlayerId, byte[]> drewB));
            r.Deliver(drewB);
            TabletopObjectId bSeesOwn = r.Tables[r.B].Containers[r.T.Hands[handB]].GetObjectAt(0);
            TabletopObjectId aSeesB = r.Tables[r.A].Containers[r.T.Hands[handB]].GetObjectAt(0);
            Assert.IsFalse(r.Tables[r.B].Cards[bSeesOwn].BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition));
            Assert.IsTrue(r.Tables[r.A].Cards[aSeesB].BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition));

            r.AssertEveryoneMatchesTheHost();

            // No message any recipient received names a card it may not know: no hidden real id, no hidden label.
            HashSet<TabletopObjectId> stillHidden = new HashSet<TabletopObjectId>(r.T.Match.Containers[r.T.Deck].ObjectIds);
            foreach (byte[] bytes in r.Sent)
            {
                string text = Encoding.UTF8.GetString(bytes);
                foreach (TabletopObjectId id in stillHidden)
                {
                    StringAssert.DoesNotContain(r.T.Labels[id], text, "a deck card's label");
                    Assert.IsFalse(Contains(bytes, id.Value.ToByteArray()), "a deck card's real id");
                }
            }
        }

        private static bool Contains(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }

            return false;
        }
    }
}
