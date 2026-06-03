// Assigns pixel rects to a MermaidGraph. Plain floats, no Vector2. Pure, NUnit-testable.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public readonly struct NodeRect
    {
        public string Id { get; }
        public float  X  { get; }
        public float  Y  { get; }
        public float  W  { get; }
        public float  H  { get; }
        public NodeRect(string id, float x, float y, float w, float h)
        { Id = id; X = x; Y = y; W = w; H = h; }
        public override string ToString() => $"{Id}({X},{Y},{W},{H})";
    }

    public readonly struct EdgeLine
    {
        public float  X1     { get; }
        public float  Y1     { get; }
        public float  X2     { get; }
        public float  Y2     { get; }
        public bool   Arrow  { get; }
        public string Label  { get; }
        public float  LabelX { get; }
        public float  LabelY { get; }
        public EdgeLine(float x1, float y1, float x2, float y2,
                        bool arrow, string label, float lx, float ly)
        { X1=x1; Y1=y1; X2=x2; Y2=y2; Arrow=arrow; Label=label; LabelX=lx; LabelY=ly; }
    }

    public class LayoutResult
    {
        public float           Width  { get; set; }
        public float           Height { get; set; }
        public List<NodeRect>  Nodes  { get; } = new List<NodeRect>();
        public List<EdgeLine>  Edges  { get; } = new List<EdgeLine>();
    }

    public static partial class MermaidLayout
    {
        /// <summary>Computes pixel layout. Safe against cycles and self-loops.</summary>
        public static LayoutResult Compute(MermaidGraph g,
            float nodeW = 120f, float nodeH = 40f, float gap = 28f)
        {
            var layers = AssignLayers(g);
            PlaceInLayers(g.Nodes, layers);
            return BuildResult(g, layers, nodeW, nodeH, gap);
        }

        private static LayoutResult BuildResult(MermaidGraph g,
            Dictionary<string, int> layers, float nodeW, float nodeH, float gap)
        {
            var result   = new LayoutResult();
            var rectById = new Dictionary<string, NodeRect>();

            var layerCount = new Dictionary<int, int>();
            foreach (var n in g.Nodes)
                layerCount[n.Layer] = layerCount.ContainsKey(n.Layer) ? layerCount[n.Layer] + 1 : 1;

            int maxLayer = 0;
            foreach (var kv in layerCount) if (kv.Key > maxLayer) maxLayer = kv.Key;
            int maxNodes = 1;
            foreach (var kv in layerCount) if (kv.Value > maxNodes) maxNodes = kv.Value;

            bool isHorizontal = g.Dir == MermaidDir.LR || g.Dir == MermaidDir.RL;
            float layerStep = isHorizontal ? nodeW + gap : nodeH + gap;
            float orderStep = isHorizontal ? nodeH + gap : nodeW + gap;

            foreach (var n in g.Nodes)
            {
                float cross = (maxNodes - layerCount[n.Layer]) * orderStep * 0.5f;
                float x, y;
                switch (g.Dir)
                {
                    case MermaidDir.TD: x = cross + n.Order * orderStep; y = n.Layer * layerStep; break;
                    case MermaidDir.BT: x = cross + n.Order * orderStep; y = (maxLayer - n.Layer) * layerStep; break;
                    case MermaidDir.LR: x = n.Layer * layerStep; y = cross + n.Order * orderStep; break;
                    case MermaidDir.RL: x = (maxLayer - n.Layer) * layerStep; y = cross + n.Order * orderStep; break;
                    default:            x = n.Order * orderStep; y = n.Layer * layerStep; break;
                }
                var nr = new NodeRect(n.Id, x, y, nodeW, nodeH);
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
