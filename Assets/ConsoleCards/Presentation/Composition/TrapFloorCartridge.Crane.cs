using ConsoleCards.Core.Identifiers;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.UI;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// CRANE-1 (owner, 2026-10-30: "the crane trap says roll two coloured dice, then it gives you the card to move on;
    /// right now it's doing it automatically"): the Crane no longer reads whatever the floor dice already show. Apply
    /// (or Resolve Trap, or End Turn) asks the player to roll the two floor dice; once both were thrown on this
    /// computer and lie still (DICE-1), the Crane reads them and lights the tile to move to. Event-time only, plus a
    /// few field checks per frame while a roll is asked.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        private TabletopObjectId craneRollTrapId;
        private TabletopObjectId craneRolledTrapId;

        private bool IsCraneRollAsked => !craneRollTrapId.IsEmpty;

        // Only the Crane rolls; Spring launches in its own direction.
        private bool NeedsCraneRoll(TabletopObjectId trapId, TrapFloorFloorContentDefinition content) =>
            content != null && content.TrapEffect == TrapFloorTrapEffectCategory.CraneDrop && craneRolledTrapId != trapId;

        internal void BeginCraneRoll(TabletopObjectId trapId)
        {
            bool alreadyAsked = craneRollTrapId == trapId;
            craneRollTrapId = trapId;
            if (!alreadyAsked) AskForFloorDiceRoll(true, true);
            host.ContextMenus.CloseContextMenu();
            host.CardInspectPanel.CloseCardInspect();
            host.FeedbackLine( // shown even with action messages off: the game waits for it
                "CRANE — roll the two floor dice (pick them up and throw them): the Crane drops you on the tile they show.",
                FeedbackSeverity.Info);
        }

        private void CompleteCraneRollIfSettled()
        {
            if (craneRollTrapId.IsEmpty) return;
            if (!CanResolveTrapFloorTrap(craneRollTrapId))
            {
                craneRollTrapId = TabletopObjectId.Empty; // the turn moved on or the trap went (Undo, a Reaction)
                return;
            }

            if (!FloorDiceRolledAsAsked()) return;
            TabletopObjectId trapId = craneRollTrapId;
            // The roll was the Crane's: a collapse waiting for its roll asks for a new one afterwards.
            if (host.physicalFloorCollapsePending) AskForFloorDiceRoll(true, true);
            craneRollTrapId = TabletopObjectId.Empty;
            craneRolledTrapId = trapId;
            ResolveTrapFloorTrap(trapId); // reads the dice and lights the tile to move to
        }
    }
}
