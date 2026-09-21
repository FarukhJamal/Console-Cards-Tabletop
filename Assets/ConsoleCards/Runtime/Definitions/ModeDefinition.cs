using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    [CreateAssetMenu(fileName = "ModeDefinition", menuName = "Console Cards/Definitions/Mode")]
    public sealed class ModeDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string objectiveConfiguration;
        [SerializeField, Min(0)] private int requiredKeyCount;
        [SerializeField, Min(0)] private int startingAbilityCount;
        [SerializeField] private CollapseScheduleKind collapseSchedule;
        [SerializeField, Min(0f)] private float collapseInterval;
        [SerializeField, TextArea] private string collapseMetadata;
        [SerializeField] private ModeBehavior behavior;
        [SerializeField, TextArea] private string modeMetadata;

        public ModeDefinitionData ToData()
        {
            return new ModeDefinitionData(
                stableId,
                displayName,
                objectiveConfiguration,
                requiredKeyCount,
                startingAbilityCount,
                new CollapseConfigurationData(collapseSchedule, collapseInterval, collapseMetadata),
                behavior,
                modeMetadata);
        }
    }
}
