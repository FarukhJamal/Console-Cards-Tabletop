using System;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorFloorContentCategory
    {
        Trap = 0,
        Friend = 1,
        Key = 2,
        SecretExit = 3,
        Entry = 4,
        Ability = 5,
    }

    public enum TrapFloorFloorContentSource
    {
        CurrentStage03Reference = 0,
        ProvisionalStage03 = 1,
    }

    /// <summary>
    /// Small authored assistance category for Trap consequences. The Card's readable text remains
    /// authoritative for manual resolution; only explicitly supported categories gain automation.
    /// </summary>
    public enum TrapFloorTrapEffectCategory
    {
        InformationalManual = 0,
        EliminateForCurrentRound = 1,
    }

    /// <summary>
    /// Trap Floor interpretation of one authored Card Definition used in the Floor content set.
    /// Effect execution remains player-enforced or feature-specific assistance.
    /// </summary>
    public sealed class TrapFloorFloorContentDefinition
    {
        public const string EliminateForCurrentRoundEffectTag =
            "trap-effect-eliminate-for-current-round";

        public TrapFloorFloorContentDefinition(CardDefinitionData card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (!Guid.TryParse(card.StableId, out Guid stableId))
            {
                throw new ArgumentException(
                    $"Trap Floor Floor Card '{card.DisplayName}' has an invalid stable GUID '{card.StableId}'.",
                    nameof(card));
            }

            if (!Enum.TryParse(card.Category, true, out TrapFloorFloorContentCategory category))
            {
                throw new ArgumentException(
                    $"Trap Floor Floor Card '{card.DisplayName}' has unsupported category '{card.Category}'.",
                    nameof(card));
            }

            Id = new ObjectDefinitionId(stableId);
            Category = category;
            DisplayName = card.DisplayName;
            DisplayText = card.Description;
            ContentSource = HasTag(card, "current-stage03-reference")
                ? TrapFloorFloorContentSource.CurrentStage03Reference
                : TrapFloorFloorContentSource.ProvisionalStage03;
            TrapEffect = ResolveTrapEffect(card, category);
            AuthoredCard = card;
        }

        public TrapFloorFloorContentDefinition(
            ObjectDefinitionId id,
            TrapFloorFloorContentCategory category,
            string displayName,
            string displayText,
            TrapFloorFloorContentSource contentSource = TrapFloorFloorContentSource.ProvisionalStage03,
            TrapFloorTrapEffectCategory trapEffect = TrapFloorTrapEffectCategory.InformationalManual)
        {
            if (id.IsEmpty) throw new ArgumentException("Floor content Definition ID cannot be empty.", nameof(id));
            if (!Enum.IsDefined(typeof(TrapFloorFloorContentCategory), category))
                throw new ArgumentOutOfRangeException(nameof(category));
            if (!Enum.IsDefined(typeof(TrapFloorFloorContentSource), contentSource))
                throw new ArgumentOutOfRangeException(nameof(contentSource));
            if (!Enum.IsDefined(typeof(TrapFloorTrapEffectCategory), trapEffect))
                throw new ArgumentOutOfRangeException(nameof(trapEffect));
            if (category != TrapFloorFloorContentCategory.Trap
                && trapEffect != TrapFloorTrapEffectCategory.InformationalManual)
                throw new ArgumentException("Only Trap Floor Trap content can define an assisted Trap effect.", nameof(trapEffect));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Floor content display name cannot be empty.", nameof(displayName));

            Id = id;
            Category = category;
            DisplayName = displayName;
            DisplayText = displayText ?? throw new ArgumentNullException(nameof(displayText));
            ContentSource = contentSource;
            TrapEffect = trapEffect;
        }

        public ObjectDefinitionId Id { get; }
        public TrapFloorFloorContentCategory Category { get; }
        public string DisplayName { get; }
        public string DisplayText { get; }
        public TrapFloorFloorContentSource ContentSource { get; }
        public TrapFloorTrapEffectCategory TrapEffect { get; }
        public CardDefinitionData AuthoredCard { get; }

        public bool HasSupportedAssistedTrapEffect =>
            Category == TrapFloorFloorContentCategory.Trap
            && TrapEffect == TrapFloorTrapEffectCategory.EliminateForCurrentRound;

        private static TrapFloorTrapEffectCategory ResolveTrapEffect(
            CardDefinitionData card,
            TrapFloorFloorContentCategory category)
        {
            if (category != TrapFloorFloorContentCategory.Trap)
                return TrapFloorTrapEffectCategory.InformationalManual;

            return HasTag(card, EliminateForCurrentRoundEffectTag)
                ? TrapFloorTrapEffectCategory.EliminateForCurrentRound
                : TrapFloorTrapEffectCategory.InformationalManual;
        }

        private static bool HasTag(CardDefinitionData card, string tag)
        {
            for (int i = 0; i < card.Tags.Count; i++)
            {
                if (string.Equals(card.Tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Compatibility input retained for older callers. Authored Mode Definition data is the active
    /// production source for required Keys and the rest of the Mode configuration.
    /// </summary>
    public sealed class TrapFloorStage03Configuration
    {
        public TrapFloorStage03Configuration(int requiredKeyCount)
        {
            if (requiredKeyCount < 1) throw new ArgumentOutOfRangeException(nameof(requiredKeyCount));
            RequiredKeyCount = requiredKeyCount;
        }

        public int RequiredKeyCount { get; }
    }
}
