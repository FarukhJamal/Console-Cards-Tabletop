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
    }
}
