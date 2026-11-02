using System;
using System.Collections.Generic;

namespace ConsoleCards.Games.TrapFloor
{
    /// <summary>
    /// STAGES-1 (owner, 2026-11-02: "think of it like stages: stage 1 with a kitchen floor which ends on 3×3, then the
    /// next stage a living room and so on"): the game's stages. A stage is one whole run of floors (6×6 down to 3×3)
    /// with the same cards and rules and its own look: a name, the back every floor tile shows, and new front pictures
    /// for any cards. Winning a stage's last floor starts the next stage at full size; winning the last stage wins
    /// the game. Losing works as before.
    /// </summary>
    public sealed class TrapFloorFloorPlan
    {
        private readonly List<TrapFloorStage> stages;

        public TrapFloorFloorPlan(List<TrapFloorStage> stages)
        {
            this.stages = stages ?? throw new ArgumentNullException(nameof(stages));
        }

        /// <summary>How many stages the game has (at least 1).</summary>
        public int StageCount => Math.Max(1, stages.Count);

        /// <summary>The stage at <paramref name="stageIndex"/> (0 = Stage 1), or null when it has no look of its own.</summary>
        public TrapFloorStage StageAt(int stageIndex) =>
            stageIndex >= 0 && stageIndex < stages.Count ? stages[stageIndex] : null;
    }

    /// <summary>One stage's look: its name, its tiles' back and front pictures by card ID (art keys).</summary>
    public sealed class TrapFloorStage
    {
        public TrapFloorStage(int number, string name, string backArtKey, IReadOnlyDictionary<string, string> fronts)
        {
            Number = number;
            Name = string.IsNullOrWhiteSpace(name) ? $"Stage {number}" : name.Trim();
            BackArtKey = backArtKey ?? string.Empty;
            Fronts = fronts ?? new Dictionary<string, string>();
        }

        public int Number { get; }
        public string Name { get; }
        public string BackArtKey { get; }
        public IReadOnlyDictionary<string, string> Fronts { get; }

        /// <summary>This stage's front picture (art key) for a card; false when the card keeps its own.</summary>
        public bool TryGetFront(string cardId, out string artKey)
        {
            artKey = null;
            return !string.IsNullOrEmpty(cardId) && Fronts.TryGetValue(cardId, out artKey) && !string.IsNullOrEmpty(artKey);
        }
    }
}
