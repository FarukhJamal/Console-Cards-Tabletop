using System;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Coordinates;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    public sealed class CardView : TabletopObjectView
    {
        [SerializeField] private GameObject faceUpVisualRoot;
        [SerializeField] private GameObject faceDownVisualRoot;

        private CardInstanceState cardState;

        public CardInstanceState CardState => cardState;

        public GameObject FaceUpVisualRoot => faceUpVisualRoot;

        public GameObject FaceDownVisualRoot => faceDownVisualRoot;

        public bool IsFacePresentationConfigured => faceUpVisualRoot != null && faceDownVisualRoot != null;

        public CardFace? DisplayedFace
        {
            get
            {
                if (!IsFacePresentationConfigured)
                {
                    return null;
                }

                bool faceUpActive = faceUpVisualRoot.activeSelf;
                bool faceDownActive = faceDownVisualRoot.activeSelf;
                if (faceUpActive == faceDownActive)
                {
                    return null;
                }

                return faceUpActive ? CardFace.FaceUp : CardFace.FaceDown;
            }
        }

        public void Bind(
            CardInstanceState state,
            TabletopCoordinateConverter converter)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (IsFacePresentationConfigured)
            {
                ValidateSupportedFace(state.Face);
            }

            BindBase(state.BaseState, converter, TabletopObjectKind.Card);
            cardState = state;
            if (IsFacePresentationConfigured)
            {
                ApplyAcceptedFacePresentation();
            }
        }

        public void ConfigureFacePresentation(
            GameObject faceUpVisualRoot,
            GameObject faceDownVisualRoot)
        {
            ValidateFacePresentation(faceUpVisualRoot, faceDownVisualRoot);
            if (IsBound && cardState != null)
            {
                ValidateSupportedFace(cardState.Face);
            }

            this.faceUpVisualRoot = faceUpVisualRoot;
            this.faceDownVisualRoot = faceDownVisualRoot;

            if (IsBound && cardState != null)
            {
                ApplyAcceptedFacePresentation();
            }
        }

        public void ApplyAcceptedFacePresentation()
        {
            if (!IsBound)
            {
                throw new InvalidOperationException("CardView is not bound to Runtime State.");
            }

            if (cardState == null)
            {
                throw new InvalidOperationException("CardView has no Card Runtime State.");
            }

            if (!IsFacePresentationConfigured)
            {
                throw new InvalidOperationException("Card face presentation is not configured.");
            }

            CardFace face = cardState.Face;
            ValidateSupportedFace(face);

            faceUpVisualRoot.SetActive(face == CardFace.FaceUp);
            faceDownVisualRoot.SetActive(face == CardFace.FaceDown);
        }

        protected override void OnUnbound()
        {
            cardState = null;
        }

        protected override void OnAcceptedStateApplied()
        {
            if (!IsFacePresentationConfigured || cardState == null)
            {
                return;
            }

            ApplyAcceptedFacePresentation();
        }

        private void ValidateFacePresentation(
            GameObject faceUpVisualRoot,
            GameObject faceDownVisualRoot)
        {
            if (faceUpVisualRoot == null)
            {
                throw new ArgumentNullException(nameof(faceUpVisualRoot));
            }

            if (faceDownVisualRoot == null)
            {
                throw new ArgumentNullException(nameof(faceDownVisualRoot));
            }

            if (ReferenceEquals(faceUpVisualRoot, faceDownVisualRoot))
            {
                throw new ArgumentException("Face visual roots must be different objects.", nameof(faceDownVisualRoot));
            }

            if (ReferenceEquals(faceUpVisualRoot, gameObject))
            {
                throw new ArgumentException("FaceUp visual root cannot be the CardView root GameObject.", nameof(faceUpVisualRoot));
            }

            if (ReferenceEquals(faceDownVisualRoot, gameObject))
            {
                throw new ArgumentException("FaceDown visual root cannot be the CardView root GameObject.", nameof(faceDownVisualRoot));
            }

            if (!faceUpVisualRoot.transform.IsChildOf(transform))
            {
                throw new ArgumentException("FaceUp visual root must be a descendant of the CardView Transform.", nameof(faceUpVisualRoot));
            }

            if (!faceDownVisualRoot.transform.IsChildOf(transform))
            {
                throw new ArgumentException("FaceDown visual root must be a descendant of the CardView Transform.", nameof(faceDownVisualRoot));
            }
        }

        private static void ValidateSupportedFace(CardFace face)
        {
            if (!Enum.IsDefined(typeof(CardFace), face))
            {
                throw new InvalidOperationException("CardState.Face has an unsupported value.");
            }
        }
    }
}
