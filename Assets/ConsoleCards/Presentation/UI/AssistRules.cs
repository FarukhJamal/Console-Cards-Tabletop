namespace ConsoleCards.Presentation.UI
{
    /// <summary>The game rules and helpers that change with the Assist level (doc 15, foundation stage).</summary>
    public enum AssistRule
    {
        /// <summary>Whose pawn, whose turn, holes, distance and Blind direction for a pawn move.</summary>
        PawnMoveRules = 0,
        /// <summary>The host checks an escape's tile, Key and floor result.</summary>
        EscapeChecks = 1,
        /// <summary>A rules result (a die a rule reads, a Trap, a turned tile, a collapse, an elimination) cannot be undone.</summary>
        RulesResultsStand = 2,
        /// <summary>A floor turn owes one collapse before it ends.</summary>
        FloorCollapseOwed = 3,
        /// <summary>The floor dice and an unresolved Blind d4 are rules dice (a value off the floor is rolled again).</summary>
        RulesDice = 4,
        /// <summary>A Blind player may not draw from the Controller deck.</summary>
        BlindNoControllerDraw = 5,
        /// <summary>Draw 3 Abilities, the buying window, returning and shuffling what is left.</summary>
        AbilityDrawThree = 6,
        /// <summary>A used Ability goes to the discard pile.</summary>
        UsedAbilityDiscard = 7,
        /// <summary>An Avatar's starting Abilities are placed beside its player's Console.</summary>
        StartingAbilities = 8,
        /// <summary>The CHOOSE YOUR AVATAR pop-up.</summary>
        AvatarDraft = 9,
        /// <summary>A Trap left waiting is applied (or its owed move made) at End Turn.</summary>
        TrapEffectsAtEndTurn = 10,
    }

    /// <summary>What the game does about a rule: nothing, a reminder, or it runs and enforces it.</summary>
    public enum AssistMode
    {
        Off = 0,
        Warn = 1,
        Enforce = 2,
    }

    /// <summary>
    /// Foundation stage (audit proposal 1, owner "OK for me", 2026-11-13): one table says what each Assist level does
    /// for each rule (Off / Warn / Enforce), so the levels are read from one place instead of scattered level tests.
    /// Every computer of a room reads it with the room's level (<see cref="PlayAssist.Level"/>), so a rule is judged
    /// the same everywhere. Rows are filled from the owner's decisions recorded in doc 15. No allocation.
    /// </summary>
    public static class AssistRules
    {
        // [rule, level]: level columns are Free, Reminders, Full.
        private static readonly AssistMode[,] Table =
        {
            /* PawnMoveRules         */ { AssistMode.Off, AssistMode.Off, AssistMode.Enforce },
            /* EscapeChecks          */ { AssistMode.Off, AssistMode.Off, AssistMode.Enforce },
            /* RulesResultsStand     */ { AssistMode.Off, AssistMode.Off, AssistMode.Enforce },
            /* FloorCollapseOwed     */ { AssistMode.Off, AssistMode.Warn, AssistMode.Enforce },
            /* RulesDice             */ { AssistMode.Off, AssistMode.Off, AssistMode.Enforce },
            /* BlindNoControllerDraw */ { AssistMode.Off, AssistMode.Warn, AssistMode.Enforce },
            /* AbilityDrawThree      */ { AssistMode.Off, AssistMode.Warn, AssistMode.Enforce },
            /* UsedAbilityDiscard    */ { AssistMode.Off, AssistMode.Warn, AssistMode.Enforce },
            /* StartingAbilities     */ { AssistMode.Off, AssistMode.Enforce, AssistMode.Enforce },
            /* AvatarDraft           */ { AssistMode.Off, AssistMode.Off, AssistMode.Enforce },
            /* TrapEffectsAtEndTurn  */ { AssistMode.Off, AssistMode.Warn, AssistMode.Enforce },
        };

        public static int RuleCount => Table.GetLength(0);

        public static AssistMode ModeOf(AssistRule rule, PlayAssistLevel level)
        {
            int row = (int)rule;
            int column = (int)level;
            if (row < 0 || row >= Table.GetLength(0) || column < 0 || column >= Table.GetLength(1)) return AssistMode.Off;
            return Table[row, column];
        }

        /// <summary>At the level in play (the room's level online).</summary>
        public static AssistMode ModeOf(AssistRule rule) => ModeOf(rule, PlayAssist.Level);

        public static bool Enforces(AssistRule rule) => ModeOf(rule) == AssistMode.Enforce;

        public static bool Warns(AssistRule rule) => ModeOf(rule) == AssistMode.Warn;

        /// <summary>The game does something about the rule (a reminder or enforcing it).</summary>
        public static bool Watches(AssistRule rule) => ModeOf(rule) != AssistMode.Off;
    }
}
