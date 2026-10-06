using System;
using System.Collections.Generic;
using ConsoleCards.GameTemplates.Definitions;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    [Serializable]
    public sealed class ConsoleLayoutSlotDefinition
    {
        [SerializeField, Min(1)] private int ordinal = 1;
        [SerializeField] private string key;
        [SerializeField] private ConsoleLayoutSlotKind kind;
        [SerializeField] private string group;
        [SerializeField] private float x;
        [SerializeField] private float z;
        [SerializeField, Min(0.001f)] private float footprintWidth = 1.4f;
        [SerializeField, Min(0.001f)] private float footprintDepth = 1.4f;
        [SerializeField, Min(0)] private int defaultCapacity;
        [SerializeField] private bool marker;

        internal ConsoleLayoutSlotData ToData()
        {
            return new ConsoleLayoutSlotData(
                ordinal,
                key,
                kind,
                group,
                x,
                z,
                footprintWidth,
                footprintDepth,
                defaultCapacity,
                marker);
        }
    }

    /// <summary>
    /// Authored layout of the one generic Console component (doc 19): every card slot and cube position
    /// on the mat, with kind, footprint, default capacity and marker flag. Games never change it.
    /// </summary>
    [CreateAssetMenu(fileName = "ConsoleLayout", menuName = "Console Cards/Definitions/Console Layout")]
    public sealed class ConsoleLayoutDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField, Min(0.01f)] private float matWidth = 6f;
        [SerializeField, Min(0.01f)] private float matDepth = 3.077f;
        [SerializeField, Min(0.001f)] private float cubePieceSize = 0.126f;
        [SerializeField] private List<ConsoleLayoutSlotDefinition> slots = new List<ConsoleLayoutSlotDefinition>();

        public ConsoleLayoutData ToData()
        {
            List<ConsoleLayoutSlotData> slotData = new List<ConsoleLayoutSlotData>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) throw new InvalidOperationException("Console layout slots cannot contain null entries.");
                slotData.Add(slots[i].ToData());
            }

            return new ConsoleLayoutData(stableId, matWidth, matDepth, cubePieceSize, slotData);
        }
    }
}
