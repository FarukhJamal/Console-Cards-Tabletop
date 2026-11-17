using System;
using System.Collections.Generic;
using System.Globalization;

namespace ConsoleCards.GameTemplates.Definitions
{
    /// <summary>The platform setting keys a rule set can carry (doc 23, R2a/R2b). Each game decides which it uses.</summary>
    public static class RuleSettingKeys
    {
        /// <summary>Number: the Controller hand limit drawn up to at the start of a turn.</summary>
        public const string HandSize = "controller.hand-size";

        /// <summary>Number, per mode: Keys required to escape.</summary>
        public const string KeysNeeded = "mode.keys-needed";

        /// <summary>Choice, all or some modes: Team (one player must escape) or Survival (all must escape).</summary>
        public const string WinMode = "game.win-mode";

        /// <summary>
        /// Number, prefix: "floor.count." + a content set ID sets how many cards the setup draws from that set
        /// (for example floor.count.trap-floor-floor-traps). Passed through to the game's setup.
        /// </summary>
        public const string FloorCountPrefix = "floor.count.";

        /// <summary>
        /// Text, prefix: "floor.deck." + the floor size ("6x6") sets that floor's deck for the modes it applies to
        /// (owner, 2026-11-17): "Friends/Abilities/Fall Traps", for example "4/10/4"; Traps fill the rest of the floor
        /// after the Keys and Exits. Passed through to the game's setup, which checks it fits (see
        /// <see cref="TryParseFloorDeck"/>).
        /// </summary>
        public const string FloorDeckPrefix = "floor.deck.";

