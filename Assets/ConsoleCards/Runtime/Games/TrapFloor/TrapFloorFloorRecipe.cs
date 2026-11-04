using System;
using System.Collections.Generic;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// Owner, 2026-11-04 (TRAP CARDS rules): what one floor of a game mode holds. The mode (Team Play, Free for all,
    /// Survival) sets how many floors the game has, the Keys on this floor, whether it has a Secret Exit, and the Keys
    /// the Grand Exit needs. The rest of the floor is either the players' own floor deck (<see cref="TileCounts"/>,
    /// card ID to copies, scaled to the cells left) or the game's floor groups weighted by the difficulty ("trap cards
    /// and ability cards are more if the difficulty is high; the easier, the more assisting cards").
    /// </summary>
    public sealed class TrapFloorFloorRecipe
    {
        public TrapFloorFloorRecipe(int floorCount, int keyCount, bool secretExit, int requiredKeys)
            : this(floorCount, keyCount, secretExit, requiredKeys, 1d, 1d, 1d, null)
        {
        }

        public TrapFloorFloorRecipe(
            int floorCount,
            int keyCount,
            bool secretExit,
            int requiredKeys,
            double trapWeight,
            double abilityWeight,
            double friendWeight,
            IReadOnlyDictionary<string, int> tileCounts,
            string excludedTag = null)
        {
            ExcludedTag = excludedTag;
            if (floorCount < 1) throw new ArgumentOutOfRangeException(nameof(floorCount));
            if (keyCount < 0) throw new ArgumentOutOfRangeException(nameof(keyCount));
            if (requiredKeys < 1) throw new ArgumentOutOfRangeException(nameof(requiredKeys));
            FloorCount = floorCount;
            KeyCount = keyCount;
            SecretExit = secretExit;
            RequiredKeys = requiredKeys;
            TrapWeight = Math.Max(0d, trapWeight);
            AbilityWeight = Math.Max(0d, abilityWeight);
            FriendWeight = Math.Max(0d, friendWeight);
            TileCounts = tileCounts;
        }

        public int FloorCount { get; }

        /// <summary>A card tag this mode leaves off the floor (Free for all: "lasso"); null: none.</summary>
        public string ExcludedTag { get; }

        internal bool Excludes(CardDefinitionData card)
        {
            if (string.IsNullOrEmpty(ExcludedTag) || card == null || card.Tags == null) return false;
            for (int i = 0; i < card.Tags.Count; i++)
            {
                if (string.Equals(card.Tags[i], ExcludedTag, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        public int KeyCount { get; }

        public bool SecretExit { get; }

        public int RequiredKeys { get; }

        public double TrapWeight { get; }

        public double AbilityWeight { get; }

        public double FriendWeight { get; }

        /// <summary>The players' floor deck: card ID to copies (Traps, Friends, Abilities); null or empty: the game's mix.</summary>
        public IReadOnlyDictionary<string, int> TileCounts { get; }

        internal bool HasTileCounts
        {
            get
            {
                if (TileCounts == null) return false;
                foreach (KeyValuePair<string, int> entry in TileCounts)
                {
                    if (entry.Value > 0) return true;
                }

                return false;
            }
        }

        internal double WeightFor(TrapFloorFloorContentCategory category)
        {
            switch (category)
            {
                case TrapFloorFloorContentCategory.Trap: return TrapWeight;
                case TrapFloorFloorContentCategory.Ability: return AbilityWeight;
                case TrapFloorFloorContentCategory.Friend: return FriendWeight;
                default: return 1d;
            }
        }
    }
}
