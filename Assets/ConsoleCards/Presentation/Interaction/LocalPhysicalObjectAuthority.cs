using System;
using System.Collections.Generic;
using ConsoleCards.Application.Authority;
using ConsoleCards.Application.Commands;
using ConsoleCards.Application.Results;
using ConsoleCards.Application.UseCases;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Identifiers;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>The one local/offline simulation authority. Future clients must not instantiate this authority adapter.</summary>
    public sealed class LocalPhysicalObjectAuthority
    {
        private readonly MatchState match;
        private readonly Func<PlayerId> actor;
        private readonly Action<Transform> stopAnimation;
        private readonly ITableAuthority tableAuthority; // physics commits go through the command door (R1b)
        private readonly List<PhysicalLooseObject> objects = new List<PhysicalLooseObject>();
        private readonly Dictionary<PhysicalLooseObject, bool> contained = new Dictionary<PhysicalLooseObject, bool>();
        public LocalPhysicalObjectAuthority(MatchState match, IReadOnlyList<PlayerId> actors, Func<PlayerId> actor,
            UnityEngine.Camera camera, PhysicalTabletopSurfaces surfaces, Action<Transform> stopAnimation,
            ITableAuthority tableAuthority, PhysicalInteractionConfig interactionConfig = null)
        {
            this.match = match; this.actor = actor; // actors: the door's handler checks the active players
            this.tableAuthority = tableAuthority ?? throw new ArgumentNullException(nameof(tableAuthority));
            Camera = camera; Surfaces = surfaces; this.stopAnimation = stopAnimation;
            InteractionConfig = interactionConfig ?? new PhysicalInteractionConfig();
        }
        public UnityEngine.Camera Camera { get; }
        public PhysicalTabletopSurfaces Surfaces { get; }
        public PhysicalInteractionConfig InteractionConfig { get; }
        public PlayerId Actor => actor();

        /// <summary>
        /// MP-2: who plays on this computer (null: everyone, offline). A piece is simulated and committed only by the
        /// computer of the player who controls it; the others show its accepted states and the poses they are sent.
        /// </summary>
        public Func<PlayerId, bool> IsLocalPlayer { get; set; }

        /// <summary>MP-2: false on a joined computer: a new piece's first resting place is the host's to commit.</summary>
        public Func<bool> MayCommitInitialState { get; set; }

        /// <summary>
        /// FAIR-DICE (owner, 2026-11-06: "the host player's rule-dependent dice must follow the same result-generation,
        /// roll-locking and reroll rules as guests"): on the host, a settle of a die a rule reads is given the same host
        /// result a guest's throw gets, before it is run. Null (or a null result): the commit runs as it is.
        /// </summary>
        public Func<CommitPhysicalObjectCommand, CommitPhysicalObjectCommand> RulesDieResult { get; set; }

        internal bool MayCommitFirstState => MayCommitInitialState == null || MayCommitInitialState();

        /// <summary>MP-2: true when another computer's player holds the piece (it cannot be taken from them).</summary>
        internal bool IsHeldElsewhere(PhysicalObjectState state) =>
            state != null && state.Mode == PhysicalObjectMode.Held && IsLocalPlayer != null && !IsLocalPlayer(state.ControllingPlayerId);

        /// <summary>MP-2: the pieces held on this computer and where they are (no allocation). Returns how many.</summary>
        public int CopyHeldPoses(TabletopObjectId[] ids, Vector3[] positions, Quaternion[] rotations)
        {
            int count = 0;
            for (int i = 0; i < objects.Count && count < ids.Length; i++)
            {
                PhysicalLooseObject physical = objects[i];
                if (physical == null || !physical.IsHeld || !physical.OwnsLooseTransform || physical.Body == null) continue;
                if (IsLocalPlayer != null && !IsLocalPlayer(physical.Controller)) continue;
                ids[count] = physical.View.ObjectId;
                positions[count] = physical.Body.position;
                rotations[count] = physical.Body.rotation;
                count++;
            }

            return count;
        }

        /// <summary>MP-2: a piece another computer's player is carrying moves towards where they hold it.</summary>
        public bool FollowRemote(TabletopObjectId id, Vector3 position, Quaternion rotation)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                PhysicalLooseObject physical = objects[i];
                if (physical == null || physical.View == null || !physical.View.IsBound || !physical.View.ObjectId.Equals(id)) continue;
                if (IsLocalPlayer == null || IsLocalPlayer(physical.Controller)) return false;
                return physical.FollowRemote(position, rotation);
            }

            return false;
        }
        public void StopAnimation(Transform transform) => stopAnimation(transform);
        public void Register(TabletopObjectView view)
        {
            if (view == null || !view.IsBound || view.PhysicalObject != null) return;
            if (view is DieView die && !die.HasPhysicalDefinition)
                throw new InvalidOperationException("Physical Dice require an authored shape and face/value definition.");
            PhysicalLooseObject physical = view.gameObject.GetComponent<PhysicalLooseObject>();
            if (physical == null) physical = view.gameObject.AddComponent<PhysicalLooseObject>();
            view.PhysicalObject = physical;
            objects.Add(physical);
            physical.Initialize(view, this);
            SetContainedCollisions(physical, !view.BoundState.ContainerId.IsEmpty);
        }
        public void Tick()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] == null) { contained.Remove(objects[i]); carrying.Remove(objects[i]); objects.RemoveAt(i); continue; }
                objects[i].Tick();
            }
        }
        internal bool Commit(
            TabletopObjectView view,
            PhysicalObjectState state,
            int? value,
            AuthoritativeActionRecordMode recordMode)
        {
            if (IsLocalPlayer != null && !IsLocalPlayer(state.ControllingPlayerId)) return false; // MP-2: its own computer commits it
            // PRIVACY-P4: a door that takes no physics commits (the private view's) is not asked every frame.
            if (!tableAuthority.CanSubmit<CommitPhysicalObjectCommand>()) return false;
            if (!view.IsBound || !match.ContainsObject(view.ObjectId)
                || !ReferenceEquals(view.BoundState, match.GetObject(view.ObjectId))) return false;
            CommitPhysicalObjectCommand command = new CommitPhysicalObjectCommand(
                new CommandContext(CommandId.New(), match.Id, state.ControllingPlayerId, match.Revision),
                view.ObjectId, state, view.BoundState.PhysicalRevision, value, recordMode);
            if (value.HasValue && RulesDieResult != null) command = RulesDieResult(command) ?? command; // FAIR-DICE
            return tableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(command).Succeeded;
        }

        internal bool Commit(TabletopObjectView view, PhysicalObjectState state, int? value)
        {
            return Commit(view, state, value, AuthoritativeActionRecordMode.None);
        }
        // B07 (owner, 2026-11-05, decision 2): a piece being carried passes over pieces lying at rest instead of
        // shoving them. Contacts come back when it is let go, so a thrown die still knocks into things. Physics is
        // never switched off globally. Set CarriedPiecesShove for the old physical shove (an option for later).
        private readonly HashSet<PhysicalLooseObject> carrying = new HashSet<PhysicalLooseObject>();

        public bool CarriedPiecesShove { get; set; }

        internal void SetCarryCollisions(PhysicalLooseObject target, bool carried)
        {
            if (target == null) return;
            if (carried)
            {
                if (CarriedPiecesShove || !carrying.Add(target)) return;
            }
            else if (!carrying.Remove(target))
            {
                return;
            }

            Collider collider = target.PhysicalCollider;
            if (collider == null) return;
            bool targetContained = contained.TryGetValue(target, out bool tc) && tc;
            foreach (PhysicalLooseObject other in objects)
            {
                if (other == null || other == target || other.PhysicalCollider == null) continue;
                if (carried && !other.IsAtRest) continue; // only pieces lying still are passed over
                bool otherContained = contained.TryGetValue(other, out bool oc) && oc;
                Physics.IgnoreCollision(collider, other.PhysicalCollider, carried || targetContained || otherContained);
            }
        }

        internal void SetContainedCollisions(PhysicalLooseObject target, bool isContained)
        {
            if (contained.TryGetValue(target, out bool previous) && previous == isContained) return;
            contained[target] = isContained;
            Collider collider = target.PhysicalCollider;
            foreach (PhysicalLooseObject other in objects)
            {
                if (other == null || other == target) continue;
                bool otherContained = contained.TryGetValue(other, out bool value) && value;
                if (collider != null && other.PhysicalCollider != null)
                    Physics.IgnoreCollision(collider, other.PhysicalCollider, isContained || otherContained);
            }
        }
        public void Shutdown()
        {
            foreach (PhysicalLooseObject physical in objects)
                if (physical != null && physical.Body != null)
                { physical.Body.isKinematic = true; physical.Body.useGravity = false; }
            objects.Clear(); contained.Clear();
        }
    }
}
