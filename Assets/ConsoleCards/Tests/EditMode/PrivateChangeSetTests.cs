using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Privacy;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// PRIVACY-P6B (s151): change sets (any change on the host's table, filtered per recipient), the general operation
    /// policy and codec, shuffles that leave no reference to follow, and an Undo-style table replacement. In-process
    /// (unit) only: the three-process acceptance run is separate.
    /// </summary>
    public sealed class PrivateChangeSetTests
    {
        private static readonly byte[] Key = { 4, 4, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };
        private const int Seed = 0x6B6B6B6B;

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

        private sealed class Room
        {
            public TrapFloorTestTable T;
            public Seats S;
            public PrivateTableHost Host;
            public PlayerId A, B;
            public readonly Dictionary<PlayerId, MatchState> Tables = new Dictionary<PlayerId, MatchState>();
            public readonly Dictionary<PlayerId, long> Shown = new Dictionary<PlayerId, long>();

            public static Room Open()
            {
                Room r = new Room { T = TrapFloorTestTable.Create(3, Seed) };
                r.S = new Seats { T = r.T };
                r.Host = new PrivateTableHost(r.T.Match, r.S, r.T.Template.CardLabels, Key);
                r.A = r.T.Players[0];
                r.B = r.T.Players[1];
                foreach (PlayerId p in r.Host.Recipients) r.Rejoin(p);
                return r;
            }

            public void Rejoin(PlayerId p)
            {
                byte[] bytes = Host.Join(p);
                RecipientBootstrapPackage package = (RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false);
                Tables[p] = RecipientClientTable.Build(package).Table;
                Shown[p] = package.Revision;
            }

            public Dictionary<PlayerId, PrivateChangeSummary> Deliver(Dictionary<PlayerId, byte[]> updates)
            {
                Assert.IsNotNull(updates, "the change was published");
                Dictionary<PlayerId, PrivateChangeSummary> summaries = new Dictionary<PlayerId, PrivateChangeSummary>();
                foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
                {
                    PrivateUpdate update = RecipientEventApplier.Read(pair.Value);
                    Assert.IsNotNull(update.Changes, "every update carries a change set");
                    Assert.IsFalse(update.Changes.NeedsPackage, "described as object changes");
                    Assert.AreEqual(PrivateRevisionStep.Apply, PrivateRevisionGate.Next(Shown[pair.Key], update.Revision));
                    PrivateChangeSummary summary = new PrivateChangeSummary();
                    Assert.IsNull(RecipientEventApplier.Apply(Tables[pair.Key], update, summary), "applies with a matching digest");
                    Shown[pair.Key] = update.Revision;
                    summaries[pair.Key] = summary;
                }

                return summaries;
            }

            public void AssertEveryoneMatchesTheHost()
            {
                foreach (PlayerId p in Host.Recipients)
                {
                    byte[] bytes = Host.Join(p);
                    MatchState fresh = RecipientClientTable.Build((RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false)).Table;
                    Assert.AreEqual(PrivateClientModel.FromTable(fresh).Digest(), PrivateClientModel.FromTable(Tables[p]).Digest(), $"recipient {p} matches the host's projection");
                    Shown[p] = Host.Revision;
                }
            }

            public ContainerId Pile()
            {
                foreach (KeyValuePair<ContainerId, ContainerState> pair in T.Match.Containers)
                    if (Host.IsPublicDiscardPile(pair.Key, out _)) return pair.Key;
                Assert.Fail("a public discard pile");
                return ContainerId.Empty;
            }
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

        private static void AssertNoHiddenIds(Room r, Dictionary<PlayerId, byte[]> updates)
        {
            foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
            {
                RecipientSnapshotProjection.Classify(r.T.Match, pair.Key, out HashSet<TabletopObjectId> hidden, out _);
                foreach (TabletopObjectId id in hidden) Assert.IsFalse(Carries(pair.Value, id.Value.ToByteArray()), $"no hidden real id in {pair.Key}'s bytes");
            }
        }

        private static HashSet<TabletopObjectId> Refs(MatchState table, ContainerId container) => new HashSet<TabletopObjectId>(table.Containers[container].ObjectIds);

        [Test]
        public void PieceRestingPlace_AndDieFace_ReachEveryRecipient_AsOneRevision()
        {
            // P7 (s154), live finding: a pawn put down by the table's physics records only its physical state (its pose
            // stays where it was made), and a die's face is its value. Both are public; both must reach every guest.
            Room r = Room.Open();
            TabletopObjectId pawn = r.T.SetupOf(r.A).PawnId;
            TabletopObjectId die = default;
            int sides = 0;
            foreach (var pair in r.T.Match.Dice) { die = pair.Key; sides = pair.Value.SideCount; break; }
            Assert.IsFalse(die.IsEmpty, "the table has a die");
            int face = r.T.Match.Dice[die].CurrentValue % sides + 1;
            PhysicalObjectState rest = new PhysicalObjectState(new PhysicalVector3(1.5f, 0.1f, -2.25f), new PhysicalRotation(0f, 0f, 0f, 1f),
                default, default, PhysicalObjectMode.Sleeping, r.A);
            long revision = r.Host.Revision;
            Dictionary<PlayerId, byte[]> updates = r.Host.Apply(() =>
            {
                r.T.Match.GetObject(pawn).SetPhysicalState(rest);
                r.T.Match.GetObject(die).SetPhysicalState(rest);
                r.T.Match.Dice[die].SetAcceptedRoll(new ConsoleCards.Core.Domain.Dice.DieRoll(sides, face));
            });
            Assert.AreEqual(revision + 1, r.Host.Revision, "one revision");
            AssertNoHiddenIds(r, updates);
            Dictionary<PlayerId, PrivateChangeSummary> seen = r.Deliver(updates);
            foreach (PlayerId p in r.Host.Recipients)
            {
                Assert.IsTrue(seen[p].Moved.Contains(pawn) && seen[p].Moved.Contains(die), $"{p}: the pawn and the die are moved");
                PhysicalObjectState at = r.Tables[p].GetObject(pawn).PhysicalState;
                Assert.IsNotNull(at, $"{p}: the pawn's resting place");
                Assert.AreEqual(1.5f, at.Position.X, 1e-4f);
                Assert.AreEqual(-2.25f, at.Position.Z, 1e-4f);
                Assert.AreEqual(face, r.Tables[p].Dice[die].CurrentValue, $"{p}: the die shows the host's face");
            }

            r.AssertEveryoneMatchesTheHost();
            Assert.IsNull(r.Host.Apply(() => { }), "nothing changed: nothing is published");

            // A piece still moving is not sent (the recipient's own physics would run it and report a new throw);
            // where it comes to rest is.
            PhysicalObjectState flying = new PhysicalObjectState(new PhysicalVector3(0.5f, 0.6f, 0.5f), new PhysicalRotation(0f, 0f, 0f, 1f),
                new PhysicalVector3(1f, 0f, 0f), default, PhysicalObjectMode.Dynamic, r.A);
            Assert.IsNull(r.Host.Apply(() => r.T.Match.GetObject(pawn).SetPhysicalState(flying)), "in flight: nothing is published");
            PhysicalObjectState landed = new PhysicalObjectState(new PhysicalVector3(0.75f, 0.1f, 0.5f), new PhysicalRotation(0f, 0f, 0f, 1f),
                default, default, PhysicalObjectMode.Sleeping, r.A);
            r.Deliver(r.Host.Apply(() => r.T.Match.GetObject(pawn).SetPhysicalState(landed)));
            foreach (PlayerId p in r.Host.Recipients) Assert.AreEqual(0.75f, r.Tables[p].GetObject(pawn).PhysicalState.Position.X, 1e-4f, $"{p}: where it came to rest");
        }

        [Test]
        public void DomainDrawOfThree_IsOneRevision_EachRecipientGetsOnlyWhatItMayKnow()
        {
            Room r = Room.Open();
            ContainerId deck = r.T.SetupOf(r.A).ControllerDeckId, hand = r.T.SetupOf(r.A).HandContainerId;
            long revision = r.Host.Revision;
            Dictionary<PlayerId, byte[]> updates = r.Host.Apply(() =>
                Assert.IsTrue(new DrawCardsUseCase().Execute(r.T.Match, new DrawCardsCommand(r.T.Context(r.A), deck, hand, 3)).Succeeded));
            Assert.AreEqual(revision + 1, r.Host.Revision, "three cards, one accepted change, one revision");
            AssertNoHiddenIds(r, updates);
            r.Deliver(updates);
            ContainerState handOnA = r.Tables[r.A].Containers[hand];
            ContainerState handOnB = r.Tables[r.B].Containers[hand];
            Assert.AreEqual(r.T.Match.Containers[hand].Count, handOnA.Count);
            Assert.AreEqual(handOnA.Count, handOnB.Count, "the count is public");
            foreach (TabletopObjectId id in handOnA.ObjectIds) Assert.IsTrue(r.T.Match.Cards.ContainsKey(id), "A holds its own cards by their real ids");
            foreach (TabletopObjectId id in handOnB.ObjectIds) Assert.IsFalse(r.T.Match.Cards.ContainsKey(id), "B holds backs only");
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void FlipOnTheConsole_ConcealsByPlace_AndRevealsAgain()
        {
            Room r = Room.Open();
            ContainerId deck = r.T.SetupOf(r.A).ControllerDeckId, hand = r.T.SetupOf(r.A).HandContainerId;
            r.Deliver(r.Host.Apply(() => new DrawCardsUseCase().Execute(r.T.Match, new DrawCardsCommand(r.T.Context(r.A), deck, hand, 1))));
            ContainerState handState = r.T.Match.Containers[hand];
            TabletopObjectId card = handState.GetObjectAt(handState.Count - 1);
            ContainerId slot = r.T.SetupOf(r.A).SideSlotContainerIds[0];
            Assert.AreEqual(PrivateRequestVerdict.Accepted, r.Host.Place(r.A, new PrivatePlaceRequest { RequestId = Guid.NewGuid(), Card = card, Destination = slot }, out Dictionary<PlayerId, byte[]> placed));
            r.Deliver(placed);
            foreach (PlayerId p in r.Host.Recipients) Assert.IsTrue(r.Tables[p].Cards.ContainsKey(card), "face up on the Console: public");

            Dictionary<PlayerId, byte[]> down = r.Host.Apply(() => r.T.Match.Cards[card].SetFace(CardFace.FaceDown));
            foreach (KeyValuePair<PlayerId, byte[]> pair in down)
            {
                Assert.IsFalse(Carries(pair.Value, card.Value.ToByteArray()), "a face-down Console card is hidden from everyone, its owner too: its real id is not sent");
                PrivateUpdate update = RecipientEventApplier.Read(pair.Value);
                Assert.AreEqual(1, update.Changes.Removed.Count);
                Assert.IsTrue(update.Changes.Removed[0].Reference.IsEmpty, "named by its place");
            }

            Dictionary<PlayerId, PrivateChangeSummary> summaries = r.Deliver(down);
            foreach (PlayerId p in r.Host.Recipients)
            {
                Assert.IsFalse(r.Tables[p].Cards.ContainsKey(card));
                Assert.AreEqual(card, summaries[p].Withdrawn[0], "the client knows which of its views it was");
            }

            Assert.AreEqual(PrivateRequestVerdict.HiddenRealId,
                PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.B, new PrivateOperation { Kind = PrivateOperationKind.Flip, RequestId = Guid.NewGuid(), Card = card, FaceUp = true }, out _),
                "the old public reference authorizes nothing");

            r.Deliver(r.Host.Apply(() => r.T.Match.Cards[card].SetFace(CardFace.FaceUp)));
            foreach (PlayerId p in r.Host.Recipients) Assert.AreEqual(r.T.Match.Cards[card].BaseState.DefinitionId, r.Tables[p].Cards[card].BaseState.DefinitionId, "revealed to everyone");
            r.AssertEveryoneMatchesTheHost();
        }

        // Live P6B finding: an Undo that took a loose face-up card back into its owner's hand sent the other guest its
        // whole package. A card loose on the table that becomes hidden is named once more by the id it was public by.
        [Test]
        public void LooseCardTakenBackIntoAHand_IsAChangeSet_NotAPackage()
        {
            Room r = Room.Open();
            ContainerId deck = r.T.SetupOf(r.A).ControllerDeckId, hand = r.T.SetupOf(r.A).HandContainerId;
            r.Deliver(r.Host.Apply(() => new DrawCardsUseCase().Execute(r.T.Match, new DrawCardsCommand(r.T.Context(r.A), deck, hand, 1))));
            ContainerState handState = r.T.Match.Containers[hand];
            TabletopObjectId card = handState.GetObjectAt(handState.Count - 1);
            ContainerTransferService transfer = new ContainerTransferService();
            r.Deliver(r.Host.Apply(() =>
            {
                Assert.IsTrue(transfer.RemoveFromContainer(r.T.Match.Cards[card].BaseState, r.T.Match.Containers[hand]).Succeeded);
                r.T.Match.Cards[card].SetFace(CardFace.FaceUp);
            }));
            Assert.IsTrue(r.Tables[r.B].Cards.ContainsKey(card), "loose and face up: public");

            Dictionary<PlayerId, byte[]> back = r.Host.Apply(() =>
                Assert.IsTrue(transfer.PlaceIntoContainer(r.T.Match.Cards[card].BaseState, r.T.Match.Containers[hand]).Succeeded));
            PrivateUpdate onB = RecipientEventApplier.Read(back[r.B]);
            Assert.IsFalse(onB.Changes.NeedsPackage, "a change set, not the whole package");
            Assert.AreEqual(1, onB.Changes.Removed.Count);
            Assert.IsTrue(onB.Changes.Removed[0].Reference.IsEmpty, "named by its place among the loose cards, never by its real id");
            Assert.IsFalse(Carries(back[r.B], card.Value.ToByteArray()), "the hidden card's real id is not in B's bytes");
            Dictionary<PlayerId, PrivateChangeSummary> summaries = r.Deliver(back);
            Assert.IsFalse(r.Tables[r.B].Cards.ContainsKey(card), "hidden from B now");
            Assert.AreEqual(card, summaries[r.B].Withdrawn[0], "B knows which of its views it was");
            Assert.AreEqual(PrivateRequestVerdict.HiddenRealId,
                PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.B, new PrivateOperation { Kind = PrivateOperationKind.Flip, RequestId = Guid.NewGuid(), Card = card, FaceUp = true }, out _),
                "the id it was public by authorizes nothing for B any more");
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void Shuffle_RenewsEveryHandle_SoNoCardCanBeFollowedThroughIt()
        {
            Room r = Room.Open();
            ContainerId deck = r.T.SetupOf(r.A).ControllerDeckId;
            HashSet<TabletopObjectId> beforeOnB = Refs(r.Tables[r.B], deck);
            HashSet<TabletopObjectId> beforeOnA = Refs(r.Tables[r.A], deck);
            Assert.Greater(beforeOnB.Count, 2);
            Dictionary<PlayerId, byte[]> updates = r.Host.Apply(() =>
            {
                List<TabletopObjectId> order = new List<TabletopObjectId>(r.T.Match.Containers[deck].ObjectIds);
                order.Reverse(); // any new order (the host's own random source in the game)
                r.T.Match.Containers[deck].ReplaceOrder(order);
            });
            AssertNoHiddenIds(r, updates);
            foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
                Assert.AreEqual(0, RecipientEventApplier.Read(pair.Value).Changes.Orders.FindAll(o => o.Container == deck).Count, "the order of a deck is never sent");
            r.Deliver(updates);
            HashSet<TabletopObjectId> afterOnB = Refs(r.Tables[r.B], deck);
            Assert.AreEqual(beforeOnB.Count, afterOnB.Count, "the count stays");
            afterOnB.IntersectWith(beforeOnB);
            Assert.AreEqual(0, afterOnB.Count, "no handle survives the shuffle (B)");
            HashSet<TabletopObjectId> afterOnA = Refs(r.Tables[r.A], deck);
            afterOnA.IntersectWith(beforeOnA);
            Assert.AreEqual(0, afterOnA.Count, "no handle survives the shuffle (A)");
            foreach (TabletopObjectId old in beforeOnB)
                Assert.AreEqual(ReferenceResolution.Unknown, r.Host.Resolve(r.B, old, out _), "an old deck handle reaches nothing");
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void PawnPose_ChangedOutsideARequest_ReachesEveryoneByASweep()
        {
            Room r = Room.Open();
            Assert.IsNull(r.Host.Sweep(), "nothing changed: no revision");
            Assume.That(r.T.Match.Pawns.Count > 0, "the test table has pawns");
            TabletopObjectId pawn = default;
            foreach (TabletopObjectId id in r.T.Match.Pawns.Keys) { pawn = id; break; }
            TabletopObjectState state = r.T.Match.Pawns[pawn].BaseState;
            TabletopPose moved = new TabletopPose(new TableCoordinate(state.Pose.Position.X + 0.25, state.Pose.Position.Y), state.Pose.RotationDegrees, state.Pose.Layer, state.Pose.LocalOrder);
            state.SetPose(moved);
            Dictionary<PlayerId, PrivateChangeSummary> summaries = r.Deliver(r.Host.Sweep());
            foreach (PlayerId p in r.Host.Recipients)
            {
                Assert.AreEqual(moved.Position.X, r.Tables[p].Pawns[pawn].BaseState.Pose.Position.X, 1e-9);
                Assert.AreEqual(pawn, summaries[p].Moved[0]);
            }
        }

        [Test]
        public void ConsoleTurnedOver_CannotBeToldAsObjects_SoEachRecipientGetsItsPackage()
        {
            Room r = Room.Open();
            Core.Domain.Seats.SeatState seat = r.T.Match.Seats[r.T.SetupOf(r.A).SeatId];
            Assume.That(seat.Console != null);
            seat.Console.SetShowsBack(!seat.Console.ShowsBack);
            int before = r.Host.FallbackCount;
            Dictionary<PlayerId, byte[]> updates = r.Host.Sweep();
            Assert.IsNotNull(updates);
            foreach (PlayerId p in r.Host.Recipients)
            {
                Assert.IsTrue(r.Host.NeedPackage.Contains(p));
                Assert.IsTrue(RecipientEventApplier.Read(updates[p]).Changes.NeedsPackage);
            }

            Assert.Greater(r.Host.FallbackCount, before, "fallbacks are counted");
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void UndoStyleReplacement_KeepsHandlesAndRevision_AndHidesTheUndoneDraw()
        {
            Room r = Room.Open();
            GameTemplateInitialSnapshot saved = GameTemplateInitialSnapshot.Capture(r.T.Match);
            ContainerId deck = r.T.SetupOf(r.A).ControllerDeckId, hand = r.T.SetupOf(r.A).HandContainerId;
            r.Deliver(r.Host.Apply(() => new DrawCardsUseCase().Execute(r.T.Match, new DrawCardsCommand(r.T.Context(r.A), deck, hand, 2))));
            ContainerState handState = r.T.Match.Containers[hand];
            TabletopObjectId drawn = handState.GetObjectAt(handState.Count - 1);
            Assert.IsTrue(r.Tables[r.A].Cards.ContainsKey(drawn), "A saw its draw");

            MatchState restored = saved.Restore(r.T.Match.Revision + 1);
            r.Host.Rebind(restored);
            long revision = r.Host.Revision;
            Dictionary<PlayerId, byte[]> updates = r.Host.Sweep();
            Assert.AreEqual(revision + 1, r.Host.Revision, "the Undo is one revision, not a new session");
            foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
                Assert.IsFalse(Carries(pair.Value, drawn.Value.ToByteArray()), "the undone draw is named by place, never by its real id");
            r.Deliver(updates);
            Assert.IsFalse(r.Tables[r.A].Cards.ContainsKey(drawn), "back in the deck: hidden from A again");
            Assert.AreEqual(restored.Containers[hand].Count, r.Tables[r.B].Containers[hand].Count);
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void Policy_SeatPilesNamedByTheLayout_SharedDeckStaysUsable_AnotherSeatsOwnPileIsRefused()
        {
            // Live P6B check: "Shuffle ANOTHER seat's deck" was accepted. Trap Floor has one Controller deck that every
            // seat's layout names, so that is right; a pile the layout names for another seat only is refused.
            Room r = Room.Open();
            RecipientPublicLayout layout = new RecipientPublicLayout();
            foreach (PlayerId p in r.T.Players)
                layout.Seats.Add(new RecipientSeatLayout { Seat = r.T.SetupOf(p).SeatId, Hand = r.T.SetupOf(p).HandContainerId, ControllerDeck = r.T.SetupOf(p).ControllerDeckId });
            r.Host.Layout = layout;
            ContainerId deck = r.T.SetupOf(r.A).ControllerDeckId, handB = r.T.SetupOf(r.B).HandContainerId;
            Assert.AreEqual(deck, r.T.SetupOf(r.B).ControllerDeckId, "Trap Floor's Controller deck is shared");
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.B, new PrivateOperation { Kind = PrivateOperationKind.Shuffle, RequestId = Guid.NewGuid(), Container = deck }, out _),
                "the shared deck stays usable by every seat");
            Assert.AreEqual(PrivateRequestVerdict.Accepted,
                PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.B, new PrivateOperation { Kind = PrivateOperationKind.DrawCount, RequestId = Guid.NewGuid(), Container = deck, Destination = handB, A = 1 }, out _));

            SeatId seatA = r.T.SetupOf(r.A).SeatId, seatB = r.T.SetupOf(r.B).SeatId;
            ContainerId onlyA = new ContainerId(Guid.NewGuid());
            RecipientPublicLayout own = new RecipientPublicLayout();
            own.Seats.Add(new RecipientSeatLayout { Seat = seatA, ActionArea = onlyA });
            own.Seats.Add(new RecipientSeatLayout { Seat = seatB, ActionArea = new ContainerId(Guid.NewGuid()) });
            Assert.IsTrue(PrivateOperationPolicy.IsOtherSeatsPile(own, seatB, onlyA), "another seat's own pile");
            Assert.IsFalse(PrivateOperationPolicy.IsOtherSeatsPile(own, seatA, onlyA), "its own pile");
            Assert.IsFalse(PrivateOperationPolicy.IsOtherSeatsPile(layout, seatB, deck), "a pile shared by every seat");
        }

        [Test]
        public void Policy_SameRulesForEverySeat_RefusesCrossSeatStaleHiddenAndDuplicate()
        {
            Room r = Room.Open();
            ContainerId deckA = r.T.SetupOf(r.A).ControllerDeckId, handA = r.T.SetupOf(r.A).HandContainerId, handB = r.T.SetupOf(r.B).HandContainerId;
            r.Deliver(r.Host.Apply(() => new DrawCardsUseCase().Execute(r.T.Match, new DrawCardsCommand(r.T.Context(r.A), deckA, handA, 2))));
            TabletopObjectId aCard = r.T.Match.Containers[handA].GetObjectAt(0);
            PrivateOperation Op(PrivateOperationKind kind) => new PrivateOperation { Kind = kind, RequestId = Guid.NewGuid() };

            PrivateOperation toB = Op(PrivateOperationKind.Transfer);
            toB.Card = aCard; toB.Container = handA; toB.Destination = handB;
            Assert.AreEqual(PrivateRequestVerdict.InvalidDestination, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.A, toB, out _), "not into another seat's hand");

            PrivateOperation take = Op(PrivateOperationKind.Transfer);
            take.Card = aCard; take.Container = handA; take.Destination = r.Pile();
            Assert.AreEqual(PrivateRequestVerdict.HiddenRealId, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.B, take, out _), "B by A's real id");
            PrivateOperation byHandle = Op(PrivateOperationKind.Transfer);
            byHandle.Card = r.Tables[r.B].Containers[handA].GetObjectAt(0); byHandle.Container = handA; byHandle.Destination = r.Pile();
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.B, byHandle, out _), "B's handle for a card in A's hand");

            PrivateOperation stale = Op(PrivateOperationKind.Transfer);
            stale.Card = aCard; stale.Container = deckA; stale.Destination = r.Pile();
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.A, stale, out _), "the card has moved since");

            PrivateOperation forged = Op(PrivateOperationKind.Flip);
            forged.Card = new TabletopObjectId(Guid.NewGuid());
            Assert.AreEqual(PrivateRequestVerdict.UnknownReference, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.A, forged, out _));

            PrivateOperation drawIntoB = Op(PrivateOperationKind.DrawCount);
            drawIntoB.Container = deckA; drawIntoB.Destination = handB; drawIntoB.A = 1;
            Assert.AreNotEqual(PrivateRequestVerdict.Accepted, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.A, drawIntoB, out _), "draws go into one's own hand");

            PrivateOperation reorderDeck = Op(PrivateOperationKind.Reorder);
            reorderDeck.Container = deckA; reorderDeck.Card = r.Tables[r.A].Containers[deckA].GetObjectAt(0); reorderDeck.A = 0; reorderDeck.B = 1;
            Assert.AreNotEqual(PrivateRequestVerdict.Accepted, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.A, reorderDeck, out _), "no reorder of an order one may not see");

            PrivateOperation reorder = Op(PrivateOperationKind.Reorder);
            reorder.Container = handA; reorder.Card = aCard; reorder.A = 0; reorder.B = 1;
            Assert.AreEqual(PrivateRequestVerdict.Accepted, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.A, reorder, out PrivateResolvedOperation resolved));
            Assert.AreEqual(aCard, resolved.RealCard);
            Assert.AreEqual(PrivateRequestVerdict.Duplicate, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, r.A, reorder, out _), "a request id runs once");

            PrivateOperation shuffle = Op(PrivateOperationKind.Shuffle);
            shuffle.Container = deckA;
            r.S.AnyTime = false; // Full
            PlayerId notActive = r.T.Turn.ActivePlayerId == r.A ? r.B : r.A;
            Assert.AreEqual(PrivateRequestVerdict.NotYourTurn, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, notActive, shuffle, out _), "Full: the game's turn");
            Assert.AreEqual(PrivateRequestVerdict.NotSeated, PrivateOperationPolicy.Check(r.T.Match, r.S, r.Host, RecipientSnapshotProjection.Spectator, Op(PrivateOperationKind.Shuffle), out _));
        }

        [Test]
        public void OperationCodec_RoundTrips_AndRefusesMalformedBytes()
        {
            PrivateOperation op = new PrivateOperation
            {
                Kind = PrivateOperationKind.Transfer, RequestId = Guid.NewGuid(), Card = new TabletopObjectId(Guid.NewGuid()),
                Container = new ContainerId(Guid.NewGuid()), HasPose = true, Pose = new TabletopPose(new TableCoordinate(1.5, -2.25), 90f, 1, 2)
            };
            byte[] bytes = PrivateOperationCodec.Write(op);
            Assert.IsTrue(PrivateOperationCodec.TryRead(bytes, 0, out PrivateOperation read));
            Assert.AreEqual(op.Card, read.Card);
            Assert.AreEqual(1.5, read.Pose.Position.X);
            Assert.AreEqual(90f, read.Pose.RotationDegrees);
            Assert.IsFalse(PrivateRequestCodec.TryRead(bytes, 0, out _, out _, out _, out _), "not a P3–P5 request");

            byte[] shortened = new byte[bytes.Length - 1];
            Array.Copy(bytes, shortened, shortened.Length);
            Assert.IsFalse(PrivateOperationCodec.TryRead(shortened, 0, out _), "length");
            byte[] both = (byte[])bytes.Clone();
            new ContainerId(Guid.NewGuid()).Value.ToByteArray().CopyTo(both, 1 + 1 + 16 + 16 + 16); // a destination AND a pose
            Assert.IsFalse(PrivateOperationCodec.TryRead(both, 0, out _), "shape");
            byte[] noId = (byte[])bytes.Clone();
            for (int i = 2; i < 18; i++) noId[i] = 0;
            Assert.IsFalse(PrivateOperationCodec.TryRead(noId, 0, out _), "request id");
            byte[] nan = (byte[])bytes.Clone();
            BitConverter.GetBytes(double.NaN).CopyTo(nan, 1 + 1 + 16 * 4 + 1);
            Assert.IsFalse(PrivateOperationCodec.TryRead(nan, 0, out _), "pose");
            PrivateOperation draw = new PrivateOperation { Kind = PrivateOperationKind.DrawCount, RequestId = Guid.NewGuid(), Container = new ContainerId(Guid.NewGuid()), Destination = new ContainerId(Guid.NewGuid()), A = 500 };
            Assert.IsFalse(PrivateOperationCodec.TryRead(PrivateOperationCodec.Write(draw), 0, out _), "a draw of 500");
        }
    }
}
