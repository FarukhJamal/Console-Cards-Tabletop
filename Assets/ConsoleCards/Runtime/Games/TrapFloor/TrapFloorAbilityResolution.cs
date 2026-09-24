using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Events;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorAbilityEffect
    {
        None = 0,
        Check = 1,
        Dodge = 2,
        Shield = 3,
        Rush = 4,
        Disarm = 5,
    }

    public enum TrapFloorTrapResolutionDisposition
    {
        Pending = 0,
        Resolved = 1,
        Disarmed = 2,
        Shielded = 3,
    }

    public readonly struct TrapFloorAbilityActivationRecord : IEquatable<TrapFloorAbilityActivationRecord>
    {
        public TrapFloorAbilityActivationRecord(
            TabletopObjectId cardInstanceId,
            PlayerId playerId,
            int round)
        {
            if (cardInstanceId.IsEmpty) throw new ArgumentException("Ability Card ID cannot be empty.", nameof(cardInstanceId));
            if (playerId.IsEmpty) throw new ArgumentException("Player ID cannot be empty.", nameof(playerId));
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));

            CardInstanceId = cardInstanceId;
            PlayerId = playerId;
            Round = round;
        }

        public TabletopObjectId CardInstanceId { get; }
        public PlayerId PlayerId { get; }
        public int Round { get; }

        public bool Equals(TrapFloorAbilityActivationRecord other) =>
            CardInstanceId == other.CardInstanceId
            && PlayerId == other.PlayerId
            && Round == other.Round;

        public override bool Equals(object obj) =>
            obj is TrapFloorAbilityActivationRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = CardInstanceId.GetHashCode();
                hashCode = (hashCode * 397) ^ PlayerId.GetHashCode();
                hashCode = (hashCode * 397) ^ Round;
                return hashCode;
            }
        }
    }

    public sealed class TrapFloorTrapResolutionRecord
    {
        internal TrapFloorTrapResolutionRecord(
            TabletopObjectId floorCardId,
            PlayerId affectedPlayerId,
            long revealedRevision,
            TrapFloorFloorContentDefinition content,
            TrapFloorTrapResolutionDisposition disposition)
        {
            if (floorCardId.IsEmpty) throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            if (affectedPlayerId.IsEmpty) throw new ArgumentException("Affected Player ID cannot be empty.", nameof(affectedPlayerId));
            if (revealedRevision < 1) throw new ArgumentOutOfRangeException(nameof(revealedRevision));
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (content.Category != TrapFloorFloorContentCategory.Trap)
                throw new ArgumentException("Only Trap content can have Trap resolution state.", nameof(content));
            if (!Enum.IsDefined(typeof(TrapFloorTrapResolutionDisposition), disposition))
                throw new ArgumentOutOfRangeException(nameof(disposition));

            FloorCardId = floorCardId;
            AffectedPlayerId = affectedPlayerId;
            RevealedRevision = revealedRevision;
            Content = content;
            Disposition = disposition;
        }

        public TabletopObjectId FloorCardId { get; }
        public PlayerId AffectedPlayerId { get; }
        public long RevealedRevision { get; }
        public TrapFloorFloorContentDefinition Content { get; }
        public TrapFloorTrapResolutionDisposition Disposition { get; private set; }
        public bool IsPending => Disposition == TrapFloorTrapResolutionDisposition.Pending;

        internal void SetDisposition(TrapFloorTrapResolutionDisposition disposition)
        {
            if (!Enum.IsDefined(typeof(TrapFloorTrapResolutionDisposition), disposition))
                throw new ArgumentOutOfRangeException(nameof(disposition));
            Disposition = disposition;
        }

        internal TrapFloorTrapResolutionRecord Copy() =>
            new TrapFloorTrapResolutionRecord(
                FloorCardId,
                AffectedPlayerId,
                RevealedRevision,
                Content,
                Disposition);
    }

    /// <summary>
    /// Match-scoped Trap/Ability assistance only. It never constrains physical tabletop interaction.
    /// </summary>
    public sealed class TrapFloorAbilityResolutionState
    {
        private readonly List<TrapFloorTrapResolutionRecord> trapRecords =
            new List<TrapFloorTrapResolutionRecord>();
        private readonly ReadOnlyCollection<TrapFloorTrapResolutionRecord> readOnlyTrapRecords;
        private readonly HashSet<TrapFloorAbilityActivationRecord> abilityActivations =
            new HashSet<TrapFloorAbilityActivationRecord>();

        public TrapFloorAbilityResolutionState(MatchId matchId)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Match ID cannot be empty.", nameof(matchId));
            MatchId = matchId;
            readOnlyTrapRecords = trapRecords.AsReadOnly();
        }

        public MatchId MatchId { get; }
        public IReadOnlyList<TrapFloorTrapResolutionRecord> TrapRecords => readOnlyTrapRecords;
        public bool HasAbilityActivatedThisTurn(
            TabletopObjectId cardInstanceId,
            PlayerId playerId,
            int round) =>
            abilityActivations.Contains(
                new TrapFloorAbilityActivationRecord(cardInstanceId, playerId, round));

        internal void RecordRevealedTrap(
            long acceptedRevision,
            PlayerId affectedPlayerId,
            TrapFloorFloorCardState floorCard)
        {
            if (floorCard == null) throw new ArgumentNullException(nameof(floorCard));
            if (floorCard.Content.Category != TrapFloorFloorContentCategory.Trap) return;
            for (int i = 0; i < trapRecords.Count; i++)
            {
                if (trapRecords[i].FloorCardId == floorCard.ObjectId) return;
            }

            trapRecords.Add(new TrapFloorTrapResolutionRecord(
                floorCard.ObjectId,
                affectedPlayerId,
                acceptedRevision,
                floorCard.Content,
                TrapFloorTrapResolutionDisposition.Pending));
        }

        public bool TryGetTrap(
            TabletopObjectId floorCardId,
            out TrapFloorTrapResolutionRecord record)
        {
            for (int i = trapRecords.Count - 1; i >= 0; i--)
            {
                if (trapRecords[i].FloorCardId == floorCardId)
                {
                    record = trapRecords[i];
                    return true;
                }
            }

            record = null;
            return false;
        }

        internal bool TryGetLatestPendingTrap(
            PlayerId playerId,
            bool requireSupportedConsequence,
            out TrapFloorTrapResolutionRecord record)
        {
            for (int i = trapRecords.Count - 1; i >= 0; i--)
            {
                TrapFloorTrapResolutionRecord candidate = trapRecords[i];
                if (candidate.AffectedPlayerId == playerId
                    && candidate.IsPending
                    && (!requireSupportedConsequence
                        || candidate.Content.HasSupportedAssistedTrapEffect))
                {
                    record = candidate;
                    return true;
                }
            }

            record = null;
            return false;
        }

        internal void RecordAbilityActivation(
            TabletopObjectId abilityCardId,
            PlayerId playerId,
            int round)
        {
            abilityActivations.Add(
                new TrapFloorAbilityActivationRecord(abilityCardId, playerId, round));
        }

        internal TrapFloorTrapResolutionRecord[] CopyTrapRecords()
        {
            TrapFloorTrapResolutionRecord[] copy = new TrapFloorTrapResolutionRecord[trapRecords.Count];
            for (int i = 0; i < trapRecords.Count; i++) copy[i] = trapRecords[i].Copy();
            return copy;
        }

        internal TrapFloorAbilityActivationRecord[] CopyAbilityActivations()
        {
            TrapFloorAbilityActivationRecord[] copy =
                new TrapFloorAbilityActivationRecord[abilityActivations.Count];
            abilityActivations.CopyTo(copy);
            return copy;
        }

        internal void Restore(
            IEnumerable<TrapFloorTrapResolutionRecord> restoredTrapRecords,
            IEnumerable<TrapFloorAbilityActivationRecord> restoredAbilityActivations)
        {
            if (restoredTrapRecords == null) throw new ArgumentNullException(nameof(restoredTrapRecords));
            if (restoredAbilityActivations == null) throw new ArgumentNullException(nameof(restoredAbilityActivations));
            trapRecords.Clear();
            foreach (TrapFloorTrapResolutionRecord record in restoredTrapRecords)
            {
                if (record == null) throw new ArgumentException("Restored Trap record cannot be null.", nameof(restoredTrapRecords));
                trapRecords.Add(record.Copy());
            }

            abilityActivations.Clear();
            foreach (TrapFloorAbilityActivationRecord activation in restoredAbilityActivations)
            {
                if (activation.CardInstanceId.IsEmpty
                    || activation.PlayerId.IsEmpty
                    || activation.Round < 1)
                {
                    throw new ArgumentException(
                        "Restored Ability activation context is invalid.",
                        nameof(restoredAbilityActivations));
                }
                if (!abilityActivations.Add(activation))
                {
                    throw new ArgumentException(
                        "Restored Ability activations must be unique.",
                        nameof(restoredAbilityActivations));
                }
            }
        }
    }

    public enum TrapFloorAbilityActivationError
    {
        None = 0,
        UnsupportedAbility = 1,
        ActorIsNotActivePlayer = 2,
        AbilityAlreadyUsed = 3,
        CardIdentityMismatch = 4,
        NoUnresolvedTrap = 5,
        NoPendingTrapConsequence = 6,
    }

    public readonly struct TrapFloorAbilityActivationResult
    {
        private TrapFloorAbilityActivationResult(
            bool succeeded,
            TrapFloorAbilityActivationError error,
            TrapFloorAbilityEffect effect,
            TrapFloorTrapResolutionRecord trap)
        {
            Succeeded = succeeded;
            Error = error;
            Effect = effect;
            Trap = trap;
        }

        public bool Succeeded { get; }
        public TrapFloorAbilityActivationError Error { get; }
        public TrapFloorAbilityEffect Effect { get; }
        public TrapFloorTrapResolutionRecord Trap { get; }

        internal static TrapFloorAbilityActivationResult Accepted(
            TrapFloorAbilityEffect effect,
            TrapFloorTrapResolutionRecord trap) =>
            new TrapFloorAbilityActivationResult(true, TrapFloorAbilityActivationError.None, effect, trap);

        internal static TrapFloorAbilityActivationResult Failure(
            TrapFloorAbilityActivationError error,
            TrapFloorAbilityEffect effect = TrapFloorAbilityEffect.None) =>
            new TrapFloorAbilityActivationResult(false, error, effect, null);
    }

    /// <summary>Trap Floor-owned interpretation of accepted generic Console insertions.</summary>
    public sealed class TrapFloorAbilityResolutionService
    {
        public const string CheckEffectMetadata = "trap-floor-ability-check";
        public const string DodgeEffectMetadata = "trap-floor-ability-dodge";
        public const string ShieldEffectMetadata = "trap-floor-ability-shield";
        public const string RushEffectMetadata = "trap-floor-ability-rush";
        public const string DisarmEffectMetadata = "trap-floor-ability-disarm";

        private readonly TrapFloorAbilityResolutionState state;
        private readonly TrapFloorTurnState turnState;
        private readonly TrapFloorActivityFeedState activityFeed;

        public TrapFloorAbilityResolutionService(
            TrapFloorAbilityResolutionState state,
            TrapFloorTurnState turnState,
            TrapFloorActivityFeedState activityFeed)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.turnState = turnState ?? throw new ArgumentNullException(nameof(turnState));
            this.activityFeed = activityFeed ?? throw new ArgumentNullException(nameof(activityFeed));
            if (state.MatchId != turnState.MatchId || state.MatchId != activityFeed.MatchId)
                throw new ArgumentException("Trap Floor Ability services must belong to one Match.");
        }

        public TrapFloorAbilityActivationResult Activate(
            MatchState matchState,
            ConsoleCardInserted insertion,
            CardDefinitionData cardDefinition)
        {
            if (matchState == null) throw new ArgumentNullException(nameof(matchState));
            if (insertion == null) throw new ArgumentNullException(nameof(insertion));
            if (cardDefinition == null) throw new ArgumentNullException(nameof(cardDefinition));

            TrapFloorAbilityEffect effect = ResolveEffect(cardDefinition.EffectMetadata);
            if (effect != TrapFloorAbilityEffect.Disarm && effect != TrapFloorAbilityEffect.Shield)
                return TrapFloorAbilityActivationResult.Failure(
                    TrapFloorAbilityActivationError.UnsupportedAbility,
                    effect);
            if (!ObjectDefinitionId.TryParse(
                    cardDefinition.StableId,
                    out ObjectDefinitionId authoredDefinitionId)
                || matchState.Id != state.MatchId
                || insertion.Context.MatchId != matchState.Id
                || insertion.AcceptedRevision != matchState.Revision
                || !matchState.Cards.TryGetValue(insertion.CardInstanceId, out CardInstanceState card)
                || !matchState.Containers.TryGetValue(
                    insertion.SlotContainerId,
                    out ContainerState slot)
                || !slot.Contains(insertion.CardInstanceId)
                || card.BaseState.DefinitionId != insertion.CardDefinitionId
                || insertion.CardDefinitionId != authoredDefinitionId)
                return TrapFloorAbilityActivationResult.Failure(
                    TrapFloorAbilityActivationError.CardIdentityMismatch,
                    effect);
            if (turnState.Phase != TrapFloorTurnPhase.PlayerTurn
                || turnState.IsCurrentFloorFailed
                || insertion.ActorPlayerId != turnState.ActivePlayerId)
                return TrapFloorAbilityActivationResult.Failure(
                    TrapFloorAbilityActivationError.ActorIsNotActivePlayer,
                    effect);
            if (state.HasAbilityActivatedThisTurn(
                    insertion.CardInstanceId,
                    turnState.ActivePlayerId,
                    turnState.CurrentRound))
                return TrapFloorAbilityActivationResult.Failure(
                    TrapFloorAbilityActivationError.AbilityAlreadyUsed,
                    effect);

            bool shield = effect == TrapFloorAbilityEffect.Shield;
            if (!state.TryGetLatestPendingTrap(
                    turnState.ActivePlayerId,
                    shield,
                    out TrapFloorTrapResolutionRecord trap))
            {
                return TrapFloorAbilityActivationResult.Failure(
                    shield
                        ? TrapFloorAbilityActivationError.NoPendingTrapConsequence
                        : TrapFloorAbilityActivationError.NoUnresolvedTrap,
                    effect);
            }

            trap.SetDisposition(
                shield
                    ? TrapFloorTrapResolutionDisposition.Shielded
                    : TrapFloorTrapResolutionDisposition.Disarmed);
            state.RecordAbilityActivation(
                insertion.CardInstanceId,
                turnState.ActivePlayerId,
                turnState.CurrentRound);
            activityFeed.RecordAbilityUsed(
                insertion.AcceptedRevision,
                insertion.ActorPlayerId,
                trap,
                shield
                    ? TrapFloorActivityKind.UsedShield
                    : TrapFloorActivityKind.UsedDisarm);
            return TrapFloorAbilityActivationResult.Accepted(effect, trap);
        }

        public TrapFloorTurnAdvanceResult ResolveSupportedTrap(
            MatchState matchState,
            CommandContext context,
            TabletopObjectId floorCardId,
            TrapFloorTurnService turnService)
        {
            if (matchState == null) throw new ArgumentNullException(nameof(matchState));
            if (turnService == null) throw new ArgumentNullException(nameof(turnService));
            if (!state.TryGetTrap(floorCardId, out TrapFloorTrapResolutionRecord trap)
                || !trap.IsPending
                || trap.AffectedPlayerId != turnState.ActivePlayerId
                || !trap.Content.HasSupportedAssistedTrapEffect)
                return TrapFloorTurnAdvanceResult.Failure(
                    TrapFloorTurnAdvanceError.ActorIsNotActivePlayer);

            trap.SetDisposition(TrapFloorTrapResolutionDisposition.Resolved);
            TrapFloorTurnAdvanceResult result = turnService.MarkPlayerEliminatedForCurrentRound(
                matchState,
                context,
                trap.AffectedPlayerId);
            if (!result.Succeeded)
                trap.SetDisposition(TrapFloorTrapResolutionDisposition.Pending);
            return result;
        }

        public static TrapFloorAbilityEffect ResolveEffect(string effectMetadata)
        {
            if (string.Equals(effectMetadata, CheckEffectMetadata, StringComparison.OrdinalIgnoreCase))
                return TrapFloorAbilityEffect.Check;
            if (string.Equals(effectMetadata, DodgeEffectMetadata, StringComparison.OrdinalIgnoreCase))
                return TrapFloorAbilityEffect.Dodge;
            if (string.Equals(effectMetadata, ShieldEffectMetadata, StringComparison.OrdinalIgnoreCase))
                return TrapFloorAbilityEffect.Shield;
            if (string.Equals(effectMetadata, RushEffectMetadata, StringComparison.OrdinalIgnoreCase))
                return TrapFloorAbilityEffect.Rush;
            if (string.Equals(effectMetadata, DisarmEffectMetadata, StringComparison.OrdinalIgnoreCase))
                return TrapFloorAbilityEffect.Disarm;
            return TrapFloorAbilityEffect.None;
        }
    }
}
