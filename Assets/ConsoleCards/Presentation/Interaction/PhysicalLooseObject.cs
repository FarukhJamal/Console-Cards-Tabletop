using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>Shared Rigidbody adapter. The injected local authority owns lifecycle/settlement; no per-object Update.</summary>
    public sealed class PhysicalLooseObject : MonoBehaviour
    {
        private const int SupportHitBufferCapacity = 32;

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
            containedPickupLift = 0f;
            gentlePlacementIntent = 0f;
            orientationRecoveryResumeTime = 0f;
            deliberateRotationPending = false;
            if (PhysicalCollider == null) throw new InvalidOperationException("Loose physics requires an enabled collider on the wrapper or its visual child.");
            physicalCollider = PhysicalCollider;
            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.mass = profile.Mass;
            body.linearDamping = profile.LinearDamping;
            body.angularDamping = profile.AngularDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.maxAngularVelocity = 35f;
            actor = authority.Actor;
            Synchronize();
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
                carryOrientationTarget = ResolveCarryOrientation();
            transform.SetPositionAndRotation(body.position, body.rotation);
            held = stateHeld;
            body.isKinematic = held || view.BoundState.IsUserLocked;
            body.useGravity = !body.isKinematic;
            body.detectCollisions = !held;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector(state.Velocity);
                body.angularVelocity = Vector(state.AngularVelocity);
                if (state.Mode == PhysicalObjectMode.Sleeping || state.Mode == PhysicalObjectMode.SleepingUnresolved)
                    body.Sleep();
                else body.WakeUp();
            }
            dynamicFrames = 0;
        }

        public void DisableForContainer()
        {
            if (body == null) return;
            held = false;
            hasPointerAnchor = false;
            hasSupportHeight = false;
            containedPickupLift = 0f;
            gentlePlacementIntent = 0f;
            deliberateRotationPending = false;
            body.isKinematic = true;
            body.useGravity = false;
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
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            authority.SetContainedCollisions(this, false);
            authority.StopAnimation(transform);
            pickupPosition = body.position;
            pickupRotation = body.rotation;
            carryOrientationTarget = ResolveCarryOrientation();
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

        public void Follow(Vector2 screenPosition)
        {
            if (!held && !BeginHold()) return;
            if (!hasPointerAnchor) InitializePointerAnchor(screenPosition);

            float deltaTime = Time.unscaledDeltaTime;
            Quaternion nextRotation = ResolveHeldRotation(deltaTime);
            body.rotation = nextRotation;
            transform.rotation = nextRotation;

            Vector2 anchoredScreenPosition = screenPosition + grabScreenOffset;
            Vector3 targetAnchor = authority.Camera.ScreenToWorldPoint(
                new Vector3(anchoredScreenPosition.x, anchoredScreenPosition.y, holdDepth));
            Vector3 target = targetAnchor - RotatedGrabAnchor(nextRotation);

            Vector3 sampledAngularVelocity = releaseMotion.GetAngularRelease(Time.unscaledTime);
            Vector3 sampledLinearVelocity = releaseMotion.GetLinearRelease(Time.unscaledTime);
            gentlePlacementIntent = profile.ResolveGentlePlacementIntent(
                sampledLinearVelocity,
                sampledAngularVelocity);

            Vector3 targetScreenPoint = authority.Camera.WorldToScreenPoint(target);
            bool hasSupport = TryResolveSupportHeight(
                new Vector2(targetScreenPoint.x, targetScreenPoint.y),
                out float supportHeight);
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
            next = ResolveCollisionAwareCarryPosition(body.position, next);
            body.position = next;
            transform.SetPositionAndRotation(next, nextRotation);
            releaseMotion.SampleLinear(
                next + RotatedGrabAnchor(nextRotation),
                Time.unscaledTime);
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
            carryOrientationTarget = rotation;
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
            return State(transform.position, transform.rotation, velocity,
                angularVelocity, PhysicalObjectMode.Dynamic, actor);
        }

        public bool Release()
        {
            if (!OwnsLooseTransform) return false;
            if (!Commit(ReleaseState(), null, AuthoritativeActionRecordMode.Intermediate)) { Cancel(); return false; }
            held = false;
            hasPointerAnchor = false;
            hasSupportHeight = false;
            containedPickupLift = 0f;
            gentlePlacementIntent = 0f;
            deliberateRotationPending = false;
            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            body.linearVelocity = Vector(applied.Velocity);
            body.angularVelocity = Vector(applied.AngularVelocity);
            body.WakeUp();
            dynamicFrames = 0;
            return true;
        }

        public void CaptureBeforeRotation()
        {
            if (held)
            {
                orientationRecoveryResumeTime = Time.unscaledTime + profile.OrientationRecoveryInputPauseSeconds;
                deliberateRotationPending = true;
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
            containedPickupLift = 0f;
            gentlePlacementIntent = 0f;
            deliberateRotationPending = false;
            userActionActive = false;
            compoundActionActive = false;
            applied = null;
            Synchronize();
        }

        public bool Roll(PlayerId? requestingActor = null, bool partOfCompoundAction = false)
        {
            if (!(view is DieView) || !OwnsLooseTransform || view.BoundState.IsUserLocked || held) return false;
            actor = requestingActor ?? authority.Actor;
            userActionActive = !partOfCompoundAction;
            compoundActionActive = partOfCompoundAction;
            PhysicalObjectState launch = State(transform.position + Vector3.up * 0.8f, transform.rotation,
                new Vector3(UnityEngine.Random.Range(-1.8f, 1.8f), 4f, UnityEngine.Random.Range(-1.8f, 1.8f)),
                UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(12f, 25f), PhysicalObjectMode.Dynamic, actor);
            if (!Commit(launch, null, AuthoritativeActionRecordMode.Intermediate))
            {
                userActionActive = false;
                compoundActionActive = false;
                return false;
            }
            applied = null;
            ApplyAccepted();
            return true;
        }

        internal void Tick()
        {
            if (view == null || !view.IsBound || !gameObject.activeInHierarchy) return;
            Synchronize();
            if (!OwnsLooseTransform || held || body.isKinematic) return;
            dynamicFrames++;
            if (body.IsSleeping() && dynamicFrames > 2)
            {
                if (applied != null && (applied.Mode == PhysicalObjectMode.Sleeping
                    || applied.Mode == PhysicalObjectMode.SleepingUnresolved)) return;
                int? value = null;
                if (view is DieView die)
                {
                    if (!die.TryResolvePhysicalValue(out int face))
                    {
                        Commit(State(body.position, body.rotation, Vector3.zero, Vector3.zero,
                            PhysicalObjectMode.SleepingUnresolved, actor), null,
                            userActionActive
                                ? AuthoritativeActionRecordMode.Transaction
                                : compoundActionActive
                                    ? AuthoritativeActionRecordMode.Intermediate
                                    : AuthoritativeActionRecordMode.None);
                        userActionActive = false;
                        compoundActionActive = false;
                        return; // Cocked: retain the prior result, but record the actual resting pose and unresolved status.
                    }
                    value = face;
                }
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
            view.RefreshAcceptedAppearance();
            return true;
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
            hasPointerAnchor = true;
        }

        private bool TryResolveSupportHeight(Vector2 screenPosition, out float height)
        {
            Physics.SyncTransforms();
            Ray ray = authority.Camera.ScreenPointToRay(screenPosition);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                supportHitBuffer,
                authority.Camera.farClipPlane,
                ~0,
                QueryTriggerInteraction.Ignore);
            bool found = false;
            height = float.MinValue;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = supportHitBuffer[i].collider;
                if (candidate == null
                    || !candidate.enabled
                    || candidate.transform == transform
                    || candidate.transform.IsChildOf(transform)
                    || Vector3.Dot(supportHitBuffer[i].normal, Vector3.up) <= 0.2f
                    || !IsSupportCollider(candidate))
                {
                    continue;
                }

                float candidateDistance = supportHitBuffer[i].distance;
                if (!found || candidateDistance < closestDistance)
                {
                    found = true;
                    closestDistance = candidateDistance;
                    height = supportHitBuffer[i].point.y;
                }
            }

            return found;
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

        private Quaternion ResolveHeldRotation(float deltaTime)
        {
            if (!profile.NormalizeOrientationOnPickup
                || profile.OrientationRecoverySpeed <= 0f
                || Time.unscaledTime < orientationRecoveryResumeTime)
            {
                return body.rotation;
            }

            float speed = profile.OrientationRecoverySpeed * Mathf.Lerp(
                1f,
                profile.GentlePlacementOrientationStrength,
                gentlePlacementIntent);
            return Quaternion.RotateTowards(
                body.rotation,
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
                body.position,
                target,
                1f - Mathf.Exp(-deltaTime / smoothing));
        }

        private Vector3 ResolveCollisionAwareCarryPosition(Vector3 current, Vector3 proposed)
        {
            Vector3 translation = proposed - current;
            Vector3 horizontal = new Vector3(translation.x, 0f, translation.z);
            float distance = horizontal.magnitude;
            Collider collider = PhysicalCollider;
            if (distance <= 0.0001f || collider == null)
            {
                return proposed;
            }

            Physics.SyncTransforms();
            Bounds bounds = collider.bounds;
            float skin = profile.HeldCollisionSkin;
            Vector3 extents = new Vector3(
                Mathf.Max(0.001f, bounds.extents.x - skin),
                Mathf.Max(0.001f, bounds.extents.y - skin),
                Mathf.Max(0.001f, bounds.extents.z - skin));
            Vector3 direction = horizontal / distance;
            int hitCount = Physics.BoxCastNonAlloc(
                bounds.center,
                extents,
                direction,
                supportHitBuffer,
                Quaternion.identity,
                distance + skin,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearestObstacle = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Collider candidate = supportHitBuffer[i].collider;
                if (!IsHeldObstacle(candidate))
                {
                    continue;
                }

                nearestObstacle = Mathf.Min(nearestObstacle, supportHitBuffer[i].distance);
            }

            if (nearestObstacle == float.MaxValue)
            {
                return proposed;
            }

            float safeDistance = Mathf.Clamp(nearestObstacle - skin, 0f, distance);
            float clearFraction = distance > 0f ? safeDistance / distance : 0f;
            float resistedDistance = safeDistance * Mathf.Lerp(
                profile.CollisionCarryScale,
                1f,
                clearFraction);
            Vector3 resisted = current + (direction * resistedDistance);
            resisted.y = proposed.y;
            return resisted;
        }

        private bool IsHeldObstacle(Collider candidate)
        {
            if (candidate == null
                || !candidate.enabled
                || candidate.isTrigger
                || candidate.transform == transform
                || candidate.transform.IsChildOf(transform)
                || candidate.GetComponentInParent<PhysicalTabletopSurface>() != null)
            {
                return false;
            }

            TabletopObjectView otherView = candidate.GetComponentInParent<TabletopObjectView>();
            return otherView != null && !ReferenceEquals(otherView, view);
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
