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
        private bool holdFaceDown;
        // HIDDEN-FACE (owner, 2026-11-27: "the other player's hand has two cards faced wrongly"): another player's hand
        // showed its cards face down by switching the face roots once at layout; any later face refresh (a card state
        // update, RefreshAcceptedCardFaces) put a face-up card's face back. Kept here, so every refresh keeps it down.
        private bool inHiddenHand;

        public CardInstanceState CardState => cardState;

#if UNITY_EDITOR
        /// <summary>
        /// P8A debug (owner, 2026-11-13: "a debug panel … to flip all the floor cards … to check winning"). EDITOR ONLY
        /// (compiled out of every player build): while set, a face-down card it names is DRAWN face up on this Editor's
        /// screen. Nothing on the table changes (its state, the rules, the other computers). Set by the Editor menu
        /// Console Cards › Debug › Fixtures › Peek at Floor.
        /// </summary>
        public static Func<ConsoleCards.Core.Identifiers.TabletopObjectId, bool> EditorPeek;
#endif

        public GameObject FaceUpVisualRoot => faceUpVisualRoot;

        public GameObject FaceDownVisualRoot => faceDownVisualRoot;

        public bool IsFacePresentationConfigured => faceUpVisualRoot != null && faceDownVisualRoot != null;

        /// <summary>
        /// True while the card is shown face down although its Runtime State is face up (Presentation only), for
        /// example a landed Trap Floor tile whose reveal already happened but which cannot turn with a pawn on it.
        /// </summary>
        public bool HoldsFaceDown => holdFaceDown;

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

        /// <summary>Shows the card face down regardless of its state (true) or its accepted face again (false).</summary>
        public void SetHoldFaceDown(bool hold)
        {
            holdFaceDown = hold;
            if (IsBound && cardState != null && IsFacePresentationConfigured)
            {
                ApplyAcceptedFacePresentation();
            }
        }

        /// <summary>HIDDEN-FACE: shown face down while it lies in another player's hand (true), or its accepted face (false).</summary>
        internal void SetInHiddenHand(bool hidden)
        {
            if (inHiddenHand == hidden) return;
            inHiddenHand = hidden;
            if (IsBound && cardState != null && IsFacePresentationConfigured)
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
            if (holdFaceDown || inHiddenHand) face = CardFace.FaceDown;
#if UNITY_EDITOR
            if (face == CardFace.FaceDown && EditorPeek != null && EditorPeek(cardState.BaseState.Id)) face = CardFace.FaceUp; // debug peek
#endif

            faceUpVisualRoot.SetActive(face == CardFace.FaceUp);
            faceDownVisualRoot.SetActive(face == CardFace.FaceDown);
        }

        protected override void OnUnbound()
        {
            cardState = null;
            holdFaceDown = false;
            inHiddenHand = false;
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
