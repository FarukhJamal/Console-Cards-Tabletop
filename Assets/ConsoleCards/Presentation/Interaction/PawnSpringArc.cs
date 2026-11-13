using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// P8A polish (owner, 2026-11-13: "the Spring trap's forced move should be animated as a spring"): the shape of a
    /// pawn sprung from its tile. Animation only: the pawn's place and the rules result are already decided by the
    /// accepted move; this only says, for a moment of the animation, how far along the path the pawn is drawn and how
    /// high above it. One high arc from the Spring's tile to the target, then two small bounces on the target that die
    /// away, and it settles exactly at rest (no lift) when the time is up. Pure, so it is unit tested.
    /// </summary>
    public static class PawnSpringArc
    {
        /// <summary>How long the whole spring takes, in seconds.</summary>
        public const float Seconds = 0.95f;

        /// <summary>The arc's peak above the path, in metres (the plain hop's is 0.35).</summary>
        public const float Height = 0.9f;

        /// <summary>The share of the time spent in the air on the way over; the rest is the two bounces.</summary>
        public const float FlightShare = 0.58f;

        private const float FirstBounceEnd = 0.82f;
        private const float FirstBounceHeight = 0.2f; // of Height
        private const float SecondBounceHeight = 0.06f; // of Height

        /// <summary>
        /// <paramref name="t"/>: 0 at the start, 1 at the end (clamped). <paramref name="along"/>: 0 at the Spring's
        /// tile, 1 at the target. <paramref name="lift"/>: metres above the straight path.
        /// </summary>
        public static void Evaluate(float t, float height, out float along, out float lift)
        {
            t = Mathf.Clamp01(t);
            if (t < FlightShare)
            {
                float u = t / FlightShare;
                along = u * u * (3f - (2f * u));
                lift = height * 4f * u * (1f - u);
                return;
            }

            along = 1f;
            if (t < FirstBounceEnd)
            {
                float v = (t - FlightShare) / (FirstBounceEnd - FlightShare);
                lift = height * FirstBounceHeight * 4f * v * (1f - v);
                return;
            }

            float w = (t - FirstBounceEnd) / (1f - FirstBounceEnd);
            lift = t >= 1f ? 0f : height * SecondBounceHeight * 4f * w * (1f - w);
        }
    }
}
