using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Immutable closed polygon on XZ plane. x=worldX, y=worldZ.
    /// Minimum 3 vertices. Closing duplicate vertex is auto-stripped.
    /// </summary>
    internal readonly struct Polygon2D
    {
        public readonly Vector2[] Vertices;
        public int Count => Vertices.Length;

        /// <summary>Construct from XZ vertex array. Makes defensive copy.</summary>
        public Polygon2D(Vector2[] vertices)
        {
            if (vertices == null || vertices.Length < 3)
                throw new ArgumentException($"Polygon requires >= 3 vertices, got {vertices?.Length ?? 0}");

            // Defensive copy
            var copy = new Vector2[vertices.Length];
            Array.Copy(vertices, copy, vertices.Length);

            // Strip closing duplicate (last == first within 1e-5)
            if (copy.Length > 3 && Vector2.Distance(copy[0], copy[copy.Length - 1]) < 1e-5f)
                Array.Resize(ref copy, copy.Length - 1);

            if (copy.Length < 3)
                throw new ArgumentException($"Polygon requires >= 3 vertices after stripping closing duplicate");

            Vertices = copy;
        }

        /// <summary>Construct from 3D world points by projecting to XZ plane.</summary>
        public Polygon2D(IList<Vector3> worldPoints) : this(ProjectXZ(worldPoints)) { }

        /// <summary>Project 3D world points to XZ plane as Vector2(x, z).</summary>
        public static Vector2[] ProjectXZ(IList<Vector3> points)
        {
            var result = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
                result[i] = new Vector2(points[i].x, points[i].z);
            return result;
        }

        // ── Point-in-polygon (winding number) ──────────────────────────

        /// <summary>
        /// Winding number PIP test. NonZero fill rule.
        /// Returns true if point p is inside this polygon.
        /// O(V) per call. Works for concave and self-intersecting polygons.
        /// </summary>
        public bool Contains(Vector2 p)
        {
            int wn = 0;
            var v = Vertices;
            int n = v.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (v[j].y <= p.y)
                {
                    if (v[i].y > p.y && IsLeft(v[j], v[i], p) > 0f) ++wn;
                }
                else
                {
                    if (v[i].y <= p.y && IsLeft(v[j], v[i], p) < 0f) --wn;
                }
            }
            return wn != 0;
        }

        /// <summary>XZ-projection shortcut for 3D world position.</summary>
        public bool Contains(Vector3 worldPos)
            => Contains(new Vector2(worldPos.x, worldPos.z));

        /// <summary>
        /// Batch containment test with AABB pre-filter.
        /// Returns list of indices into points[] that are inside polygon.
        /// O(N) AABB + O(N'*V) PIP.
        /// </summary>
        public List<int> ContainsBatch(Vector2[] points)
        {
            var bounds = ComputeBounds();
            var result = new List<int>();
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                if (p.x < bounds.xMin || p.x > bounds.xMax ||
                    p.y < bounds.yMin || p.y > bounds.yMax) continue;
                if (Contains(p)) result.Add(i);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float IsLeft(Vector2 p0, Vector2 p1, Vector2 p2)
            => (p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y);

        // ── Metrics ────────────────────────────────────────────────────

        /// <summary>Signed area via Shoelace formula. Positive=CCW, Negative=CW.</summary>
        public float SignedArea()
        {
            float sum = 0f;
            var v = Vertices;
            int n = v.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
                sum += (v[j].x * v[i].y) - (v[i].x * v[j].y);
            return sum * 0.5f;
        }

        /// <summary>Absolute area in world square units.</summary>
        public float Area() => Math.Abs(SignedArea());

        /// <summary>Centroid (center of mass). Falls back to vertex average for degenerate polygons.</summary>
        public Vector2 Centroid()
        {
            float cx = 0f, cy = 0f, area2 = 0f;
            var v = Vertices;
            int n = v.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float cross = v[j].x * v[i].y - v[i].x * v[j].y;
                cx += (v[j].x + v[i].x) * cross;
                cy += (v[j].y + v[i].y) * cross;
                area2 += cross;
            }
            float a6 = area2 * 3f;
            if (Math.Abs(a6) < 1e-10f)
            {
                var avg = Vector2.zero;
                foreach (var p in v) avg += p;
                return avg / n;
            }
            return new Vector2(cx / a6, cy / a6);
        }

        /// <summary>AABB of polygon vertices.</summary>
        public Rect ComputeBounds()
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in Vertices)
            {
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        // ── Serialization ──────────────────────────────────────────────

        /// <summary>
        /// Serialize to CSV: "x1,z1;x2,z2;x3,z3"
        /// F2 precision (1cm). InvariantCulture.
        /// </summary>
        public string ToCsv()
        {
            var sb = new StringBuilder(Count * 14);
            for (int i = 0; i < Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(Vertices[i].x.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(Vertices[i].y.ToString("F2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parse CSV "x1,z1;x2,z2;x3,z3" back to Polygon2D.
        /// Throws ArgumentException on bad format or too few vertices.
        /// Max 256 vertices enforced here.
        /// </summary>
        public static Polygon2D FromCsv(string csv)
        {
            if (string.IsNullOrEmpty(csv))
                throw new ArgumentException("Empty polygon CSV");

            var pairs = csv.Split(';');
            if (pairs.Length < 3)
                throw new ArgumentException($"Polygon requires >= 3 vertices, got {pairs.Length}");
            if (pairs.Length > 256)
                throw new ArgumentException($"Too many vertices: {pairs.Length} (max 256). Simplify polygon.");

            var verts = new Vector2[pairs.Length];
            for (int i = 0; i < pairs.Length; i++)
            {
                var xy = pairs[i].Trim().Split(',');
                if (xy.Length != 2)
                    throw new ArgumentException($"Invalid vertex format at index {i}: '{pairs[i].Trim()}'. Expected 'x,z'");

                if (!float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                    throw new ArgumentException($"Non-numeric x at vertex {i}: '{xy[0]}'");
                if (!float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                    throw new ArgumentException($"Non-numeric z at vertex {i}: '{xy[1]}'");

                if (Mathf.Abs(x) > 100_000f || Mathf.Abs(z) > 100_000f)
                    throw new ArgumentException($"Vertex {i} coordinates out of range (max 100000)");

                verts[i] = new Vector2(x, z);
            }

            return new Polygon2D(verts);
        }

        // ── Simplification (RDP) ───────────────────────────────────────

        /// <summary>
        /// Ramer-Douglas-Peucker simplification.
        /// epsilon: perpendicular distance threshold in world units.
        /// Never reduces below 3 vertices.
        /// </summary>
        public Polygon2D Simplify(float epsilon = 0.3f)
        {
            if (Count <= 3) return this;
            // Close the polygon temporarily so RDP considers the closing edge v[n-1]→v[0]
            var closed = new Vector2[Count + 1];
            Array.Copy(Vertices, closed, Count);
            closed[Count] = Vertices[0];

            var keep = new bool[Count + 1];
            keep[0] = true;
            keep[Count] = true;
            RDPRecurse(closed, 0, Count, epsilon, keep);

            var result = new List<Vector2>(Count);
            for (int i = 0; i < Count; i++)   // skip duplicated closing vertex
                if (keep[i]) result.Add(Vertices[i]);
            return result.Count >= 3 ? new Polygon2D(result.ToArray()) : this;
        }

        private static void RDPRecurse(Vector2[] pts, int start, int end, float eps, bool[] keep)
        {
            if (end - start < 2) return;
            float maxDist = 0f;
            int maxIdx = start;
            for (int i = start + 1; i < end; i++)
            {
                float d = PerpendicularDistance(pts[i], pts[start], pts[end]);
                if (d > maxDist) { maxDist = d; maxIdx = i; }
            }
            if (maxDist > eps)
            {
                keep[maxIdx] = true;
                RDPRecurse(pts, start, maxIdx, eps, keep);
                RDPRecurse(pts, maxIdx, end, eps, keep);
            }
        }

        private static float PerpendicularDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            float dx = b.x - a.x, dy = b.y - a.y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-10f) return Vector2.Distance(p, a);
            return Math.Abs(dx * (a.y - p.y) - dy * (a.x - p.x)) / (float)Math.Sqrt(lenSq);
        }
    }
}
