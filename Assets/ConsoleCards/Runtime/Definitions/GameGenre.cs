namespace ConsoleCards.Definitions
{
    /// <summary>
    /// GENRE-1 (owner, 2026-11-02: "every game will have its own definition … Super Leroy Sisters is a platformer …
    /// divide the games under genres"): what kind of game a game is. Console Cards' general rules (the Console, the
    /// controller cards, the table) hold for every genre; each game brings its own rules on top. The numbers are kept
    /// when names change, so saved games keep their genre.
    /// </summary>
    public enum GameGenre
    {
        Unsorted = 0,
        Party = 1,
        Platformer = 2,
        Strategy = 3,
        Puzzle = 4,
        Racing = 5,
        Adventure = 6,
        Fighting = 7,
        Sports = 8,
        Card = 9,
        Dice = 10,
    }

    public static class GameGenres
    {
        /// <summary>The genre in words for the screens ("Platformer"); empty for Unsorted.</summary>
        public static string Words(GameGenre genre) => genre == GameGenre.Unsorted ? string.Empty : genre.ToString();
    }
}
