using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Authority;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Privacy;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// PRIVACY-P2 (s142, doc 11 §13): the isolated seed-free path, checked on the bytes a recipient would receive and on
    /// the table the client builds from them. Not wired to the live game.
    /// </summary>
    public sealed class PrivacyBootstrapTests
    {
        private static readonly byte[] Key = { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
        private const int Seed = 0x5EED1234; // distinctive, so its bytes can be searched for

        private static byte[] Wire(object value) => TableWire.Write(value, false);

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

        private static void MoveToHand(TrapFloorTestTable t, PlayerId player, int count)
        {
            ContainerState deck = t.Match.Containers[t.SetupOf(player).ControllerDeckId];
            ContainerState hand = t.Match.Containers[t.SetupOf(player).HandContainerId];
            ContainerTransferService transfer = new ContainerTransferService();
            for (int i = 0; i < count; i++)
                Assert.IsTrue(transfer.MoveBetweenContainers(t.Match.Cards[deck.GetObjectAt(deck.TopIndex)].BaseState, deck, hand).Succeeded);
        }

        private static RecipientBootstrapPackage Package(TrapFloorTestTable t, PlayerId recipient) =>
            RecipientBootstrapPackage.Build(t.Match, new RecipientHandleTable(Key), recipient, t.Template.CardLabels);

        private static List<TabletopObjectId> FaceDownTiles(TrapFloorTestTable t)
        {
            List<TabletopObjectId> tiles = new List<TabletopObjectId>();
            foreach (TabletopObjectId id in t.Template.FloorCardIds.Values)
                if (t.Match.Cards[id].Face == CardFace.FaceDown) tiles.Add(id);
            return tiles;
        }

        // ---------- the client builds its table from its package alone ----------

        [Test]
        public void SeedFreeClient_BuildsTheAuthorizedTable_FromItsPackageBytes()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3, Seed);
            PlayerId a = t.Players[0], b = t.Players[1];
            MoveToHand(t, a, 2);
            ContainerState handA = t.Match.Containers[t.SetupOf(a).HandContainerId];

            foreach (PlayerId recipient in new[] { a, b, RecipientSnapshotProjection.Spectator })
            {
                byte[] bytes = Wire(Package(t, recipient));
                Assert.IsFalse(Carries(bytes, BitConverter.GetBytes(Seed)), "no deal seed in the package");
                RecipientBootstrapPackage received = (RecipientBootstrapPackage)TableWire.Read(bytes, 0, bytes.Length, false);
                RecipientClientTable client = RecipientClientTable.Build(received); // also checks the host's digest

                Assert.AreEqual(t.Match.ObjectCount, client.Table.ObjectCount, "every piece is on the client's table, backs included");
                foreach (KeyValuePair<ContainerId, ContainerState> pair in t.Match.Containers)
                    Assert.AreEqual(pair.Value.Count, client.Counts[pair.Key], "public count of " + pair.Key);

                int ownFaces = 0, hiddenInA = 0;
                foreach (ClientCardView card in client.Cards)
                {
                    if (card.IsHidden) { Assert.AreEqual(string.Empty, card.Label, "a back has no name"); }
                    else if (t.Match.Cards.TryGetValue(card.Reference, out CardInstanceState real))
                    {
                        Assert.AreEqual(real.BaseState.DefinitionId, card.Definition, "a public card keeps its identity");
                        Assert.AreEqual(t.Template.CardLabels.TryGetValue(card.Reference, out string label) ? label : string.Empty, card.Label);
                    }
                    else Assert.Fail("a card shown with an identity must carry its real id");

                    if (card.Container == handA.Id) { if (card.IsHidden) hiddenInA++; else ownFaces++; }
                }

                if (recipient == a) Assert.AreEqual(2, ownFaces, "A sees A's hand");
                else Assert.AreEqual(2, hiddenInA, "others see two backs in A's hand");
                foreach (TabletopObjectId tile in FaceDownTiles(t)) Assert.IsFalse(Carries(bytes, tile.Value), "face-down tile id");
            }
        }

        [Test]
        public void SeedFreePackage_NamesNoHiddenTile()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3, Seed);
            HashSet<string> publicNames = new HashSet<string>();
            List<TabletopObjectId> tiles = FaceDownTiles(t);
            foreach (KeyValuePair<TabletopObjectId, string> pair in t.Template.CardLabels)
                if (!tiles.Contains(pair.Key) && t.Match.Cards.TryGetValue(pair.Key, out CardInstanceState c) && c.Face == CardFace.FaceUp) publicNames.Add(pair.Value);
            byte[] bytes = Wire(Package(t, t.Players[1]));
            int checkedNames = 0;
            foreach (TabletopObjectId tile in tiles)
            {
                string name = t.Template.CardLabels[tile];
                if (string.IsNullOrEmpty(name) || publicNames.Contains(name)) continue;
                checkedNames++;
                Assert.IsFalse(Carries(bytes, Encoding.UTF8.GetBytes(name)), "the package names hidden tile content: " + name);
            }

            Assert.Greater(checkedNames, 0);
        }

        // ---------- two tables differing only in hidden facts look the same ----------

        [Test]
        public void Indistinguishable_TablesThatDifferOnlyInHiddenDeckOrder()
        {
            TrapFloorTestTable one = TrapFloorTestTable.Create(3, Seed), two = TrapFloorTestTable.Create(3, Seed);
            HashSet<ContainerId> decks = new HashSet<ContainerId>(); // seats may share one Controller deck
            foreach (PlayerId p in two.Players) decks.Add(two.SetupOf(p).ControllerDeckId);
            foreach (ContainerId deckId in decks)
            {
                ContainerState deck = two.Match.Containers[deckId];
                List<TabletopObjectId> reversed = new List<TabletopObjectId>(deck.ObjectIds);
                reversed.Reverse();
                deck.ReplaceOrder(reversed);
            }

            foreach (PlayerId recipient in new[] { one.Players[1], RecipientSnapshotProjection.Spectator })
                CollectionAssert.AreEqual(Wire(Package(one, recipient)), Wire(Package(two, recipient)),
                    "the bytes must not tell the two deck orders apart");
        }

        [Test]
        public void Indistinguishable_TablesThatDifferOnlyInWhichFaceDownTileIsWhere()
        {
            TrapFloorTestTable one = TrapFloorTestTable.Create(3, Seed), two = TrapFloorTestTable.Create(3, Seed);
            List<TabletopObjectId> tiles = FaceDownTiles(two);
            TabletopObjectState x = two.Match.Cards[tiles[0]].BaseState, y = two.Match.Cards[tiles[tiles.Count - 1]].BaseState;
            Assert.AreNotEqual(two.Match.Cards[tiles[0]].BaseState.DefinitionId, two.Match.Cards[tiles[tiles.Count - 1]].BaseState.DefinitionId,
                "pick two tiles with different content");
            Assert.IsTrue(x.ContainerId.IsEmpty && y.ContainerId.IsEmpty, "floor tiles lie loose on the board");
            TabletopPose px = x.Pose; PhysicalObjectState sx = x.PhysicalState;
            x.SetPose(y.Pose);
            y.SetPose(px);
            if (sx != null && y.PhysicalState != null) { x.CorrectPhysicalState(y.PhysicalState); y.CorrectPhysicalState(sx); } // none before the scene runs

            PlayerId b = one.Players[1];
            CollectionAssert.AreEqual(Wire(Package(one, b)), Wire(Package(two, b)), "the bytes must not tell which content lies where");

            two.Match.Cards[tiles[0]].SetFace(CardFace.FaceUp); // a legal reveal: now the recipient may tell them apart
            byte[] revealed = Wire(Package(two, b));
            CollectionAssert.AreNotEqual(Wire(Package(one, b)), revealed);
            Assert.IsTrue(Carries(revealed, x.DefinitionId.Value), "the revealed tile shows its content");
        }

        // ---------- references a client may send back ----------

        [Test]
        public void Handles_AreWithdrawnByARevealOrAShuffle()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2, Seed);
            PlayerId a = t.Players[0];
            RecipientHandleTable handles = new RecipientHandleTable(Key);
            handles.Sync(t.Match, a);

            TabletopObjectId tile = FaceDownTiles(t)[0];
            TabletopObjectId tileHandle = handles.ReferenceFor(a, tile);
            Assert.AreNotEqual(tile, tileHandle);
            Assert.AreEqual(ReferenceResolution.Resolved, handles.Resolve(t.Match, a, tileHandle, out TabletopObjectId real));
            Assert.AreEqual(tile, real);
            t.Match.Cards[tile].SetFace(CardFace.FaceUp);
            Assert.AreEqual(ReferenceResolution.Unknown, handles.Resolve(t.Match, a, tileHandle, out _), "the old handle is stale after the reveal");
            Assert.AreEqual(ReferenceResolution.Resolved, handles.Resolve(t.Match, a, tile, out _), "the revealed tile goes by its real id");
            t.Match.Cards[tile].SetFace(CardFace.FaceDown);
            handles.Sync(t.Match, a);
            Assert.AreNotEqual(tileHandle, handles.ReferenceFor(a, tile), "hidden again: a fresh handle, so it cannot be followed");

            ContainerId deckId = t.SetupOf(a).ControllerDeckId;
            TabletopObjectId top = t.Match.Containers[deckId].GetObjectAt(0);
            TabletopObjectId before = handles.ReferenceFor(a, top);
            handles.Reshuffled(t.Match, deckId);
            Assert.AreEqual(ReferenceResolution.Unknown, handles.Resolve(t.Match, a, before, out _), "a handle from before the shuffle is stale");
            Assert.AreNotEqual(before, handles.ReferenceFor(a, top));
        }

        [Test]
        public void Handles_CannotBeForged_Borrowed_OrBypassedWithARealId()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3, Seed);
            PlayerId a = t.Players[0], b = t.Players[1];
            RecipientHandleTable handles = new RecipientHandleTable(Key);
            TabletopObjectId tile = FaceDownTiles(t)[0];
            handles.Sync(t.Match, a);
            handles.Sync(t.Match, b);
            TabletopObjectId handleOfA = handles.ReferenceFor(a, tile);

            Assert.AreEqual(ReferenceResolution.Unknown, handles.Resolve(t.Match, b, handleOfA, out _), "another recipient's handle");
            Assert.AreEqual(ReferenceResolution.Unknown, handles.Resolve(t.Match, a, new TabletopObjectId(Guid.NewGuid()), out _), "a forged handle");
            Assert.AreEqual(ReferenceResolution.HiddenRealId, handles.Resolve(t.Match, a, tile, out TabletopObjectId none), "a hidden card's real id");
            Assert.IsTrue(none.IsEmpty);

            HiddenCardRequestGate gate = new HiddenCardRequestGate();
            Guid request = Guid.NewGuid();
            Assert.IsTrue(gate.Accept(request));
            Assert.IsFalse(gate.Accept(request), "a repeated request runs once");
            Assert.IsFalse(gate.Accept(Guid.Empty));
        }

        // ---------- events instead of replayed commands ----------

        [Test]
        public void DrawEvent_ShowsTheFaceToTheOwnerOnly()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(3, Seed);
            PlayerId a = t.Players[0], b = t.Players[1];
            RecipientHandleTable handles = new RecipientHandleTable(Key);
            ContainerState deck = t.Match.Containers[t.SetupOf(a).ControllerDeckId];
            TabletopObjectId drawn = deck.GetObjectAt(deck.TopIndex);
            ObjectDefinitionId definition = t.Match.Cards[drawn].BaseState.DefinitionId;
            MoveToHand(t, a, 1);

            RecipientCardEvent toA = RecipientEventProjection.CardMoved(t.Match, handles, a, drawn, deck.Id);
            Assert.AreEqual(drawn, toA.Reference);
            Assert.AreEqual(definition, toA.Definition);

            foreach (PlayerId other in new[] { b, RecipientSnapshotProjection.Spectator })
            {
                RecipientCardEvent ev = RecipientEventProjection.CardMoved(t.Match, handles, other, drawn, deck.Id);
                byte[] bytes = Wire(ev);
                Assert.AreEqual(RecipientSnapshotProjection.HiddenDefinition, ev.Definition);
                Assert.IsFalse(Carries(bytes, drawn.Value), "the real id of the drawn card");
                Assert.IsFalse(Carries(bytes, definition.Value), "the drawn card's identity");
                Assert.AreEqual(1, ev.ToCount, "the hand's new count is public");
            }
        }

        // ---------- the reconciled Console policy ----------

        [Test]
        public void ConsoleCards_FollowTheirFace_ForEveryoneIncludingTheOwner()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2, Seed);
            PlayerId a = t.Players[0], b = t.Players[1];
            MoveToHand(t, a, 1);
            ContainerState hand = t.Match.Containers[t.SetupOf(a).HandContainerId];
            TabletopObjectId card = hand.GetObjectAt(0);
            t.StageInConsole(a, new List<TabletopObjectId> { card });
            CardInstanceState placed = t.Match.Cards[card];

            placed.SetFace(CardFace.FaceUp); // placed for payment: public, as at a real table
            Assert.IsTrue(RecipientSnapshotProjection.MayKnowIdentity(t.Match, placed, a));
            Assert.IsTrue(RecipientSnapshotProjection.MayKnowIdentity(t.Match, placed, b));
            Assert.IsTrue(RecipientSnapshotProjection.MayKnowIdentity(t.Match, placed, RecipientSnapshotProjection.Spectator));

            placed.SetFace(CardFace.FaceDown); // flipped on the Console: a back for everyone
            Assert.IsFalse(RecipientSnapshotProjection.MayKnowIdentity(t.Match, placed, a));
            Assert.IsFalse(RecipientSnapshotProjection.MayKnowIdentity(t.Match, placed, b));
        }
    }
}
