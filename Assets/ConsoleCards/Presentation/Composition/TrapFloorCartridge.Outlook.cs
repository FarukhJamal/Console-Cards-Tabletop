using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;
using ConsoleCards.Games.TrapFloor;
using ConsoleCards.Presentation.UI;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// P7 (s162/s163): the floor's standing checks, run on the computer that holds the table's authority after a deal
    /// and after every collapse. A joined computer never runs them (it does not know what face-down tiles hold).
    /// <list type="bullet">
    /// <item>Grand Exit invariant (owner rule 2026-11-08): a floor without exactly one standing Grand Exit is a software
    /// defect, reported as one (error log, a table message that names no tile), never as a lost floor.</item>
    /// <item>No escape (owner decision 2026-11-08, option A): when the host PROVES that no player still on the floor
    /// can ever escape (<see cref="TrapFloorEscapeVerdict.Impossible"/> for every one of them), they are put out
    /// through the game's own elimination command, and the existing floor-end rules decide the result: lost when
    /// nobody escaped, won when the mode's win is already met by those who escaped (Free for all: everyone still in
    /// has escaped; Survival: someone escaped). Undetermined never puts anyone out.</item>
    /// <item>Where the proof cannot be relied on, the result stays a host log line: on a normal online table every
    /// joined computer re-checks an elimination itself and could not see what the host sees, so there the existing
    /// end conditions alone apply.</item>
    /// </list>
    /// The search runs at most once per table revision.
    /// </summary>
    internal sealed partial class TrapFloorCartridge
    {
        internal TrapFloorGrandExitInvariant LastGrandExitInvariant { get; private set; } = TrapFloorGrandExitInvariant.Holds;

        private readonly Dictionary<PlayerId, TrapFloorEscapeOutlook> provenOutlook = new Dictionary<PlayerId, TrapFloorEscapeOutlook>();
        private long outlookRevision = -1;
        private TrapFloorTemplateDefinition outlookFloor;

        private bool HoldsTableAuthority => !(host.IsOnlineTable && !(host.HasOnline && host.Online.IsHosting));

        // Auto elimination where the host's proof is the authority: offline, the private session, and (P8A, owner
        // 2026-11-13: "the host must resolve the loss once and replicate the terminal state") a normal online table's
        // host. A joined computer takes the host's elimination as the host checked it (TrustsHostsNoEscapeProof) and the
        // whole table follows; it never runs the proof itself.
        private bool NoEscapeEndsFloorHere => HoldsTableAuthority;

        // A joined computer replaying the host's NoEscapePossible elimination: the host proved it (and checked any
        // player's request before relaying it); this computer cannot run the proof, so it accepts the host's.
        internal bool TrustsHostsNoEscapeProof =>
            host.IsOnlineTable && host.HasOnline && host.Online.IsClientTable && host.Replicated != null && host.Replicated.Replaying;

        internal bool IsProvenTrapped(PlayerId player) =>
            NoEscapeEndsFloorHere
            && host.matchState != null
            && outlookRevision == host.matchState.Revision
            && ReferenceEquals(outlookFloor, host.trapFloorTemplate)
            && provenOutlook.TryGetValue(player, out TrapFloorEscapeOutlook o)
            && o.Verdict == TrapFloorEscapeVerdict.Impossible;

        internal void ReviewFloorStanding(string when)
        {
            if (!HoldsTableAuthority || host.trapFloorTemplate == null || host.matchState == null) return;
            TrapFloorTemplateDefinition template = host.trapFloorTemplate;

            List<TrapFloorCoordinate> grand = new List<TrapFloorCoordinate>();
            HashSet<TrapFloorCoordinate> exits = new HashSet<TrapFloorCoordinate>();
            HashSet<TrapFloorCoordinate> faceDown = new HashSet<TrapFloorCoordinate>();
            HashSet<TrapFloorCoordinate> holes = new HashSet<TrapFloorCoordinate>();
            HashSet<TrapFloorCoordinate> takeableCards = new HashSet<TrapFloorCoordinate>();
            foreach (KeyValuePair<TrapFloorCoordinate, TabletopObjectId> pair in template.FloorCardIds)
            {
                bool takenPrize = false;
                bool hole = host.trapFloorCollapseState != null
                    && host.trapFloorCollapseState.TryGetCollapsedFloor(pair.Value, out TrapFloorCollapsedFloorState collapsed)
                    && !(takenPrize = collapsed.IsTakenPrize); // a taken prize's cell is not a hole (counted as standing)
                if (hole) holes.Add(pair.Key);
                if (!template.TryGetFloorCardState(host.matchState, pair.Value, out TrapFloorFloorCardState floor)) continue;
                if (!floor.IsRevealed) faceDown.Add(pair.Key);
                TrapFloorFloorContentCategory kind = floor.Content.Category;
                if (!hole && !takenPrize && floor.IsRevealed
                    && (kind == TrapFloorFloorContentCategory.Ability || kind == TrapFloorFloorContentCategory.Friend))
                    takeableCards.Add(pair.Key);
                if (kind != TrapFloorFloorContentCategory.SecretExit) continue;
                if (TrapFloorObjectiveUseCase.IsGrandExit(floor.Content)) grand.Add(pair.Key);
                if (!hole) exits.Add(pair.Key);
            }

            TrapFloorGrandExitInvariant invariant = TrapFloorGrandExitCheck.Check(grand, holes.Contains);
            LastGrandExitInvariant = invariant;
            if (invariant != TrapFloorGrandExitInvariant.Holds)
            {
                provenOutlook.Clear();
                outlookRevision = -1;
                Debug.LogError($"[Invariant] Floor {template.FloorIndex + 1}: Grand Exit {invariant} ({when}). "
                    + "This is a software defect, not a lost floor.", host);
                host.FeedbackLine(
                    $"Software check failed on this floor (Grand Exit {invariant.ToString().ToLowerInvariant()}). "
                    + "This is a bug, not a loss: please report it.", FeedbackSeverity.Warning);
                return; // never a loss on a broken floor
            }

            if (host.trapFloorTurnState == null || host.trapFloorTurnState.IsCurrentFloorFailed) return;
            if (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsWon) return;
            if (outlookRevision == host.matchState.Revision && ReferenceEquals(outlookFloor, template)) return; // nothing changed

            // Every seat's held Ability or Friend cards count, escaped or not: help from another player is a move too.
            bool anyMoveCard = false;
            string heldDetail = null;
            ControllerInputCardCatalog.TryCreate(template.GameDefinition, out ControllerInputCardCatalog controllers);
            HashSet<TabletopObjectId> floorCards = new HashSet<TabletopObjectId>(template.FloorCardIds.Values);
            foreach (PlayerId player in host.trapFloorTurnState.PlayerOrder)
            {
                if (HoldsSpecialCard(player, controllers, floorCards, out string where))
                {
                    anyMoveCard = true;
                    heldDetail = $"{FormatPlayerName(player)} holds one {where}"; // P8A diagnostics: where, never which card
                    break;
                }
            }

            // Cards can still come into play while the Shop is open, while any Ability card is left in the Ability deck
            // or its discard pile (a rule vote can open the Shop later; a discard is shuffled back), or while a special
            // card lies loose on the table. Only when none of that is true is "no card can be bought" proven.
            List<string> supply = new List<string>(4);
            if (IsShopOpen()) supply.Add("the Shop is open");
            if (ContainerHasCards(template.AbilityDeckId)) supply.Add($"{CountIn(template.AbilityDeckId)} card(s) left in the Ability deck");
            if (ContainerHasCards(template.AbilityDiscardId)) supply.Add($"{CountIn(template.AbilityDiscardId)} card(s) in the Ability discard");
            if (LooseSpecialCardOnTable(controllers, floorCards)) supply.Add("a special card lies loose on the table");
            bool shopOpen = supply.Count > 0;
            string supplyDetail = string.Join("; ", supply);
            List<PlayerId> onFloor = new List<PlayerId>();
            foreach (PlayerId player in host.trapFloorTurnState.PlayerOrder)
            {
                if (host.trapFloorTurnState.GetPlayerState(player) != TrapFloorCurrentFloorPlayerState.Active) continue;
                if (host.trapFloorObjectiveState != null && host.trapFloorObjectiveState.IsPlayerEscaped(player)) continue;
                onFloor.Add(player);
            }

            provenOutlook.Clear();
            int impossible = 0;
            foreach (PlayerId player in onFloor)
            {
                TrapFloorEscapeOutlook outlook = !TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)
                    || !TryReadPawnTile(setup.PawnId, out TrapFloorCoordinate tile)
                        ? new TrapFloorEscapeOutlook(TrapFloorEscapeVerdict.Undetermined, "the pawn is not on the floor")
                        : TrapFloorEscapeOutlook.Evaluate(template.GridColumns, template.GridRows, tile, holes.Contains,
                            exits.Contains, faceDown.Contains, anyMoveCard, takeableCards.Contains, shopOpen, heldDetail, supplyDetail);
                provenOutlook[player] = outlook;
                if (outlook.Verdict == TrapFloorEscapeVerdict.Impossible) impossible++;
                if (outlook.Verdict != TrapFloorEscapeVerdict.Possible)
                    Debug.Log($"[Outlook] Floor {template.FloorIndex + 1} ({when}): {FormatPlayerName(player)}: "
                        + $"{outlook.Verdict}: {outlook.Reason}.", host);
            }

            outlookRevision = host.matchState.Revision;
            outlookFloor = template;
            List<TrapFloorEscapeOutlook> verdicts = new List<TrapFloorEscapeOutlook>(onFloor.Count);
            foreach (PlayerId player in onFloor) verdicts.Add(provenOutlook[player]);
            bool everyoneOut = TrapFloorNoEscapeRule.PutsEveryoneOut(verdicts);
            if (impossible > 0 || verdicts.Count != onFloor.Count)
                Debug.Log($"[Outlook] Floor {template.FloorIndex + 1} ({when}): {onFloor.Count} still on the floor, {impossible} "
                    + $"PROVABLY_NO_ESCAPE: {(everyoneOut ? "everyone is out" : "play on (someone may still escape, or it is not proven)")}.", host);
            if (!everyoneOut) return; // someone may still escape: play on

            if (!NoEscapeEndsFloorHere)
            {
                Debug.LogWarning($"[Outlook] Floor {template.FloorIndex + 1} ({when}): no player still on the floor can "
                    + "ever escape, but on a normal online table every computer re-checks an elimination and cannot see "
                    + "this proof: the existing end conditions apply.", host);
                return;
            }

            PutOutTrappedPlayers(onFloor);
        }

        // Owner decision 2026-11-08 (option A), through the game's own elimination command (one Undo step, refused by
        // the rules if the proof no longer holds). The floor-end rules then say won or lost.
        private void PutOutTrappedPlayers(List<PlayerId> trapped)
        {
            bool anyOut = false;
            host.SessionService.BeginUndoBatch();
            try
            {
                foreach (PlayerId player in trapped)
                {
                    if (host.trapFloorTurnState.IsCurrentFloorFailed) break;
                    if (host.trapFloorTurnState.GetPlayerState(player) != TrapFloorCurrentFloorPlayerState.Active) continue; // never twice
                    TrapFloorTurnAdvanceResult knocked = SubmitElimination(
                        player, TrapFloorEliminationReason.NoEscapePossible, host.trapFloorTurnState.ActivePlayerId);
                    if (!knocked.Succeeded) continue;
                    anyOut = true;
                    host.FeedbackLine(
                        $"{FormatPlayerName(player).ToUpperInvariant()} can no longer reach any exit: OUT for this floor.",
                        FeedbackSeverity.Loss);
                }
            }
            finally
            {
                host.SessionService.EndUndoBatch();
            }

            if (!anyOut) return;
            provenOutlook.Clear();
            outlookRevision = -1;
            RefreshTrapFloorStatusUi();
            if (host.trapFloorTurnState.IsCurrentFloorFailed) ShowFloorEnded(FloorFailedCountsAsWin());
        }

        private bool ContainerHasCards(ContainerId id) =>
            !id.IsEmpty && host.matchState.Containers.TryGetValue(id, out ContainerState c) && c.Count > 0;

        private bool LooseSpecialCardOnTable(ControllerInputCardCatalog controllers, HashSet<TabletopObjectId> floorCards)
        {
            foreach (KeyValuePair<TabletopObjectId, CardInstanceState> pair in host.matchState.Cards)
            {
                if (!pair.Value.BaseState.ContainerId.IsEmpty) continue;
                if (floorCards.Contains(pair.Key))
                {
                    // A taken Ability or Friend prize lies by its owner's Console: it is held, and may move a pawn.
                    if (host.trapFloorCollapseState != null
                        && host.trapFloorCollapseState.TryGetCollapsedFloor(pair.Key, out TrapFloorCollapsedFloorState taken)
                        && taken.IsTakenPrize
                        && host.trapFloorTemplate.TryGetFloorCardState(host.matchState, pair.Key, out TrapFloorFloorCardState prize)
                        && prize.Content.Category != TrapFloorFloorContentCategory.Key)
                        return true;
                    continue;
                }
                if (controllers != null && controllers.TryGetInput(pair.Value.BaseState.DefinitionId, out _)) continue;
                return true;
            }

            return false;
        }

        // An Ability or Friend card in the seat's hand, beside its Console or in a slot. A Controller card (it pays costs,
        // it never moves a pawn) and a taken Key do not count; anything else, including a card this computer cannot
        // name, is counted as one that could move a pawn.
        private bool HoldsSpecialCard(PlayerId player, ControllerInputCardCatalog controllers, HashSet<TabletopObjectId> floorCards, out string where)
        {
            where = null;
            if (!TryGetTrapFloorPlayerSetup(player, out TrapFloorPlayerSetupDefinition setup)) return false;
            if (HasSpecialCardIn(setup.HandContainerId, controllers, floorCards)) { where = "in the Hand"; return true; }
            if (HasSpecialCardIn(setup.ActionAbilityAreaContainerId, controllers, floorCards)) { where = "beside the Console"; return true; }
            if (HasSpecialCardIn(setup.MainSlotContainerId, controllers, floorCards)) { where = "in the Main slot (an Avatar counts: it may move a pawn)"; return true; }
            for (int i = 0; i < setup.SideSlotContainerIds.Count; i++)
            {
                if (HasSpecialCardIn(setup.SideSlotContainerIds[i], controllers, floorCards)) { where = "in a Console slot"; return true; }
            }

            return false;
        }

        private int CountIn(ContainerId id) =>
            !id.IsEmpty && host.matchState.Containers.TryGetValue(id, out ContainerState c) ? c.Count : 0;

        private bool HasSpecialCardIn(ContainerId containerId, ControllerInputCardCatalog controllers, HashSet<TabletopObjectId> floorCards)
        {
            if (containerId.IsEmpty || !host.matchState.Containers.TryGetValue(containerId, out ContainerState container)) return false;
            for (int i = 0; i < container.Count; i++)
            {
                TabletopObjectId cardId = container.GetObjectAt(i);
                if (!host.matchState.Cards.TryGetValue(cardId, out CardInstanceState card)) return true; // unknown: assume it could
                if (controllers != null && controllers.TryGetInput(card.BaseState.DefinitionId, out _)) continue;
                if (floorCards.Contains(cardId)
                    && host.trapFloorTemplate.TryGetFloorCardState(host.matchState, cardId, out TrapFloorFloorCardState floor)
                    && floor.Content.Category == TrapFloorFloorContentCategory.Key) continue;
                return true;
            }

            return false;
        }
    }
}
