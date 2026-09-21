using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConsoleCards.GameTemplates.Definitions
{
    [Serializable]
    public sealed class AuthoredStatData
    {
        public AuthoredStatData(string name, double value)
        {
            Name = name ?? string.Empty;
            Value = value;
        }

        public string Name { get; }
        public double Value { get; }
    }

    [Serializable]
    public sealed class AvatarDefinitionData
    {
        private readonly ReadOnlyCollection<AuthoredStatData> stats;
        private readonly ReadOnlyCollection<string> startingAbilityIds;

        public AvatarDefinitionData(
            string stableId,
            string displayName,
            string artworkReference,
            IEnumerable<AuthoredStatData> stats,
            IEnumerable<string> startingAbilityIds)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Avatar ID is required.", nameof(stableId));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Avatar name is required.", nameof(displayName));

            StableId = stableId;
            DisplayName = displayName;
            ArtworkReference = artworkReference ?? string.Empty;
            this.stats = new ReadOnlyCollection<AuthoredStatData>(new List<AuthoredStatData>(stats ?? throw new ArgumentNullException(nameof(stats))));
            this.startingAbilityIds = new ReadOnlyCollection<string>(new List<string>(startingAbilityIds ?? throw new ArgumentNullException(nameof(startingAbilityIds))));
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string ArtworkReference { get; }
        public IReadOnlyList<AuthoredStatData> Stats => stats;
        public IReadOnlyList<string> StartingAbilityIds => startingAbilityIds;
    }
}
