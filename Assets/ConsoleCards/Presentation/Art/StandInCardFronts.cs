using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.Art
{
    /// <summary>
    /// Stand-in card fronts (owner's choices 2026-10-20): a picture for each built-in card that has no front art yet,
    /// showing its name, a type band, a simple symbol and its rules text. The pictures are real PNGs made by
    /// Console Cards > Content > Create Stand-in Card Fronts (Content/Art/StandIn); this list at
    /// Resources/StandInCardFronts ties each to its card. A stand-in shows only where a card has no art of its own
    /// (built-in, a pack or your picture), so real art always wins. Looked up by the card's stable id; no allocation
    /// after the first look.
    /// </summary>
    [CreateAssetMenu(fileName = "StandInCardFronts", menuName = "Console Cards/Cards/Stand-in Card Fronts")]
    public sealed class StandInCardFronts : ScriptableObject
    {
        public const string ResourcePath = "StandInCardFronts";

        [Serializable]
        public sealed class Entry
        {
            public string stableId = string.Empty;
            public Texture2D front;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private static StandInCardFronts loaded;
        private static bool looked;
        private Dictionary<string, Texture2D> byId;

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>The stand-in front of a card, or null (no stand-in, or the list is missing).</summary>
        public static Texture2D FrontFor(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return null;
            if (!looked)
            {
                looked = true;
                loaded = Resources.Load<StandInCardFronts>(ResourcePath);
            }

            return loaded != null ? loaded.Find(stableId) : null;
        }

        private Texture2D Find(string stableId)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    if (entry != null && entry.front != null && !string.IsNullOrEmpty(entry.stableId))
                    {
                        byId[entry.stableId] = entry.front;
                    }
                }
            }

            return byId.TryGetValue(stableId, out Texture2D front) ? front : null;
        }

        private void OnValidate()
        {
            byId = null; // edited in the Inspector: look again
        }
    }
}
