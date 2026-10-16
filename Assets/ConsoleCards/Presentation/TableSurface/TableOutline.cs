using System.Collections.Generic;
using UnityEngine;

namespace ConsoleCards.Presentation.TableSurface
{
    /// <summary>
    /// A table's outline (doc 30, B2; T1): points around the edge, counter-clockwise seen from above, in the table's
    /// own flat space (x across, y deep, centre at 0). Points are spaced by distance with a cap, so a huge table gets
    /// wider spacing instead of more points (B5). Also answers "is this point on the table?" and "where is the
    /// nearest point on it?". The point list is made once and reused.
    /// </summary>
    public sealed class TableOutline
    {
        public const int MaximumPoints = 512;
        public const int MinimumCurvePoints = 32;

        private readonly List<Vector2> points = new List<Vector2>(MaximumPoints);
        private readonly int pointCap;

        public TableOutline(int pointCap = MaximumPoints)
        {
            this.pointCap = Mathf.Clamp(pointCap, 8, MaximumPoints);
        }

        public IReadOnlyList<Vector2> Points => points;
        public int Count => points.Count;

        /// <summary>Half the width and depth of the outline's bounding box.</summary>
        public Vector2 HalfSize { get; private set; }

        /// <summary>
        /// Builds the outline for <paramref name="spec"/> with <paramref name="worldPerUnit"/> world units per table
        /// unit, points about <paramref name="spacing"/> world units apart. False for Classic or a broken result.
        /// </summary>
        public bool Build(TableSpec spec, float worldPerUnit, float spacing)
        {
            points.Clear();
            if (spec == null || spec.shape == TableShape.Classic || worldPerUnit <= 0f) return false;
            float halfW = 0.5f * spec.width * worldPerUnit;
            float halfD = 0.5f * spec.depth * worldPerUnit;
            spacing = Mathf.Max(0.01f, spacing);
            switch (spec.shape)
            {
                case TableShape.Rectangle:
                case TableShape.Square:
                    AddRoundedRectangle(halfW, halfD, spec.cornerRadius * worldPerUnit, spacing);
                    break;
                default:
                    AddEllipse(halfW, halfD, spacing);
                    break;
            }

            HalfSize = new Vector2(halfW, halfD);
            return IsUsable();
        }

        /// <summary>True when the flat point lies on the table (edge included).</summary>
        public bool Contains(Vector2 point)
        {
            bool inside = false;
            int count = points.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[j];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < ((b.x - a.x) * (point.y - a.y) / (b.y - a.y)) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>The flat point itself when it is on the table, otherwise the nearest point on the edge.</summary>
        public Vector2 ClosestPoint(Vector2 point)
        {
            if (points.Count < 3 || Contains(point)) return point;
            Vector2 best = points[0];
            float bestDistance = float.PositiveInfinity;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                Vector2 a = points[j];
                Vector2 ab = points[i] - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                Vector2 candidate = a + (ab * t);
                float distance = (point - candidate).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>The outward direction at point <paramref name="index"/> (average of its two sides).</summary>
        public Vector2 Normal(int index)
        {
            int count = points.Count;
            Vector2 previous = points[(index + count - 1) % count];
            Vector2 current = points[index];
            Vector2 next = points[(index + 1) % count];
            Vector2 n1 = Outward(current - previous);
            Vector2 n2 = Outward(next - current);
            Vector2 n = n1 + n2;
            return n.sqrMagnitude > 1e-8f ? n.normalized : n2;
        }

        /// <summary>
        /// How far to move point <paramref name="index"/> along <see cref="Normal"/> so both sides move out by one
        /// unit: 1 on smooth curves, more at sharp corners (capped so a spike never shoots out).
        /// </summary>
        public float MitreScale(int index)
        {
            int count = points.Count;
            Vector2 next = points[(index + 1) % count];
            float cos = Vector2.Dot(Normal(index), Outward(next - points[index]));
            return cos > 0.25f ? 1f / cos : 4f;
        }

        // Counter-clockwise outline: the outward side of a direction is its right-hand side seen from above.
        private static Vector2 Outward(Vector2 direction)
        {
            Vector2 n = new Vector2(direction.y, -direction.x);
            return n.sqrMagnitude > 1e-12f ? n.normalized : Vector2.zero;
        }

        private void AddRoundedRectangle(float halfW, float halfD, float radius, float spacing)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(halfW, halfD) - 0.001f);
            // Straight sides need no points of their own; each corner is a quarter circle (or one point when sharp).
            float arc = 0.5f * Mathf.PI * radius;
            int perCorner = radius <= 0.001f ? 1 : Mathf.Clamp(Mathf.CeilToInt(arc / spacing) + 1, 3, (pointCap / 4));
            for (int corner = 0; corner < 4; corner++)
            {
                // The four corners in turn, counter-clockwise from (+x, +y).
                float sx = corner == 0 || corner == 3 ? 1f : -1f;
                float sy = corner < 2 ? 1f : -1f;
                Vector2 centre = new Vector2(sx * (halfW - radius), sy * (halfD - radius));
                float start = corner * 0.5f * Mathf.PI;
                for (int i = 0; i < perCorner; i++)
                {
                    float angle = perCorner == 1
                        ? start + (0.25f * Mathf.PI)
                        : start + (0.5f * Mathf.PI * i / (perCorner - 1));
                    Vector2 offset = perCorner == 1
                        ? new Vector2(Mathf.Sign(Mathf.Cos(angle)) * radius, Mathf.Sign(Mathf.Sin(angle)) * radius)
                        : new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    points.Add(centre + offset);
                }
            }
        }

        private void AddEllipse(float halfW, float halfD, float spacing)
        {
            // Ramanujan's perimeter estimate decides the point count; equal angle steps are fine for these shapes.
            float h = ((halfW - halfD) * (halfW - halfD)) / ((halfW + halfD) * (halfW + halfD));
            float perimeter = Mathf.PI * (halfW + halfD) * (1f + (3f * h / (10f + Mathf.Sqrt(4f - (3f * h)))));
            int count = Mathf.Clamp(Mathf.CeilToInt(perimeter / spacing), MinimumCurvePoints, pointCap);
            for (int i = 0; i < count; i++)
            {
                float angle = 2f * Mathf.PI * i / count;
                points.Add(new Vector2(Mathf.Cos(angle) * halfW, Mathf.Sin(angle) * halfD));
            }
        }

        // Safety (doc 30, B2): at least three points, all finite, and a real, counter-clockwise area.
        private bool IsUsable()
        {
            if (points.Count < 3) return false;
            float area = 0f;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                Vector2 p = points[i];
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y)) return false;
                area += (points[j].x * p.y) - (p.x * points[j].y);
            }

            return area > 1e-4f;
        }
    }
}
