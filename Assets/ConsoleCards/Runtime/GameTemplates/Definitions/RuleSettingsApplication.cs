using System;
using System.Collections.Generic;
using System.Globalization;

namespace ConsoleCards.GameTemplates.Definitions
{
    /// <summary>The platform setting keys a rule set can carry (doc 23, R2a). Each game decides which it uses.</summary>
    public static class RuleSettingKeys
    {
        /// <summary>Number: the Controller hand limit drawn up to at the start of a turn.</summary>
        public const string HandSize = "controller.hand-size";

        /// <summary>Number, per mode: Keys required to escape.</summary>
        public const string KeysNeeded = "mode.keys-needed";

        /// <summary>Choice, all or some modes: Team (one player must escape) or Survival (all must escape).</summary>
        public const string WinMode = "game.win-mode";
    }

    /// <summary>One typed setting taken from a rule set: its key, raw value and the modes it applies to.</summary>
    public readonly struct RuleSettingValue
    {
        public RuleSettingValue(string key, string value, IReadOnlyList<string> modeStableIds)
        {
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
            ModeStableIds = modeStableIds ?? Array.Empty<string>();
        }

        public string Key { get; }
        public string Value { get; }

        /// <summary>Modes the setting applies to; empty means every mode.</summary>
        public IReadOnlyList<string> ModeStableIds { get; }

        public bool AppliesToMode(string modeStableId)
        {
            if (ModeStableIds.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < ModeStableIds.Count; i++)
            {
                if (string.Equals(ModeStableIds[i], modeStableId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Applies a rule set's settings to game data before a template is built (doc 23, R2a). The game's services
    /// already read everything from this data, so rules change play without code changes. Settings it cannot
    /// read are skipped with a warning and the authored value is kept.
    /// </summary>
    public static class RuleSettingsApplication
    {
        public static GameDefinitionData Apply(
            GameDefinitionData data,
            IReadOnlyList<RuleSettingValue> settings,
            ICollection<string> warnings)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (settings == null || settings.Count == 0)
            {
                return data;
            }

            ControllerConfigurationData controller = data.ControllerConfiguration;
            for (int i = 0; i < settings.Count; i++)
            {
                RuleSettingValue setting = settings[i];
                if (string.Equals(setting.Key, RuleSettingKeys.HandSize, StringComparison.Ordinal))
                {
                    if (controller == null)
                    {
                        Warn(warnings, setting, "the game has no Controller configuration");
                    }
                    else if (TryReadCount(setting, warnings, out int handSize))
                    {
                        controller = new ControllerConfigurationData(
                            handSize,
                            controller.DrawToMaximumAtTurnStart,
                            controller.UnusedCardsCarryOver,
                            controller.CostsAreAllOrNothing,
                            controller.SharedPaymentAllowed);
                    }
                }
                else if (!string.Equals(setting.Key, RuleSettingKeys.KeysNeeded, StringComparison.Ordinal)
                    && !string.Equals(setting.Key, RuleSettingKeys.WinMode, StringComparison.Ordinal))
                {
                    Warn(warnings, setting, "unknown setting key");
                }
            }

            List<ModeDefinitionData> modes = new List<ModeDefinitionData>(data.Modes.Count);
            for (int m = 0; m < data.Modes.Count; m++)
            {
                modes.Add(ApplyToMode(data.Modes[m], settings, warnings));
            }

            return new GameDefinitionData(
                data.StableId,
                data.DisplayName,
                data.ManualRules,
                data.MinimumPlayers,
                data.MaximumPlayers,
                data.Grid,
                data.Cards,
                data.ContentSets,
                data.Avatars,
                modes,
                data.DefaultModeStableId,
                data.Console,
                data.InputVocabulary,
                controller,
                data.ControllerMappingKind,
                data.PresentationReference,
                data.AssistanceConfiguration);
        }

        private static ModeDefinitionData ApplyToMode(
            ModeDefinitionData mode,
            IReadOnlyList<RuleSettingValue> settings,
            ICollection<string> warnings)
        {
            int requiredKeys = mode.RequiredKeyCount;
            ModeBehavior behavior = mode.Behavior;
            bool changed = false;
            for (int i = 0; i < settings.Count; i++)
            {
                RuleSettingValue setting = settings[i];
                if (!setting.AppliesToMode(mode.StableId))
                {
                    continue;
                }

                if (string.Equals(setting.Key, RuleSettingKeys.KeysNeeded, StringComparison.Ordinal))
                {
                    if (TryReadCount(setting, warnings, out int keys))
                    {
                        requiredKeys = keys;
                        changed = true;
                    }
                }
                else if (string.Equals(setting.Key, RuleSettingKeys.WinMode, StringComparison.Ordinal))
                {
                    if (Enum.TryParse(setting.Value.Trim(), true, out ModeBehavior parsed)
                        && Enum.IsDefined(typeof(ModeBehavior), parsed))
                    {
                        behavior = parsed;
                        changed = true;
                    }
                    else
                    {
                        Warn(warnings, setting, "expected Team or Survival");
                    }
                }
            }

            if (!changed)
            {
                return mode;
            }

            KeyObjectiveConfigurationData keyObjective = mode.KeyObjective;
            if (keyObjective != null && keyObjective.RequirementKind == KeyObjectiveRequirementKind.AnyKeyCount)
            {
                keyObjective = new KeyObjectiveConfigurationData(
                    keyObjective.RequirementKind,
                    requiredKeys,
                    keyObjective.SpecificKeyRequirements);
            }

            return new ModeDefinitionData(
                mode.StableId,
                mode.DisplayName,
                mode.ObjectiveConfiguration,
                requiredKeys,
                mode.StartingAbilityCount,
                mode.Collapse,
                behavior,
                mode.Metadata,
                keyObjective);
        }

        private static bool TryReadCount(RuleSettingValue setting, ICollection<string> warnings, out int count)
        {
            if (int.TryParse(setting.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                && count >= 0)
            {
                return true;
            }

            Warn(warnings, setting, "expected a whole number of 0 or more");
            count = 0;
            return false;
        }

        private static void Warn(ICollection<string> warnings, RuleSettingValue setting, string reason)
        {
            warnings?.Add($"Rule setting '{setting.Key}' = '{setting.Value}' was skipped: {reason}.");
        }
    }
}
