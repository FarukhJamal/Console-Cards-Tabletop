using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>
    /// The one turn-over motion shared by cards and the Console mat (doc 22 §14): the piece lifts, turns half a
    /// circle and lands, like a card turned over by hand. t runs 0..1 over the turn.
    /// </summary>
    public static class FlipTurnCurve
    {
        public const float DefaultCardSeconds = 0.38f;
        public const float DefaultCardLift = 0.35f;

        /// <summary>Turned angle in degrees, 0..180, easing in and out.</summary>
        public static float Angle(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - (2f * t)) * 180f;
        }

        /// <summary>Lift as a fraction of the full lift: 0 at both ends, 1 half way.</summary>
        public static float Lift(float t)
        {
            return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
        }
    }

    /// <summary>
    /// The double-sided Console mat (doc 22 §14). Its visual (the turn pivot, holding a front face and a back
    /// face) rests front up or back up and turns over about the mat's long axis. Cards lying in the slots can
    /// ride along for the turn. Presentation-only: the side facing up is Runtime State (ConsoleState.ShowsBack).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConsoleMatFaces : MonoBehaviour
    {
        [SerializeField] private Transform turnPivot;
        [SerializeField, Min(0.05f)] private float turnSeconds = 0.55f;
        [Tooltip("How high the mat rises while it turns. At least half the mat depth keeps its edge above the table.")]
        [SerializeField, Min(0f)] private float turnLift = 1.7f;

        private readonly List<Rider> riders = new List<Rider>();
        private bool restCaptured;
        private Vector3 restLocalPosition;
        private bool showsBack;
        private bool turning;
        private bool midpointDone;
        private float turnStarted;
        private Action atMidpoint;
        private Action atEnd;

        public Transform TurnPivot => turnPivot;
        public bool IsTurning => turning;
        public bool ShowsBack => showsBack;

        /// <summary>Shows the given side at rest with no motion. Ends any turn first.</summary>
        public void SetSideImmediate(bool back)
        {
            CompleteTurn();
            showsBack = back;
            ApplyRest();
        }

        /// <summary>
        /// Starts turning the mat over. atMidpoint runs when the mat stands on edge (the moment the side facing
        /// up changes); atEnd runs when it lands. Riders added before the midpoint ride the outgoing side and are
        /// released there; riders added with <see cref="AddIncomingRider"/> ride the incoming side to the end.
        /// </summary>
        public void BeginTurn(bool toBack, Action midpoint, Action end)
        {
            CompleteTurn();
            if (turnPivot == null)
            {
                showsBack = toBack;
                midpoint?.Invoke();
                end?.Invoke();
                return;
            }

            CaptureRest();
            showsBack = toBack;
            atMidpoint = midpoint;
            atEnd = end;
            midpointDone = false;
            turnStarted = Time.unscaledTime;
            turning = true;
        }

        /// <summary>A piece lying on the side facing up turns with the mat until the midpoint.</summary>
        public void AddRider(Transform rider)
        {
            if (!turning || midpointDone || rider == null || turnPivot == null) return;
            riders.Add(new Rider(rider, rider.parent));
            rider.SetParent(turnPivot, true);
        }

        /// <summary>
        /// A piece already placed where it rests on the incoming side once the mat lands. From the midpoint it
        /// turns up with the mat and arrives exactly there.
        /// </summary>
        public void AddIncomingRider(Transform rider)
        {
            if (!turning || !midpointDone || rider == null || turnPivot == null) return;
            Transform pivotParent = turnPivot.parent;
            Matrix4x4 landedPivot = (pivotParent != null ? pivotParent.localToWorldMatrix : Matrix4x4.identity)
                * Matrix4x4.TRS(restLocalPosition, RestRotation(showsBack), turnPivot.localScale);
            Matrix4x4 toCurrent = turnPivot.localToWorldMatrix * landedPivot.inverse;
            Vector3 position = toCurrent.MultiplyPoint3x4(rider.position);
            Quaternion rotation = turnPivot.rotation * Quaternion.Inverse(landedPivot.rotation) * rider.rotation;
            riders.Add(new Rider(rider, rider.parent));
            rider.SetPositionAndRotation(position, rotation);
            rider.SetParent(turnPivot, true);
        }

        /// <summary>Finishes a running turn at once: riders are released and both callbacks have run.</summary>
        public void CompleteTurn()
        {
            if (!turning) return;
            turning = false;
            ReleaseRiders();
            ApplyRest();
            if (!midpointDone)
            {
                midpointDone = true;
                Action midpoint = atMidpoint;
                atMidpoint = null;
                midpoint?.Invoke();
            }

            ReleaseRiders();
            Action end = atEnd;
            atEnd = null;
            end?.Invoke();
        }

        private void Update()
        {
            if (!turning) return;
            float t = Mathf.Clamp01((Time.unscaledTime - turnStarted) / Mathf.Max(0.05f, turnSeconds));
            if (t >= 1f)
            {
                CompleteTurn();
                return;
            }

            float startAngle = showsBack ? 0f : 180f;
            float angle = startAngle + FlipTurnCurve.Angle(t);
            turnPivot.localRotation = Quaternion.AngleAxis(angle, Vector3.right);
            turnPivot.localPosition = restLocalPosition + (Vector3.up * (turnLift * FlipTurnCurve.Lift(t)));
            if (!midpointDone && t >= 0.5f)
            {
                midpointDone = true;
                ReleaseRiders();
                Action midpoint = atMidpoint;
                atMidpoint = null;
                midpoint?.Invoke();
            }
        }

        private void OnDisable()
        {
            // A turn never outlives the mat being shown; riders go back where they came from.
            if (turning && turnPivot != null && turnPivot.gameObject.activeInHierarchy)
            {
                CompleteTurn();
            }
        }

        private void ApplyRest()
        {
            if (turnPivot == null) return;
            CaptureRest();
            turnPivot.localPosition = restLocalPosition;
            turnPivot.localRotation = RestRotation(showsBack);
        }

        private void CaptureRest()
        {
            if (restCaptured || turnPivot == null) return;
            restLocalPosition = turnPivot.localPosition;
            restCaptured = true;
        }

        private static Quaternion RestRotation(bool back)
        {
            return back ? Quaternion.AngleAxis(180f, Vector3.right) : Quaternion.identity;
        }

        private void ReleaseRiders()
        {
            for (int i = 0; i < riders.Count; i++)
            {
                Rider rider = riders[i];
                if (rider.Transform != null && rider.Transform.parent == turnPivot)
                {
                    rider.Transform.SetParent(rider.OriginalParent, true);
                }
            }

            riders.Clear();
        }

        private readonly struct Rider
        {
            public Rider(Transform transform, Transform originalParent)
            {
                Transform = transform;
                OriginalParent = originalParent;
            }

            public Transform Transform { get; }
            public Transform OriginalParent { get; }
        }
    }
}
