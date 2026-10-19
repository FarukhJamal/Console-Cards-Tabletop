using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    internal readonly struct TabletopTransformSnapshot
    {
        public TabletopTransformSnapshot(Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            Position = position;
            Rotation = rotation;
            LocalScale = localScale;
        }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public Vector3 LocalScale { get; }
    }

    /// <summary>
    /// Owns the short-lived View-only motion used by the tabletop prototype.
    /// Authoritative layouts and poses are applied first; transitions only interpolate toward those results.
    /// </summary>
    internal sealed class TabletopPresentationTransitionController
    {
        private const float MinimumDuration = 0.0001f;
        private const float PickupScaleMultiplier = 1.035f;

        private readonly Dictionary<Transform, Motion> motions = new Dictionary<Transform, Motion>();
        private readonly Dictionary<Transform, PulseState> pulses = new Dictionary<Transform, PulseState>();
        private readonly Dictionary<Transform, Vector3> restingScales = new Dictionary<Transform, Vector3>();
        private readonly List<Transform> completedTransforms = new List<Transform>();
        // SMOOTH-1: reused when cards are ordered for a staggered move or a riffle (no allocation per call).
        private readonly List<KeyValuePair<Transform, TabletopTransformSnapshot>> orderedStarts =
            new List<KeyValuePair<Transform, TabletopTransformSnapshot>>();
        private readonly List<Transform> orderedDestinations = new List<Transform>();
        private static readonly IComparer<KeyValuePair<Transform, TabletopTransformSnapshot>> LowestStartFirst =
            new StartHeightComparer();
        private static readonly IComparer<Transform> LowestDestinationFirst = new DestinationHeightComparer();

        private static bool PhysicalOwns(Transform target)
        {
            PhysicalLooseObject body = target.GetComponent<PhysicalLooseObject>();
            return body != null && (body.OwnsLooseTransform || body.IsHeld);
        }

        public TabletopTransformSnapshot Capture(Transform target)
        {
            if (target == null)
            {
                return default;
            }

            return new TabletopTransformSnapshot(target.position, target.rotation, target.localScale);
        }

        public void BeginPickup(Transform target, float lift, float duration)
        {
            if (target == null)
            {
                return;
            }

            Complete(target);
            RememberRestingScale(target);
            TabletopTransformSnapshot start = Capture(target);
            TabletopTransformSnapshot destination = new TabletopTransformSnapshot(
                start.Position + (Vector3.up * lift),
                start.Rotation,
                restingScales[target] * PickupScaleMultiplier);
            BeginTimed(target, start, destination, duration, 0f, 0f);
        }

        public void Follow(
            Transform target,
            Vector3 position,
            Quaternion rotation,
            float lift,
            float smoothingDuration)
        {
            if (target == null)
            {
                return;
            }

            RememberRestingScale(target);
            motions[target] = Motion.Follow(
                new TabletopTransformSnapshot(
                    position + (Vector3.up * lift),
                    rotation,
                    restingScales[target] * PickupScaleMultiplier),
                smoothingDuration);
        }

        public TabletopTransformSnapshot StopAndCapture(Transform target)
        {
            if (target == null)
            {
                return default;
            }

            Stop(target, false);
            return Capture(target);
        }

        public void AnimateFromCurrentResult(
            Transform target,
            TabletopTransformSnapshot start,
            float duration,
            float arcHeight = 0f,
            float delay = 0f)
        {
            if (target == null || PhysicalOwns(target))
            {
                return;
            }

            RememberRestingScale(target);
            TabletopTransformSnapshot destination = Capture(target);
            destination = new TabletopTransformSnapshot(
                destination.Position,
                destination.Rotation,
                restingScales[target]);
            BeginTimed(target, start, destination, duration, delay, arcHeight);
        }

        public void AnimateCardsFromCurrentResults(
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts,
            float duration,
            float arcHeight = 0f)
        {
            if (starts == null)
            {
                throw new ArgumentNullException(nameof(starts));
            }

            foreach (KeyValuePair<Transform, TabletopTransformSnapshot> pair in starts)
            {
                if (pair.Key != null)
                {
                    AnimateFromCurrentResult(pair.Key, pair.Value, duration, arcHeight);
                }
            }
        }

        /// <summary>
        /// SMOOTH-1: the cards glide to their accepted places along a small arc, one after another (the top of the pile
        /// first, or the bottom first), so a split, a draw or a merge reads as cards moving rather than a jump. The
        /// whole stagger never takes longer than <paramref name="maximumStagger"/>.
        /// </summary>
        public void AnimateCardsStaggered(
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts,
            float duration,
            float arcHeight,
            float staggerPerCard,
            float maximumStagger,
            bool topFirst)
        {
            if (starts == null)
            {
                throw new ArgumentNullException(nameof(starts));
            }

            FillOrderedStarts(starts);
            int count = orderedStarts.Count;
            float step = count > 1 ? Mathf.Min(Mathf.Max(0f, staggerPerCard), Mathf.Max(0f, maximumStagger) / (count - 1)) : 0f;
            for (int i = 0; i < count; i++)
            {
                KeyValuePair<Transform, TabletopTransformSnapshot> pair = orderedStarts[topFirst ? count - 1 - i : i];
                AnimateFromCurrentResult(pair.Key, pair.Value, duration, arcHeight, i * step);
            }

            orderedStarts.Clear();
        }

        /// <summary>
        /// SMOOTH-1: a riffle shuffle. The accepted (shuffled) layout is already applied; the cards start where they
        /// were. The pile splits in two halves that slide apart along <paramref name="sideAxis"/> and tip a little, then
        /// the cards drop back one by one, bottom of the new order first, so the halves interleave; the last card lands
        /// on its place with no jump.
        /// </summary>
        public void AnimateRiffle(
            IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts,
            Vector3 sideAxis,
            float spread,
            float lift,
            float tiltDegrees,
            float splitDuration,
            float dropDuration,
            float staggerPerCard,
            float maximumStagger)
        {
            if (starts == null)
            {
                throw new ArgumentNullException(nameof(starts));
            }

            FillOrderedStarts(starts);
            int count = orderedStarts.Count;
            if (count == 0)
            {
                return;
            }

            Vector3 side = sideAxis.sqrMagnitude > 0.0001f ? sideAxis.normalized : Vector3.right;
            Vector3 tiltAxis = Vector3.Cross(side, Vector3.up);
            if (tiltAxis.sqrMagnitude < 0.0001f) tiltAxis = Vector3.forward;
            tiltAxis.Normalize();
            int half = count / 2;
            // The upper half comes down beside the lower one, so both halves sit on the table.
            float upperDrop = count > 1 ? orderedStarts[half].Value.Position.y - orderedStarts[0].Value.Position.y : 0f;

            // Drop order: by where each card ends up, lowest first.
            orderedDestinations.Clear();
            for (int i = 0; i < count; i++)
            {
                if (orderedStarts[i].Key != null) orderedDestinations.Add(orderedStarts[i].Key);
            }

            orderedDestinations.Sort(LowestDestinationFirst);
            float step = count > 1 ? Mathf.Min(Mathf.Max(0f, staggerPerCard), Mathf.Max(0f, maximumStagger) / (count - 1)) : 0f;
            for (int i = 0; i < count; i++)
            {
                Transform target = orderedStarts[i].Key;
                if (target == null || PhysicalOwns(target))
                {
                    continue;
                }

                bool upper = i >= half;
                float sign = upper ? 1f : -1f;
                TabletopTransformSnapshot start = orderedStarts[i].Value;
                Vector3 waypoint = start.Position + (side * (spread * sign)) + (Vector3.up * lift);
                if (upper) waypoint.y -= upperDrop;
                Quaternion waypointRotation = Quaternion.AngleAxis(tiltDegrees * sign, tiltAxis) * start.Rotation;
                int rank = orderedDestinations.IndexOf(target);
                RememberRestingScale(target);
                TabletopTransformSnapshot destination = Capture(target);
                destination = new TabletopTransformSnapshot(destination.Position, destination.Rotation, restingScales[target]);
                motions.Remove(target);
                Apply(target, start);
                motions[target] = Motion.Riffle(
                    start,
                    new TabletopTransformSnapshot(waypoint, waypointRotation, restingScales[target]),
                    destination,
                    Mathf.Max(splitDuration, MinimumDuration),
                    Mathf.Max(0f, rank) * step,
                    Mathf.Max(dropDuration, MinimumDuration),
                    lift * 0.5f);
            }

            orderedStarts.Clear();
            orderedDestinations.Clear();
        }

        /// <summary>
        /// SHUF-1: one card's part in a shuffle or flourish. The card's accepted place is already applied (it is the
        /// destination); the card starts at <paramref name="start"/>, waits <paramref name="leaveDelay"/>, moves to
        /// <paramref name="waypoint"/> along an arc of <paramref name="moveArc"/>, stays there (circling
        /// <paramref name="swirlCentre"/> at <paramref name="swirlDegreesPerSecond"/> while it waits, for a wash) and,
        /// <paramref name="dropAt"/> seconds after the start, goes to its place along an arc of
        /// <paramref name="dropArc"/>. Times are clamped so the card never leaves before it arrives.
        /// </summary>
        public void AnimateThrough(
            Transform target,
            TabletopTransformSnapshot start,
            Vector3 waypointPosition,
            Quaternion waypointRotation,
            float leaveDelay,
            float moveDuration,
            float moveArc,
            float dropAt,
            float dropDuration,
            float dropArc,
            Vector3 swirlCentre = default,
            float swirlDegreesPerSecond = 0f)
        {
            if (target == null || PhysicalOwns(target))
            {
                return;
            }

            RememberRestingScale(target);
            TabletopTransformSnapshot destination = Capture(target);
            destination = new TabletopTransformSnapshot(destination.Position, destination.Rotation, restingScales[target]);
            leaveDelay = Mathf.Max(0f, leaveDelay);
            moveDuration = Mathf.Max(moveDuration, MinimumDuration);
            float wait = Mathf.Max(0f, dropAt - leaveDelay - moveDuration);
            motions.Remove(target);
            Apply(target, start);
            Motion motion = Motion.Riffle(
                start,
                new TabletopTransformSnapshot(waypointPosition, waypointRotation, restingScales[target]),
                destination,
                moveDuration,
                wait,
                Mathf.Max(dropDuration, MinimumDuration),
                Mathf.Max(0f, dropArc));
            motion.LeaveDelay = leaveDelay;
            motion.MoveArc = Mathf.Max(0f, moveArc);
            motion.SwirlCentre = swirlCentre;
            motion.SwirlSpeed = swirlDegreesPerSecond;
            motions[target] = motion;
        }

        private void FillOrderedStarts(IReadOnlyDictionary<Transform, TabletopTransformSnapshot> starts)
        {
            orderedStarts.Clear();
            foreach (KeyValuePair<Transform, TabletopTransformSnapshot> pair in starts)
            {
                if (pair.Key != null) orderedStarts.Add(pair);
            }

            orderedStarts.Sort(LowestStartFirst);
        }

        public void Appear(Transform target, float duration)
        {
            if (target == null || PhysicalOwns(target))
            {
                return;
            }

            Complete(target);
            RememberRestingScale(target);
            TabletopTransformSnapshot destination = Capture(target);
            TabletopTransformSnapshot start = new TabletopTransformSnapshot(
                destination.Position + (Vector3.up * 0.04f),
                destination.Rotation,
                Vector3.Scale(restingScales[target], new Vector3(0.92f, 0.92f, 0.92f)));
            BeginTimed(target, start, destination, duration, 0f, 0f);
        }

        public void Pulse(Transform target, float compression, float duration)
        {
            if (target == null || PhysicalOwns(target))
            {
                return;
            }

            RememberRestingScale(target);
            pulses[target] = new PulseState(
                restingScales[target],
                Mathf.Clamp01(compression),
                Mathf.Max(duration, MinimumDuration));
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsFinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            }

            TickMotions(unscaledDeltaTime);
            TickPulses(unscaledDeltaTime);
        }

        /// <summary>True while a timed or follow motion owns the target's transform.</summary>
        public bool IsAnimating(Transform target)
        {
            return target != null && motions.ContainsKey(target);
        }

        public void Complete(Transform target)
        {
            if (target == null)
            {
                return;
            }

            if (motions.TryGetValue(target, out Motion motion))
            {
                if (!motion.IsFollowing)
                {
                    Apply(target, motion.Destination);
                }
                else if (restingScales.TryGetValue(target, out Vector3 restingScale))
                {
                    target.localScale = restingScale;
                }

                motions.Remove(target);
            }

            if (pulses.Remove(target) && restingScales.TryGetValue(target, out Vector3 pulseRestingScale))
            {
                target.localScale = pulseRestingScale;
            }
        }

        public void Stop(Transform target, bool restoreScale)
        {
            if (target == null)
            {
                return;
            }

            motions.Remove(target);
            pulses.Remove(target);
            if (restoreScale && restingScales.TryGetValue(target, out Vector3 restingScale))
            {
                target.localScale = restingScale;
            }
        }

        public void Forget(Transform target)
        {
            if (target == null)
            {
                return;
            }

            Stop(target, true);
            restingScales.Remove(target);
        }

        public void CompleteAll()
        {
            completedTransforms.Clear();
            completedTransforms.AddRange(motions.Keys);
            for (int i = 0; i < completedTransforms.Count; i++)
            {
                Complete(completedTransforms[i]);
            }

            completedTransforms.Clear();
            completedTransforms.AddRange(pulses.Keys);
            for (int i = 0; i < completedTransforms.Count; i++)
            {
                Complete(completedTransforms[i]);
            }

            completedTransforms.Clear();
            motions.Clear();
            pulses.Clear();
            restingScales.Clear();
        }

        private void TickMotions(float deltaTime)
        {
            completedTransforms.Clear();
            completedTransforms.AddRange(motions.Keys);
            for (int i = completedTransforms.Count - 1; i >= 0; i--)
            {
                Transform target = completedTransforms[i];
                if (target == null)
                {
                    motions.Remove(target);
                    completedTransforms.RemoveAt(i);
                    continue;
                }

                Motion motion = motions[target];
                if (motion.IsFollowing)
                {
                    float follow = 1f - Mathf.Exp(-deltaTime / Mathf.Max(motion.Duration, MinimumDuration));
                    target.position = Vector3.Lerp(target.position, motion.Destination.Position, follow);
                    target.rotation = Quaternion.Slerp(target.rotation, motion.Destination.Rotation, follow);
                    target.localScale = Vector3.Lerp(target.localScale, motion.Destination.LocalScale, follow);
                    completedTransforms.RemoveAt(i);
                    continue;
                }

                if (motion.IsRiffle)
                {
                    motion.Elapsed += deltaTime;
                    if (TickRiffle(target, motion))
                    {
                        completedTransforms.Add(target);
                    }
                    else
                    {
                        motions[target] = motion;
                        completedTransforms.RemoveAt(i);
                    }

                    continue;
                }

                motion.Elapsed += deltaTime;
                if (motion.Elapsed < motion.Delay)
                {
                    motions[target] = motion;
                    completedTransforms.RemoveAt(i);
                    continue;
                }

                float normalizedTime = Mathf.Clamp01((motion.Elapsed - motion.Delay) / motion.Duration);
                float easedTime = normalizedTime * normalizedTime * (3f - (2f * normalizedTime));
                Vector3 position = Vector3.Lerp(motion.Start.Position, motion.Destination.Position, easedTime);
                position += Vector3.up * (Mathf.Sin(normalizedTime * Mathf.PI) * motion.ArcHeight);
                target.position = position;
                target.rotation = Quaternion.Slerp(motion.Start.Rotation, motion.Destination.Rotation, easedTime);
                target.localScale = Vector3.Lerp(motion.Start.LocalScale, motion.Destination.LocalScale, easedTime);

                if (normalizedTime >= 1f)
                {
                    Apply(target, motion.Destination);
                    completedTransforms.Add(target);
                }
                else
                {
                    motions[target] = motion;
                    completedTransforms.RemoveAt(i);
                }
            }

            for (int i = 0; i < completedTransforms.Count; i++)
            {
                motions.Remove(completedTransforms[i]);
            }
        }

        // Wait to leave, go to the waypoint (all cards together in a riffle), wait there for the card's turn (circling for
        // a wash), drop onto its place. True when it has landed.
        private static bool TickRiffle(Transform target, Motion motion)
        {
            float elapsed = motion.Elapsed - motion.LeaveDelay;
            if (elapsed < 0f)
            {
                target.SetPositionAndRotation(motion.Start.Position, motion.Start.Rotation);
                return false;
            }

            if (elapsed < motion.Duration)
            {
                float m = elapsed / motion.Duration;
                float t = Smooth(m);
                target.SetPositionAndRotation(
                    Vector3.Lerp(motion.Start.Position, motion.Waypoint.Position, t)
                        + (Vector3.up * (Mathf.Sin(m * Mathf.PI) * motion.MoveArc)),
                    Quaternion.Slerp(motion.Start.Rotation, motion.Waypoint.Rotation, t));
                return false;
            }

            float dropStart = motion.Duration + motion.Delay;
            if (elapsed < dropStart)
            {
                WaypointAt(motion, elapsed - motion.Duration, out Vector3 holdPosition, out Quaternion holdRotation);
                target.SetPositionAndRotation(holdPosition, holdRotation);
                return false;
            }

            WaypointAt(motion, motion.Delay, out Vector3 fromPosition, out Quaternion fromRotation);
            float n = Mathf.Clamp01((elapsed - dropStart) / motion.DropDuration);
            float e = Smooth(n);
            Vector3 position = Vector3.Lerp(fromPosition, motion.Destination.Position, e)
                + (Vector3.up * (Mathf.Sin(n * Mathf.PI) * motion.ArcHeight));
            target.SetPositionAndRotation(position, Quaternion.Slerp(fromRotation, motion.Destination.Rotation, e));
            if (n < 1f)
            {
                return false;
            }

            Apply(target, motion.Destination);
            return true;
        }

        // The waypoint after waiting there for holdTime: turned about the swirl centre when the card swirls (a wash).
        private static void WaypointAt(Motion motion, float holdTime, out Vector3 position, out Quaternion rotation)
        {
            position = motion.Waypoint.Position;
            rotation = motion.Waypoint.Rotation;
            if (motion.SwirlSpeed == 0f || holdTime <= 0f)
            {
                return;
            }

            Quaternion turn = Quaternion.AngleAxis(motion.SwirlSpeed * holdTime, Vector3.up);
            position = motion.SwirlCentre + (turn * (position - motion.SwirlCentre));
            rotation = turn * rotation;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - (2f * t));
        }

        private void TickPulses(float deltaTime)
        {
            completedTransforms.Clear();
            completedTransforms.AddRange(pulses.Keys);
            for (int i = completedTransforms.Count - 1; i >= 0; i--)
            {
                Transform target = completedTransforms[i];
                if (target == null)
                {
                    pulses.Remove(target);
                    completedTransforms.RemoveAt(i);
                    continue;
                }

                PulseState pulse = pulses[target];
                pulse.Elapsed += deltaTime;
                float normalizedTime = Mathf.Clamp01(pulse.Elapsed / pulse.Duration);
                float compression = 1f - (Mathf.Sin(normalizedTime * Mathf.PI) * pulse.Compression);
                target.localScale = Vector3.Scale(
                    pulse.RestingScale,
                    new Vector3(compression, 1f - ((1f - compression) * 0.5f), compression));

                if (normalizedTime >= 1f)
                {
                    target.localScale = pulse.RestingScale;
                    completedTransforms.Add(target);
                }
                else
                {
                    pulses[target] = pulse;
                    completedTransforms.RemoveAt(i);
                }
            }

            for (int i = 0; i < completedTransforms.Count; i++)
            {
                pulses.Remove(completedTransforms[i]);
            }
        }

        private void BeginTimed(
            Transform target,
            TabletopTransformSnapshot start,
            TabletopTransformSnapshot destination,
            float duration,
            float delay,
            float arcHeight)
        {
            motions.Remove(target);
            Apply(target, start);
            if (duration <= 0f && delay <= 0f)
            {
                Apply(target, destination);
                return;
            }

            motions[target] = Motion.Timed(
                start,
                destination,
                Mathf.Max(duration, MinimumDuration),
                Mathf.Max(0f, delay),
                Mathf.Max(0f, arcHeight));
        }

        private void RememberRestingScale(Transform target)
        {
            if (!restingScales.ContainsKey(target))
            {
                restingScales.Add(target, target.localScale);
            }
        }

        private static void Apply(Transform target, TabletopTransformSnapshot snapshot)
        {
            target.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
            target.localScale = snapshot.LocalScale;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private struct Motion
        {
            public TabletopTransformSnapshot Start;
            public TabletopTransformSnapshot Destination;
            public float Duration;
            public float Delay;
            public float ArcHeight;
            public float Elapsed;
            public bool IsFollowing;
            public bool IsRiffle;
            public TabletopTransformSnapshot Waypoint;
            public float DropDuration;
            // SHUF-1: wait before leaving for the waypoint, arc on the way there, and circling while waiting there.
            public float LeaveDelay;
            public float MoveArc;
            public Vector3 SwirlCentre;
            public float SwirlSpeed;

            public static Motion Follow(TabletopTransformSnapshot destination, float smoothingDuration)
            {
                return new Motion
                {
                    Destination = destination,
                    Duration = Mathf.Max(smoothingDuration, MinimumDuration),
                    IsFollowing = true
                };
            }

            // Riffle: Duration is the split time, Delay the wait at the waypoint, DropDuration the fall onto the place.
            public static Motion Riffle(
                TabletopTransformSnapshot start,
                TabletopTransformSnapshot waypoint,
                TabletopTransformSnapshot destination,
                float splitDuration,
                float wait,
                float dropDuration,
                float arcHeight)
            {
                return new Motion
                {
                    Start = start,
                    Waypoint = waypoint,
                    Destination = destination,
                    Duration = splitDuration,
                    Delay = wait,
                    DropDuration = dropDuration,
                    ArcHeight = arcHeight,
                    Elapsed = 0f,
                    IsFollowing = false,
                    IsRiffle = true
                };
            }

            public static Motion Timed(
                TabletopTransformSnapshot start,
                TabletopTransformSnapshot destination,
                float duration,
                float delay,
                float arcHeight)
            {
                return new Motion
                {
                    Start = start,
                    Destination = destination,
                    Duration = duration,
                    Delay = delay,
                    ArcHeight = arcHeight,
                    Elapsed = 0f,
                    IsFollowing = false
                };
            }
        }

        private sealed class StartHeightComparer : IComparer<KeyValuePair<Transform, TabletopTransformSnapshot>>
        {
            public int Compare(
                KeyValuePair<Transform, TabletopTransformSnapshot> x,
                KeyValuePair<Transform, TabletopTransformSnapshot> y)
            {
                return x.Value.Position.y.CompareTo(y.Value.Position.y);
            }
        }

        private sealed class DestinationHeightComparer : IComparer<Transform>
        {
            public int Compare(Transform x, Transform y)
            {
                return x.position.y.CompareTo(y.position.y);
            }
        }

        private struct PulseState
        {
            public PulseState(Vector3 restingScale, float compression, float duration)
            {
                RestingScale = restingScale;
                Compression = compression;
                Duration = duration;
                Elapsed = 0f;
            }

            public Vector3 RestingScale;
            public float Compression;
            public float Duration;
            public float Elapsed;
        }
    }
}
