using System;
using System.Collections.Generic;
using ConsoleCards.GameTemplates.Definitions;
using NUnit.Framework;

namespace ConsoleCards.Tests.EditMode.GameTemplates
{
    /// <summary>Doc 23, R2a: a rule set's settings feed the game data that templates are built from.</summary>
    public sealed class RuleSettingsApplicationTests
    {
        private const string Easy = "mode-easy";
        private const string Hard = "mode-hard";

        [Test]
        public void NoSettings_ReturnsTheSameData()
        {
            GameDefinitionData data = CreateData();

            Assert.AreSame(data, RuleSettingsApplication.Apply(data, Array.Empty<RuleSettingValue>(), null));
            Assert.AreSame(data, RuleSettingsApplication.Apply(data, null, null));
        }

        [Test]
        public void HandSize_ReplacesTheControllerLimitAndKeepsTheOtherOptions()
        {
            GameDefinitionData result = RuleSettingsApplication.Apply(
                CreateData(),
                new[] { Setting(RuleSettingKeys.HandSize, "6") },
                null);

            Assert.AreEqual(6, result.ControllerConfiguration.MaximumHandSize);
            Assert.IsTrue(result.ControllerConfiguration.DrawToMaximumAtTurnStart);
            Assert.IsTrue(result.ControllerConfiguration.CostsAreAllOrNothing);
            Assert.IsFalse(result.ControllerConfiguration.SharedPaymentAllowed);
        }

        [Test]
        public void KeysNeeded_AppliesOnlyToTheListedMode_AndUpdatesTheKeyObjective()
        {
            GameDefinitionData result = RuleSettingsApplication.Apply(
                CreateData(),
                new[] { Setting(RuleSettingKeys.KeysNeeded, "2", Easy) },
                null);

            ModeDefinitionData easy = FindMode(result, Easy);
            ModeDefinitionData hard = FindMode(result, Hard);
            Assert.AreEqual(2, easy.RequiredKeyCount);
            Assert.AreEqual(2, easy.KeyObjective.AnyKeyCount);
            Assert.AreEqual(3, hard.RequiredKeyCount);
            Assert.AreEqual(3, hard.KeyObjective.AnyKeyCount);
        }

        [Test]
        public void WinMode_WithoutAModeFilter_AppliesToEveryMode()
        {
            GameDefinitionData result = RuleSettingsApplication.Apply(
                CreateData(),
                new[] { Setting(RuleSettingKeys.WinMode, "survival") },
                null);

            Assert.AreEqual(ModeBehavior.Survival, FindMode(result, Easy).Behavior);
            Assert.AreEqual(ModeBehavior.Survival, FindMode(result, Hard).Behavior);
        }

        [Test]
        public void UnreadableAndUnknownSettings_AreSkippedWithWarnings_AndKeepAuthoredValues()
        {
            List<string> warnings = new List<string>();
            GameDefinitionData result = RuleSettingsApplication.Apply(
                CreateData(),
                new[]
                {
                    Setting(RuleSettingKeys.HandSize, "ten"),
                    Setting(RuleSettingKeys.KeysNeeded, "-1", Easy),
                    Setting(RuleSettingKeys.WinMode, "Everyone"),
                    Setting("game.not-a-setting", "1"),
                },
                warnings);

            Assert.AreEqual(10, result.ControllerConfiguration.MaximumHandSize);
            Assert.AreEqual(1, FindMode(result, Easy).RequiredKeyCount);
            Assert.AreEqual(ModeBehavior.Team, FindMode(result, Easy).Behavior);
            Assert.AreEqual(4, warnings.Count);
        }

        [Test]
        public void UnchangedModes_AreKeptAsTheSameInstances()
        {
            GameDefinitionData data = CreateData();
            GameDefinitionData result = RuleSettingsApplication.Apply(
                data,
                new[] { Setting(RuleSettingKeys.KeysNeeded, "2", Easy) },
                null);

            Assert.AreSame(FindMode(data, Hard), FindMode(result, Hard));
            Assert.AreNotSame(FindMode(data, Easy), FindMode(result, Easy));
        }

        private static RuleSettingValue Setting(string key, string value, params string[] modes)
        {
            return new RuleSettingValue(key, value, modes);
        }

        private static ModeDefinitionData FindMode(GameDefinitionData data, string stableId)
        {
            for (int i = 0; i < data.Modes.Count; i++)
            {
                if (data.Modes[i].StableId == stableId)
                {
                    return data.Modes[i];
                }
            }

            Assert.Fail($"Mode '{stableId}' is missing.");
            return null;
        }

        private static GameDefinitionData CreateData()
        {
            CollapseConfigurationData collapse = new CollapseConfigurationData(CollapseScheduleKind.RoundBased, 0d, string.Empty);
            ModeDefinitionData easy = new ModeDefinitionData(Easy, "Easy", string.Empty, 1, 0, collapse, ModeBehavior.Team, string.Empty);
            ModeDefinitionData hard = new ModeDefinitionData(Hard, "Hard", string.Empty, 3, 0, collapse, ModeBehavior.Team, string.Empty);
            return new GameDefinitionData(
                Guid.NewGuid().ToString(),
                "Test Game",
                string.Empty,
                1,
                4,
                null,
                Array.Empty<CardDefinitionData>(),
                Array.Empty<GameContentSetData>(),
                Array.Empty<AvatarDefinitionData>(),
                new[] { easy, hard },
                Easy,
                null,
                Array.Empty<ControllerInput>(),
                new ControllerConfigurationData(10, true, true, true, false),
                ControllerMappingKind.None,
                string.Empty,
                string.Empty);
        }
    }
}
