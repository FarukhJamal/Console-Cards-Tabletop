using System;
using UnityEngine;

namespace ConsoleCards.Presentation.Settings
{
    /// <summary>The table's light (V1, owner 2026-10-27: three presets, switchable in SETTINGS).</summary>
    public enum LightPreset
    {
        Day = 0,
        Evening = 1,
        Arcade = 2,
    }

    /// <summary>Graphics quality (V7, owner 2026-10-27: Low, Medium, High; picked for the computer on first run).</summary>
    public enum GraphicsQuality
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    /// <summary>
    /// V1, V5, V6, V7 (owner, 2026-10-27): this computer's look settings: the light preset, colour-blind seat colours,
    /// graphics quality and the room sound. Kept with the computer (PlayerPrefs), not the profile: they describe the
    /// screen and the speakers, like a graphics menu. Read once, written when changed; never per frame.
    /// </summary>
    public static class LookSettings
    {
        private const string LightKey = "ConsoleCards.Look.Light";
        private const string QualityKey = "ConsoleCards.Look.Quality";
        private const string ColourBlindKey = "ConsoleCards.Look.ColourBlind";
        private const string RoomSoundKey = "ConsoleCards.Look.RoomSound";

        private static bool loaded;
        private static LightPreset light = LightPreset.Arcade;
        private static GraphicsQuality quality = GraphicsQuality.High;
        private static bool colourBlind;
        private static bool roomSound = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Forget() => loaded = false;

        public static LightPreset Light
        {
            get { Load(); return light; }
            set { Load(); light = value; Save(); }
        }

        public static GraphicsQuality Quality
        {
            get { Load(); return quality; }
            set { Load(); quality = value; Save(); }
        }

        /// <summary>Seat colours from a set people with colour blindness tell apart (Okabe and Ito).</summary>
        public static bool ColourBlind
        {
            get { Load(); return colourBlind; }
            set { Load(); colourBlind = value; Save(); }
        }

        /// <summary>A quiet hum of the console around the table.</summary>
        public static bool RoomSound
        {
            get { Load(); return roomSound; }
            set { Load(); roomSound = value; Save(); }
        }

        public static string LightName(LightPreset preset) =>
            preset == LightPreset.Day ? "Day" : preset == LightPreset.Evening ? "Evening" : "Arcade";

        public static string QualityName(GraphicsQuality level) =>
            level == GraphicsQuality.Low ? "Low" : level == GraphicsQuality.Medium ? "Medium" : "High";

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                light = (LightPreset)Mathf.Clamp(PlayerPrefs.GetInt(LightKey, (int)LightPreset.Arcade), 0, 2);
                quality = PlayerPrefs.HasKey(QualityKey)
                    ? (GraphicsQuality)Mathf.Clamp(PlayerPrefs.GetInt(QualityKey), 0, 2)
                    : SuggestedQuality();
                colourBlind = PlayerPrefs.GetInt(ColourBlindKey, 0) != 0;
                roomSound = PlayerPrefs.GetInt(RoomSoundKey, 1) != 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Look] The look settings could not be read; defaults are used ({exception.Message}).");
            }
        }

        private static void Save()
        {
            try
            {
                PlayerPrefs.SetInt(LightKey, (int)light);
                PlayerPrefs.SetInt(QualityKey, (int)quality);
                PlayerPrefs.SetInt(ColourBlindKey, colourBlind ? 1 : 0);
                PlayerPrefs.SetInt(RoomSoundKey, roomSound ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Look] The look settings could not be saved ({exception.Message}).");
            }
        }

        // First run: the graphics card's memory and the processor pick the level (a mid-range PC gets High).
        private static GraphicsQuality SuggestedQuality()
        {
            int videoMemory = SystemInfo.graphicsMemorySize;
            if (videoMemory > 0 && videoMemory < 2500) return GraphicsQuality.Low;
            if ((videoMemory > 0 && videoMemory < 4500) || SystemInfo.processorCount < 4) return GraphicsQuality.Medium;
            return GraphicsQuality.High;
        }
    }
}
