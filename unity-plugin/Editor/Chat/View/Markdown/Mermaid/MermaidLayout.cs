// Assigns pixel rects to a MermaidGraph. Plain floats, no Vector2. Pure, NUnit-testable.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static partial class MermaidLayout
    {
        private const float CharWidth  = 7f;
        private const float LineHeight = 16f;
        private const float PadX       = 20f;
        private const float PadY       = 12f;
        private const float MinW       = 60f;
        private const float MaxW       = 280f;
        private const float MinH       = 30f;
        private const float MaxH       = 120f;

        /// <summary>Estimates node pixel size from its label text. Pure.</summary>
        internal static (float w, float h) MeasureNode(string label)
        {
            if (string.IsNullOrEmpty(label))
                return (MinW, MinH);

            var lines = label.Split('\n');
            int maxChars = 0;
            foreach (var line in lines)
                if (line.Length > maxChars) maxChars = line.Length;

            float w = Math.Min(Math.Max(maxChars * CharWidth + PadX, MinW), MaxW);
            float h = Math.Min(Math.Max(lines.Length * LineHeight + PadY, MinH), MaxH);
            return (w, h);
        }

        /// <summary>Computes pixel layout. Safe against cycles and self-loops.</summary>
        public static LayoutResult Compute(MermaidGraph g, float gap = 28f)
        {
            var layers = AssignLayers(g);
            PlaceInLayers(g.Nodes, layers);

            var sizes = new Dictionary<string, (float w, float h)>(g.Nodes.Count);
            foreach (var n in g.Nodes)
                sizes[n.Id] = MeasureNode(n.Label);

            return BuildResult(g, layers, sizes, gap);
        }

        private static LayoutResult BuildResult(MermaidGraph g,
            Dictionary<string, int> layers,
            Dictionary<string, (float w, float h)> sizes, float gap)
        {
            var result   = new LayoutResult();
            var rectById = new Dictionary<string, NodeRect>();

            // Group nodes by layer, compute per-layer max dimensions
            var layerNodes = new Dictionary<int, List<MermaidNode>>();
            var layerMaxW  = new Dictionary<int, float>();
            var layerMaxH  = new Dictionary<int, float>();
            foreach (var n in g.Nodes)
            {
                if (!layerNodes.ContainsKey(n.Layer))
                {
                    layerNodes[n.Layer] = new List<MermaidNode>();
                    layerMaxW[n.Layer] = 0f;
                    layerMaxH[n.Layer] = 0f;
                }
                layerNodes[n.Layer].Add(n);
                var (w, h) = sizes[n.Id];
                if (w > layerMaxW[n.Layer]) layerMaxW[n.Layer] = w;
                if (h > layerMaxH[n.Layer]) layerMaxH[n.Layer] = h;
            }

            bool isHz = g.Dir == MermaidDir.LR || g.Dir == MermaidDir.RL;

            // Cumulative layer offsets
            int maxLayer = 0;
            foreach (var kv in layerNodes) if (kv.Key > maxLayer) maxLayer = kv.Key;

            var layerOffset = new Dictionary<int, float>();
            float cumOffset = 0f;
            for (int L = 0; L <= maxLayer; L++)
            {
                layerOffset[L] = cumOffset;
                float extent = isHz
                    ? (layerMaxW.ContainsKey(L) ? layerMaxW[L] : MinW)
                    : (layerMaxH.ContainsKey(L) ? layerMaxH[L] : MinH);
                cumOffset += extent + gap;
            }

            // Global max cross-axis dimension for uniform order-step
            float maxCross = 0f;
            foreach (var n in g.Nodes)
            {
                var (w, h) = sizes[n.Id];
                float cross = isHz ? h : w;
                if (cross > maxCross) maxCross = cross;
            }
            float orderStep = maxCross + gap;

            // Max nodes in any layer (for centering)
            int maxNodesInAnyLayer = 1;
            foreach (var kv in layerNodes)
                if (kv.Value.Count > maxNodesInAnyLayer) maxNodesInAnyLayer = kv.Value.Count;

            foreach (var n in g.Nodes)
            {
                var (nw, nh) = sizes[n.Id];
                int layerSize     = layerNodes[n.Layer].Count;
                float crossCenter = (maxNodesInAnyLayer - layerSize) * orderStep * 0.5f;
                float nodeSize    = isHz ? nh : nw;
                float crossSlot   = (orderStep - nodeSize) * 0.5f;
                float layerPos    = layerOffset[n.Layer];
                float layerExtent = isHz ? layerMaxW[n.Layer] : layerMaxH[n.Layer];
                float layerCenter = (layerExtent - (isHz ? nw : nh)) * 0.5f;

                float x, y;
                switch (g.Dir)
                {
                    case MermaidDir.TD:
                        x = crossCenter + n.Order * orderStep + crossSlot;
                        y = layerPos + layerCenter;
                        break;
                    case MermaidDir.BT:
                        x = crossCenter + n.Order * orderStep + crossSlot;
                        y = (cumOffset - gap) - layerPos - nh + layerCenter;
                        break;
                    case MermaidDir.LR:
                        x = layerPos + layerCenter;
                        y = crossCenter + n.Order * orderStep + crossSlot;
                        break;
                    case MermaidDir.RL:
                        x = (cumOffset - gap) - layerPos - nw + layerCenter;
                        y = crossCenter + n.Order * orderStep + crossSlot;
                        break;
                    default:
                        x = n.Order * orderStep;
                        y = layerPos;
                        break;
                }
                var nr = new NodeRect(n.Id, x, y, nw, nh);
                rectById[n.Id] = nr;
                result.Nodes.Add(nr);
            }

            float maxX = 0f, maxY = 0f;
            foreach (var nr in result.Nodes)
            {
                if (nr.X + nr.W > maxX) maxX = nr.X + nr.W;
                if (nr.Y + nr.H > maxY) maxY = nr.Y + nr.H;
            }
            result.Width  = maxX;
            result.Height = maxY;

            foreach (var e in g.Edges)
            {
                if (!rectById.ContainsKey(e.From) || !rectById.ContainsKey(e.To)) continue;
                var from = rectById[e.From];
                var to   = rectById[e.To];
                var (x1, y1) = BorderPoint(from, CX(to),   CY(to));
                var (x2, y2) = BorderPoint(to,   CX(from), CY(from));
                result.Edges.Add(new EdgeLine(x1, y1, x2, y2, e.Arrow, e.Label,
                    (x1+x2)*0.5f, (y1+y2)*0.5f));
            }

            return result;
        }

        private static float CX(NodeRect r) => r.X + r.W * 0.5f;
        private static float CY(NodeRect r) => r.Y + r.H * 0.5f;

        // Returns the point on rect's border closest to direction of (tx,ty).
        private static (float x, float y) BorderPoint(NodeRect rect, float tx, float ty)
        {
            float cx = CX(rect), cy = CY(rect);
            float dx = tx - cx,  dy = ty - cy;
            if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                return (rect.X + rect.W, cy); // self-loop: right border
            float sx = Math.Abs(dx) > 0.001f ? rect.W * 0.5f / Math.Abs(dx) : float.MaxValue;
            float sy = Math.Abs(dy) > 0.001f ? rect.H * 0.5f / Math.Abs(dy) : float.MaxValue;
            float s  = Math.Min(sx, sy);
            return (cx + dx * s, cy + dy * s);
        }
    }
}
