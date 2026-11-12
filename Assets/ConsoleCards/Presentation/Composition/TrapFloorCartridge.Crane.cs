using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// CRANE-1 (owner, 2026-10-30) and P8A batch 4 (owner, 2026-11-12: "Full: Crane must automatically roll"; CR-1..CR-3):
    /// the Crane reads the two floor dice only after a roll asked for that Crane occurrence (TrapFloorCraneRollGate: this
    /// match, this tile, this reveal), never a roll left over from Undo, another floor or another match.
    /// Full: the dice are thrown for the turn player on their own computer (the host decides the faces, AUTH-DICE), the
    /// Crane reads them, the pawn is moved and its new tile resolved. Reminders: the player throws the dice; once they lie
    /// still the Crane offers Apply (popup, turn menu, the tile's menu) for as long as the dice show that roll.
    /// Event-time only, plus a few field checks per frame while a roll is asked.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        private TabletopObjectId craneRollTrapId;
        private readonly TrapFloorCraneRollGate craneGate = new TrapFloorCraneRollGate();

        private bool IsCraneRollAsked => !craneRollTrapId.IsEmpty;

        // Only the Crane rolls; Spring launches in its own direction. True while this Crane occurrence has no usable roll.
        private bool NeedsCraneRoll(TabletopObjectId trapId, TrapFloorFloorContentDefinition content) =>
            content != null && content.TrapEffect == TrapFloorTrapEffectCategory.CraneDrop && !IsCraneRollReady(trapId);

        private bool TryCraneOccurrence(TabletopObjectId trapId, out TrapFloorCraneOccurrence occurrence)
        {
            occurrence = default;
            if (host.matchState == null || host.trapFloorAbilityResolutionState == null
                || !host.trapFloorAbilityResolutionState.TryGetTrap(trapId, out TrapFloorTrapResolutionRecord record)
                || !record.IsPending
                || record.Content.TrapEffect != TrapFloorTrapEffectCategory.CraneDrop)
            {
                return false;
            }

            occurrence = TrapFloorCraneOccurrence.For(host.matchState.Id.Value, record);
            return true;
        }

        // The faces both floor dice show, when both lie still.
        private bool FloorDiceFaces(out int column, out int row)
        {
            column = row = 0;
            if (host.matchState == null || host.trapFloorTemplate == null
                || !host.matchState.Dice.TryGetValue(host.trapFloorTemplate.FloorfallXAxisDieId, out DieState x)
                || !host.matchState.Dice.TryGetValue(host.trapFloorTemplate.FloorfallYAxisDieId, out DieState y)
                || !LiesStill(x) || !LiesStill(y))
            {
                return false;
            }

            column = x.CurrentValue;
            row = y.CurrentValue;
            return true;
        }

        private static bool LiesStill(DieState die) =>
            die.BaseState.PhysicalState == null || die.BaseState.PhysicalState.Mode == PhysicalObjectMode.Sleeping;

        /// <summary>True when this Crane occurrence was rolled for and the dice still show that roll (Reminders' Apply).</summary>
        internal bool IsCraneRollReady(TabletopObjectId trapId) =>
            TryCraneOccurrence(trapId, out TrapFloorCraneOccurrence occurrence)
            && FloorDiceFaces(out int column, out int row)
            && craneGate.IsReadyFor(occurrence, column, row);

        internal void BeginCraneRoll(TabletopObjectId trapId)
        {
            if (!TryCraneOccurrence(trapId, out TrapFloorCraneOccurrence occurrence)) return;
            bool alreadyAsked = craneRollTrapId == trapId && craneGate.IsAskedFor(occurrence);
            craneRollTrapId = trapId;
            if (!alreadyAsked)
            {
                AskForFloorDiceRoll(true, true); // only a throw from now on counts (MP-2)
                craneGate.Ask(occurrence);
            }

            host.ContextMenus.CloseContextMenu();
            host.CardInspectPanel.CloseCardInspect();
            if (PlayAssist.Level == PlayAssistLevel.Full)
            {
                // P8A batch 4: Full throws the dice for the turn player (on their own computer only).
                bool thrown = !alreadyAsked && ThrowFloorDiceForCrane();
                host.FeedbackLine(thrown
                    ? "CRANE — the two floor dice are rolled for you: the Crane drops your pawn on the tile they show."
                    : alreadyAsked
                    ? "CRANE — waiting for the two floor dice to lie still (throw them if they were not rolled)."
                    : "CRANE — throw the two floor dice (pick them up and throw them): the Crane drops your pawn on the tile they show.",
                    FeedbackSeverity.Info);
                return;
            }

            host.FeedbackLine( // shown even with action messages off: the game waits for it
                "CRANE — roll the two floor dice yourself (pick them up and throw them; first die = column, second = row). "
                + "When they lie still, choose Apply.",
                FeedbackSeverity.Info);
        }

        // Full: both floor dice thrown for the turn player, as a player's own roll would be (the same commands, the host's
        // faces). False when this computer does not play the turn or a die cannot be thrown (held, locked).
        private bool ThrowFloorDiceForCrane()
        {
            PlayerId active = host.trapFloorTurnState != null ? host.trapFloorTurnState.ActivePlayerId : PlayerId.Empty;
            if (active.IsEmpty || !host.IsPlayerOnThisComputer(active) || host.IsActingForAnotherComputer) return false;
            if (!host.Registry.TryGetDieView(host.trapFloorTemplate.FloorfallXAxisDieId, out DieView x) || x.PhysicalObject == null
                || !host.Registry.TryGetDieView(host.trapFloorTemplate.FloorfallYAxisDieId, out DieView y) || y.PhysicalObject == null)
            {
                return false;
            }

            bool first = x.PhysicalObject.Roll(active);
            bool second = y.PhysicalObject.Roll(active);
            if (!first || !second) Debug.LogWarning($"[Crane] A floor die could not be thrown for the Crane (column {first}, row {second}).");
            return first && second;
        }

        private void CompleteCraneRollIfSettled()
        {
            if (craneRollTrapId.IsEmpty) return;
            if (!CanResolveTrapFloorTrap(craneRollTrapId)
                || !TryCraneOccurrence(craneRollTrapId, out TrapFloorCraneOccurrence occurrence)
                || !craneGate.IsAskedFor(occurrence))
            {
                craneRollTrapId = TabletopObjectId.Empty; // the turn moved on or the trap went (Undo, a Reaction)
                return;
            }

            if (!FloorDiceRolledAsAsked()) return;
            TabletopObjectId trapId = craneRollTrapId;
            // The roll was the Crane's: a collapse waiting for its roll asks for a new one afterwards.
            if (host.physicalFloorCollapsePending) AskForFloorDiceRoll(true, true);
            craneRollTrapId = TabletopObjectId.Empty;
            if (!FloorDiceFaces(out int column, out int row) || !craneGate.Rolled(occurrence, column, row)) return;
            if (!host.trapFloorTemplate.TryGetFloorCardState(host.matchState, trapId, out TrapFloorFloorCardState crane)) return;

            if (PlayAssist.Level == PlayAssistLevel.Full)
            {
                ResolveTrapFloorLaunchTrap(trapId, crane.Content); // reads the dice, moves the pawn, resolves its new tile
                return;
            }

            // Reminders (and Free's own Resolve Trap): the roll is kept for this Crane; the player applies it.
            FloorPopupArt(crane, out Texture back, out Texture front);
            ShowTrapPopup(crane, $"CRANE — DICE: COLUMN {column}, ROW {row}", back, front);
            host.FeedbackLine(
                $"CRANE — the dice show column {column}, row {row}. Choose Apply (the popup, the turn menu, or the Crane tile's menu) to be sent there.",
                FeedbackSeverity.Info);
        }
    }
}
