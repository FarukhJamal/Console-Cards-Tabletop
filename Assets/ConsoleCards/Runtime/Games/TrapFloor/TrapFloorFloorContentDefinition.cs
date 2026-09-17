using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Identifiers;

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
    /// Static Trap Floor content referenced by a Floor Card's authoritative Definition ID.
    /// Effect rules and reveal interaction are deliberately outside this definition.
    /// </summary>
    public sealed class TrapFloorFloorContentDefinition
    {
        public TrapFloorFloorContentDefinition(
            ObjectDefinitionId id,
            TrapFloorFloorContentCategory category,
            string displayName,
            string displayText)
            : this(
                id,
                category,
                displayName,
                displayText,
                TrapFloorFloorContentSource.ProvisionalStage03)
        {
        }

        public TrapFloorFloorContentDefinition(
            ObjectDefinitionId id,
            TrapFloorFloorContentCategory category,
            string displayName,
            string displayText,
            TrapFloorFloorContentSource contentSource)
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Floor content Definition ID cannot be empty.", nameof(id));
            }

            if (!Enum.IsDefined(typeof(TrapFloorFloorContentCategory), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }

            if (!Enum.IsDefined(typeof(TrapFloorFloorContentSource), contentSource))
            {
                throw new ArgumentOutOfRangeException(nameof(contentSource));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Floor content display name cannot be empty.", nameof(displayName));
            }

            Id = id;
            Category = category;
            DisplayName = displayName;
            DisplayText = displayText ?? throw new ArgumentNullException(nameof(displayText));
            ContentSource = contentSource;
        }

        public ObjectDefinitionId Id { get; }

        public TrapFloorFloorContentCategory Category { get; }

        public string DisplayName { get; }

        public string DisplayText { get; }

        public TrapFloorFloorContentSource ContentSource { get; }
    }

    /// <summary>
    /// Central provisional Stage-03 pool. Replace these definitions when the approved content changes;
    /// Template construction and Match assignment do not encode the category counts elsewhere.
    /// </summary>
    public static class TrapFloorStage03ContentPool
    {
        public const int TrapCount = 18;
        public const int FriendCount = 4;
        public const int KeyCount = 6;
        public const int SecretExitCount = 1;
        public const int EntryCount = 1;
        public const int AbilityCount = 6;
        public const int TotalCount = TrapCount
            + FriendCount
            + KeyCount
            + SecretExitCount
            + EntryCount
            + AbilityCount;

        // Stage-03 objective tuning belongs with the provisional content/configuration rather
        // than in the command logic. Change this value (or supply another configuration to the
        // Template factory) when the approved required-Key count changes.
        public const int DefaultRequiredKeyCount = KeyCount;

        public static IReadOnlyList<TrapFloorFloorContentDefinition> CreateDefinitions()
        {
            List<TrapFloorFloorContentDefinition> definitions =
                new List<TrapFloorFloorContentDefinition>(TotalCount);
            int stableIndex = 0;

            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Loose Planks",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 3-6, pass safely. On 1-2, return your Pawn to the previous usable Floor.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Dart Volley",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 4-6, pass safely. On 1-3, move your Pawn two orthogonal usable Floors toward Entry.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Spike Plate",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 5-6, pass safely. On 1-4, return your Pawn to Entry.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Swinging Blade",
                "PROVISIONAL STAGE-03 - Challenge: roll 2d6. On a total of 7 or more, pass safely. Otherwise return your Pawn to the previous usable Floor.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Falling Stones",
                "PROVISIONAL STAGE-03 - Challenge: roll 2d6. If either die shows 5 or 6, pass safely. Otherwise return your Pawn to Entry.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Poison Mist",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 4-6, pass safely. On 1-3, your next manual movement is limited to one orthogonal Floor.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Web Snare",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 3-6, break free. On 1-2, leave your Pawn here until another Player completes a Search.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Shock Tile",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. An even result passes safely. An odd result returns your Pawn to the previous usable Floor.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Flame Jets",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 5-6, pass safely. On 1-4, move your Pawn two orthogonal usable Floors toward Entry.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Frost Slide",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 3-6, stop safely. On 1-2, move one extra Floor in the direction you entered; if that Floor is unusable, return to the previous usable Floor.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Crushing Walls",
                "PROVISIONAL STAGE-03 - Challenge: roll 2d6. Doubles or a total of 9 or more passes safely. Any other result returns your Pawn to Entry.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Echoing Alarm",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 4-6, silence it. On 1-3, choose one orthogonally adjacent unrevealed Floor and Search/Reveal it immediately.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Quicksand",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 4-6, escape. On 1-3, remain here and limit your next manual movement to one orthogonal Floor.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Chain Snare",
                "PROVISIONAL STAGE-03 - Challenge: another Player rolls 1d6 for you. On 4-6, you are freed. On 1-3, return your Pawn to the previous usable Floor.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Rune Lock",
                "PROVISIONAL STAGE-03 - Challenge: name Trap, Friend, Key, Exit, Entry, or Ability, then Search/Reveal one orthogonally adjacent Floor. If its category matches, pass; otherwise return to the previous usable Floor.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Darkness",
                "PROVISIONAL STAGE-03 - Challenge: roll 1d6. On 3-6, pass safely. On 1-2, do not Search again until another Player reveals a Floor.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "Pressure Chamber",
                "PROVISIONAL STAGE-03 - Challenge: choose to return your Pawn to Entry, or roll 1d6. On 4-6, remain here safely; on 1-3, the group loses.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Trap,
                "The Final Drop",
                "PROVISIONAL STAGE-03 - Challenge: roll 2d6. On a total of 8 or more, survive. On 7 or less, the group loses.",
                TrapFloorFloorContentSource.ProvisionalStage03);

            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Friend,
                "The Scout",
                "PROVISIONAL STAGE-03 - One-time manual effect: choose one orthogonally adjacent, uncollapsed Floor and Search/Reveal it. Then treat this Friend effect as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Friend,
                "The Guide",
                "PROVISIONAL STAGE-03 - One-time manual effect: move one Pawn up to three orthogonal usable Floors. Then treat this Friend effect as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Friend,
                "The Tinkerer",
                "PROVISIONAL STAGE-03 - One-time manual effect: reroll one die from a Trap challenge and use the new result. Then treat this Friend effect as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Friend,
                "The Rescuer",
                "PROVISIONAL STAGE-03 - One-time manual effect: return any Pawn to Entry instead of applying a Trap movement consequence to it. Then treat this Friend effect as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);

            for (int keyIndex = 1; keyIndex <= KeyCount; keyIndex++)
            {
                AddDefinition(
                    definitions,
                    ref stableIndex,
                    TrapFloorFloorContentCategory.Key,
                    $"Key {keyIndex:00}",
                    "CURRENT STAGE-03 OBJECTIVE - This revealed Key remains UNCLAIMED until a Player chooses Claim Key. A claimed Key contributes to the shared KEYS X / Y objective.",
                    TrapFloorFloorContentSource.CurrentStage03Reference);
            }

            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.SecretExit,
                "Secret Exit",
                "CURRENT STAGE-03 OBJECTIVE - After revealing this Floor, choose Attempt Escape. Escape succeeds only when the configured required number of Keys has been claimed.",
                TrapFloorFloorContentSource.CurrentStage03Reference);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Entry,
                "Entry",
                "PROVISIONAL STAGE-03 REFERENCE - This marks the Entry Floor. No additional Entry effect is currently defined; use it as a shared physical reference.",
                TrapFloorFloorContentSource.ProvisionalStage03);

            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Ability,
                "Sure Footing",
                "PROVISIONAL STAGE-03 - One-time manual ability: treat one failed Trap roll made for your Pawn as a successful result, then treat this Ability as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Ability,
                "Second Chance",
                "PROVISIONAL STAGE-03 - One-time manual ability: reroll all dice from one Trap challenge affecting your Pawn and use the new result, then treat this Ability as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Ability,
                "Long Step",
                "PROVISIONAL STAGE-03 - One-time manual ability: move your Pawn up to four orthogonal usable Floors, then treat this Ability as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Ability,
                "Swap Places",
                "PROVISIONAL STAGE-03 - One-time manual ability: exchange your Pawn's Floor with another Pawn on a usable Floor, then treat this Ability as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Ability,
                "Safe Search",
                "PROVISIONAL STAGE-03 - One-time manual ability: Search/Reveal one orthogonally adjacent, uncollapsed Floor without moving your Pawn, then treat this Ability as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);
            AddDefinition(
                definitions,
                ref stableIndex,
                TrapFloorFloorContentCategory.Ability,
                "Bridge Step",
                "PROVISIONAL STAGE-03 - One-time manual ability: move across one collapsed Floor to the next orthogonally adjacent usable Floor; do not stop on the hole. Then treat this Ability as spent.",
                TrapFloorFloorContentSource.ProvisionalStage03);

            return new ReadOnlyCollection<TrapFloorFloorContentDefinition>(definitions);
        }

        private static void AddDefinition(
            ICollection<TrapFloorFloorContentDefinition> definitions,
            ref int stableIndex,
            TrapFloorFloorContentCategory category,
            string displayName,
            string displayText,
            TrapFloorFloorContentSource contentSource)
        {
            stableIndex++;
            definitions.Add(new TrapFloorFloorContentDefinition(
                new ObjectDefinitionId(CreateStableGuid(stableIndex)),
                category,
                displayName,
                displayText,
                contentSource));
        }

        private static Guid CreateStableGuid(int index)
        {
            return new Guid(
                unchecked((int)0x54460015),
                unchecked((short)0x4f4f),
                unchecked((short)0x4000),
                0x80,
                0x00,
                0x00,
                0x15,
                (byte)(index >> 24),
                (byte)(index >> 16),
                (byte)(index >> 8),
                (byte)index);
        }
    }

    public sealed class TrapFloorStage03Configuration
    {
        public TrapFloorStage03Configuration(int requiredKeyCount)
        {
            if (requiredKeyCount < 1 || requiredKeyCount > TrapFloorStage03ContentPool.KeyCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredKeyCount),
                    $"Required Key count must be between 1 and {TrapFloorStage03ContentPool.KeyCount}.");
            }

            RequiredKeyCount = requiredKeyCount;
        }

        public int RequiredKeyCount { get; }

        public static TrapFloorStage03Configuration CreateDefault()
        {
            return new TrapFloorStage03Configuration(
                TrapFloorStage03ContentPool.DefaultRequiredKeyCount);
        }
    }
}
