using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Match
{
    public enum AuthoritativeActionKind
    {
        Unspecified = 0,
        MoveObject = 1,
        MoveContainer = 2,
        RotateObject = 3,
        FlipCard = 4,
        TransferCard = 5,
        TransferToken = 6,
        ShuffleDeck = 7,
        DrawCards = 8,
        ReorderContainer = 9,
        MergeStacks = 10,
        SplitStack = 11,
        CreateComponent = 12,
        DuplicateComponent = 13,
        DeleteComponent = 14,
        PopulateDeck = 15,
        PhysicalObjectSettled = 16,
        TrapFloorReveal = 17,
        TrapFloorClaimKey = 18,
        TrapFloorAttemptEscape = 19,
        TrapFloorCollapse = 20,
        PurchaseActionOrAbility = 21,
    }

    public enum AuthoritativeActionRecordMode
    {
        None = 0,
        Intermediate = 1,
        Transaction = 2,
        CancelTransaction = 3,
    }

    public readonly struct AuthoritativeActionAcceptance
    {
        public AuthoritativeActionAcceptance(
            long revision,
            CommandId commandId,
            PlayerId actorPlayerId,
            AuthoritativeActionKind kind,
            AuthoritativeActionRecordMode recordMode)
        {
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            if (commandId.IsEmpty) throw new ArgumentException("Accepted action Command ID cannot be empty.", nameof(commandId));
            if (actorPlayerId.IsEmpty) throw new ArgumentException("Accepted action actor cannot be empty.", nameof(actorPlayerId));
            if (!Enum.IsDefined(typeof(AuthoritativeActionKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(AuthoritativeActionRecordMode), recordMode)) throw new ArgumentOutOfRangeException(nameof(recordMode));
            Revision = revision;
            CommandId = commandId;
            ActorPlayerId = actorPlayerId;
            Kind = kind;
            RecordMode = recordMode;
        }

        public long Revision { get; }
        public CommandId CommandId { get; }
        public PlayerId ActorPlayerId { get; }
        public AuthoritativeActionKind Kind { get; }
        public AuthoritativeActionRecordMode RecordMode { get; }
    }
}
