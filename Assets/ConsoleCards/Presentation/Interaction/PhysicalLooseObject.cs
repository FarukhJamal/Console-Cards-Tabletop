using System;
using ConsoleCards.Core.Coordinates;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>Shared Rigidbody adapter. The injected local authority owns lifecycle/settlement; no per-object Update.</summary>
    public sealed class PhysicalLooseObject : MonoBehaviour
    {
        private TabletopObjectView view;
        private Rigidbody body;
        private Collider physicalCollider;
        private LocalPhysicalObjectAuthority authority;
        private PhysicalObjectState applied;
        private bool held;
        private float holdDepth;
        private PhysicalReleaseMotion releaseMotion;
        private float nextCheckpoint;
        private int dynamicFrames;
        private PlayerId actor;
        private PhysicalObjectState grabOrigin;
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
                return null;
            }
        }

        internal void Initialize(TabletopObjectView view, LocalPhysicalObjectAuthority authority)
        {
            this.view = view; this.authority = authority;
            releaseMotion = new PhysicalReleaseMotion(authority.InteractionConfig);
            held = false;
            applied = null;
            grabOrigin = null;
            if (PhysicalCollider == null) throw new InvalidOperationException("Loose physics requires the existing root collider.");
            physicalCollider = PhysicalCollider;
            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.mass = view is CardView ? 0.02f : 0.1f;
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
            if (OwnsLooseTransform && !Commit(Capture(PhysicalObjectMode.Held))) return false;
            held = true;
            body.isKinematic = true;
            body.useGravity = false;
            authority.SetContainedCollisions(this, false);
            authority.StopAnimation(transform);
            Vector3 lifted = transform.position + Vector3.up * 0.8f;
            holdDepth = authority.Camera.WorldToScreenPoint(lifted).z;
            releaseMotion.Reset();
            return true;
        }

        public void Follow(Vector2 screenPosition)
        {
            if (!held && !BeginHold()) return;
            Vector3 target = authority.Camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, holdDepth));
            // Sample pointer motion before automatic surface/obstacle clearance changes the held height.
            releaseMotion.Sample(target, transform.rotation, Time.unscaledTime);
            if (authority.Surfaces.TryPointer(screenPosition, out RaycastHit hit))
                target.y = Mathf.Max(target.y, hit.point.y + 0.8f);
            Ray ray = authority.Camera.ScreenPointToRay(screenPosition);
            foreach (RaycastHit obstacle in Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Ignore))
                if (obstacle.rigidbody != body && obstacle.collider != null)
                    target.y = Mathf.Max(target.y, obstacle.point.y + 0.8f);
            body.position = target;
            transform.position = target;
        }

        public PhysicalObjectState ReleaseState()
        {
            releaseMotion.GetRelease(Time.unscaledTime, out Vector3 velocity, out Vector3 angularVelocity);
            return State(transform.position, transform.rotation, velocity,
                angularVelocity, PhysicalObjectMode.Dynamic, actor);
        }

        public bool Release()
        {
            if (!OwnsLooseTransform) return false;
            if (!Commit(ReleaseState())) { Cancel(); return false; }
            held = false;
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
            if (OwnsLooseTransform) Commit(Capture(held ? PhysicalObjectMode.Held : PhysicalObjectMode.Dynamic));
        }

        public void Cancel()
        {
            if (held && OwnsLooseTransform && grabOrigin != null)
                Commit(State(Vector(grabOrigin.Position), Rotation(grabOrigin.Rotation), Vector(grabOrigin.Velocity),
                    Vector(grabOrigin.AngularVelocity), PhysicalObjectMode.Dynamic, actor));
            held = false;
            applied = null;
            Synchronize();
        }

        public bool Roll(PlayerId? requestingActor = null)
        {
            if (!(view is DieView) || !OwnsLooseTransform || view.BoundState.IsUserLocked || held) return false;
            actor = requestingActor ?? authority.Actor;
            PhysicalObjectState launch = State(transform.position + Vector3.up * 0.8f, transform.rotation,
                new Vector3(UnityEngine.Random.Range(-1.8f, 1.8f), 4f, UnityEngine.Random.Range(-1.8f, 1.8f)),
                UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(12f, 25f), PhysicalObjectMode.Dynamic, actor);
            if (!Commit(launch)) return false;
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
                            PhysicalObjectMode.SleepingUnresolved, actor));
                        return; // Cocked: retain the prior result, but record the actual resting pose and unresolved status.
                    }
                    value = face;
                }
                Commit(State(body.position, body.rotation, Vector3.zero, Vector3.zero,
                    PhysicalObjectMode.Sleeping, actor), value);
            }
            else if (Time.unscaledTime >= nextCheckpoint)
            {
                nextCheckpoint = Time.unscaledTime + 0.25f;
                Commit(Capture(PhysicalObjectMode.Dynamic)); // Includes continuing off-table falls.
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
                if (!Commit(initial)) return;
                applied = null;
            }
            ApplyAccepted();
        }

        private bool Commit(PhysicalObjectState state, int? value = null)
        {
            if (!authority.Commit(view, state, value)) return false;
            applied = view.BoundState.PhysicalState;
            view.RefreshAcceptedAppearance();
            return true;
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
