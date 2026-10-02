using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Shared Rigidbody adapter. The injected local authority owns lifecycle/settlement.
    /// TTS-FEEL: a held object is now a DYNAMIC body (gravity off) that chases the cursor target with
    /// velocity in FixedUpdate. It lags a little, leans into travel, collides with and shoves other pieces,
    /// and release velocity comes from the real body motion. Update-time Follow() only computes the target.
    /// </summary>
    public sealed class PhysicalLooseObject : MonoBehaviour
    {
        private const int SupportHitBufferCapacity = 32;

        /// <summary>
        /// TTS-FEEL tuning. Defaults are a starting point; migrate into PhysicalObjectInteractionProfile
        /// when you want per-kind control. All speeds are in world units/second.
        /// </summary>
        private static class Tune
        {
            // Held body
            public const float HeldMassMultiplier = 6f;          // heavier while held so it shoves lighter pieces
            public const float HeldLinearDamping = 0f;
            public const float HeldAngularDamping = 4f;
            public const float HeldMaxDepenetrationVelocity = 4f; // avoids violent pop-outs when shoved into things
            public const float FollowStiffness = 35f;            // 1/s. higher = tighter to cursor
            public const float MaxHeldSpeed = 60f;
            public const float VelocitySmoothing = 30f;          // 1/s. higher = snappier velocity response
            public const float TiltDegreesPerSpeed = 1.6f;       // lean per unit/second of planar speed
            public const float MaxTiltDegrees = 0f;
            public const float RotationGain = 14f;
            public const float MaxHeldAngularSpeed = 20f;

            // Rigidbody quality
            public const float MaxAngularVelocity = 50f;         // Unity default of 7 kills dice spin
            public const int SolverIterations = 12;
            public const int SolverVelocityIterations = 4;

            // Release spin
            public const float SpinMinSpeed = 0.8f;
            public const float DiceRollFactor = 0.55f;           // fraction of true rolling omega = v / r
            public const float DiceSpinRandomness = 0.35f;
            public const float PieceYawSpinPerSpeed = 0.12f;
            public const float PieceYawSpinMax = 1.5f;

            // Settlement
            public const float RestLinearSpeed = 0.05f;
            public const float RestAngularSpeed = 0.10f;
            public const float RestSeconds = 0.35f;
            public const int MaxCockedNudges = 3;
            public const float CockedNudgeUpSpeed = 2.0f;
            public const float CockedNudgeSpin = 6f;

            // Support detection
            public const float SupportRayLift = 10f;
            public const float SupportRayDepth = 100f;
            public const float FootprintInset = 0.8f;
            public const float FootprintMaxStep = 0.35f;        // corners may lift the object over steps up to this tall

            // Roll()
            public const float RollApexHeight = 1.2f;            // keeps roll arc constant when gravity changes

            // Out-of-bounds recovery
            public const float RecoverBelowSurfaceDistance = 2f; // recover once below the lowest tabletop surface top by this much
        }

        private TabletopObjectView view;
        private Rigidbody body;
        private Collider physicalCollider;
        private LocalPhysicalObjectAuthority authority;
        private PhysicalObjectInteractionProfile profile;
        private PhysicalObjectState applied;
        private bool held;
        private float holdDepth;
        private Vector2 grabScreenOffset;
        private Vector3 grabLocalAnchor;
        private bool hasPointerAnchor;
        private float pickupStartTime;
        private float pickupStartBottomHeight;
        private float containedPickupLift;
        private bool hasSupportHeight;
        private float smoothedSupportHeight;
        private float currentClearance;
        private float gentlePlacementIntent;
        private float orientationRecoveryResumeTime;
        private Vector3 pickupPosition;
        private Quaternion pickupRotation;
        private Quaternion carryOrientationTarget;
        private bool deliberateRotationPending;
        private PhysicalReleaseMotion releaseMotion;
        private readonly RaycastHit[] supportHitBuffer = new RaycastHit[SupportHitBufferCapacity];
        private float nextCheckpoint;
        private int dynamicFrames;
        private PlayerId actor;
        private PhysicalObjectState grabOrigin;
        private bool userActionActive;
        private bool compoundActionActive;

        // TTS-FEEL state
        private Quaternion heldBaseRotation;   // logical carry orientation (no lean); body chases lean * this
        private Vector3 heldTargetPosition;    // computed in Follow (Update), chased in FixedUpdate
        private bool hasHeldTarget;
        private float lastAnchorHeight;        // world Y of the grabbed point last frame (breaks projection/support circularity)
        private float restTimer;
        private int cockedNudges;

        // Out-of-bounds recovery (runtime only; never committed)
        private bool frozenAfterRecovery;      // kinematic hover after recovery until grabbed/rolled
        private float recoveryHeight = float.NaN;
        private bool hasReleasePose;
        private Vector3 releasePosition;
        private Quaternion releaseRotation;
        private bool hasSettledPose;
        private Vector3 settledPosition;
        private Quaternion settledRotation;
        private bool hasHeldPose;
        private Vector3 heldPosition;
        private Quaternion heldRotation;
        private bool missingRecoveryPoseReported;

        public bool IsHeld => held;
        public bool OwnsLooseTransform => view != null && view.IsBound && view.BoundState.ContainerId.IsEmpty;
        public Rigidbody Body => body;
        public TableCoordinate LayoutCoordinate => authority.Surfaces.Coordinate(body.position);
        public Collider PhysicalCollider
        {
            get
            {
                if (physicalCollider != null) return physicalCollider;
                foreach (Collider candidate in GetComponents<Collider>()) if (candidate.enabled) return candidate;
                foreach (Collider candidate in GetComponentsInChildren<Collider>(true)) if (candidate.enabled) return candidate;
                return null;
            }
        }

        internal void Initialize(TabletopObjectView view, LocalPhysicalObjectAuthority authority)
        {
            this.view = view; this.authority = authority;
            profile = authority.InteractionConfig.ResolveProfile(view.BoundState.Kind);
            releaseMotion = new PhysicalReleaseMotion(authority.InteractionConfig);
            held = false;
            applied = null;
            grabOrigin = null;
            userActionActive = false;
            compoundActionActive = false;
            hasPointerAnchor = false;
            hasSupportHeight = false;
            hasHeldTarget = false;
            containedPickupLift = 0f;
            gentlePlacementIntent = 0f;
            orientationRecoveryResumeTime = 0f;
            deliberateRotationPending = false;
            restTimer = 0f;
            cockedNudges = 0;
            frozenAfterRecovery = false;
            hasReleasePose = false;
            hasSettledPose = false;
            hasHeldPose = false;
            missingRecoveryPoseReported = false;
            if (PhysicalCollider == null) throw new InvalidOperationException("Loose physics requires an enabled collider on the wrapper or its visual child.");
            physicalCollider = PhysicalCollider;
            if (physicalCollider.sharedMaterial == null)
                physicalCollider.sharedMaterial = TabletopPhysicsSettings.MaterialFor(view is DieView);
            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            RestoreProfilePhysics();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = view is DieView
                ? CollisionDetectionMode.ContinuousDynamic
                : CollisionDetectionMode.ContinuousSpeculative;
            body.maxAngularVelocity = Tune.MaxAngularVelocity;
            body.solverIterations = Tune.SolverIterations;
            body.solverVelocityIterations = Tune.SolverVelocityIterations;
            actor = authority.Actor;
            RefreshRecoveryHeight();
            Synchronize();
            if (OwnsLooseTransform && applied != null) RecordSettledPose(body.position, body.rotation);
        }

        public void ApplyAccepted()
        {
            if (body == null || !view.IsBound) return;
            if (!OwnsLooseTransform) { DisableForContainer(); return; }
            PhysicalObjectState state = view.BoundState.PhysicalState;
            if (state == null || ReferenceEquals(state, applied)) return;
            bool stateHeld = state.Mode == PhysicalObjectMode.Held;
            bool sampleDeliberateRotation = held && stateHeld && deliberateRotationPending;
            bool preserveHeldAnchor = held && hasPointerAnchor;
            Vector3 heldAnchorWorld = preserveHeldAnchor ? GrabAnchorWorld() : Vector3.zero;
            applied = state;
            actor = state.ControllingPlayerId;
            if (state.Mode != PhysicalObjectMode.Sleeping && state.Mode != PhysicalObjectMode.SleepingUnresolved)
                frozenAfterRecovery = false;
            body.position = Vector(state.Position);
            body.rotation = Rotation(state.Rotation);
            if (sampleDeliberateRotation)
            {
                releaseMotion.SampleDeliberateRotation(body.rotation, Time.unscaledTime);
            }
            deliberateRotationPending = false;
            if (preserveHeldAnchor && stateHeld)
            {
                body.position = heldAnchorWorld - RotatedGrabAnchor(body.rotation);
            }
            if (stateHeld)
            {
                carryOrientationTarget = ResolveCarryOrientation();
                heldBaseRotation = body.rotation;
            }
            transform.SetPositionAndRotation(body.position, body.rotation);
            held = stateHeld;

            // TTS-FEEL: held bodies stay DYNAMIC (gravity off). Only user-locked objects are kinematic.
            bool locked = view.BoundState.IsUserLocked;
            body.isKinematic = locked || frozenAfterRecovery;
            body.useGravity = !held && !locked && !frozenAfterRecovery;
            body.detectCollisions = true;
            if (held) ApplyHeldPhysics(); else RestoreProfilePhysics();
            if (!body.isKinematic)
            {
                if (!held)
                {
                    body.linearVelocity = Vector(state.Velocity);
                    body.angularVelocity = Vector(state.AngularVelocity);
                    if (state.Mode == PhysicalObjectMode.Sleeping || state.Mode == PhysicalObjectMode.SleepingUnresolved)
                        body.Sleep();
                    else body.WakeUp();
                }
                else body.WakeUp(); // FixedUpdate owns held velocities
            }
            dynamicFrames = 0;
            restTimer = 0f;
            RecordAcceptedPose(state);
        }

        public void DisableForContainer()
        {
            if (body == null) return;
            held = false;
            hasPointerAnchor = false;
            hasSupportHeight = false;
            hasHeldTarget = false;
            containedPickupLift = 0f;
            gentlePlacementIntent = 0f;
            deliberateRotationPending = false;
            frozenAfterRecovery = false;
            body.isKinematic = true;
            body.useGravity = false;
            RestoreProfilePhysics();
            // Retain raycast selection colliders, but exclude contained pieces from dynamic contacts.
            authority.SetContainedCollisions(this, true);
            applied = null;
        }

        public bool BeginHold()
        {
            if (held) return true;
            actor = authority.Actor;
            grabOrigin = view.BoundState.PhysicalState;
            userActionActive = true;
            compoundActionActive = false;
            if (OwnsLooseTransform && !Commit(
                    Capture(PhysicalObjectMode.Held),
                    null,
                    AuthoritativeActionRecordMode.Intermediate))
            {
                userActionActive = false;
                return false;
            }
            held = true;
            frozenAfterRecovery = false;

            // Contained cards are positioned by presentation (transform) while kinematic; make sure the
            // physics pose matches the visible pose before the body becomes dynamic.
            if (!OwnsLooseTransform)
            {
                body.position = transform.position;
                body.rotation = transform.rotation;
                Physics.SyncTransforms();
            }

            // TTS-FEEL: dynamic held body, gravity off, real collisions.
            body.isKinematic = false;
            body.useGravity = false;
            body.detectCollisions = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            ApplyHeldPhysics();
            body.WakeUp();

            authority.SetContainedCollisions(this, false);
            authority.StopAnimation(transform);
            pickupPosition = body.position;
            pickupRotation = body.rotation;
            carryOrientationTarget = ResolveCarryOrientation();
            heldBaseRotation = body.rotation;
            heldTargetPosition = body.position;
            hasHeldTarget = false;
            lastAnchorHeight = hasPointerAnchor ? GrabAnchorWorld().y : body.position.y;
            pickupStartTime = Time.unscaledTime;
            pickupStartBottomHeight = PhysicalCollider != null
                ? PhysicalCollider.bounds.min.y
                : body.position.y;
            containedPickupLift = 0f;
            hasSupportHeight = false;
            currentClearance = profile.HeldClearance;
            gentlePlacementIntent = 0f;
            orientationRecoveryResumeTime = 0f;
            deliberateRotationPending = false;
            restTimer = 0f;
            cockedNudges = 0;
            releaseMotion.Reset();
            return true;
        }

        internal void PreparePointerAnchor(Vector2 screenPosition)
        {
            if (body == null)
            {
                return;
            }

            InitializePointerAnchor(screenPosition);
        }

        /// <summary>
        /// Update-time: compute where the grabbed point should be. Does NOT move the body.
        /// FixedUpdate chases heldTargetPosition / heldBaseRotation with velocities.
        /// </summary>
        public void Follow(Vector2 screenPosition)
        {
            if (!held && !BeginHold()) return;
            if (!hasPointerAnchor) InitializePointerAnchor(screenPosition);

            float deltaTime = Time.unscaledDeltaTime;
            UpdateHeldBaseRotation(deltaTime);

            // TTS-FEEL: intersect the cursor ray with a HORIZONTAL plane at the grabbed point's height.
            // (Camera-parallel projection drifts from the cursor whenever Y changes.)
            Vector2 anchoredScreenPosition = screenPosition + grabScreenOffset;
            Vector3 targetAnchor = ProjectScreenToHeight(anchoredScreenPosition, lastAnchorHeight);
            Vector3 target = targetAnchor - RotatedGrabAnchor(body.rotation);

            Vector3 sampledAngularVelocity = releaseMotion.GetAngularRelease(Time.unscaledTime);
            Vector3 sampledLinearVelocity = releaseMotion.GetLinearRelease(Time.unscaledTime);
            gentlePlacementIntent = profile.ResolveGentlePlacementIntent(
                sampledLinearVelocity,
                sampledAngularVelocity);

            bool hasSupport = TryResolveSupportHeight(target, out float supportHeight);
            if (hasSupport)
            {
                smoothedSupportHeight = SmoothSupportHeight(supportHeight, deltaTime);
                float pickupProgress = PickupProgress();
                float targetClearance = pickupProgress < 1f
                    ? profile.HeldClearance
                    : Mathf.Lerp(
                        profile.HeldClearance,
                        profile.GentlePlacementClearance,
                        gentlePlacementIntent);
                float clearanceResponse = targetClearance > currentClearance
                    ? profile.SupportHeightResponseSeconds
                    : profile.GentlePlacementDescentResponseSeconds;
                currentClearance = SmoothValue(
                    currentClearance,
                    targetClearance,
                    clearanceResponse,
                    deltaTime);

                float targetBottomHeight = smoothedSupportHeight + currentClearance;
                float easedPickup = Mathf.SmoothStep(0f, 1f, pickupProgress);
                float bottomHeight = pickupProgress < 1f
                    ? Mathf.Lerp(
                        pickupStartBottomHeight,
                        Mathf.Max(pickupStartBottomHeight, targetBottomHeight),
                        easedPickup)
                    : targetBottomHeight;
                target.y = bottomHeight + RootHeightAboveBottom();
            }
            else if (containedPickupLift > 0f)
            {
                target.y += containedPickupLift * Mathf.SmoothStep(0f, 1f, PickupProgress());
            }

            Vector3 next = ResolveDirectCarryPosition(target, deltaTime);
            lastAnchorHeight = next.y + RotatedGrabAnchor(body.rotation).y;
            heldTargetPosition = next;
            hasHeldTarget = true;
        }

        /// <summary>TTS-FEEL: velocity-driven chase. Collisions are real; the body can shove and be blocked.</summary>
        private void FixedUpdate()
        {
            if (!held || body == null || body.isKinematic || !hasHeldTarget) return;
            if (body.IsSleeping()) body.WakeUp();

            float dt = Time.fixedDeltaTime;
            float k = 1f - Mathf.Exp(-Tune.VelocitySmoothing * dt);

            // Linear: spring-like chase toward the target.
            Vector3 desired = Vector3.ClampMagnitude(
                (heldTargetPosition - body.position) * Tune.FollowStiffness,
                Tune.MaxHeldSpeed);
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, desired, k);

            // Angular: lean into the direction of travel on top of the carry orientation.
            Vector3 velocity = body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            float planarSpeed = planar.magnitude;
            Quaternion lean = Quaternion.identity;
            if (planarSpeed > 0.05f)
            {
                float tilt = Mathf.Min(planarSpeed * Tune.TiltDegreesPerSpeed, Tune.MaxTiltDegrees);
                // Leading edge dips (top leans forward). Negate tilt to lean the other way.
                lean = Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, planar / planarSpeed));
            }

            Quaternion targetRotation = lean * heldBaseRotation;
            body.angularVelocity = Vector3.Lerp(
                body.angularVelocity,
                AngularVelocityTo(body.rotation, targetRotation, Tune.RotationGain, Tune.MaxHeldAngularSpeed),
                k);

            // Sample the REAL body (not the commanded target) so release velocity matches what the player saw.
            releaseMotion.SampleLinear(
                body.position + RotatedGrabAnchor(body.rotation),
                Time.fixedUnscaledTime);
        }

        internal bool BeginContainedPickup(float lift)
        {
            if (OwnsLooseTransform || !BeginHold()) return false;
            containedPickupLift = Mathf.Max(0f, lift);
            releaseMotion.Reset();
            return true;
        }

        internal bool SnapHeldPreview(Vector3 position, Quaternion rotation)
        {
            if (!held && !BeginHold()) return false;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            carryOrientationTarget = rotation;
            heldBaseRotation = rotation;
            heldTargetPosition = position;
            hasHeldTarget = true;
            hasSupportHeight = false;
            deliberateRotationPending = false;
            releaseMotion.Reset();
            return true;
        }

        public PhysicalObjectState ReleaseState()
        {
            Vector3 velocity = releaseMotion.GetLinearRelease(Time.unscaledTime);
            Vector3 angularVelocity = releaseMotion.GetAngularRelease(Time.unscaledTime);
            profile.ApplyRelease(ref velocity, ref angularVelocity);
            // TTS-FEEL: spin is synthesized AFTER profile clamps so a flick actually tumbles.
            angularVelocity = SynthesizeReleaseSpin(velocity, angularVelocity);
            hasReleasePose = true;
            releasePosition = body.position;
            releaseRotation = body.rotation;
            missingRecoveryPoseReported = false;
            if (float.IsNaN(recoveryHeight)) RefreshRecoveryHeight();
            // Use the physics pose (not the interpolated transform) so the committed pose matches the body.
            return State(body.position, body.rotation, velocity,
                angularVelocity, PhysicalObjectMode.Dynamic, actor);
        }

        public bool Release()
        {
            if (!OwnsLooseTransform) return false;
            if (!Commit(ReleaseState(), null, AuthoritativeActionRecordMode.Intermediate)) { Cancel(); return false; }
            held = false;
            hasPointerAnchor = false;
            hasSupportHeight = false;
            hasHeldTarget = false;
            containedPickupLift = 0f;
            gentlePlacementIntent = 0f;
            deliberateRotationPending = false;
            RestoreProfilePhysics();
            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            body.linearVelocity = Vector(applied.Velocity);
            body.angularVelocity = Vector(applied.AngularVelocity);
            body.WakeUp();
            dynamicFrames = 0;
            restTimer = 0f;
            cockedNudges = 0;
            return true;
        }

        public void CaptureBeforeRotation()
        {
            if (held)
            {
                orientationRecoveryResumeTime = Time.unscaledTime + profile.OrientationRecoveryInputPauseSeconds;
                deliberateRotationPending = true;
            }
            if (!held && frozenAfterRecovery)
            {
                // Frozen pieces stay frozen while rotated in place: commit a zero-velocity sleeping state.
                if (OwnsLooseTransform) CommitFrozenPose(body.position, body.rotation,
                    userActionActive || compoundActionActive
                        ? AuthoritativeActionRecordMode.Intermediate
                        : AuthoritativeActionRecordMode.None);
                return;
            }
            if (OwnsLooseTransform) Commit(
                Capture(held ? PhysicalObjectMode.Held : PhysicalObjectMode.Dynamic),
                null,
                userActionActive || compoundActionActive
                    ? AuthoritativeActionRecordMode.Intermediate
                    : AuthoritativeActionRecordMode.None);
        }

        public void Cancel()
        {
            if (held && OwnsLooseTransform && grabOrigin != null)
                Commit(State(Vector(grabOrigin.Position), Rotation(grabOrigin.Rotation), Vector(grabOrigin.Velocity),
                    Vector(grabOrigin.AngularVelocity), PhysicalObjectMode.Dynamic, actor), null,
                    AuthoritativeActionRecordMode.CancelTransaction);
            held = false;
            hasPointerAnchor = false;
            hasSupportHeight = false;
            hasHeldTarget = false;
            containedPickupLift = 0f;
            gentlePlacementIntent = 0f;
            deliberateRotationPending = false;
            userActionActive = false;
            compoundActionActive = false;
            frozenAfterRecovery = false;
            applied = null;
            RestoreProfilePhysics();
            Synchronize();
        }

        public bool Roll(PlayerId? requestingActor = null, bool partOfCompoundAction = false)
        {
            if (!(view is DieView) || !OwnsLooseTransform || view.BoundState.IsUserLocked || held) return false;
            actor = requestingActor ?? authority.Actor;
            userActionActive = !partOfCompoundAction;
            compoundActionActive = partOfCompoundAction;

            // TTS-FEEL: launch speed derived from gravity so the roll arc stays the same when gravity changes.
            float upSpeed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * Tune.RollApexHeight);
            float side = upSpeed * 0.3f;
            PhysicalObjectState launch = State(transform.position + Vector3.up * 0.8f, transform.rotation,
                new Vector3(UnityEngine.Random.Range(-side, side), upSpeed, UnityEngine.Random.Range(-side, side)),
                UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(20f, 40f), PhysicalObjectMode.Dynamic, actor);
            if (!Commit(launch, null, AuthoritativeActionRecordMode.Intermediate))
            {
                userActionActive = false;
                compoundActionActive = false;
                return false;
            }
            cockedNudges = 0;
            frozenAfterRecovery = false;
            if (float.IsNaN(recoveryHeight)) RefreshRecoveryHeight();
            applied = null;
            ApplyAccepted();
            return true;
        }

        internal void Tick()
        {
            if (view == null || !view.IsBound || !gameObject.activeInHierarchy) return;
            Synchronize();
            if (!OwnsLooseTransform || held || body.isKinematic) return;
            if (body.position.y < recoveryHeight) { RecoverOutOfBounds(); return; } // NaN height never recovers
            dynamicFrames++;

            // TTS-FEEL: don't wait on Unity's energy-based sleep alone (dice can creep for seconds).
            bool slow = body.linearVelocity.sqrMagnitude < Tune.RestLinearSpeed * Tune.RestLinearSpeed
                && body.angularVelocity.sqrMagnitude < Tune.RestAngularSpeed * Tune.RestAngularSpeed;
            restTimer = slow ? restTimer + Time.unscaledDeltaTime : 0f;
            bool restedLongEnough = dynamicFrames > 2 && restTimer >= Tune.RestSeconds;
            bool settled = (body.IsSleeping() && dynamicFrames > 2) || restedLongEnough;

            if (settled)
            {
                if (applied != null && (applied.Mode == PhysicalObjectMode.Sleeping
                    || applied.Mode == PhysicalObjectMode.SleepingUnresolved)) return;

                int? value = null;
                if (view is DieView die)
                {
                    if (!die.TryResolvePhysicalValue(out int face))
                    {
                        // Cocked: nudge a few times before giving up, instead of leaving it unresolved.
                        if (cockedNudges < Tune.MaxCockedNudges)
                        {
                            cockedNudges++;
                            body.WakeUp();
                            body.AddForce(Vector3.up * Tune.CockedNudgeUpSpeed, ForceMode.VelocityChange);
                            body.AddTorque(UnityEngine.Random.onUnitSphere * Tune.CockedNudgeSpin, ForceMode.VelocityChange);
                            dynamicFrames = 0;
                            restTimer = 0f;
                            return;
                        }

                        FreezeBody();
                        Commit(State(body.position, body.rotation, Vector3.zero, Vector3.zero,
                            PhysicalObjectMode.SleepingUnresolved, actor), null,
                            userActionActive
                                ? AuthoritativeActionRecordMode.Transaction
                                : compoundActionActive
                                    ? AuthoritativeActionRecordMode.Intermediate
                                    : AuthoritativeActionRecordMode.None);
                        userActionActive = false;
                        compoundActionActive = false;
                        return; // Still cocked after nudges: record the actual resting pose and unresolved status.
                    }
                    value = face;
                }

                FreezeBody();
                Commit(State(body.position, body.rotation, Vector3.zero, Vector3.zero,
                    PhysicalObjectMode.Sleeping, actor), value,
                    userActionActive
                        ? AuthoritativeActionRecordMode.Transaction
                        : compoundActionActive
                            ? AuthoritativeActionRecordMode.Intermediate
                            : AuthoritativeActionRecordMode.None);
                userActionActive = false;
                compoundActionActive = false;
            }
            else if (Time.unscaledTime >= nextCheckpoint)
            {
                nextCheckpoint = Time.unscaledTime + 0.25f;
                Commit(Capture(PhysicalObjectMode.Dynamic), null,
                    userActionActive || compoundActionActive
                        ? AuthoritativeActionRecordMode.Intermediate
                        : AuthoritativeActionRecordMode.None); // Includes continuing off-table falls.
            }
        }

        /// <summary>Make the physics body match a committed Sleeping state when our own rest detector fires first.</summary>
        private void FreezeBody()
        {
            if (body.IsSleeping()) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }

        /// <summary>
        /// Out-of-bounds recovery: return to the last release pose (then last settled, then last held pose),
        /// freeze kinematic there with zero velocity, and commit it as a sleeping state through the authority.
        /// </summary>
        private void RecoverOutOfBounds()
        {
            Vector3 position;
            Quaternion rotation;
            if (hasReleasePose) { position = releasePosition; rotation = releaseRotation; }
            else if (hasSettledPose) { position = settledPosition; rotation = settledRotation; }
            else if (hasHeldPose) { position = heldPosition; rotation = heldRotation; }
            else
            {
                if (!missingRecoveryPoseReported) ReportMissingRecoveryPose();
                missingRecoveryPoseReported = true;
                return;
            }

            if (view is DieView die && hasSettledPose)
            {
                // Keep the release position; prefer a rotation that reads a face.
                body.rotation = rotation;
                if (!die.TryResolvePhysicalValue(out _))
                {
                    body.rotation = settledRotation;
                    if (die.TryResolvePhysicalValue(out _)) rotation = settledRotation;
                }
            }

            FreezeAt(position, rotation);
            bool accepted = CommitFrozenPose(position, rotation,
                userActionActive
                    ? AuthoritativeActionRecordMode.Transaction
                    : compoundActionActive
                        ? AuthoritativeActionRecordMode.Intermediate
                        : AuthoritativeActionRecordMode.None);
            userActionActive = false;
            compoundActionActive = false;
            dynamicFrames = 0;
            restTimer = 0f;
            cockedNudges = 0;
            if (!accepted)
            {
                // Not accepted: drop the local freeze and let the accepted state re-apply on the next Synchronize.
                frozenAfterRecovery = false;
                applied = null;
            }
        }

        private void FreezeAt(Vector3 position, Quaternion rotation)
        {
            // Zero velocities while still dynamic (setting velocity on a kinematic body is not supported).
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            frozenAfterRecovery = true;
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = true;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Commits a zero-velocity sleeping state for a frozen pose; dice report a face or SleepingUnresolved.</summary>
        private bool CommitFrozenPose(Vector3 position, Quaternion rotation, AuthoritativeActionRecordMode recordMode)
        {
            int? value = null;
            PhysicalObjectMode mode = PhysicalObjectMode.Sleeping;
            if (view is DieView die)
            {
                if (die.TryResolvePhysicalValue(out int face)) value = face;
                else mode = PhysicalObjectMode.SleepingUnresolved;
            }

            return Commit(State(position, rotation, Vector3.zero, Vector3.zero, mode, actor), value, recordMode);
        }

        private void RecordAcceptedPose(PhysicalObjectState state)
        {
            // Only resting and held poses are recovery targets; Dynamic checkpoints can be mid-fall.
            if (state == null) return;
            if (state.Mode == PhysicalObjectMode.Sleeping || state.Mode == PhysicalObjectMode.SleepingUnresolved)
                RecordSettledPose(Vector(state.Position), Rotation(state.Rotation));
            else if (state.Mode == PhysicalObjectMode.Held)
            {
                hasHeldPose = true;
                heldPosition = Vector(state.Position);
                heldRotation = Rotation(state.Rotation);
                missingRecoveryPoseReported = false;
            }
        }

        private void RecordSettledPose(Vector3 position, Quaternion rotation)
        {
            hasSettledPose = true;
            settledPosition = position;
            settledRotation = rotation;
            missingRecoveryPoseReported = false;
        }

        /// <summary>Event-time only (the surface registry enumerates with an allocation): never call per frame.</summary>
        private void RefreshRecoveryHeight()
        {
            float lowestTop = float.PositiveInfinity;
            PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
            foreach (PhysicalTabletopSurface surface in PhysicalTabletopSurface.Registered)
            {
                if (surface != null
                    && surface.ParticipatesIn(physicsScene)
                    && surface.TryGetCollider(out Collider surfaceCollider, out _))
                {
                    lowestTop = Mathf.Min(lowestTop, surfaceCollider.bounds.max.y);
                }
            }

            recoveryHeight = float.IsPositiveInfinity(lowestTop)
                ? float.NaN
                : lowestTop - Tune.RecoverBelowSurfaceDistance;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ReportMissingRecoveryPose()
        {
            Debug.LogWarning($"PhysicalLooseObject '{name}' fell out of bounds but has no recorded release, settled or held pose; not recovering.", this);
        }

        private void Synchronize()
        {
            if (held && !OwnsLooseTransform) return; // Contained drag is a preview until transfer acceptance.
            if (!OwnsLooseTransform) { DisableForContainer(); return; }
            authority.SetContainedCollisions(this, false);
            if (view.BoundState.PhysicalState == null)
            {
                PhysicalObjectState initial;
                if (!authority.Surfaces.TryResolveAuthoredLooseObject(
                        view.BoundState.Pose,
                        actor,
                        physicalCollider,
                        view.BoundState.IsUserLocked,
                        out initial))
                    initial = Capture(PhysicalObjectMode.Dynamic); // Authored/template extraction may start off-table.
                if (!Commit(initial, null, AuthoritativeActionRecordMode.None)) return;
                applied = null;
            }
            ApplyAccepted();
        }

        private bool Commit(
            PhysicalObjectState state,
            int? value = null,
            AuthoritativeActionRecordMode recordMode = AuthoritativeActionRecordMode.None)
        {
            if (!authority.Commit(view, state, value, recordMode)) return false;
            applied = view.BoundState.PhysicalState;
            RecordAcceptedPose(applied);
            view.RefreshAcceptedAppearance();
            return true;
        }

        private void ApplyHeldPhysics()
        {
            body.mass = profile.Mass * Tune.HeldMassMultiplier;
            body.linearDamping = Tune.HeldLinearDamping;
            body.angularDamping = Tune.HeldAngularDamping;
            body.maxDepenetrationVelocity = Tune.HeldMaxDepenetrationVelocity;
        }

        private void RestoreProfilePhysics()
        {
            body.mass = profile.Mass;
            body.linearDamping = profile.LinearDamping;
            body.angularDamping = profile.AngularDamping;
            body.maxDepenetrationVelocity = Physics.defaultMaxDepenetrationVelocity;
        }

        private void InitializePointerAnchor(Vector2 screenPosition)
        {
            Ray pointerRay = authority.Camera.ScreenPointToRay(screenPosition);
            Vector3 anchorWorld = body.position;
            Collider collider = PhysicalCollider;
            RaycastHit hit = default;
            bool hitObject = collider != null
                && collider.Raycast(pointerRay, out hit, authority.Camera.farClipPlane);
            if (hitObject)
            {
                anchorWorld = hit.point;
                grabLocalAnchor = transform.InverseTransformPoint(anchorWorld);
                grabScreenOffset = Vector2.zero;
            }
            else
            {
                grabLocalAnchor = Vector3.zero;
                Vector3 centerScreen = authority.Camera.WorldToScreenPoint(body.position);
                grabScreenOffset = new Vector2(
                    centerScreen.x - screenPosition.x,
                    centerScreen.y - screenPosition.y);
            }

            Vector3 projected = authority.Camera.WorldToScreenPoint(anchorWorld);
            holdDepth = Mathf.Max(authority.Camera.nearClipPlane + 0.01f, projected.z);
            lastAnchorHeight = anchorWorld.y;
            hasPointerAnchor = true;
        }

        /// <summary>Cursor ray intersected with a horizontal plane at the given world height.</summary>
        private Vector3 ProjectScreenToHeight(Vector2 screenPosition, float worldHeight)
        {
            UnityEngine.Camera camera = authority.Camera;
            Ray ray = camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, worldHeight, 0f));
            if (plane.Raycast(ray, out float enter) && enter > 0f && enter <= holdDepth * 4f)
                return ray.GetPoint(enter);
            // Near-horizon or parallel ray: fall back to the original camera-depth projection.
            return camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, holdDepth));
        }

        /// <summary>
        /// Support height under the target. Center uses a top-down ray; four inset footprint corners may raise the
        /// result so edges don't clip into shallow steps (e.g. a card hanging over a token). Taller things are
        /// left to real collisions, so held pieces shove tall objects instead of hopping onto them.
        /// </summary>
        private bool TryResolveSupportHeight(Vector3 target, out float height)
        {
            height = float.MinValue;
            Collider collider = PhysicalCollider;
            if (collider == null) return false;

            Bounds bounds = collider.bounds;
            Vector3 offset = bounds.center - body.position;
            offset.y = 0f;
            Vector3 center = target + offset;
            float originY = Mathf.Max(body.position.y, lastAnchorHeight) + Tune.SupportRayLift;

            if (!TryRaycastSupportDown(center.x, center.z, originY, out float centerHeight)) return false;

            float best = centerHeight;
            float halfX = bounds.extents.x * Tune.FootprintInset;
            float halfZ = bounds.extents.z * Tune.FootprintInset;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sz = (i & 2) == 0 ? -1f : 1f;
                if (TryRaycastSupportDown(center.x + sx * halfX, center.z + sz * halfZ, originY, out float cornerHeight)
                    && cornerHeight > best
                    && cornerHeight <= centerHeight + Tune.FootprintMaxStep)
                {
                    best = cornerHeight;
                }
            }

            height = best;
            return true;
        }

        private bool TryRaycastSupportDown(float x, float z, float originY, out float height)
        {
            height = float.MinValue;
            int hitCount = Physics.RaycastNonAlloc(
                new Vector3(x, originY, z),
                Vector3.down,
                supportHitBuffer,
                originY + Tune.SupportRayDepth,
                ~0,
                QueryTriggerInteraction.Ignore);
            bool found = false;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                if (!IsValidSupportHit(supportHitBuffer[i])) continue;
                if (!found || supportHitBuffer[i].distance < closestDistance)
                {
                    found = true;
                    closestDistance = supportHitBuffer[i].distance;
                    height = supportHitBuffer[i].point.y;
                }
            }

            return found;
        }

        private bool IsValidSupportHit(RaycastHit hit)
        {
            Collider candidate = hit.collider;
            return candidate != null
                && candidate.enabled
                && candidate.transform != transform
                && !candidate.transform.IsChildOf(transform)
                && Vector3.Dot(hit.normal, Vector3.up) > 0.2f
                && IsSupportCollider(candidate);
        }

        private bool IsSupportCollider(Collider candidate)
        {
            PhysicalTabletopSurface surface = candidate.GetComponent<PhysicalTabletopSurface>();
            if (surface != null)
            {
                return surface.TryGetCollider(out Collider authoredCollider, out _)
                    && ReferenceEquals(authoredCollider, candidate);
            }

            TabletopObjectView supportingView = candidate.GetComponentInParent<TabletopObjectView>();
            return supportingView != null && !ReferenceEquals(supportingView, view);
        }

        /// <summary>Advances the logical carry orientation (no lean). Physics chases it in FixedUpdate.</summary>
        private void UpdateHeldBaseRotation(float deltaTime)
        {
            if (!profile.NormalizeOrientationOnPickup
                || profile.OrientationRecoverySpeed <= 0f
                || Time.unscaledTime < orientationRecoveryResumeTime)
            {
                return;
            }

            float speed = profile.OrientationRecoverySpeed * Mathf.Lerp(
                1f,
                profile.GentlePlacementOrientationStrength,
                gentlePlacementIntent);
            heldBaseRotation = Quaternion.RotateTowards(
                heldBaseRotation,
                carryOrientationTarget,
                speed * Mathf.Max(0f, deltaTime));
        }

        private Quaternion ResolveCarryOrientation() =>
            Quaternion.AngleAxis(view.BoundState.Pose.RotationDegrees, Vector3.up)
            * profile.CarryLocalRotation;

        private float PickupProgress()
        {
            float duration = profile.PickupLiftDuration;
            return duration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.unscaledTime - pickupStartTime) / duration);
        }

        private float SmoothSupportHeight(float target, float deltaTime)
        {
            if (!hasSupportHeight)
            {
                hasSupportHeight = true;
                smoothedSupportHeight = target;
                return target;
            }

            smoothedSupportHeight = SmoothValue(
                smoothedSupportHeight,
                target,
                profile.SupportHeightResponseSeconds,
                deltaTime);
            return smoothedSupportHeight;
        }

        private static float SmoothValue(float current, float target, float responseSeconds, float deltaTime)
        {
            if (responseSeconds <= 0f || deltaTime <= 0f)
            {
                return target;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-deltaTime / responseSeconds));
        }

        private Vector3 ResolveDirectCarryPosition(Vector3 target, float deltaTime)
        {
            float smoothing = profile.HeldFollowSmoothingSeconds;
            if (smoothing <= 0f || deltaTime <= 0f)
            {
                return target;
            }

            return Vector3.Lerp(
                heldTargetPosition,
                target,
                1f - Mathf.Exp(-deltaTime / smoothing));
        }

        private static Vector3 AngularVelocityTo(Quaternion from, Quaternion to, float gain, float max)
        {
            Quaternion delta = to * Quaternion.Inverse(from);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x)) return Vector3.zero;
            return Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * gain), max);
        }

        /// <summary>
        /// Dice get a rolling spin (omega = v / r, scaled) plus randomness so a flick tumbles. Other pieces get a
        /// small yaw so a slid card doesn't stay perfectly aligned. Added AFTER profile clamps on purpose.
        /// </summary>
        private Vector3 SynthesizeReleaseSpin(Vector3 linear, Vector3 angular)
        {
            Vector3 planar = new Vector3(linear.x, 0f, linear.z);
            float speed = planar.magnitude;
            if (speed < Tune.SpinMinSpeed) return angular;

            if (view is DieView)
            {
                Vector3 direction = planar / speed;
                Bounds bounds = PhysicalCollider.bounds;
                float radius = Mathf.Max(0.05f, (bounds.extents.x + bounds.extents.y + bounds.extents.z) / 3f);
                float roll = speed / radius * Tune.DiceRollFactor;
                Vector3 spin = Vector3.Cross(Vector3.up, direction) * roll
                    + UnityEngine.Random.onUnitSphere * (roll * Tune.DiceSpinRandomness);
                return Vector3.ClampMagnitude(angular + spin, body.maxAngularVelocity);
            }

            if (gentlePlacementIntent < 0.5f && speed > 2f)
            {
                float yaw = Mathf.Min(speed * Tune.PieceYawSpinPerSpeed, Tune.PieceYawSpinMax)
                    * UnityEngine.Random.Range(-1f, 1f);
                return angular + Vector3.up * yaw;
            }

            return angular;
        }

        private float RootHeightAboveBottom()
        {
            Collider collider = PhysicalCollider;
            return collider != null
                ? Mathf.Max(0f, body.position.y - collider.bounds.min.y)
                : 0f;
        }

        private Vector3 GrabAnchorWorld() => body.position + RotatedGrabAnchor(body.rotation);

        private Vector3 RotatedGrabAnchor(Quaternion rotation) =>
            rotation * Vector3.Scale(grabLocalAnchor, transform.lossyScale);

        private PhysicalObjectState Capture(PhysicalObjectMode mode) =>
            State(body.position, body.rotation, body.linearVelocity, body.angularVelocity, mode, actor);
        public static PhysicalObjectState State(Vector3 p, Quaternion q, Vector3 v, Vector3 w, PhysicalObjectMode mode, PlayerId actor) =>
            new PhysicalObjectState(new PhysicalVector3(p.x, p.y, p.z), new PhysicalRotation(q.x, q.y, q.z, q.w),
                new PhysicalVector3(v.x, v.y, v.z), new PhysicalVector3(w.x, w.y, w.z), mode, actor);
        public static Vector3 Vector(PhysicalVector3 p) => new Vector3(p.X, p.Y, p.Z);
        public static Quaternion Rotation(PhysicalRotation q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