        /// <summary>Reads a floor deck value "Friends/Abilities/Fall Traps" (three whole numbers, 0 or more).</summary>
        public static bool TryParseFloorDeck(string value, out int friends, out int abilities, out int fallTraps)
        {
            friends = abilities = fallTraps = 0;
            string[] parts = (value ?? string.Empty).Split('/');
            return parts.Length == 3
                && int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out friends)
                && int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out abilities)
                && int.TryParse(parts[2].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out fallTraps);
        }

        /// <summary>Choice: how floor cards are placed, <see cref="FloorPlacementRandom"/> or <see cref="FloorPlacementPattern"/>.</summary>
        public const string FloorPlacement = "floor.placement";

        /// <summary>
        /// Text, prefix: "card.cost." + a card's stable ID sets that card's input cost in card notation, for
        /// example "Up + A/B/X/Y" (see <see cref="InputCostNotation"/>). Empty means the card has no cost yet.
        /// </summary>
        public const string CardCostPrefix = "card.cost.";

        /// <summary>
        /// Number: how many tiles a player may move on their turn (doc 23, R4). Passed through; hints read it to
        /// light the tiles in reach and to explain a longer move. Nothing is blocked.
        /// </summary>
        public const string MoveTiles = "move.tiles";

        /// <summary>
        /// Number, prefix: "move.tiles.avatar." + an Avatar's stable ID gives that Avatar its own move count in
        /// place of <see cref="MoveTiles"/> (for example the Athlete). Passed through.
        /// </summary>
        public const string MoveTilesAvatarPrefix = "move.tiles.avatar.";

        /// <summary>
        /// Toggle: whether the game's Shop is open (Trap Floor, doc 18 §15.4). Passed through. Unlike the other
        /// settings it applies at once when the table agrees to a change, not at restart.
        /// </summary>
        public const string ShopEnabled = "shop.enabled";

        public const string FloorPlacementRandom = "random";
        public const string FloorPlacementPattern = "pattern";

        /// <summary>
        /// Stage 2 (owner, 2026-11-17): the players lay the floor out themselves ("by the players"); the game deals it
        /// at random otherwise ("at random").
        /// </summary>
        public const string FloorPlacementPlayers = "by the players";

        /// <summary>The floor placement value, read: random (also "at random"), pattern or players (also "by the players").</summary>
        public static string ReadFloorPlacement(string value)
        {
            string v = (value ?? string.Empty).Trim();
            if (v.Equals(FloorPlacementRandom, StringComparison.OrdinalIgnoreCase)
                || v.Equals("at random", StringComparison.OrdinalIgnoreCase)) return FloorPlacementRandom;
            if (v.Equals(FloorPlacementPattern, StringComparison.OrdinalIgnoreCase)) return FloorPlacementPattern;
            if (v.Equals(FloorPlacementPlayers, StringComparison.OrdinalIgnoreCase)
                || v.Equals("players", StringComparison.OrdinalIgnoreCase)) return FloorPlacementPlayers;
            return null;
        }

        /// <summary>Stage 2: the players lay the floor out themselves.</summary>
        public static bool IsFloorPlacedByPlayers(string value) =>
            string.Equals(ReadFloorPlacement(value), FloorPlacementPlayers, StringComparison.Ordinal);

        /// <summary>True for an on/off value that means on (on, true, yes); false for anything else.</summary>
        public static bool IsToggleOn(string value)
        {
            string v = (value ?? string.Empty).Trim();
            return v.Equals("on", StringComparison.OrdinalIgnoreCase)
                || v.Equals("true", StringComparison.OrdinalIgnoreCase)
                || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when the value is a recognised on/off word.</summary>
        public static bool IsToggleValue(string value)
        {
            string v = (value ?? string.Empty).Trim();
            return IsToggleOn(v)
                || v.Equals("off", StringComparison.OrdinalIgnoreCase)
                || v.Equals("false", StringComparison.OrdinalIgnoreCase)
                || v.Equals("no", StringComparison.OrdinalIgnoreCase);
        }
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
    /// Applies a rule set's settings to game data before a template is built (doc 23, R2a/R2b). The game's services
    /// already read everything from this data, so rules change play without code changes. Floor settings are
    /// checked and passed through for the setup to read; card costs replace the authored ones (C1). Settings it
    /// cannot read are skipped with a warning and the authored value is kept.
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
            List<RuleSettingValue> passedThrough = new List<RuleSettingValue>(data.RuleSettings);
            Dictionary<string, InputCostData> costs = null;
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
                else if (setting.Key.StartsWith(RuleSettingKeys.CardCostPrefix, StringComparison.Ordinal))
                {
                    string cardId = setting.Key.Substring(RuleSettingKeys.CardCostPrefix.Length);
                    if (!data.TryGetCard(cardId, out CardDefinitionData card))
                    {
                        Warn(warnings, setting, $"the game has no card '{cardId}'");
                    }
                    else if (!InputCostNotation.TryParse(setting.Value, out InputCostData cost, out string error))
                    {
                        Warn(warnings, setting, error);
                    }
                    else
                    {
                        costs = costs ?? new Dictionary<string, InputCostData>(StringComparer.OrdinalIgnoreCase);
                        costs[card.StableId] = cost;
                    }
                }
                else if (setting.Key.StartsWith(RuleSettingKeys.FloorCountPrefix, StringComparison.Ordinal))
                {
                    string contentSetId = setting.Key.Substring(RuleSettingKeys.FloorCountPrefix.Length);
                    if (!data.TryGetContentSet(contentSetId, out _))
                    {
                        Warn(warnings, setting, $"the game has no content set '{contentSetId}'");
                    }
                    else if (TryReadCount(setting, warnings, out _))
                    {
                        passedThrough.Add(setting);
                    }
                }
                else if (setting.Key.StartsWith(RuleSettingKeys.MoveTilesAvatarPrefix, StringComparison.Ordinal))
                {
                    string avatarId = setting.Key.Substring(RuleSettingKeys.MoveTilesAvatarPrefix.Length);
                    if (!HasAvatar(data, avatarId))
                    {
                        Warn(warnings, setting, $"the game has no Avatar '{avatarId}'");
                    }
                    else if (TryReadCount(setting, warnings, out _))
                    {
                        passedThrough.Add(setting);
                    }
                }
                else if (setting.Key.StartsWith(RuleSettingKeys.FloorDeckPrefix, StringComparison.Ordinal))
                {
                    // Passed through even when it does not read, so the game's setup check can say what is wrong.
                    if (!RuleSettingKeys.TryParseFloorDeck(setting.Value, out _, out _, out _))
                        Warn(warnings, setting, "expected Friends/Abilities/Fall Traps, for example 4/10/4");
                    passedThrough.Add(setting);
                }
                else if (string.Equals(setting.Key, RuleSettingKeys.MoveTiles, StringComparison.Ordinal))
                {
                    if (TryReadCount(setting, warnings, out _))
                    {
                        passedThrough.Add(setting);
                    }
                }
                else if (string.Equals(setting.Key, RuleSettingKeys.FloorPlacement, StringComparison.Ordinal))
                {
                    string placement = RuleSettingKeys.ReadFloorPlacement(setting.Value);
                    if (placement != null)
                    {
                        passedThrough.Add(new RuleSettingValue(
                            setting.Key,
                            placement,
                            setting.ModeStableIds));
                    }
                    else
                    {
                        Warn(warnings, setting, "expected at random or by the players");
                    }
                }
                else if (string.Equals(setting.Key, RuleSettingKeys.ShopEnabled, StringComparison.Ordinal))
                {
                    if (RuleSettingKeys.IsToggleValue(setting.Value)) passedThrough.Add(setting);
                    else Warn(warnings, setting, "expected on or off");
                }
                else if (!string.Equals(setting.Key, RuleSettingKeys.KeysNeeded, StringComparison.Ordinal)
                    && !string.Equals(setting.Key, RuleSettingKeys.WinMode, StringComparison.Ordinal))
                {
                    Warn(warnings, setting, "unknown setting key");
                }
            }

            IReadOnlyList<CardDefinitionData> cards = data.Cards;
            if (costs != null)
            {
                List<CardDefinitionData> costed = new List<CardDefinitionData>(data.Cards.Count);
                for (int c = 0; c < data.Cards.Count; c++)
                {
                    CardDefinitionData card = data.Cards[c];
                    costed.Add(costs.TryGetValue(card.StableId, out InputCostData cost) ? card.WithInputCost(cost) : card);
                }

                cards = costed;
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
                cards,
                data.ContentSets,
                data.Avatars,
                modes,
                data.DefaultModeStableId,
                data.Console,
                data.InputVocabulary,
                controller,
                data.ControllerMappingKind,
                data.PresentationReference,
                data.AssistanceConfiguration,
                passedThrough);
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

        private static bool HasAvatar(GameDefinitionData data, string avatarId)
        {
            for (int i = 0; i < data.Avatars.Count; i++)
            {
                if (data.Avatars[i] != null
                    && string.Equals(data.Avatars[i].StableId, avatarId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

        // A per-mode setting is read once for each mode it applies to, so the same warning is reported once.
        private static void Warn(ICollection<string> warnings, RuleSettingValue setting, string reason)
        {
            string warning = $"Rule setting '{setting.Key}' = '{setting.Value}' was skipped: {reason}.";
            if (warnings != null && !warnings.Contains(warning))
            {
                warnings.Add(warning);
            }
        }
    }
}
