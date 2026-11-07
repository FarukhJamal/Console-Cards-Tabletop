using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Privacy;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// PRIVACY-P6A (s150): one pipeline for every seat (the host's own seat is just another sender), the authoritative
    /// revision, concealment without the card's real id, handle rotation, the public turn, and the End Turn bytes the
    /// game's own End Turn authority checks. In-process (unit) only: the three-process acceptance run is separate.
    /// </summary>
    public sealed class PrivateUnifiedPipelineTests
    {
        private static readonly byte[] Key = { 6, 6, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };
        private static readonly byte[] OtherKey = { 7, 7, 7, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 99 };
        private const int Seed = 0x5EED6A6A;

        private sealed class Seats : IPrivateSeats
        {
            public TrapFloorTestTable T;
            public bool AnyTime = true;
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
            public int DrawLimit(PlayerId player) => -1;
        }

        // The host's own seat (H) and two guests (A, B), plus the spectator view. Every client keeps its own table and
        // the revision it shows; an update is applied only through the revision gate.
        private sealed class Room
        {
            public TrapFloorTestTable T;
            public Seats S;
            public PrivateTableHost Host;
            public PlayerId H, A, B;
            public readonly Dictionary<PlayerId, MatchState> Tables = new Dictionary<PlayerId, MatchState>();
            public readonly Dictionary<PlayerId, long> Shown = new Dictionary<PlayerId, long>();
            public int Mismatches;
            public int Packages;

            public static Room Open()
            {
                Room r = new Room { T = TrapFloorTestTable.Create(3, Seed) };
                r.S = new Seats { T = r.T };
                r.Host = new PrivateTableHost(r.T.Match, r.S, r.T.Template.CardLabels, Key);
                r.H = r.T.Players[0];
                r.A = r.T.Players[1];
                r.B = r.T.Players[2];
                foreach (PlayerId p in r.Host.Recipients) r.Rejoin(p);
                r.Packages = 0; // the joins are not counted as recovery
                return r;
            }

            public void Rejoin(PlayerId p)
            {
                byte[] bytes = Host.Join(p);
                RecipientBootstrapPackage package = (RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false);
                Tables[p] = RecipientClientTable.Build(package).Table;
                Shown[p] = package.Revision;
                Packages++;
            }

            public void Deliver(Dictionary<PlayerId, byte[]> updates)
            {
                Assert.AreEqual(Tables.Count, updates.Count, "one update per recipient");
                long revision = -1;
                foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
                {
                    PrivateUpdate update = RecipientEventApplier.Read(pair.Value);
                    if (revision < 0) revision = update.Revision;
                    Assert.AreEqual(revision, update.Revision, "every recipient's update of one action carries the same revision");
                    Assert.AreEqual(PrivateRevisionStep.Apply, PrivateRevisionGate.Next(Shown[pair.Key], update.Revision), "the next revision, in order");
                    string problem = RecipientEventApplier.Apply(Tables[pair.Key], update);
                    if (problem != null) Mismatches++;
                    Assert.IsNull(problem, "update applies with a matching digest");
                    Shown[pair.Key] = update.Revision;
                }

                Assert.AreEqual(Host.Revision, revision, "the update carries the host's revision after the action");
            }

            // L: each client's table, built by its updates alone, equals the host's projection for it (a fresh package).
            public void AssertEveryoneMatchesTheHost()
            {
                foreach (PlayerId p in Host.Recipients)
                {
                    byte[] bytes = Host.Join(p);
                    MatchState fresh = RecipientClientTable.Build((RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false)).Table;
                    Assert.AreEqual(PrivateClientModel.FromTable(fresh).Digest(), PrivateClientModel.FromTable(Tables[p]).Digest(), $"recipient {p} matches the host's projection");
                    foreach (KeyValuePair<ContainerId, ContainerState> pair in fresh.Containers)
                        Assert.AreEqual(pair.Value.Count, Tables[p].Containers[pair.Key].Count, "container counts");
                }
            }

            public PrivateRequestVerdict Draw(PlayerId p, out Dictionary<PlayerId, byte[]> u) =>
                Host.Draw(p, new PrivateDrawRequest { RequestId = Guid.NewGuid(), Deck = T.SetupOf(p).ControllerDeckId }, out u);

            public TabletopObjectId LastInHand(PlayerId viewer, PlayerId owner)
            {
                ContainerState hand = Tables[viewer].Containers[T.SetupOf(owner).HandContainerId];
                return hand.GetObjectAt(hand.Count - 1);
            }

            public ContainerId Pile()
            {
                foreach (KeyValuePair<ContainerId, ContainerState> pair in T.Match.Containers)
                    if (Host.IsPublicDiscardPile(pair.Key, out _)) return pair.Key;
                Assert.Fail("a public discard pile");
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

        // K: no real id of a card hidden from this recipient anywhere in its bytes (strict: no exception for a previous reference).
        private static void AssertNoHiddenIds(Room r, PlayerId recipient, byte[] bytes)
        {
            RecipientSnapshotProjection.Classify(r.T.Match, recipient, out HashSet<TabletopObjectId> hidden, out _);
            foreach (TabletopObjectId id in hidden)
                Assert.IsFalse(Carries(bytes, id.Value.ToByteArray()), $"no hidden real id in {recipient}'s bytes");
        }

        private static PrivateCardRequest Req(TabletopObjectId card, ContainerId to = default) =>
            new PrivateCardRequest { RequestId = Guid.NewGuid(), Card = card, Destination = to };

        [Test]
        public void Task2Sequence_HostSeatThenGuest_EveryRecipientFollowsByUpdatesAlone_InOrder()
        {
            Room r = Room.Open();
            long start = r.Host.Revision;

            // 1–2. The host's own seat draws: H sees the face, everyone else a back and the count.
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Draw(r.H, out Dictionary<PlayerId, byte[]> drawn));
            r.Deliver(drawn);
            TabletopObjectId card = r.LastInHand(r.H, r.H);
            Assert.IsFalse(Hidden(r.Tables[r.H], card));
            foreach (PlayerId other in new[] { r.A, r.B, RecipientSnapshotProjection.Spectator })
            {
                Assert.IsFalse(r.Tables[other].Cards.ContainsKey(card), "others never get the drawn card's id");
                Assert.IsTrue(Hidden(r.Tables[other], r.LastInHand(other, r.H)), "others see a back");
                AssertNoHiddenIds(r, other, drawn[other]);
            }

            // 3–4. Face up on H's Console: everyone gets the identity.
            ContainerId slot = r.T.SetupOf(r.H).SideSlotContainerIds[0];
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Place(r.H, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = card, Destination = slot }, out Dictionary<PlayerId, byte[]> placed));
            r.Deliver(placed);
            foreach (PlayerId p in r.Host.Recipients)
            {
                Assert.IsTrue(r.Tables[p].Cards.ContainsKey(card), "public on the Console");
                Assert.AreEqual(1, r.Tables[p].Containers[slot].Count, "Console occupancy");
            }

            // 5–6. Back to H's hand: hidden again for everyone else.
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Return(r.H, Req(card), out Dictionary<PlayerId, byte[]> returned));
            r.Deliver(returned);
            foreach (PlayerId other in new[] { r.A, r.B, RecipientSnapshotProjection.Spectator })
            {
                Assert.IsFalse(r.Tables[other].Cards.ContainsKey(card), "concealed again");
                Assert.AreEqual(0, r.Tables[other].Containers[slot].Count);
                AssertNoHiddenIds(r, other, returned[other]);
            }

            // 7–8. Guest A draws and places: B sees the back, then the identity.
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Draw(r.A, out Dictionary<PlayerId, byte[]> aDrawn));
            r.Deliver(aDrawn);
            TabletopObjectId aCard = r.LastInHand(r.A, r.A);
            Assert.IsFalse(r.Tables[r.B].Cards.ContainsKey(aCard));
            Assert.IsFalse(r.Tables[r.H].Cards.ContainsKey(aCard), "the host's own seat is a recipient like any other");
            ContainerId aSlot = r.T.SetupOf(r.A).SideSlotContainerIds[0];
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = aCard, Destination = aSlot }, out Dictionary<PlayerId, byte[]> aPlaced));
            r.Deliver(aPlaced);
            Assert.AreEqual(r.T.Match.Cards[aCard].BaseState.DefinitionId, r.Tables[r.B].Cards[aCard].BaseState.DefinitionId, "B sees A's placed card");

            // 9. Five accepted actions, five revisions; no recovery was needed.
            Assert.AreEqual(start + 5, r.Host.Revision);
            Assert.AreEqual(0, r.Mismatches);
            Assert.AreEqual(0, r.Packages, "no package after any ordinary action");
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void HostSeat_GetsNoException_SameRefusalsAsAGuest()
        {
            Room r = Room.Open();
            r.Draw(r.A, out Dictionary<PlayerId, byte[]> u);
            r.Deliver(u);
            TabletopObjectId aCard = r.LastInHand(r.A, r.A);
            ContainerId hSlot = r.T.SetupOf(r.H).SideSlotContainerIds[0];
            ContainerId aSlot = r.T.SetupOf(r.A).SideSlotContainerIds[0];
            long revision = r.Host.Revision;
            string before = RecipientTableDigest.Of(r.T.Match);

            // The host's own seat may not use a guest's card, a guest's slot, or a guest's turn (Full).
            Assert.AreEqual(PrivateRequestVerdict.HiddenRealId,
                r.Host.Place(r.H, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = aCard, Destination = hSlot }, out _), "a guest's card by its real id");
            TabletopObjectId hHandleForA = r.LastInHand(r.H, r.A);
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, r.Host.Discard(r.H, Req(hHandleForA, r.Pile()), out _), "H's handle for A's card");
            r.Draw(r.H, out Dictionary<PlayerId, byte[]> hd);
            r.Deliver(hd);
            revision = r.Host.Revision;
            before = RecipientTableDigest.Of(r.T.Match);
            Assert.AreEqual(PrivateRequestVerdict.InvalidDestination,
                r.Host.Place(r.H, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = r.LastInHand(r.H, r.H), Destination = aSlot }, out _), "a guest's Console slot");
            r.S.AnyTime = false; // Full
            PlayerId notActive = r.T.Turn.ActivePlayerId == r.H ? r.A : r.H;
            Assert.AreEqual(PrivateRequestVerdict.NotYourTurn, r.Draw(notActive, out _), "Full: the turn applies to the host's own seat too");
            Assert.AreEqual(before, RecipientTableDigest.Of(r.T.Match));
            Assert.AreEqual(revision, r.Host.Revision, "refused requests leave the revision unchanged");
        }

        [Test]
        public void Concealment_SendsNoRealId_TheOldViewIsNamedByItsPlace()
        {
            Room r = Room.Open();
            r.Draw(r.A, out Dictionary<PlayerId, byte[]> u);
            r.Deliver(u);
            TabletopObjectId card = r.LastInHand(r.A, r.A);
            ContainerId slot = r.T.SetupOf(r.A).SideSlotContainerIds[0];
            r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = card, Destination = slot }, out Dictionary<PlayerId, byte[]> placed);
            r.Deliver(placed);

            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Return(r.A, Req(card), out Dictionary<PlayerId, byte[]> returned));
            foreach (PlayerId other in new[] { r.B, r.H, RecipientSnapshotProjection.Spectator })
            {
                PrivateUpdate update = RecipientEventApplier.Read(returned[other]);
                Assert.IsTrue(update.Event.PreviousReference.IsEmpty, "no previous reference: the card's real id is not sent again");
                Assert.AreEqual(0, update.Event.FromIndex, "the old view is named by its public place");
                Assert.AreEqual(slot, update.Event.From);
                Assert.IsFalse(Carries(returned[other], card.Value.ToByteArray()), "the real id is nowhere in the bytes");
                AssertNoHiddenIds(r, other, returned[other]);
            }

            PrivateUpdate own = RecipientEventApplier.Read(returned[r.A]);
            Assert.AreEqual(card, own.Event.PreviousReference, "the owner, who still sees it, keeps the real id");
            r.Deliver(returned);
            TabletopObjectId handleB = r.LastInHand(r.B, r.A);
            Assert.IsTrue(Hidden(r.Tables[r.B], handleB));
            Assert.AreNotEqual(card, handleB);

            // The old public reference does not authorize a hidden action; the new handle is B's own (not A's card to use).
            Assert.AreEqual(PrivateRequestVerdict.HiddenRealId, r.Host.Discard(r.B, Req(card, r.Pile()), out _));
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, r.Host.Discard(r.B, Req(handleB, r.Pile()), out _));
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void FaceDownDiscard_ConcealsFromTheOwnerToo_ByPlace()
        {
            Room r = Room.Open();
            r.Draw(r.A, out Dictionary<PlayerId, byte[]> u1);
            r.Deliver(u1);
            r.Draw(r.A, out Dictionary<PlayerId, byte[]> u2);
            r.Deliver(u2);
            ContainerState hand = r.T.Match.Containers[r.T.SetupOf(r.A).HandContainerId];
            TabletopObjectId first = hand.GetObjectAt(0);
            ContainerId pile = r.Pile();
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Discard(r.A, Req(first, pile), out Dictionary<PlayerId, byte[]> d));
            bool faceUp = r.T.Match.Cards[first].Face == CardFace.FaceUp;
            PrivateUpdate own = RecipientEventApplier.Read(d[r.A]);
            if (!faceUp)
            {
                Assert.IsTrue(own.Event.PreviousReference.IsEmpty, "a face-down discard hides the card from its owner: named by place");
                Assert.AreEqual(0, own.Event.FromIndex);
                Assert.IsFalse(Carries(d[r.A], first.Value.ToByteArray()));
            }

            r.Deliver(d);
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void HandOrder_ConsoleOccupancy_PileCounts_StayInStepWithTheHost()
        {
            Room r = Room.Open();
            for (int i = 0; i < 3; i++)
            {
                r.Draw(r.A, out Dictionary<PlayerId, byte[]> u);
                r.Deliver(u);
            }

            ContainerId handA = r.T.SetupOf(r.A).HandContainerId;
            TabletopObjectId middle = r.T.Match.Containers[handA].GetObjectAt(1);
            r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = middle, Destination = r.T.SetupOf(r.A).SideSlotContainerIds[1] }, out Dictionary<PlayerId, byte[]> p);
            r.Deliver(p);
            r.Host.Discard(r.A, Req(r.T.Match.Containers[handA].GetObjectAt(0), r.Pile()), out Dictionary<PlayerId, byte[]> d);
            r.Deliver(d);
            r.Host.Return(r.A, Req(middle), out Dictionary<PlayerId, byte[]> back);
            r.Deliver(back);

            // The owner's hand order, card by card, is the host's.
            ContainerState host = r.T.Match.Containers[handA];
            ContainerState mine = r.Tables[r.A].Containers[handA];
            Assert.AreEqual(host.Count, mine.Count);
            for (int i = 0; i < host.Count; i++) Assert.AreEqual(host.GetObjectAt(i), mine.GetObjectAt(i), $"hand position {i}");
            foreach (PlayerId q in r.Host.Recipients)
            {
                foreach (ContainerId slot in r.T.SetupOf(r.A).SideSlotContainerIds)
                    Assert.AreEqual(r.T.Match.Containers[slot].Count, r.Tables[q].Containers[slot].Count, "Console occupancy");
                Assert.AreEqual(r.T.Match.Containers[r.Pile()].Count, r.Tables[q].Containers[r.Pile()].Count, "discard pile count");
            }

            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void RevisionGate_AppliesTheNext_IgnoresRepeats_FlagsGaps()
        {
            Assert.AreEqual(PrivateRevisionStep.Apply, PrivateRevisionGate.Next(4, 5));
            Assert.AreEqual(PrivateRevisionStep.AlreadyShown, PrivateRevisionGate.Next(5, 5), "a repeat");
            Assert.AreEqual(PrivateRevisionStep.AlreadyShown, PrivateRevisionGate.Next(9, 5), "older than the package taken since");
            Assert.AreEqual(PrivateRevisionStep.Gap, PrivateRevisionGate.Next(4, 6), "one missed");

            // A client that missed an update: the next one is a gap (it takes its own package), never a wrong table.
            Room r = Room.Open();
            r.Draw(r.A, out Dictionary<PlayerId, byte[]> missed);
            r.Draw(r.A, out Dictionary<PlayerId, byte[]> next);
            PrivateUpdate second = RecipientEventApplier.Read(next[r.B]);
            Assert.AreEqual(PrivateRevisionStep.Gap, PrivateRevisionGate.Next(r.Shown[r.B], second.Revision));
            r.Rejoin(r.B);
            Assert.AreEqual(r.Host.Revision, r.Shown[r.B], "the package carries the host's revision");
            Assert.AreEqual(PrivateRevisionStep.AlreadyShown, PrivateRevisionGate.Next(r.Shown[r.B], second.Revision), "the late update is then ignored");
        }

        [Test]
        public void RotateHandles_OldHandlesReachNothing_NoHandleSurvives_PackagesStayConsistent()
        {
            Room r = Room.Open();
            r.Draw(r.A, out Dictionary<PlayerId, byte[]> u);
            r.Deliver(u);
            HashSet<TabletopObjectId> before = new HashSet<TabletopObjectId>();
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in r.Tables[r.B].Cards)
                if (Hidden(r.Tables[r.B], pair.Key)) before.Add(pair.Key);
            Assert.Greater(before.Count, 0);
            TabletopObjectId oldHandle = r.LastInHand(r.B, r.A);
            long revision = r.Host.Revision;

            r.Host.RotateHandles(OtherKey);
            Assert.AreEqual(revision + 1, r.Host.Revision, "a rotation is a revision (clients take their packages)");
            Assert.AreEqual(PrivateRequestVerdict.UnknownReference, r.Host.Discard(r.B, Req(oldHandle, r.Pile()), out _), "an old handle reaches nothing");
            r.Rejoin(r.B);
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in r.Tables[r.B].Cards)
                if (Hidden(r.Tables[r.B], pair.Key)) Assert.IsFalse(before.Contains(pair.Key), "no handle survives a rotation (nothing to correlate across it)");
            foreach (PlayerId p in r.Host.Recipients) if (p != r.B) r.Rejoin(p);
            r.Draw(r.A, out Dictionary<PlayerId, byte[]> after);
            r.Deliver(after);
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void Reconnect_AfterManyTransfers_RestoresRevisionTurnAndTable()
        {
            Room r = Room.Open();
            r.Host.SetInitialTurn(new PrivateTurnSnapshot { Round = 1, Phase = (int)r.T.Turn.Phase, TurnOwner = TrapFloorEndTurnAuthority.TurnOwnerOf(r.T.Turn).Value, ActivePlayer = r.T.Turn.ActivePlayerId.Value });
            for (int i = 0; i < 2; i++)
            {
                foreach (PlayerId p in new[] { r.H, r.A, r.B })
                {
                    r.Draw(p, out Dictionary<PlayerId, byte[]> u);
                    r.Deliver(u);
                }
            }

            TabletopObjectId h = r.LastInHand(r.H, r.H);
            r.Host.Place(r.H, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = h, Destination = r.T.SetupOf(r.H).SideSlotContainerIds[0] }, out Dictionary<PlayerId, byte[]> pl);
            r.Deliver(pl);
            r.Host.Discard(r.H, Req(h, r.Pile()), out Dictionary<PlayerId, byte[]> di);
            r.Deliver(di);
            byte[] turnBytes = r.Host.TurnChanged(new PrivateTurnSnapshot { Round = 1, Phase = 0, TurnOwner = r.A.Value, ActivePlayer = r.A.Value });
            PrivateTurnUpdate turn = (PrivateTurnUpdate)TableWire.Read(turnBytes, 0, turnBytes.Length, false);
            Assert.AreEqual(r.Host.Revision, turn.Revision);
            Assert.AreEqual(r.A.Value, turn.Turn.TurnOwner);

            foreach (PlayerId p in new List<PlayerId>(r.Tables.Keys))
            {
                string kept = PrivateClientModel.FromTable(r.Tables[p]).Digest();
                byte[] bytes = r.Host.Join(p);
                RecipientBootstrapPackage package = (RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false);
                Assert.AreEqual(r.Host.Revision, package.Revision, "reconnect: the revision");
                Assert.AreEqual(r.A.Value, package.Turn.TurnOwner, "reconnect: the active seat");
                Assert.AreEqual(kept, PrivateClientModel.FromTable(RecipientClientTable.Build(package).Table).Digest(), "reconnect: hand, Console, pile, visibility");
                AssertNoHiddenIds(r, p, bytes);
            }
        }

        [Test]
        public void EndTurnBytes_RoundTrip_AreStrict_AndTheGameAuthorityRunsEachOnce()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3, Seed);
            PlayerId owner = TrapFloorEndTurnAuthority.TurnOwnerOf(t.Turn);
            PrivateEndTurnRequest wire = new PrivateEndTurnRequest { RequestId = Guid.NewGuid(), Round = t.Turn.CurrentRound, Phase = (int)t.Turn.Phase, TurnOwner = owner };
            byte[] bytes = PrivateRequestCodec.EndTurn(wire);
            Assert.AreEqual(PrivateRequestCodec.Size, bytes.Length);
            Assert.IsTrue(PrivateRequestCodec.TryReadEndTurn(bytes, 0, out PrivateEndTurnRequest read));
            Assert.AreEqual(wire.RequestId, read.RequestId);
            Assert.AreEqual(wire.Round, read.Round);
            Assert.AreEqual(wire.Phase, read.Phase);
            Assert.AreEqual(owner, read.TurnOwner);
            Assert.IsFalse(PrivateRequestCodec.TryRead(bytes, 0, out _, out _, out _, out _), "not a card request");
            byte[] padded = (byte[])bytes.Clone();
            padded[45] = 1;
            Assert.IsFalse(PrivateRequestCodec.TryReadEndTurn(padded, 0, out _), "unused bytes must be empty");
            byte[] noId = (byte[])bytes.Clone();
            for (int i = 1; i < 17; i++) noId[i] = 0;
            Assert.IsFalse(PrivateRequestCodec.TryReadEndTurn(noId, 0, out _));

            // As the host runs it: the game's own End Turn authority and turn service, the sender from the connection.
            TrapFloorEndTurnAuthority authority = new TrapFloorEndTurnAuthority();
            TrapFloorTurnService turns = new TrapFloorTurnService(t.Template, t.Turn, new ControllerInputHandService(), t.Feed);
            TrapFloorEndTurnRequest request = new TrapFloorEndTurnRequest(read.RequestId, read.Round, (TrapFloorTurnPhase)read.Phase, read.TurnOwner, read.Skipped);
            PlayerId someoneElse = Array.Find(t.Players, p => p != owner);
            Assert.AreEqual(TrapFloorEndTurnVerdict.NotTurnOwner, authority.Check(t.Turn, someoneElse, request), "another seat (the host's own included) cannot end it");
            Assert.AreEqual(TrapFloorEndTurnVerdict.Accepted, authority.Check(t.Turn, owner, request));
            Assert.IsTrue(turns.Advance(t.Match, t.Context(owner)).Succeeded);
            Assert.AreNotEqual(owner, TrapFloorEndTurnAuthority.TurnOwnerOf(t.Turn), "advanced once");
            Assert.AreEqual(TrapFloorEndTurnVerdict.Duplicate, authority.Check(t.Turn, owner, request), "the same request id again");
            TrapFloorEndTurnRequest stale = new TrapFloorEndTurnRequest(Guid.NewGuid(), read.Round, (TrapFloorTurnPhase)read.Phase, read.TurnOwner, false);
            Assert.AreNotEqual(TrapFloorEndTurnVerdict.Accepted, authority.Check(t.Turn, owner, stale), "a new id for the old turn");
            PlayerId next = TrapFloorEndTurnAuthority.TurnOwnerOf(t.Turn);
            TrapFloorEndTurnRequest staleRound = new TrapFloorEndTurnRequest(Guid.NewGuid(), t.Turn.CurrentRound - 1, t.Turn.Phase, next, false);
            Assert.AreEqual(TrapFloorEndTurnVerdict.StaleTurn, authority.Check(t.Turn, next, staleRound), "an earlier round");
        }

        [Test]
        public void TurnTransition_IsPublic_OneRevision_SameBytesForEveryone()
        {
            Room r = Room.Open();
            long revision = r.Host.Revision;
            byte[] bytes = r.Host.TurnChanged(new PrivateTurnSnapshot { Round = 2, Phase = 1, TurnOwner = r.B.Value, ActivePlayer = r.B.Value });
            Assert.AreEqual(revision + 1, r.Host.Revision);
            foreach (PlayerId p in r.Host.Recipients) AssertNoHiddenIds(r, p, bytes);
            Assert.IsFalse(Carries(bytes, BitConverter.GetBytes(Seed)), "no seed");
            // The next card update follows the turn's revision in order.
            r.Draw(r.A, out Dictionary<PlayerId, byte[]> u);
            Assert.AreEqual(revision + 2, RecipientEventApplier.Read(u[r.B]).Revision);
        }
    }
}
