using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConsoleCards.GameTemplates.Definitions
{
    [Serializable]
    public sealed class KeyRequirementData
    {
        public KeyRequirementData(string keyDefinitionId, int count)
        {
            if (string.IsNullOrWhiteSpace(keyDefinitionId))
                throw new ArgumentException("Key Definition ID is required.", nameof(keyDefinitionId));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));

            KeyDefinitionId = keyDefinitionId;
            Count = count;
        }

        public string KeyDefinitionId { get; }
        public int Count { get; }
    }

    [Serializable]
    public sealed class KeyObjectiveConfigurationData
    {
        private readonly ReadOnlyCollection<KeyRequirementData> specificKeyRequirements;

        public KeyObjectiveConfigurationData(
            KeyObjectiveRequirementKind requirementKind,
            int anyKeyCount,
            IEnumerable<KeyRequirementData> specificKeyRequirements)
        {
            if (!Enum.IsDefined(typeof(KeyObjectiveRequirementKind), requirementKind))
                throw new ArgumentOutOfRangeException(nameof(requirementKind));
            if (anyKeyCount < 0) throw new ArgumentOutOfRangeException(nameof(anyKeyCount));

            RequirementKind = requirementKind;
            AnyKeyCount = anyKeyCount;
            this.specificKeyRequirements = new ReadOnlyCollection<KeyRequirementData>(
                new List<KeyRequirementData>(specificKeyRequirements
                    ?? throw new ArgumentNullException(nameof(specificKeyRequirements))));
        }

        public KeyObjectiveRequirementKind RequirementKind { get; }
        public int AnyKeyCount { get; }
        public IReadOnlyList<KeyRequirementData> SpecificKeyRequirements => specificKeyRequirements;
    }

    [Serializable]
    public sealed class CollapseConfigurationData
    {
        public CollapseConfigurationData(CollapseScheduleKind scheduleKind, double interval, string metadata)
        {
            if (interval < 0d) throw new ArgumentOutOfRangeException(nameof(interval));
            ScheduleKind = scheduleKind;
            Interval = interval;
            Metadata = metadata ?? string.Empty;
        }

        public CollapseScheduleKind ScheduleKind { get; }
        public double Interval { get; }
        public string Metadata { get; }
    }

    [Serializable]
    public sealed class ModeDefinitionData
    {
        public ModeDefinitionData(
            string stableId,
            string displayName,
            string objectiveConfiguration,
            int requiredKeyCount,
            int startingAbilityCount,
            CollapseConfigurationData collapse,
            ModeBehavior behavior,
            string metadata,
            KeyObjectiveConfigurationData keyObjective = null)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Mode ID is required.", nameof(stableId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Mode name is required.", nameof(displayName));
            if (requiredKeyCount < 0) throw new ArgumentOutOfRangeException(nameof(requiredKeyCount));
            if (startingAbilityCount < 0) throw new ArgumentOutOfRangeException(nameof(startingAbilityCount));

            StableId = stableId;
            DisplayName = displayName;
            ObjectiveConfiguration = objectiveConfiguration ?? string.Empty;
            RequiredKeyCount = requiredKeyCount;
            KeyObjective = keyObjective ?? new KeyObjectiveConfigurationData(
                KeyObjectiveRequirementKind.AnyKeyCount,
                requiredKeyCount,
                Array.Empty<KeyRequirementData>());
            StartingAbilityCount = startingAbilityCount;
            Collapse = collapse ?? throw new ArgumentNullException(nameof(collapse));
            Behavior = behavior;
            Metadata = metadata ?? string.Empty;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string ObjectiveConfiguration { get; }
        public int RequiredKeyCount { get; }
        public KeyObjectiveConfigurationData KeyObjective { get; }
        public int StartingAbilityCount { get; }
        public CollapseConfigurationData Collapse { get; }
        public ModeBehavior Behavior { get; }
        public string Metadata { get; }
    }
}
