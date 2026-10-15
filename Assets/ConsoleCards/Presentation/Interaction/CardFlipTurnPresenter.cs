using System;
using System.Collections.Generic;
using ConsoleCards.Core.Domain;
using ConsoleCards.Presentation.Views;
using UnityEngine;

namespace ConsoleCards.Presentation.Interaction
{
    /// <summary>
    /// Plays the turn-over of a card whose face was just changed (doc 22 §14): the old face lifts and turns to
    /// its edge, the new face turns down onto the table. Only the card's two face visuals move; the card itself
    /// stays where Runtime State puts it. Presentation-only; ticked by the composition, no per-frame allocation.
    /// </summary>
    public sealed class CardFlipTurnPresenter
    {
        private readonly List<Turn> turns = new List<Turn>(8);

        public float Seconds { get; set; } = FlipTurnCurve.DefaultCardSeconds;
        public float LiftHeight { get; set; } = FlipTurnCurve.DefaultCardLift;

        /// <summary>Called once when a turn lands, with the card's position (for the reveal flash).</summary>
        public Action<Vector3> Landed { get; set; }

        public bool IsTurning(CardView card)
        {
            for (int i = 0; i < turns.Count; i++)
            {
                if (ReferenceEquals(turns[i].Card, card)) return true;
            }

            return false;
        }

        /// <summary>Starts the turn of a card already accepted on its new face.</summary>
        public void Begin(CardView card, float now)
        {
            if (card == null || !card.IsBound || card.CardState == null || !card.IsFacePresentationConfigured)
            {
                return;
            }

            Complete(card);
            bool faceUp = card.CardState.Face == CardFace.FaceUp;
            Transform newFace = (faceUp ? card.FaceUpVisualRoot : card.FaceDownVisualRoot).transform;
            Transform oldFace = (faceUp ? card.FaceDownVisualRoot : card.FaceUpVisualRoot).transform;
            turns.Add(new Turn
            {
                Card = card,
                OldFace = oldFace,
                NewFace = newFace,
                OldRestPosition = oldFace.localPosition,
                OldRestRotation = oldFace.localRotation,
                NewRestPosition = newFace.localPosition,
                NewRestRotation = newFace.localRotation,
                Started = now,
            });
            oldFace.gameObject.SetActive(true);
            newFace.gameObject.SetActive(false);
        }

        public void Tick(float now)
        {
            for (int i = turns.Count - 1; i >= 0; i--)
            {
                Turn turn = turns[i];
                if (turn.Card == null || turn.OldFace == null || turn.NewFace == null)
                {
                    turns.RemoveAt(i);
                    continue;
                }

                float t = Mathf.Clamp01((now - turn.Started) / Mathf.Max(0.05f, Seconds));
                if (t >= 1f)
                {
                    Vector3 landedAt = turn.Card.transform.position;
                    Finish(turn);
                    turns.RemoveAt(i);
                    Landed?.Invoke(landedAt);
                    continue;
                }

                float angle = FlipTurnCurve.Angle(t);
                Vector3 lift = turn.Card.transform.InverseTransformVector(Vector3.up * (LiftHeight * FlipTurnCurve.Lift(t)));
                if (angle < 90f)
                {
                    Quaternion turnRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                    Place(turn.OldFace, turnRotation, turn.OldRestPosition, turn.OldRestRotation, lift);
                    SetActive(turn.OldFace, true);
                    SetActive(turn.NewFace, false);
                }
                else
                {
                    Quaternion turnRotation = Quaternion.AngleAxis(angle - 180f, Vector3.forward);
                    Place(turn.NewFace, turnRotation, turn.NewRestPosition, turn.NewRestRotation, lift);
                    SetActive(turn.NewFace, true);
                    SetActive(turn.OldFace, false);
                }
            }
        }

        /// <summary>Ends every turn at once with the faces at rest (before a rebuild or teardown).</summary>
        public void CompleteAll()
        {
            for (int i = 0; i < turns.Count; i++) Finish(turns[i]);
            turns.Clear();
        }

        private void Complete(CardView card)
        {
            for (int i = turns.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(turns[i].Card, card)) continue;
                Finish(turns[i]);
                turns.RemoveAt(i);
            }
        }

        private static void Finish(Turn turn)
        {
            if (turn.OldFace != null)
            {
                turn.OldFace.localPosition = turn.OldRestPosition;
                turn.OldFace.localRotation = turn.OldRestRotation;
            }

            if (turn.NewFace != null)
            {
                turn.NewFace.localPosition = turn.NewRestPosition;
                turn.NewFace.localRotation = turn.NewRestRotation;
            }

            if (turn.Card != null && turn.Card.IsBound && turn.Card.CardState != null && turn.Card.IsFacePresentationConfigured)
            {
                turn.Card.ApplyAcceptedFacePresentation();
            }
        }

        private static void Place(Transform face, Quaternion turnRotation, Vector3 restPosition, Quaternion restRotation, Vector3 lift)
        {
            face.localPosition = (turnRotation * restPosition) + lift;
            face.localRotation = turnRotation * restRotation;
        }

        private static void SetActive(Transform face, bool active)
        {
            if (face.gameObject.activeSelf != active) face.gameObject.SetActive(active);
        }

        private struct Turn
        {
            public CardView Card;
            public Transform OldFace;
            public Transform NewFace;
            public Vector3 OldRestPosition;
            public Quaternion OldRestRotation;
            public Vector3 NewRestPosition;
            public Quaternion NewRestRotation;
            public float Started;
        }
    }
}
