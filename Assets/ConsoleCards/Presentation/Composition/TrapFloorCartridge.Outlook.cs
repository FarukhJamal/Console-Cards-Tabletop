using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Definitions;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// P7 (s162): the floor's standing checks, run on the computer that holds the table's authority after a deal and
    /// after every collapse. A joined computer never runs them (it does not know what face-down tiles hold).
    /// <list type="bullet">
    /// <item>Grand Exit invariant (owner rule 2026-11-08): a floor without exactly one standing Grand Exit is a software
    /// defect. It is reported as one (error log, a table message that names no hidden tile), never as a lost floor.</item>
    /// <item>Escape outlook: for each player still on the floor, whether a usable exit is certainly out of reach
    /// (<see cref="TrapFloorEscapeOutlook"/>). Diagnostic only: no approved rule ends a floor for it, so nothing is
    /// changed; the host log names the verdict (it is never sent, as it depends on where face-down exits are).</item>
    /// </list>
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        internal TrapFloorGrandExitInvariant LastGrandExitInvariant { get; private set; } = TrapFloorGrandExitInvariant.Holds;

        private bool HoldsTableAuthority => !(host.IsOnlineTable && !(host.HasOnline && host.Online.IsHosting));

        internal void ReviewFloorStanding(string when)
        {
            if (!HoldsTableAuthority || host.trapFloorTemplate == null || host.matchState == null) return;
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;

            List<TrapFloorCoordinate> grand = new List<TrapFloorCoordinate>();
            HashSet<TrapFloorCoordinate> exits = new HashSet<TrapFloorCoordinate>();
            HashSet<TrapFloorCoordinate> faceDown = new HashSet<TrapFloorCoordinate>();
            HashSet<TrapFloorCoordinate> holes = new HashSet<TrapFloorCoordinate>();
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in template.FloorCardIds)
            {
                bool hole = host.trapFloorCollapseState != null && host.trapFloorCollapseState.IsCollapsed(pair.Value);
                if (hole) holes.Add(pair.Key);
                if (!template.TryGetFloorCardState(host.matchState, pair.Value, out TrapFloorFloorCardState floor)) continue;
                if (!floor.IsRevealed) faceDown.Add(pair.Key);
                if (floor.Content.Category != TrapFloorFloorContentCategory.SecretExit) continue;
                if (TrapFloorObjectiveUseCase.IsGrandExit(floor.Content)) grand.Add(pair.Key);
                if (!hole) exits.Add(pair.Key);
            }

            TrapFloorGrandExitInvariant invariant = TrapFloorGrandExitCheck.Check(grand, holes.Contains);
            LastGrandExitInvariant = invariant;
            if (invariant != TrapFloorGrandExitInvariant.Holds)
            {
                Debug.LogError($"[Invariant] Floor {template.FloorIndex + 1}: Grand Exit {invariant} ({when}). "
                    + "This is a software defect, not a lost floor.", host);
                host.FeedbackLine(
                    $"Software check failed on this floor (Grand Exit {invariant.ToString().ToLowerInvariant()}). "
                    + "This is a bug, not a loss: please report it.", FeedbackSeverity.Warning);
                return; // no outlook on a broken floor
            }

            if (host.trapFloorTurnState == null || host.trapFloorTurnState.IsCurrentFloorFailed) return;
            if (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsWon) return;

            bool anyMoveCard = false;
            List<PlayerId> onFloor = new List<PlayerId>();
            foreach (PlayerId player in host.trapFloorTurnState.PlayerOrder)
            {
                if (host.trapFloorTurnState.GetPlayerState(player) != TrapFloorCurrentFloorPlayerState.Active) continue;
                if (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsPlayerEscaped(player)) continue;
                onFloor.Add(player);
                List<TabletopObjectId> cards = new List<TabletopObjectId>();
                CollectHeldAbilities(player, false, cards, new List<CardDefinition>());
                if (cards.Count > 0) anyMoveCard = true;
            }

            int noEscape = 0;
            foreach (PlayerId player in onFloor)
            {
                if (!TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                    || !TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate tile)) continue;
                TrapFloorEscapeOutlook outlook = TrapFloorEscapeOutlook.Evaluate(
                    template.GridColumns, template.GridRows, tile, holes.Contains, exits.Contains, faceDown.Contains, anyMoveCard);
                if (outlook.Verdict == TrapFloorEscapeVerdict.NoEscape) noEscape++;
                if (outlook.Verdict != TrapFloorEscapeVerdict.CanReachExit)
                    Debug.Log($"[Outlook] Floor {template.FloorIndex + 1} ({when}): {FormatPlayerName(player)}: "
                        + $"{outlook.Verdict}: {outlook.Reason}.", host);
            }

            if (onFloor.Count > 0 && noEscape == onFloor.Count)
                Debug.LogWarning($"[Outlook] Floor {template.FloorIndex + 1} ({when}): no player still on the floor can "
                    + "reach an exit under the written rules. No approved rule ends the floor for this (owner decision "
                    + "pending); play continues.", host);
        }
    }
}
