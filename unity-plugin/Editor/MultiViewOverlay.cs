using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal enum VisState { Visible, OffLeft, OffRight, OffTop, OffBottom, Behind }

    /// <summary>Snapshot of a single view camera state (immutable).</summary>
    internal readonly struct CamState
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float OrthoSize;
        public CamState(Vector3 pos, Quaternion rot, float orthoSize)
        { Position = pos; Rotation = rot; OrthoSize = orthoSize; }
    }

    internal static class MultiViewOverlay
    {
        private static readonly string[] ViewNames = { "FRONT", "LEFT", "TOP", "ISO" };
        private static readonly char[]   ViewLabels = { 'F', 'L', 'T', 'I' };

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        /// <summary>Parse "path,path:#RRGGBB,..." → list of (go, color), cap 8, skip missing.</summary>
        internal static List<(GameObject go, Color color)> ParseHighlight(string highlight)
        {
            var result = new List<(GameObject, Color)>();
            if (string.IsNullOrEmpty(highlight)) return result;

            var entries = highlight.Split(',');
            foreach (var entry in entries)
            {
                if (result.Count >= 8) break;
                var trimmed = entry.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string path = trimmed;
                Color color = Color.yellow;

                // split on last ':' only if it looks like a color hex
                int colon = trimmed.LastIndexOf(":#", StringComparison.Ordinal);
                if (colon >= 0)
                {
                    path = trimmed.Substring(0, colon);
                    var hex = trimmed.Substring(colon + 2); // skip :#
                    if (!TryParseHex(hex, out color)) color = Color.yellow;
                }

                var go = ComponentSerializer.FindObject(path);
                if (go == null) continue;
                result.Add((go, color));
            }
            return result;
        }

        /// <summary>Classify object visibility for a single orthographic camera snapshot.</summary>
        internal static VisState Classify(CamState cam, Bounds worldBounds)
        {
            // Build worldToLocal matrix from snapshot (no live Camera needed)
            var worldToLocal = Matrix4x4.TRS(cam.Position, cam.Rotation, Vector3.one).inverse;
            var min = worldBounds.min;
            var max = worldBounds.max;

            float bMinX = float.MaxValue, bMaxX = float.MinValue;
            float bMinY = float.MaxValue, bMaxY = float.MinValue;
            bool anyFront = false;

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                var local = worldToLocal.MultiplyPoint3x4(corner);
                // Unity camera: forward = local +Z, but objects in front have local.z > 0
                // (Unity's camera looks down -Z in world, but worldToLocal flips it)
                if (local.z > 0) anyFront = true;
                if (local.x < bMinX) bMinX = local.x;
                if (local.x > bMaxX) bMaxX = local.x;
                if (local.y < bMinY) bMinY = local.y;
                if (local.y > bMaxY) bMaxY = local.y;
            }

            if (!anyFront) return VisState.Behind;

            float hs = cam.OrthoSize;
            bool overlapX = bMinX <= hs && bMaxX >= -hs;
            bool overlapY = bMinY <= hs && bMaxY >= -hs;
            if (overlapX && overlapY) return VisState.Visible;

            float cx = (bMinX + bMaxX) * 0.5f;
            float cy = (bMinY + bMaxY) * 0.5f;
            if (Mathf.Abs(cx) >= Mathf.Abs(cy))
                return cx > 0 ? VisState.OffRight : VisState.OffLeft;
            return cy > 0 ? VisState.OffTop : VisState.OffBottom;
        }

        /// <summary>Build compact manifest: "FRONT:Obj(vis),Enemy(off-R)\nLEFT:..."</summary>
        internal static string BuildManifest(
            CamState[] cams, List<(GameObject go, Color color)> objs)
        {
            var sb = new StringBuilder();
            int viewCount = Mathf.Min(cams.Length, 4);
            for (int v = 0; v < viewCount; v++)
            {
                sb.Append(ViewNames[v]).Append(':');
                for (int o = 0; o < objs.Count; o++)
                {
                    if (o > 0) sb.Append(',');
                    var bounds = MultiViewCapture.ComputeBounds(objs[o].go);
                    var vis = Classify(cams[v], bounds);
                    sb.Append(objs[o].go.name).Append('(').Append(StateLabel(vis)).Append(')');
                }
                if (v < viewCount - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        // Cell origins for a 2x2 grid (bottom-left of each quadrant, Y=0 at bottom)
        /// <summary>Replace 1px gray dividers with 5px styled lines (1 black + 3 white + 1 black).</summary>
        internal static void DrawSeparators(Texture2D composite, int cellSize, int gridSize)
        {
            var black = Color.black;
            var white = Color.white;
            int mid = cellSize;

            for (int p = 0; p < gridSize; p++)
            {
                // vertical line at x = mid
                composite.SetPixel(mid - 2, p, black);
                composite.SetPixel(mid - 1, p, white);
                composite.SetPixel(mid,     p, white);
                composite.SetPixel(mid + 1, p, white);
                composite.SetPixel(mid + 2, p, black);
                // horizontal line at y = mid
                composite.SetPixel(p, mid - 2, black);
                composite.SetPixel(p, mid - 1, white);
                composite.SetPixel(p, mid,     white);
                composite.SetPixel(p, mid + 1, white);
                composite.SetPixel(p, mid + 2, black);
            }
        }

        /// <summary>Draw F/L/T/I labels in each quadrant corner using 5x7 pixel blocks.</summary>
        internal static void DrawLabels(Texture2D composite, int cellSize)
        {
            // Quadrant origins (bottom-left of each cell in Texture2D coords, Y=0 at bottom)
            int[] ox = { 0, cellSize, 0, cellSize };
            int[] oy = { cellSize, cellSize, 0, 0 };
            int inset = 4;

            for (int i = 0; i < 4; i++)
            {
                char label = ViewLabels[i];
                int lx = ox[i] + inset;
                int ly = oy[i] + inset; // bottom of label block (Y=0 at bottom)
                OverlayDrawer.DrawCharBlock(composite, label, lx, ly);
            }
        }

        // ------------------------------------------------------------------ //
        // Private helpers
        // ------------------------------------------------------------------ //

        private static string StateLabel(VisState s) => s switch
        {
            VisState.Visible    => "vis",
            VisState.OffLeft    => "off-L",
            VisState.OffRight   => "off-R",
            VisState.OffTop     => "off-T",
            VisState.OffBottom  => "off-B",
            VisState.Behind     => "behind",
            _ => "?"
        };

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Color.yellow;
            if (hex.Length != 6) return false;
            try
            {
                float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                color = new Color(r, g, b);
                return true;
            }
            catch { return false; }
        }

    }
}

