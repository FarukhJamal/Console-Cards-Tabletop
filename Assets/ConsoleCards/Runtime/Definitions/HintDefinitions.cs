using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Definitions
{
    /// <summary>
    /// How much help the table gives (doc 23 §10). The Hints switch turns hints on or off; the Tips switch picks
    /// Learning (tips about the table and the game) or Reminders only (why a move is not allowed, turn help).
    /// </summary>
    public enum HintLevel
    {
        Off = 0,
        Reminders = 1,
        Learning = 2,
    }

    /// <summary>A tip teaches (Learning only); a reminder explains what the rules say now (Reminders and Learning).</summary>
    public enum HintKind
    {
        Tip = 0,
        Reminder = 1,
    }

    /// <summary>
    /// The events a hint can be shown on (doc 23 §10). The platform raises these; a hint names one, with an
    /// optional context (for example the component kind). Every accepted table action is also an event, named
    /// <see cref="ActionPrefix"/> + its kind (for example "action.FlipConsole"), so new actions need no new keys.
    /// </summary>
    public static class HintTriggers
    {
        /// <summary>A table is ready. Context: "empty" (Empty Table) or "game".</summary>
        public const string TableReady = "table.ready";

        /// <summary>A Toolbox piece is being placed. Context: the catalog kind (Console, GameBoard, Die, Deck, Card…).</summary>
        public const string ToolboxPlacing = "toolbox.placing";

        /// <summary>A card is picked up and dragged.</summary>
        public const string CardDragged = "drag.card";

        /// <summary>A pawn is picked up and dragged. Context: "own" (the turn player's pawn) or "other".</summary>
        public const string PawnDragged = "drag.pawn";

        /// <summary>The first step that can be undone was recorded.</summary>
        public const string UndoAvailable = "undo.available";

        /// <summary>Prefix for accepted table actions; the rest is the action kind's name.</summary>
        public const string ActionPrefix = "action.";

        public static readonly string[] Platform =
        {
            TableReady, ToolboxPlacing, CardDragged, PawnDragged, UndoAvailable,
        };
    }

    /// <summary>One hint: when it shows, its words, how important it is and how often it may show.</summary>
    [Serializable]
    public sealed class HintEntry
    {
        [SerializeField] private string stableId;
        [Tooltip("The event that shows it, for example table.ready, toolbox.placing or action.FlipConsole.")]
        [SerializeField] private string trigger;
        [Tooltip("Optional: only when the event has this context (for example Console). Empty: any context.")]
        [SerializeField] private string context;
        [SerializeField, TextArea(2, 5)] private string text;
        [SerializeField] private HintKind kind;
        [Tooltip("Higher shows first when several hints match.")]
        [SerializeField] private int priority;
        [Tooltip("How many times it may show before it stops (0: no limit). \"Got it\" stops it at once.")]
        [SerializeField, Min(0)] private int maxShows = 2;

        public HintEntry()
        {
        }

        public HintEntry(string stableId, string trigger, string context, string text, HintKind kind, int priority, int maxShows)
        {
            this.stableId = stableId;
            this.trigger = trigger;
            this.context = context;
            this.text = text;
            this.kind = kind;
            this.priority = priority;
            this.maxShows = maxShows;
        }

        public string StableId => stableId ?? string.Empty;
        public string Trigger => trigger ?? string.Empty;
        public string Context => context ?? string.Empty;
        public string Text => text ?? string.Empty;
        public HintKind Kind => kind;
        public int Priority => priority;
        public int MaxShows => maxShows;

        public bool Matches(string eventTrigger, string eventContext)
        {
            return string.Equals(Trigger, eventTrigger, StringComparison.Ordinal)
                && (string.IsNullOrEmpty(Context) || string.Equals(Context, eventContext, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// A set of hints (doc 23 §10). The platform's set lives at Resources/PlatformHints; games add their own
    /// later (H2, from their rules). Plain data: edit, add or remove hints freely.
    /// </summary>
    [CreateAssetMenu(fileName = "PlatformHints", menuName = "Console Cards/Hints/Hint Library")]
    public sealed class HintLibraryDefinition : ScriptableObject
    {
        public const string PlatformResourcePath = "PlatformHints";

        [SerializeField] private List<HintEntry> entries = new List<HintEntry>();

        public IReadOnlyList<HintEntry> Entries => entries;
    }
}
