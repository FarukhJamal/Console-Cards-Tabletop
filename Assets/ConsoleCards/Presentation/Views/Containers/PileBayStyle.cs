using System;
using UnityEngine;

namespace ConsoleCards.Presentation.Views.Containers
{
    /// <summary>
    /// The platform's single look for pile bays (doc 21 principle 16): material, state colours and bar sizes.
    /// One shared asset; every pile prefab's bay reads it, so changing it changes every bay in every game.
    /// The bay's outer size is not here: it is the pile footprint from the placement settings.
    /// </summary>
    [CreateAssetMenu(fileName = "PileBayStyle", menuName = "Console Cards/Components/Pile Bay Style")]
    public sealed class PileBayStyle : ScriptableObject
    {
        [SerializeField] private Material material;
        [SerializeField] private Color restColor = new Color(0.55f, 0.66f, 0.63f, 1f);
        [SerializeField] private Color sourceColor = new Color(0.45f, 0.62f, 1f, 1f);
        [SerializeField] private Color validColor = new Color(0.45f, 0.95f, 0.6f, 1f);
        [SerializeField] private Color invalidColor = new Color(0.75f, 0.22f, 0.18f, 1f);
        [SerializeField] private float barThickness = 0.035f;
        [SerializeField] private float feedbackThicknessScale = 1.7f;
        [SerializeField] private float barHeight = 0.004f;
        [SerializeField] private float lift = 0.004f;
        [SerializeField] private float mouthHalfOpening = 0.18f;
        [SerializeField] private float mouthInset = 0.07f;
        [SerializeField] private float mouthSlant = 0.05f;
        [SerializeField] private float markThickness = 0.04f;
        [SerializeField] private float markScale = 1f;

        public Material Material => material;

        public Color RestColor => restColor;

        public Color SourceColor => sourceColor;

        public Color ValidColor => validColor;

        public Color InvalidColor => invalidColor;

        public float BarThickness => barThickness;

        public float FeedbackThicknessScale => feedbackThicknessScale;

        public float BarHeight => barHeight;

        public float Lift => lift;

        public float MouthHalfOpening => mouthHalfOpening;

        public float MouthInset => mouthInset;

        public float MouthSlant => mouthSlant;

        public float MarkThickness => markThickness;

        public float MarkScale => markScale;

        public void Validate()
        {
            if (material == null)
            {
                throw new InvalidOperationException($"Pile bay style '{name}' requires a material.");
            }

            RequirePositive(barThickness, nameof(barThickness));
            RequirePositive(feedbackThicknessScale, nameof(feedbackThicknessScale));
            RequirePositive(barHeight, nameof(barHeight));
            RequirePositive(mouthHalfOpening, nameof(mouthHalfOpening));
            RequirePositive(markThickness, nameof(markThickness));
            RequirePositive(markScale, nameof(markScale));
            if (float.IsNaN(lift) || float.IsInfinity(lift) || lift < 0f
                || float.IsNaN(mouthInset) || float.IsInfinity(mouthInset) || mouthInset < 0f
                || float.IsNaN(mouthSlant) || float.IsInfinity(mouthSlant) || mouthSlant < 0f
                || mouthSlant > mouthHalfOpening)
            {
                throw new InvalidOperationException(
                    $"Pile bay style '{name}' requires a finite, non-negative lift and mouth inset, and a slant within the opening.");
            }
        }

        private void RequirePositive(float value, string field)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                throw new InvalidOperationException($"Pile bay style '{name}' requires {field} above zero.");
            }
        }
    }
}
