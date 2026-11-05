using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;
using UnityEditor;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// 2026-11-05: a known, reproducible Trap Floor table for the authoritative use cases (seeded deal, fixed player
    /// and match IDs, the game's own authored content and default rules). Test-only: it lives in the EditMode test
    /// assembly, which is never in a player build, and it calls no network or client code.
    /// </summary>
    internal sealed class TrapFloorTestTable
    {
        private const string DefinitionPath = "Assets/ConsoleCards/Content/Definitions/TrapFloor/TrapFloorGameDefinition.asset";
        private const string RulesPath = "Assets/ConsoleCards/Content/Definitions/TrapFloor/TrapFloorDefaultRules.asset";

        public GameDefinitionData Game;
        public TrapFloorTemplateDefinition Template;
        public MatchState Match;
        public TrapFloorTurnState Turn;
        public TrapFloorActivityFeedState Feed;
        public PlayerId[] Players;

        public static GameDefinitionData LoadGame()
        {
            GameDefinition game = AssetDatabase.LoadAssetAtPath<GameDefinition>(DefinitionPath);
            RuleSetDefinition rules = AssetDatabase.LoadAssetAtPath<RuleSetDefinition>(RulesPath);
            Assert.IsNotNull(game, DefinitionPath);
            Assert.IsNotNull(rules, RulesPath);
            return game.ToData(rules);
        }

        /// <summary>Same seed, same IDs: two calls build two identical tables (as two computers replaying one game).</summary>
        public static TrapFloorTestTable Create(int playerCount = 2, int seed = 11, GameDefinitionData game = null)
        {
            TrapFloorTestTable table = new TrapFloorTestTable();
            table.Game = game ?? LoadGame();
            table.Template = TrapFloorTemplateFactory.CreateStandardFourPlayer(
                new SystemRandomValueSource(seed), table.Game, table.Game.Modes[0].StableId, null, 0, null, playerCount, null, null,
                new TrapFloorFloorRecipe(3, 3, true, 1));
            table.Players = new PlayerId[playerCount];
            for (int i = 0; i < playerCount; i++)
                table.Players[i] = new PlayerId(new Guid(0x0C0FFEE0 + i, 0, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, (byte)i }));
            MatchId matchId = new MatchId(new Guid("5eed0000-0000-4000-8000-000000000001"));
            GameTemplateMatchBuildResult built = table.Template.TryCreateMatch(table.Players, matchId);
            Assert.IsTrue(built.Succeeded, "the test table's match could not be made");
            table.Match = built.Session.CurrentMatch;
            table.Turn = new TrapFloorTurnState(table.Match.Id, table.Players);
            table.Feed = new TrapFloorActivityFeedState(table.Match.Id);
            return table;
        }

        public CommandContext Context(PlayerId actor) => new CommandContext(CommandId.New(), Match.Id, actor, null);

        public TrapFloorPlayerSetupDefinition SetupOf(PlayerId player)
        {
            for (int i = 0; i < Template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition setup = Template.Players[i];
                if (Match.Seats.TryGetValue(setup.SeatId, out SeatState seat) && seat.OccupantPlayerId == player) return setup;
            }

            Assert.Fail("no seat for the player");
            return null;
        }

        /// <summary>The first Ability for sale, with its price.</summary>
        public CardDefinitionData PricedAbility()
        {
            List<CardDefinitionData> catalog = TrapFloorShop.Catalog(Game);
            for (int i = 0; i < catalog.Count; i++)
                if (TrapFloorShop.IsForSale(catalog[i]) && catalog[i].InputCost.CardCount >= 1) return catalog[i];
            Assert.Fail("no Ability for sale in the authored content");
            return null;
        }

        /// <summary>
        /// Picks controller cards from <paramref name="player"/>'s Controller deck that pay <paramref name="cost"/>
        /// exactly (one per requirement slot); with <paramref name="leaveOut"/> the last slot is left unpaid.
        /// </summary>
        public List<TabletopObjectId> PaymentFromDeck(PlayerId player, InputCostData cost, bool leaveOut = false)
        {
            Assert.IsTrue(ControllerInputCardCatalog.TryCreate(Game, out ControllerInputCardCatalog catalog));
            ContainerState deck = Match.Containers[SetupOf(player).ControllerDeckId];
            List<TabletopObjectId> chosen = new List<TabletopObjectId>();
            HashSet<TabletopObjectId> used = new HashSet<TabletopObjectId>();
            for (int r = 0; r < cost.Requirements.Count; r++)
            {
                InputRequirementData requirement = cost.Requirements[r];
                for (int copy = 0; copy < requirement.Count; copy++)
                {
                    bool found = false;
                    for (int i = 0; i < deck.Count && !found; i++)
                    {
                        TabletopObjectId id = deck.GetObjectAt(i);
                        if (used.Contains(id) || !Match.Cards.TryGetValue(id, out CardInstanceState card)) continue;
                        if (catalog.TryGetInput(card.BaseState.DefinitionId, out ControllerInput input) && requirement.Accepts(input))
                        {
                            used.Add(id);
                            chosen.Add(id);
                            found = true;
                        }
                    }

                    Assert.IsTrue(found, "the Controller deck cannot pay the price");
                }
            }

            if (leaveOut) chosen.RemoveAt(chosen.Count - 1);
            return chosen;
        }

        /// <summary>A controller card the price does not accept, or Empty when every card is accepted.</summary>
        public TabletopObjectId UnacceptedController(PlayerId player, InputCostData cost)
        {
            Assert.IsTrue(ControllerInputCardCatalog.TryCreate(Game, out ControllerInputCardCatalog catalog));
            ContainerState deck = Match.Containers[SetupOf(player).ControllerDeckId];
            for (int i = 0; i < deck.Count; i++)
            {
                TabletopObjectId id = deck.GetObjectAt(i);
                if (Match.Cards.TryGetValue(id, out CardInstanceState card)
                    && catalog.TryGetInput(card.BaseState.DefinitionId, out ControllerInput input)
                    && !cost.Accepts(input)) return id;
            }

            return TabletopObjectId.Empty;
        }

        /// <summary>Puts the cards into <paramref name="owner"/>'s Console side slots (one card per slot, then wraps).</summary>
        public void StageInConsole(PlayerId owner, IReadOnlyList<TabletopObjectId> cards)
        {
            TrapFloorPlayerSetupDefinition setup = SetupOf(owner);
            Assert.Greater(setup.SideSlotContainerIds.Count, 0, "no Console side slots");
            ContainerTransferService transfer = new ContainerTransferService();
            for (int i = 0; i < cards.Count; i++)
            {
                CardInstanceState card = Match.Cards[cards[i]];
                ContainerState from = Match.Containers[card.BaseState.ContainerId];
                bool placed = false;
                for (int s = 0; s < setup.SideSlotContainerIds.Count && !placed; s++)
                {
                    ContainerState slot = Match.Containers[setup.SideSlotContainerIds[(i + s) % setup.SideSlotContainerIds.Count]];
                    placed = transfer.MoveBetweenContainers(card.BaseState, from, slot).Succeeded;
                }

                Assert.IsTrue(placed, "a payment card could not be put in a Console slot");
            }
        }

        /// <summary>Where every card is: card ID to container (Empty when loose). Two equal maps mean the same table.</summary>
        public SortedDictionary<string, string> CardPlaces()
        {
            SortedDictionary<string, string> places = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in Match.Cards)
                places[pair.Key.ToString()] = pair.Value.BaseState.ContainerId.ToString() + "/" + pair.Value.Face;
            return places;
        }

        /// <summary>Each card is listed in exactly the container its state names, and in no other.</summary>
        public void AssertNoDuplicatedOrLostCards(int expectedCardCount)
        {
            Assert.AreEqual(expectedCardCount, Match.Cards.Count, "cards were created or lost");
            Dictionary<TabletopObjectId, int> listed = new Dictionary<TabletopObjectId, int>();
            foreach (KeyValuePair<ContainerId, ContainerState> pair in Match.Containers)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    TabletopObjectId id = pair.Value.GetObjectAt(i);
                    listed.TryGetValue(id, out int n);
                    listed[id] = n + 1;
                    if (Match.Cards.TryGetValue(id, out CardInstanceState card))
                        Assert.AreEqual(pair.Key, card.BaseState.ContainerId, $"card {id} is listed in a container it is not in");
                }
            }

            foreach (KeyValuePair<TabletopObjectId, int> pair in listed)
                Assert.AreEqual(1, pair.Value, $"card {pair.Key} is in {pair.Value} containers");
        }
    }
}
