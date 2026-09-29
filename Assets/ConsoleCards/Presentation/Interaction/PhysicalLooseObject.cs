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
        private bool hasPointerAnchor;
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
            applied = state;
            actor = state.ControllingPlayerId;
            body.position = Vector(state.Position);
            body.rotation = Rotation(state.Rotation);
            transform.SetPositionAndRotation(body.position, body.rotation);
            held = state.Mode == PhysicalObjectMode.Held;
            body.isKinematic = held || view.BoundState.IsUserLocked;
            body.useGravity = !body.isKinematic;
            body.detectCollisions = true;
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
            authority.SetContainedCollisions(this, false);
            authority.StopAnimation(transform);
            hasPointerAnchor = false;
            releaseMotion.Reset();
            return true;
        }

        public void Follow(Vector2 screenPosition)
        {
            if (!held && !BeginHold()) return;
            if (!hasPointerAnchor) InitializePointerAnchor(screenPosition);

            Vector2 anchoredScreenPosition = screenPosition + grabScreenOffset;
            Vector3 target = authority.Camera.ScreenToWorldPoint(
                new Vector3(anchoredScreenPosition.x, anchoredScreenPosition.y, holdDepth));
            if (TryResolveSupportHeight(anchoredScreenPosition, out float supportHeight))
                target.y = supportHeight + HeldHalfHeight() + profile.HeldClearance;

            float smoothing = profile.HeldFollowSmoothingSeconds;
            float blend = smoothing <= 0f || Time.unscaledDeltaTime <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothing);
            Vector3 next = Vector3.Lerp(body.position, target, blend);
            body.position = next;
            transform.position = next;

            // Clearance/lift is Presentation assistance, not throw intent. Sample horizontal gesture motion.
            releaseMotion.Sample(
                new Vector3(target.x, 0f, target.z),
                transform.rotation,
                Time.unscaledTime);
        }

        internal bool BeginContainedPickup(float lift)
        {
            if (OwnsLooseTransform || !BeginHold()) return false;
            Vector3 target = transform.position + (Vector3.up * Mathf.Max(0f, lift));
            body.position = target;
            transform.position = target;
            hasPointerAnchor = false;
            releaseMotion.Reset();
            return true;
        }

        internal bool SnapHeldPreview(Vector3 position, Quaternion rotation)
        {
            if (!held && !BeginHold()) return false;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            releaseMotion.Reset();
            return true;
        }

        public PhysicalObjectState ReleaseState()
        {
            releaseMotion.GetRelease(Time.unscaledTime, out Vector3 velocity, out Vector3 angularVelocity);
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
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector(applied.Velocity);
            body.angularVelocity = Vector(applied.AngularVelocity);
            body.WakeUp();
            dynamicFrames = 0;
            return true;
        }

        public void CaptureBeforeRotation()
        {
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
            Vector3 projected = authority.Camera.WorldToScreenPoint(body.position);
            holdDepth = Mathf.Max(authority.Camera.nearClipPlane + 0.01f, projected.z);
            grabScreenOffset = new Vector2(projected.x - screenPosition.x, projected.y - screenPosition.y);
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
                    || candidate.transform.IsChildOf(transform))
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

        private float HeldHalfHeight()
        {
            Collider collider = PhysicalCollider;
            return collider != null ? Mathf.Max(0f, collider.bounds.extents.y) : 0f;
        }

        private PhysicalObjectState Capture(PhysicalObjectMode mode) =>
            State(body.position, body.rotation, body.linearVelocity, body.angularVelocity, mode, actor);
        public static PhysicalObjectState State(Vector3 p, Quaternion q, Vector3 v, Vector3 w, PhysicalObjectMode mode, PlayerId actor) =>
            new PhysicalObjectState(new PhysicalVector3(p.x, p.y, p.z), new PhysicalRotation(q.x, q.y, q.z, q.w),
                new PhysicalVector3(v.x, v.y, v.z), new PhysicalVector3(w.x, w.y, w.z), mode, actor);
        public static Vector3 Vector(PhysicalVector3 p) => new Vector3(p.X, p.Y, p.Z);
        public static Quaternion Rotation(PhysicalRotation q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
