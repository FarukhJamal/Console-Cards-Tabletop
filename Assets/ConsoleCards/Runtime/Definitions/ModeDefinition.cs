using System;
using System.Collections.Generic;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    [Serializable]
    public sealed class AuthoredKeyRequirement
    {
        [SerializeField] private CardDefinition keyDefinition;
        [SerializeField, Min(1)] private int count = 1;

        internal KeyRequirementData ToData()
        {
            if (keyDefinition == null)
                throw new InvalidOperationException("Specific Key requirements require a Card Definition.");
            return new KeyRequirementData(keyDefinition.StableId, count);
        }
    }

    [CreateAssetMenu(fileName = "ModeDefinition", menuName = "Console Cards/Definitions/Mode")]
    public sealed class ModeDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string objectiveConfiguration;
        [SerializeField, Min(0)] private int requiredKeyCount;
        [SerializeField] private KeyObjectiveRequirementKind keyObjectiveRequirementKind;
        [SerializeField] private List<AuthoredKeyRequirement> specificKeyRequirements =
            new List<AuthoredKeyRequirement>();
        [SerializeField, Min(0)] private int startingAbilityCount;
        [SerializeField] private CollapseScheduleKind collapseSchedule;
        [SerializeField, Min(0f)] private float collapseInterval;
        [SerializeField, TextArea] private string collapseMetadata;
        [SerializeField] private ModeBehavior behavior;
        [SerializeField, TextArea] private string modeMetadata;

        public ModeDefinitionData ToData()
        {
            List<KeyRequirementData> keyRequirements =
                new List<KeyRequirementData>(specificKeyRequirements.Count);
            for (int i = 0; i < specificKeyRequirements.Count; i++)
            {
                if (specificKeyRequirements[i] == null)
                    throw new InvalidOperationException("Specific Key requirements cannot contain null entries.");
                keyRequirements.Add(specificKeyRequirements[i].ToData());
            }

            return new ModeDefinitionData(
                stableId,
                displayName,
                objectiveConfiguration,
                requiredKeyCount,
                startingAbilityCount,
                new CollapseConfigurationData(collapseSchedule, collapseInterval, collapseMetadata),
                behavior,
                modeMetadata,
                new KeyObjectiveConfigurationData(
                    keyObjectiveRequirementKind,
                    requiredKeyCount,
                    keyRequirements));
        }
    }
}
