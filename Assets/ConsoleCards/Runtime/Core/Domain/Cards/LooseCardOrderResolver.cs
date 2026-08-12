using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.Core.Domain.Cards
{
    /// <summary>
    /// Resolves the authoritative pose that places a loose Card above the other loose Cards
    /// in the same logical layer. Container membership order remains a separate concern.
    /// </summary>
    public static class LooseCardOrderResolver
    {
        public static bool TryResolveTopPose(
            MatchState matchState,
            TabletopObjectId excludedCardId,
            TabletopPose requestedPose,
            out TabletopPose resolvedPose)
        {
            if (matchState == null)
            {
                throw new ArgumentNullException(nameof(matchState));
            }

            bool foundPeer = false;
            int highestLocalOrder = int.MinValue;
            foreach (CardInstanceState card in matchState.Cards.Values)
            {
                TabletopObjectState candidate = card.BaseState;
                if (candidate.Id == excludedCardId
                    || !candidate.ContainerId.IsEmpty
                    || candidate.Pose.Layer != requestedPose.Layer)
                {
                    continue;
                }

                if (!foundPeer || candidate.Pose.LocalOrder > highestLocalOrder)
                {
                    foundPeer = true;
                    highestLocalOrder = candidate.Pose.LocalOrder;
                }
            }

            if (!foundPeer)
            {
                resolvedPose = requestedPose;
                return true;
            }

            if (highestLocalOrder == int.MaxValue)
            {
                resolvedPose = requestedPose;
                return false;
            }

            resolvedPose = new TabletopPose(
                requestedPose.Position,
                requestedPose.RotationDegrees,
                requestedPose.Layer,
                highestLocalOrder + 1);
            return true;
        }
    }
}
