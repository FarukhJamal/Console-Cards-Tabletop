using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ConsoleCards.Presentation.Saving
{
    /// <summary>
    /// A game a player made (doc 29): its name, players and description, its written rules, and the table it starts
    /// with, made from the platform's components (boards, Consoles, decks of game or player cards, loose cards,
    /// pawns, tokens, dice). Saved in the player's profile as my-games/&lt;id&gt;.json. Serialised with JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class PlayerGameFile
    {
        public const int CurrentFormatVersion = 1;
        public const int MaximumNameLength = 40;
        public const int MaximumPlayers = 8;

        public int formatVersion = CurrentFormatVersion;
        public string id = string.Empty;
        public string name = string.Empty;
        public string description = string.Empty;
        public int minimumPlayers = 1;
        public int maximumPlayers = 4;

        /// <summary>
        /// The written rules, one line per rule. A line starting with "#" is a section heading ("# How to play").
        /// Players follow the rules themselves; nothing is enforced.
        /// </summary>
        public string rulesText = string.Empty;

        public string createdUtc = string.Empty;
        public string savedUtc = string.Empty;
        public SavedTable table = new SavedTable();

        /// <summary>
        /// The game's own deck (L2-6b): one of the player's decks or a game's deck, by reference, so changes to that
        /// deck are used the next time the game starts. Start puts it on the table, shuffled, where it lay.
        /// </summary>
        public SavedGameDeck deck = new SavedGameDeck();

        /// <summary>
        /// How the game deals (DEAL-1), as <see cref="ConsoleCards.Definitions.DealRule"/> text
        /// ("count=5;packet=1;face=down;to=hand;when=setup"); empty: no dealing. Set in the Deal step.
        /// </summary>
        public string deal = string.Empty;

        /// <summary>About how long a game takes, in minutes (Make a game › ABOUT); 0: not said.</summary>
        public int minutes;

        public PlayerGameFile Normalised()
        {
            if (table == null) table = new SavedTable();
            if (deck == null) deck = new SavedGameDeck();
            if (deck.pose == null) deck.pose = new SavedPose();
            table.Normalised();
            minimumPlayers = Mathf.Clamp(minimumPlayers, 1, MaximumPlayers);
            maximumPlayers = Mathf.Clamp(maximumPlayers, minimumPlayers, MaximumPlayers);
            return this;
        }

        public static string NewId() => "game-" + Guid.NewGuid().ToString("N").Substring(0, 12);

        public static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// What is on a table, by kind, position and contents: the table save file (doc 29). Built from the table and
    /// laid back out by the game; it never holds Unity objects, only IDs and numbers.
    /// </summary>
    [Serializable]
    public sealed class SavedTable
    {
        public List<SavedPiece> pieces = new List<SavedPiece>();
        public List<SavedPile> piles = new List<SavedPile>();
        public List<SavedConsole> consoles = new List<SavedConsole>();
        public List<SavedBoard> boards = new List<SavedBoard>();

        /// <summary>The drawings on the table: pen and highlighter lines and labels (drawing tool).</summary>
        public List<SavedMark> marks = new List<SavedMark>();

        public int Count => pieces.Count + piles.Count + consoles.Count + boards.Count + marks.Count;

        public SavedTable Normalised()
        {
            if (pieces == null) pieces = new List<SavedPiece>();
            if (piles == null) piles = new List<SavedPile>();
            if (consoles == null) consoles = new List<SavedConsole>();
            if (boards == null) boards = new List<SavedBoard>();
            if (marks == null) marks = new List<SavedMark>();
            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i] != null && marks[i].points == null) marks[i].points = new List<float>();
            }

            for (int i = 0; i < piles.Count; i++)
            {
                if (piles[i] != null && piles[i].cards == null) piles[i].cards = new List<string>();
            }

            return this;
        }
    }

    /// <summary>
    /// One drawing on the table (drawing tool): a pen or highlighter line, or a label. Its points are table
    /// coordinates with a height above the table's top, three numbers per point (x, y, height); a label has one.
    /// </summary>
    [Serializable]
    public sealed class SavedMark
    {
        public const string Pen = "pen";
        public const string Highlighter = "highlighter";
        public const string Label = "label";

        public string kind = Pen;
        public int style; // 0 plain, 1 brush, 2 swirl, 3 wavy, 4 dashed, 5 dotted
        public float r;
        public float g;
        public float b;
        public float a = 1f;
        public float width;
        public string text = string.Empty;
        public float yaw;
        public List<float> points = new List<float>();
    }

    /// <summary>
    /// A game's own deck (L2-6b): the game whose cards it uses, which of that game's decks ("game": the game's own
    /// deck; otherwise one of the player's decks for it), its name when saved, and where it lies on the table.
    /// </summary>
    [Serializable]
    public sealed class SavedGameDeck
    {
        public const string GameDeck = "game";

        public string gameId = string.Empty;
        public string deckId = string.Empty;
        public string name = string.Empty;
        public SavedPose pose = new SavedPose();

        public bool IsSet => !string.IsNullOrWhiteSpace(gameId) && !string.IsNullOrWhiteSpace(deckId);
    }

    /// <summary>A position on the table (the table's own coordinates).</summary>
    [Serializable]
    public sealed class SavedPose
    {
        public double x;
        public double y;
        public float rotation;
        public int layer;
        public int order;
    }

    /// <summary>A loose piece: a card (with its card ID and face), a pawn, a token or a die (with its sides).</summary>
    [Serializable]
    public sealed class SavedPiece
    {
        public const string Card = "card";
        public const string Pawn = "pawn";
        public const string Token = "token";
        public const string Die = "die";

        public string kind = Card;
        public string definitionId = string.Empty;
        public bool faceDown;
        public int sides;
        public SavedPose pose = new SavedPose();
    }

    /// <summary>A deck, stack or discard pile, with its cards from bottom to top.</summary>
    [Serializable]
    public sealed class SavedPile
    {
        public const string Deck = "deck";
        public const string Stack = "stack";
        public const string DiscardPile = "discard";

        public string kind = Deck;
        public int arrivalFace;
        public List<string> cards = new List<string>();
        public SavedPose pose = new SavedPose();
    }

    /// <summary>A Console mat and which side is up.</summary>
    [Serializable]
    public sealed class SavedConsole
    {
        public bool showsBack;
        public SavedPose pose = new SavedPose();
    }

    /// <summary>A game board and its grid.</summary>
    [Serializable]
    public sealed class SavedBoard
    {
        public int columns;
        public int rows;
        public float spaceWidth;
        public float spaceDepth;
        public float gap;
        public float frame;
        public SavedPose pose = new SavedPose();
    }

    /// <summary>The player's own games in their profile (doc 29): my-games/&lt;id&gt;.json, through <see cref="SaveFiles"/>.</summary>
    public sealed class PlayerGameStore
    {
        public PlayerGameStore(string folder)
        {
            Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        }

        public static PlayerGameStore ForProfile(ProfileService profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            profiles.EnsureActive();
            return new PlayerGameStore(profiles.MyGamesFolder);
        }

        public string Folder { get; }

        public string PathOf(string gameId) => System.IO.Path.Combine(Folder, SaveFiles.SafeName(gameId, "game") + ".json");

        /// <summary>Every readable game, by name. Files made by a newer version are skipped with a warning.</summary>
        public List<PlayerGameFile> List()
        {
            List<PlayerGameFile> games = new List<PlayerGameFile>();
            if (!Directory.Exists(Folder)) return games;
            foreach (string path in Directory.GetFiles(Folder, "*.json"))
            {
                PlayerGameFile game = SaveFiles.ReadJson<PlayerGameFile>(path, "Your games");
                if (game == null) continue;
                if (game.formatVersion > PlayerGameFile.CurrentFormatVersion)
                {
                    Debug.LogWarning($"[Your games] {path} was made by a newer version; skipped.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(game.id)) game.id = System.IO.Path.GetFileNameWithoutExtension(path);
                games.Add(game.Normalised());
            }

            games.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return games;
        }

        public PlayerGameFile Load(string gameId)
        {
            PlayerGameFile game = SaveFiles.ReadJson<PlayerGameFile>(PathOf(gameId), "Your games");
            return game != null && game.formatVersion <= PlayerGameFile.CurrentFormatVersion ? game.Normalised() : null;
        }

        public bool Save(PlayerGameFile game, out string error)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.formatVersion = PlayerGameFile.CurrentFormatVersion;
            if (string.IsNullOrWhiteSpace(game.id)) game.id = PlayerGameFile.NewId();
            if (string.IsNullOrEmpty(game.createdUtc)) game.createdUtc = PlayerGameFile.Now();
            game.savedUtc = PlayerGameFile.Now();
            return SaveFiles.TryWriteJson(PathOf(game.id), game.Normalised(), out error);
        }

        public bool Delete(string gameId, out string error)
        {
            error = string.Empty;
            try
            {
                string path = PathOf(gameId);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
