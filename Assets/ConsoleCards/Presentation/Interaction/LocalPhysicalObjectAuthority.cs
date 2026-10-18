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
                if (objects[i] == null) { contained.Remove(objects[i]); objects.RemoveAt(i); continue; }
                objects[i].Tick();
            }
        }
        internal bool Commit(
            TabletopObjectView view,
            PhysicalObjectState state,
            int? value,
            AuthoritativeActionRecordMode recordMode)
        {
            if (!view.IsBound || !match.ContainsObject(view.ObjectId)
                || !ReferenceEquals(view.BoundState, match.GetObject(view.ObjectId))) return false;
            return tableAuthority.Submit<CommitPhysicalObjectCommand, CommandResult>(new CommitPhysicalObjectCommand(
                new CommandContext(CommandId.New(), match.Id, state.ControllingPlayerId, match.Revision),
                view.ObjectId, state, view.BoundState.PhysicalRevision, value, recordMode)).Succeeded;
        }

        internal bool Commit(TabletopObjectView view, PhysicalObjectState state, int? value)
        {
            return Commit(view, state, value, AuthoritativeActionRecordMode.None);
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
