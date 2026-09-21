using System;

namespace ConsoleCards.GameTemplates.Definitions
{
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
            string metadata)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Mode ID is required.", nameof(stableId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Mode name is required.", nameof(displayName));
            if (requiredKeyCount < 0) throw new ArgumentOutOfRangeException(nameof(requiredKeyCount));
            if (startingAbilityCount < 0) throw new ArgumentOutOfRangeException(nameof(startingAbilityCount));

            StableId = stableId;
            DisplayName = displayName;
            ObjectiveConfiguration = objectiveConfiguration ?? string.Empty;
            RequiredKeyCount = requiredKeyCount;
            StartingAbilityCount = startingAbilityCount;
            Collapse = collapse ?? throw new ArgumentNullException(nameof(collapse));
            Behavior = behavior;
            Metadata = metadata ?? string.Empty;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string ObjectiveConfiguration { get; }
        public int RequiredKeyCount { get; }
        public int StartingAbilityCount { get; }
        public CollapseConfigurationData Collapse { get; }
        public ModeBehavior Behavior { get; }
        public string Metadata { get; }
    }
}
