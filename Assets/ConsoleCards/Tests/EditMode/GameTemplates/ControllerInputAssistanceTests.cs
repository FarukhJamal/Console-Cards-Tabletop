using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.GameTemplates
{
    public sealed class ControllerInputAssistanceTests
    {
        [Test]
        public void Purchase_ConsumesExactPlayerHandPaymentAndGrantsOneAuthoredCardInOneAction()
        {
            Fixture fixture = CreateFixture();
            TabletopObjectId rightOne = fixture.AddCardToHand(fixture.RightDefinitionId);
            TabletopObjectId unrelatedUp = fixture.AddCardToHand(fixture.UpDefinitionId);
            TabletopObjectId requiredA = fixture.AddCardToHand(fixture.ADefinitionId);
            TabletopObjectId rightTwo = fixture.AddCardToHand(fixture.RightDefinitionId);
            GameTemplateInitialSnapshot before = GameTemplateInitialSnapshot.Capture(fixture.Match);
            AuthoritativeActionAcceptance? acceptance = null;
            fixture.Match.AuthoritativeActionAccepted += accepted => acceptance = accepted;

            TabletopObjectId grantedId = TabletopObjectId.New();
            ActionAbilityPurchaseResult result = new ActionAbilityPurchaseService().Purchase(
                fixture.Match,
                fixture.Definition,
                new PurchaseActionOrAbilityCommand(
                    fixture.Context(),
                    fixture.SeatId,
                    fixture.ActionArea.Id,
                    fixture.AbilityStableId,
                    grantedId));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ConsumedCardIds, Is.EquivalentTo(new[] { rightOne, requiredA, rightTwo }));
            Assert.That(fixture.Hand.ObjectIds, Is.EqualTo(new[] { unrelatedUp }));
            Assert.That(fixture.Match.Cards.ContainsKey(rightOne), Is.False);
            Assert.That(fixture.Match.Cards.ContainsKey(requiredA), Is.False);
            Assert.That(fixture.Match.Cards.ContainsKey(rightTwo), Is.False);
            Assert.That(fixture.ActionArea.ObjectIds, Is.EqualTo(new[] { grantedId }));
            Assert.That(fixture.Match.Cards[grantedId].BaseState.DefinitionId, Is.EqualTo(fixture.AbilityDefinitionId));
            Assert.That(fixture.Match.Cards[grantedId].BaseState.OwnerPlayerId, Is.EqualTo(fixture.PlayerId));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
            Assert.That(acceptance.HasValue, Is.True);
            Assert.That(acceptance.Value.Kind, Is.EqualTo(AuthoritativeActionKind.PurchaseActionOrAbility));
            Assert.That(acceptance.Value.RecordMode, Is.EqualTo(AuthoritativeActionRecordMode.Transaction));

            MatchState restoredBefore = before.Restore(2);
            Assert.That(restoredBefore.GetContainer(fixture.Hand.Id).ObjectIds,
                Is.EqualTo(new[] { rightOne, unrelatedUp, requiredA, rightTwo }));
            Assert.That(restoredBefore.Cards.ContainsKey(grantedId), Is.False);
        }

        [Test]
        public void Purchase_WhenCompleteCostIsUnavailable_ConsumesNothingAndCreatesNoAction()
        {
            Fixture fixture = CreateFixture();
            TabletopObjectId right = fixture.AddCardToHand(fixture.RightDefinitionId);
            long revision = fixture.Match.Revision;
            int acceptedActions = 0;
            fixture.Match.AuthoritativeActionAccepted += _ => acceptedActions++;

            ActionAbilityPurchaseResult result = new ActionAbilityPurchaseService().Purchase(
                fixture.Match,
                fixture.Definition,
                new PurchaseActionOrAbilityCommand(
                    fixture.Context(),
                    fixture.SeatId,
                    fixture.ActionArea.Id,
                    fixture.AbilityStableId,
                    TabletopObjectId.New()));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ActionAbilityPurchaseError.CannotAfford));
            Assert.That(fixture.Hand.ObjectIds, Is.EqualTo(new[] { right }));
            Assert.That(fixture.Match.Cards.ContainsKey(right), Is.True);
            Assert.That(fixture.ActionArea.Count, Is.Zero);
            Assert.That(fixture.Match.Revision, Is.EqualTo(revision));
            Assert.That(acceptedActions, Is.Zero);
        }

        [Test]
        public void DrawUpToConfiguredHandLimit_WithFourCards_DrawsSixThenNoOpsAtTen()
        {
            Fixture fixture = CreateFixture();
            for (int i = 0; i < 4; i++) fixture.AddCardToHand(fixture.UpDefinitionId);
            for (int i = 0; i < 10; i++) fixture.AddCardToDeck(fixture.RightDefinitionId);

            ControllerInputHandDrawResult first = new ControllerInputHandService()
                .DrawUpToConfiguredHandLimit(
                    fixture.Match,
                    fixture.Definition,
                    new DrawUpToConfiguredHandLimitCommand(
                        fixture.Context(),
                        fixture.SeatId,
                        fixture.ControllerDeck.Id));

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Changed, Is.True);
            Assert.That(first.DrawnCount, Is.EqualTo(6));
            Assert.That(fixture.Hand.Count, Is.EqualTo(10));
            Assert.That(fixture.ControllerDeck.Count, Is.EqualTo(4));
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));

            ControllerInputHandDrawResult second = new ControllerInputHandService()
                .DrawUpToConfiguredHandLimit(
                    fixture.Match,
                    fixture.Definition,
                    new DrawUpToConfiguredHandLimitCommand(
                        fixture.Context(),
                        fixture.SeatId,
                        fixture.ControllerDeck.Id));

            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.Changed, Is.False);
            Assert.That(second.DrawnCount, Is.Zero);
            Assert.That(fixture.Match.Revision, Is.EqualTo(1));
        }

        private static Fixture CreateFixture()
        {
            PlayerId playerId = PlayerId.New();
            SeatId seatId = SeatId.New();
            ContainerState hand = new ContainerState(
                ContainerId.New(), ContainerKind.Hand, seatId, ObjectVisibility.OwnerOnly, 10);
            ContainerState controllerDeck = new ContainerState(
                ContainerId.New(), ContainerKind.Deck, seatId, ObjectVisibility.Public, 0);
            ContainerState actionArea = new ContainerState(
                ContainerId.New(), ContainerKind.Stack, seatId, ObjectVisibility.Public, 0);
            SeatState seat = new SeatState(
                seatId,
                TabletopPose.Default,
                hand.Id,
                new ConsoleState(seatId, Array.Empty<ContainerId>()),
                playerId,
                SeatStatus.Occupied);
            MatchState match = new MatchState(
                MatchId.New(),
                GameTemplateId.Empty,
                0,
                Array.Empty<CardInstanceState>(),
                Array.Empty<PawnState>(),
                Array.Empty<TokenState>(),
                new[] { hand, controllerDeck, actionArea },
                new[] { seat });

            Guid up = Guid.NewGuid();
            Guid right = Guid.NewGuid();
            Guid a = Guid.NewGuid();
            Guid ability = Guid.NewGuid();
            CardDefinitionData upCard = CreateCard(up, "Up", ControllerInput.Up);
            CardDefinitionData rightCard = CreateCard(right, "Right", ControllerInput.Right);
            CardDefinitionData aCard = CreateCard(a, "A", ControllerInput.A);
            CardDefinitionData abilityCard = new CardDefinitionData(
                ability.ToString(),
                "Ability",
                "Ability",
                string.Empty,
                string.Empty,
                string.Empty,
                1,
                new InputCostData(new[]
                {
                    new InputRequirementData(ControllerInput.Right, 2),
                    new InputRequirementData(ControllerInput.A, 1),
                }),
                CardOrientation.Portrait,
                "ActionArea",
                Array.Empty<string>());
            GameDefinitionData definition = new GameDefinitionData(
                Guid.NewGuid().ToString(),
                "Test Game",
                string.Empty,
                1,
                1,
                null,
                new[] { upCard, rightCard, aCard, abilityCard },
                Array.Empty<GameContentSetData>(),
                Array.Empty<AvatarDefinitionData>(),
                Array.Empty<ModeDefinitionData>(),
                string.Empty,
                null,
                new[] { ControllerInput.Up, ControllerInput.Right, ControllerInput.A },
                new ControllerConfigurationData(10, true, true, true, false),
                ControllerMappingKind.None,
                string.Empty,
                string.Empty);
            return new Fixture(match, definition, playerId, seatId, hand, controllerDeck, actionArea, up, right, a, ability);
        }

        private static CardDefinitionData CreateCard(Guid id, string name, ControllerInput input)
        {
            return new CardDefinitionData(
                id.ToString(),
                name,
                "ControllerInput",
                string.Empty,
                string.Empty,
                string.Empty,
                20,
                new InputCostData(Array.Empty<InputRequirementData>()),
                CardOrientation.Portrait,
                string.Empty,
                Array.Empty<string>(),
                input);
        }

        private sealed class Fixture
        {
            private readonly ContainerTransferService transfer = new ContainerTransferService();

            public Fixture(
                MatchState match,
                GameDefinitionData definition,
                PlayerId playerId,
                SeatId seatId,
                ContainerState hand,
                ContainerState controllerDeck,
                ContainerState actionArea,
                Guid upDefinitionId,
                Guid rightDefinitionId,
                Guid aDefinitionId,
                Guid abilityDefinitionId)
            {
                Match = match;
                Definition = definition;
                PlayerId = playerId;
                SeatId = seatId;
                Hand = hand;
                ControllerDeck = controllerDeck;
                ActionArea = actionArea;
                UpDefinitionId = new ObjectDefinitionId(upDefinitionId);
                RightDefinitionId = new ObjectDefinitionId(rightDefinitionId);
                ADefinitionId = new ObjectDefinitionId(aDefinitionId);
                AbilityDefinitionId = new ObjectDefinitionId(abilityDefinitionId);
                AbilityStableId = abilityDefinitionId.ToString();
            }

            public MatchState Match { get; }
            public GameDefinitionData Definition { get; }
            public PlayerId PlayerId { get; }
            public SeatId SeatId { get; }
            public ContainerState Hand { get; }
            public ContainerState ControllerDeck { get; }
            public ContainerState ActionArea { get; }
            public ObjectDefinitionId UpDefinitionId { get; }
            public ObjectDefinitionId RightDefinitionId { get; }
            public ObjectDefinitionId ADefinitionId { get; }
            public ObjectDefinitionId AbilityDefinitionId { get; }
            public string AbilityStableId { get; }

            public CommandContext Context()
            {
                return new CommandContext(CommandId.New(), Match.Id, PlayerId, Match.Revision);
            }

            public TabletopObjectId AddCardToHand(ObjectDefinitionId definitionId)
            {
                return AddCard(definitionId, Hand);
            }

            public TabletopObjectId AddCardToDeck(ObjectDefinitionId definitionId)
            {
                return AddCard(definitionId, ControllerDeck);
            }

            private TabletopObjectId AddCard(ObjectDefinitionId definitionId, ContainerState destination)
            {
                TabletopObjectId id = TabletopObjectId.New();
                CardInstanceState card = new CardInstanceState(
                    new TabletopObjectState(
                        id,
                        definitionId,
                        TabletopObjectKind.Card,
                        TabletopPose.Default,
                        ContainerId.Empty,
                        PlayerId,
                        destination.Visibility,
                        false),
                    CardFace.FaceUp);
                Match.AddUncontainedCard(card);
                Assert.That(transfer.PlaceIntoContainer(card.BaseState, destination).Succeeded, Is.True);
                return id;
            }
        }
    }
}
