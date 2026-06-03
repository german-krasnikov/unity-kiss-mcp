// Pure POCO model for a parsed Mermaid flowchart. NUnit-testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public enum MermaidDir { TD, LR, RL, BT }

    public enum NodeShape { Rect, Round, Diamond }

    public class MermaidNode
    {
        public string    Id    { get; set; }
        public string    Label { get; set; }
        public NodeShape Shape { get; set; }
        public int       Layer { get; set; }  // assigned by layout
        public int       Order { get; set; }  // position within layer
    }

    public class MermaidEdge
    {
        public string From  { get; set; }
        public string To    { get; set; }
        public string Label { get; set; }
        public bool   Arrow { get; set; }
    }

    public class MermaidGraph
    {
        public MermaidDir       Dir   { get; set; }
        public List<MermaidNode> Nodes { get; } = new List<MermaidNode>();
        public List<MermaidEdge> Edges { get; } = new List<MermaidEdge>();

        /// <summary>Adds node if id not yet present (first-definition-wins).</summary>
        public MermaidNode GetOrAdd(string id, string label, NodeShape shape)
        {
            foreach (var n in Nodes)
                if (n.Id == id) return n;
            var node = new MermaidNode { Id = id, Label = label, Shape = shape };
            Nodes.Add(node);
            return node;
        }

        /// <summary>Ensures a bare-id node exists; no-op if already defined.</summary>
        public MermaidNode EnsureNode(string id)
        {
            foreach (var n in Nodes)
                if (n.Id == id) return n;
            var node = new MermaidNode { Id = id, Label = id, Shape = NodeShape.Rect };
            Nodes.Add(node);
            return node;
        }
    }
}
