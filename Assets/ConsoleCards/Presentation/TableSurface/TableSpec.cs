using System;
using UnityEngine;

namespace ConsoleCards.Presentation.TableSurface
{
    /// <summary>The table's shape (doc 30, B2). Classic is the table built into the scene.</summary>
    public enum TableShape
    {
        Classic = 0,
        Rectangle = 1,
        Square = 2,
        Round = 3,
        Oval = 4,

        /// <summary>A table model (doc 30, B8; T1b), named by <see cref="TableSpec.modelId"/>.</summary>
        Model = 5,

        /// <summary>A star (T2): points alternate between the outer and inner radius.</summary>
        Star = 6,
    }

    /// <summary>What runs around the table's edge (doc 30, B2; T2).</summary>
    public enum TableEdge
    {
        None = 0,
        Lip = 1,
        RoundedRail = 2,
        WoodenRail = 3,

        /// <summary>A lip with spikes standing on it.</summary>
        Spikes = 4,

        /// <summary>A lip with a glowing strip along its top.</summary>
        Neon = 5,
    }

    /// <summary>
    /// A table described as data (doc 30, B2; T1, T2): the shape, its size in table units, the corner rounding and the
    /// edge. Only this is saved (in the profile, later with games) and, in multiplayer, sent: every player builds
    /// the same table from it.
    /// </summary>
    [Serializable]
    public sealed class TableSpec
    {
        public const int CurrentVersion = 1;
        public const float MinimumSize = 6f;
        public const float MaximumSize = 200f;
        public const float DefaultCornerRadius = 1f;
        public const float DefaultEdgeHeight = 0.3f;
        public const float DefaultEdgeWidth = 0.5f;
        public const float DefaultThickness = 0.6f;

        public int version = CurrentVersion;
        public TableShape shape = TableShape.Classic;

        // For TableShape.Model: the TableModel's id (T1b). Empty for every other shape.
        public string modelId = string.Empty;

        // Across (left to right) and deep (front to back), in table units. Round uses width as its diameter.
        public float width;
        public float depth;
        public float cornerRadius = DefaultCornerRadius;
        public TableEdge edge = TableEdge.Lip;
        public float edgeHeight = DefaultEdgeHeight;
        public float edgeWidth = DefaultEdgeWidth;
        public float thickness = DefaultThickness;

        // Star (T2): how many points, and the inner radius as a share of the outer one.
        public const int DefaultStarPoints = 5;
        public const float DefaultStarInner = 0.6f;
        public int starPoints = DefaultStarPoints;
        public float starInner = DefaultStarInner;

        public TableSpec Copy() => (TableSpec)MemberwiseClone();

