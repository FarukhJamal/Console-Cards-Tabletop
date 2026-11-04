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

        public static PlayAssistLevel Level
        {
            get
            {
                if (cached < 0) cached = Mathf.Clamp(PlayerPrefs.GetInt(Pref, (int)PlayAssistLevel.Free), 0, 2);
                return (PlayAssistLevel)cached;
            }
            set
            {
                cached = Mathf.Clamp((int)value, 0, 2);
                PlayerPrefs.SetInt(Pref, cached);
                PlayerPrefs.Save();
            }
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
