using System;
using ConsoleCards.Application.Authority.HiddenState;
using ConsoleCards.Core.Identifiers;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// Stage 3 proof of concept (doc 10): what each recipient is sent, and what the authority accepts. Pure C#.
    /// </summary>
    public sealed class HiddenStatePocTests
    {
        private static readonly PlayerId Blue = new PlayerId(new Guid("11111111-1111-1111-1111-111111111111"));
        private static readonly PlayerId Red = new PlayerId(new Guid("22222222-2222-2222-2222-222222222222"));
        private const string Deck = "deck";
        private const string Discard = "discard";

        private static TabletopObjectId Id(int n) => new TabletopObjectId(new Guid(n, 0, 0, new byte[8]));

        // Blue holds "blue-secret-*", Red holds "red-secret-*", the deck holds "deck-secret-*" (top last), one card face up.
        private static AuthorityTable NewTable()
        {
            AuthorityTable table = new AuthorityTable { TurnPlayer = Blue };
            table.AddSeat(Blue);
            table.AddSeat(Red);
            AuthorityPile blueHand = table.AddPile(AuthorityTable.HandKey(Blue), PileVisibility.OwnerOnly, Blue);
            AuthorityPile redHand = table.AddPile(AuthorityTable.HandKey(Red), PileVisibility.OwnerOnly, Red);
            AuthorityPile deck = table.AddPile(Deck, PileVisibility.Nobody);
            AuthorityPile discard = table.AddPile(Discard, PileVisibility.Public);
            blueHand.Add(new AuthorityCard(Id(1), "blue-secret-A"));
            blueHand.Add(new AuthorityCard(Id(2), "blue-secret-B"));
            redHand.Add(new AuthorityCard(Id(3), "red-secret-X"));
            for (int i = 0; i < 5; i++) deck.Add(new AuthorityCard(Id(10 + i), "deck-secret-" + i));
            discard.Add(new AuthorityCard(Id(20), "public-up"));
            return table;
        }

        private static PileView Pile(RecipientView view, string key)
        {
            for (int i = 0; i < view.Piles.Count; i++) if (view.Piles[i].Key == key) return view.Piles[i];
            throw new AssertionException("No pile " + key);
        }

        [Test]
        public void Seat_SeesOwnHand_AndOnlyTheCountOfOtherHands()
        {
            RecipientView blue = RecipientViewBuilder.Build(NewTable(), Recipient.Seat(Blue));
            Assert.AreEqual(2, Pile(blue, AuthorityTable.HandKey(Blue)).VisibleCards.Count);
            Assert.AreEqual(1, Pile(blue, AuthorityTable.HandKey(Red)).Count);
            Assert.AreEqual(0, Pile(blue, AuthorityTable.HandKey(Red)).VisibleCards.Count);
            string sent = blue.Describe();
            StringAssert.Contains("blue-secret-A", sent);
            StringAssert.DoesNotContain("red-secret", sent);
            StringAssert.DoesNotContain(Id(3).Value.ToString("N"), sent); // not even the other hand's card ids
        }

        [Test]
        public void Deck_FacesIdsAndOrder_AreSentToNobody()
        {
            AuthorityTable table = NewTable();
            foreach (Recipient who in new[] { Recipient.Seat(Blue), Recipient.Seat(Red), Recipient.Spectator })
            {
                RecipientView view = RecipientViewBuilder.Build(table, who);
                Assert.AreEqual(5, Pile(view, Deck).Count);
                Assert.AreEqual(0, Pile(view, Deck).VisibleCards.Count);
                string sent = view.Describe();
                StringAssert.DoesNotContain("deck-secret", sent);
                for (int i = 0; i < 5; i++) StringAssert.DoesNotContain(Id(10 + i).Value.ToString("N"), sent);
            }
        }

        [Test]
        public void Spectator_GetsPublicStateOnly()
        {
            RecipientView view = RecipientViewBuilder.Build(NewTable(), Recipient.Spectator);
            string sent = view.Describe();
            StringAssert.DoesNotContain("blue-secret", sent);
            StringAssert.DoesNotContain("red-secret", sent);
            StringAssert.Contains("public-up", sent);
            Assert.AreEqual(2, Pile(view, AuthorityTable.HandKey(Blue)).Count); // counts stay public, as at a real table
        }

        [Test]
        public void PlayedCard_BecomesVisibleToEveryone()
        {
            AuthorityTable table = NewTable();
            AuthorityRequestProcessor door = new AuthorityRequestProcessor(table);
            Assert.IsTrue(door.Submit(Blue, new PlayCardRequest(Id(1), Discard)).Accepted);
            StringAssert.Contains("blue-secret-A", RecipientViewBuilder.Build(table, Recipient.Seat(Red)).Describe());
            StringAssert.Contains("blue-secret-A", RecipientViewBuilder.Build(table, Recipient.Spectator).Describe());
            StringAssert.DoesNotContain("blue-secret-B", RecipientViewBuilder.Build(table, Recipient.Seat(Red)).Describe());
        }

        [Test]
        public void Draw_GoesToTheDrawersHand_AndOthersOnlySeeTheCount()
        {
            AuthorityTable table = NewTable();
            AuthorityRequestProcessor door = new AuthorityRequestProcessor(table);
            TableRequestResult result = door.Submit(Blue, new DrawRequest(Deck, 2));
            Assert.IsTrue(result.Accepted);
            StringAssert.Contains("deck-secret-4", RecipientViewBuilder.Build(table, Recipient.Seat(Blue)).Describe());
            RecipientView red = RecipientViewBuilder.Build(table, Recipient.Seat(Red));
            Assert.AreEqual(4, Pile(red, AuthorityTable.HandKey(Blue)).Count);
            StringAssert.DoesNotContain("deck-secret", red.Describe());
        }

        [Test]
        public void Requests_AreChecked_NotTrusted()
        {
            AuthorityTable table = NewTable();
            AuthorityRequestProcessor door = new AuthorityRequestProcessor(table);
            long before = table.Revision;
            Assert.AreEqual(TableRequestError.NotYourTurn, door.Submit(Red, new DrawRequest(Deck, 1)).Error);
            Assert.AreEqual(TableRequestError.NotSeated, door.Submit(PlayerId.New(), new DrawRequest(Deck, 1)).Error);
            Assert.AreEqual(TableRequestError.NotInYourHand, door.Submit(Blue, new PlayCardRequest(Id(3), Discard)).Error);
            Assert.AreEqual(TableRequestError.NotAPublicPile, door.Submit(Blue, new PlayCardRequest(Id(1), Deck)).Error);
            Assert.AreEqual(TableRequestError.StateReplacementRefused, door.Submit(Blue, new ReplaceTableRequest()).Error);
            Assert.AreEqual(TableRequestError.NotEnoughCards, door.Submit(Blue, new DrawRequest(Deck, 99)).Error);
            Assert.AreEqual(before, table.Revision, "a refused request changes nothing");
        }

        [Test]
        public void ManualPlay_KeepsFreedom_ButNotOtherPlayersCards()
        {
            AuthorityTable table = NewTable();
            AuthorityRequestProcessor door = new AuthorityRequestProcessor(table) { RequireTurn = false };
            Assert.IsTrue(door.Submit(Red, new DrawRequest(Deck, 1)).Accepted, "out of turn is fine in Manual play");
            Assert.AreEqual(TableRequestError.NotInYourHand, door.Submit(Red, new PlayCardRequest(Id(1), Discard)).Error);
            Assert.AreEqual(TableRequestError.NotSeated, door.Submit(PlayerId.Empty, new DrawRequest(Deck, 1)).Error);
        }

        [Test]
        public void Reconnect_WithTheSecret_GetsTheSameView_AndThePublicIdGetsASpectatorsView()
        {
            AuthorityTable table = NewTable();
            SeatClaims claims = new SeatClaims();
            const string blueSecret = "0123456789abcdef-blue";
            claims.Register(Blue, blueSecret);
            string before = RecipientViewBuilder.Build(table, Recipient.Seat(Blue)).Describe();
            Recipient back = claims.Claim(blueSecret);
            Assert.IsFalse(back.IsSpectator);
            Assert.AreEqual(before, RecipientViewBuilder.Build(table, back).Describe());
            Recipient impostor = claims.Claim(Blue.Value.ToString()); // the id everyone sees in the roster
            Assert.IsTrue(impostor.IsSpectator);
            StringAssert.DoesNotContain("blue-secret", RecipientViewBuilder.Build(table, impostor).Describe());
        }
    }
}
