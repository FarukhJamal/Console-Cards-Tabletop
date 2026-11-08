using System;
using System.Collections.Generic;
using System.Text;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Random;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Boards;
using ConsoleCards.Core.Domain.Cards;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Consoles;
using ConsoleCards.Core.Domain.Dice;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.Art;
using ConsoleCards.Presentation.Camera;
using ConsoleCards.Presentation.Content;
using ConsoleCards.Presentation.Catalog;
using ConsoleCards.Presentation.Coordinates;
using ConsoleCards.Presentation.Feedback;
using ConsoleCards.Presentation.Hints;
using ConsoleCards.Presentation.Input;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Settings;
using ConsoleCards.Presentation.UI;
using ConsoleCards.Presentation.UI.Toolbox;
using ConsoleCards.Presentation.Views;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Trap Floor hint helpers (doc 23 §10, layer C, H3). Each helper is named by a rule's hintHelper field and
    /// runs only while a rule in play names it; the hint service ticks it each frame and asks it about every table
    /// event. The helpers explain and mark tiles; they never block a move or change game state.
    /// <list type="bullet">
    /// <item>trapfloor.reach (rule "Move up to N tiles"): reach markers while holding your pawn, and a RULE
    /// reminder while it is over a tile the rules do not allow. A landing the rules do not allow is a rule check
    /// (warning pointing at the Undo button, no flip, Keep move in the turn menu), not a hint.</item>
    /// <item>trapfloor.prize (rule "Standing on an Ability or Friend?"): you stand on a prize you may take.</item>
    /// <item>trapfloor.traps-waiting (rule "Reaction abilities"): a Trap waits until End Turn.</item>
    /// <item>trapfloor.collapse (the floor-turn rules): the tile the floor dice point at, marked.</item>
    /// </list>
    /// </summary>
    public sealed partial class TabletopComposition
    {

        /// <summary>
        /// P7 (s155): on a private host, the active player plays on another computer. The helper lines that talk to the
        /// active player as "you" are not shown here (they would speak to the host player about someone else).
        /// </summary>
        internal bool ActivePlayerPlaysElsewhere =>
            HasOnline && Online.IsPrivateHost && trapFloorTurnState != null && Online.IsPlayerOnAnotherComputer(trapFloorTurnState.ActivePlayerId);

        internal abstract class TrapFloorHintHelper : IGameHintHelper
        {
            protected readonly TabletopComposition owner;

            protected TrapFloorHintHelper(TabletopComposition owner, string helperId)
            {
                this.owner = owner;
                HelperId = helperId;
            }

            public string HelperId { get; }

            public virtual void Tick(HintLevel level)
            {
            }

            public virtual HintEntry Explain(string trigger, string context, HintLevel level) => null;

            public virtual void Stop()
            {
            }

            protected static bool Is(string value, string expected) =>
                string.Equals(value, expected, StringComparison.Ordinal);
        }

        // trapfloor.reach: the reach markers (doc 23 §9) and, while holding the pawn over a tile the rules do not
        // allow, a RULE reminder saying why. (The landing check itself is a rule check and runs without hints.)
        internal sealed class ReachHintHelper : TrapFloorHintHelper
        {
            public ReachHintHelper(TabletopComposition owner) : base(owner, "trapfloor.reach")
            {
            }

            public override void Tick(HintLevel level)
            {
                owner.TrapFloorGame.UpdateReachHint(level != HintLevel.Off);
                owner.TrapFloorGame.UpdateHoldReminder(level != HintLevel.Off);
            }

            public override void Stop()
            {
                owner.TrapFloorGame.reachHintShown = false;
                owner.boardFeedback?.HideReach();
                owner.TrapFloorGame.UpdateHoldReminder(false);
            }
        }

        // trapfloor.prize: the active pawn stands on a face-up Ability or Friend it may take.
        internal sealed class PrizeHintHelper : TrapFloorHintHelper
        {
            private PlayerId shownFor;
            private int shownRound = -1;

            public PrizeHintHelper(TabletopComposition owner) : base(owner, "trapfloor.prize")
            {
            }

            // P7 (s157): the line speaks about one turn's pawn on one tile; it goes once that is no longer so (another
            // turn, another phase, the pawn moved off, the prize taken, the floor over), instead of staying up.
            public override void Tick(HintLevel level)
            {
                if (!owner.TrapFloorGame.IsShownHint(TrapFloorCartridge.PrizeHintId)) { shownRound = -1; return; }
                TrapFloorTurnState turn = owner.trapFloorTurnState;
                if (shownRound < 0 && turn != null) { shownFor = turn.ActivePlayerId; shownRound = turn.CurrentRound; }
                bool stale = level == HintLevel.Off || turn == null || turn.IsCurrentFloorFailed || turn.Phase != TrapFloorTurnPhase.PlayerTurn
                    || turn.ActivePlayerId != shownFor || turn.CurrentRound != shownRound || owner.ActivePlayerPlaysElsewhere
                    || !owner.TrapFloorGame.TryGetActivePawnPrizeTile(out _);
                if (!stale) return;
                shownRound = -1;
                owner.RulesCardPanel.HideShownHint();
            }

            public override HintEntry Explain(string trigger, string context, HintLevel level)
            {
                bool moved = Is(trigger, HintTriggers.PawnMoved) && Is(context, "own");
                bool revealed = Is(trigger, HintTriggers.ContentRevealed)
                    && (Is(context, nameof(TrapFloorFloorContentCategory.Ability))
                        || Is(context, nameof(TrapFloorFloorContentCategory.Friend)));
                if ((!moved && !revealed)
                    || owner.ActivePlayerPlaysElsewhere // P7: "you" would be another computer's player
                    || owner.TrapFloorGame.tooFarStateIndex >= 0 // the move is not allowed: say that, not "take it"
                    || (owner.pendingControllerPurchaseState != null && owner.pendingControllerPurchaseState.IsActive)
                    || !owner.TrapFloorGame.TryGetActivePawnPrizeTile(out TrapFloorFloorCardState prize))
                {
                    return null;
                }

                return TrapFloorCartridge.HelperReminder(TrapFloorCartridge.PrizeHintId, trigger, context,
                    $"You are standing on {prize.Content.DisplayName}. Choose {TrapFloorCartridge.TakePrizeLabel(prize)} in the turn menu, "
                    + "or leave it: the next player to land here may take it.",
                    TrapFloorCartridge.HelperHintPriority);
            }
        }

        // trapfloor.traps-waiting: a Trap that hit the active player waits until End Turn; a Reaction can stop it.
        internal sealed class TrapsWaitingHintHelper : TrapFloorHintHelper
        {
            private readonly List<string> lines = new List<string>(4);

            private PlayerId shownFor;
            private int shownRound = -1;

            public TrapsWaitingHintHelper(TabletopComposition owner) : base(owner, "trapfloor.traps-waiting")
            {
            }

            // P7 (s157, "stale hint banner persists into another turn"): the line is about the effects waiting on one
            // turn's End Turn; it goes when that turn is over (or nothing waits any more), instead of staying up.
            public override void Tick(HintLevel level)
            {
                if (!owner.TrapFloorGame.IsShownHint(TrapFloorCartridge.TrapsWaitingHintId)) { shownRound = -1; return; }
                TrapFloorTurnState turn = owner.trapFloorTurnState;
                if (shownRound < 0 && turn != null) { shownFor = turn.ActivePlayerId; shownRound = turn.CurrentRound; }
                bool stale = level == HintLevel.Off || turn == null || turn.IsCurrentFloorFailed || turn.Phase != TrapFloorTurnPhase.PlayerTurn
                    || turn.ActivePlayerId != shownFor || turn.CurrentRound != shownRound || owner.ActivePlayerPlaysElsewhere;
                if (!stale)
                {
                    lines.Clear();
                    stale = owner.TrapFloorGame.PendingEndTurnEffects(turn.ActivePlayerId, lines) == 0;
                }

                if (!stale) return;
                shownRound = -1;
                owner.RulesCardPanel.HideShownHint();
            }

            public override HintEntry Explain(string trigger, string context, HintLevel level)
            {
                if (!Is(trigger, HintTriggers.ContentRevealed)
                    || !Is(context, nameof(TrapFloorFloorContentCategory.Trap))
                    || owner.ActivePlayerPlaysElsewhere // P7: "you" would be another computer's player
                    || owner.trapFloorTurnState == null
                    || owner.trapFloorTurnState.IsCurrentFloorFailed
                    || owner.trapFloorTurnState.Phase != TrapFloorTurnPhase.PlayerTurn)
                {
                    return null;
                }

                lines.Clear();
                int waiting = owner.TrapFloorGame.PendingEndTurnEffects(owner.trapFloorTurnState.ActivePlayerId, lines);
                if (waiting == 0) return null;
                string what = waiting == 1
                    ? $"Waiting until you end your turn: {lines[0]}"
                    : $"{waiting} effects wait until you end your turn: {string.Join(" ", lines)}";
                return TrapFloorCartridge.HelperReminder(TrapFloorCartridge.TrapsWaitingHintId, trigger, context,
                    $"{what} Play a Reaction (Dodge, Shield) first if you want to stop it.",
                    TrapFloorCartridge.HelperHintPriority);
            }
        }

        // trapfloor.collapse: during the floor turn, the tile the floor dice point at is marked (and explained once
        // it collapses) until the next player turn. Reads only object references each frame; no allocation.
        internal sealed class CollapseTargetHintHelper : TrapFloorHintHelper
        {
            private TrapFloorCollapseRollState rollBeforeFloorTurn;
            private TrapFloorCollapseRollState shownRoll;

            private int explainedRound = -1;
            private PlayerId explainedFor;

            public CollapseTargetHintHelper(TabletopComposition owner) : base(owner, "trapfloor.collapse")
            {
            }

            // P7 (s157, live run: "the floor dice rolled …: column …, row … is a hole now" stayed up for the whole next
            // round): the line explains the collapse to the turn right after the floor turn; it goes when that turn ends.
            private void HideExplanationAfterFirstTurn(TrapFloorTurnState turn)
            {
                if (!owner.TrapFloorGame.IsShownHint(TrapFloorCartridge.CollapseHintId)) { explainedRound = -1; return; }
                if (turn == null || turn.IsCurrentFloorFailed) { explainedRound = -1; owner.RulesCardPanel.HideShownHint(); return; }
                if (explainedRound < 0) { explainedRound = turn.CurrentRound; explainedFor = turn.ActivePlayerId; return; }
                if (turn.CurrentRound == explainedRound && turn.ActivePlayerId == explainedFor) return;
                explainedRound = -1;
                owner.RulesCardPanel.HideShownHint();
            }

            public override void Tick(HintLevel level)
            {
                TrapFloorCollapseState collapse = owner.trapFloorCollapseState;
                TrapFloorTurnState turn = owner.trapFloorTurnState;
                bool floorTurn = collapse != null
                    && turn != null
                    && !turn.IsCurrentFloorFailed
                    && turn.Phase == TrapFloorTurnPhase.FloorTurn;
                if (!floorTurn)
                {
                    rollBeforeFloorTurn = collapse?.LastRoll; // a roll from an earlier floor turn is not shown
                    Hide();
                    HideExplanationAfterFirstTurn(turn);
                    return;
                }

                explainedRound = -1;

                TrapFloorCollapseRollState roll = collapse.LastRoll;
                if (level == HintLevel.Off || roll == null || roll == rollBeforeFloorTurn)
                {
                    Hide();
                    return;
                }

                if (roll == shownRoll) return;
                // FIX-3 (owner, 2026-10-22: "already removed floor tile was marked for collapse"): every recorded roll's
                // tile is already gone (collapsed, or the one just collapsed), so the hint marks nothing; the tile the
                // dice point at before Collapse is marked by the floor turn itself (ROUND-1).
                shownRoll = roll;
            }

            public override HintEntry Explain(string trigger, string context, HintLevel level)
            {
                TrapFloorCollapseRollState roll = owner.trapFloorCollapseState?.LastRoll;
                if (!Is(trigger, HintTriggers.GameActivity)
                    || !Is(context, nameof(TrapFloorActivityKind.CollapsedFloor))
                    || roll == null)
                {
                    return null;
                }

                return TrapFloorCartridge.HelperReminder(TrapFloorCartridge.CollapseHintId, trigger, context,
                    TrapFloorCartridge.CollapseHintText(roll),
                    TrapFloorCartridge.HelperHintPriority);
            }

            public override void Stop()
            {
                Hide();
            }

            private void Hide()
            {
                if (shownRoll == null) return;
                shownRoll = null;
                owner.floorfallTargetPresenter?.Clear();
            }
        }
    }
}
