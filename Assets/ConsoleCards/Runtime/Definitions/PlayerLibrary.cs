using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace ConsoleCards.Definitions
{
    /// <summary>
    /// Doc 63 (owner, 2026-11-21): "Your cards", the player's own library of cards and decks, not tied to any game.
    /// It is kept in the player's profile only (games/your-cards/: cards.json, decks.json, uploads/), never in the
    /// project's GameData, so it is never in git and goes with the profile's games/ folder to an online room. Its
    /// cards are plain cards: no rules, effects, tags, Console behaviour or controller input; they need no "Works
    /// like" and take their look (size, back) from <see cref="PlainModelId"/>, the library's one plain card.
    /// </summary>
    public static class PlayerLibrary
    {
        public const string Id = "your-cards";
        public const string DisplayName = "Your cards";

        /// <summary>The first part of the library's art keys ("your-cards/player/&lt;card id&gt;/front").</summary>
        public const string ArtFolder = Id;

        /// <summary>The plain card every library card is made from (not a GUID, so it is never a card on the table).</summary>
        public const string PlainModelId = "your-cards-plain";

        public static bool IsLibrary(string gameId) => string.Equals(gameId, Id, StringComparison.OrdinalIgnoreCase);

        public static bool IsLibrary(GameDefinition game) => game != null && IsLibrary(game.StableId);

        /// <summary>True for an art key of the library ("your-cards/…"). No allocation.</summary>
        public static bool IsLibraryArtKey(string key) =>
            key != null
            && key.Length > ArtFolder.Length
            && key[ArtFolder.Length] == '/'
            && key.StartsWith(ArtFolder, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The library as a runtime game (see <see cref="GameDefinition.CreateRuntime"/>) with its plain card, which
        /// borrows the look of <paramref name="lookFrom"/> (a game card; null: a blank card). Destroy it with
        /// <see cref="Destroy"/>.
        /// </summary>
        public static GameDefinition CreateDefinition(CardDefinition lookFrom)
        {
            CardDefinition plain = CardDefinition.CreatePlain(lookFrom, PlainModelId, "Plain card", ArtFolder + "/plain/front");
            GameContentSet set = new GameContentSet(PlainModelId, "Plain card", new List<CardDefinition> { plain });
            return GameDefinition.CreateRuntime(Id, DisplayName, set);
        }

        /// <summary>The library's plain card, or null when <paramref name="library"/> is not the library.</summary>
        public static CardDefinition PlainModelOf(GameDefinition library)
        {
            if (!IsLibrary(library) || library.ContentSets.Count == 0 || library.ContentSets[0] == null) return null;
            IReadOnlyList<CardDefinition> cards = library.ContentSets[0].Cards;
            return cards.Count > 0 ? cards[0] : null;
        }

        /// <summary>Destroys a library made by <see cref="CreateDefinition"/> and its plain card.</summary>
        public static void Destroy(GameDefinition library)
        {
            if (library == null) return;
            DestroyRuntime(PlainModelOf(library));
            DestroyRuntime(library);
        }

        private static void DestroyRuntime(Object target)
        {
            if (target == null) return;
            if (UnityEngine.Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
