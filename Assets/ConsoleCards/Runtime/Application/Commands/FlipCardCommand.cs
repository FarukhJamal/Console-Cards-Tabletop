using System;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Application.Commands
{
    public sealed class FlipCardCommand : ITabletopCommand
    {
        public FlipCardCommand(
            CommandContext context,
            TabletopObjectId objectId,
            CardFace targetFace)
        {
            if (objectId.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", nameof(objectId));
            }

            if (!Enum.IsDefined(typeof(CardFace), targetFace))
            {
                throw new ArgumentOutOfRangeException(nameof(targetFace), "Target face must be a defined card face.");
            }

            Context = context;
            ObjectId = objectId;
            TargetFace = targetFace;
        }

        public CommandContext Context { get; }

        public TabletopObjectId ObjectId { get; }

        public CardFace TargetFace { get; }
    }
}
