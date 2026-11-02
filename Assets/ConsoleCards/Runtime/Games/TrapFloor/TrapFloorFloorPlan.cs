using System;
using System.Collections.Generic;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// FLOORDECK-1 (owner, 2026-11-02: "floor decks … this next floor will be a different"): each floor of the game
    /// can have its own floor deck, the cards that floor is dealt from (with copies) and the back its tiles show.
    /// Floor n (0 = the first) without a deck is dealt from every floor card, as before. A plan with more floors than
    /// the board can shrink to adds floors at the smallest size, like extra rounds of a game show.
    /// </summary>
    public sealed class TrapFloorFloorPlan
    {
        private readonly IReadOnlyDictionary<string, int>[] cards;
        private readonly string[] backs;

        /// <param name="floorCards">Per floor (index 0 = Floor 1): card ID to copies; null for a floor without a deck.</param>
        /// <param name="floorBacks">Per floor: the back's art key; null or empty for the game's own back.</param>
        public TrapFloorFloorPlan(IReadOnlyList<IReadOnlyDictionary<string, int>> floorCards, IReadOnlyList<string> floorBacks)
        {
            if (floorCards == null) throw new ArgumentNullException(nameof(floorCards));
            cards = new IReadOnlyDictionary<string, int>[floorCards.Count];
            backs = new string[floorCards.Count];
            for (int i = 0; i < floorCards.Count; i++)
            {
                cards[i] = floorCards[i];
                backs[i] = floorBacks != null && i < floorBacks.Count ? floorBacks[i] ?? string.Empty : string.Empty;
            }
        }

        /// <summary>How many floors the decks name (the highest floor with a deck).</summary>
        public int FloorCount => cards.Length;

        /// <summary>The cards floor <paramref name="floorIndex"/> is dealt from; false when that floor has no deck.</summary>
        public bool TryGetCards(int floorIndex, out IReadOnlyDictionary<string, int> floorCards)
        {
            floorCards = floorIndex >= 0 && floorIndex < cards.Length ? cards[floorIndex] : null;
            return floorCards != null;
        }

        /// <summary>The back's art key for floor <paramref name="floorIndex"/>; empty for the game's own back.</summary>
        public string BackOf(int floorIndex) =>
            floorIndex >= 0 && floorIndex < backs.Length ? backs[floorIndex] : string.Empty;
    }
}
