// Parses Mermaid flowchart text into a MermaidGraph model. Pure, NUnit-testable.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    public static class MermaidParser
    {
        // Node definition: id[label], id(label), id{label}
        private static readonly Regex _nodeDef = new Regex(
            @"^([A-Za-z0-9_]+)(\[([^\]]*)\]|\(([^)]*)\)|\{([^}]*)\})$",
            RegexOptions.Compiled);

        // Edge: A-->B, A---B, A-->|label|B, A-- text -->B  (possibly chained)
        // We split on -->  or --- tokens, capturing optional |label| after arrow.
        private static readonly Regex _edgeSplit = new Regex(
            @"(-->|---)", RegexOptions.Compiled);

        // Pipe label after arrow: -->|label|NodeId
        private static readonly Regex _pipeLabel = new Regex(
            @"^\|(.+?)\|(.+)$", RegexOptions.Compiled);

        /// <summary>
        /// Parses mermaid flowchart lines. Returns null for non-flowchart diagrams or empty input.
        /// </summary>
        public static MermaidGraph TryParse(string[] lines)
        {
            if (lines == null || lines.Length == 0) return null;

            // Find first non-empty line — must be graph/flowchart directive
            int start = 0;
            while (start < lines.Length && string.IsNullOrWhiteSpace(lines[start]))
                start++;
            if (start >= lines.Length) return null;

            var header = lines[start].Trim();
            var dir    = ParseHeader(header);
            if (dir == null) return null;

            var graph = new MermaidGraph { Dir = dir.Value };

            for (int i = start + 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                ParseLine(line, graph);
            }

            return graph;
        }

        private static MermaidDir? ParseHeader(string header)
        {
            // "graph TD|TB|LR|RL|BT" or "flowchart ..."
            string[] parts = header.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) return null;

            var keyword = parts[0].ToLowerInvariant();
            if (keyword != "graph" && keyword != "flowchart") return null;

            var dirToken = parts.Length > 1 ? parts[1].Trim().ToUpperInvariant() : "TD";
            switch (dirToken)
            {
                case "TD": case "TB": return MermaidDir.TD;
                case "LR":            return MermaidDir.LR;
                case "RL":            return MermaidDir.RL;
                case "BT":            return MermaidDir.BT;
                default:              return MermaidDir.TD;
            }
        }

        private static void ParseLine(string line, MermaidGraph graph)
        {
            // Detect if this line contains an edge operator
            if (line.Contains("-->") || line.Contains("---"))
            {
                ParseEdgeLine(line, graph);
                return;
            }

            // Try pure node definition
            var nodeMatch = _nodeDef.Match(line);
            if (nodeMatch.Success)
            {
                ParseNodeDef(nodeMatch, graph);
                return;
            }

            // Bare id (no brackets) — ensure node exists
            if (IsValidId(line))
                graph.EnsureNode(line);
        }

        private static void ParseNodeDef(Match m, MermaidGraph graph)
        {
            var id      = m.Groups[1].Value;
            var bracket = m.Groups[2].Value;
            string label;
            NodeShape shape;

            if (bracket.StartsWith("["))      { shape = NodeShape.Rect;    label = m.Groups[3].Value; }
            else if (bracket.StartsWith("(")) { shape = NodeShape.Round;   label = m.Groups[4].Value; }
            else                              { shape = NodeShape.Diamond; label = m.Groups[5].Value; }

            graph.GetOrAdd(id, NormalizeBr(label), shape);
        }

        private static void ParseEdgeLine(string line, MermaidGraph graph)
        {
            // Tokenise by splitting on --> / --- (delimiters kept), then walk chained segments.
            var parts = _edgeSplit.Split(line);
            if (parts.Length < 3) return;

            // Normalise first segment (trim inline -- text from end)
            string fromId = ExtractNodeId(parts[0].Trim(), graph);

            int p = 1;
            while (p + 1 < parts.Length)
            {
                var op  = parts[p].Trim();   // --> or ---
                var seg = parts[p + 1].Trim(); // may be "|label|nextId" or just "nextId"

                bool arrow = op == "-->";
                string label = null;
                string toId;

                // Check for pipe label: |label|NodeId
                var pipeMatch = _pipeLabel.Match(seg);
                if (pipeMatch.Success)
                {
                    label = NormalizeBr(pipeMatch.Groups[1].Value);
                    toId  = ExtractNodeId(pipeMatch.Groups[2].Value.Trim(), graph);
                }
                else
                {
                    toId = ExtractNodeId(seg, graph);
                }

                graph.EnsureNode(fromId);
                graph.EnsureNode(toId);

                graph.Edges.Add(new MermaidEdge
                {
                    From  = fromId,
                    To    = toId,
                    Arrow = arrow,
                    Label = label,
                });

                fromId = toId;
                p += 2;
            }
        }

        // Extracts just the node id from a segment that may have [label], (label), {label}
        // and registers the node if it has a definition.
        private static string ExtractNodeId(string seg, MermaidGraph graph)
        {
            // Strip trailing inline "-- text" (for "A-- text -->B" style)
            var dashIdx = seg.IndexOf("--");
            if (dashIdx > 0) seg = seg.Substring(0, dashIdx).Trim();

            var m = _nodeDef.Match(seg);
            if (m.Success)
            {
                ParseNodeDef(m, graph);
                return m.Groups[1].Value;
            }

            // Bare id
            if (IsValidId(seg))
            {
                graph.EnsureNode(seg);
                return seg;
            }

            return seg;
        }

        private static bool IsValidId(string s) =>
            !string.IsNullOrEmpty(s) && Regex.IsMatch(s, @"^[A-Za-z0-9_]+$");

        // Case-insensitive: <br>, <br/>, <br />, <BR/>, etc.
        private static readonly Regex _br = new Regex(
            @"<br\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static string NormalizeBr(string label) =>
            string.IsNullOrEmpty(label) ? label : _br.Replace(label, "\n");
    }
}
