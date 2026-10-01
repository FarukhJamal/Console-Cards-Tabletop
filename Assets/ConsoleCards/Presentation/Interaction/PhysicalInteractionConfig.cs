using System;
using ConsoleCards.Core.Domain;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    [Serializable]
    public sealed class PhysicalObjectInteractionProfile
    {
        [SerializeField, Min(0f), Tooltip("Optional tiny positional smoothing for direct held following. Zero follows immediately.")]
        private float heldFollowSmoothingSeconds;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of otherwise-safe held translation retained while meeting another tabletop object.")]
        private float collisionCarryScale = 0.25f;
        [SerializeField, Min(0f), Tooltip("Small separation kept between a carried collider and a solid tabletop object in its path.")]
        private float heldCollisionSkin = 0.005f;
        [SerializeField, Min(0f), Tooltip("Seconds used to lift from the supporting surface to full carry clearance.")]
        private float pickupLiftDuration = 0.1f;
        [SerializeField, Min(0f), Tooltip("Clearance kept between the supporting collider and the held object's collider bounds.")]
        private float heldClearance = 0.08f;
        [SerializeField, Min(0f), Tooltip("Seconds used to blend carry height across surfaces at different elevations.")]
        private float supportHeightResponseSeconds = 0.06f;
        [SerializeField, Tooltip("Gradually returns a held object to its authored carry orientation.")]
        private bool normalizeOrientationOnPickup = true;
        [SerializeField, Tooltip("Authored carry orientation relative to the object's accepted tabletop yaw.")]
        private Vector3 carryLocalEulerAngles = Vector3.zero;
        [SerializeField, Min(0f), Tooltip("Maximum carry-orientation recovery speed in degrees per second.")]
        private float orientationRecoverySpeed = 540f;
        [SerializeField, Min(0f), Tooltip("How long orientation recovery yields after deliberate rotation input.")]
        private float orientationRecoveryInputPauseSeconds = 0.2f;
        [SerializeField, Min(0.001f)] private float mass = 0.1f;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of measured tabletop-plane drag velocity retained on release.")]
        private float releaseLinearVelocityRetention = 0.85f;
        [SerializeField, Min(0f), Tooltip("Maximum tabletop-plane drag speed transferred to the Rigidbody on release.")]
        private float maximumReleaseLinearVelocity = 5f;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of measured held angular velocity retained on release.")]
        private float releaseAngularVelocityMultiplier = 1f;
        [SerializeField, Min(0f)] private float maximumReleaseAngularVelocity = 6f;
        [SerializeField, Min(0f)] private float linearDamping = 0.5f;
        [SerializeField, Min(0f)] private float angularDamping = 0.5f;
        [SerializeField, Min(0f), Tooltip("Releases below both gentle thresholds discard incidental angular momentum.")]
        private float gentlePlacementSpeed = 0.45f;
        [SerializeField, Min(0f)] private float gentlePlacementAngularSpeed = 1f;
        [SerializeField] private bool stabilizeGentlePlacement = true;
        [SerializeField, Min(0f), Tooltip("Clearance retained while a low-velocity placement approaches its support.")]
        private float gentlePlacementClearance = 0.015f;
        [SerializeField, Min(0f), Tooltip("Seconds used to descend from carry clearance during a gentle placement.")]
        private float gentlePlacementDescentResponseSeconds = 0.08f;
        [SerializeField, Min(0f), Tooltip("Multiplier applied to orientation recovery while gentle placement intent is present.")]
        private float gentlePlacementOrientationStrength = 1.25f;
        [SerializeField, Range(0f, 1f), Tooltip("Retained angular velocity for a gentle placement. Fast throws are unaffected.")]
        private float gentlePlacementAngularVelocityScale = 0.05f;

        public float HeldFollowSmoothingSeconds => Mathf.Max(0f, heldFollowSmoothingSeconds);
        public float CollisionCarryScale => Mathf.Clamp01(collisionCarryScale);
        public float HeldCollisionSkin => Mathf.Max(0f, heldCollisionSkin);
        public float PickupLiftDuration => Mathf.Max(0f, pickupLiftDuration);
        public float HeldClearance => Mathf.Max(0f, heldClearance);
        public float SupportHeightResponseSeconds => Mathf.Max(0f, supportHeightResponseSeconds);
        public bool NormalizeOrientationOnPickup => normalizeOrientationOnPickup;
        public Quaternion CarryLocalRotation => Quaternion.Euler(carryLocalEulerAngles);
        public float OrientationRecoverySpeed => Mathf.Max(0f, orientationRecoverySpeed);
        public float OrientationRecoveryInputPauseSeconds => Mathf.Max(0f, orientationRecoveryInputPauseSeconds);
        public float Mass => Mathf.Max(0.001f, mass);
        public float ReleaseLinearVelocityRetention => Mathf.Clamp01(releaseLinearVelocityRetention);
        public float MaximumReleaseLinearVelocity => Mathf.Max(0f, maximumReleaseLinearVelocity);
        public float ReleaseAngularVelocityMultiplier => Mathf.Clamp01(releaseAngularVelocityMultiplier);
        public float MaximumReleaseAngularVelocity => Mathf.Max(0f, maximumReleaseAngularVelocity);
        public float LinearDamping => Mathf.Max(0f, linearDamping);
        public float AngularDamping => Mathf.Max(0f, angularDamping);
        public float GentlePlacementSpeed => Mathf.Max(0f, gentlePlacementSpeed);
        public float GentlePlacementAngularSpeed => Mathf.Max(0f, gentlePlacementAngularSpeed);
        public bool StabilizeGentlePlacement => stabilizeGentlePlacement;
        public float GentlePlacementClearance => Mathf.Max(0f, gentlePlacementClearance);
        public float GentlePlacementDescentResponseSeconds => Mathf.Max(0f, gentlePlacementDescentResponseSeconds);
        public float GentlePlacementOrientationStrength => Mathf.Max(0f, gentlePlacementOrientationStrength);
        public float GentlePlacementAngularVelocityScale => Mathf.Clamp01(gentlePlacementAngularVelocityScale);

        internal static PhysicalObjectInteractionProfile CardDefault() =>
            new PhysicalObjectInteractionProfile
            {
                heldFollowSmoothingSeconds = 0f,
                collisionCarryScale = 0.35f,
                heldCollisionSkin = 0.003f,
                pickupLiftDuration = 0.1f,
                heldClearance = 0.18f,
                supportHeightResponseSeconds = 0.06f,
                normalizeOrientationOnPickup = true,
                carryLocalEulerAngles = Vector3.zero,
                orientationRecoverySpeed = 720f,
                orientationRecoveryInputPauseSeconds = 0.2f,
                mass = 0.03f,
                releaseLinearVelocityRetention = 0.9f,
                maximumReleaseLinearVelocity = 6f,
                releaseAngularVelocityMultiplier = 0.85f,
                maximumReleaseAngularVelocity = 2.5f,
                linearDamping = 1f,
                angularDamping = 3.5f,
                gentlePlacementSpeed = 0.45f,
                gentlePlacementAngularSpeed = 1f,
                stabilizeGentlePlacement = true,
                gentlePlacementClearance = 0.012f,
                gentlePlacementDescentResponseSeconds = 0.06f,
                gentlePlacementOrientationStrength = 1.5f,
                gentlePlacementAngularVelocityScale = 0f
            };

        internal static PhysicalObjectInteractionProfile DieDefault() =>
            new PhysicalObjectInteractionProfile
            {
                heldFollowSmoothingSeconds = 0f,
                collisionCarryScale = 0.3f,
                heldCollisionSkin = 0.004f,
                pickupLiftDuration = 0.1f,
                heldClearance = 0.35f,
                supportHeightResponseSeconds = 0.08f,
                normalizeOrientationOnPickup = true,
                carryLocalEulerAngles = Vector3.zero,
                orientationRecoverySpeed = 240f,
                orientationRecoveryInputPauseSeconds = 0.2f,
                mass = 0.12f,
                releaseLinearVelocityRetention = 1f,
                maximumReleaseLinearVelocity = 8f,
                releaseAngularVelocityMultiplier = 1f,
                maximumReleaseAngularVelocity = 6f,
                linearDamping = 0.12f,
                angularDamping = 0.12f,
                gentlePlacementSpeed = 0.25f,
                gentlePlacementAngularSpeed = 0.6f,
                stabilizeGentlePlacement = true,
                gentlePlacementClearance = 0.07f,
                gentlePlacementDescentResponseSeconds = 0.12f,
                gentlePlacementOrientationStrength = 0.35f,
                gentlePlacementAngularVelocityScale = 0.8f
            };

        internal static PhysicalObjectInteractionProfile PawnDefault() =>
            new PhysicalObjectInteractionProfile
            {
                heldFollowSmoothingSeconds = 0f,
                collisionCarryScale = 0.2f,
                heldCollisionSkin = 0.005f,
                pickupLiftDuration = 0.12f,
                heldClearance = 0.25f,
                supportHeightResponseSeconds = 0.07f,
                normalizeOrientationOnPickup = true,
                carryLocalEulerAngles = Vector3.zero,
                orientationRecoverySpeed = 540f,
                orientationRecoveryInputPauseSeconds = 0.2f,
                mass = 0.1f,
                releaseLinearVelocityRetention = 0.8f,
                maximumReleaseLinearVelocity = 5f,
                releaseAngularVelocityMultiplier = 0.8f,
                maximumReleaseAngularVelocity = 3f,
                linearDamping = 2f,
                angularDamping = 5f,
                gentlePlacementSpeed = 0.5f,
                gentlePlacementAngularSpeed = 1.25f,
                stabilizeGentlePlacement = true,
                gentlePlacementClearance = 0.02f,
                gentlePlacementDescentResponseSeconds = 0.08f,
                gentlePlacementOrientationStrength = 1.25f,
                gentlePlacementAngularVelocityScale = 0.05f
            };

        internal static PhysicalObjectInteractionProfile TokenDefault() =>
            new PhysicalObjectInteractionProfile
            {
                heldFollowSmoothingSeconds = 0f,
                collisionCarryScale = 0.25f,
                heldCollisionSkin = 0.004f,
                pickupLiftDuration = 0.1f,
                heldClearance = 0.2f,
                supportHeightResponseSeconds = 0.06f,
                normalizeOrientationOnPickup = true,
                carryLocalEulerAngles = Vector3.zero,
                orientationRecoverySpeed = 600f,
                orientationRecoveryInputPauseSeconds = 0.2f,
                mass = 0.05f,
                releaseLinearVelocityRetention = 0.85f,
                maximumReleaseLinearVelocity = 5.5f,
                releaseAngularVelocityMultiplier = 0.85f,
                maximumReleaseAngularVelocity = 4f,
                linearDamping = 1.8f,
                angularDamping = 4f,
                gentlePlacementSpeed = 0.5f,
                gentlePlacementAngularSpeed = 1.25f,
                stabilizeGentlePlacement = true,
                gentlePlacementClearance = 0.01f,
                gentlePlacementDescentResponseSeconds = 0.06f,
                gentlePlacementOrientationStrength = 1.4f,
                gentlePlacementAngularVelocityScale = 0.03f
            };

        public bool ApplyRelease(ref Vector3 linear, ref Vector3 angular)
        {
            linear.y = 0f;
            linear = Vector3.ClampMagnitude(
                linear * ReleaseLinearVelocityRetention,
                MaximumReleaseLinearVelocity);
            bool gentle = IsGentlePlacement(linear, angular);
            angular = Vector3.ClampMagnitude(
                angular * Mathf.Clamp01(ReleaseAngularVelocityMultiplier),
                MaximumReleaseAngularVelocity);

            if (StabilizeGentlePlacement && gentle)
            {
                angular *= GentlePlacementAngularVelocityScale;
            }

            return gentle;
        }

        public float ResolveGentlePlacementIntent(Vector3 linear, Vector3 angular)
        {
            if (!StabilizeGentlePlacement || GentlePlacementSpeed <= 0f || GentlePlacementAngularSpeed <= 0f)
            {
                return 0f;
            }

            float linearIntent = 1f - Mathf.Clamp01(linear.magnitude / GentlePlacementSpeed);
            float angularIntent = 1f - Mathf.Clamp01(angular.magnitude / GentlePlacementAngularSpeed);
            return Mathf.Min(linearIntent, angularIntent);
        }

        private bool IsGentlePlacement(Vector3 linear, Vector3 angular) =>
            GentlePlacementSpeed > 0f
            && GentlePlacementAngularSpeed > 0f
            && linear.magnitude <= GentlePlacementSpeed
            && angular.magnitude <= GentlePlacementAngularSpeed;
    }

    /// <summary>Shared manual-release tuning, not Die Roll impulses or Runtime State.</summary>
    [Serializable]
    public sealed class PhysicalInteractionConfig
    {
        [SerializeField, Min(0.001f), Tooltip("Recent held-motion history used to calculate tabletop-plane drag velocity.")]
        private float dragVelocitySampleWindowSeconds = 0.06f;
        [SerializeField, Min(0f), Tooltip("Deliberate held-rotation smoothing time in seconds. Zero disables smoothing.")]
        private float releaseSmoothingSeconds = 0.04f;
        [SerializeField, Min(0f), Tooltip("Maximum manual-release angular speed in radians per second. Does not limit Roll impulses.")]
        private float maximumReleaseAngularVelocity = 6f;
        [SerializeField, Min(0f), Tooltip("Discard release momentum when the last pointer sample is older than this many seconds.")]
        private float releaseSampleTimeoutSeconds = 0.16f;
        [Header("Broad Object Profiles")]
        [SerializeField] private PhysicalObjectInteractionProfile card = PhysicalObjectInteractionProfile.CardDefault();
        [SerializeField] private PhysicalObjectInteractionProfile die = PhysicalObjectInteractionProfile.DieDefault();
        [SerializeField] private PhysicalObjectInteractionProfile pawn = PhysicalObjectInteractionProfile.PawnDefault();
        [SerializeField] private PhysicalObjectInteractionProfile token = PhysicalObjectInteractionProfile.TokenDefault();

        public float DragVelocitySampleWindowSeconds => Mathf.Max(0.001f, dragVelocitySampleWindowSeconds);
        public float ReleaseSmoothingSeconds => Mathf.Max(0f, releaseSmoothingSeconds);
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

    /// <summary>Per-grab planar drag and deliberate-rotation sampling, using reusable storage and unscaled time.</summary>
    internal sealed class PhysicalReleaseMotion
    {
        private const int LinearSampleCapacity = 16;

        private readonly PhysicalInteractionConfig config;
        private readonly Vector3[] linearPositions = new Vector3[LinearSampleCapacity];
        private readonly float[] linearTimes = new float[LinearSampleCapacity];
        private int linearSampleCount;
        private int linearSampleWriteIndex;
        private bool hasAngularSample;
        private bool hasAngularEstimate;
        private float lastAngularSampleTime;
        private Quaternion lastDeliberateRotation;
        private Vector3 angularVelocity;

        public PhysicalReleaseMotion(PhysicalInteractionConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Reset()
        {
            linearSampleCount = 0;
            linearSampleWriteIndex = 0;
            hasAngularSample = false;
            hasAngularEstimate = false;
            angularVelocity = Vector3.zero;
        }

        public void SampleLinear(Vector3 actualHeldAnchorPosition, float time)
        {
            actualHeldAnchorPosition.y = 0f;
            linearPositions[linearSampleWriteIndex] = actualHeldAnchorPosition;
            linearTimes[linearSampleWriteIndex] = time;
            linearSampleWriteIndex = (linearSampleWriteIndex + 1) % LinearSampleCapacity;
            linearSampleCount = Mathf.Min(linearSampleCount + 1, LinearSampleCapacity);
        }

        public Vector3 GetLinearRelease(float time)
        {
            if (linearSampleCount < 2)
            {
                return Vector3.zero;
            }

            int newest = (linearSampleWriteIndex - 1 + LinearSampleCapacity) % LinearSampleCapacity;
            float newestTime = linearTimes[newest];
            if (time - newestTime >= config.ReleaseSampleTimeoutSeconds)
            {
                return Vector3.zero;
            }

            int oldest = newest;
            for (int offset = 1; offset < linearSampleCount; offset++)
            {
                int candidate = (newest - offset + LinearSampleCapacity) % LinearSampleCapacity;
                if (newestTime - linearTimes[candidate] > config.DragVelocitySampleWindowSeconds)
                {
                    break;
                }

                oldest = candidate;
            }

            float dt = newestTime - linearTimes[oldest];
            return dt > 0.0001f
                ? (linearPositions[newest] - linearPositions[oldest]) / dt
                : Vector3.zero;
        }

        public void SampleDeliberateRotation(Quaternion rotation, float time)
        {
            if (!hasAngularSample)
            {
                hasAngularSample = true;
                StoreAngularSample(rotation, time);
                return;
            }

            float dt = time - lastAngularSampleTime;
            if (dt <= 0.0001f) return;
            Quaternion delta = rotation * Quaternion.Inverse(lastDeliberateRotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Vector3 sampledAngularVelocity = axis.sqrMagnitude > 0f && !float.IsNaN(axis.x)
                ? axis * (angle * Mathf.Deg2Rad / dt) : Vector3.zero;

            // Clamp before filtering too, so a single discontinuity cannot leave a long impulse tail.
            sampledAngularVelocity = Vector3.ClampMagnitude(
                sampledAngularVelocity,
                config.MaximumReleaseAngularVelocity);
            if (!hasAngularEstimate)
            {
                angularVelocity = sampledAngularVelocity;
                hasAngularEstimate = true;
            }
            else
            {
                float blend = config.ReleaseSmoothingSeconds > 0f
                    ? 1f - Mathf.Exp(-dt / config.ReleaseSmoothingSeconds) : 1f;
                angularVelocity = Vector3.Lerp(angularVelocity, sampledAngularVelocity, blend);
            }
            StoreAngularSample(rotation, time);
        }

        public Vector3 GetAngularRelease(float time)
        {
            bool recent = hasAngularEstimate
                && time - lastAngularSampleTime < config.ReleaseSampleTimeoutSeconds;
            return recent
                ? Vector3.ClampMagnitude(angularVelocity, config.MaximumReleaseAngularVelocity)
                : Vector3.zero;
        }

        // TEMP TTS DIAGNOSTICS BEGIN
        internal int DiagnosticSampleCount(float time, out float newestAgeSeconds)
        {
            newestAgeSeconds = -1f;
            if (linearSampleCount == 0) return 0;
            int newest = (linearSampleWriteIndex - 1 + LinearSampleCapacity) % LinearSampleCapacity;
            float newestTime = linearTimes[newest];
            newestAgeSeconds = time - newestTime;
            int count = 1;
            for (int offset = 1; offset < linearSampleCount; offset++)
            {
                int candidate = (newest - offset + LinearSampleCapacity) % LinearSampleCapacity;
                if (newestTime - linearTimes[candidate] > config.DragVelocitySampleWindowSeconds) break;
                count++;
            }

            return count;
        }
        // TEMP TTS DIAGNOSTICS END
        private void StoreAngularSample(Quaternion rotation, float time)
        {
            lastDeliberateRotation = rotation;
            lastAngularSampleTime = time;
        }
    }
}
