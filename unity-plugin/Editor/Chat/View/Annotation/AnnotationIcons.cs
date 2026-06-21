using UnityEngine;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal static class AnnotationIcons
    {
        private const int S = 18;

        private static Texture2D _pen, _line, _arrow, _rect, _ellipse, _text, _erase;
        private static Texture2D _undo, _redo, _clear, _cube3d, _send;
        private static Texture2D _widthS, _widthM, _widthL;

        internal static Texture2D Pen     => _pen     ??= MakePen();
        internal static Texture2D Line    => _line    ??= MakeLine();
        internal static Texture2D Arrow   => _arrow   ??= MakeArrow();
        internal static Texture2D Rect    => _rect    ??= MakeRect();
        internal static Texture2D Ellipse => _ellipse ??= MakeEllipse();
        internal static Texture2D Text    => _text    ??= MakeText();
        internal static Texture2D Erase   => _erase   ??= MakeErase();
        internal static Texture2D Undo    => _undo    ??= MakeUndo();
        internal static Texture2D Redo    => _redo    ??= MakeRedo();
        internal static Texture2D Clear   => _clear   ??= MakeClear();
        internal static Texture2D Cube3D  => _cube3d  ??= MakeCube3D();
        internal static Texture2D Send    => _send    ??= MakeSend();
        internal static Texture2D WidthS  => _widthS  ??= MakeWidthDot(2);
        internal static Texture2D WidthM  => _widthM  ??= MakeWidthDot(4);
        internal static Texture2D WidthL  => _widthL  ??= MakeWidthDot(6);

        private static Texture2D Create()
        {
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags  = HideFlags.HideAndDontSave
            };
            var blank = new Color(0, 0, 0, 0);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    tex.SetPixel(x, y, blank);
            return tex;
        }

        private static void SetPx(Texture2D t, int x, int y, Color c)
        {
            if (x >= 0 && x < S && y >= 0 && y < S) t.SetPixel(x, y, c);
        }

        private static void DrawLine(Texture2D t, float x0, float y0, float x1, float y1, Color c, float thickness = 2f)
        {
            int ix0 = (int)x0, iy0 = (int)y0, ix1 = (int)x1, iy1 = (int)y1;
            if (ix0 == ix1 && iy0 == iy1) { SetPx(t, ix0, iy0, c); return; }
            int dx = Mathf.Abs(ix1 - ix0), sx = ix0 < ix1 ? 1 : -1;
            int dy = -Mathf.Abs(iy1 - iy0), sy = iy0 < iy1 ? 1 : -1;
            int err = dx + dy;
            int half = Mathf.Max(0, (int)(thickness / 2f));
            while (true)
            {
                for (int ty = -half; ty <= half; ty++)
                    for (int tx = -half; tx <= half; tx++)
                        SetPx(t, ix0 + tx, iy0 + ty, c);
                if (ix0 == ix1 && iy0 == iy1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; ix0 += sx; }
                if (e2 <= dx) { err += dx; iy0 += sy; }
            }
        }

        private static void DrawCircle(Texture2D t, int cx, int cy, int radius, Color c)
        {
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= radius * radius)
                        SetPx(t, cx + x, cy + y, c);
        }

        private static void DrawEllipse(Texture2D t, int cx, int cy, int rx, int ry, Color c, float thick = 1.5f)
        {
            int steps = 32;
            for (int i = 0; i < steps; i++)
            {
                float a0 = 2 * Mathf.PI * i / steps;
                float a1 = 2 * Mathf.PI * (i + 1) / steps;
                DrawLine(t, cx + rx * Mathf.Cos(a0), cy + ry * Mathf.Sin(a0),
                            cx + rx * Mathf.Cos(a1), cy + ry * Mathf.Sin(a1), c, thick);
            }
        }

        private static Texture2D MakePen()
        {
            var t = Create(); var w = Color.white;
            DrawLine(t, 3, 4,  7, 12, w);
            DrawLine(t, 7, 12, 11, 6, w);
            DrawLine(t, 11, 6, 15, 14, w);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeLine()
        {
            var t = Create();
            DrawLine(t, 3, 3, 14, 14, Color.white);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeArrow()
        {
            var t = Create(); var w = Color.white;
            DrawLine(t, 3, 14, 14, 3, w);
            DrawLine(t, 14, 3, 9,  4, w);
            DrawLine(t, 14, 3, 13, 8, w);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeRect()
        {
            var t = Create(); var w = Color.white;
            DrawLine(t, 3,  4,  14,  4, w);
            DrawLine(t, 14, 4,  14, 13, w);
            DrawLine(t, 14, 13,  3, 13, w);
            DrawLine(t,  3, 13,  3,  4, w);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeEllipse()
        {
            var t = Create();
            DrawEllipse(t, 9, 9, 6, 4, Color.white);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeText()
        {
            var t = Create(); var w = Color.white;
            DrawLine(t, 4, 13, 14, 13, w);   // top bar
            DrawLine(t, 9, 13,  9,  3, w);   // stem
            DrawLine(t, 7,  3, 11,  3, w);   // serif top
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeErase()
        {
            var t = Create(); var w = Color.white;
            DrawLine(t,  3,  5, 10,  5, w);
            DrawLine(t, 10,  5, 15, 13, w);
            DrawLine(t, 15, 13,  8, 13, w);
            DrawLine(t,  8, 13,  3,  5, w);
            DrawLine(t,  7,  5, 12, 13, w, 1);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeUndo()
        {
            var t = Create(); var w = Color.white;
            DrawLine(t,  5,  7,  7, 13, w);
            DrawLine(t,  7, 13, 11, 14, w);
            DrawLine(t, 11, 14, 14, 11, w);
            DrawLine(t,  5,  7,  3, 10, w);
            DrawLine(t,  5,  7,  8,  9, w);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeRedo()
        {
            var t = Create(); var w = Color.white;
            DrawLine(t, 13,  7, 11, 13, w);
            DrawLine(t, 11, 13,  7, 14, w);
            DrawLine(t,  7, 14,  4, 11, w);
            DrawLine(t, 13,  7, 15, 10, w);
            DrawLine(t, 13,  7, 10,  9, w);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeClear()
        {
            var t = Create(); var w = Color.white;
            // body
            DrawLine(t,  5,  4,  5, 13, w);
            DrawLine(t,  5,  4, 13,  4, w);
            DrawLine(t, 13,  4, 13, 13, w);
            // rim
            DrawLine(t,  4, 13, 14, 13, w);
            // lid
            DrawLine(t,  7, 14,  7, 16, w);
            DrawLine(t,  7, 16, 11, 16, w);
            DrawLine(t, 11, 16, 11, 14, w);
            // inner lines
            DrawLine(t,  8, 12,  8,  6, w, 1);
            DrawLine(t, 10, 12, 10,  6, w, 1);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeCube3D()
        {
            var t = Create(); var w = Color.white;
            // front face
            DrawLine(t,  3,  3, 10,  3, w, 1);
            DrawLine(t, 10,  3, 10, 10, w, 1);
            DrawLine(t, 10, 10,  3, 10, w, 1);
            DrawLine(t,  3, 10,  3,  3, w, 1);
            // back face
            DrawLine(t,  7,  7, 14,  7, w, 1);
            DrawLine(t, 14,  7, 14, 14, w, 1);
            DrawLine(t, 14, 14,  7, 14, w, 1);
            DrawLine(t,  7, 14,  7,  7, w, 1);
            // connecting edges
            DrawLine(t,  3,  3,  7,  7, w, 1);
            DrawLine(t, 10,  3, 14,  7, w, 1);
            DrawLine(t, 10, 10, 14, 14, w, 1);
            DrawLine(t,  3, 10,  7, 14, w, 1);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeSend()
        {
            var t = Create(); var w = Color.white;
            DrawLine(t,  2,  9, 16, 14, w);
            DrawLine(t, 16, 14,  6, 11, w);
            DrawLine(t,  6, 11,  2,  9, w);
            DrawLine(t, 16, 14,  6,  4, w);
            DrawLine(t,  6,  4,  6, 11, w);
            t.Apply(false, true); return t;
        }

        private static Texture2D MakeWidthDot(int radius)
        {
            var t = Create();
            DrawCircle(t, S / 2, S / 2, radius, Color.white);
            t.Apply(false, true); return t;
        }
    }
}
