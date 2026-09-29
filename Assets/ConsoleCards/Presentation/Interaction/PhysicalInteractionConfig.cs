using System;
using ConsoleCards.Core.Domain;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    [Serializable]
    public sealed class PhysicalObjectInteractionProfile
    {
        [SerializeField, Min(0f), Tooltip("Seconds used to smooth held-object following. Zero follows immediately.")]
        private float heldFollowSmoothingSeconds = 0.025f;
        [SerializeField, Min(0f), Tooltip("Clearance above the supporting collider, in addition to the object's own half-height.")]
        private float heldClearance = 0.08f;
        [SerializeField, Min(0.001f)] private float mass = 0.1f;
        [SerializeField, Min(0f)] private float releaseVelocityMultiplier = 1f;
        [SerializeField, Min(0f)] private float releaseAngularVelocityMultiplier = 1f;
        [SerializeField, Min(0f)] private float maximumReleaseVelocity = 4f;
        [SerializeField, Min(0f)] private float maximumReleaseAngularVelocity = 6f;
        [SerializeField, Min(0f)] private float linearDamping = 0.5f;
        [SerializeField, Min(0f)] private float angularDamping = 0.5f;
        [SerializeField, Min(0f), Tooltip("Releases below both gentle thresholds discard incidental angular momentum.")]
        private float gentlePlacementSpeed = 0.45f;
        [SerializeField, Min(0f)] private float gentlePlacementAngularSpeed = 1f;
        [SerializeField] private bool stabilizeGentlePlacement = true;

        public float HeldFollowSmoothingSeconds => Mathf.Max(0f, heldFollowSmoothingSeconds);
        public float HeldClearance => Mathf.Max(0f, heldClearance);
        public float Mass => Mathf.Max(0.001f, mass);
        public float ReleaseVelocityMultiplier => Mathf.Max(0f, releaseVelocityMultiplier);
        public float ReleaseAngularVelocityMultiplier => Mathf.Max(0f, releaseAngularVelocityMultiplier);
        public float MaximumReleaseVelocity => Mathf.Max(0f, maximumReleaseVelocity);
        public float MaximumReleaseAngularVelocity => Mathf.Max(0f, maximumReleaseAngularVelocity);
        public float LinearDamping => Mathf.Max(0f, linearDamping);
        public float AngularDamping => Mathf.Max(0f, angularDamping);
        public float GentlePlacementSpeed => Mathf.Max(0f, gentlePlacementSpeed);
        public float GentlePlacementAngularSpeed => Mathf.Max(0f, gentlePlacementAngularSpeed);
        public bool StabilizeGentlePlacement => stabilizeGentlePlacement;

        internal static PhysicalObjectInteractionProfile CardDefault() =>
            new PhysicalObjectInteractionProfile
            {
                heldFollowSmoothingSeconds = 0.025f,
                heldClearance = 0.08f,
                mass = 0.03f,
                releaseVelocityMultiplier = 0.85f,
                releaseAngularVelocityMultiplier = 0.4f,
                maximumReleaseVelocity = 3.25f,
                maximumReleaseAngularVelocity = 2.5f,
                linearDamping = 1.5f,
                angularDamping = 3.5f,
                gentlePlacementSpeed = 0.45f,
                gentlePlacementAngularSpeed = 1f,
                stabilizeGentlePlacement = true
            };

        internal static PhysicalObjectInteractionProfile DieDefault() =>
            new PhysicalObjectInteractionProfile
            {
                heldFollowSmoothingSeconds = 0.02f,
                heldClearance = 0.1f,
                mass = 0.12f,
                releaseVelocityMultiplier = 1.4f,
                releaseAngularVelocityMultiplier = 1.5f,
                maximumReleaseVelocity = 4f,
                maximumReleaseAngularVelocity = 6f,
                linearDamping = 0.12f,
                angularDamping = 0.12f,
                gentlePlacementSpeed = 0f,
                gentlePlacementAngularSpeed = 0f,
                stabilizeGentlePlacement = false
            };

        internal static PhysicalObjectInteractionProfile PawnDefault() =>
            new PhysicalObjectInteractionProfile
            {
                heldFollowSmoothingSeconds = 0.025f,
                heldClearance = 0.08f,
                mass = 0.1f,
                releaseVelocityMultiplier = 0.8f,
                releaseAngularVelocityMultiplier = 0.25f,
                maximumReleaseVelocity = 2.75f,
                maximumReleaseAngularVelocity = 1.5f,
                linearDamping = 2f,
                angularDamping = 5f,
                gentlePlacementSpeed = 0.5f,
                gentlePlacementAngularSpeed = 1.25f,
                stabilizeGentlePlacement = true
            };

        internal static PhysicalObjectInteractionProfile TokenDefault() =>
            new PhysicalObjectInteractionProfile
            {
                heldFollowSmoothingSeconds = 0.02f,
                heldClearance = 0.06f,
                mass = 0.05f,
                releaseVelocityMultiplier = 0.9f,
                releaseAngularVelocityMultiplier = 0.35f,
                maximumReleaseVelocity = 3f,
                maximumReleaseAngularVelocity = 2f,
                linearDamping = 1.8f,
                angularDamping = 4f,
                gentlePlacementSpeed = 0.5f,
                gentlePlacementAngularSpeed = 1.25f,
                stabilizeGentlePlacement = true
            };

        public void ApplyRelease(ref Vector3 linear, ref Vector3 angular)
        {
            linear = Vector3.ClampMagnitude(
                linear * ReleaseVelocityMultiplier,
                MaximumReleaseVelocity);
            angular = Vector3.ClampMagnitude(
                angular * ReleaseAngularVelocityMultiplier,
                MaximumReleaseAngularVelocity);

            if (StabilizeGentlePlacement
                && linear.magnitude <= GentlePlacementSpeed
                && angular.magnitude <= GentlePlacementAngularSpeed)
            {
                angular = Vector3.zero;
            }
        }
    }

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
        [Header("Broad Object Profiles")]
        [SerializeField] private PhysicalObjectInteractionProfile card = PhysicalObjectInteractionProfile.CardDefault();
        [SerializeField] private PhysicalObjectInteractionProfile die = PhysicalObjectInteractionProfile.DieDefault();
        [SerializeField] private PhysicalObjectInteractionProfile pawn = PhysicalObjectInteractionProfile.PawnDefault();
        [SerializeField] private PhysicalObjectInteractionProfile token = PhysicalObjectInteractionProfile.TokenDefault();

        public float ReleaseSmoothingSeconds => Mathf.Max(0f, releaseSmoothingSeconds);
        public float ReleaseVelocityMultiplier => Mathf.Max(0f, releaseVelocityMultiplier);
        public float MaximumReleaseVelocity => Mathf.Max(0f, maximumReleaseVelocity);
        public float ReleaseAngularVelocityMultiplier => Mathf.Max(0f, releaseAngularVelocityMultiplier);
        public float MaximumReleaseAngularVelocity => Mathf.Max(0f, maximumReleaseAngularVelocity);
        public float ReleaseSampleTimeoutSeconds => Mathf.Max(0f, releaseSampleTimeoutSeconds);

        public PhysicalObjectInteractionProfile ResolveProfile(TabletopObjectKind kind)
        {
            switch (kind)
            {
                case TabletopObjectKind.Card: return card ?? (card = PhysicalObjectInteractionProfile.CardDefault());
                case TabletopObjectKind.Die: return die ?? (die = PhysicalObjectInteractionProfile.DieDefault());
                case TabletopObjectKind.Pawn: return pawn ?? (pawn = PhysicalObjectInteractionProfile.PawnDefault());
                case TabletopObjectKind.Token: return token ?? (token = PhysicalObjectInteractionProfile.TokenDefault());
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported physical object kind.");
            }
        }
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
