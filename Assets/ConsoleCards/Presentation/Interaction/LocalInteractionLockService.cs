using System;
using System.Collections.Generic;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Presentation.Interaction
{
    public sealed class LocalInteractionLockService
    {
        private readonly Dictionary<TabletopObjectId, InteractionOwnerId> ownersByObjectId =
            new Dictionary<TabletopObjectId, InteractionOwnerId>();

        public int Count => ownersByObjectId.Count;

        public bool IsLocked(TabletopObjectId objectId)
        {
            ValidateObjectId(objectId, nameof(objectId));
            return ownersByObjectId.ContainsKey(objectId);
        }

        public bool IsOwnedBy(TabletopObjectId objectId, InteractionOwnerId ownerId)
        {
            ValidateObjectId(objectId, nameof(objectId));
            ValidateOwnerId(ownerId, nameof(ownerId));

            return ownersByObjectId.TryGetValue(objectId, out InteractionOwnerId currentOwnerId)
                && currentOwnerId == ownerId;
        }

        public bool TryGetOwner(TabletopObjectId objectId, out InteractionOwnerId ownerId)
        {
            ValidateObjectId(objectId, nameof(objectId));

            if (ownersByObjectId.TryGetValue(objectId, out ownerId))
            {
                return true;
            }

            ownerId = InteractionOwnerId.Empty;
            return false;
        }

        public InteractionLockAcquireResult Acquire(
            TabletopObjectId objectId,
            InteractionOwnerId requesterId)
        {
            ValidateObjectId(objectId, nameof(objectId));
            ValidateOwnerId(requesterId, nameof(requesterId));

            if (!ownersByObjectId.TryGetValue(objectId, out InteractionOwnerId currentOwnerId))
            {
                ownersByObjectId.Add(objectId, requesterId);
                return InteractionLockAcquireResult.Acquired(requesterId);
            }

            if (currentOwnerId == requesterId)
            {
                return InteractionLockAcquireResult.AlreadyOwned(requesterId);
            }

            return InteractionLockAcquireResult.Conflict(currentOwnerId);
        }

        public bool Release(TabletopObjectId objectId, InteractionOwnerId requesterId)
        {
            ValidateObjectId(objectId, nameof(objectId));
            ValidateOwnerId(requesterId, nameof(requesterId));

            if (!ownersByObjectId.TryGetValue(objectId, out InteractionOwnerId currentOwnerId)
                || currentOwnerId != requesterId)
            {
                return false;
            }

            ownersByObjectId.Remove(objectId);
            return true;
        }

        public bool ReleaseObject(TabletopObjectId objectId)
        {
            ValidateObjectId(objectId, nameof(objectId));
            return ownersByObjectId.Remove(objectId);
        }

        public int ReleaseAllForOwner(InteractionOwnerId ownerId)
        {
            ValidateOwnerId(ownerId, nameof(ownerId));

            List<TabletopObjectId> objectIdsToRelease = new List<TabletopObjectId>();
            foreach (KeyValuePair<TabletopObjectId, InteractionOwnerId> entry in ownersByObjectId)
            {
                if (entry.Value == ownerId)
                {
                    objectIdsToRelease.Add(entry.Key);
                }
            }

            for (int i = 0; i < objectIdsToRelease.Count; i++)
            {
                ownersByObjectId.Remove(objectIdsToRelease[i]);
            }

            return objectIdsToRelease.Count;
        }

        public void Clear()
        {
            ownersByObjectId.Clear();
        }

        private static void ValidateObjectId(TabletopObjectId objectId, string parameterName)
        {
            if (objectId.IsEmpty)
            {
                throw new ArgumentException("Tabletop object ID cannot be empty.", parameterName);
            }
        }

        private static void ValidateOwnerId(InteractionOwnerId ownerId, string parameterName)
        {
            if (ownerId.IsEmpty)
            {
                throw new ArgumentException("Interaction owner ID cannot be empty.", parameterName);
            }
        }
    }
}
