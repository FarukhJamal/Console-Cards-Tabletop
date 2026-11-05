using System;
using System.Collections.Generic;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// 2026-11-05 deterministic payment tests: the Shop purchase is the authoritative use case every computer runs
    /// (the host orders it, every computer replays it). Known table, known cards, no random draws.
    /// </summary>
    public sealed class PaymentAuthorityTests
    {
        private static TrapFloorBuyFromShopResult Buy(TrapFloorTestTable t, PlayerId actor, CardDefinitionData card,
            IReadOnlyList<TabletopObjectId> payment, TabletopObjectId granted) =>
            new TrapFloorBuyFromShopUseCase(t.Template, t.Turn).Execute(
                t.Match, new TrapFloorBuyFromShopCommand(t.Context(actor), card.StableId, granted, payment));

        [Test]
        public void ValidPurchase_PaysExactly_ReturnsPaymentToTheDeck_AddsOneCard()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId buyer = t.Turn.ActivePlayerId;
            CardDefinitionData ability = t.PricedAbility();
            List<TabletopObjectId> payment = t.PaymentFromDeck(buyer, ability.InputCost);
            t.StageInConsole(buyer, payment);
            int cards = t.Match.Cards.Count;
            long revision = t.Match.Revision;
            TabletopObjectId granted = TabletopObjectId.New();

            TrapFloorBuyFromShopResult result = Buy(t, buyer, ability, payment, granted);

            Assert.IsTrue(result.Succeeded, result.Error.ToString());
            Assert.AreEqual(revision + 1, t.Match.Revision, "one authoritative step");
            Assert.IsTrue(t.Match.Cards.ContainsKey(granted));
            ContainerId deck = t.SetupOf(buyer).ControllerDeckId;
            for (int i = 0; i < payment.Count; i++)
            {
                Assert.AreEqual(deck, t.Match.Cards[payment[i]].BaseState.ContainerId, "a paid card did not go back to the deck");
                Assert.AreEqual(CardFace.FaceDown, t.Match.Cards[payment[i]].Face);
            }

            t.AssertNoDuplicatedOrLostCards(cards + 1);
        }

        [Test]
        public void InsufficientPayment_IsRefused_AndChangesNothing()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId buyer = t.Turn.ActivePlayerId;
            CardDefinitionData ability = t.PricedAbility();
            List<TabletopObjectId> payment = t.PaymentFromDeck(buyer, ability.InputCost, leaveOut: true);
            t.StageInConsole(buyer, payment);
            SortedDictionary<string, string> before = t.CardPlaces();
            long revision = t.Match.Revision;

            TrapFloorBuyFromShopResult result = Buy(t, buyer, ability, payment, TabletopObjectId.New());

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(TrapFloorShopError.PaymentInvalid, result.Error);
            Assert.AreEqual(revision, t.Match.Revision);
            CollectionAssert.AreEqual(before, t.CardPlaces(), "a refused purchase moved cards");
        }

        [Test]
        public void WrongCards_AreRefused_AndChangeNothing()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId buyer = t.Turn.ActivePlayerId;
            CardDefinitionData ability = t.PricedAbility();
            List<TabletopObjectId> payment = t.PaymentFromDeck(buyer, ability.InputCost);
            TabletopObjectId wrong = t.UnacceptedController(buyer, ability.InputCost);
            if (wrong.IsEmpty) Assert.Inconclusive("every controller card pays this price");
            payment[payment.Count - 1] = wrong;
            t.StageInConsole(buyer, payment);
            SortedDictionary<string, string> before = t.CardPlaces();

            TrapFloorBuyFromShopResult result = Buy(t, buyer, ability, payment, TabletopObjectId.New());

            Assert.AreEqual(TrapFloorShopError.PaymentInvalid, result.Error);
            CollectionAssert.AreEqual(before, t.CardPlaces());
        }

        [Test]
        public void PurchaseByThePlayerWhoseTurnItIsNot_IsRefused()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId other = t.Players[0] == t.Turn.ActivePlayerId ? t.Players[1] : t.Players[0];
            CardDefinitionData ability = t.PricedAbility();
            List<TabletopObjectId> payment = t.PaymentFromDeck(other, ability.InputCost);
            t.StageInConsole(other, payment);
            SortedDictionary<string, string> before = t.CardPlaces();

            TrapFloorBuyFromShopResult result = Buy(t, other, ability, payment, TabletopObjectId.New());

            Assert.AreEqual(TrapFloorShopError.ActorIsNotActivePlayer, result.Error);
            CollectionAssert.AreEqual(before, t.CardPlaces());
        }

        [Test]
        public void PayingWithAnotherPlayersConsoleCards_IsRefused()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId buyer = t.Turn.ActivePlayerId;
            PlayerId victim = t.Players[0] == buyer ? t.Players[1] : t.Players[0];
            CardDefinitionData ability = t.PricedAbility();
            List<TabletopObjectId> payment = t.PaymentFromDeck(victim, ability.InputCost);
            t.StageInConsole(victim, payment);
            SortedDictionary<string, string> before = t.CardPlaces();

            TrapFloorBuyFromShopResult result = Buy(t, buyer, ability, payment, TabletopObjectId.New());

            Assert.AreEqual(TrapFloorShopError.PaymentCardNotInConsole, result.Error);
            CollectionAssert.AreEqual(before, t.CardPlaces());
        }

        [Test]
        public void PayingWithCardsStillInTheDeck_IsRefused()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId buyer = t.Turn.ActivePlayerId;
            CardDefinitionData ability = t.PricedAbility();
            List<TabletopObjectId> payment = t.PaymentFromDeck(buyer, ability.InputCost); // never put in the Console

            TrapFloorBuyFromShopResult result = Buy(t, buyer, ability, payment, TabletopObjectId.New());

            Assert.AreEqual(TrapFloorShopError.PaymentCardNotInConsole, result.Error);
        }

        [Test]
        public void TheSamePurchaseSentTwice_IsAppliedOnce()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId buyer = t.Turn.ActivePlayerId;
            CardDefinitionData ability = t.PricedAbility();
            List<TabletopObjectId> payment = t.PaymentFromDeck(buyer, ability.InputCost);
            t.StageInConsole(buyer, payment);
            int cards = t.Match.Cards.Count;
            TabletopObjectId granted = TabletopObjectId.New();

            Assert.IsTrue(Buy(t, buyer, ability, payment, granted).Succeeded);
            TrapFloorBuyFromShopResult again = Buy(t, buyer, ability, payment, granted);

            Assert.IsFalse(again.Succeeded, "a repeated purchase was applied twice");
            t.AssertNoDuplicatedOrLostCards(cards + 1);
        }

        [Test]
        public void NoPurchase_OnceTheFloorHasFailed()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId buyer = t.Turn.ActivePlayerId;
            CardDefinitionData ability = t.PricedAbility();
            List<TabletopObjectId> payment = t.PaymentFromDeck(buyer, ability.InputCost);
            t.StageInConsole(buyer, payment);
            TrapFloorTurnService turns = new TrapFloorTurnService(t.Template, t.Turn, new ConsoleCards.GameTemplates.ControllerInputs.ControllerInputHandService(), t.Feed);
            for (int i = 0; i < t.Players.Length; i++)
                Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(t.Players[i]), t.Players[i]).Succeeded);

            TrapFloorBuyFromShopResult result = Buy(t, buyer, ability, payment, TabletopObjectId.New());

            Assert.AreEqual(TrapFloorShopError.ActorIsNotActivePlayer, result.Error);
        }

        [Test]
        public void TwoComputersReplayingThePurchase_EndWithTheSameTable()
        {
            // Every computer runs the host's ordered command on its own copy (ReplicatedTableAuthority). Same table,
            // same command: the same result, card for card.
            TrapFloorTestTable host = TrapFloorTestTable.Create();
            TrapFloorTestTable guest = TrapFloorTestTable.Create(game: host.Game);
            CollectionAssert.AreEqual(host.CardPlaces(), guest.CardPlaces(), "the two tables did not start the same");
            PlayerId buyer = host.Turn.ActivePlayerId;
            CardDefinitionData ability = host.PricedAbility();
            List<TabletopObjectId> payment = host.PaymentFromDeck(buyer, ability.InputCost);
            host.StageInConsole(buyer, payment);
            guest.StageInConsole(buyer, payment);
            TabletopObjectId granted = TabletopObjectId.New();

            Assert.IsTrue(Buy(host, buyer, ability, payment, granted).Succeeded);
            Assert.IsTrue(Buy(guest, buyer, ability, payment, granted).Succeeded);

            CollectionAssert.AreEqual(host.CardPlaces(), guest.CardPlaces(), "the computers disagree after the purchase");
            Assert.AreEqual(host.Match.Revision, guest.Match.Revision);
        }
    }
}
