using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal sealed class AnnotationRasterizer
    {
        private readonly Color32[] _buffer;
        private readonly int _width, _height;

        internal AnnotationRasterizer(int width, int height)
        {
            _width = width; _height = height;
            _buffer = new Color32[width * height];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < _buffer.Length; i++) _buffer[i] = clear;
        }

        internal Color32[] Buffer => _buffer;
        internal int Width => _width;
        internal int Height => _height;

        internal void RenderAll(IReadOnlyList<IAnnotationCommand> commands)
        {
            for (int i = 0; i < commands.Count; i++) Render(commands[i]);
        }

        internal void Render(IAnnotationCommand cmd)
        {
            switch (cmd.Tool)
            {
                case AnnotationTool.Pen:    RenderPen(cmd); break;
                case AnnotationTool.Line:   RenderLine(cmd.Points[0], cmd.Points[1], cmd.Color, cmd.StrokeWidth); break;
                case AnnotationTool.Arrow:  RenderArrow(cmd); break;
                case AnnotationTool.Rect:   RenderRect(cmd); break;
                case AnnotationTool.Ellipse: RenderEllipse(cmd); break;
                case AnnotationTool.Text:   RenderTextPlaceholder(cmd); break;
                // Erase committed as PenCommand with alpha=0 — SetPixel handles transparency
            }
        }

        private void RenderPen(IAnnotationCommand cmd)
        {
            var pts = cmd.Points;
            for (int i = 1; i < pts.Count; i++)
                RenderLine(pts[i - 1], pts[i], cmd.Color, cmd.StrokeWidth);
        }

        private void RenderLine(Vector2 a, Vector2 b, Color32 color, float width)
        {
            int x0 = (int)(a.x * _width),  y0 = (int)(a.y * _height);
            int x1 = (int)(b.x * _width),  y1 = (int)(b.y * _height);
            BresenhamThick(x0, y0, x1, y1, color, Mathf.Max(1, (int)width));
        }

        private void RenderArrow(IAnnotationCommand cmd)
        {
            var start = cmd.Points[0];
            var end   = cmd.Points[1];
            RenderLine(start, end, cmd.Color, cmd.StrokeWidth);

            var dir = end - start;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            var perp = new Vector2(-dir.y, dir.x);
            float headSize = 0.02f;
            var left  = end - dir * headSize + perp * (headSize * 0.5f);
            var right = end - dir * headSize - perp * (headSize * 0.5f);
            RenderLine(end, left,  cmd.Color, cmd.StrokeWidth);
            RenderLine(end, right, cmd.Color, cmd.StrokeWidth);
        }

        private void RenderRect(IAnnotationCommand cmd)
        {
            var c1 = cmd.Points[0]; var c2 = cmd.Points[1];
            var tl = new Vector2(Mathf.Min(c1.x, c2.x), Mathf.Min(c1.y, c2.y));
            var br = new Vector2(Mathf.Max(c1.x, c2.x), Mathf.Max(c1.y, c2.y));
            var tr = new Vector2(br.x, tl.y);
            var bl = new Vector2(tl.x, br.y);

            if (cmd.Fill != AnnotationFill.None)
            {
                byte alpha = cmd.Fill == AnnotationFill.SemiTransparent ? (byte)80 : (byte)255;
                FillRect(tl, br, new Color32(cmd.Color.r, cmd.Color.g, cmd.Color.b, alpha));
            }
            RenderLine(tl, tr, cmd.Color, cmd.StrokeWidth);
            RenderLine(tr, br, cmd.Color, cmd.StrokeWidth);
            RenderLine(br, bl, cmd.Color, cmd.StrokeWidth);
            RenderLine(bl, tl, cmd.Color, cmd.StrokeWidth);
        }

        private void RenderEllipse(IAnnotationCommand cmd)
        {
            var center = cmd.Points[0]; var edge = cmd.Points[1];
            int cx = (int)(center.x * _width),  cy = (int)(center.y * _height);
            int rx = Mathf.Abs((int)(edge.x * _width)  - cx);
            int ry = Mathf.Abs((int)(edge.y * _height) - cy);
            if (rx < 1 || ry < 1) return;
            ParametricEllipse(cx, cy, rx, ry, cmd.Color, Mathf.Max(1, (int)cmd.StrokeWidth));
        }

        private void RenderTextPlaceholder(IAnnotationCommand cmd)
        {
            var pos = cmd.Points[0];
            int px = (int)(pos.x * _width), py = (int)(pos.y * _height);
            int w = Mathf.Min(80, _width  - px);
            int h = Mathf.Min(20, _height - py);
            var bg = new Color32(cmd.Color.r, cmd.Color.g, cmd.Color.b, 180);
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    SetPixel(px + dx, py + dy, bg);
        }

        private void SetPixel(int x, int y, Color32 c)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height) return;
            int idx = y * _width + x;
            if (c.a >= 255) { _buffer[idx] = c; return; }
            if (c.a == 0)   { _buffer[idx] = c; return; }
            var dst = _buffer[idx];
            float sa = c.a / 255f, da = 1f - sa;
            _buffer[idx] = new Color32(
                (byte)(c.r * sa + dst.r * da),
                (byte)(c.g * sa + dst.g * da),
                (byte)(c.b * sa + dst.b * da),
                (byte)Mathf.Min(255, c.a + dst.a));
        }

        private void BresenhamThick(int x0, int y0, int x1, int y1, Color32 color, int thickness)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int half = thickness / 2;
            while (true)
            {
                for (int ty = -half; ty <= half; ty++)
                    for (int tx = -half; tx <= half; tx++)
                        SetPixel(x0 + tx, y0 + ty, color);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void FillRect(Vector2 tl, Vector2 br, Color32 color)
        {
            int x0 = Mathf.Clamp((int)(tl.x * _width),  0, _width  - 1);
            int y0 = Mathf.Clamp((int)(tl.y * _height), 0, _height - 1);
            int x1 = Mathf.Clamp((int)(br.x * _width),  0, _width  - 1);
            int y1 = Mathf.Clamp((int)(br.y * _height), 0, _height - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    SetPixel(x, y, color);
        }

        private void ParametricEllipse(int cx, int cy, int rx, int ry, Color32 color, int thickness)
        {
            int segments = Mathf.Max(16, (rx + ry) / 2);
            float step = 2f * Mathf.PI / segments;
            int prevX = cx + rx, prevY = cy;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * step;
                int nx = cx + (int)(rx * Mathf.Cos(angle));
                int ny = cy + (int)(ry * Mathf.Sin(angle));
                BresenhamThick(prevX, prevY, nx, ny, color, thickness);
                prevX = nx; prevY = ny;
            }
        }
    }
}
