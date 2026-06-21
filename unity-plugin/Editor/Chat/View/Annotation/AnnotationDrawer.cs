using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal static class AnnotationDrawer
    {
        internal static void DrawPenTrail(Rect canvasRect, IReadOnlyList<Vector2> points, float width)
        {
            if (points.Count < 2) return;
            var px = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var p = NormalizedToPixel(points[i], canvasRect);
                px[i] = new Vector3(p.x, p.y, 0);
            }
            Handles.DrawAAPolyLine(width, px);
        }

        internal static void DrawLine(Rect canvasRect, Vector2 a, Vector2 b, float width)
        {
            var pa = NormalizedToPixel(a, canvasRect);
            var pb = NormalizedToPixel(b, canvasRect);
            Handles.DrawAAPolyLine(width, new Vector3(pa.x, pa.y, 0), new Vector3(pb.x, pb.y, 0));
        }

        internal static void DrawArrowhead(Rect canvasRect, Vector2 start, Vector2 end, float width)
        {
            var dir = (end - start).normalized;
            var perp = new Vector2(-dir.y, dir.x);
            const float headSize = 0.03f;
            DrawLine(canvasRect, end, end - dir * headSize + perp * headSize * 0.5f, width);
            DrawLine(canvasRect, end, end - dir * headSize - perp * headSize * 0.5f, width);
        }

        internal static void DrawRect(Rect canvasRect, IAnnotationCommand cmd)
        {
            var a = cmd.Points[0]; var b = cmd.Points[1];
            var tl = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
            var br = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            if (cmd.Fill != AnnotationFill.None)
            {
                byte alpha = cmd.Fill == AnnotationFill.SemiTransparent ? (byte)80 : (byte)255;
                var ptl = NormalizedToPixel(tl, canvasRect);
                var pbr = NormalizedToPixel(br, canvasRect);
                EditorGUI.DrawRect(new Rect(ptl.x, ptl.y, pbr.x - ptl.x, pbr.y - ptl.y),
                    new Color32(cmd.Color.r, cmd.Color.g, cmd.Color.b, alpha));
            }
            DrawRectOutline(canvasRect, tl, br, cmd.StrokeWidth);
        }

        internal static void DrawRectOutline(Rect canvasRect, Vector2 a, Vector2 b, float width)
        {
            var tl = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
            var br = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            var tr = new Vector2(br.x, tl.y);
            var bl = new Vector2(tl.x, br.y);
            DrawLine(canvasRect, tl, tr, width);
            DrawLine(canvasRect, tr, br, width);
            DrawLine(canvasRect, br, bl, width);
            DrawLine(canvasRect, bl, tl, width);
        }

        internal static void DrawEllipseOutline(Rect canvasRect, Vector2 center, Vector2 edge, float width)
        {
            var pc = NormalizedToPixel(center, canvasRect);
            var pe = NormalizedToPixel(edge, canvasRect);
            float rx = Mathf.Abs(pe.x - pc.x);
            float ry = Mathf.Abs(pe.y - pc.y);
            int segments = Mathf.Max(16, (int)(rx + ry) / 2);
            var pts = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                pts[i] = new Vector3(pc.x + rx * Mathf.Cos(angle), pc.y + ry * Mathf.Sin(angle), 0);
            }
            Handles.DrawAAPolyLine(width, pts);
        }

        internal static Vector2 PixelToNormalized(Vector2 pixel, Rect canvasRect)
            => new Vector2((pixel.x - canvasRect.x) / canvasRect.width,
                           (pixel.y - canvasRect.y) / canvasRect.height);

        internal static Vector2 NormalizedToPixel(Vector2 norm, Rect canvasRect)
            => new Vector2(canvasRect.x + norm.x * canvasRect.width,
                           canvasRect.y + norm.y * canvasRect.height);
    }
}
