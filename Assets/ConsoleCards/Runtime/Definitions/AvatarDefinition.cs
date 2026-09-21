using System;
using System.Collections.Generic;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    [Serializable]
    public sealed class AuthoredStat
    {
        [SerializeField] private string name;
        [SerializeField] private float value;

        internal AuthoredStatData ToData()
        {
            return new AuthoredStatData(name, value);
        }
    }

    [CreateAssetMenu(fileName = "AvatarDefinition", menuName = "Console Cards/Definitions/Avatar")]
    public sealed class AvatarDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private Texture2D artwork;
        [SerializeField] private string artworkReference;
        [SerializeField] private List<AuthoredStat> stats = new List<AuthoredStat>();
        [SerializeField] private List<CardDefinition> startingAbilities = new List<CardDefinition>();

        public Texture2D Artwork => artwork;

        public AvatarDefinitionData ToData()
        {
            List<AuthoredStatData> statData = new List<AuthoredStatData>(stats.Count);
            for (int i = 0; i < stats.Count; i++)
            {
                if (stats[i] == null) throw new InvalidOperationException("Avatar stats cannot contain null entries.");
                statData.Add(stats[i].ToData());
            }

            List<string> abilityIds = new List<string>(startingAbilities.Count);
            for (int i = 0; i < startingAbilities.Count; i++)
            {
                if (startingAbilities[i] == null) throw new InvalidOperationException("Starting Abilities cannot contain null entries.");
                abilityIds.Add(startingAbilities[i].StableId);
            }

            return new AvatarDefinitionData(stableId, displayName, artworkReference, statData, abilityIds);
        }
    }
}
