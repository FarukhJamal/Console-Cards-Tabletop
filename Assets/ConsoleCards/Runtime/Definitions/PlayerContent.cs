using System;
using System.Collections.Generic;

namespace ConsoleCards.Definitions
{
    /// <summary>
    /// A player's own cards for one game (doc 27 List 2, L2-1): new cards and changes to the game's default cards.
    /// Saved in the player's profile as games/&lt;game id&gt;/cards.json (doc 28), and carried by game packs from
    /// format 2 (doc 24). Serialised with Unity's JsonUtility, so every field is a plain public field.
    /// </summary>
    [Serializable]
    public sealed class PlayerCardsFile
    {
        public const string FileName = "cards.json";
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public string gameStableId = string.Empty;
        public List<PlayerCardRecord> cards = new List<PlayerCardRecord>();
        public List<CardChangeRecord> changes = new List<CardChangeRecord>();

        public bool IsEmpty => (cards == null || cards.Count == 0) && (changes == null || changes.Count == 0);

        /// <summary>A new, unique card ID (a GUID, as every card of the platform has).</summary>
        public static string NewCardId() => Guid.NewGuid().ToString("D");

        /// <summary>Lists are never null after this (JsonUtility leaves missing ones null in old files).</summary>
        public PlayerCardsFile Normalised()
        {
            if (cards == null) cards = new List<PlayerCardRecord>();
            if (changes == null) changes = new List<CardChangeRecord>();
            return this;
        }

        /// <summary>
        /// One set of cards from two layers: <paramref name="lower"/> (the active pack) under <paramref name="upper"/>
        /// (the player's own). A card or change with the same ID in the upper layer wins.
        /// </summary>
        public static PlayerCardsFile Merge(PlayerCardsFile lower, PlayerCardsFile upper)
        {
            PlayerCardsFile merged = new PlayerCardsFile();
            lower?.Normalised();
            upper?.Normalised();
            merged.gameStableId = PlayerContent.FirstNonEmpty(upper?.gameStableId, lower?.gameStableId);
            PlayerContent.MergeById(merged.cards, upper?.cards, lower?.cards, c => c?.id);
            PlayerContent.MergeById(merged.changes, upper?.changes, lower?.changes, c => c?.cardId);
            return merged;
        }
    }

    /// <summary>
    /// A player's decks for one game, saved in the profile as games/&lt;game id&gt;/decks.json (doc 28); also carried
    /// by game packs from format 2.
    /// </summary>
    [Serializable]
    public sealed class PlayerDecksFile
    {
        public const string FileName = "decks.json";
        public const int CurrentFormatVersion = 1;

        public int formatVersion = CurrentFormatVersion;
        public string gameStableId = string.Empty;
        public List<PlayerDeckRecord> decks = new List<PlayerDeckRecord>();

        public bool IsEmpty => decks == null || decks.Count == 0;

