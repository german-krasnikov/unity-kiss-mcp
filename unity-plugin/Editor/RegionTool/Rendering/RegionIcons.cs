using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    internal static class RegionIcons
    {
        private const int S = 16;

        private static Texture2D _lasso, _rect, _circle, _pbp;

        internal static Texture2D Lasso  => _lasso  ??= MakeLasso();
        internal static Texture2D Rect   => _rect   ??= MakeRect();
        internal static Texture2D Circle => _circle ??= MakeCircle();
        internal static Texture2D PbP    => _pbp    ??= MakePbP();

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

        private static void DrawLine(Texture2D t, float x0, float y0, float x1, float y1, Color c, float thickness = 1.5f)
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

        private static void DrawEllipseOutline(Texture2D t, int cx, int cy, int rx, int ry, Color c)
        {
            int steps = 32;
            for (int i = 0; i < steps; i++)
            {
                float a0 = 2 * Mathf.PI * i / steps;
                float a1 = 2 * Mathf.PI * (i + 1) / steps;
                DrawLine(t, cx + rx * Mathf.Cos(a0), cy + ry * Mathf.Sin(a0),
                            cx + rx * Mathf.Cos(a1), cy + ry * Mathf.Sin(a1), c, 1.5f);
            }
        }

        // Freehand wavy closed loop
        private static Texture2D MakeLasso()
        {
            var t = Create(); var w = Color.white;
            // Wavy closed curve approximated by line segments
            DrawLine(t,  8, 14,  3, 11, w);
            DrawLine(t,  3, 11,  2,  7, w);
            DrawLine(t,  2,  7,  4,  4, w);
            DrawLine(t,  4,  4,  7,  2, w);
            DrawLine(t,  7,  2, 11,  3, w);
            DrawLine(t, 11,  3, 13,  6, w);
            DrawLine(t, 13,  6, 13, 10, w);
            DrawLine(t, 13, 10, 10, 13, w);
            DrawLine(t, 10, 13,  8, 14, w);
            // Small hook tail
            DrawLine(t,  8, 14,  7, 12, w);
            t.Apply(false, true); return t;
        }

        // Rectangle outline
        private static Texture2D MakeRect()
        {
            var t = Create(); var w = Color.white;
            DrawLine(t,  2,  3, 13,  3, w);
            DrawLine(t, 13,  3, 13, 12, w);
            DrawLine(t, 13, 12,  2, 12, w);
            DrawLine(t,  2, 12,  2,  3, w);
            t.Apply(false, true); return t;
        }

        // Circle outline
        private static Texture2D MakeCircle()
        {
            var t = Create();
            DrawEllipseOutline(t, 8, 8, 5, 5, Color.white);
            t.Apply(false, true); return t;
        }

        // 5 dots connected by lines (polygon)
        private static Texture2D MakePbP()
        {
            var t = Create(); var w = Color.white;
            // Vertices of a rough pentagon
            (float x, float y)[] pts =
            {
                (8f, 13f), (2f, 9f), (4f, 3f), (12f, 3f), (14f, 9f)
            };
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Length];
                DrawLine(t, a.x, a.y, b.x, b.y, w);
                // Dot at each vertex
                SetPx(t, (int)a.x,     (int)a.y,     w);
                SetPx(t, (int)a.x + 1, (int)a.y,     w);
                SetPx(t, (int)a.x,     (int)a.y + 1, w);
                SetPx(t, (int)a.x + 1, (int)a.y + 1, w);
            }
            t.Apply(false, true); return t;
        }
    }
}
