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
        {
            if (id.IsEmpty)
            {
                throw new ArgumentException("Floor content Definition ID cannot be empty.", nameof(id));
            }

            if (!Enum.IsDefined(typeof(TrapFloorFloorContentCategory), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Floor content display name cannot be empty.", nameof(displayName));
            }

            Id = id;
            Category = category;
            DisplayName = displayName;
            DisplayText = displayText ?? throw new ArgumentNullException(nameof(displayText));
        }

        public ObjectDefinitionId Id { get; }

        public TrapFloorFloorContentCategory Category { get; }

        public string DisplayName { get; }

        public string DisplayText { get; }
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

        public static IReadOnlyList<TrapFloorFloorContentDefinition> CreateDefinitions()
        {
            List<TrapFloorFloorContentDefinition> definitions =
                new List<TrapFloorFloorContentDefinition>(TotalCount);
            int stableIndex = 0;
            AddCategory(definitions, TrapFloorFloorContentCategory.Trap, "Trap", TrapCount, ref stableIndex);
            AddCategory(definitions, TrapFloorFloorContentCategory.Friend, "Friend", FriendCount, ref stableIndex);
            AddCategory(definitions, TrapFloorFloorContentCategory.Key, "Key", KeyCount, ref stableIndex);
            AddCategory(
                definitions,
                TrapFloorFloorContentCategory.SecretExit,
                "Secret Exit",
                SecretExitCount,
                ref stableIndex);
            AddCategory(definitions, TrapFloorFloorContentCategory.Entry, "Entry", EntryCount, ref stableIndex);
            AddCategory(definitions, TrapFloorFloorContentCategory.Ability, "Ability", AbilityCount, ref stableIndex);
            return new ReadOnlyCollection<TrapFloorFloorContentDefinition>(definitions);
        }

        private static void AddCategory(
            ICollection<TrapFloorFloorContentDefinition> definitions,
            TrapFloorFloorContentCategory category,
            string categoryName,
            int count,
            ref int stableIndex)
        {
            for (int categoryIndex = 1; categoryIndex <= count; categoryIndex++)
            {
                stableIndex++;
                string displayName = count == 1
                    ? categoryName
                    : $"{categoryName} {categoryIndex:00}";
                definitions.Add(new TrapFloorFloorContentDefinition(
                    new ObjectDefinitionId(CreateStableGuid(stableIndex)),
                    category,
                    displayName,
                    categoryName));
            }
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
}
