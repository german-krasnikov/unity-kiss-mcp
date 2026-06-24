using UnityEngine;

namespace UnityMCP.Editor.UI
{
    /// <summary>
    /// Procedural icon builder with a shared design system.
    /// All strokes are 2px, round-cap distance-to-segment rasterizer,
    /// single near-white ink readable on both Unity dark and light themes.
    /// Theme switches after icon creation are acceptable — icons are cached forever.
    /// </summary>
    internal sealed class IconCanvas
    {
        public const int CanvasSize  = 18;
        public const int ContentMin  = 1;
        public const int ContentMax  = 16;
        public const int StrokeWidth = 2;

        // Near-white: readable on Unity dark (#383838) and light (#C8C8C8) backgrounds.
        public static readonly Color DefaultInk = new Color(0.92f, 0.92f, 0.92f, 1f);

        private readonly int     _size;
        private readonly Color[] _pixels;   // y * _size + x, y=0 = bottom row (Unity convention)
        private Color            _ink;

        private IconCanvas(int size)
        {
            _size   = size;
            _pixels = new Color[size * size];   // pre-zeroed (alpha=0)
            _ink    = DefaultInk;
        }

        public static IconCanvas New(int size = CanvasSize) => new IconCanvas(size);

        public IconCanvas WithInk(Color ink) { _ink = ink; return this; }

        // --- Drawing API (fluent) ---

        public IconCanvas Line(float x0, float y0, float x1, float y1)
        {
            DrawSegment(x0, y0, x1, y1);
            return this;
        }

        public IconCanvas Poly(params (float x, float y)[] pts)
        {
            for (int i = 0; i < pts.Length - 1; i++)
                DrawSegment(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y);
            return this;
        }

        public IconCanvas Closed(params (float x, float y)[] pts)
        {
            for (int i = 0; i < pts.Length; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Length];
                DrawSegment(a.x, a.y, b.x, b.y);
            }
            return this;
        }

        public IconCanvas Circle(float cx, float cy, float rx, float ry)
        {
            const int Steps = 32;
            for (int i = 0; i < Steps; i++)
            {
                float a0 = 2 * Mathf.PI * i / Steps;
                float a1 = 2 * Mathf.PI * (i + 1) / Steps;
                DrawSegment(
                    cx + rx * Mathf.Cos(a0), cy + ry * Mathf.Sin(a0),
                    cx + rx * Mathf.Cos(a1), cy + ry * Mathf.Sin(a1));
            }
            return this;
        }

        public IconCanvas Disc(float cx, float cy, float r)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r));
            int x1 = Mathf.Min(_size - 1, Mathf.CeilToInt(cx + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r));
            int y1 = Mathf.Min(_size - 1, Mathf.CeilToInt(cy + r));
            float r2 = r * r;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        SetPx(x, y);
                }
            return this;
        }

        // 2×2 filled square vertex marker at (x, y)–(x+1, y+1).
        public IconCanvas Dot(int x, int y)
        {
            SetPx(x,     y);
            SetPx(x + 1, y);
            SetPx(x,     y + 1);
            SetPx(x + 1, y + 1);
            return this;
        }

        // --- Terminals ---

        /// <summary>
        /// Returns raw pixel buffer WITHOUT uploading to GPU.
        /// Use in tests only. Layout: index = y * size + x, y=0 = bottom row.
        /// Binary alpha (no antialiasing) — all lit pixels have alpha=1.
        /// </summary>
        internal Color[] Bake() => _pixels;

        /// <summary>
        /// Creates Texture2D, uploads pixels, calls Apply(false, true) (non-readable).
        /// </summary>
        public Texture2D Build()
        {
            var tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags  = HideFlags.HideAndDontSave
            };
            tex.SetPixels(_pixels);
            tex.Apply(false, true);
            return tex;
        }

        // --- Private helpers ---

        private void DrawSegment(float x0, float y0, float x1, float y1)
        {
            float half = StrokeWidth * 0.5f;
            // Compute tight bounding box with padding for stroke radius.
            int bx0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - half));
            int bx1 = Mathf.Min(_size - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + half));
            int by0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - half));
            int by1 = Mathf.Min(_size - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + half));

            for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                    if (DistToSegment(x, y, x0, y0, x1, y1) <= half)
                        SetPx(x, y);
        }

        private static float DistToSegment(float px, float py,
                                           float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-10f) return Mathf.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / lenSq);
            float cx = ax + t * dx, cy = ay + t * dy;
            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }

        // Clamp to content box [1, size-2] — border row/col is always transparent (design system margin).
        // Disc() and Dot() call this so they also respect the border.
        private void SetPx(int x, int y)
        {
            if (x < 1 || x > _size - 2 || y < 1 || y > _size - 2) return;
            _pixels[y * _size + x] = _ink;
        }
    }
}
