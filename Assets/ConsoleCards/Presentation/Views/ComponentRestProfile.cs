using UnityEngine;

namespace ConsoleCards.Presentation.Views
{
    /// <summary>
    /// Authored rest data for a placed component root. <see cref="ComponentRestHeight.RestLift"/> adds
    /// <see cref="RestOffset"/> after measuring the root's lowest body point: positive raises the piece,
    /// negative sinks it. A root without this component rests with an offset of 0.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComponentRestProfile : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Extra height added after the measured rest height. 0 rests the lowest body point on the surface.")]
        private float restOffset = 0f;

        /// <summary>The authored offset; a non-finite value counts as 0.</summary>
        public float RestOffset => float.IsNaN(restOffset) || float.IsInfinity(restOffset) ? 0f : restOffset;
    }
}
