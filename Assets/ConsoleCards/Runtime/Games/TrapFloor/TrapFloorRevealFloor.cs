using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.GameTemplates.ControllerInputs;
using ConsoleCards.GameTemplates.Definitions;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorActivityKind
    {
        SearchedFloor = 0,
        RevealedFloorContent = 1,
        ClaimedKey = 2,
        WonGame = 3,
        TriggeredFloorfall = 4,
        RolledFloorfall = 5,
        CollapsedFloor = 6,
        SkippedTurn = 7,
        PlayerEliminated = 8,
        AllPlayersEliminated = 9,
        PlayersReactivated = 10,
        PlayerEscaped = 11,
        UsedDisarm = 12,
        UsedShield = 13,
        UsedDodge = 14,
        UsedRush = 15,
        UsedCheck = 16,
        BlindApplied = 17,
        BlindDirectionRolled = 18,
        SlowApplied = 19,
        StickyApplied = 20,
        CarefullySearchedFloor = 21,
        SafelyRevealedTrap = 22,
    }

    public enum TrapFloorSearchKind
    {
        Normal = 0,
        Careful = 1,
    }

    /// <summary>
    /// Replication-ready shared activity data. Display strings are derived by Presentation;
    /// identity and causality remain actor, Match, revision, definition, and Object ID based.
    /// </summary>
    public sealed class TrapFloorActivityEntry
    {
        internal TrapFloorActivityEntry(
            long sequence,
            MatchId matchId,
            long acceptedRevision,
            PlayerId actorPlayerId,
            TabletopObjectId floorCardId,
            TrapFloorCoordinate coordinate,
            TrapFloorActivityKind kind,
            TrapFloorFloorContentDefinition content)
            : this(
                sequence,
                matchId,
                acceptedRevision,
                actorPlayerId,
                floorCardId,
                coordinate,
                true,
                kind,
                content,
                TabletopObjectId.Empty,
                TabletopObjectId.Empty,
                null,
                null)
        {
        }

        internal TrapFloorActivityEntry(
            long sequence,
            MatchId matchId,
            long acceptedRevision,
            PlayerId actorPlayerId,
            TabletopObjectId floorCardId,
            TrapFloorCoordinate coordinate,
            bool hasCoordinate,
            TrapFloorActivityKind kind,
            TrapFloorFloorContentDefinition content,
            TabletopObjectId xAxisDieId,
            TabletopObjectId yAxisDieId,
            int? xAxisResult,
            int? yAxisResult,
            TrapFloorSearchKind? searchKind = null,
            IEnumerable<TabletopObjectId> paymentCardIds = null,
            IEnumerable<ControllerInput> paymentInputs = null,
            bool trapSuppressed = false)
        {
            if (sequence < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (matchId.IsEmpty)
            {
                throw new ArgumentException("Activity Match ID cannot be empty.", nameof(matchId));
            }

            if (acceptedRevision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(acceptedRevision));
            }

            if (actorPlayerId.IsEmpty)
            {
                throw new ArgumentException("Activity actor Player ID cannot be empty.", nameof(actorPlayerId));
            }

            bool floorfallActivity = kind == TrapFloorActivityKind.TriggeredFloorfall
                || kind == TrapFloorActivityKind.RolledFloorfall
                || kind == TrapFloorActivityKind.CollapsedFloor;
            bool turnActivity = kind == TrapFloorActivityKind.SkippedTurn
                || kind == TrapFloorActivityKind.PlayerEliminated
                || kind == TrapFloorActivityKind.AllPlayersEliminated
                || kind == TrapFloorActivityKind.PlayersReactivated
                || kind == TrapFloorActivityKind.UsedRush
                || kind == TrapFloorActivityKind.BlindDirectionRolled;
            if (floorCardId.IsEmpty
                && kind != TrapFloorActivityKind.TriggeredFloorfall
                && !turnActivity)
            {
                throw new ArgumentException("Activity Floor Card ID cannot be empty.", nameof(floorCardId));
            }

            if (floorfallActivity && (xAxisDieId.IsEmpty || yAxisDieId.IsEmpty || xAxisDieId == yAxisDieId))
            {
                throw new ArgumentException("Floorfall activity requires two distinct official Die IDs.");
            }

            if ((kind == TrapFloorActivityKind.RolledFloorfall
                    || kind == TrapFloorActivityKind.CollapsedFloor)
                && (!hasCoordinate || !xAxisResult.HasValue || !yAxisResult.HasValue))
            {
                throw new ArgumentException("Resolved Floorfall activity requires Dice results and a Floor coordinate.");
            }

            if (!floorfallActivity && !turnActivity && content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (!Enum.IsDefined(typeof(TrapFloorActivityKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Sequence = sequence;
            MatchId = matchId;
            AcceptedRevision = acceptedRevision;
            ActorPlayerId = actorPlayerId;
            FloorCardId = floorCardId;
            Coordinate = coordinate;
            HasCoordinate = hasCoordinate;
            Kind = kind;
            ContentDefinitionId = content?.Id ?? ObjectDefinitionId.Empty;
            ContentCategory = content?.Category ?? default;
            ContentName = content?.DisplayName ?? string.Empty;
            XAxisDieId = xAxisDieId;
            YAxisDieId = yAxisDieId;
            XAxisResult = xAxisResult;
            YAxisResult = yAxisResult;
            SearchKind = searchKind;
            PaymentCardIds = new ReadOnlyCollection<TabletopObjectId>(
                new List<TabletopObjectId>(paymentCardIds ?? Array.Empty<TabletopObjectId>()));
            PaymentInputs = new ReadOnlyCollection<ControllerInput>(
                new List<ControllerInput>(paymentInputs ?? Array.Empty<ControllerInput>()));
            TrapSuppressed = trapSuppressed;
        }

        public long Sequence { get; }

        public MatchId MatchId { get; }

        public long AcceptedRevision { get; }

        public PlayerId ActorPlayerId { get; }

        public TabletopObjectId FloorCardId { get; }

        public TrapFloorCoordinate Coordinate { get; }

        public bool HasCoordinate { get; }

        public TrapFloorActivityKind Kind { get; }

        public ObjectDefinitionId ContentDefinitionId { get; }

        public TrapFloorFloorContentCategory ContentCategory { get; }

        public string ContentName { get; }

        public TabletopObjectId XAxisDieId { get; }

        public TabletopObjectId YAxisDieId { get; }

        public int? XAxisResult { get; }

        public int? YAxisResult { get; }

        public TrapFloorSearchKind? SearchKind { get; }

        public IReadOnlyList<TabletopObjectId> PaymentCardIds { get; }

        public IReadOnlyList<ControllerInput> PaymentInputs { get; }

        public bool TrapSuppressed { get; }

        public int? BlindDirectionResult =>
            Kind == TrapFloorActivityKind.BlindDirectionRolled ? XAxisResult : null;
    }

    /// <summary>
    /// Match-scoped shared activity model. It is intentionally engine-independent so a future
    /// authority/transport layer can replicate the same accepted entries to every client.
    /// </summary>
    public sealed class TrapFloorActivityFeedState
    {
        private readonly List<TrapFloorActivityEntry> entries = new List<TrapFloorActivityEntry>();
        private readonly ReadOnlyCollection<TrapFloorActivityEntry> readOnlyEntries;

        public TrapFloorActivityFeedState(MatchId matchId)
        {
            if (matchId.IsEmpty)
            {
                throw new ArgumentException("Activity feed Match ID cannot be empty.", nameof(matchId));
            }

            MatchId = matchId;
            readOnlyEntries = entries.AsReadOnly();
        }

        public MatchId MatchId { get; }

        public IReadOnlyList<TrapFloorActivityEntry> Entries => readOnlyEntries;

        internal void RecordReveal(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorFloorCardState floorCard,
            TrapFloorSearchKind searchKind,
            IReadOnlyList<TabletopObjectId> paymentCardIds,
            IReadOnlyList<ControllerInput> paymentInputs,
            bool trapSuppressed,
            out TrapFloorActivityEntry searchedEntry,
            out TrapFloorActivityEntry revealedEntry)
        {
            if (floorCard == null)
            {
                throw new ArgumentNullException(nameof(floorCard));
            }

            searchedEntry = CreateEntry(
                acceptedRevision,
                actorPlayerId,
                floorCard,
                searchKind == TrapFloorSearchKind.Careful
                    ? TrapFloorActivityKind.CarefullySearchedFloor
                    : TrapFloorActivityKind.SearchedFloor,
                searchKind,
                paymentCardIds,
                paymentInputs,
                trapSuppressed);
            entries.Add(searchedEntry);
            revealedEntry = CreateEntry(
                acceptedRevision,
                actorPlayerId,
                floorCard,
                trapSuppressed && floorCard.Content.Category == TrapFloorFloorContentCategory.Trap
                    ? TrapFloorActivityKind.SafelyRevealedTrap
                    : TrapFloorActivityKind.RevealedFloorContent,
                searchKind,
                paymentCardIds,
                paymentInputs,
                trapSuppressed);
            entries.Add(revealedEntry);
        }

        internal TrapFloorActivityEntry RecordKeyClaim(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorFloorCardState floorCard)
        {
            TrapFloorActivityEntry entry = CreateEntry(
                acceptedRevision,
                actorPlayerId,
                floorCard,
                TrapFloorActivityKind.ClaimedKey);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordVictory(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorFloorCardState floorCard)
        {
            TrapFloorActivityEntry entry = CreateEntry(
                acceptedRevision,
                actorPlayerId,
                floorCard,
                TrapFloorActivityKind.WonGame);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordPlayerEscaped(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorFloorCardState floorCard)
        {
            TrapFloorActivityEntry entry = CreateEntry(
                acceptedRevision,
                actorPlayerId,
                floorCard,
                TrapFloorActivityKind.PlayerEscaped);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordFloorfallTriggered(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TabletopObjectId xAxisDieId,
            TabletopObjectId yAxisDieId)
        {
            TrapFloorActivityEntry entry = CreateFloorfallEntry(
                acceptedRevision,
                actorPlayerId,
                TrapFloorActivityKind.TriggeredFloorfall,
                null,
                xAxisDieId,
                yAxisDieId,
                null,
                null);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordFloorfallRoll(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorFloorCardState floorCard,
            TabletopObjectId xAxisDieId,
            TabletopObjectId yAxisDieId,
            int xAxisResult,
            int yAxisResult)
        {
            TrapFloorActivityEntry entry = CreateFloorfallEntry(
                acceptedRevision,
                actorPlayerId,
                TrapFloorActivityKind.RolledFloorfall,
                floorCard,
                xAxisDieId,
                yAxisDieId,
                xAxisResult,
                yAxisResult);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordFloorCollapsed(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorFloorCardState floorCard,
            TabletopObjectId xAxisDieId,
            TabletopObjectId yAxisDieId,
            int xAxisResult,
            int yAxisResult)
        {
            TrapFloorActivityEntry entry = CreateFloorfallEntry(
                acceptedRevision,
                actorPlayerId,
                TrapFloorActivityKind.CollapsedFloor,
                floorCard,
                xAxisDieId,
                yAxisDieId,
                xAxisResult,
                yAxisResult);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordSkippedTurn(
            long acceptedRevision,
            PlayerId actorPlayerId)
        {
            return RecordPlayerActivity(
                acceptedRevision,
                actorPlayerId,
                TrapFloorActivityKind.SkippedTurn);
        }

        internal TrapFloorActivityEntry RecordPlayerEliminated(
            long acceptedRevision,
            PlayerId actorPlayerId)
        {
            return RecordPlayerActivity(
                acceptedRevision,
                actorPlayerId,
                TrapFloorActivityKind.PlayerEliminated);
        }

        internal TrapFloorActivityEntry RecordPlayerEliminated(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorTrapResolutionRecord trap)
        {
            if (trap == null) throw new ArgumentNullException(nameof(trap));
            TrapFloorActivityEntry entry = new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                trap.FloorCardId,
                default,
                false,
                TrapFloorActivityKind.PlayerEliminated,
                trap.Content,
                TabletopObjectId.Empty,
                TabletopObjectId.Empty,
                null,
                null);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordAllPlayersEliminated(
            long acceptedRevision,
            PlayerId actorPlayerId)
        {
            return RecordPlayerActivity(
                acceptedRevision,
                actorPlayerId,
                TrapFloorActivityKind.AllPlayersEliminated);
        }

        internal TrapFloorActivityEntry RecordPlayersReactivated(
            long acceptedRevision,
            PlayerId actorPlayerId)
        {
            return RecordPlayerActivity(
                acceptedRevision,
                actorPlayerId,
                TrapFloorActivityKind.PlayersReactivated);
        }

        internal TrapFloorActivityEntry RecordAbilityUsed(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorTrapResolutionRecord trap,
            TrapFloorActivityKind kind)
        {
            if (trap == null) throw new ArgumentNullException(nameof(trap));
            if (kind != TrapFloorActivityKind.UsedDisarm
                && kind != TrapFloorActivityKind.UsedShield
                && kind != TrapFloorActivityKind.UsedDodge
                && kind != TrapFloorActivityKind.UsedCheck)
                throw new ArgumentOutOfRangeException(nameof(kind));
            TrapFloorActivityEntry entry = new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                trap.FloorCardId,
                default,
                false,
                kind,
                trap.Content,
                TabletopObjectId.Empty,
                TabletopObjectId.Empty,
                null,
                null);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordRushActivated(
            long acceptedRevision,
            PlayerId actorPlayerId)
        {
            return RecordPlayerActivity(
                acceptedRevision,
                actorPlayerId,
                TrapFloorActivityKind.UsedRush);
        }

        internal TrapFloorActivityEntry RecordBlindApplied(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorTrapResolutionRecord trap)
        {
            if (trap == null) throw new ArgumentNullException(nameof(trap));
            TrapFloorActivityEntry entry = new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                trap.FloorCardId,
                default,
                false,
                TrapFloorActivityKind.BlindApplied,
                trap.Content,
                TabletopObjectId.Empty,
                TabletopObjectId.Empty,
                null,
                null);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordBlindDirectionRolled(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TabletopObjectId dieId,
            int d4Result)
        {
            if (dieId.IsEmpty) throw new ArgumentException("Blind direction Die ID cannot be empty.", nameof(dieId));
            if (d4Result < 1 || d4Result > 4) throw new ArgumentOutOfRangeException(nameof(d4Result));
            TrapFloorActivityEntry entry = new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                TabletopObjectId.Empty,
                default,
                false,
                TrapFloorActivityKind.BlindDirectionRolled,
                null,
                dieId,
                TabletopObjectId.Empty,
                d4Result,
                null);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordSlowApplied(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorTrapResolutionRecord trap)
        {
            if (trap == null) throw new ArgumentNullException(nameof(trap));
            TrapFloorActivityEntry entry = new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                trap.FloorCardId,
                default,
                false,
                TrapFloorActivityKind.SlowApplied,
                trap.Content,
                TabletopObjectId.Empty,
                TabletopObjectId.Empty,
                null,
                null);
            entries.Add(entry);
            return entry;
        }

        internal TrapFloorActivityEntry RecordStickyApplied(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorTrapResolutionRecord trap)
        {
            if (trap == null) throw new ArgumentNullException(nameof(trap));
            TrapFloorActivityEntry entry = new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                trap.FloorCardId,
                default,
                false,
                TrapFloorActivityKind.StickyApplied,
                trap.Content,
                TabletopObjectId.Empty,
                TabletopObjectId.Empty,
                null,
                null);
            entries.Add(entry);
            return entry;
        }

        private TrapFloorActivityEntry RecordPlayerActivity(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorActivityKind kind)
        {
            TrapFloorActivityEntry entry = new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                TabletopObjectId.Empty,
                default,
                false,
                kind,
                null,
                TabletopObjectId.Empty,
                TabletopObjectId.Empty,
                null,
                null);
            entries.Add(entry);
            return entry;
        }

        internal void RemoveLast(TrapFloorActivityEntry expectedEntry)
        {
            if (expectedEntry == null
                || entries.Count == 0
                || !ReferenceEquals(entries[entries.Count - 1], expectedEntry))
            {
                throw new InvalidOperationException("Only the latest Trap Floor activity may be rolled back.");
            }

            entries.RemoveAt(entries.Count - 1);
        }

        public void Clear()
        {
            entries.Clear();
        }

        internal void RestoreEntries(IEnumerable<TrapFloorActivityEntry> restoredEntries)
        {
            if (restoredEntries == null) throw new ArgumentNullException(nameof(restoredEntries));
            entries.Clear();
            foreach (TrapFloorActivityEntry entry in restoredEntries)
            {
                if (entry == null || entry.MatchId != MatchId)
                    throw new ArgumentException("Activity snapshot does not belong to this Match.", nameof(restoredEntries));
                entries.Add(entry);
            }
        }

        private TrapFloorActivityEntry CreateEntry(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorFloorCardState floorCard,
            TrapFloorActivityKind kind,
            TrapFloorSearchKind? searchKind = null,
            IReadOnlyList<TabletopObjectId> paymentCardIds = null,
            IReadOnlyList<ControllerInput> paymentInputs = null,
            bool trapSuppressed = false)
        {
            return new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                floorCard.ObjectId,
                floorCard.Coordinate,
                true,
                kind,
                floorCard.Content,
                TabletopObjectId.Empty,
                TabletopObjectId.Empty,
                null,
                null,
                searchKind,
                paymentCardIds,
                paymentInputs,
                trapSuppressed);
        }

        private TrapFloorActivityEntry CreateFloorfallEntry(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorActivityKind kind,
            TrapFloorFloorCardState floorCard,
            TabletopObjectId xAxisDieId,
            TabletopObjectId yAxisDieId,
            int? xAxisResult,
            int? yAxisResult)
        {
            return new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                floorCard?.ObjectId ?? TabletopObjectId.Empty,
                floorCard?.Coordinate ?? default,
                floorCard != null,
                kind,
                floorCard?.Content,
                xAxisDieId,
                yAxisDieId,
                xAxisResult,
                yAxisResult);
        }
    }

    /// <summary>Trap Floor-specific, authored assisted Search costs and provisional behavior.</summary>
    public sealed class TrapFloorSearchConfiguration
    {
        private const string NormalCostKey = "trap-floor-search-normal-any";
        private const string CarefulCostKey = "trap-floor-search-careful";
        private const string CarefulTrapSuppressionKey = "trap-floor-careful-trap-suppression";
        private readonly ReadOnlyCollection<ControllerInput> normalEligibleInputs;
        private readonly ReadOnlyCollection<InputRequirementData> carefulRequirements;

        private TrapFloorSearchConfiguration(
            IEnumerable<ControllerInput> normalEligibleInputs,
            IEnumerable<InputRequirementData> carefulRequirements,
            bool carefulSearchSuppressesTrap)
        {
            this.normalEligibleInputs = new ReadOnlyCollection<ControllerInput>(
                new List<ControllerInput>(normalEligibleInputs));
            this.carefulRequirements = new ReadOnlyCollection<InputRequirementData>(
                new List<InputRequirementData>(carefulRequirements));
            CarefulSearchSuppressesTrap = carefulSearchSuppressesTrap;
        }

        public IReadOnlyList<ControllerInput> NormalEligibleInputs => normalEligibleInputs;
        public IReadOnlyList<InputRequirementData> CarefulRequirements => carefulRequirements;
        public bool CarefulSearchSuppressesTrap { get; }

        public bool IsNormalSearchInput(ControllerInput input)
        {
            for (int i = 0; i < normalEligibleInputs.Count; i++)
            {
                if (normalEligibleInputs[i] == input) return true;
            }
            return false;
        }

        public int RequiredCarefulCount(ControllerInput input)
        {
            int count = 0;
            for (int i = 0; i < carefulRequirements.Count; i++)
            {
                if (carefulRequirements[i].Input == input)
                    count = checked(count + carefulRequirements[i].Count);
            }
            return count;
        }

        public static bool TryCreate(GameDefinitionData gameDefinition, out TrapFloorSearchConfiguration configuration)
        {
            configuration = null;
            if (gameDefinition == null || string.IsNullOrWhiteSpace(gameDefinition.AssistanceConfiguration))
                return false;

            string normalValue = null;
            string carefulValue = null;
            bool? suppressesTrap = null;
            string[] entries = gameDefinition.AssistanceConfiguration.Split(';');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                int separator = entry.IndexOf('=');
                if (separator <= 0 || separator >= entry.Length - 1) continue;
                string key = entry.Substring(0, separator).Trim();
                string value = entry.Substring(separator + 1).Trim();
                if (string.Equals(key, NormalCostKey, StringComparison.OrdinalIgnoreCase))
                    normalValue = value;
                else if (string.Equals(key, CarefulCostKey, StringComparison.OrdinalIgnoreCase))
                    carefulValue = value;
                else if (string.Equals(key, CarefulTrapSuppressionKey, StringComparison.OrdinalIgnoreCase)
                    && bool.TryParse(value, out bool parsed))
                    suppressesTrap = parsed;
            }

            if (!TryParseDistinctInputs(normalValue, ',', out List<ControllerInput> normalInputs)
                || !TryParseCost(carefulValue, out List<InputRequirementData> carefulCost)
                || !suppressesTrap.HasValue)
                return false;

            configuration = new TrapFloorSearchConfiguration(
                normalInputs,
                carefulCost,
                suppressesTrap.Value);
            return true;
        }

        private static bool TryParseDistinctInputs(
            string value,
            char separator,
            out List<ControllerInput> inputs)
        {
            inputs = new List<ControllerInput>();
            if (string.IsNullOrWhiteSpace(value)) return false;
            string[] parts = value.Split(separator);
            HashSet<ControllerInput> seen = new HashSet<ControllerInput>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (!Enum.TryParse(parts[i].Trim(), true, out ControllerInput input)
                    || !Enum.IsDefined(typeof(ControllerInput), input)
                    || !seen.Add(input))
                    return false;
                inputs.Add(input);
            }
            return inputs.Count > 0;
        }

        private static bool TryParseCost(string value, out List<InputRequirementData> requirements)
        {
            requirements = new List<InputRequirementData>();
            if (string.IsNullOrWhiteSpace(value)) return false;
            string[] parts = value.Split('+');
            Dictionary<ControllerInput, int> counts = new Dictionary<ControllerInput, int>();
            List<ControllerInput> order = new List<ControllerInput>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (!Enum.TryParse(parts[i].Trim(), true, out ControllerInput input)
                    || !Enum.IsDefined(typeof(ControllerInput), input))
                    return false;
                if (!counts.ContainsKey(input)) order.Add(input);
                counts.TryGetValue(input, out int count);
                counts[input] = checked(count + 1);
            }
            for (int i = 0; i < order.Count; i++)
                requirements.Add(new InputRequirementData(order[i], counts[order[i]]));
            return requirements.Count > 0;
        }
    }

    public sealed class TrapFloorRevealFloorCommand : ITabletopCommand
    {
        public TrapFloorRevealFloorCommand(CommandContext context, TabletopObjectId floorCardId)
            : this(context, floorCardId, TrapFloorSearchKind.Normal, Array.Empty<TabletopObjectId>())
        {
        }

        public TrapFloorRevealFloorCommand(
            CommandContext context,
            TabletopObjectId floorCardId,
            TrapFloorSearchKind searchKind,
            IEnumerable<TabletopObjectId> paymentCardIds)
        {
            if (floorCardId.IsEmpty)
            {
                throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            }

            Context = context;
            FloorCardId = floorCardId;
            if (!Enum.IsDefined(typeof(TrapFloorSearchKind), searchKind))
                throw new ArgumentOutOfRangeException(nameof(searchKind));
            SearchKind = searchKind;
            PaymentCardIds = new ReadOnlyCollection<TabletopObjectId>(
                new List<TabletopObjectId>(paymentCardIds ?? throw new ArgumentNullException(nameof(paymentCardIds))));
        }

        public CommandContext Context { get; }

        public TabletopObjectId FloorCardId { get; }
        public TrapFloorSearchKind SearchKind { get; }
        public IReadOnlyList<TabletopObjectId> PaymentCardIds { get; }
    }

    public enum TrapFloorRevealFloorError
    {
        None,
        MatchRequired,
        CommandRequired,
        MatchIdMismatch,
        MatchRevisionConflict,
        MatchTemplateMismatch,
        ActivityFeedMismatch,
        ActorNotParticipating,
        FloorCardMissingOrInvalid,
        FloorCollapsed,
        FloorAlreadyRevealed,
        RevisionOverflow,
        SearchConfigurationMissing,
        ActorIsNotActivePlayer,
        HandMissing,
        PaymentCardCountInvalid,
        PaymentCardNotInHand,
        PaymentCardInvalid,
        PaymentCardLocked,
        PaymentDoesNotSatisfyCost,
        MutationFailed,
    }

    public readonly struct TrapFloorRevealFloorResult
    {
        private TrapFloorRevealFloorResult(
            CommandResult commandResult,
            TrapFloorRevealFloorError error,
            TrapFloorFloorCardState floorCard,
            TrapFloorActivityEntry searchedActivity,
            TrapFloorActivityEntry revealedActivity,
            TrapFloorSearchKind searchKind,
            IReadOnlyList<TabletopObjectId> consumedCardIds,
            bool trapSuppressed)
        {
            CommandResult = commandResult;
            Error = error;
            FloorCard = floorCard;
            SearchedActivity = searchedActivity;
            RevealedActivity = revealedActivity;
            SearchKind = searchKind;
            ConsumedCardIds = consumedCardIds;
            TrapSuppressed = trapSuppressed;
        }

        public CommandResult CommandResult { get; }

        public TrapFloorRevealFloorError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public long Revision => CommandResult.Revision;

        public TrapFloorFloorCardState FloorCard { get; }

        public TrapFloorActivityEntry SearchedActivity { get; }

        public TrapFloorActivityEntry RevealedActivity { get; }
        public TrapFloorSearchKind SearchKind { get; }
        public IReadOnlyList<TabletopObjectId> ConsumedCardIds { get; }
        public bool TrapSuppressed { get; }

        internal static TrapFloorRevealFloorResult Accepted(
            long revision,
            TrapFloorFloorCardState floorCard,
            TrapFloorActivityEntry searchedActivity,
            TrapFloorActivityEntry revealedActivity,
            TrapFloorSearchKind searchKind,
            IReadOnlyList<TabletopObjectId> consumedCardIds,
            bool trapSuppressed)
        {
            return new TrapFloorRevealFloorResult(
                CommandResult.Accepted(revision),
                TrapFloorRevealFloorError.None,
                floorCard,
                searchedActivity,
                revealedActivity,
                searchKind,
                new ReadOnlyCollection<TabletopObjectId>(new List<TabletopObjectId>(consumedCardIds)),
                trapSuppressed);
        }

        internal static TrapFloorRevealFloorResult Failure(
            CommandResultStatus status,
            TrapFloorRevealFloorError error)
        {
            if (status == CommandResultStatus.Accepted)
            {
                throw new ArgumentException("Reveal failure must use a non-Accepted status.", nameof(status));
            }

            if (error == TrapFloorRevealFloorError.None)
            {
                throw new ArgumentException("Reveal failure must include an error.", nameof(error));
            }

            return new TrapFloorRevealFloorResult(
                CommandResult.Failure(status),
                error,
                null,
                null,
                null,
                TrapFloorSearchKind.Normal,
                Array.Empty<TabletopObjectId>(),
                false);
        }
    }

    public sealed class TrapFloorRevealFloorUseCase
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly TrapFloorActivityFeedState activityFeed;
        private readonly TrapFloorCollapseState collapseState;
        private readonly TrapFloorAbilityResolutionState abilityResolutionState;
        private readonly TrapFloorTurnState turnState;

        public TrapFloorRevealFloorUseCase(
            TrapFloorTemplateDefinition template,
            TrapFloorActivityFeedState activityFeed)
            : this(template, activityFeed, null, null, null)
        {
        }

        public TrapFloorRevealFloorUseCase(
            TrapFloorTemplateDefinition template,
            TrapFloorActivityFeedState activityFeed,
            TrapFloorCollapseState collapseState)
            : this(template, activityFeed, collapseState, null, null)
        {
        }

        public TrapFloorRevealFloorUseCase(
            TrapFloorTemplateDefinition template,
            TrapFloorActivityFeedState activityFeed,
            TrapFloorCollapseState collapseState,
            TrapFloorAbilityResolutionState abilityResolutionState)
            : this(template, activityFeed, collapseState, abilityResolutionState, null)
        {
        }

        public TrapFloorRevealFloorUseCase(
            TrapFloorTemplateDefinition template,
            TrapFloorActivityFeedState activityFeed,
            TrapFloorCollapseState collapseState,
            TrapFloorAbilityResolutionState abilityResolutionState,
            TrapFloorTurnState turnState)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.activityFeed = activityFeed ?? throw new ArgumentNullException(nameof(activityFeed));
            this.collapseState = collapseState;
            this.abilityResolutionState = abilityResolutionState;
            this.turnState = turnState;
        }

        public TrapFloorRevealFloorResult Execute(
            MatchState matchState,
            TrapFloorRevealFloorCommand command)
        {
            if (matchState == null)
            {
                return Failure(CommandResultStatus.Invalid, TrapFloorRevealFloorError.MatchRequired);
            }

            if (command == null)
            {
                return Failure(CommandResultStatus.Invalid, TrapFloorRevealFloorError.CommandRequired);
            }

            if (command.Context.MatchId != matchState.Id)
            {
                return Failure(CommandResultStatus.Invalid, TrapFloorRevealFloorError.MatchIdMismatch);
            }

            if (command.Context.ExpectedRevision.HasValue
                && command.Context.ExpectedRevision.Value != matchState.Revision)
            {
                return Failure(CommandResultStatus.Conflict, TrapFloorRevealFloorError.MatchRevisionConflict);
            }

            if (matchState.GameTemplateId != template.Template.Id)
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.MatchTemplateMismatch);
            }

            if (activityFeed.MatchId != matchState.Id)
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.ActivityFeedMismatch);
            }

            if (!MatchContainsPlayer(matchState, command.Context.RequestedByPlayerId))
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.ActorNotParticipating);
            }

            if (turnState != null
                && (turnState.MatchId != matchState.Id
                    || turnState.IsCurrentFloorFailed
                    || turnState.Phase != TrapFloorTurnPhase.PlayerTurn
                    || turnState.ActivePlayerId != command.Context.RequestedByPlayerId))
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.ActorIsNotActivePlayer);
            }

            if (!template.TryGetFloorCardState(matchState, command.FloorCardId, out TrapFloorFloorCardState floorCard))
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.FloorCardMissingOrInvalid);
            }

            if (collapseState != null)
            {
                if (collapseState.MatchId != matchState.Id)
                {
                    return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.MatchTemplateMismatch);
                }

                if (collapseState.IsCollapsed(command.FloorCardId))
                {
                    return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.FloorCollapsed);
                }
            }

            if (floorCard.IsRevealed)
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.FloorAlreadyRevealed);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return Failure(CommandResultStatus.Conflict, TrapFloorRevealFloorError.RevisionOverflow);
            }


            if (!TrapFloorSearchConfiguration.TryCreate(
                    template.GameDefinition,
                    out TrapFloorSearchConfiguration searchConfiguration))
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.SearchConfigurationMissing);
            }

            if (!TryGetPlayerHand(
                    matchState,
                    command.Context.RequestedByPlayerId,
                    out ContainerState hand))
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.HandMissing);
            }

            if (!TryValidatePayment(
                    matchState,
                    hand,
                    command,
                    searchConfiguration,
                    out List<CardInstanceState> paymentCards,
                    out List<ControllerInput> paymentInputs,
                    out TrapFloorRevealFloorError paymentError))
            {
                return Failure(CommandResultStatus.Rejected, paymentError);
            }

            long acceptedRevision = checked(matchState.Revision + 1L);
            TrapFloorFloorCardState revealedFloorCard = new TrapFloorFloorCardState(
                floorCard.ObjectId,
                floorCard.Coordinate,
                floorCard.Content,
                true);
            bool trapSuppressed = command.SearchKind == TrapFloorSearchKind.Careful
                && searchConfiguration.CarefulSearchSuppressesTrap
                && floorCard.Content.Category == TrapFloorFloorContentCategory.Trap;
            List<TabletopObjectId> originalHandOrder = new List<TabletopObjectId>(hand.ObjectIds);
            ContainerTransferService transfer = new ContainerTransferService();
            TrapFloorActivityEntry searchedActivity = null;
            TrapFloorActivityEntry revealedActivity = null;
            try
            {
                for (int i = 0; i < paymentCards.Count; i++)
                {
                    CardInstanceState paymentCard = paymentCards[i];
                    ContainerTransferResult removal = transfer.RemoveFromContainer(paymentCard.BaseState, hand);
                    if (!removal.Succeeded)
                        throw new InvalidOperationException($"Search payment removal failed: {removal.Error}.");
                    matchState.RemoveObject(paymentCard.BaseState.Id);
                }

                matchState.Cards[command.FloorCardId].SetFace(CardFace.FaceUp);
                activityFeed.RecordReveal(
                    acceptedRevision,
                    command.Context.RequestedByPlayerId,
                    revealedFloorCard,
                    command.SearchKind,
                    command.PaymentCardIds,
                    paymentInputs,
                    trapSuppressed,
                    out searchedActivity,
                    out revealedActivity);
            }
            catch
            {
                if (revealedActivity != null) activityFeed.RemoveLast(revealedActivity);
                if (searchedActivity != null) activityFeed.RemoveLast(searchedActivity);
                matchState.Cards[command.FloorCardId].SetFace(CardFace.FaceDown);
                RestorePayment(matchState, hand, paymentCards, originalHandOrder, transfer);
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.MutationFailed);
            }

            if (!trapSuppressed && abilityResolutionState != null)
            {
                abilityResolutionState.RecordRevealedTrap(
                    acceptedRevision,
                    command.Context.RequestedByPlayerId,
                    revealedFloorCard);
            }

            matchState.AdvanceRevision(
                command.Context.Id,
                command.Context.RequestedByPlayerId,
                AuthoritativeActionKind.TrapFloorReveal);

            return TrapFloorRevealFloorResult.Accepted(
                acceptedRevision,
                revealedFloorCard,
                searchedActivity,
                revealedActivity,
                command.SearchKind,
                command.PaymentCardIds,
                trapSuppressed);
        }

        private bool TryGetPlayerHand(
            MatchState matchState,
            PlayerId playerId,
            out ContainerState hand)
        {
            for (int i = 0; i < template.Players.Count; i++)
            {
                TrapFloorPlayerSetupDefinition player = template.Players[i];
                if (matchState.Seats.TryGetValue(player.SeatId, out SeatState seat)
                    && seat.OccupantPlayerId == playerId
                    && matchState.Containers.TryGetValue(player.HandContainerId, out hand)
                    && hand.Kind == ContainerKind.Hand
                    && hand.OwnerSeatId == player.SeatId)
                    return true;
            }
            hand = null;
            return false;
        }

        private bool TryValidatePayment(
            MatchState matchState,
            ContainerState hand,
            TrapFloorRevealFloorCommand command,
            TrapFloorSearchConfiguration configuration,
            out List<CardInstanceState> cards,
            out List<ControllerInput> inputs,
            out TrapFloorRevealFloorError error)
        {
            cards = new List<CardInstanceState>(command.PaymentCardIds.Count);
            inputs = new List<ControllerInput>(command.PaymentCardIds.Count);
            error = TrapFloorRevealFloorError.None;
            int requiredCount = command.SearchKind == TrapFloorSearchKind.Normal
                ? 1
                : TotalRequiredCount(configuration.CarefulRequirements);
            if (command.PaymentCardIds.Count != requiredCount)
            {
                error = TrapFloorRevealFloorError.PaymentCardCountInvalid;
                return false;
            }
            if (!ControllerInputCardCatalog.TryCreate(
                    template.GameDefinition,
                    out ControllerInputCardCatalog catalog))
            {
                error = TrapFloorRevealFloorError.PaymentCardInvalid;
                return false;
            }

            HashSet<TabletopObjectId> seen = new HashSet<TabletopObjectId>();
            Dictionary<ControllerInput, int> paidCounts = new Dictionary<ControllerInput, int>();
            for (int i = 0; i < command.PaymentCardIds.Count; i++)
            {
                TabletopObjectId cardId = command.PaymentCardIds[i];
                if (cardId.IsEmpty || !seen.Add(cardId)
                    || !hand.Contains(cardId)
                    || !matchState.Cards.TryGetValue(cardId, out CardInstanceState card)
                    || card.BaseState.ContainerId != hand.Id)
                {
                    error = TrapFloorRevealFloorError.PaymentCardNotInHand;
                    return false;
                }
                if (card.BaseState.IsUserLocked)
                {
                    error = TrapFloorRevealFloorError.PaymentCardLocked;
                    return false;
                }
                if (!catalog.TryGetInput(card.BaseState.DefinitionId, out ControllerInput input))
                {
                    error = TrapFloorRevealFloorError.PaymentCardInvalid;
                    return false;
                }
                cards.Add(card);
                inputs.Add(input);
                paidCounts.TryGetValue(input, out int paidCount);
                paidCounts[input] = checked(paidCount + 1);
            }

            if (command.SearchKind == TrapFloorSearchKind.Normal)
            {
                if (!configuration.IsNormalSearchInput(inputs[0]))
                {
                    error = TrapFloorRevealFloorError.PaymentDoesNotSatisfyCost;
                    return false;
                }
                return true;
            }

            for (int i = 0; i < configuration.CarefulRequirements.Count; i++)
            {
                InputRequirementData requirement = configuration.CarefulRequirements[i];
                paidCounts.TryGetValue(requirement.Input, out int paidCount);
                if (paidCount != requirement.Count)
                {
                    error = TrapFloorRevealFloorError.PaymentDoesNotSatisfyCost;
                    return false;
                }
            }
            return paidCounts.Count == configuration.CarefulRequirements.Count;
        }

        private static int TotalRequiredCount(IReadOnlyList<InputRequirementData> requirements)
        {
            int total = 0;
            for (int i = 0; i < requirements.Count; i++) total = checked(total + requirements[i].Count);
            return total;
        }

        private static void RestorePayment(
            MatchState matchState,
            ContainerState hand,
            IReadOnlyList<CardInstanceState> paymentCards,
            IReadOnlyList<TabletopObjectId> originalHandOrder,
            ContainerTransferService transfer)
        {
            for (int i = 0; i < paymentCards.Count; i++)
            {
                CardInstanceState card = paymentCards[i];
                if (!matchState.ContainsObject(card.BaseState.Id)) matchState.AddUncontainedCard(card);
            }
            for (int index = 0; index < originalHandOrder.Count; index++)
            {
                TabletopObjectId objectId = originalHandOrder[index];
                if (!hand.Contains(objectId) && matchState.Cards.TryGetValue(objectId, out CardInstanceState card))
                {
                    ContainerTransferResult restored = transfer.PlaceIntoContainer(card.BaseState, hand, index);
                    if (!restored.Succeeded)
                        throw new InvalidOperationException($"Search payment rollback failed: {restored.Error}.");
                }
            }
        }

        private static bool MatchContainsPlayer(MatchState matchState, PlayerId playerId)
        {
            foreach (var seat in matchState.Seats.Values)
            {
                if (seat.OccupantPlayerId == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static TrapFloorRevealFloorResult Failure(
            CommandResultStatus status,
            TrapFloorRevealFloorError error)
        {
            return TrapFloorRevealFloorResult.Failure(status, error);
        }
    }
}
