using UnityEngine;

namespace ConsoleCards.Presentation.UI
{
    /// <summary>How much the game helps while you play (owner, 2026-11-03, doc 57).</summary>
    public enum PlayAssistLevel
    {
        /// <summary>A free table: no popups, prompts, hints or "apply" questions. The game still keeps score quietly.</summary>
        Free = 0,

        /// <summary>Free, plus the Hints switch and the quiet referee's notes.</summary>
        Reminders = 1,

        /// <summary>The game runs its rules for you, with every popup and prompt (as before).</summary>
        Full = 2,
    }

    /// <summary>
    /// Owner, 2026-11-03 ("make it more like a simulator; yes, Free by default"): the Assist setting, saved on this
    /// computer. The game (the composition) watches <see cref="Level"/> and applies it; Settings › GAME › Assist
    /// changes it.
    /// </summary>
    public static class PlayAssist
    {
        private const string Pref = "ConsoleCards.PlayAssist";
        private static int cached = -1;

        // ASSIST-ROOM (2026-11-06, Full Mode configuration audit): online, the level is the room's, not each
        // computer's. The rules code reads Level on whichever computer runs a game step (eliminations, exits, Key
        // offers), so two computers on different levels enforced different rules in one match. The host's level is
        // the room's; it reaches the joined computers in the roster, and a joined computer cannot change it.
        private static int roomLevel = -1;
        private static bool roomLocked;

        /// <summary>The level in play: the room's while in an online room, else this computer's own choice.</summary>
        public static PlayAssistLevel Level
        {
            get => roomLevel >= 0 ? (PlayAssistLevel)roomLevel : LocalLevel;
            set => TryChoose(value);
        }

        /// <summary>This computer's own choice, kept for play on this computer (Settings › GAME › Assist).</summary>
        public static PlayAssistLevel LocalLevel
        {
            get
            {
                if (cached < 0) cached = Mathf.Clamp(PlayerPrefs.GetInt(Pref, (int)PlayAssistLevel.Free), 0, 2);
                return (PlayAssistLevel)cached;
            }
        }

        /// <summary>True on a joined computer in an online room: the host sets the level for everyone.</summary>
        public static bool IsSetByRoom => roomLevel >= 0 && roomLocked;

        /// <summary>
        /// Settings › Assist. False (nothing changes) on a joined computer: the room's host sets it. On the host it
        /// changes the room's level too, which the roster then sends to everyone.
        /// </summary>
        public static bool TryChoose(PlayAssistLevel value)
        {
            if (IsSetByRoom) return false;
            int level = Mathf.Clamp((int)value, 0, 2);
            cached = level;
            PlayerPrefs.SetInt(Pref, level);
            PlayerPrefs.Save();
            if (roomLevel >= 0) roomLevel = level;
            return true;
        }

        /// <summary>
        /// Online: the room's level. <paramref name="locked"/> is true on a joined computer (the host's level, from the
        /// roster) and false on the host (its own level, which it sends).
        /// </summary>
        public static void UseRoomLevel(PlayAssistLevel level, bool locked)
        {
            roomLevel = Mathf.Clamp((int)level, 0, 2);
            roomLocked = locked;
        }

        /// <summary>Back to this computer's own level (the room was left).</summary>
        public static void ClearRoomLevel()
        {
            roomLevel = -1;
            roomLocked = false;
        }

        /// <summary>True when the game asks nothing: its information popups become a short line at the top.</summary>
        public static bool IsQuiet => Level != PlayAssistLevel.Full;

        public static bool IsFree => Level == PlayAssistLevel.Free;

        public static string Name(PlayAssistLevel level)
        {
            switch (level)
            {
                case PlayAssistLevel.Free: return "FREE";
                case PlayAssistLevel.Reminders: return "REMINDERS";
                default: return "FULL";
            }
        }

        public static string Describe(PlayAssistLevel level)
        {
            switch (level)
            {
                case PlayAssistLevel.Free: return "A free table: you play the rules yourselves; the game keeps score quietly.";
                case PlayAssistLevel.Reminders: return "Free, plus hints and quiet notes when something looks off.";
                default: return "The game runs its rules for you, with popups and prompts.";
            }
        }
    }
}
