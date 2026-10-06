using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConsoleCards.GameTemplates.Definitions
{
    /// <summary>What a Console layout slot holds. Cube entries are layout data only until Stage (d).</summary>
    public enum ConsoleLayoutSlotKind
    {
        Card = 0,
        Cube = 1
    }

    /// <summary>
    /// Default card orientation of a Card slot, relative to the Console. A default only: a card placed in a slot
    /// keeps its own orientation; the default applies when a card is placed without one (for example a
    /// template's starting content). Cube entries use None.
    /// </summary>
    public enum ConsoleSlotOrientation
    {
        None = 0,
        Portrait = 1,
        Landscape = 2
    }

    /// <summary>
    /// One authored Console layout slot: a stable ordinal and key, its console-local centre on the mat
    /// (origin at the mat centre, +x right, +z toward the top of the mat texture), its footprint and its
    /// default capacity (0 = unbounded). Ordinals are append-only and are never reused or renumbered.
    /// </summary>
    [Serializable]
    public sealed class ConsoleLayoutSlotData
    {
        public const int MaximumOrdinal = 65535;

        public ConsoleLayoutSlotData(
            int ordinal,
            string key,
            ConsoleLayoutSlotKind kind,
            string group,
            float x,
            float z,
            float footprintWidth,
            float footprintDepth,
            int defaultCapacity,
            bool marker,
            ConsoleSlotOrientation defaultOrientation)
        {
            if (ordinal < 1 || ordinal > MaximumOrdinal) throw new ArgumentOutOfRangeException(nameof(ordinal));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Console layout slot key is required.", nameof(key));
            if (!Enum.IsDefined(typeof(ConsoleLayoutSlotKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
            if (!IsFinite(z)) throw new ArgumentOutOfRangeException(nameof(z));
            if (!IsFinite(footprintWidth) || footprintWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(footprintWidth));
            if (!IsFinite(footprintDepth) || footprintDepth <= 0f) throw new ArgumentOutOfRangeException(nameof(footprintDepth));
            if (defaultCapacity < 0) throw new ArgumentOutOfRangeException(nameof(defaultCapacity));
            if (!Enum.IsDefined(typeof(ConsoleSlotOrientation), defaultOrientation)) throw new ArgumentOutOfRangeException(nameof(defaultOrientation));
            if ((kind == ConsoleLayoutSlotKind.Card) == (defaultOrientation == ConsoleSlotOrientation.None))
                throw new ArgumentException("Card slots need a Portrait or Landscape default orientation; Cube slots use None.", nameof(defaultOrientation));

            Ordinal = ordinal;
            Key = key;
            Kind = kind;
            Group = group ?? string.Empty;
            X = x;
            Z = z;
            FootprintWidth = footprintWidth;
            FootprintDepth = footprintDepth;
            DefaultCapacity = defaultCapacity;
            Marker = marker;
            DefaultOrientation = defaultOrientation;
        }

        public int Ordinal { get; }
        public string Key { get; }
        public ConsoleLayoutSlotKind Kind { get; }
        public string Group { get; }
        public float X { get; }
        public float Z { get; }
        public float FootprintWidth { get; }
        public float FootprintDepth { get; }
        public int DefaultCapacity { get; }
        public bool Marker { get; }
        public ConsoleSlotOrientation DefaultOrientation { get; }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// The one generic Console component layout. Every Console exposes every slot; Games never add,
    /// remove or restrict slots. Card slots map to runtime Console slot indices in ordinal order.
    /// </summary>
    [Serializable]
    public sealed class ConsoleLayoutData
    {
        private readonly ReadOnlyCollection<ConsoleLayoutSlotData> slots;
        private readonly ReadOnlyCollection<ConsoleLayoutSlotData> cardSlots;
        private readonly ReadOnlyCollection<int> cardSlotCapacities;

        public ConsoleLayoutData(
            string stableId,
            float matWidth,
            float matDepth,
            float cubePieceSize,
            IEnumerable<ConsoleLayoutSlotData> slots)
        {
            if (string.IsNullOrWhiteSpace(stableId)) throw new ArgumentException("Console layout ID is required.", nameof(stableId));
            if (!(matWidth > 0f) || float.IsInfinity(matWidth)) throw new ArgumentOutOfRangeException(nameof(matWidth));
            if (!(matDepth > 0f) || float.IsInfinity(matDepth)) throw new ArgumentOutOfRangeException(nameof(matDepth));
            if (!(cubePieceSize > 0f) || float.IsInfinity(cubePieceSize)) throw new ArgumentOutOfRangeException(nameof(cubePieceSize));
            if (slots == null) throw new ArgumentNullException(nameof(slots));

            List<ConsoleLayoutSlotData> ordered = new List<ConsoleLayoutSlotData>(slots);
            HashSet<int> ordinals = new HashSet<int>();
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ordered.Count; i++)
            {
                ConsoleLayoutSlotData slot = ordered[i];
                if (slot == null) throw new ArgumentException("Console layout slots cannot contain null entries.", nameof(slots));
                if (!ordinals.Add(slot.Ordinal)) throw new ArgumentException($"Console layout ordinal {slot.Ordinal} is used more than once.", nameof(slots));
                if (!keys.Add(slot.Key)) throw new ArgumentException($"Console layout key '{slot.Key}' is used more than once.", nameof(slots));
            }

            ordered.Sort((left, right) => left.Ordinal.CompareTo(right.Ordinal));
            List<ConsoleLayoutSlotData> cards = new List<ConsoleLayoutSlotData>();
            List<int> capacities = new List<int>();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Kind != ConsoleLayoutSlotKind.Card) continue;
                cards.Add(ordered[i]);
                capacities.Add(ordered[i].DefaultCapacity);
            }

            if (cards.Count == 0) throw new ArgumentException("A Console layout requires at least one Card slot.", nameof(slots));

            StableId = stableId;
            MatWidth = matWidth;
            MatDepth = matDepth;
            CubePieceSize = cubePieceSize;
            this.slots = new ReadOnlyCollection<ConsoleLayoutSlotData>(ordered);
            cardSlots = new ReadOnlyCollection<ConsoleLayoutSlotData>(cards);
            cardSlotCapacities = new ReadOnlyCollection<int>(capacities);
        }

        public string StableId { get; }
        public float MatWidth { get; }
        public float MatDepth { get; }
        public float CubePieceSize { get; }

        /// <summary>Every slot, Card and Cube, in ordinal order.</summary>
        public IReadOnlyList<ConsoleLayoutSlotData> Slots => slots;

        /// <summary>Card slots in ordinal order; index i is runtime Console slot index i.</summary>
        public IReadOnlyList<ConsoleLayoutSlotData> CardSlots => cardSlots;

        /// <summary>Default capacity of each Card slot, in runtime slot index order (0 = unbounded).</summary>
        public IReadOnlyList<int> CardSlotCapacities => cardSlotCapacities;

        /// <summary>Finds a slot by its stable key (case-insensitive), for starting content and rule hooks.</summary>
        public bool TryGetSlot(string key, out ConsoleLayoutSlotData slot)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (string.Equals(slots[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    slot = slots[i];
                    return true;
                }
            }

            slot = null;
            return false;
        }
    }
}
