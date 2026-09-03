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

            public static Motion Follow(TabletopTransformSnapshot destination, float smoothingDuration)
            {
                return new Motion
                {
                    Destination = destination,
                    Duration = Mathf.Max(smoothingDuration, MinimumDuration),
                    IsFollowing = true
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
