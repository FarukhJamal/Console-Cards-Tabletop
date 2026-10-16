using System;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Composition
{
    public sealed class ConsoleSlotVisual : MonoBehaviour
    {
        [SerializeField] private ConsoleSlotView slotView;
        [SerializeField] private Transform layoutAnchor;
        [SerializeField] private Collider targetCollider;
        [SerializeField] private TabletopContainerDropTarget dropTarget;
        [SerializeField] private GameObject emptyStateRoot;
        [SerializeField] private GameObject validTargetFeedbackRoot;
        [SerializeField] private GameObject sourceTargetFeedbackRoot;
        [SerializeField] private GameObject invalidTargetFeedbackRoot;

        public ConsoleSlotView SlotView => slotView;

        public Transform LayoutAnchor => layoutAnchor;

        public Collider TargetCollider => targetCollider;

        public TabletopContainerDropTarget DropTarget => dropTarget;

        public void ValidateReferences()
        {
            RequireReference(slotView, nameof(slotView));
            RequireReference(layoutAnchor, nameof(layoutAnchor));
            RequireReference(targetCollider, nameof(targetCollider));
            RequireReference(dropTarget, nameof(dropTarget));
            RequireReference(emptyStateRoot, nameof(emptyStateRoot));
            RequireReference(validTargetFeedbackRoot, nameof(validTargetFeedbackRoot));
            RequireReference(sourceTargetFeedbackRoot, nameof(sourceTargetFeedbackRoot));
            RequireReference(invalidTargetFeedbackRoot, nameof(invalidTargetFeedbackRoot));

            if (!ReferenceEquals(slotView.gameObject, gameObject)
                || !ReferenceEquals(dropTarget.gameObject, gameObject))
            {
                throw new InvalidOperationException("ConsoleSlotVisual requires its View and drop target on the Slot root.");
            }

            if (!ReferenceEquals(slotView.LayoutAnchor, layoutAnchor))
            {
                throw new InvalidOperationException("ConsoleSlotVisual layout anchor must match ConsoleSlotView.LayoutAnchor.");
            }

            if (targetCollider.transform != transform && !targetCollider.transform.IsChildOf(transform))
            {
                throw new InvalidOperationException("ConsoleSlotVisual target Collider must belong to the Slot hierarchy.");
            }

            if (ReferenceEquals(validTargetFeedbackRoot, sourceTargetFeedbackRoot)
                || ReferenceEquals(validTargetFeedbackRoot, invalidTargetFeedbackRoot)
                || ReferenceEquals(sourceTargetFeedbackRoot, invalidTargetFeedbackRoot))
            {
                throw new InvalidOperationException("ConsoleSlotVisual feedback roots must be distinct.");
            }
        }

        public void ShowValidTarget()
        {
            SetFeedback(validTargetFeedbackRoot);
        }

        public void ShowSourceTarget()
        {
            SetFeedback(sourceTargetFeedbackRoot);
        }

        public void ShowInvalidTarget()
        {
            SetFeedback(invalidTargetFeedbackRoot);
        }

        public void SetEmptyStateHidden(bool hidden)
        {
            emptyStateRoot.SetActive(!hidden);
        }

        public void ClearFeedback()
        {
            SetFeedback(null);
        }

        private void SetFeedback(GameObject activeRoot)
        {
            validTargetFeedbackRoot.SetActive(ReferenceEquals(activeRoot, validTargetFeedbackRoot));
            sourceTargetFeedbackRoot.SetActive(ReferenceEquals(activeRoot, sourceTargetFeedbackRoot));
            invalidTargetFeedbackRoot.SetActive(ReferenceEquals(activeRoot, invalidTargetFeedbackRoot));
        }

        private static void RequireReference(UnityEngine.Object reference, string name)
        {
            if (reference == null)
            {
                throw new InvalidOperationException($"ConsoleSlotVisual requires {name}.");
            }
        }
    }
}
