using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
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
    /// PRIVACY (s141, doc 11 v2): adversarial checks on the BYTES a recipient would receive (TableWire, the codec the
    /// network uses), not on what the scene draws. The projection is not wired to the network yet (doc 10 §39).
    /// </summary>
    public sealed class PrivacyProjectionTests
    {
        private static readonly byte[] Key = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };

        private static byte[] Wire(GameTemplateInitialSnapshot snapshot) => TableWire.Write(snapshot, false);

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

        /// <summary>Asserts the recipient's bytes name no hidden card: neither its real id nor a definition only hidden cards have.</summary>
        private static void AssertNothingHiddenOnTheWire(MatchState match, PlayerId recipient, string who)
        {
            byte[] bytes = Wire(RecipientSnapshotProjection.Project(match, recipient, Key));
            HashSet<Guid> knownDefinitions = new HashSet<Guid>();
            List<CardInstanceState> hidden = new List<CardInstanceState>();
            foreach (CardInstanceState card in match.Cards.Values)
            {
                if (RecipientSnapshotProjection.MayKnowIdentity(match, card, recipient)) knownDefinitions.Add(card.BaseState.DefinitionId.Value);
                else hidden.Add(card);
            }

            foreach (PawnState pawn in match.Pawns.Values) knownDefinitions.Add(pawn.BaseState.DefinitionId.Value);
            foreach (TokenState token in match.Tokens.Values) knownDefinitions.Add(token.BaseState.DefinitionId.Value);
            Assert.Greater(hidden.Count, 0, who + ": the test table hides nothing, so it proves nothing");
            foreach (CardInstanceState card in hidden)
            {
                Assert.IsFalse(Carries(bytes, card.BaseState.Id.Value), $"{who} receives the real id of hidden card {card.BaseState.Id}");
                if (!knownDefinitions.Contains(card.BaseState.DefinitionId.Value))
                    Assert.IsFalse(Carries(bytes, card.BaseState.DefinitionId.Value), $"{who} receives the identity of hidden card {card.BaseState.Id}");
            }
        }

        private static void MoveToHand(TrapFloorTestTable t, PlayerId player, int count)
        {
            ContainerState deck = t.Match.Containers[t.SetupOf(player).ControllerDeckId];
            ContainerState hand = t.Match.Containers[t.SetupOf(player).HandContainerId];
            ContainerTransferService transfer = new ContainerTransferService();
            for (int i = 0; i < count; i++)
            {
                TabletopObjectId top = deck.GetObjectAt(deck.TopIndex);
                Assert.IsTrue(transfer.MoveBetweenContainers(t.Match.Cards[top].BaseState, deck, hand).Succeeded, "draw into the hand");
            }
        }

        // ---------- baseline: what the live path sends today ----------

        [Test]
        public void Baseline_TheCurrentFullSnapshot_CarriesEveryFaceDownTileIdentity()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3);
            byte[] full = Wire(GameTemplateInitialSnapshot.Capture(t.Match));
            int faceDown = 0;
            foreach (TabletopObjectId tile in t.Template.FloorCardIds.Values)
            {
                CardInstanceState card = t.Match.Cards[tile];
                if (card.Face != CardFace.FaceDown) continue;
                faceDown++;
                Assert.IsTrue(Carries(full, card.BaseState.DefinitionId.Value),
                    "BASELINE: the unfiltered snapshot (sent today to every computer) names each face-down tile's content");
            }

            Assert.Greater(faceDown, 0);
        }

        // ---------- the projection (slice P1) ----------

        [Test]
        public void Projection_FaceDownTilesAndDecks_AreHiddenFromEverySeatAndSpectators()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3);
            for (int i = 0; i < t.Players.Length; i++) AssertNothingHiddenOnTheWire(t.Match, t.Players[i], "player " + (i + 1));
            AssertNothingHiddenOnTheWire(t.Match, RecipientSnapshotProjection.Spectator, "a spectator");
        }

        [Test]
        public void Projection_AHand_IsReadableByItsOwnerOnly()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3);
            PlayerId a = t.Players[0], b = t.Players[1];
            MoveToHand(t, a, 2);
            ContainerState handA = t.Match.Containers[t.SetupOf(a).HandContainerId];
            byte[] toA = Wire(RecipientSnapshotProjection.Project(t.Match, a, Key));
            foreach (TabletopObjectId id in handA.ObjectIds)
            {
                Assert.IsTrue(Carries(toA, id.Value), "A reads A's own hand");
                Assert.IsTrue(Carries(toA, t.Match.Cards[id].BaseState.DefinitionId.Value), "A reads the faces in A's hand");
            }

            AssertNothingHiddenOnTheWire(t.Match, b, "B (A holds two cards)");
            AssertNothingHiddenOnTheWire(t.Match, RecipientSnapshotProjection.Spectator, "a spectator (A holds two cards)");
            MoveToHand(t, b, 1);
            AssertNothingHiddenOnTheWire(t.Match, a, "A (B holds a card)");

            MatchState seenByB = RecipientSnapshotProjection.Project(t.Match, b, Key).Restore();
            Assert.AreEqual(handA.Count, seenByB.Containers[handA.Id].Count, "B still sees how many cards A holds (the backs)");
            foreach (TabletopObjectId id in seenByB.Containers[handA.Id].ObjectIds)
                Assert.AreEqual(RecipientSnapshotProjection.HiddenDefinition, seenByB.Cards[id].BaseState.DefinitionId, "a back only");
        }

        [Test]
        public void Projection_DeckOrder_CannotBeReadBack()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            PlayerId a = t.Players[0];
            ContainerId deckId = t.SetupOf(a).ControllerDeckId;
            ContainerState deck = t.Match.Containers[deckId];
            Assert.Greater(deck.Count, 2);
            List<TabletopObjectId> before = new List<TabletopObjectId>(
                RecipientSnapshotProjection.Project(t.Match, a, Key).Restore().Containers[deckId].ObjectIds);

            List<TabletopObjectId> reversed = new List<TabletopObjectId>(deck.ObjectIds);
            reversed.Reverse();
            deck.ReplaceOrder(reversed); // the authority's deck is in a different order now
            List<TabletopObjectId> after = new List<TabletopObjectId>(
                RecipientSnapshotProjection.Project(t.Match, a, Key).Restore().Containers[deckId].ObjectIds);

            CollectionAssert.AreEqual(before, after, "the projected deck is the same whatever the real order");
            Assert.AreEqual(deck.Count, after.Count, "the count is public");
        }

        [Test]
        public void Projection_CarriesNoCommandHistory_SoNoShuffleSeedCanBeReplayed()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            CommandId earlier = CommandId.New();
            t.Match.RecordPhysicalCommand(earlier);
            Assert.IsTrue(Carries(Wire(GameTemplateInitialSnapshot.Capture(t.Match)), earlier.Value), "BASELINE: the full snapshot carries it");
            Assert.IsFalse(Carries(Wire(RecipientSnapshotProjection.Project(t.Match, t.Players[0], Key)), earlier.Value),
                "a command id seeds that command's random numbers (CommandScopedIdentitySource.SeedOf)");
        }

        [Test]
        public void Projection_ALegitimateReveal_DeliversThePublicFace()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2);
            TabletopObjectId tile = default;
            foreach (TabletopObjectId id in t.Template.FloorCardIds.Values)
                if (t.Match.Cards[id].Face == CardFace.FaceDown) { tile = id; break; }
            Assert.IsFalse(tile.IsEmpty);
            CardInstanceState card = t.Match.Cards[tile];
            Assert.IsFalse(Carries(Wire(RecipientSnapshotProjection.Project(t.Match, t.Players[1], Key)), tile.Value));

            card.SetFace(CardFace.FaceUp); // the tile is turned over on the authority's table
            byte[] after = Wire(RecipientSnapshotProjection.Project(t.Match, t.Players[1], Key));
            Assert.IsTrue(Carries(after, tile.Value), "the revealed tile is named by its real id");
            Assert.IsTrue(Carries(after, card.BaseState.DefinitionId.Value), "and shows its real content");
        }

        [Test]
        public void Projection_RestoresToAPlayableTable_WithStableOpaqueReferences()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3);
            PlayerId a = t.Players[0], b = t.Players[1];
            GameTemplateInitialSnapshot first = RecipientSnapshotProjection.Project(t.Match, a, Key);
            MatchState restored = first.Restore();
            Assert.AreEqual(t.Match.ObjectCount, restored.ObjectCount, "every object is still there (backs included)");
            Assert.AreEqual(t.Match.Containers.Count, restored.Containers.Count);

            CollectionAssert.AreEquivalent(
                new List<TabletopObjectId>(restored.Cards.Keys),
                new List<TabletopObjectId>(RecipientSnapshotProjection.Project(t.Match, a, Key).Restore().Cards.Keys),
                "the same recipient gets the same references each time, so it can point at a back to act on it");

            HashSet<TabletopObjectId> forB = new HashSet<TabletopObjectId>(RecipientSnapshotProjection.Project(t.Match, b, Key).Restore().Cards.Keys);
            HashSet<TabletopObjectId> withOtherKey = new HashSet<TabletopObjectId>(
                RecipientSnapshotProjection.Project(t.Match, a, RecipientSnapshotProjection.NewSessionKey()).Restore().Cards.Keys);
            int sharedHidden = 0, sameUnderOtherKey = 0;
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in restored.Cards)
            {
                if (!pair.Value.BaseState.DefinitionId.Equals(RecipientSnapshotProjection.HiddenDefinition)) continue;
                if (forB.Contains(pair.Key)) sharedHidden++;
                if (withOtherKey.Contains(pair.Key)) sameUnderOtherKey++;
            }

            Assert.AreEqual(0, sharedHidden, "two players cannot match up their references to the same hidden card");
            Assert.AreEqual(0, sameUnderOtherKey, "the references depend on the host's secret key");
        }
    }
}
