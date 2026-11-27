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
        // PINK-1 (owner, 2026-11-26): the slot's "source" look renders as a solid magenta block (see SLOT-HINT in TabletopComposition.Consoles.cs).
        // Off until the source feedback material in the Console slot prefab is fixed; then tick this.
        [SerializeField] private bool showSourceLook;
        // PINK-2 (owner, 2026-11-27): root cause: the prefab's SourceTargetFeedbackRoot uses SelectionHighlight.mat, whose
        // colour IS magenta (0.95, 0.08, 0.90), not a broken shader. The source look is now drawn in this colour through a
        // property block (no prefab edit, no new material); the valid (green) and refused (red) looks are unchanged.
        [SerializeField] private Color sourceLookColour = new Color(0.95f, 0.72f, 0.25f, 1f);
        private bool sourceLookTinted;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

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
            // PINK-1: lifting a card out of a Console slot (and hovering back over it) made the slot turn pink.
            SetFeedback(showSourceLook || TintSourceLook() ? sourceTargetFeedbackRoot : null);
        }

        // PINK-2: once per slot (not per frame): every renderer of the source look takes sourceLookColour.
        private bool TintSourceLook()
        {
            if (sourceLookTinted) return true;
            if (sourceTargetFeedbackRoot == null) return false;
            Renderer[] renderers = sourceTargetFeedbackRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, sourceLookColour);
            block.SetColor(ColorId, sourceLookColour);
            for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(block);
            sourceLookTinted = true;
            return true;
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
            if (ConsoleCards.Presentation.UI.TableUiController.IsQuitting) return; // the scene is being destroyed
            SetFeedback(null);
        }

        private void SetFeedback(GameObject activeRoot)
        {
            // A part already destroyed (the table closing) is skipped.
            if (validTargetFeedbackRoot != null) validTargetFeedbackRoot.SetActive(ReferenceEquals(activeRoot, validTargetFeedbackRoot));
            if (sourceTargetFeedbackRoot != null) sourceTargetFeedbackRoot.SetActive(ReferenceEquals(activeRoot, sourceTargetFeedbackRoot));
            if (invalidTargetFeedbackRoot != null) invalidTargetFeedbackRoot.SetActive(ReferenceEquals(activeRoot, invalidTargetFeedbackRoot));
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
