using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Games.TrapFloor
{
    public enum TrapFloorActivityKind
    {
        SearchedFloor = 0,
        RevealedFloorContent = 1,
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

            if (floorCardId.IsEmpty)
            {
                throw new ArgumentException("Activity Floor Card ID cannot be empty.", nameof(floorCardId));
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
            Kind = kind;
            ContentDefinitionId = (content ?? throw new ArgumentNullException(nameof(content))).Id;
            ContentCategory = content.Category;
            ContentName = content.DisplayName;
        }

        public long Sequence { get; }

        public MatchId MatchId { get; }

        public long AcceptedRevision { get; }

        public PlayerId ActorPlayerId { get; }

        public TabletopObjectId FloorCardId { get; }

        public TrapFloorCoordinate Coordinate { get; }

        public TrapFloorActivityKind Kind { get; }

        public ObjectDefinitionId ContentDefinitionId { get; }

        public TrapFloorFloorContentCategory ContentCategory { get; }

        public string ContentName { get; }
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
                TrapFloorActivityKind.SearchedFloor);
            entries.Add(searchedEntry);
            revealedEntry = CreateEntry(
                acceptedRevision,
                actorPlayerId,
                floorCard,
                TrapFloorActivityKind.RevealedFloorContent);
            entries.Add(revealedEntry);
        }

        public void Clear()
        {
            entries.Clear();
        }

        private TrapFloorActivityEntry CreateEntry(
            long acceptedRevision,
            PlayerId actorPlayerId,
            TrapFloorFloorCardState floorCard,
            TrapFloorActivityKind kind)
        {
            return new TrapFloorActivityEntry(
                entries.Count + 1L,
                MatchId,
                acceptedRevision,
                actorPlayerId,
                floorCard.ObjectId,
                floorCard.Coordinate,
                kind,
                floorCard.Content);
        }
    }

    public sealed class TrapFloorRevealFloorCommand : ITabletopCommand
    {
        public TrapFloorRevealFloorCommand(CommandContext context, TabletopObjectId floorCardId)
        {
            if (floorCardId.IsEmpty)
            {
                throw new ArgumentException("Floor Card ID cannot be empty.", nameof(floorCardId));
            }

            Context = context;
            FloorCardId = floorCardId;
        }

        public CommandContext Context { get; }

        public TabletopObjectId FloorCardId { get; }
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
        FloorAlreadyRevealed,
        RevisionOverflow,
    }

    public readonly struct TrapFloorRevealFloorResult
    {
        private TrapFloorRevealFloorResult(
            CommandResult commandResult,
            TrapFloorRevealFloorError error,
            TrapFloorFloorCardState floorCard,
            TrapFloorActivityEntry searchedActivity,
            TrapFloorActivityEntry revealedActivity)
        {
            CommandResult = commandResult;
            Error = error;
            FloorCard = floorCard;
            SearchedActivity = searchedActivity;
            RevealedActivity = revealedActivity;
        }

        public CommandResult CommandResult { get; }

        public TrapFloorRevealFloorError Error { get; }

        public bool Succeeded => CommandResult.Succeeded;

        public long Revision => CommandResult.Revision;

        public TrapFloorFloorCardState FloorCard { get; }

        public TrapFloorActivityEntry SearchedActivity { get; }

        public TrapFloorActivityEntry RevealedActivity { get; }

        internal static TrapFloorRevealFloorResult Accepted(
            long revision,
            TrapFloorFloorCardState floorCard,
            TrapFloorActivityEntry searchedActivity,
            TrapFloorActivityEntry revealedActivity)
        {
            return new TrapFloorRevealFloorResult(
                CommandResult.Accepted(revision),
                TrapFloorRevealFloorError.None,
                floorCard,
                searchedActivity,
                revealedActivity);
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
                null);
        }
    }

    public sealed class TrapFloorRevealFloorUseCase
    {
        private readonly TrapFloorTemplateDefinition template;
        private readonly TrapFloorActivityFeedState activityFeed;

        public TrapFloorRevealFloorUseCase(
            TrapFloorTemplateDefinition template,
            TrapFloorActivityFeedState activityFeed)
        {
            this.template = template ?? throw new ArgumentNullException(nameof(template));
            this.activityFeed = activityFeed ?? throw new ArgumentNullException(nameof(activityFeed));
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

            if (!template.TryGetFloorCardState(matchState, command.FloorCardId, out TrapFloorFloorCardState floorCard))
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.FloorCardMissingOrInvalid);
            }

            if (floorCard.IsRevealed)
            {
                return Failure(CommandResultStatus.Rejected, TrapFloorRevealFloorError.FloorAlreadyRevealed);
            }

            if (matchState.Revision == long.MaxValue)
            {
                return Failure(CommandResultStatus.Conflict, TrapFloorRevealFloorError.RevisionOverflow);
            }

            matchState.Cards[command.FloorCardId].SetFace(CardFace.FaceUp);
            long acceptedRevision = matchState.AdvanceRevision();
            TrapFloorFloorCardState revealedFloorCard = new TrapFloorFloorCardState(
                floorCard.ObjectId,
                floorCard.Coordinate,
                floorCard.Content,
                true);
            activityFeed.RecordReveal(
                acceptedRevision,
                command.Context.RequestedByPlayerId,
                revealedFloorCard,
                out TrapFloorActivityEntry searchedActivity,
                out TrapFloorActivityEntry revealedActivity);
            return TrapFloorRevealFloorResult.Accepted(
                acceptedRevision,
                revealedFloorCard,
                searchedActivity,
                revealedActivity);
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
