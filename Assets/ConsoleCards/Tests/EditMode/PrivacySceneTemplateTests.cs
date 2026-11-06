using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.GameTemplates.Privacy;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// PRIVACY-SCENE-2 (s147, doc 10 §56): the public table template a seed-free client builds its tabletop from, and
    /// the deterministic fixture its scene is checked with (nonempty hands, Console cards face up and face down, loose
    /// cards, one revealed tile). Checked on the package bytes and on the table the client builds from them.
    /// </summary>
    public sealed class PrivacySceneTemplateTests
    {
        private static readonly byte[] Key = { 3, 1, 4, 1, 5, 9, 2, 6, 5, 3, 5, 8, 9, 7, 9, 3, 2, 3, 8, 4 };
        private const int Seed = 0x5EED4321;

        private static byte[] Wire(object value) => TableWire.Write(value, false);

        private static bool Carries(byte[] haystack, Guid value)
        {
            byte[] needle = value.ToByteArray();
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }

            return false;
        }

        private static RecipientPublicLayout LayoutOf(TrapFloorTestTable t)
        {
            RecipientPublicLayout layout = new RecipientPublicLayout();
            foreach (TrapFloorPlayerSetupDefinition player in t.Template.Players)
                layout.Seats.Add(new RecipientSeatLayout
                {
                    LayoutSeatIndex = player.LayoutSeatIndex,
                    Seat = player.SeatId,
                    Hand = player.HandContainerId,
                    ControllerDeck = player.ControllerDeckId,
                    ActionArea = player.ActionAbilityAreaContainerId,
                    Pawn = player.PawnId,
                });
            return layout;
        }

        private static (TrapFloorTestTable table, RecipientPublicLayout layout, RecipientViewFixture.Result fixture) Fixture(int players = 3)
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(players, Seed);
            RecipientPublicLayout layout = LayoutOf(t);
            RecipientViewFixture.Result fixture = RecipientViewFixture.Apply(t.Match, layout.Seats, t.Template.FloorCardIds.Values);
            return (t, layout, fixture);
        }

        private static RecipientClientTable ClientOf(TrapFloorTestTable t, RecipientPublicLayout layout, PlayerId recipient, out byte[] bytes)
        {
            bytes = Wire(RecipientBootstrapPackage.Build(t.Match, new RecipientHandleTable(Key), recipient, t.Template.CardLabels, layout));
            return RecipientClientTable.Build((RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false));
        }

        private static bool IsHidden(RecipientClientTable client, TabletopObjectId id) =>
            !client.Table.Cards.TryGetValue(id, out CardInstanceState card)
            || card.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition);

        [Test]
        public void Fixture_GivesTheSceneEverythingItMustShow_AndIsTheSameEachTime()
        {
            var (t, layout, fixture) = Fixture();
            Assert.AreEqual(3 * 3, fixture.HandCards, "three cards in each seat's hand");
            Assert.AreEqual(3, fixture.ConsoleFaceUp, "a public card in each Console");
            Assert.AreEqual(3, fixture.ConsoleFaceDown, "a face-down card in each Console");
            Assert.IsFalse(fixture.LooseFaceUp.IsEmpty, "a face-up card on the table");
            Assert.IsFalse(fixture.LooseFaceDown.IsEmpty, "a face-down card on the table");
            Assert.IsFalse(fixture.RevealedTile.IsEmpty, "a revealed tile");
            Assert.Greater(fixture.UnrevealedTiles, 1, "several unrevealed tiles");
            CollectionAssert.IsEmpty(fixture.Notes);

            var (again, _, fixtureAgain) = Fixture();
            Assert.AreEqual(fixture.RevealedTile, fixtureAgain.RevealedTile);
            Assert.AreEqual(fixture.LooseFaceUp, fixtureAgain.LooseFaceUp);
            foreach (KeyValuePair<ContainerId, ContainerState> pair in t.Match.Containers)
                CollectionAssert.AreEqual(pair.Value.ObjectIds, again.Match.Containers[pair.Key].ObjectIds, "same table, same fixture: " + pair.Key);
        }

        [Test]
        public void EachRecipient_SeesItsOwnHand_OnlyBacksOfOthers_AndTheSamePublicTable()
        {
            var (t, layout, fixture) = Fixture();
            PlayerId[] recipients = { t.Players[0], t.Players[1], t.Players[2], RecipientSnapshotProjection.Spectator };
            Dictionary<TabletopObjectId, ObjectDefinitionId> publicSeenBy0 = null;
            foreach (PlayerId recipient in recipients)
            {
                RecipientClientTable client = ClientOf(t, layout, recipient, out byte[] bytes);
                Assert.IsNull(RecipientPublicLayout.Check(client.Layout, client.Table), "the template matches the table");
                Assert.AreEqual(layout.Seats.Count, client.Layout.Seats.Count);

                foreach (PlayerId owner in t.Players)
                {
                    ContainerState hand = t.Match.Containers[t.SetupOf(owner).HandContainerId];
                    Assert.GreaterOrEqual(hand.Count, 3, "the fixture filled the hand");
                    Assert.AreEqual(hand.Count, client.Counts[hand.Id], "every recipient sees the count of every hand");
                    foreach (TabletopObjectId real in hand.ObjectIds)
                    {
                        if (owner == recipient)
                        {
                            Assert.IsFalse(IsHidden(client, real), "a player sees their own hand");
                            Assert.AreEqual(t.Match.Cards[real].BaseState.DefinitionId, client.Table.Cards[real].BaseState.DefinitionId);
                        }
                        else
                        {
                            Assert.IsTrue(IsHidden(client, real), "another player's hand is backs only");
                            Assert.IsFalse(Carries(bytes, real.Value), "another player's hand card id is not on the wire");
                        }
                    }

                    if (owner == recipient)
                    {
                        List<TabletopObjectId> order = new List<TabletopObjectId>(client.Table.Containers[hand.Id].ObjectIds);
                        CollectionAssert.AreEqual(hand.ObjectIds, order, "the own hand keeps its order");
                    }

                    Assert.IsTrue(t.Match.TryGetSeatConsole(t.SetupOf(owner).SeatId, out ConsoleState console));
                    foreach (ContainerId slot in console.SlotContainerIds)
                    {
                        foreach (TabletopObjectId real in t.Match.Containers[slot].ObjectIds)
                        {
                            bool faceDown = t.Match.Cards[real].Face == CardFace.FaceDown;
                            Assert.AreEqual(faceDown, IsHidden(client, real),
                                faceDown ? "a face-down Console card is hidden from everyone, its owner too" : "a face-up Console card is public");
                        }
                    }
                }

                Assert.IsFalse(IsHidden(client, fixture.LooseFaceUp), "a face-up table card is public");
                Assert.IsTrue(IsHidden(client, fixture.LooseFaceDown), "a face-down table card is hidden");
                Assert.IsFalse(IsHidden(client, fixture.RevealedTile), "the revealed tile is public");
                Assert.AreEqual(t.Match.Cards[fixture.RevealedTile].BaseState.DefinitionId, client.Table.Cards[fixture.RevealedTile].BaseState.DefinitionId);
                foreach (TabletopObjectId tile in t.Template.FloorCardIds.Values)
                    if (t.Match.Cards[tile].Face == CardFace.FaceDown) Assert.IsFalse(Carries(bytes, tile.Value), "an unrevealed tile id is not on the wire");

                // The public table is the same for everyone: every card that is public to all has the same identity.
                Dictionary<TabletopObjectId, ObjectDefinitionId> publicCards = new Dictionary<TabletopObjectId, ObjectDefinitionId>();
                foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in client.Table.Cards)
                    if (!IsHidden(client, pair.Key) && !IsInAHand(t, pair.Key)) publicCards[pair.Key] = pair.Value.BaseState.DefinitionId;
                if (publicSeenBy0 == null) publicSeenBy0 = publicCards;
                else CollectionAssert.AreEquivalent(publicSeenBy0, publicCards, "the same public tabletop for every recipient");
            }
        }

        [Test]
        public void PublicGeometry_OfUnrevealedTiles_SaysNothingButWhereTheyLie()
        {
            var (t, layout, _) = Fixture();
            foreach (PlayerId recipient in new[] { t.Players[0], RecipientSnapshotProjection.Spectator })
            {
                RecipientClientTable client = ClientOf(t, layout, recipient, out _);
                HashSet<string> fingerprints = new HashSet<string>();
                int unrevealed = 0;
                foreach (TabletopObjectId real in t.Template.FloorCardIds.Values)
                {
                    if (t.Match.Cards[real].Face != CardFace.FaceDown) continue;
                    unrevealed++;
                    TabletopObjectId shown = HandleAt(client, t.Match.Cards[real].BaseState);
                    TabletopObjectState s = client.Table.Cards[shown].BaseState;
                    // Everything but the place. (Pose.LocalOrder is the cell's row-major number, a function of the place:
                    // checked against a differently dealt floor below.)
                    fingerprints.Add($"{s.Kind}|{s.DefinitionId.Value}|{s.Pose.RotationDegrees}|{s.Pose.Layer}|{s.OwnerPlayerId.Value}|{s.Visibility}|{s.IsUserLocked}|{s.PhysicalRevision}|{s.PhysicalState == null}|{s.ContainerId.Value}|{client.Table.Cards[shown].Face}");
                }

                Assert.Greater(unrevealed, 1);
                Assert.AreEqual(1, fingerprints.Count, "every unrevealed tile looks the same apart from its place: " + string.Join(" / ", fingerprints));
            }
        }

        [Test]
        public void PublicGeometry_IsTheSame_ForAFloorDealtDifferently()
        {
            var (t, layout, _) = Fixture();
            TrapFloorTestTable other = TrapFloorTestTable.Create(3, Seed + 1);
            RecipientClientTable one = ClientOf(t, layout, RecipientSnapshotProjection.Spectator, out _);
            RecipientClientTable two = ClientOf(other, LayoutOf(other), RecipientSnapshotProjection.Spectator, out _);
            int compared = 0, differentContent = 0;
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> cell in t.Template.FloorCardIds)
            {
                if (!other.Template.FloorCardIds.TryGetValue(cell.Key, out TabletopObjectId otherId)) continue;
                CardInstanceState a = t.Match.Cards[cell.Value], b = other.Match.Cards[otherId];
                if (a.Face != CardFace.FaceDown || b.Face != CardFace.FaceDown) continue;
                compared++;
                if (!a.BaseState.DefinitionId.Equals(b.BaseState.DefinitionId)) differentContent++;
                TabletopObjectState shownA = one.Table.Cards[HandleAt(one, a.BaseState)].BaseState;
                TabletopObjectState shownB = two.Table.Cards[HandleAt(two, b.BaseState)].BaseState;
                Assert.AreEqual(shownA.Pose, shownB.Pose, "an unrevealed tile's whole pose (place, turn, layer, order) depends on its cell only: " + cell.Key);
                Assert.AreEqual(shownA.DefinitionId, shownB.DefinitionId);
            }

            Assert.Greater(compared, 1);
            Assert.Greater(differentContent, 0, "the two floors really are dealt differently");
        }

        [Test]
        public void PublicLayout_NamesNoCard_AndIsCheckedAgainstTheTable()
        {
            var (t, layout, _) = Fixture();
            byte[] layoutBytes = Wire(layout);
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in t.Match.Cards)
                Assert.IsFalse(Carries(layoutBytes, pair.Key.Value), "the table template names no card");
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in t.Match.Cards)
                Assert.IsFalse(Carries(layoutBytes, pair.Value.BaseState.DefinitionId.Value), "the table template names no card definition");

            RecipientPublicLayout forged = LayoutOf(t);
            forged.Seats[0].Seat = new SeatId(Guid.NewGuid());
            byte[] bytes = Wire(RecipientBootstrapPackage.Build(t.Match, new RecipientHandleTable(Key), t.Players[0], t.Template.CardLabels, forged));
            Assert.Throws<InvalidOperationException>(
                () => RecipientClientTable.Build((RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false)),
                "a template that names a seat the table does not have is refused");
        }

        private static bool IsInAHand(TrapFloorTestTable t, TabletopObjectId id)
        {
            ContainerId container = t.Match.Cards.TryGetValue(id, out CardInstanceState card) ? card.BaseState.ContainerId : ContainerId.Empty;
            return !container.IsEmpty && t.Match.Containers[container].Kind == ContainerKind.Hand;
        }

        // The client's card lying where the real tile lies (same container, same place).
        private static TabletopObjectId HandleAt(RecipientClientTable client, TabletopObjectState real)
        {
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in client.Table.Cards)
                if (pair.Value.BaseState.ContainerId == real.ContainerId && pair.Value.BaseState.Pose.Position.Equals(real.Pose.Position)) return pair.Key;
            Assert.Fail("no card at the tile's place");
            return TabletopObjectId.Empty;
        }
    }
}
