using System;

namespace ConsoleCards.Presentation.Interaction
{
    public readonly struct TabletopInteractionReleaseResult : IEquatable<TabletopInteractionReleaseResult>
    {
        private TabletopInteractionReleaseResult(
            TabletopInteractionRoute route,
            bool hadActiveInteraction,
            MoveInteractionReleaseResult? moveResult,
            ContainedCardDragReleaseResult? containedCardResult)
        {
            Route = route;
            HadActiveInteraction = hadActiveInteraction;
            MoveResult = moveResult;
            ContainedCardResult = containedCardResult;
        }

        public TabletopInteractionRoute Route { get; }

        public bool HadActiveInteraction { get; }

        public MoveInteractionReleaseResult? MoveResult { get; }

        public ContainedCardDragReleaseResult? ContainedCardResult { get; }

        public static TabletopInteractionReleaseResult NoActiveInteraction()
        {
            return new TabletopInteractionReleaseResult(
                TabletopInteractionRoute.None,
                false,
                null,
                null);
        }

        public static TabletopInteractionReleaseResult FromMove(MoveInteractionReleaseResult result)
        {
            return new TabletopInteractionReleaseResult(
                TabletopInteractionRoute.TabletopMove,
                true,
                result,
                null);
        }

        public static TabletopInteractionReleaseResult FromContainedCard(ContainedCardDragReleaseResult result)
        {
            return new TabletopInteractionReleaseResult(
                TabletopInteractionRoute.ContainedCardDrag,
                true,
                null,
                result);
        }

        public bool Equals(TabletopInteractionReleaseResult other)
        {
            return Route == other.Route
                && HadActiveInteraction == other.HadActiveInteraction
                && Nullable.Equals(MoveResult, other.MoveResult)
                && Nullable.Equals(ContainedCardResult, other.ContainedCardResult);
        }

        public override bool Equals(object obj)
        {
            return obj is TabletopInteractionReleaseResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Route, HadActiveInteraction, MoveResult, ContainedCardResult);
        }

        public override string ToString()
        {
            return $"Route: {Route}, HadActiveInteraction: {HadActiveInteraction}, MoveResult: {MoveResult?.ToString() ?? "None"}, ContainedCardResult: {ContainedCardResult?.ToString() ?? "None"}";
        }

        public static bool operator ==(TabletopInteractionReleaseResult left, TabletopInteractionReleaseResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TabletopInteractionReleaseResult left, TabletopInteractionReleaseResult right)
        {
            return !left.Equals(right);
        }
    }
}
