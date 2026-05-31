using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>Pixel-level drawing utilities for Texture2D overlays.</summary>
    internal static class OverlayDrawer
    {
        /// <summary>Bounds-checked pixel write.</summary>
        internal static void SetSafe(Texture2D tex, int x, int y, Color c)
        {
            if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                tex.SetPixel(x, y, c);
        }

        internal static void HLine(Texture2D t, int x, int y, int len, Color c)
        { for (int i = 0; i < len; i++) SetSafe(t, x + i, y, c); }

        internal static void VLine(Texture2D t, int x, int y, int len, Color c)
        { for (int i = 0; i < len; i++) SetSafe(t, x, y + i, c); }

        /// <summary>Minimal 5x7 pixel font for F/L/T/I view labels.</summary>
        internal static void DrawCharBlock(Texture2D tex, char c, int x, int y)
        {
            var bg = new Color(0.2f, 0.2f, 0.2f);
            var fg = Color.white;
            for (int dy = 0; dy < 9; dy++)
            for (int dx = 0; dx < 7; dx++)
                SetSafe(tex, x + dx, y + dy, bg);

            switch (c)
            {
                case 'F': HLine(tex,x+1,y+6,5,fg); HLine(tex,x+1,y+3,4,fg); VLine(tex,x+1,y+0,7,fg); break;
                case 'L': VLine(tex,x+1,y+0,7,fg); HLine(tex,x+1,y+0,5,fg); break;
                case 'T': HLine(tex,x+1,y+6,5,fg); VLine(tex,x+3,y+0,7,fg); break;
                case 'I': HLine(tex,x+1,y+6,5,fg); HLine(tex,x+1,y+0,5,fg); VLine(tex,x+3,y+0,7,fg); break;
            }
        }

        /// <summary>Bresenham line with optional thickness.</summary>
        internal static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color color, int thickness = 1)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int half = thickness / 2;

            while (true)
            {
                for (int ty = -half; ty <= half; ty++)
                for (int tx = -half; tx <= half; tx++)
                    SetSafe(tex, x0 + tx, y0 + ty, color);

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { if (x0 == x1) break; err += dy; x0 += sx; }
                if (e2 <= dx) { if (y0 == y1) break; err += dx; y0 += sy; }
            }
        }

        /// <summary>Wireframe rectangle (bottom-left origin). Y=0 at bottom.</summary>
        internal static void DrawRect(Texture2D tex, int x, int y, int w, int h, Color color, int thickness = 1)
        {
            int x1 = x + w - 1, y1 = y + h - 1;
            DrawLine(tex, x,  y,  x1, y,  color, thickness); // bottom
            DrawLine(tex, x,  y1, x1, y1, color, thickness); // top
            DrawLine(tex, x,  y,  x,  y1, color, thickness); // left
            DrawLine(tex, x1, y,  x1, y1, color, thickness); // right
        }

        /// <summary>Filled rectangle.</summary>
        internal static void FillRect(Texture2D tex, int x, int y, int w, int h, Color color)
        {
            for (int row = y; row < y + h; row++)
            for (int col = x; col < x + w; col++)
                SetSafe(tex, col, row, color);
        }

        // Cell offset arrays: 0=FRONT(TL), 1=LEFT(TR), 2=TOP(BL), 3=ISO(BR)
        private static readonly int[] CellOX = { 0, 1, 0, 1 };
        private static readonly int[] CellOY = { 1, 1, 0, 0 };

        /// <summary>Project world point to pixel coords in a single cell (ortho camera).</summary>
        internal static Vector2 ProjectToPixel(Vector3 worldPoint, Matrix4x4 worldToLocal, float halfOrtho, int cellSize)
        {
            var local = worldToLocal.MultiplyPoint3x4(worldPoint);
            return new Vector2(
                (local.x + halfOrtho) / (2f * halfOrtho) * cellSize,
                (local.y + halfOrtho) / (2f * halfOrtho) * cellSize);
        }

        /// <summary>Draw collider wireframes (Box, Sphere, Capsule) projected onto texture.</summary>
        internal static void DrawColliderShapes(
            Texture2D composite, int cellSize,
            CamState cam, int viewIndex,
            List<(GameObject go, Color color)> objs)
            => DrawColliderShapes(composite, cellSize, cam, CellOX[viewIndex] * cellSize, CellOY[viewIndex] * cellSize, objs);

        internal static void DrawColliderShapes(
            Texture2D composite, int cellSize,
            CamState cam, int ox, int oy,
            List<(GameObject go, Color color)> objs)
        {
            var worldToLocal = Matrix4x4.TRS(cam.Position, cam.Rotation, Vector3.one).inverse;
            float hs = cam.OrthoSize;
            var cyan = new Color(0f, 1f, 1f);

            foreach (var (go, color) in objs)
            {
                var c = Color.Lerp(color, cyan, 0.5f);
                foreach (var col in go.GetComponentsInChildren<Collider>())
                {
                    if (col is BoxCollider box)
                        DrawBoxCollider(composite, cellSize, ox, oy, worldToLocal, hs, box, c);
                    else if (col is SphereCollider sphere)
                        DrawSphereCollider(composite, cellSize, ox, oy, worldToLocal, hs, sphere, c);
                    else if (col is CapsuleCollider capsule)
                        DrawCapsuleCollider(composite, cellSize, ox, oy, worldToLocal, hs, capsule, c);
                }
            }
        }

        private static void DrawBoxCollider(Texture2D tex, int cs, int ox, int oy,
            Matrix4x4 w2l, float hs, BoxCollider box, Color c)
        {
            var m = box.transform.localToWorldMatrix;
            var center = box.center;
            var half = box.size * 0.5f;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = m.MultiplyPoint3x4(center + new Vector3(
                    (i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z));
            int[][] edges = { new[]{0,1}, new[]{2,3}, new[]{4,5}, new[]{6,7},
                              new[]{0,2}, new[]{1,3}, new[]{4,6}, new[]{5,7},
                              new[]{0,4}, new[]{1,5}, new[]{2,6}, new[]{3,7} };
            foreach (var e in edges)
            {
                var a = ProjectToPixel(corners[e[0]], w2l, hs, cs);
                var b = ProjectToPixel(corners[e[1]], w2l, hs, cs);
                DrawLine(tex, ox+(int)a.x, oy+(int)a.y, ox+(int)b.x, oy+(int)b.y, c, 1);
            }
        }

        private static void DrawSphereCollider(Texture2D tex, int cs, int ox, int oy,
            Matrix4x4 w2l, float hs, SphereCollider sphere, Color c)
        {
            var worldCenter = sphere.transform.TransformPoint(sphere.center);
            var scale = sphere.transform.lossyScale;
            float worldRadius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var cp = ProjectToPixel(worldCenter, w2l, hs, cs);
            float pixelRadius = worldRadius / (2f * hs) * cs;
            int seg = 32;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                int x0 = ox + (int)(cp.x + Mathf.Cos(a0) * pixelRadius);
                int y0 = oy + (int)(cp.y + Mathf.Sin(a0) * pixelRadius);
                int x1 = ox + (int)(cp.x + Mathf.Cos(a1) * pixelRadius);
                int y1 = oy + (int)(cp.y + Mathf.Sin(a1) * pixelRadius);
                DrawLine(tex, x0, y0, x1, y1, c, 1);
            }
        }

        private static void DrawCapsuleCollider(Texture2D tex, int cs, int ox, int oy,
            Matrix4x4 w2l, float hs, CapsuleCollider capsule, Color c)
        {
            var t = capsule.transform;
            var worldCenter = t.TransformPoint(capsule.center);
            var scale = t.lossyScale;
            float r = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfH = Mathf.Max(capsule.height * 0.5f * Mathf.Abs(scale.y) - r, 0f);
            var up = t.up;
            var top = worldCenter + up * halfH;
            var bot = worldCenter - up * halfH;
            var tp = ProjectToPixel(top, w2l, hs, cs);
            var bp = ProjectToPixel(bot, w2l, hs, cs);
            float pr = r / (2f * hs) * cs;
            // Two circles + connecting lines
            int seg = 32;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                DrawLine(tex, ox+(int)(tp.x+Mathf.Cos(a0)*pr), oy+(int)(tp.y+Mathf.Sin(a0)*pr),
                              ox+(int)(tp.x+Mathf.Cos(a1)*pr), oy+(int)(tp.y+Mathf.Sin(a1)*pr), c, 1);
                DrawLine(tex, ox+(int)(bp.x+Mathf.Cos(a0)*pr), oy+(int)(bp.y+Mathf.Sin(a0)*pr),
                              ox+(int)(bp.x+Mathf.Cos(a1)*pr), oy+(int)(bp.y+Mathf.Sin(a1)*pr), c, 1);
            }
            // 4 connecting lines at ±X, ±Y offsets
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                float dx = Mathf.Cos(a) * pr, dy = Mathf.Sin(a) * pr;
                DrawLine(tex, ox+(int)(tp.x+dx), oy+(int)(tp.y+dy),
                              ox+(int)(bp.x+dx), oy+(int)(bp.y+dy), c, 1);
            }
        }

        /// <summary>Project highlight object bounds into cell pixel coords and draw wireframe boxes.</summary>
        internal static void DrawBoundingBoxes(
            Texture2D composite, int cellSize,
            CamState cam, int viewIndex,
            List<(GameObject go, Color color)> objs)
            => DrawBoundingBoxes(composite, cellSize, cam, CellOX[viewIndex] * cellSize, CellOY[viewIndex] * cellSize, objs);

        internal static void DrawBoundingBoxes(
            Texture2D composite, int cellSize,
            CamState cam, int ox, int oy,
            List<(GameObject go, Color color)> objs)
        {

            var worldToLocal = Matrix4x4.TRS(cam.Position, cam.Rotation, Vector3.one).inverse;
            float hs = cam.OrthoSize;

            foreach (var (go, color) in objs)
            {
                var bounds = MultiViewCapture.ComputeBounds(go);
                if (MultiViewOverlay.Classify(cam, bounds) != VisState.Visible) continue;

                var min = bounds.min;
                var max = bounds.max;
                float pxMin = float.MaxValue, pxMax = float.MinValue;
                float pyMin = float.MaxValue, pyMax = float.MinValue;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);
                    var local = worldToLocal.MultiplyPoint3x4(corner);
                    float px = (local.x + hs) / (2f * hs) * cellSize;
                    float py = (local.y + hs) / (2f * hs) * cellSize;
                    if (px < pxMin) pxMin = px;
                    if (px > pxMax) pxMax = px;
                    if (py < pyMin) pyMin = py;
                    if (py > pyMax) pyMax = py;
                }

                int rx = Mathf.Clamp(Mathf.RoundToInt(pxMin), 0, cellSize - 1);
                int ry = Mathf.Clamp(Mathf.RoundToInt(pyMin), 0, cellSize - 1);
                int rw = Mathf.Clamp(Mathf.RoundToInt(pxMax - pxMin), 1, cellSize - rx);
                int rh = Mathf.Clamp(Mathf.RoundToInt(pyMax - pyMin), 1, cellSize - ry);

                DrawRect(composite, ox + rx, oy + ry, rw, rh, color, 2);
            }
        }
    }
}

