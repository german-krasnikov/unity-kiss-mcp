// Layout value types for MermaidLayout. Plain floats, no UnityEngine deps. Pure.
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
}
