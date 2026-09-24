using System;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Events
{
    public interface IDomainEvent
    {
        DomainEventContext Context { get; }
    }

    public interface IConsoleCardInteraction : IDomainEvent
    {
        PlayerId ActorPlayerId { get; }
        TabletopObjectId CardInstanceId { get; }
        ObjectDefinitionId CardDefinitionId { get; }
        PlayerId ConsoleOwnerPlayerId { get; }
        ContainerId SlotContainerId { get; }
        long AcceptedRevision { get; }
    }

    public sealed class ConsoleCardInserted : IConsoleCardInteraction
    {
        public ConsoleCardInserted(
            DomainEventContext context,
            PlayerId actorPlayerId,
            TabletopObjectId cardInstanceId,
            ObjectDefinitionId cardDefinitionId,
            PlayerId consoleOwnerPlayerId,
            ContainerId slotContainerId)
        {
            ConsoleCardInteractionValidation.Validate(
                context,
                actorPlayerId,
                cardInstanceId,
                cardDefinitionId,
                slotContainerId);

            Context = context;
            ActorPlayerId = actorPlayerId;
            CardInstanceId = cardInstanceId;
            CardDefinitionId = cardDefinitionId;
            ConsoleOwnerPlayerId = consoleOwnerPlayerId;
            SlotContainerId = slotContainerId;
        }

        public DomainEventContext Context { get; }
        public PlayerId ActorPlayerId { get; }
        public TabletopObjectId CardInstanceId { get; }
        public ObjectDefinitionId CardDefinitionId { get; }
        public PlayerId ConsoleOwnerPlayerId { get; }
        public ContainerId SlotContainerId { get; }
        public long AcceptedRevision => Context.Revision;
    }

    public sealed class ConsoleCardRemoved : IConsoleCardInteraction
    {
        public ConsoleCardRemoved(
            DomainEventContext context,
            PlayerId actorPlayerId,
            TabletopObjectId cardInstanceId,
            ObjectDefinitionId cardDefinitionId,
            PlayerId consoleOwnerPlayerId,
            ContainerId slotContainerId)
        {
            ConsoleCardInteractionValidation.Validate(
                context,
                actorPlayerId,
                cardInstanceId,
                cardDefinitionId,
                slotContainerId);

            Context = context;
            ActorPlayerId = actorPlayerId;
            CardInstanceId = cardInstanceId;
            CardDefinitionId = cardDefinitionId;
            ConsoleOwnerPlayerId = consoleOwnerPlayerId;
            SlotContainerId = slotContainerId;
        }

        public DomainEventContext Context { get; }
        public PlayerId ActorPlayerId { get; }
        public TabletopObjectId CardInstanceId { get; }
        public ObjectDefinitionId CardDefinitionId { get; }
        public PlayerId ConsoleOwnerPlayerId { get; }
        public ContainerId SlotContainerId { get; }
        public long AcceptedRevision => Context.Revision;
    }

    internal static class ConsoleCardInteractionValidation
    {
        public static void Validate(
            DomainEventContext context,
            PlayerId actorPlayerId,
            TabletopObjectId cardInstanceId,
            ObjectDefinitionId cardDefinitionId,
            ContainerId slotContainerId)
        {
            if (context.MatchId.IsEmpty)
            {
                throw new ArgumentException("Console Card interaction Match ID cannot be empty.", nameof(context));
            }

            if (actorPlayerId.IsEmpty)
            {
                throw new ArgumentException("Console Card interaction actor cannot be empty.", nameof(actorPlayerId));
            }

            if (cardInstanceId.IsEmpty)
            {
                throw new ArgumentException("Console Card interaction Card instance ID cannot be empty.", nameof(cardInstanceId));
            }

            if (cardDefinitionId.IsEmpty)
            {
                throw new ArgumentException("Console Card interaction Card Definition ID cannot be empty.", nameof(cardDefinitionId));
            }

            if (slotContainerId.IsEmpty)
            {
                throw new ArgumentException("Console Card interaction Slot Container ID cannot be empty.", nameof(slotContainerId));
            }
        }
    }
}