        public PlayerDecksFile Normalised()
        {
            if (decks == null) decks = new List<PlayerDeckRecord>();
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i] != null && decks[i].cards == null) decks[i].cards = new List<PlayerDeckEntry>();
            }

            return this;
        }

        /// <summary>The pack's decks under the player's own; same ID: the player's wins.</summary>
        public static PlayerDecksFile Merge(PlayerDecksFile lower, PlayerDecksFile upper)
        {
            PlayerDecksFile merged = new PlayerDecksFile();
            lower?.Normalised();
            upper?.Normalised();
            merged.gameStableId = PlayerContent.FirstNonEmpty(upper?.gameStableId, lower?.gameStableId);
            PlayerContent.MergeById(merged.decks, upper?.decks, lower?.decks, d => d?.id);
            return merged.Normalised();
        }
    }

    /// <summary>
    /// A new card: the blank base card with its own words, cost and pictures, working like an existing card of the
    /// game (<see cref="behavesLike"/>, for example a new Trap that works like Fall).
    /// </summary>
    [Serializable]
    public sealed class PlayerCardRecord
    {
        /// <summary>Made for you (<see cref="PlayerCardsFile.NewCardId"/>); never changes.</summary>
        public string id = string.Empty;

        /// <summary>Optional short code the player chooses, shown with the card.</summary>
        public string code = string.Empty;

        public string name = string.Empty;
        public string description = string.Empty;

        /// <summary>The stable ID of the game card whose kind and effect this card uses.</summary>
        public string behavesLike = string.Empty;

        public int quantity = 1;

        /// <summary>Card cost notation (for example "Up + A/B/X/Y"); empty keeps the model card's cost.</summary>
        public string cost = string.Empty;

        /// <summary>
        /// The front and back pictures, by art key. A picture the player chooses is copied into their profile
        /// (games/&lt;game id&gt;/uploads, doc 28) under this key. Empty front: <see cref="PlayerContent.DefaultArtKey"/>;
        /// empty back: the back of the card it behaves like.
        /// </summary>
        public string frontArtKey = string.Empty;
        public string backArtKey = string.Empty;

        /// <summary>
        /// The 8 icons on the card front (ICON-1) as symbol names, "Top,Right,Bottom,Left,TopRight,BottomRight,
        /// BottomLeft,TopLeft" order (for example "Square,None,None,None,None,None,None,Circle"). Empty: no icons.
        /// </summary>
        public string icons = string.Empty;

        /// <summary>The frame variant ID (FRAME-1) of the game's Card Frame Style; empty: the game's default frame.</summary>
        public string frame = string.Empty;
    }

    /// <summary>
    /// A change to one of the game's default cards. Only the fields marked as changed apply; Reset is removing the
    /// record. The built-in card itself is never changed.
    /// </summary>
    [Serializable]
    public sealed class CardChangeRecord
    {
        public string cardId = string.Empty;

        public bool changeName;
        public string name = string.Empty;

        public bool changeDescription;
        public string description = string.Empty;

        public bool changeQuantity;
        public int quantity = 1;

        public bool changeCost;
        public string cost = string.Empty;

        public bool changeFrontArt;
        public string frontArtKey = string.Empty;

        public bool changeBackArt;
        public string backArtKey = string.Empty;

        /// <summary>New icons for the card front (ICON-1), as in <see cref="PlayerCardRecord.icons"/>.</summary>
        public bool changeIcons;
        public string icons = string.Empty;

        /// <summary>Another frame variant (FRAME-1), as in <see cref="PlayerCardRecord.frame"/>.</summary>
        public bool changeFrame;
        public string frame = string.Empty;

        public bool HasAnyChange =>
            changeName || changeDescription || changeQuantity || changeCost || changeFrontArt || changeBackArt
            || changeIcons || changeFrame;
    }

    /// <summary>A player's deck: a name and cards with counts, game cards or your own (used by games from L2-5).</summary>
    [Serializable]
    public sealed class PlayerDeckRecord
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public List<PlayerDeckEntry> cards = new List<PlayerDeckEntry>();
    }

    [Serializable]
    public sealed class PlayerDeckEntry
    {
        public string cardId = string.Empty;
        public int count = 1;
    }

    public static class PlayerContent
    {
        internal static string FirstNonEmpty(string first, string second) =>
            !string.IsNullOrEmpty(first) ? first : second ?? string.Empty;

        internal static void MergeById<T>(List<T> into, List<T> upper, List<T> lower, Func<T, string> id)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddNew(into, upper, id, seen);
            AddNew(into, lower, id, seen);
        }

        private static void AddNew<T>(List<T> into, List<T> from, Func<T, string> id, HashSet<string> seen)
        {
            if (from == null) return;
            for (int i = 0; i < from.Count; i++)
            {
                string key = id(from[i]);
                if (!string.IsNullOrWhiteSpace(key) && seen.Add(key)) into.Add(from[i]);
            }
        }

        /// <summary>
        /// The art key of a player card's picture: "&lt;game folder&gt;/player/&lt;card id&gt;/front" (or "/back"). The game
        /// folder is the first part of the game's own art keys (doc 24), for Trap Floor "trap-floor".
        /// </summary>
        public static string DefaultArtKey(string gameArtFolder, string cardId, bool front)
        {
            string folder = string.IsNullOrWhiteSpace(gameArtFolder) ? "game" : gameArtFolder.Trim().ToLowerInvariant();
            return $"{folder}/player/{(cardId ?? string.Empty).Trim().ToLowerInvariant()}/{(front ? "front" : "back")}";
        }
    }
}
