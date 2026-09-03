using System;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>Shared manual-release tuning, not Die Roll impulses or Runtime State.</summary>
    [Serializable]
    public sealed class PhysicalInteractionConfig
    {
        [SerializeField, Min(0f), Tooltip("Velocity smoothing time in seconds. Zero disables smoothing.")]
        private float releaseSmoothingSeconds = 0.08f;
        [SerializeField, Min(0f), Tooltip("Scales sampled pointer velocity for manual releases.")]
        private float releaseVelocityMultiplier = 0.25f;
        [SerializeField, Min(0f), Tooltip("Maximum manual-release speed in world units per second.")]
        private float maximumReleaseVelocity = 4f;
        [SerializeField, Min(0f), Tooltip("Scales sampled rotation velocity for manual releases.")]
        private float releaseAngularVelocityMultiplier = 0.25f;
        [SerializeField, Min(0f), Tooltip("Maximum manual-release angular speed in radians per second. Does not limit Roll impulses.")]
        private float maximumReleaseAngularVelocity = 6f;
        [SerializeField, Min(0f), Tooltip("Discard release momentum when the last pointer sample is older than this many seconds.")]
        private float releaseSampleTimeoutSeconds = 0.12f;

        public float ReleaseSmoothingSeconds => Mathf.Max(0f, releaseSmoothingSeconds);
        public float ReleaseVelocityMultiplier => Mathf.Max(0f, releaseVelocityMultiplier);
        public float MaximumReleaseVelocity => Mathf.Max(0f, maximumReleaseVelocity);
        public float ReleaseAngularVelocityMultiplier => Mathf.Max(0f, releaseAngularVelocityMultiplier);
        public float MaximumReleaseAngularVelocity => Mathf.Max(0f, maximumReleaseAngularVelocity);
        public float ReleaseSampleTimeoutSeconds => Mathf.Max(0f, releaseSampleTimeoutSeconds);
    }

    /// <summary>Per-grab sampling shared by every loose component, using unscaled time.</summary>
    internal sealed class PhysicalReleaseMotion
    {
        private readonly PhysicalInteractionConfig config;
        private bool hasSample;
        private float lastSampleTime;
        private Vector3 lastPointerPosition;
        private Quaternion lastRotation;
        private Vector3 velocity;
        private Vector3 angularVelocity;

        public PhysicalReleaseMotion(PhysicalInteractionConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Reset()
        {
            hasSample = false;
            velocity = angularVelocity = Vector3.zero;
        }

        public void Sample(Vector3 pointerPosition, Quaternion rotation, float time)
        {
            // The first follow may recenter/lift the object. It establishes an origin, not a throw.
            if (!hasSample)
            {
                hasSample = true;
                StoreSample(pointerPosition, rotation, time);
                return;
            }

            float dt = time - lastSampleTime;
            if (dt <= 0.0001f) return;
            Vector3 sampledVelocity = (pointerPosition - lastPointerPosition) / dt;
            Quaternion delta = rotation * Quaternion.Inverse(lastRotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 sampledAngularVelocity = axis.sqrMagnitude > 0f && !float.IsNaN(axis.x)
                ? axis * (angle * Mathf.Deg2Rad / dt) : Vector3.zero;

            float blend = config.ReleaseSmoothingSeconds > 0f
                ? 1f - Mathf.Exp(-dt / config.ReleaseSmoothingSeconds) : 1f;
            // Clamp before filtering too, so a single discontinuity cannot leave a long impulse tail.
            velocity = Vector3.Lerp(velocity, Vector3.ClampMagnitude(
                sampledVelocity * config.ReleaseVelocityMultiplier, config.MaximumReleaseVelocity), blend);
            angularVelocity = Vector3.Lerp(angularVelocity, Vector3.ClampMagnitude(
                sampledAngularVelocity * config.ReleaseAngularVelocityMultiplier,
                config.MaximumReleaseAngularVelocity), blend);
            StoreSample(pointerPosition, rotation, time);
        }

        public void GetRelease(float time, out Vector3 linear, out Vector3 angular)
        {
            bool recent = hasSample && time - lastSampleTime < config.ReleaseSampleTimeoutSeconds;
            linear = recent ? Vector3.ClampMagnitude(velocity, config.MaximumReleaseVelocity) : Vector3.zero;
            angular = recent ? Vector3.ClampMagnitude(angularVelocity, config.MaximumReleaseAngularVelocity) : Vector3.zero;
        }

        private void StoreSample(Vector3 pointerPosition, Quaternion rotation, float time)
        {
            lastPointerPosition = pointerPosition;
            lastRotation = rotation;
            lastSampleTime = time;
        }
    }
}
