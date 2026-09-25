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
        BlindNextRound = 2,
        SlowNextRound = 3,
        StickyNextRound = 4,
    }

    public enum TrapFloorBlindMovementDirection
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,
    }

    /// <summary>Authored d4 interpretation for the Blind Trap; it is Game data, not Presentation logic.</summary>
    public sealed class TrapFloorBlindDirectionMapping
    {
        private readonly TrapFloorBlindMovementDirection[] directions;

        public TrapFloorBlindDirectionMapping(
            TrapFloorBlindMovementDirection one,
            TrapFloorBlindMovementDirection two,
            TrapFloorBlindMovementDirection three,
            TrapFloorBlindMovementDirection four)
        {
            directions = new[] { one, two, three, four };
        }

        public TrapFloorBlindMovementDirection Resolve(int d4Result)
        {
            if (d4Result < 1 || d4Result > 4) throw new ArgumentOutOfRangeException(nameof(d4Result));
            return directions[d4Result - 1];
        }

        internal TrapFloorBlindDirectionMapping Copy() =>
            new TrapFloorBlindDirectionMapping(
                directions[0],
                directions[1],
                directions[2],
                directions[3]);
    }

    /// <summary>
    /// Trap Floor interpretation of one authored Card Definition used in the Floor content set.
    /// Effect execution remains player-enforced or feature-specific assistance.
    /// </summary>
    public sealed class TrapFloorFloorContentDefinition
    {
        public const string EliminateForCurrentRoundEffectMetadata =
            "trap-effect-eliminate-for-current-round";
        public const string BlindNextRoundEffectMetadata = "trap-effect-blind-next-round";
        public const string SlowNextRoundEffectMetadata = "trap-effect-slow-next-round";
        public const string StickyNextRoundEffectMetadata = "trap-effect-sticky-next-round";
        private const string BlindDirectionMappingPrefix = "d4-directions=";
        private const string MovementModifierPrefix = "movement-modifier=";
        private const string MovementAllowedPrefix = "movement-allowed=";

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
            BlindDirectionMapping = TrapEffect == TrapFloorTrapEffectCategory.BlindNextRound
                ? ParseBlindDirectionMapping(card.EffectMetadata)
                : null;
            MovementModifier = TrapEffect == TrapFloorTrapEffectCategory.SlowNextRound
                ? ParseMovementModifier(card.EffectMetadata)
                : (int?)null;
            MovementAllowed = TrapEffect == TrapFloorTrapEffectCategory.StickyNextRound
                ? ParseMovementAllowed(card.EffectMetadata)
                : (bool?)null;
            AuthoredCard = card;
        }

        public TrapFloorFloorContentDefinition(
            ObjectDefinitionId id,
            TrapFloorFloorContentCategory category,
            string displayName,
            string displayText,
            TrapFloorFloorContentSource contentSource = TrapFloorFloorContentSource.ProvisionalStage03,
            TrapFloorTrapEffectCategory trapEffect = TrapFloorTrapEffectCategory.InformationalManual,
            TrapFloorBlindDirectionMapping blindDirectionMapping = null,
            int? movementModifier = null,
            bool? movementAllowed = null)
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
            if ((trapEffect == TrapFloorTrapEffectCategory.BlindNextRound) != (blindDirectionMapping != null))
                throw new ArgumentException("Blind Trap assistance requires exactly one authored d4 direction mapping.", nameof(blindDirectionMapping));
            if ((trapEffect == TrapFloorTrapEffectCategory.SlowNextRound) != movementModifier.HasValue
                || movementModifier == 0)
                throw new ArgumentException("Slow Trap assistance requires one non-zero authored movement modifier.", nameof(movementModifier));
            if ((trapEffect == TrapFloorTrapEffectCategory.StickyNextRound) != movementAllowed.HasValue
                || movementAllowed == true)
                throw new ArgumentException("Sticky Trap assistance requires authored movement-allowed=false guidance.", nameof(movementAllowed));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Floor content display name cannot be empty.", nameof(displayName));

            Id = id;
            Category = category;
            DisplayName = displayName;
            DisplayText = displayText ?? throw new ArgumentNullException(nameof(displayText));
            ContentSource = contentSource;
            TrapEffect = trapEffect;
            BlindDirectionMapping = blindDirectionMapping?.Copy();
            MovementModifier = movementModifier;
            MovementAllowed = movementAllowed;
        }

        public ObjectDefinitionId Id { get; }
        public TrapFloorFloorContentCategory Category { get; }
        public string DisplayName { get; }
        public string DisplayText { get; }
        public TrapFloorFloorContentSource ContentSource { get; }
        public TrapFloorTrapEffectCategory TrapEffect { get; }
        public TrapFloorBlindDirectionMapping BlindDirectionMapping { get; }
        public int? MovementModifier { get; }
        public bool? MovementAllowed { get; }
        public CardDefinitionData AuthoredCard { get; }

        public bool HasSupportedAssistedTrapEffect =>
            Category == TrapFloorFloorContentCategory.Trap
            && (TrapEffect == TrapFloorTrapEffectCategory.EliminateForCurrentRound
                || TrapEffect == TrapFloorTrapEffectCategory.BlindNextRound
                || TrapEffect == TrapFloorTrapEffectCategory.SlowNextRound
                || TrapEffect == TrapFloorTrapEffectCategory.StickyNextRound);

        private static TrapFloorTrapEffectCategory ResolveTrapEffect(
            CardDefinitionData card,
            TrapFloorFloorContentCategory category)
        {
            if (category != TrapFloorFloorContentCategory.Trap)
                return TrapFloorTrapEffectCategory.InformationalManual;

            string effectIdentifier = EffectIdentifier(card.EffectMetadata);
            if (string.Equals(
                    effectIdentifier,
                    EliminateForCurrentRoundEffectMetadata,
                    StringComparison.OrdinalIgnoreCase))
            {
                return TrapFloorTrapEffectCategory.EliminateForCurrentRound;
            }
            if (string.Equals(
                    effectIdentifier,
                    BlindNextRoundEffectMetadata,
                    StringComparison.OrdinalIgnoreCase))
                return TrapFloorTrapEffectCategory.BlindNextRound;
            if (string.Equals(
                    effectIdentifier,
                    SlowNextRoundEffectMetadata,
                    StringComparison.OrdinalIgnoreCase))
                return TrapFloorTrapEffectCategory.SlowNextRound;
            return string.Equals(
                    effectIdentifier,
                    StickyNextRoundEffectMetadata,
                    StringComparison.OrdinalIgnoreCase)
                ? TrapFloorTrapEffectCategory.StickyNextRound
                : TrapFloorTrapEffectCategory.InformationalManual;
        }

        private static string EffectIdentifier(string metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata)) return string.Empty;
            int separator = metadata.IndexOf(';');
            return (separator < 0 ? metadata : metadata.Substring(0, separator)).Trim();
        }

        private static TrapFloorBlindDirectionMapping ParseBlindDirectionMapping(string metadata)
        {
            string[] segments = (metadata ?? string.Empty).Split(';');
            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (!segment.StartsWith(BlindDirectionMappingPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] authoredDirections = segment.Substring(BlindDirectionMappingPrefix.Length).Split(',');
                TrapFloorBlindMovementDirection[] mapping = new TrapFloorBlindMovementDirection[4];
                bool[] assigned = new bool[4];
                for (int directionIndex = 0; directionIndex < authoredDirections.Length; directionIndex++)
                {
                    string[] pair = authoredDirections[directionIndex].Split(':');
                    if (pair.Length != 2
                        || !int.TryParse(pair[0].Trim(), out int result)
                        || result < 1
                        || result > 4
                        || assigned[result - 1]
                        || !Enum.TryParse(pair[1].Trim(), true, out TrapFloorBlindMovementDirection direction)
                        || !Enum.IsDefined(typeof(TrapFloorBlindMovementDirection), direction))
                    {
                        throw new ArgumentException("Blind Trap d4 direction mapping is invalid.", nameof(metadata));
                    }
                    mapping[result - 1] = direction;
                    assigned[result - 1] = true;
                }

                for (int resultIndex = 0; resultIndex < assigned.Length; resultIndex++)
                {
                    if (!assigned[resultIndex])
                        throw new ArgumentException("Blind Trap d4 direction mapping must author all four results.", nameof(metadata));
                }
                return new TrapFloorBlindDirectionMapping(mapping[0], mapping[1], mapping[2], mapping[3]);
            }

            throw new ArgumentException("Blind Trap effect metadata requires an authored d4 direction mapping.", nameof(metadata));
        }

        private static int ParseMovementModifier(string metadata)
        {
            string[] segments = (metadata ?? string.Empty).Split(';');
            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (!segment.StartsWith(MovementModifierPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (int.TryParse(
                        segment.Substring(MovementModifierPrefix.Length).Trim(),
                        out int modifier)
                    && modifier != 0)
                    return modifier;
                break;
            }

            throw new ArgumentException("Slow Trap effect metadata requires a non-zero authored movement modifier.", nameof(metadata));
        }

        private static bool ParseMovementAllowed(string metadata)
        {
            string[] segments = (metadata ?? string.Empty).Split(';');
            for (int i = 1; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (!segment.StartsWith(MovementAllowedPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (bool.TryParse(
                        segment.Substring(MovementAllowedPrefix.Length).Trim(),
                        out bool movementAllowed)
                    && !movementAllowed)
                    return false;
                break;
            }

            throw new ArgumentException("Sticky Trap effect metadata requires movement-allowed=false.", nameof(metadata));
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
