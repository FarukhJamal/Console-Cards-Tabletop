using System;
using System.Collections.Generic;
using ConsoleCards.Application.Random;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// P8A (owner, 2026-11-13: "Buying sync, Blind Trap & floor progression"). On the game's real rules and seeded test
    /// tables (no scene, no network): Draw 3 returns, the run's progression after a floor, a run keeping each player's
    /// Avatar and unused Abilities, and the Blind Trap's d4 legality and no-draw rule.
    /// </summary>
    public sealed class RunProgressionAndBlindTests
    {
        private static readonly MatchId TestMatch = new MatchId(new Guid("5eed0000-0000-4000-8000-000000000001"));

        // A table dealt at a floor with these seats' Avatars already chosen (a later floor of the same run).
        private static (TrapFloorTemplateDefinition template, MatchState match) Deal(
            GameDefinitionData game, PlayerId[] players, int floorIndex, IReadOnlyList<ObjectDefinitionId> seatAvatars, int seed = 11)
        {
            TrapFloorTemplateDefinition template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                new SystemRandomValueSource(seed), game, game.Modes[0].StableId, null, floorIndex, null, players.Length, null, seatAvatars,
                new TrapFloorFloorRecipe(3, 3, true, 1));
            GameTemplateMatchBuildResult built = template.TryCreateMatch(players, TestMatch);
            Assert.IsTrue(built.Succeeded, "the test table's match could not be made");
            return (template, built.Session.CurrentMatch);
        }

        private static ContainerState Deck(MatchState match, TrapFloorTemplateDefinition template) => match.Containers[template.AbilityDeckId];

        // A card dealt out of the Ability deck to lie loose and face up at this place (Draw 3).
        private static void DealOut(MatchState match, ContainerState deck, TabletopObjectId cardId, double x)
        {
            CardInstanceState card = match.Cards[cardId];
            Assert.IsTrue(new ContainerTransferService().RemoveFromContainer(card.BaseState, deck).Succeeded);
            card.BaseState.SetPose(new TabletopPose(new TableCoordinate(x, 0.5d), 0f, 0, 0));
            card.SetFace(CardFace.FaceUp);
        }

        // End Buying (Full): every dealt card nobody bought goes face down back into the deck; a bought one stays.
        private static int ReturnUnbought(MatchState match, ContainerState deck, IEnumerable<TabletopObjectId> dealt)
        {
            int returned = 0;
            foreach (TabletopObjectId id in dealt)
            {
                CardInstanceState card = match.Cards[id];
                if (!card.BaseState.ContainerId.IsEmpty || !card.BaseState.OwnerPlayerId.IsEmpty) continue;
                card.SetFace(CardFace.FaceDown);
                Assert.IsTrue(new ContainerTransferService().PlaceIntoContainer(card.BaseState, deck).Succeeded);
                returned++;
            }

            return returned;
        }

        // ---------- 1, 2, 10: Draw 3 returns ----------

        [Test]
        public void EndBuying_UnboughtCardsGoBackIntoTheDeck_TheBoughtOneStays_HostAndGuestAgree()
        {
            TrapFloorTestTable hostTable = TrapFloorTestTable.Create();
            TrapFloorTestTable guestTable = TrapFloorTestTable.Create();
            PlayerId buyer = hostTable.Turn.ActivePlayerId;
            int cards = hostTable.Match.Cards.Count;
            List<TabletopObjectId> dealt = new List<TabletopObjectId>();
            ContainerState hostDeck = Deck(hostTable.Match, hostTable.Template);
            for (int i = 0; i < 3; i++) dealt.Add(hostDeck.GetObjectAt(hostDeck.Count - 1 - i));

            foreach (TrapFloorTestTable t in new[] { hostTable, guestTable })
            {
                ContainerState deck = Deck(t.Match, t.Template);
                for (int i = 0; i < dealt.Count; i++) DealOut(t.Match, deck, dealt[i], 1d + i);
                t.Match.Cards[dealt[1]].BaseState.SetOwner(buyer); // bought (the purchase sets the owner)
                Assert.AreEqual(2, ReturnUnbought(t.Match, deck, dealt));
            }

            CardInstanceState bought = hostTable.Match.Cards[dealt[1]];
            Assert.IsTrue(bought.BaseState.ContainerId.IsEmpty, "the bought card stays where it lies");
            Assert.AreEqual(buyer, bought.BaseState.OwnerPlayerId, "and stays the buyer's");
            Assert.AreEqual(2d, bought.BaseState.Pose.Position.X, 1e-9, "untouched");
            Assert.AreEqual(CardFace.FaceUp, bought.Face);
            foreach (TabletopObjectId id in new[] { dealt[0], dealt[2] })
            {
                Assert.IsTrue(hostDeck.Contains(id), "an unbought card is back in the deck");
                Assert.AreEqual(CardFace.FaceDown, hostTable.Match.Cards[id].Face, "face down");
                Assert.IsTrue(Deck(guestTable.Match, guestTable.Template).Contains(id), "on the guest's table too");
            }

            Assert.AreEqual(cards, hostTable.Match.Cards.Count, "no card made or lost");
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in hostTable.Match.Cards)
            {
                CardInstanceState other = guestTable.Match.Cards[pair.Key];
                Assert.AreEqual(pair.Value.BaseState.ContainerId, other.BaseState.ContainerId, "the same place on both tables");
                Assert.AreEqual(pair.Value.BaseState.OwnerPlayerId, other.BaseState.OwnerPlayerId);
                Assert.AreEqual(pair.Value.Face, other.Face);
            }
        }

        // ---------- 7, 8, 9: the run's progression ----------

        [Test]
        public void FloorEnd_WonGoesOn_LastWonIsGameWon_LostIsGameOverOnAnyFloor()
        {
            Assert.AreEqual(TrapFloorRunStep.NextFloor, TrapFloorRunProgression.After(true, false, false));
            Assert.AreEqual(TrapFloorRunStep.NextStage, TrapFloorRunProgression.After(true, true, true));
            Assert.AreEqual(TrapFloorRunStep.GameWon, TrapFloorRunProgression.After(true, true, false), "the final floor won: no other floor");
            Assert.AreEqual(TrapFloorRunStep.GameOver, TrapFloorRunProgression.After(false, false, false), "lost on Floor 1: not the next floor");
            Assert.AreEqual(TrapFloorRunStep.GameOver, TrapFloorRunProgression.After(false, true, true));
            Assert.IsTrue(TrapFloorRunProgression.GoesOn(TrapFloorRunStep.NextFloor));
            Assert.IsTrue(TrapFloorRunProgression.GoesOn(TrapFloorRunStep.NextStage));
            Assert.IsFalse(TrapFloorRunProgression.GoesOn(TrapFloorRunStep.GameWon), "Play Again starts a fresh run");
            Assert.IsFalse(TrapFloorRunProgression.GoesOn(TrapFloorRunStep.GameOver), "Play Again starts a fresh run");
        }

        [Test]
        public void AllPlayersEliminated_IsGameOver_AndPlayAgainDealsACleanFloorOne()
        {
            TrapFloorTestTable t = TrapFloorTestTable.Create(2, 11);
            TrapFloorAbilityResolutionState ability = new TrapFloorAbilityResolutionState(t.Match.Id);
            TrapFloorTurnService turns = new TrapFloorTurnService(t.Template, t.Turn, new ControllerInputHandService(), t.Feed, ability);
            foreach (PlayerId p in t.Players)
            {
                PlayerId actor = t.Turn.Phase == TrapFloorTurnPhase.FloorTurn ? t.Turn.FloorOperatorPlayerId : t.Turn.ActivePlayerId;
                Assert.IsTrue(turns.MarkPlayerEliminatedForCurrentRound(t.Match, t.Context(actor.IsEmpty ? p : actor), p).Succeeded);
            }

            Assert.IsTrue(t.Turn.IsCurrentFloorFailed);
            TrapFloorRunStep step = TrapFloorRunProgression.After(!t.Turn.IsCurrentFloorFailed, t.Template.IsLastFloor, false);
            Assert.AreEqual(TrapFloorRunStep.GameOver, step, "everyone out: GAME OVER, not the next floor");
            Assert.IsFalse(TrapFloorRunProgression.GoesOn(step));

            // Play Again: a fresh run from Floor 1, nothing carried (no Avatar in place, no bought card, a full deck).
            (TrapFloorTemplateDefinition template, MatchState match) fresh = Deal(t.Game, t.Players, 0, null);
            Assert.AreEqual(0, fresh.template.FloorIndex);
            foreach (TrapFloorPlayerSetupDefinition setup in fresh.template.Players)
                Assert.AreEqual(0, fresh.match.Containers[setup.MainSlotContainerId].Count, "Avatars are chosen again");
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in fresh.match.Cards)
                Assert.IsTrue(pair.Value.BaseState.OwnerPlayerId.IsEmpty || !TrapFloorTemplateFactory.IsAbilityDeckCardId(pair.Key), "no bought card");
            Assert.AreEqual(0, TrapFloorRunCarry.Capture(fresh.match, fresh.template).Count, "nothing to carry on a fresh run");
        }

        // ---------- 5, 6, 10: a run keeps Avatars and unused Abilities ----------

        [Test]
        public void FloorWon_KeepsTheAvatar_AndEveryUnusedBoughtAndStartingAbility_NoCopies_HostAndGuestTheSame()
        {
            TrapFloorTestTable floor1 = TrapFloorTestTable.Create(2, 11);
            MatchState m1 = floor1.Match;
            TrapFloorTemplateDefinition t1 = floor1.Template;
            PlayerId me = floor1.Players[0];
            TrapFloorPlayerSetupDefinition seat = t1.Players[0];
            ContainerTransferService moves = new ContainerTransferService();

            // Seat 1 chose the Avatar with the most starting Abilities: it is in their Main slot; its starting Abilities came
            // out of the pile beside the Console.
            int avatarIndex = 0;
            int startingCount = 0;
            for (int ai = 0; m1.Cards.ContainsKey(TrapFloorTemplateFactory.AvatarDeckCardId(ai)); ai++)
            {
                int n = 0;
                while (m1.Cards.ContainsKey(TrapFloorTemplateFactory.StartingAbilityCardId(ai, n))) n++;
                if (n > startingCount) { startingCount = n; avatarIndex = ai; }
            }

            Assume.That(startingCount, Is.GreaterThanOrEqualTo(1), "an Avatar with starting Abilities");
            TabletopObjectId avatar = TrapFloorTemplateFactory.AvatarDeckCardId(avatarIndex);
            Assert.IsTrue(moves.MoveBetweenContainers(m1.Cards[avatar].BaseState, m1.Containers[TrapFloorTemplateFactory.AvatarDeckId],
                m1.Containers[seat.MainSlotContainerId]).Succeeded);
            ObjectDefinitionId avatarDefinition = m1.Cards[avatar].BaseState.DefinitionId;
            for (int a = 0; a < startingCount; a++)
            {
                CardInstanceState s = m1.Cards[TrapFloorTemplateFactory.StartingAbilityCardId(avatarIndex, a)];
                Assert.IsTrue(moves.RemoveFromContainer(s.BaseState, m1.Containers[TrapFloorTemplateFactory.StartingAbilitiesId]).Succeeded);
                s.BaseState.SetPose(new TabletopPose(new TableCoordinate(-3d - a, 2d), 0f, 0, 0));
            }

            // With two or more, one starting Ability was used (it is in the Ability discard).
            int usedCount = startingCount >= 2 ? 1 : 0;
            if (usedCount == 1)
            {
                CardInstanceState used = m1.Cards[TrapFloorTemplateFactory.StartingAbilityCardId(avatarIndex, 0)];
                Assert.IsTrue(moves.PlaceIntoContainer(used.BaseState, m1.Containers[t1.AbilityDiscardId]).Succeeded);
            }

            // Two bought Ability deck cards: one beside the Console, one in the Hand.
            ContainerState deck1 = Deck(m1, t1);
            TabletopObjectId besideId = deck1.GetObjectAt(0);
            TabletopObjectId inHandId = deck1.GetObjectAt(1);
            DealOut(m1, deck1, besideId, 4d);
            m1.Cards[besideId].BaseState.SetOwner(me);
            DealOut(m1, deck1, inHandId, 5d);
            Assert.IsTrue(moves.PlaceIntoContainer(m1.Cards[inHandId].BaseState, m1.Containers[seat.HandContainerId]).Succeeded);
            m1.Cards[inHandId].BaseState.SetOwner(me);
            TabletopPose besidePose = m1.Cards[besideId].BaseState.Pose;

            List<TrapFloorCarriedAbility> carried = TrapFloorRunCarry.Capture(m1, t1);
            Assert.AreEqual(2, carried.FindAll(c => c.Bought).Count, "both bought, unused Abilities");
            Assert.AreEqual(startingCount - usedCount, carried.FindAll(c => !c.Bought).Count, "the starting Abilities still held (not the used one)");

            // Floor 2 of the same run, on the host and on a guest (the same setup: the same Avatars in place).
            ObjectDefinitionId[] avatars = { avatarDefinition, default(ObjectDefinitionId) };
            (TrapFloorTemplateDefinition template, MatchState match) host2 = Deal(floor1.Game, floor1.Players, 1, avatars, 23);
            (TrapFloorTemplateDefinition template, MatchState match) guest2 = Deal(floor1.Game, floor1.Players, 1, avatars, 23);
            int cards2 = host2.match.Cards.Count;
            TrapFloorCarryResult result = TrapFloorRunCarry.Apply(host2.match, host2.template, carried);
            TrapFloorRunCarry.Apply(guest2.match, guest2.template, TrapFloorRunCarry.Capture(m1, t1));

            MatchState m2 = host2.match;
            TrapFloorPlayerSetupDefinition seat2 = host2.template.Players[0];
            Assert.AreEqual(1, m2.Containers[seat2.MainSlotContainerId].Count, "the Avatar is in place: no new choice");
            Assert.AreEqual(avatarDefinition, m2.Cards[m2.Containers[seat2.MainSlotContainerId].GetObjectAt(0)].BaseState.DefinitionId);
            Assert.AreEqual(2, result.BoughtKept);
            Assert.IsFalse(Deck(m2, host2.template).Contains(besideId), "the bought card is not for sale again");
            Assert.IsTrue(m2.Cards[besideId].BaseState.ContainerId.IsEmpty);
            Assert.AreEqual(besidePose.Position.X, m2.Cards[besideId].BaseState.Pose.Position.X, 1e-9, "where it lay");
            Assert.AreEqual(me, m2.Cards[besideId].BaseState.OwnerPlayerId, "still the buyer's");
            Assert.IsTrue(m2.Containers[seat2.HandContainerId].Contains(inHandId), "back in the Hand");
            Assert.AreEqual(me, m2.Cards[inHandId].BaseState.OwnerPlayerId);
            Assert.AreEqual(startingCount - usedCount, result.StartingKept, "the starting Abilities still held, not a new full set");
            Assert.AreEqual(usedCount, result.StartingLeftOut, "a used one is not dealt again");
            Assert.AreEqual(cards2, m2.Cards.Count, "no card made or lost (no copies)");

            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in m2.Cards)
            {
                CardInstanceState other = guest2.match.Cards[pair.Key];
                Assert.AreEqual(pair.Value.BaseState.ContainerId, other.BaseState.ContainerId, "host and guest: the same table");
                Assert.AreEqual(pair.Value.BaseState.OwnerPlayerId, other.BaseState.OwnerPlayerId);
            }
        }

        // ---------- 3, 4: Blind ----------

        [Test]
        public void BlindDirection_BlockedRollsAgain_OpenIsAccepted_NoneOpenStands()
        {
            // A 3×3 floor, the pawn at the bottom-left corner (1,1).
            Func<TrapFloorCoordinate, bool> open = c => c.X >= 1 && c.X <= 3 && c.Y >= 1 && c.Y <= 3;
            TrapFloorCoordinate corner = new TrapFloorCoordinate(1, 1);
            Assert.AreEqual(TrapFloorBlindRules.Decision.RollAgain, TrapFloorBlindRules.Decide(corner, TrapFloorBlindMovementDirection.Left, open), "off the left edge");
            Assert.AreEqual(TrapFloorBlindRules.Decision.RollAgain, TrapFloorBlindRules.Decide(corner, TrapFloorBlindMovementDirection.Down, open), "off the bottom row");
            Assert.AreEqual(TrapFloorBlindRules.Decision.Accept, TrapFloorBlindRules.Decide(corner, TrapFloorBlindMovementDirection.Right, open));
            Assert.AreEqual(TrapFloorBlindRules.Decision.Accept, TrapFloorBlindRules.Decide(corner, TrapFloorBlindMovementDirection.Up, open));

            // Holes on both open sides: no way has a step; the roll stands (stuck this turn), no endless re-roll.
            Func<TrapFloorCoordinate, bool> walled = c => open(c) && !(c.X == 2 && c.Y == 1) && !(c.X == 1 && c.Y == 2);
            foreach (TrapFloorBlindMovementDirection d in TrapFloorBlindRules.AllDirections)
                Assert.AreEqual(TrapFloorBlindRules.Decision.NoLegalDirection, TrapFloorBlindRules.Decide(corner, d, walled));
            StringAssert.Contains("column 1", TrapFloorBlindRules.Describe(TrapFloorBlindMovementDirection.Left), "said in board words");
            StringAssert.Contains("row 1", TrapFloorBlindRules.Describe(TrapFloorBlindMovementDirection.Down));
        }

        [Test]
        public void BlindNoDraw_RefusedOnlyForTheBlindPlayer_OnlyDuringTheirBlindRound_ThenExpires()
        {
            TrapFloorTestTable t = null;
            TabletopObjectId blind = TabletopObjectId.Empty;
            for (int seed = 11; seed < 260 && blind.IsEmpty; seed++)
            {
                t = TrapFloorTestTable.Create(2, seed);
                foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in t.Template.FloorCardIds)
                {
                    if (t.Template.TryGetFloorCardState(t.Match, pair.Value, out TrapFloorFloorCardState floor)
                        && floor.Content.Category == TrapFloorFloorContentCategory.Trap
                        && floor.Content.TrapEffect == TrapFloorTrapEffectCategory.BlindNextRound
                        && floor.Content.HasSupportedAssistedTrapEffect)
                    {
                        blind = pair.Value;
                        break;
                    }
                }
            }

            if (blind.IsEmpty) Assert.Inconclusive("no seed deals a Blind Trap");
            TrapFloorAbilityResolutionState ability = new TrapFloorAbilityResolutionState(t.Match.Id);
            TrapFloorCollapseState collapse = new TrapFloorCollapseState(t.Match.Id, t.Template.FloorCardIds.Count);
            TrapFloorAbilityResolutionService service = new TrapFloorAbilityResolutionService(ability, t.Turn, t.Feed, t.Template, collapse);
            TrapFloorTurnService turns = new TrapFloorTurnService(t.Template, t.Turn, new ControllerInputHandService(), t.Feed, ability);
            TrapFloorRevealFloorUseCase reveal = new TrapFloorRevealFloorUseCase(t.Template, t.Feed, collapse, ability, t.Turn);
            PlayerId me = t.Turn.ActivePlayerId;
            PlayerId other = Array.Find(t.Players, p => p != me);
            int round = t.Turn.CurrentRound;
            Assert.IsTrue(reveal.Execute(t.Match, new TrapFloorRevealFloorCommand(t.Context(me), blind)).Succeeded);
            Assert.IsTrue(service.ResolveSupportedTrap(t.Match, t.Context(me), blind, turns).Succeeded, "the Blind Trap is applied");
            ContainerId controller = t.Template.Players[0].ControllerDeckId;

            Assert.IsFalse(TrapFloorBlindRules.RefusesControllerDraw(t.Template, t.Turn, ability, controller, me), "not this round");
            for (int i = 0; i < 40 && !(t.Turn.CurrentRound == round + 1 && t.Turn.Phase == TrapFloorTurnPhase.PlayerTurn && t.Turn.ActivePlayerId == me); i++)
            {
                PlayerId actor = t.Turn.Phase == TrapFloorTurnPhase.FloorTurn ? t.Turn.FloorOperatorPlayerId : t.Turn.ActivePlayerId;
                Assert.IsTrue(turns.Advance(t.Match, t.Context(actor)).Succeeded);
            }

            Assert.AreEqual(round + 1, t.Turn.CurrentRound);
            Assert.IsTrue(TrapFloorBlindRules.RefusesControllerDraw(t.Template, t.Turn, ability, controller, me), "Blind round: no Controller draw");
            Assert.IsFalse(TrapFloorBlindRules.RefusesControllerDraw(t.Template, t.Turn, ability, controller, other), "another player draws");
            Assert.IsFalse(TrapFloorBlindRules.RefusesControllerDraw(t.Template, t.Turn, ability, t.Template.AbilityDeckId, me), "not a Controller deck");

            for (int i = 0; i < 40 && t.Turn.CurrentRound < round + 2; i++)
            {
                PlayerId actor = t.Turn.Phase == TrapFloorTurnPhase.FloorTurn ? t.Turn.FloorOperatorPlayerId : t.Turn.ActivePlayerId;
                Assert.IsTrue(turns.Advance(t.Match, t.Context(actor)).Succeeded);
            }

            Assert.IsFalse(TrapFloorBlindRules.RefusesControllerDraw(t.Template, t.Turn, ability, controller, me), "expired with its round");
        }
    }
}
