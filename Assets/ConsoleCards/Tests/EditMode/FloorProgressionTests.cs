using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using NUnit.Framework;
using UnityEditor;

namespace ConsoleCards.Tests
{
    /// <summary>
    /// NEXTFLOOR-1 (2026-11-05): dealing every floor of every win mode, for every difficulty, from the game's own
    /// authored content and default rules. Deterministic (seeded). The recipes below copy the owner's 2026-11-04 table
    /// (TrapFloorCartridge.RecipeFor): Team Play / Free for all 6×6 3 Keys, then 5×5 and 4×4 2 Keys; the Grand Exit
    /// needs 1 Key; Survival one 6×6 floor with 3 Keys, all 3 needed.
    /// </summary>
    public sealed class FloorProgressionTests
    {
        private const string DefinitionPath = "Assets/ConsoleCards/Content/Definitions/TrapFloor/TrapFloorGameDefinition.asset";
        private const string RulesPath = "Assets/ConsoleCards/Content/Definitions/TrapFloor/TrapFloorDefaultRules.asset";

        private static GameDefinitionData LoadGame()
        {
            GameDefinition game = AssetDatabase.LoadAssetAtPath<GameDefinition>(DefinitionPath);
            RuleSetDefinition rules = AssetDatabase.LoadAssetAtPath<RuleSetDefinition>(RulesPath);
            Assert.IsNotNull(game, DefinitionPath);
            Assert.IsNotNull(rules, RulesPath);
            return game.ToData(rules);
        }

        private static TrapFloorFloorRecipe TeamPlay(int floor) => new TrapFloorFloorRecipe(3, floor == 0 ? 3 : 2, floor == 0, 1);

        private static TrapFloorFloorRecipe FreeForAll(int floor) => new TrapFloorFloorRecipe(3, floor == 0 ? 3 : 2, floor <= 1, 1);

        private static TrapFloorFloorRecipe Survival() => new TrapFloorFloorRecipe(1, 3, false, 3);

        private static TrapFloorTemplateDefinition Deal(GameDefinitionData game, string mode, int floor, TrapFloorFloorRecipe recipe, int seed = 7) =>
            TrapFloorTemplateFactory.CreateStandardFourPlayer(
                new SystemRandomValueSource(seed), game, mode, null, floor, null, 2, null, null, recipe);

        [Test]
        public void EveryFloor_OfEveryWinMode_DealsForEveryDifficulty()
        {
            GameDefinitionData game = LoadGame();
            List<string> failures = new List<string>();
            for (int m = 0; m < game.Modes.Count; m++)
            {
                string mode = game.Modes[m].StableId;
                for (int floor = 0; floor < 3; floor++)
                {
                    Try(failures, $"{mode} Team Play floor {floor + 1}", () => Deal(game, mode, floor, TeamPlay(floor)), floor, 1);
                    Try(failures, $"{mode} Free for all floor {floor + 1}", () => Deal(game, mode, floor, FreeForAll(floor)), floor, 1);
                }

                Try(failures, $"{mode} Survival", () => Deal(game, mode, 0, Survival()), 0, 3);
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        private static void Try(List<string> failures, string name, Func<TrapFloorTemplateDefinition> deal, int floor, int keys)
        {
            try
            {
                TrapFloorTemplateDefinition template = deal();
                if (template.FloorIndex != floor) failures.Add($"{name}: dealt floor {template.FloorIndex + 1}");
                if (template.FloorRequiredKeyCount != keys) failures.Add($"{name}: requires {template.FloorRequiredKeyCount} Keys, expected {keys}");
            }
            catch (Exception exception)
            {
                failures.Add($"{name}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        [Test]
        public void WithoutARecipe_TheAuthoredModeCountStillApplies()
        {
            GameDefinitionData game = LoadGame();
            for (int m = 0; m < game.Modes.Count; m++)
            {
                TrapFloorTemplateDefinition template;
                try
                {
                    template = Deal(game, game.Modes[m].StableId, 0, null);
                }
                catch (ArgumentException)
                {
                    continue; // a legacy deal the authored content can't satisfy is still refused, as before
                }

                Assert.AreEqual(game.Modes[m].RequiredKeyCount, template.FloorRequiredKeyCount, game.Modes[m].StableId);
            }
        }

        [Test]
        public void KeyClaims_AreNotRefusedAsAMismatch_ForAnyDifficulty()
        {
            // The table makes the objective with the win mode's Grand Exit Keys (1 outside Survival). Every difficulty
            // must accept that objective, whatever Key count its asset still carries (Hard / Impossible: 3).
            GameDefinitionData game = LoadGame();
            List<string> failures = new List<string>();
            for (int m = 0; m < game.Modes.Count; m++)
            {
                TrapFloorTemplateDefinition template = Deal(game, game.Modes[m].StableId, 0, TeamPlay(0));
                PlayerId first = PlayerId.New();
                MatchId matchId = MatchId.New();
                GameTemplateMatchBuildResult built = template.TryCreateMatch(new[] { first, PlayerId.New() }, matchId);
                Assert.IsTrue(built.Succeeded, game.Modes[m].StableId);
                MatchState match = built.Session.CurrentMatch;
                TrapFloorObjectiveState objective = new TrapFloorObjectiveState(match.Id, 1, template.ActiveMode.Behavior);
                TrapFloorObjectiveUseCase useCase = new TrapFloorObjectiveUseCase(
                    template, objective, new TrapFloorActivityFeedState(match.Id));
                TabletopObjectId anyFloorCard = default;
                foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in template.FloorCardIds)
                {
                    anyFloorCard = pair.Value;
                    break;
                }

                TrapFloorObjectiveResult result = useCase.ClaimKey(
                    match,
                    new TrapFloorClaimKeyCommand(new CommandContext(CommandId.New(), match.Id, first, null), anyFloorCard));
                if (result.Error == TrapFloorObjectiveError.ObjectiveStateMismatch)
                    failures.Add($"{game.Modes[m].StableId}: claim refused as ObjectiveStateMismatch");
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
