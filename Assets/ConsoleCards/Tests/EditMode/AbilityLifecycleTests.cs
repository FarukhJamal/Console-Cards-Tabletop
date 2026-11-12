using System.Collections.Generic;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A batch 2 (owner, 2026-11-12, R4): an Ability's life is read from the table's own state. A card of the Ability
    /// deck dealt out of every pile with no owner is "drawn, not bought"; buying it (the game's own purchase, the same
    /// use case every computer replays) gives it the buyer as owner and creates no card. Starting Abilities and Avatars
    /// are never Ability deck cards.
    /// </summary>
    public sealed class AbilityLifecycleTests
    {
        [Test]
        public void AbilityDeckCards_AreKnownByTheirId_StartingAbilitiesAndAvatarsAreNot()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            ContainerState deck = t.Match.Containers[t.Template.AbilityDeckId];
            Assert.Greater(deck.Count, 0, "the authored game has an Ability deck");
            for (int i = 0; i < deck.Count; i++)
                Assert.IsTrue(TrapFloorTemplateFactory.IsAbilityDeckCardId(deck.GetObjectAt(i)), "an Ability deck card");

            Assert.IsFalse(TrapFloorTemplateFactory.IsAbilityDeckCardId(TrapFloorTemplateFactory.StartingAbilityCardId(0, 0)), "a starting Ability");
            Assert.IsFalse(TrapFloorTemplateFactory.IsAbilityDeckCardId(TrapFloorTemplateFactory.AvatarDeckCardId(0)), "an Avatar");
            Assert.IsFalse(TrapFloorTemplateFactory.IsAbilityDeckCardId(TabletopObjectId.New()), "any other card");
        }

        [Test]
        public void BuyingADealtCard_BuysThatCard_NoCopy_TheBuyerOwnsIt_ItIsNoLongerUnbought()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId buyer = t.Turn.ActivePlayerId;
            CardDefinitionData ability = t.PricedAbility();
            ContainerState deck = t.Match.Containers[t.Template.AbilityDeckId];
            ObjectDefinitionId definition = new ObjectDefinitionId(System.Guid.Parse(ability.StableId));
            TabletopObjectId dealt = TabletopObjectId.Empty;
            for (int i = 0; i < deck.Count && dealt.IsEmpty; i++)
            {
                TabletopObjectId id = deck.GetObjectAt(i);
                if (t.Match.Cards[id].BaseState.DefinitionId == definition) dealt = id;
            }

            Assert.IsFalse(dealt.IsEmpty, "the deck holds the priced Ability");
            CardInstanceState card = t.Match.Cards[dealt];
            Assert.IsTrue(new ContainerTransferService().RemoveFromContainer(card.BaseState, deck).Succeeded, "dealt out of the deck");
            card.SetFace(CardFace.FaceUp);
            Assert.IsTrue(card.BaseState.ContainerId.IsEmpty && card.BaseState.OwnerPlayerId.IsEmpty, "dealt: loose, nobody's");

            TabletopPose dealtAt = card.BaseState.Pose;
            List<TabletopObjectId> payment = t.PaymentFromDeck(buyer, ability.InputCost);
            t.StageInConsole(buyer, payment);
            int cards = t.Match.Cards.Count;
            TrapFloorBuyFromShopResult result = new TrapFloorBuyFromShopUseCase(t.Template, t.Turn).Execute(
                t.Match, new TrapFloorBuyFromShopCommand(t.Context(buyer), ability.StableId, TabletopObjectId.New(), payment, dealt));

            Assert.IsTrue(result.Succeeded, result.Error.ToString());
            t.AssertNoDuplicatedOrLostCards(cards); // the dealt card itself, no Shop copy
            Assert.AreEqual(buyer, card.BaseState.OwnerPlayerId, "bought: the buyer's");
            Assert.AreEqual(CardFace.FaceUp, card.Face);
            Assert.IsFalse(card.BaseState.IsUserLocked, "a bought card is free to handle");
            Assert.IsFalse(deck.Contains(dealt));
            Assert.IsTrue(card.BaseState.ContainerId.IsEmpty, "owner, 2026-11-12: bought where it lies (not moved into the Hand)");
            Assert.AreEqual(dealtAt.Position, card.BaseState.Pose.Position, "same place on the table");
            Assert.IsFalse(result.PlacedInHand);

            // The same card cannot be bought twice (it is no longer a free Ability deck card for sale).
            List<TabletopObjectId> again = t.PaymentFromDeck(buyer, ability.InputCost);
            t.StageInConsole(buyer, again);
            TrapFloorBuyFromShopResult second = new TrapFloorBuyFromShopUseCase(t.Template, t.Turn).Execute(
                t.Match, new TrapFloorBuyFromShopCommand(t.Context(buyer), ability.StableId, TabletopObjectId.New(), again, dealt));
            Assert.IsFalse(second.Succeeded, "a bought card is not for sale again");
            Assert.AreEqual(buyer, card.BaseState.OwnerPlayerId);
            t.AssertNoDuplicatedOrLostCards(cards);
        }

        [Test]
        public void Free_ACardPutInTheBuyersConsole_IsBoughtWhereItIs_AndIsNeverItsOwnPayment()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create();
            PlayerId buyer = t.Turn.ActivePlayerId;
            CardDefinitionData ability = t.PricedAbility();
            ContainerState deck = t.Match.Containers[t.Template.AbilityDeckId];
            ObjectDefinitionId definition = new ObjectDefinitionId(System.Guid.Parse(ability.StableId));
            TabletopObjectId chosen = TabletopObjectId.Empty;
            for (int i = 0; i < deck.Count && chosen.IsEmpty; i++)
                if (t.Match.Cards[deck.GetObjectAt(i)].BaseState.DefinitionId == definition) chosen = deck.GetObjectAt(i);
            Assert.IsFalse(chosen.IsEmpty);

            // The player draws it by hand and puts it in their own Console, then stages the price beside it.
            List<TabletopObjectId> payment = t.PaymentFromDeck(buyer, ability.InputCost);
            List<TabletopObjectId> everything = new List<TabletopObjectId>(payment) { chosen };
            t.StageInConsole(buyer, everything);
            CardInstanceState card = t.Match.Cards[chosen];
            card.SetFace(CardFace.FaceUp);
            ContainerId slot = card.BaseState.ContainerId;
            Assert.IsFalse(slot.IsEmpty, "in a Console slot");
            int cards = t.Match.Cards.Count;
            long revision = t.Match.Revision;

            TrapFloorBuyFromShopResult itself = new TrapFloorBuyFromShopUseCase(t.Template, t.Turn).Execute(
                t.Match, new TrapFloorBuyFromShopCommand(t.Context(buyer), ability.StableId, TabletopObjectId.New(), everything, chosen));
            Assert.IsFalse(itself.Succeeded, "the bought card cannot pay for itself");
            Assert.AreEqual(revision, t.Match.Revision, "a refused purchase changes nothing");

            TrapFloorBuyFromShopResult bought = new TrapFloorBuyFromShopUseCase(t.Template, t.Turn).Execute(
                t.Match, new TrapFloorBuyFromShopCommand(t.Context(buyer), ability.StableId, TabletopObjectId.New(), payment, chosen));
            Assert.IsTrue(bought.Succeeded, bought.Error.ToString());
            Assert.AreEqual(slot, card.BaseState.ContainerId, "it stays in the Console slot the player put it in");
            Assert.AreEqual(buyer, card.BaseState.OwnerPlayerId);
            Assert.AreEqual(revision + 1, t.Match.Revision, "one recorded purchase");
            t.AssertNoDuplicatedOrLostCards(cards);
        }
    }
}
