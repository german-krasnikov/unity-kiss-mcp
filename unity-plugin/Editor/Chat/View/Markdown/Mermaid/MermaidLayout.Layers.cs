// Layer assignment and within-layer ordering (partial). Pure, NUnit-testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static partial class MermaidLayout
    {
        // Kahn topological sort + longest-path rank.
        // Self-loops excluded from in-degree so they never block a node.
        // Cycle members (never reach in-degree 0) are assigned max_layer + 1.
        // Visited set prevents infinite loops if queue ever re-enqueues a processed node.
        internal static Dictionary<string, int> AssignLayers(MermaidGraph g)
        {
            var inDegree = new Dictionary<string, int>();
            var succs    = new Dictionary<string, List<string>>();

            foreach (var n in g.Nodes) { inDegree[n.Id] = 0; succs[n.Id] = new List<string>(); }

            foreach (var e in g.Edges)
            {
                if (e.From == e.To) continue; // self-loop: skip
                if (!inDegree.ContainsKey(e.From)) { inDegree[e.From] = 0; succs[e.From] = new List<string>(); }
                if (!inDegree.ContainsKey(e.To))   { inDegree[e.To]   = 0; succs[e.To]   = new List<string>(); }
                inDegree[e.To]++;
                succs[e.From].Add(e.To);
            }

            var layers  = new Dictionary<string, int>();
            var queue   = new Queue<string>();
            var visited = new HashSet<string>(); // cycle safety: never process a node twice

            foreach (var kv in inDegree)
                if (kv.Value == 0) { queue.Enqueue(kv.Key); layers[kv.Key] = 0; }

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!visited.Add(id)) continue;

                foreach (var next in succs[id])
                {
                    int proposed = layers[id] + 1;
                    if (!layers.ContainsKey(next) || layers[next] < proposed)
                        layers[next] = proposed;
                    inDegree[next]--;
                    if (inDegree[next] == 0) queue.Enqueue(next);
                }
            }

            // Assign cycle members that were never reached
            int maxLayer = 0;
            foreach (var kv in layers) if (kv.Value > maxLayer) maxLayer = kv.Value;
            foreach (var n in g.Nodes)
                if (!layers.ContainsKey(n.Id)) layers[n.Id] = maxLayer + 1;

            return layers;
        }

        internal static void PlaceInLayers(List<MermaidNode> nodes, Dictionary<string, int> layers)
        {
            var counter = new Dictionary<int, int>();
            foreach (var n in nodes)
            {
                int layer = layers[n.Id];
                n.Layer = layer;
                if (!counter.ContainsKey(layer)) counter[layer] = 0;
                n.Order = counter[layer]++;
            }
        }
    }
}
