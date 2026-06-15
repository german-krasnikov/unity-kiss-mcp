// Paints Mermaid edges (lines + arrowhead chevrons) via UIToolkit Painter2D.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class MermaidEdgePainter
    {
        private static readonly Color EdgeColor = new Color(0.48f, 0.55f, 0.7f);
        private const float LineWidth    = 2f;
        private const float ArrowLen     = 9f;   // chevron arm length
        private const float ArrowHalf    = 0.42f; // half-angle in radians (~24 deg)

        public static void Paint(MeshGenerationContext mgc, List<EdgeLine> edges)
        {
            if (edges == null || edges.Count == 0) return;

            var p = mgc.painter2D;
            p.lineWidth   = LineWidth;
            p.strokeColor = EdgeColor;

            foreach (var edge in edges)
            {
                var from = new Vector2(edge.X1, edge.Y1);
                var to   = new Vector2(edge.X2, edge.Y2);

                // Draw line.
                p.BeginPath();
                p.MoveTo(from);
                p.LineTo(to);
                p.Stroke();

                // Draw arrowhead chevron at destination if edge has an arrow.
                if (edge.Arrow)
                    DrawArrowhead(p, from, to);
            }
        }

        private static void DrawArrowhead(Painter2D p, Vector2 from, Vector2 to)
        {
            var dir = (to - from);
            if (dir.sqrMagnitude < 0.001f) return;
            dir.Normalize();

            // Angle of the incoming direction (pointing toward 'to').
            float angle = Mathf.Atan2(dir.y, dir.x);

            var left  = new Vector2(
                to.x - ArrowLen * Mathf.Cos(angle - ArrowHalf),
                to.y - ArrowLen * Mathf.Sin(angle - ArrowHalf));
            var right = new Vector2(
                to.x - ArrowLen * Mathf.Cos(angle + ArrowHalf),
                to.y - ArrowLen * Mathf.Sin(angle + ArrowHalf));

            p.BeginPath();
            p.MoveTo(left);
            p.LineTo(to);
            p.LineTo(right);
            p.Stroke();
        }
    }
}