        /// <summary>
        /// Star corner <paramref name="index"/> (0 to 2·points−1) for an outer radius of 1: even corners are the
        /// tips (the first points forward, +y), odd ones the inner corners. Counter-clockwise seen from above.
        /// </summary>
        public static Vector2 StarCorner(int index, int points, float inner)
        {
            float angle = (0.5f * Mathf.PI) + (index * Mathf.PI / points);
            float radius = index % 2 == 0 ? 1f : inner;
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        /// <summary>
        /// The default size of a shape for a classic table area of <paramref name="classicWidth"/> ×
        /// <paramref name="classicDepth"/>: each shape covers the whole classic area, so every game layout still
        /// fits. Rectangle: the same; Square: the longer side; Round: the diagonal as diameter; Oval: the ellipse
        /// through the area's corners.
        /// </summary>
        public static TableSpec DefaultFor(TableShape shape, float classicWidth, float classicDepth)
        {
            float w = Mathf.Max(MinimumSize, classicWidth);
            float d = Mathf.Max(MinimumSize, classicDepth);
            TableSpec spec = new TableSpec { shape = shape, width = w, depth = d };
            switch (shape)
            {
                case TableShape.Square:
                    spec.width = spec.depth = Mathf.Max(w, d);
                    break;
                case TableShape.Round:
                    spec.width = spec.depth = Mathf.Sqrt((w * w) + (d * d));
                    break;
                case TableShape.Oval:
                    spec.width = w * Mathf.Sqrt(2f);
                    spec.depth = d * Mathf.Sqrt(2f);
                    break;
                case TableShape.Star:
                    spec.width = spec.depth = 2f * StarRadiusHolding(0.5f * w, 0.5f * d, spec.starPoints, spec.starInner);
                    break;
            }

            spec.Clamp();
            return spec;
        }

        /// <summary>Keeps every value inside its safe range (doc 30, B2 safety); NaN becomes the minimum.</summary>
        public void Clamp()
        {
            if (!Enum.IsDefined(typeof(TableShape), shape)) shape = TableShape.Classic;
            if (!Enum.IsDefined(typeof(TableEdge), edge)) edge = TableEdge.Lip;
            starPoints = Mathf.Clamp(starPoints, 3, 12);
            starInner = Range(starInner, 0.25f, 0.9f);
            if (shape != TableShape.Model || modelId == null) modelId = string.Empty;
            width = Range(width, MinimumSize, MaximumSize);
            depth = Range(depth, MinimumSize, MaximumSize);
            if (shape == TableShape.Square || shape == TableShape.Round || shape == TableShape.Star) depth = width;
            cornerRadius = Range(cornerRadius, 0f, 0.5f * Mathf.Min(width, depth) - 0.01f);
            edgeHeight = Range(edgeHeight, 0.05f, 3f);
            edgeWidth = Range(edgeWidth, 0.05f, 3f);
            thickness = Range(thickness, 0.1f, 3f);
            version = CurrentVersion;
        }

        /// <summary>The shape after <paramref name="shape"/> in the Table menu's cycle.</summary>
        public static TableShape Next(TableShape shape) => shape switch
        {
            TableShape.Classic => TableShape.Rectangle,
            TableShape.Rectangle => TableShape.Square,
            TableShape.Square => TableShape.Round,
            TableShape.Round => TableShape.Oval,
            TableShape.Oval => TableShape.Star,
            _ => TableShape.Classic,
        };

        public static string Label(TableShape shape) => shape switch
        {
            TableShape.Rectangle => "Rectangle",
            TableShape.Square => "Square",
            TableShape.Round => "Round",
            TableShape.Oval => "Oval",
            TableShape.Model => "Model",
            TableShape.Star => "Star",
            _ => "Classic",
        };

        /// <summary>The edge after <paramref name="edge"/> in the Table menu's cycle.</summary>
        public static TableEdge NextEdge(TableEdge edge) => edge switch
        {
            TableEdge.None => TableEdge.Lip,
            TableEdge.Lip => TableEdge.RoundedRail,
            TableEdge.RoundedRail => TableEdge.WoodenRail,
            TableEdge.WoodenRail => TableEdge.Spikes,
            TableEdge.Spikes => TableEdge.Neon,
            _ => TableEdge.None,
        };

        public static string EdgeLabel(TableEdge edge) => edge switch
        {
            TableEdge.Lip => "Lip",
            TableEdge.RoundedRail => "Rounded rail",
            TableEdge.WoodenRail => "Wooden rail",
            TableEdge.Spikes => "Spikes",
            TableEdge.Neon => "Neon",
            _ => "None",
        };

        // The smallest outer radius whose star holds the whole half-width × half-depth area (the star grows evenly
        // from its centre, so a halving search finds it).
        private static float StarRadiusHolding(float halfWidth, float halfDepth, int points, float inner)
        {
            points = Mathf.Clamp(points, 3, 12);
            inner = Mathf.Clamp(inner, 0.25f, 0.9f);
            float low = Mathf.Sqrt((halfWidth * halfWidth) + (halfDepth * halfDepth));
            float high = low / (inner * Mathf.Cos(Mathf.PI / points)) * 1.5f;
            for (int step = 0; step < 32; step++)
            {
                float middle = 0.5f * (low + high);
                if (StarHolds(middle, halfWidth, halfDepth, points, inner)) high = middle;
                else low = middle;
            }

            return high;
        }

        private static bool StarHolds(float radius, float halfWidth, float halfDepth, int points, float inner)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 p = new Vector2(corner % 2 == 0 ? halfWidth : -halfWidth, corner < 2 ? halfDepth : -halfDepth);
                bool inside = false;
                int count = 2 * points;
                for (int i = 0, j = count - 1; i < count; j = i++)
                {
                    Vector2 a = StarCorner(i, points, inner) * radius;
                    Vector2 b = StarCorner(j, points, inner) * radius;
                    if ((a.y > p.y) != (b.y > p.y) && p.x < ((b.x - a.x) * (p.y - a.y) / (b.y - a.y)) + a.x) inside = !inside;
                }

                if (!inside) return false;
            }

            return true;
        }

        private static float Range(float value, float minimum, float maximum) =>
            float.IsNaN(value) || float.IsInfinity(value) ? minimum : Mathf.Clamp(value, minimum, Mathf.Max(minimum, maximum));
    }
}
