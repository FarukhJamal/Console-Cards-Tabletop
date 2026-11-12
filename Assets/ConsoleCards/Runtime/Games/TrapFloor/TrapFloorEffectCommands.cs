using System;
using System.Collections.Generic;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    // AUTH-EFFECTS (owner, 2026-11-06: "migrate gameplay-relevant state changes to host-executed commands and effects"):
    // the Trap and Ability steps a player's own computer used to run on its copy of the rules state, then push. Each is
    // now a table command. It runs at once on the player's computer (the screen answers as before), then the host
    // replays it against its own table: the host decides from its own state whether the effect applies, to whom, for how
    // long and whether it already happened, and only then is it sent to everyone. The command carries what the player
    // chose (which Trap, which card, which die), never a resulting status or value.

    /// <summary>Apply a waiting Fall / Blind / Slow / Sticky Trap of the player whose turn it is.</summary>
    public sealed class TrapFloorApplyTrapCommand : ITabletopCommand
    {
        public TrapFloorApplyTrapCommand(CommandContext context, TabletopObjectId floorCardId)
        {
            Context = context;
            FloorCardId = floorCardId;
        }

        public CommandContext Context { get; }
        public TabletopObjectId FloorCardId { get; }
    }

    /// <summary>Resolve a waiting Spring or Crane Trap (the Crane reads the floor dice on the table that runs it).</summary>
    public sealed class TrapFloorLaunchTrapCommand : ITabletopCommand
    {
        public TrapFloorLaunchTrapCommand(CommandContext context, TabletopObjectId floorCardId)
        {
            Context = context;
            FloorCardId = floorCardId;
        }

        public CommandContext Context { get; }
        public TabletopObjectId FloorCardId { get; }
    }

    /// <summary>Read a settled d4 as this turn's Blind direction (the value is the die's on the table that runs it).</summary>
    public sealed class TrapFloorRollBlindDirectionCommand : ITabletopCommand
    {
        public TrapFloorRollBlindDirectionCommand(CommandContext context, TabletopObjectId dieId)
        {
            Context = context;
            DieId = dieId;
        }

        public CommandContext Context { get; }
        public TabletopObjectId DieId { get; }
    }

    /// <summary>Survival endgame: a face-up Trap the player's pawn landed on triggers again.</summary>
    public sealed class TrapFloorRetriggerTrapCommand : ITabletopCommand
    {
        public TrapFloorRetriggerTrapCommand(CommandContext context, TabletopObjectId floorCardId)
        {
            Context = context;
            FloorCardId = floorCardId;
        }

        public CommandContext Context { get; }
        public TabletopObjectId FloorCardId { get; }
    }

    /// <summary>Use the Ability card that lies in a Console slot (Check, Disarm, Shield, Dodge, Rush).</summary>
    public sealed class TrapFloorUseAbilityCommand : ITabletopCommand
    {
        public TrapFloorUseAbilityCommand(CommandContext context, TabletopObjectId cardInstanceId, ContainerId slotContainerId)
        {
            Context = context;
            CardInstanceId = cardInstanceId;
            SlotContainerId = slotContainerId;
        }

        public CommandContext Context { get; }
        public TabletopObjectId CardInstanceId { get; }
        public ContainerId SlotContainerId { get; }
    }

    /// <summary>A guided move (Dodge, Rush, Spring, Crane) reached a lit tile: the guidance ends.</summary>
    public sealed class TrapFloorClearGuidanceCommand : ITabletopCommand
    {
        public TrapFloorClearGuidanceCommand(CommandContext context)
        {
            Context = context;
        }

        public CommandContext Context { get; }
    }

    /// <summary>Why a player is out for the floor. There is no concession: the rules have none.</summary>
    public enum TrapFloorEliminationReason
    {
        None = 0,
        /// <summary>The player escaped (Secret Exit or Grand Exit) and takes no more turns this floor.</summary>
        Escaped = 1,
        /// <summary>No Key of their own (or their team's) and no Key left to take.</summary>
        StuckWithoutKey = 2,
        /// <summary>The pawn stands on a collapsed tile, an empty cell, or off the floor (fell).</summary>
        Fell = 3,
        /// <summary>
        /// Owner decision 2026-11-08 (option A): the host proved no legal way to escape is left for this player
        /// (TrapFloorEscapeVerdict.Impossible). Never on an undetermined outlook.
        /// </summary>
        NoEscapePossible = 4,
    }

    /// <summary>A player is out for the floor, for a reason the table that runs it must see for itself.</summary>
    public sealed class TrapFloorEliminatePlayerCommand : ITabletopCommand
    {
        public TrapFloorEliminatePlayerCommand(CommandContext context, PlayerId playerId, TrapFloorEliminationReason reason)
        {
            Context = context;
            PlayerId = playerId;
            Reason = reason;
        }

        public CommandContext Context { get; }
        public PlayerId PlayerId { get; }
        public TrapFloorEliminationReason Reason { get; }
    }

    public enum TrapFloorEffectError
    {
        None = 0,
        Unavailable = 1,
        NotTheActivePlayer = 2,
        NotOnThatTile = 3,
        NotAllowedNow = 4,
        NoReasonOnThisTable = 5,
        AlreadyOut = 6,
        Rejected = 7,
    }

    public readonly struct TrapFloorEffectResult
    {
        private TrapFloorEffectResult(bool succeeded, long revision, TrapFloorEffectError error)
        {
            Succeeded = succeeded;
            Revision = revision;
            Error = error;
        }

        public bool Succeeded { get; }
        public long Revision { get; }
        public TrapFloorEffectError Error { get; }

        public static TrapFloorEffectResult Accepted(long revision) => new TrapFloorEffectResult(true, revision, TrapFloorEffectError.None);

        public static TrapFloorEffectResult Failure(TrapFloorEffectError error) => new TrapFloorEffectResult(false, 0, error);
    }

    /// <summary>What a table can see for itself about a player (read from that table, never from a request).</summary>
    public interface ITrapFloorEffectEvidence
    {
        bool IsEscaped(PlayerId player);
        bool StandsOnHoleOrOffFloor(PlayerId player);
        bool IsStuckWithoutKey(PlayerId player);
        /// <summary>The authoritative host proved escape impossible for this player (false wherever that is not proven).</summary>
        bool CannotEscape(PlayerId player);
        bool StandsOnFloorCard(PlayerId player, TabletopObjectId floorCardId);
        bool IsSurvivalEndgame { get; }
    }

    /// <summary>AUTH-EFFECTS: whether an elimination has a reason on this table. Pure, so it is unit tested.</summary>
    public static class TrapFloorEliminationRules
    {
        public static bool HasReason(TrapFloorEliminationReason reason, PlayerId player, ITrapFloorEffectEvidence evidence)
        {
            if (evidence == null || player.IsEmpty) return false;
            switch (reason)
            {
                case TrapFloorEliminationReason.Escaped: return evidence.IsEscaped(player);
                case TrapFloorEliminationReason.StuckWithoutKey: return evidence.IsStuckWithoutKey(player);
                case TrapFloorEliminationReason.Fell: return evidence.StandsOnHoleOrOffFloor(player);
                case TrapFloorEliminationReason.NoEscapePossible: return evidence.CannotEscape(player);
                default: return false; // no concession in the rules: a player cannot simply declare themselves out
            }
        }
    }

    /// <summary>
    /// AUTH-EFFECTS: runs the effect commands on one table with that table's own services and state. Every computer
    /// runs it the same way (the player's at once, the host when it replays, the others when the host sends it), so the
    /// host's decision is the one that stands: a command its table refuses is not sent on, and the player's table is set
    /// right. Duplicates: the host runs a command id once (HostCommandPolicy), and each step refuses a second application
    /// itself (a Trap no longer waiting, an Ability already used this turn, a Blind direction already rolled, a player
    /// already out).
    /// </summary>
    public sealed class TrapFloorEffectExecutor
    {
        private readonly TrapFloorAbilityResolutionService ability;
        private readonly TrapFloorAbilityResolutionState abilityState;
        private readonly TrapFloorTurnService turnService;
        private readonly TrapFloorTurnState turn;
        private readonly TrapFloorTemplateDefinition template;
        private readonly ITrapFloorEffectEvidence evidence;
        private readonly Func<ObjectDefinitionId, CardDefinitionData> definitionOf;

        public TrapFloorEffectExecutor(
            TrapFloorAbilityResolutionService ability,
            TrapFloorAbilityResolutionState abilityState,
            TrapFloorTurnService turnService,
            TrapFloorTurnState turn,
            TrapFloorTemplateDefinition template,
            ITrapFloorEffectEvidence evidence,
            Func<ObjectDefinitionId, CardDefinitionData> definitionOf)
        {
            this.ability = ability;
            this.abilityState = abilityState;
            this.turnService = turnService;
            this.turn = turn;
            this.template = template;
            this.evidence = evidence;
            this.definitionOf = definitionOf;
        }

        private bool Ready => ability != null && abilityState != null && turnService != null && turn != null;

        private bool IsActive(PlayerId requester) =>
            !turn.IsCurrentFloorFailed && turn.Phase == TrapFloorTurnPhase.PlayerTurn && turn.ActivePlayerId == requester;

        public TrapFloorTurnAdvanceResult ApplyTrap(MatchState match, TrapFloorApplyTrapCommand command)
        {
            if (!Ready || match == null || command == null)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.MatchMismatch);
            // The Fall branch of the service checks the Trap's player, not the requester: only that player applies it.
            if (!IsActive(command.Context.RequestedByPlayerId))
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);
            if (!abilityState.TryGetTrap(command.FloorCardId, out TrapFloorTrapResolutionRecord trap)
                || trap.Content.IsLaunchTrap)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);
            return ability.ResolveSupportedTrap(match, command.Context, command.FloorCardId, turnService);
        }

        public TrapFloorLaunchResult LaunchTrap(MatchState match, TrapFloorLaunchTrapCommand command)
        {
            if (!Ready || match == null || command == null) return TrapFloorLaunchResult.Failure(TrapFloorLaunchError.Unavailable);
            return ability.ResolveLaunchTrap(match, command.Context, command.FloorCardId, turnService);
        }

        public TrapFloorBlindDirectionRollResult RollBlindDirection(MatchState match, TrapFloorRollBlindDirectionCommand command)
        {
            if (!Ready || match == null || command == null)
                return TrapFloorBlindDirectionRollResult.Failure(TrapFloorBlindDirectionRollError.MatchMismatch);
            return ability.ResolveBlindDirection(match, command.Context, command.DieId);
        }

        public TrapFloorEffectResult RetriggerTrap(MatchState match, TrapFloorRetriggerTrapCommand command)
        {
            if (!Ready || match == null || command == null || evidence == null) return TrapFloorEffectResult.Failure(TrapFloorEffectError.Unavailable);
            PlayerId requester = command.Context.RequestedByPlayerId;
            if (!IsActive(requester)) return TrapFloorEffectResult.Failure(TrapFloorEffectError.NotTheActivePlayer);
            if (!evidence.IsSurvivalEndgame) return TrapFloorEffectResult.Failure(TrapFloorEffectError.NotAllowedNow);
            if (!evidence.StandsOnFloorCard(requester, command.FloorCardId)) return TrapFloorEffectResult.Failure(TrapFloorEffectError.NotOnThatTile);
            return ability.RetriggerRevealedTrap(match, command.Context, command.FloorCardId, requester)
                ? TrapFloorEffectResult.Accepted(match.Revision)
                : TrapFloorEffectResult.Failure(TrapFloorEffectError.Rejected);
        }

        public TrapFloorAbilityActivationResult UseAbility(MatchState match, TrapFloorUseAbilityCommand command)
        {
            if (!Ready || match == null || command == null || template == null || definitionOf == null)
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.CardIdentityMismatch, TrapFloorAbilityEffect.None);
            if (!match.Cards.TryGetValue(command.CardInstanceId, out CardInstanceState card)
                || card.BaseState.ContainerId != command.SlotContainerId)
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.CardIdentityMismatch, TrapFloorAbilityEffect.None);
            PlayerId owner = ConsoleOwnerOf(match, command.SlotContainerId);
            if (owner.IsEmpty)
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.CardIdentityMismatch, TrapFloorAbilityEffect.None);
            CardDefinitionData definition = definitionOf(card.BaseState.DefinitionId); // this table's own card text
            if (definition == null)
                return TrapFloorAbilityActivationResult.Failure(TrapFloorAbilityActivationError.CardIdentityMismatch, TrapFloorAbilityEffect.None);

            ConsoleCardInserted insertion = new ConsoleCardInserted(
                new DomainEventContext(match.Id, match.Revision),
                command.Context.RequestedByPlayerId,
                command.CardInstanceId,
                card.BaseState.DefinitionId,
                owner,
                command.SlotContainerId);
            TrapFloorAbilityActivationResult result = ability.Activate(match, insertion, definition);
            if (result.Succeeded)
            {
                // One step with the card's way into the Console (it amends that Undo step, as before).
                match.AdvanceRevision(
                    command.Context.Id,
                    command.Context.RequestedByPlayerId,
                    AuthoritativeActionKind.Unspecified,
                    AuthoritativeActionRecordMode.None);
            }

            return result;
        }

        public TrapFloorEffectResult ClearGuidance(MatchState match, TrapFloorClearGuidanceCommand command)
        {
            if (!Ready || match == null || command == null || template == null || evidence == null)
                return TrapFloorEffectResult.Failure(TrapFloorEffectError.Unavailable);
            PlayerId requester = command.Context.RequestedByPlayerId;
            IReadOnlyList<TabletopObjectId> targets = null;
            if (abilityState.ActiveRushAssistance != null && abilityState.ActiveRushAssistance.PlayerId == requester)
                targets = abilityState.ActiveRushAssistance.TargetFloorCardIds;
            else if (abilityState.ActiveDodgeAssistance != null && abilityState.ActiveDodgeAssistance.PlayerId == requester)
                targets = abilityState.ActiveDodgeAssistance.TargetFloorCardIds;
            if (targets == null) return TrapFloorEffectResult.Failure(TrapFloorEffectError.NotAllowedNow); // nothing of theirs is lit
            bool onLit = false;
            for (int i = 0; i < targets.Count && !onLit; i++) onLit = evidence.StandsOnFloorCard(requester, targets[i]);
            if (!onLit) return TrapFloorEffectResult.Failure(TrapFloorEffectError.NotOnThatTile);
            if (!ability.ClearMovementAssistance()) return TrapFloorEffectResult.Failure(TrapFloorEffectError.Rejected);
            // P8A batch 4 (CR-1): the Spring/Crane move owed to the tile the pawn stands on is made: that occurrence is
            // done on this table (the host's included), whichever computer moved the pawn.
            if (abilityState.TryGetOwedMove(requester, out TrapFloorTrapResolutionRecord owed)
                && evidence.StandsOnFloorCard(requester, owed.LaunchTargetFloorCardId))
                ability.CompleteOwedMove(requester, owed.LaunchTargetFloorCardId);
            long revision = match.AdvanceRevision(
                command.Context.Id,
                requester,
                AuthoritativeActionKind.Unspecified,
                AuthoritativeActionRecordMode.None);
            return TrapFloorEffectResult.Accepted(revision);
        }

        public TrapFloorTurnAdvanceResult Eliminate(MatchState match, TrapFloorEliminatePlayerCommand command)
        {
            if (!Ready || match == null || command == null)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.MatchMismatch);
            if (!turn.ContainsPlayer(command.PlayerId))
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.PlayerIsNotParticipant);
            if (turn.GetPlayerState(command.PlayerId) == TrapFloorCurrentFloorPlayerState.EliminatedForCurrentRound)
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.PlayerAlreadyEliminatedForCurrentRound);
            if (!TrapFloorEliminationRules.HasReason(command.Reason, command.PlayerId, evidence))
                return TrapFloorTurnAdvanceResult.Failure(TrapFloorTurnAdvanceError.PlayerIsNotParticipant); // no reason on this table
            return turnService.MarkPlayerEliminatedForCurrentRound(match, command.Context, command.PlayerId);
        }

        // The player whose Console has this slot (Main or a side slot), from this table's seats.
        private PlayerId ConsoleOwnerOf(MatchState match, ContainerId slot)
        {
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition player = template.Players[i];
                bool mine = player.MainSlotContainerId == slot;
                for (int s = 0; !mine && s < player.SideSlotContainerIds.Count; s++) mine = player.SideSlotContainerIds[s] == slot;
                if (!mine) continue;
                return match.Seats.TryGetValue(player.SeatId, out var seat) ? seat.OccupantPlayerId : PlayerId.Empty;
            }

            return PlayerId.Empty;
        }
    }

    /// <summary>
    /// AUTH-DICE (owner, 2026-11-06: "the host determines the authoritative random result"): the host's result for a die
    /// whose number a rule reads in Full (the floor dice for a collapse or a Crane, a d4 for Blind). The thrower's physics
    /// still throws the die; the face it shows when it lands is replaced by the host's own random number, and the die
    /// is turned to show it on every computer. A throw again before any rule read the result keeps the host's result (no
    /// fishing for a better face); a new result is drawn when a rule has read the last one, when the turn moved on, or
    /// when the last one asks for a re-roll by the rules (a floor die beyond this floor's size). Pure, so it is tested.
    /// </summary>
    public sealed class TrapFloorHostDice
    {
        private struct Held
        {
            public int Value;
            public long TurnKey;
        }

        private readonly Dictionary<TabletopObjectId, Held> held = new Dictionary<TabletopObjectId, Held>();

        /// <param name="needsReroll">True when the rules ask for this die to be thrown again with that value.</param>
        /// <param name="roll">The host's random source: a number from 1 to the given side count.</param>
        public int Decide(TabletopObjectId dieId, int sides, long turnKey, Func<int, bool> needsReroll, Func<int, int> roll)
        {
            if (sides < 2) throw new ArgumentOutOfRangeException(nameof(sides));
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            if (held.TryGetValue(dieId, out Held last)
                && last.TurnKey == turnKey
                && last.Value >= 1 && last.Value <= sides
                && (needsReroll == null || !needsReroll(last.Value)))
            {
                return last.Value; // thrown again before a rule read it: the same result
            }

            int value = roll(sides);
            if (value < 1 || value > sides) throw new InvalidOperationException("The host's die result is out of range.");
            held[dieId] = new Held { Value = value, TurnKey = turnKey };
            return value;
        }

        /// <summary>A rule read this die's result: its next throw gets a new one.</summary>
        public void Consumed(TabletopObjectId dieId) => held.Remove(dieId);

        public void Clear() => held.Clear();

        public static long TurnKeyOf(TrapFloorTurnState turn) =>
            turn == null ? 0 : ((long)turn.CurrentRound << 20) | ((long)turn.Phase << 16) | (uint)(turn.CapturePosition().ActivePlayerIndex & 0xFFFF);
    }

    /// <summary>
    /// P8A batch 4 (CR-2): one Crane occurrence: this match, this tile, revealed at this revision. A roll counts only for
    /// the occurrence it was asked for, never for a later one (Undo, another floor, another match).
    /// </summary>
    public readonly struct TrapFloorCraneOccurrence : IEquatable<TrapFloorCraneOccurrence>
    {
        public TrapFloorCraneOccurrence(Guid matchId, TabletopObjectId floorCardId, long revealedRevision)
        {
            MatchId = matchId;
            FloorCardId = floorCardId;
            RevealedRevision = revealedRevision;
        }

        public Guid MatchId { get; }
        public TabletopObjectId FloorCardId { get; }
        public long RevealedRevision { get; }
        public bool IsEmpty => FloorCardId.IsEmpty;

        public static TrapFloorCraneOccurrence For(Guid matchId, TrapFloorTrapResolutionRecord record) =>
            record == null ? default : new TrapFloorCraneOccurrence(matchId, record.FloorCardId, record.RevealedRevision);

        public bool Equals(TrapFloorCraneOccurrence other) =>
            MatchId == other.MatchId && FloorCardId == other.FloorCardId && RevealedRevision == other.RevealedRevision;

        public override bool Equals(object obj) => obj is TrapFloorCraneOccurrence other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(MatchId, FloorCardId, RevealedRevision);
    }

    /// <summary>
    /// P8A batch 4 (CR-2, CR-3): the Crane's own roll. It is asked for one occurrence; a roll thrown after the ask makes
    /// it ready with the faces the dice settled on; it stays ready only while the dice still show those faces and only
    /// for that occurrence. Using it (or any new ask) clears it, so a roll is never used twice or for another Crane.
    /// Pure: the cartridge supplies the dice and decides what a roll is.
    /// </summary>
    public sealed class TrapFloorCraneRollGate
    {
        private TrapFloorCraneOccurrence asked;
        private TrapFloorCraneOccurrence ready;
        private int readyColumn;
        private int readyRow;

        public void Ask(TrapFloorCraneOccurrence occurrence)
        {
            asked = occurrence;
            ready = default;
        }

        public bool IsAskedFor(TrapFloorCraneOccurrence occurrence) => !occurrence.IsEmpty && asked.Equals(occurrence);

        /// <summary>The roll asked for that occurrence settled on these faces. False (nothing kept) for any other.</summary>
        public bool Rolled(TrapFloorCraneOccurrence occurrence, int column, int row)
        {
            if (!IsAskedFor(occurrence) || column < 1 || row < 1) return false;
            ready = occurrence;
            readyColumn = column;
            readyRow = row;
            return true;
        }

        public bool IsReadyFor(TrapFloorCraneOccurrence occurrence, int column, int row) =>
            !occurrence.IsEmpty && ready.Equals(occurrence) && readyColumn == column && readyRow == row;

        public void Clear()
        {
            asked = default;
            ready = default;
        }
    }

    /// <summary>What a rule waiting for a die does with it now.</summary>
    /// <summary>
    /// P7 (s160, owner rule 2026-11-08 "movement origin return"): after a Key, Ability or Friend is taken from a floor
    /// tile, the pawn goes back to the tile THIS move began on (C3 → C5 takes a Key → back to C3), never to the spawn
    /// corner unless that is where the move began. Null: the pawn stays (no move origin known, the pawn already stands
    /// on it, or the origin tile cannot be stood on any more). Pure; the cartridge supplies the facts.
    /// </summary>
    public static class TrapFloorPickupReturn
    {
        public static TrapFloorCoordinate? Destination(
            TrapFloorCoordinate? moveOrigin,
            TrapFloorCoordinate pawnTile,
            Func<TrapFloorCoordinate, bool> standable)
        {
            if (!moveOrigin.HasValue || moveOrigin.Value == pawnTile) return null;
            if (standable != null && !standable(moveOrigin.Value)) return null;
            return moveOrigin.Value;
        }
    }

    public enum TrapFloorDieRollStep
    {
        /// <summary>Not rolled since the rule asked (or still moving): keep waiting.</summary>
        Wait,
        /// <summary>Rolled since the rule asked and at rest: read the result once.</summary>
        Read,
        /// <summary>Rolled but cocked: ask for another roll.</summary>
        Cocked,
    }

    /// <summary>
    /// P7 (s158) DICE-EVENT: whether a rule that asked for a roll reads the die now. A roll is an explicit event — a
    /// player's throw on the computer that runs the rule, or a throw from another computer that the host accepted (its
    /// result is the host's) — counted when it happens. It is never inferred from the die having moved: an accepted roll
    /// whose host result is the face the die already showed, resting where it already rested, still resolves once.
    /// A die that only settled again (a rebuilt table, physics waking it) is not a roll. Pure, so it is tested.
    /// </summary>
    public static class TrapFloorDiceRollGate
    {
        public static TrapFloorDieRollStep Next(bool rolledSinceAsked, PhysicalObjectMode mode)
        {
            if (!rolledSinceAsked) return TrapFloorDieRollStep.Wait;
            if (mode == PhysicalObjectMode.SleepingUnresolved) return TrapFloorDieRollStep.Cocked;
            return mode == PhysicalObjectMode.Sleeping ? TrapFloorDieRollStep.Read : TrapFloorDieRollStep.Wait;
        }
    }

    /// <summary>
    /// AUTH-UNDO-2 (owner, 2026-11-06: "do not allow a player to repeatedly Undo an unfavorable rules-driven dice result"):
    /// whether one Undo step holds a rules result, judged from the step's own before and after rules state: a tile
    /// turned over, a Trap applied, launched or triggered again, a status scheduled, a Blind direction rolled, a collapse,
    /// or an elimination. Ability uses, Key claims, escapes and plain moves are not rules results (a player may take back
    /// their own choice). Pure, so it is unit tested.
    /// </summary>
    public static class TrapFloorUndoEligibility
    {
        public static bool IsRulesResult(TrapFloorSessionStateSnapshot before, TrapFloorSessionStateSnapshot after)
        {
            if (before == null || after == null) return false;
            TrapFloorSessionState a = before.Restore();
            TrapFloorSessionState b = after.Restore();

            long last = 0;
            IReadOnlyList<TrapFloorActivityEntry> old = a.Activity.Entries;
            for (int i = 0; i < old.Count; i++) last = Math.Max(last, old[i].Sequence);
            IReadOnlyList<TrapFloorActivityEntry> entries = b.Activity.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Sequence > last && IsRulesKind(entries[i].Kind)) return true;
            }

            // Spring / Crane resolve without a log line; a retrigger adds a waiting Trap: compare the Trap records.
            TrapFloorTrapResolutionRecord[] traps = b.AbilityResolution.CopyTrapRecords();
            TrapFloorTrapResolutionRecord[] earlier = a.AbilityResolution.CopyTrapRecords();
            for (int i = 0; i < traps.Length; i++)
            {
                TrapFloorTrapResolutionRecord was = null;
                for (int j = 0; j < earlier.Length && was == null; j++)
                {
                    if (earlier[j].FloorCardId == traps[i].FloorCardId
                        && earlier[j].AffectedPlayerId == traps[i].AffectedPlayerId
                        && earlier[j].RevealedRevision == traps[i].RevealedRevision)
                        was = earlier[j];
                }

                if (was == null) return true; // a Trap came up in this step
                if (was.Disposition == TrapFloorTrapResolutionDisposition.Pending
                    && (traps[i].Disposition == TrapFloorTrapResolutionDisposition.Resolved
                        || traps[i].Disposition == TrapFloorTrapResolutionDisposition.MoveOwed))
                    return true; // applied or launched in this step (P8A batch 4: a launch now leaves its move owed)
            }

            return Holes(b.Collapse) > Holes(a.Collapse) // a taken prize leaves the floor too, but that is a choice
                || b.Turn.CopyEliminatedPlayerIds().Length > a.Turn.CopyEliminatedPlayerIds().Length;
        }

        private static int Holes(TrapFloorCollapseState collapse)
        {
            int holes = 0;
            for (int i = 0; i < collapse.CollapsedFloors.Count; i++)
            {
                if (!collapse.CollapsedFloors[i].IsTakenPrize) holes++;
            }

            return holes;
        }

        private static bool IsRulesKind(TrapFloorActivityKind kind)
        {
            switch (kind)
            {
                case TrapFloorActivityKind.RevealedFloorContent:
                case TrapFloorActivityKind.SafelyRevealedTrap:
                case TrapFloorActivityKind.CollapsedFloor:
                case TrapFloorActivityKind.PlayerEliminated:
                case TrapFloorActivityKind.AllPlayersEliminated:
                case TrapFloorActivityKind.BlindApplied:
                case TrapFloorActivityKind.BlindDirectionRolled:
                case TrapFloorActivityKind.SlowApplied:
                case TrapFloorActivityKind.StickyApplied:
                case TrapFloorActivityKind.TriggeredFloorfall:
                case TrapFloorActivityKind.RolledFloorfall:
                    return true;
                default:
                    return false;
            }
        }
    }
}
