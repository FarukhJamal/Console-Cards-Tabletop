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

        /// <summary>
        /// A regular hexagon (T-3, owner 2026-10-23: it replaces the star): six straight edges, one seat per edge, with
        /// flat edges at the near and far sides. A saved star table opens as a hexagon (same number).
        /// </summary>
        Hexagon = 6,
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

        public TableSpec Copy() => (TableSpec)MemberwiseClone();

        /// <summary>The hexagon's depth (flat edge to flat edge) as a share of its width (corner to corner): √3 / 2.</summary>
        public const float HexagonDepthShare = 0.8660254f;

        /// <summary>
        /// Hexagon corner <paramref name="index"/> (0 to 5) for a corner radius of 1: the first points right (+x), so
        /// the near (−y) and far (+y) sides are flat edges. Counter-clockwise seen from above.
        /// </summary>
        public static Vector2 HexagonCorner(int index)
        {
            float angle = index * Mathf.PI / 3f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
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
                case TableShape.Hexagon:
                    // The smallest hexagon holding the area: its flat sides clear the depth, its slanted sides the
                    // area's corners (√3·|x| + |y| ≤ √3·R).
                    spec.width = spec.depth = 2f * Mathf.Max(d / Mathf.Sqrt(3f), (0.5f * w) + (0.5f * d / Mathf.Sqrt(3f)));
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
            if (shape != TableShape.Model || modelId == null) modelId = string.Empty;
            width = Range(width, MinimumSize, MaximumSize);
            depth = Range(depth, MinimumSize, MaximumSize);
            if (shape == TableShape.Square || shape == TableShape.Round || shape == TableShape.Hexagon) depth = width;
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
            TableShape.Oval => TableShape.Hexagon,
            _ => TableShape.Classic,
        };

        public static string Label(TableShape shape) => shape switch
        {
            TableShape.Rectangle => "Rectangle",
            TableShape.Square => "Square",
            TableShape.Round => "Round",
            TableShape.Oval => "Oval",
            TableShape.Model => "Model",
            TableShape.Hexagon => "Hexagon",
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

        private static float Range(float value, float minimum, float maximum) =>
            float.IsNaN(value) || float.IsInfinity(value) ? minimum : Mathf.Clamp(value, minimum, Mathf.Max(minimum, maximum));
    }
}
