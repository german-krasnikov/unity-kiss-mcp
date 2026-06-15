// UIToolkit VisualElement that renders a computed Mermaid layout.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class MermaidView : VisualElement
    {
        public MermaidView(MermaidGraph graph, LayoutResult layout)
        {
            AddToClassList("mermaid");

            // Size must be set explicitly or the element collapses.
            style.width  = layout.Width;
            style.height = layout.Height;
            style.position = Position.Relative;

            // Build lookups from node id to shape (styling) and label (text).
            var shapeById = new Dictionary<string, NodeShape>(graph.Nodes.Count);
            var labelById = new Dictionary<string, string>(graph.Nodes.Count);
            foreach (var n in graph.Nodes) { shapeById[n.Id] = n.Shape; labelById[n.Id] = n.Label; }

            // Render node boxes.
            foreach (var nr in layout.Nodes)
            {
                var nodeVe = new VisualElement();
                nodeVe.AddToClassList("mermaid-node");

                var shape = shapeById.ContainsKey(nr.Id) ? shapeById[nr.Id] : NodeShape.Rect;
                switch (shape)
                {
                    case NodeShape.Round:   nodeVe.AddToClassList("mermaid-node--round");   break;
                    case NodeShape.Diamond: nodeVe.AddToClassList("mermaid-node--diamond"); break;
                }

                nodeVe.style.position = Position.Absolute;
                nodeVe.style.left     = nr.X;
                nodeVe.style.top      = nr.Y;
                nodeVe.style.width    = nr.W;
                nodeVe.style.height   = nr.H;

                // NodeRect has no label field; look it up by id.
                string label = labelById.TryGetValue(nr.Id, out var l) ? l : nr.Id;

                var lbl = new Label(MarkdownInline.ToRichText(label));
                lbl.enableRichText = true;
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                nodeVe.Add(lbl);
                Add(nodeVe);
            }

            // Edge overlay — painted via generateVisualContent.
            var edgeLayer = new VisualElement();
            edgeLayer.AddToClassList("mermaid-edge-layer");
            edgeLayer.style.position    = Position.Absolute;
            edgeLayer.style.left        = 0;
            edgeLayer.style.top         = 0;
            edgeLayer.style.width       = layout.Width;
            edgeLayer.style.height      = layout.Height;
            edgeLayer.pickingMode       = PickingMode.Ignore;

            var capturedEdges = layout.Edges; // capture for lambda
            edgeLayer.generateVisualContent += ctx => MermaidEdgePainter.Paint(ctx, capturedEdges);

            // MANDATORY: repaint when geometry changes (avoids blank overlay on first layout).
            edgeLayer.RegisterCallback<GeometryChangedEvent>(_ => edgeLayer.MarkDirtyRepaint());

            Add(edgeLayer);

            // Edge labels (absolute Labels positioned at midpoint).
            foreach (var edge in layout.Edges)
            {
                if (string.IsNullOrEmpty(edge.Label)) continue;
                var edgeLbl = new Label(MarkdownInline.ToRichText(edge.Label));
                edgeLbl.enableRichText = true;
                edgeLbl.AddToClassList("mermaid-edge-label");
                edgeLbl.style.position = Position.Absolute;
                edgeLbl.style.left     = edge.LabelX;
                edgeLbl.style.top      = edge.LabelY;
                Add(edgeLbl);
            }
        }
    }
}
