using System;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Prototype
{
    /// <summary>
    /// Provides the explicit authored presentation references owned by PrototypeCard.
    /// </summary>
    public sealed class PrototypeCardVisualReferences : MonoBehaviour
    {
        private const float FaceLabelSurfaceEpsilon = 0.0001f;

        [SerializeField] private CardView cardView;
        [SerializeField] private TabletopSelectionVisual selectionVisual;
        [SerializeField] private Renderer faceUpRenderer;
        [SerializeField] private Renderer faceDownRenderer;
        [SerializeField] private TextMesh frontLabel;
        [SerializeField] private TextMesh backLabel;

        public CardView CardView => cardView;

        public TabletopSelectionVisual SelectionVisual => selectionVisual;

        public Renderer FaceUpRenderer => faceUpRenderer;

        public Renderer FaceDownRenderer => faceDownRenderer;

        public TextMesh FrontLabel => frontLabel;

        public TextMesh BackLabel => backLabel;

        public void AlignFaceLabelsToSurface(float localOrderHeight)
        {
            if (float.IsNaN(localOrderHeight)
                || float.IsInfinity(localOrderHeight)
                || localOrderHeight <= FaceLabelSurfaceEpsilon)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localOrderHeight),
                    "Card local-order height must exceed the face-label surface epsilon.");
            }

            ValidateReferences();
            AlignFaceLabelToSurface(
                frontLabel,
                faceUpRenderer,
                cardView.FaceUpVisualRoot.transform,
                localOrderHeight);
            AlignFaceLabelToSurface(
                backLabel,
                faceDownRenderer,
                cardView.FaceDownVisualRoot.transform,
                localOrderHeight);
        }

        public void SetCardContentVisible(bool visible)
        {
            if (frontLabel != null && frontLabel.gameObject.activeSelf != visible)
            {
                frontLabel.gameObject.SetActive(visible);
            }

            if (backLabel != null && backLabel.gameObject.activeSelf != visible)
            {
                backLabel.gameObject.SetActive(visible);
            }
        }

        public void ValidateReferences()
        {
            RequireReference(cardView, nameof(cardView));
            RequireReference(selectionVisual, nameof(selectionVisual));
            RequireReference(faceUpRenderer, nameof(faceUpRenderer));
            RequireReference(faceDownRenderer, nameof(faceDownRenderer));
            RequireReference(frontLabel, nameof(frontLabel));
            RequireReference(backLabel, nameof(backLabel));

            if (!ReferenceEquals(cardView.gameObject, gameObject)
                || !ReferenceEquals(selectionVisual.gameObject, gameObject))
            {
                throw new InvalidOperationException(
                    "PrototypeCard visual references must belong to the PrototypeCard root.");
            }

            if (!cardView.IsFacePresentationConfigured)
            {
                throw new InvalidOperationException("PrototypeCard requires explicit CardView face roots.");
            }

            if (!selectionVisual.IsConfigured
                || !ReferenceEquals(selectionVisual.ObjectView, cardView))
            {
                throw new InvalidOperationException(
                    "PrototypeCard requires an explicitly configured TabletopSelectionVisual.");
            }

            RequireDescendant(faceUpRenderer.transform, cardView.FaceUpVisualRoot.transform, nameof(faceUpRenderer));
            RequireDescendant(faceDownRenderer.transform, cardView.FaceDownVisualRoot.transform, nameof(faceDownRenderer));
            RequireDescendant(frontLabel.transform, cardView.FaceUpVisualRoot.transform, nameof(frontLabel));
            RequireDescendant(backLabel.transform, cardView.FaceDownVisualRoot.transform, nameof(backLabel));
        }

        private static void RequireReference(UnityEngine.Object reference, string name)
        {
            if (reference == null)
            {
                throw new InvalidOperationException($"PrototypeCard requires {name}.");
            }
        }

        private static void RequireDescendant(Transform candidate, Transform expectedRoot, string name)
        {
            if (!candidate.IsChildOf(expectedRoot))
            {
                throw new InvalidOperationException(
                    $"PrototypeCard {name} must be a descendant of {expectedRoot.name}.");
            }
        }

        private static void AlignFaceLabelToSurface(
            TextMesh label,
            Renderer faceRenderer,
            Transform faceRoot,
            float localOrderHeight)
        {
            MeshFilter meshFilter = faceRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                throw new InvalidOperationException(
                    $"PrototypeCard {faceRenderer.name} requires a readable face mesh.");
            }

            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            Vector3 rendererLocalSurface = new Vector3(
                meshBounds.center.x,
                meshBounds.max.y,
                meshBounds.center.z);
            Vector3 worldSurface = faceRenderer.transform.TransformPoint(rendererLocalSurface);
            Vector3 faceRootLocalSurface = faceRoot.InverseTransformPoint(worldSurface);

            Vector3 labelLocalPosition = label.transform.localPosition;
            labelLocalPosition.y = faceRootLocalSurface.y + FaceLabelSurfaceEpsilon;
            label.transform.localPosition = labelLocalPosition;

            float worldSurfaceOffset = label.transform.position.y - worldSurface.y;
            if (worldSurfaceOffset <= 0f || worldSurfaceOffset >= localOrderHeight)
            {
                throw new InvalidOperationException(
                    "PrototypeCard face-label depth must remain above its face and below the next local-order plane.");
            }
        }
    }
}
