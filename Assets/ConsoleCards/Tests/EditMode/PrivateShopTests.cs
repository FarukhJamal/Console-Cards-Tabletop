using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.GameTemplates.Privacy;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// PRIVACY-P6C (s152): the private path's Shop and Payment. The purchase is the game's own
    /// TrapFloorBuyFromShopUseCase run once on the host's table; these tests check who may ask (the transaction
    /// policy), the strict request bytes, what each recipient receives (one change set, nothing hidden), that a
    /// refused purchase changes nothing, the public Shop list a guest uses with the game's own cost services, and the
    /// refused-command result fix. In-process (unit) only.
    /// </summary>
    public sealed class PrivateShopTests
    {
        private static readonly byte[] Key = { 6, 12, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };

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
            public PlayerId Buyer, Other;
            public readonly Dictionary<PlayerId, MatchState> Tables = new Dictionary<PlayerId, MatchState>();
            public readonly Dictionary<PlayerId, long> Shown = new Dictionary<PlayerId, long>();

            public static Room Open()
            {
                Room r = new Room { T = TrapFloorTestTable.Create(3, 0x6C6C) };
                r.S = new Seats { T = r.T };
                r.Host = new PrivateTableHost(r.T.Match, r.S, r.T.Template.CardLabels, Key);
                r.Buyer = r.T.Turn.ActivePlayerId;
                r.Other = r.T.Players[0] == r.Buyer ? r.T.Players[1] : r.T.Players[0];
                foreach (PlayerId p in r.Host.Recipients)
                {
                    byte[] bytes = r.Host.Join(p);
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
                    PrivateUpdate update = RecipientEventApplier.Read(pair.Value);
                    Assert.IsNotNull(update.Changes, "a change set");
                    Assert.IsFalse(update.Changes.NeedsPackage, "a purchase is described as object changes, not a package");
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
                    Assert.AreEqual(PrivateClientModel.FromTable(fresh).Digest(), PrivateClientModel.FromTable(Tables[p]).Digest(), $"recipient {p} matches the host's projection");
                    Shown[p] = Host.Revision;
                }
            }

            /// <summary>The buyer's payment for the first priced Ability, staged face up in its Console (published).</summary>
            public List<TabletopObjectId> Stage(CardDefinitionData ability, bool leaveOut = false)
            {
                List<TabletopObjectId> payment = T.PaymentFromDeck(Buyer, ability.InputCost, leaveOut);
                Deliver(Host.Apply(() =>
                {
                    T.StageInConsole(Buyer, payment);
                    foreach (TabletopObjectId id in payment) T.Match.Cards[id].SetFace(CardFace.FaceUp);
                }));
                return payment;
            }

            public PrivateTransaction Buy(PlayerId sender, CardDefinitionData ability, IEnumerable<TabletopObjectId> payment)
            {
                PrivateTransaction tx = new PrivateTransaction { Kind = PrivateTransactionKind.ShopBuy, RequestId = Guid.NewGuid(), Revision = Host.Revision, Definition = Guid.Parse(ability.StableId) };
                foreach (TabletopObjectId id in payment) tx.Items.Add(new PrivateTransactionItem { Card = Host.ReferenceFor(sender, id) });
                return tx;
            }

            public TrapFloorBuyFromShopResult Run(PlayerId sender, CardDefinitionData ability, PrivateResolvedTransaction resolved, out Dictionary<PlayerId, byte[]> updates)
            {
                TrapFloorBuyFromShopResult result = default;
                updates = Host.Apply(() => result = new TrapFloorBuyFromShopUseCase(T.Template, T.Turn).Execute(T.Match,
                    new TrapFloorBuyFromShopCommand(T.Context(sender), ability.StableId, TabletopObjectId.New(), resolved.RealCards)));
                return result;
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

        [Test]
        public void ConfirmPayment_BuysOnce_OneRevision_EachSeatSeesOnlyWhatItMay_DuplicateRefused()
        {
            Room r = Room.Open();
            CardDefinitionData ability = r.T.PricedAbility();
            List<TabletopObjectId> payment = r.Stage(ability);
            int cards = r.T.Match.Cards.Count;
            ContainerId hand = r.T.SetupOf(r.Buyer).HandContainerId;
            int handBefore = r.T.Match.Containers[hand].Count;
            PrivateTransaction tx = r.Buy(r.Buyer, ability, payment);
            byte[] wire = PrivateTransactionCodec.Write(tx);
            Assert.IsTrue(PrivateTransactionCodec.TryRead(wire, 0, out PrivateTransaction read));
            Assert.AreEqual(PrivateRequestVerdict.Accepted, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Buyer, read, out PrivateResolvedTransaction resolved));
            long revision = r.Host.Revision;
            TrapFloorBuyFromShopResult result = r.Run(r.Buyer, ability, resolved, out Dictionary<PlayerId, byte[]> updates);
            Assert.IsTrue(result.Succeeded, "the game's own purchase accepted it: " + result.Error);
            Assert.AreEqual(revision + 1, r.Host.Revision, "one purchase, one revision");
            Assert.AreEqual(cards + 1, r.T.Match.Cards.Count, "exactly one card granted");
            foreach (KeyValuePair<PlayerId, byte[]> pair in updates)
            {
                RecipientSnapshotProjection.Classify(r.T.Match, pair.Key, out HashSet<TabletopObjectId> hidden, out _);
                foreach (TabletopObjectId id in hidden) Assert.IsFalse(Carries(pair.Value, id.Value.ToByteArray()), $"no hidden real id in {pair.Key}'s bytes");
            }

            r.Deliver(updates);
            Assert.IsTrue(r.T.Match.Containers[hand].Contains(result.GrantedCardId) || result.DestinationContainerId != hand);
            if (result.PlacedInHand)
            {
                Assert.AreEqual(handBefore + 1, r.Tables[r.Buyer].Containers[hand].Count);
                Assert.IsTrue(r.Tables[r.Buyer].Cards.ContainsKey(result.GrantedCardId), "the buyer holds its new card by its real id");
                Assert.IsFalse(r.Tables[r.Other].Cards.ContainsKey(result.GrantedCardId), "the other seat sees only a back with its own reference");
                Assert.AreEqual(handBefore + 1, r.Tables[r.Other].Containers[hand].Count, "the hand's count is public");
            }

            foreach (TabletopObjectId id in payment)
                foreach (PlayerId p in r.Host.Recipients)
                    Assert.IsFalse(r.Tables[p].Cards.ContainsKey(id), "the payment went back into the Controller deck: no one holds it by its old id");
            r.AssertEveryoneMatchesTheHost();

            SortedDictionary<string, string> places = r.T.CardPlaces();
            Assert.AreEqual(PrivateRequestVerdict.Duplicate, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Buyer, read, out _), "the same Confirm again buys nothing");
            CollectionAssert.AreEqual(places, r.T.CardPlaces());
        }

        [Test]
        public void InsufficientPayment_RefusedByTheGame_NothingMoves_NoCardGranted()
        {
            Room r = Room.Open();
            CardDefinitionData ability = r.T.PricedAbility();
            List<TabletopObjectId> payment = r.Stage(ability, leaveOut: ability.InputCost.CardCount > 1);
            if (ability.InputCost.CardCount <= 1) payment.Clear();
            int cards = r.T.Match.Cards.Count;
            SortedDictionary<string, string> places = r.T.CardPlaces();
            PrivateTransaction tx = r.Buy(r.Buyer, ability, payment);
            if (payment.Count == 0)
            {
                Assert.IsFalse(PrivateTransactionCodec.TryRead(PrivateTransactionCodec.Write(tx), 0, out _), "a purchase with no payment card is not even a request");
                return;
            }

            Assert.AreEqual(PrivateRequestVerdict.Accepted, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Buyer, tx, out PrivateResolvedTransaction resolved), "its own cards in its own Console: the game decides");
            TrapFloorBuyFromShopResult result = r.Run(r.Buyer, ability, resolved, out Dictionary<PlayerId, byte[]> updates);
            long revision = r.Host.Revision;
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(TrapFloorShopError.PaymentInvalid, result.Error);
            Assert.AreEqual(cards, r.T.Match.Cards.Count, "no card granted");
            CollectionAssert.AreEqual(places, r.T.CardPlaces(), "no card moved, no deck count changed");
            Assert.IsNull(updates, "nothing changed: nothing is published (no empty revision)");
            Assert.AreEqual(revision, r.Host.Revision);
            r.AssertEveryoneMatchesTheHost();
        }

        [Test]
        public void AnotherSeatCannotPayWithTheBuyersCards_StaleAndHiddenRefused()
        {
            Room r = Room.Open();
            CardDefinitionData ability = r.T.PricedAbility();
            List<TabletopObjectId> payment = r.Stage(ability);
            SortedDictionary<string, string> places = r.T.CardPlaces();
            // The buyer's staged Console cards are public, but they are not the other seat's to pay with.
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Other, r.Buy(r.Other, ability, payment), out _));
            // A card in the buyer's hidden hand: the other seat holds only its own handle for it, which is not its card.
            ContainerId hand = r.T.SetupOf(r.Buyer).HandContainerId;
            r.Deliver(r.Host.Apply(() => new ContainerTransferService().MoveBetweenContainers(
                r.T.Match.Cards[r.T.Match.Containers[r.T.SetupOf(r.Buyer).ControllerDeckId].GetObjectAt(0)].BaseState,
                r.T.Match.Containers[r.T.SetupOf(r.Buyer).ControllerDeckId], r.T.Match.Containers[hand])));
            TabletopObjectId hidden = r.T.Match.Containers[hand].GetObjectAt(0);
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Other, r.Buy(r.Other, ability, new[] { hidden }), out _), "a handle names the card, but not a card of its own Console");
            PrivateTransaction byRealId = r.Buy(r.Other, ability, new TabletopObjectId[0]);
            byRealId.Items.Add(new PrivateTransactionItem { Card = hidden });
            Assert.AreEqual(PrivateRequestVerdict.HiddenRealId, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Other, byRealId, out _));
            // A Confirm against an older table is refused.
            PrivateTransaction stale = r.Buy(r.Buyer, ability, payment);
            stale.Revision = r.Host.Revision - 1;
            Assert.AreEqual(PrivateRequestVerdict.StaleRevision, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Buyer, stale, out _));
            // Full: only on the buyer's own turn.
            r.S.AnyTime = false;
            Assert.AreEqual(PrivateRequestVerdict.NotYourTurn, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Other, r.Buy(r.Other, ability, payment), out _));
            Assert.AreEqual(1, r.T.Match.Containers[hand].Count);
        }

        [Test]
        public void CancelPayment_OwnPlacesOnly()
        {
            Room r = Room.Open();
            CardDefinitionData ability = r.T.PricedAbility();
            List<TabletopObjectId> payment = r.Stage(ability);
            ContainerId hand = r.T.SetupOf(r.Buyer).HandContainerId, otherHand = r.T.SetupOf(r.Other).HandContainerId;
            PrivateTransaction back = new PrivateTransaction { Kind = PrivateTransactionKind.PaymentRestore, RequestId = Guid.NewGuid(), Revision = r.Host.Revision };
            back.Items.Add(new PrivateTransactionItem { Card = r.Host.ReferenceFor(r.Buyer, payment[0]), Container = hand, Index = 0, FaceUp = true });
            Assert.IsTrue(PrivateTransactionCodec.TryRead(PrivateTransactionCodec.Write(back), 0, out PrivateTransaction read));
            Assert.AreEqual(PrivateRequestVerdict.Accepted, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Buyer, read, out _));
            PrivateTransaction intoOther = new PrivateTransaction { Kind = PrivateTransactionKind.PaymentRestore, RequestId = Guid.NewGuid(), Revision = r.Host.Revision };
            intoOther.Items.Add(new PrivateTransactionItem { Card = r.Host.ReferenceFor(r.Buyer, payment[0]), Container = otherHand, Index = 0, FaceUp = true });
            Assert.AreEqual(PrivateRequestVerdict.InvalidDestination, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Buyer, intoOther, out _));
            PrivateTransaction othersStaged = new PrivateTransaction { Kind = PrivateTransactionKind.PaymentRestore, RequestId = Guid.NewGuid(), Revision = r.Host.Revision };
            othersStaged.Items.Add(new PrivateTransactionItem { Card = r.Host.ReferenceFor(r.Other, payment[0]), Container = otherHand, Index = 0, FaceUp = true });
            Assert.AreEqual(PrivateRequestVerdict.InvalidSource, PrivateTransactionPolicy.Check(r.T.Match, r.S, r.Host, r.Other, othersStaged, out _), "another seat's staged cards are not its own");
        }

        [Test]
        public void TransactionCodec_StrictShape()
        {
            PrivateTransaction buy = new PrivateTransaction { Kind = PrivateTransactionKind.ShopBuy, RequestId = Guid.NewGuid(), Revision = 3, Definition = Guid.NewGuid() };
            buy.Items.Add(new PrivateTransactionItem { Card = TabletopObjectId.New() });
            Assert.IsTrue(PrivateTransactionCodec.TryRead(PrivateTransactionCodec.Write(buy), 0, out PrivateTransaction read));
            Assert.AreEqual(buy.Definition, read.Definition);
            Assert.AreEqual(buy.Items[0].Card, read.Items[0].Card);

            byte[] bytes = PrivateTransactionCodec.Write(buy);
            Assert.IsFalse(PrivateTransactionCodec.TryRead(bytes, 0, out _) && bytes.Length < 2, "sanity");
            byte[] longer = new byte[bytes.Length + 1];
            Array.Copy(bytes, longer, bytes.Length);
            Assert.IsFalse(PrivateTransactionCodec.TryRead(longer, 0, out _), "a byte too many");
            buy.Items.Add(new PrivateTransactionItem { Card = buy.Items[0].Card });
            Assert.IsFalse(PrivateTransactionCodec.TryRead(PrivateTransactionCodec.Write(buy), 0, out _), "the same card twice");
            buy.Items.RemoveAt(1);
            buy.Items[0].Container = new ContainerId(Guid.NewGuid());
            Assert.IsFalse(PrivateTransactionCodec.TryRead(PrivateTransactionCodec.Write(buy), 0, out _), "a purchase names payment cards only");
            PrivateTransaction many = new PrivateTransaction { Kind = PrivateTransactionKind.ShopBuy, RequestId = Guid.NewGuid(), Definition = Guid.NewGuid() };
            for (int i = 0; i <= PrivateTransactionCodec.MaxItems; i++) many.Items.Add(new PrivateTransactionItem { Card = TabletopObjectId.New() });
            Assert.IsFalse(PrivateTransactionCodec.TryRead(PrivateTransactionCodec.Write(many), 0, out _), "too many cards");
            PrivateTransaction restore = new PrivateTransaction { Kind = PrivateTransactionKind.PaymentRestore, RequestId = Guid.NewGuid() };
            restore.Items.Add(new PrivateTransactionItem { Card = TabletopObjectId.New() }); // neither a pile nor a spot
            Assert.IsFalse(PrivateTransactionCodec.TryRead(PrivateTransactionCodec.Write(restore), 0, out _));
            restore.Items[0].HasPose = true;
            restore.Items[0].Pose = new TabletopPose(new TableCoordinate(1, 2), 0f, 0, 0);
            Assert.IsTrue(PrivateTransactionCodec.TryRead(PrivateTransactionCodec.Write(restore), 0, out _));
            Assert.IsFalse(PrivateOperationCodec.IsOperation(PrivateTransactionCodec.Write(restore), 0), "never read as a general operation");
        }

        [Test]
        public void PublicShopList_GivesTheSameAffordabilityAsTheGame_AndCarriesNoHiddenIds()
        {
            Room r = Room.Open();
            GameDefinitionData game = r.T.Game;
            List<CardDefinitionData> controllers = new List<CardDefinitionData>();
            foreach (CardDefinitionData card in game.Cards) if (card.RepresentedControllerInput.HasValue) controllers.Add(card);
            PrivateShopCatalog catalog = PrivateShopCatalog.From(TrapFloorShop.Catalog(game), controllers);
            byte[] bytes = TableWire.Write(catalog, false);
            PrivateShopCatalog read = (PrivateShopCatalog)TableWire.Read(bytes, 0, bytes.Length, false);
            GameDefinitionData shop = read.ToDefinition();
            Assert.IsNotNull(shop, "the list reads as game data");
            Assert.AreEqual(TrapFloorShop.Catalog(game).Count, TrapFloorShop.Catalog(shop).Count, "the same Abilities");
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in r.T.Match.Cards)
                Assert.IsFalse(Carries(bytes, pair.Key.Value.ToByteArray()), "no card instance id at all");

            CardDefinitionData ability = r.T.PricedAbility();
            List<TabletopObjectId> payment = r.Stage(ability);
            Assert.IsTrue(shop.TryGetCard(ability.StableId, out CardDefinitionData priced));
            Assert.AreEqual(InputCostNotation.Format(ability.InputCost), InputCostNotation.Format(priced.InputCost), "the same price");
            ControllerInputCostEvaluator evaluator = new ControllerInputCostEvaluator();
            Assert.AreEqual(evaluator.EvaluateExact(r.T.Match, payment, ability.InputCost, game).CanPay,
                evaluator.EvaluateExact(r.Tables[r.Buyer], payment, priced.InputCost, shop).CanPay, "the guest's own table and the list pay alike");
            Assert.IsTrue(evaluator.EvaluateExact(r.Tables[r.Buyer], payment, priced.InputCost, shop).CanPay);
        }

        [Test]
        public void RefusedCommandResult_ReadsAsRefused()
        {
            Assert.AreEqual(CommandResultStatus.Accepted, default(TrapFloorBuyFromShopResult).Status, "a default result's status reads as Accepted");
            Assert.IsFalse(RefusedResults.Of<TrapFloorBuyFromShopResult>().Succeeded);
            Assert.AreEqual(CommandResultStatus.Rejected, RefusedResults.Of<TrapFloorBuyFromShopResult>().Status);
            Assert.IsFalse(RefusedResults.Of<TransferCardResult>().Succeeded);
            Assert.AreEqual(CommandResultStatus.Rejected, RefusedResults.Of<TransferCardResult>().Status);
            Assert.IsFalse(RefusedResults.Of<DrawCardsResult>().Succeeded);
        }
    }
}
