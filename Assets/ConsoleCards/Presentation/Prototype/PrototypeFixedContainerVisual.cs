using System;
using ConsoleCards.Presentation.Interaction;
using ConsoleCards.Presentation.Views.Containers;
using UnityEngine;

namespace ConsoleCards.Presentation.Prototype
{
    public sealed class PrototypeFixedContainerVisual : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour containerView;
        [SerializeField] private Transform layoutAnchor;
        [SerializeField] private Collider targetCollider;
        [SerializeField] private TabletopContainerDropTarget dropTarget;
        [SerializeField] private TextMesh label;
        [SerializeField] private Renderer feedbackRenderer;
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material validTargetMaterial;
        [SerializeField] private Material sourceTargetMaterial;
        [SerializeField] private Material invalidTargetMaterial;

        public IContainerView ContainerView => containerView as IContainerView;

        public Transform LayoutAnchor => layoutAnchor;

        public Collider TargetCollider => targetCollider;

        public TabletopContainerDropTarget DropTarget => dropTarget;

        public TextMesh Label => label;

        public TView GetView<TView>() where TView : MonoBehaviour, IContainerView
        {
            TView resolvedView = containerView as TView;
            if (resolvedView == null)
            {
                throw new InvalidOperationException(
                    $"{name} requires a {typeof(TView).Name} reference.");
            }

            return resolvedView;
        }

        public void ValidateReferences()
        {
            RequireReference(containerView, nameof(containerView));
            RequireReference(layoutAnchor, nameof(layoutAnchor));
            RequireReference(targetCollider, nameof(targetCollider));
            RequireReference(dropTarget, nameof(dropTarget));
            RequireReference(label, nameof(label));
            RequireReference(feedbackRenderer, nameof(feedbackRenderer));
            RequireReference(baseMaterial, nameof(baseMaterial));
            RequireReference(validTargetMaterial, nameof(validTargetMaterial));
            RequireReference(sourceTargetMaterial, nameof(sourceTargetMaterial));
            RequireReference(invalidTargetMaterial, nameof(invalidTargetMaterial));

            if (!(containerView is IContainerView))
            {
                throw new InvalidOperationException(
                    "PrototypeFixedContainerVisual requires an IContainerView component.");
            }

            if (!ReferenceEquals(containerView.gameObject, gameObject)
                || !ReferenceEquals(dropTarget.gameObject, gameObject))
            {
                throw new InvalidOperationException(
                    "PrototypeFixedContainerVisual requires its View and drop target on the container root.");
            }

            ValidateHierarchyReference(layoutAnchor, nameof(layoutAnchor));
            ValidateHierarchyReference(targetCollider.transform, nameof(targetCollider));
            ValidateHierarchyReference(label.transform, nameof(label));
            ValidateHierarchyReference(feedbackRenderer.transform, nameof(feedbackRenderer));
        }

        public void Reactivate()
        {
            gameObject.SetActive(true);
            targetCollider.enabled = true;
            dropTarget.enabled = true;
            ClearFeedback();
        }

        public void ShowValidTarget()
        {
            SetFeedbackMaterial(validTargetMaterial);
        }

        public void ShowSourceTarget()
        {
            SetFeedbackMaterial(sourceTargetMaterial);
        }

        public void ShowInvalidTarget()
        {
            SetFeedbackMaterial(invalidTargetMaterial);
        }

        public void ClearFeedback()
        {
            SetFeedbackMaterial(baseMaterial);
        }

        private void SetFeedbackMaterial(Material material)
        {
            feedbackRenderer.sharedMaterial = material;
        }

        private void ValidateHierarchyReference(Transform referencedTransform, string referenceName)
        {
            if (referencedTransform != transform && !referencedTransform.IsChildOf(transform))
            {
                throw new InvalidOperationException(
                    $"PrototypeFixedContainerVisual {referenceName} must belong to the container hierarchy.");
            }
        }

        private static void RequireReference(UnityEngine.Object reference, string name)
        {
            if (reference == null)
            {
                throw new InvalidOperationException($"PrototypeFixedContainerVisual requires {name}.");
            }
        }
    }
}
