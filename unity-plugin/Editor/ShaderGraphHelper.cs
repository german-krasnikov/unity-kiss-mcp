using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ShaderGraphHelper
    {
        public static string Get(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".shadergraph"))
                throw new ArgumentException($"Path must end with .shadergraph: {path}");
            if (!File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");

            var sb = new StringBuilder();
            sb.AppendLine($"ShaderGraph: {path}");

            // Use Unity API to get compiled shader info
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader != null)
            {
                sb.Append("compiled: ").AppendLine(shader.name);
                sb.Append("passes: ").AppendLine(shader.passCount.ToString());
                sb.Append("errors: ").AppendLine(ShaderUtil.ShaderHasError(shader) ? "yes" : "none");
            }

            // Parse raw file for graph structure — no public Unity API for nodes/edges
            var content = File.ReadAllText(path);
            var blocks = SplitBlocks(content);
            var byId = BuildIdMap(blocks);
            var root = FindRoot(blocks);
            if (root != null)
                AppendGraphStructure(sb, root, byId);

            return sb.ToString().TrimEnd();
        }

        public static string Create(string path, string preset)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".shadergraph"))
                throw new ArgumentException($"Path must end with .shadergraph: {path}");

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var templatePath = FindTemplate(preset);
            if (templatePath == null)
                throw new InvalidOperationException(
                    $"Template not found for preset '{preset}'. Available: unlit_graph, lit_graph");

            File.Copy(templatePath, path, overwrite: true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return Get(path);
        }

        static string FindTemplate(string preset)
        {
            var fileName = preset switch
            {
                "unlit_graph" => "Unlit Simple.shadergraph",
                "lit_graph"   => "0_Lit Basic.shadergraph",
                _ => throw new ArgumentException($"Unknown preset '{preset}'. Available: unlit_graph, lit_graph")
            };
            var pkg = ResolvePackage("com.unity.shadergraph");
            if (pkg == null) return null;
            var files = Directory.GetFiles(pkg, fileName, SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }

        static string ResolvePackage(string id)
        {
            var direct = Path.GetFullPath($"Packages/{id}");
            if (Directory.Exists(direct)) return direct;
            var cache = "Library/PackageCache";
            if (!Directory.Exists(cache)) return null;
            foreach (var dir in Directory.GetDirectories(cache, $"{id}*")) return dir;
            return null;
        }

        static void AppendGraphStructure(StringBuilder sb, string root, Dictionary<string, string> byId)
        {
            var nodeRefs = ExtractIdArray(root, "m_Nodes");
            sb.AppendLine($"nodes: {nodeRefs.Count}");
            foreach (var id in nodeRefs)
            {
                if (!byId.TryGetValue(id, out var blk)) { sb.AppendLine($"  [{id}] (missing)"); continue; }
                var type = ShortType(JsonHelper.ExtractString(blk, "m_Type") ?? "Unknown");
                var name = JsonHelper.ExtractString(blk, "m_Name") ?? "";
                var linked = JsonHelper.ExtractString(blk, "m_PropertyGuidSerialized");
                if (linked != null && byId.TryGetValue(linked, out var propBlk))
                    sb.AppendLine($"  [{id}] {type} \"{name}\" -> {JsonHelper.ExtractString(propBlk, "m_Name")}");
                else
                    sb.AppendLine($"  [{id}] {type} \"{name}\"");
            }

            var edges = ExtractEdges(root);
            sb.AppendLine($"edges: {edges.Count}");
            foreach (var e in edges) sb.AppendLine(e);

            var propRefs = ExtractIdArray(root, "m_Properties");
            sb.AppendLine($"properties: {propRefs.Count}");
            foreach (var id in propRefs)
            {
                if (!byId.TryGetValue(id, out var blk)) { sb.AppendLine($"  (missing {id})"); continue; }
                var pname = JsonHelper.ExtractString(blk, "m_Name") ?? id;
                var ptype = ShortType(JsonHelper.ExtractString(blk, "m_Type") ?? "");
                var refname = JsonHelper.ExtractString(blk, "m_DefaultReferenceName") ?? "";
                sb.AppendLine($"  {pname}: {ptype} ({refname})");
            }
        }

        // Splits content into top-level JSON objects {…}, optionally skipping string literals
        static List<string> SplitBlocks(string content, bool skipStrings = true)
        {
            var blocks = new List<string>();
            int depth = 0, start = -1;
            bool inStr = false;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (skipStrings && c == '"' && (i == 0 || content[i - 1] != '\\')) { inStr = !inStr; continue; }
                if (inStr) continue;
                if (c == '{') { if (depth == 0) start = i; depth++; }
                else if (c == '}') { depth--; if (depth == 0 && start >= 0) { blocks.Add(content.Substring(start, i - start + 1)); start = -1; } }
            }
            return blocks;
        }

        static Dictionary<string, string> BuildIdMap(List<string> blocks)
        {
            var map = new Dictionary<string, string>();
            foreach (var b in blocks) { var id = JsonHelper.ExtractString(b, "m_ObjectId"); if (id != null) map[id] = b; }
            return map;
        }

        static string FindRoot(List<string> blocks)
        {
            foreach (var b in blocks) if ((JsonHelper.ExtractString(b, "m_Type") ?? "").Contains("GraphData")) return b;
            return null;
        }

        static List<string> ExtractIdArray(string json, string key)
        {
            var ids = new List<string>();
            var arr = JsonHelper.ExtractArray(json, key);
            if (arr == "[]") return ids;
            for (int i = 0; i < arr.Length;)
            {
                var s = arr.IndexOf("\"m_Id\"", i); if (s < 0) break;
                var id = JsonHelper.ExtractString(arr.Substring(s), "m_Id");
                if (id != null) ids.Add(id);
                i = s + 6;
            }
            return ids;
        }

        static List<string> ExtractEdges(string root)
        {
            var edges = new List<string>();
            var arr = JsonHelper.ExtractArray(root, "m_Edges");
            if (arr == "[]") return edges;
            for (int i = 0; i < arr.Length;)
            {
                var outS = arr.IndexOf("\"m_OutputSlot\"", i);
                var inS  = arr.IndexOf("\"m_InputSlot\"", i);
                if (outS < 0 || inS < 0) break;
                var next = arr.IndexOf("\"m_OutputSlot\"", outS + 1);
                var o = ExtractSlotRef(arr, outS); var n = ExtractSlotRef(arr, inS);
                if (o != null && n != null) edges.Add($"  [{o.Item1}]:{o.Item2} -> [{n.Item1}]:{n.Item2}");
                i = next > 0 ? next : arr.Length;
            }
            return edges;
        }

        static Tuple<string, string> ExtractSlotRef(string json, int slotStart)
        {
            var brace = json.IndexOf('{', slotStart + 14); if (brace < 0) return null;
            int depth = 0, end = brace;
            for (; end < json.Length; end++) { if (json[end] == '{') depth++; else if (json[end] == '}') { depth--; if (depth == 0) break; } }
            var obj = json.Substring(brace, end - brace + 1);
            var nodeId = JsonHelper.ExtractString(obj, "m_Id");
            var slotId = JsonHelper.ExtractString(obj, "m_SlotId");
            return (nodeId != null && slotId != null) ? Tuple.Create(nodeId, slotId) : null;
        }

        static string ShortType(string t) { var d = t.LastIndexOf('.'); return d >= 0 ? t.Substring(d + 1) : t; }

        // ---- Phase 20d: graph_node + graph_edge ----

        public static string ManageNode(string path, string nodeType, string nodeId, string action)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");
            var content = File.ReadAllText(path);
            var blocks = SplitBlocks(content);
            var root = FindRoot(blocks);
            if (root == null) throw new InvalidOperationException("No GraphData block found");
            content = action == "remove"
                ? RemoveNode(content, blocks, root, nodeId)
                : AddNode(content, root, nodeType);
            File.WriteAllText(path, content);
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); } catch { }
            return Get(path);
        }

        public static string ManageEdge(string path, string outputNode, int outputSlot, string inputNode, int inputSlot, string action)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException($"ShaderGraph not found: {path}");
            var content = File.ReadAllText(path);
            var blocks = SplitBlocks(content);
            var root = FindRoot(blocks);
            if (root == null) throw new InvalidOperationException("No GraphData block found");
            content = action == "remove"
                ? RemoveEdge(content, root, outputNode, outputSlot, inputNode, inputSlot)
                : AddEdge(content, root, outputNode, outputSlot, inputNode, inputSlot);
            File.WriteAllText(path, content);
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); } catch { }
            return Get(path);
        }

        static string AddNode(string content, string root, string nodeType)
        {
            var newId = Guid.NewGuid().ToString("N");
            var nodeBlock = $"{{\n  \"m_SGVersion\": 0,\n  \"m_Type\": \"UnityEditor.ShaderGraph.{nodeType}\",\n  \"m_ObjectId\": \"{newId}\",\n  \"m_Group\": {{\"m_Id\": \"\"}},\n  \"m_Name\": \"{nodeType}\",\n  \"m_DrawState\": {{\"m_Expanded\": true, \"m_Position\": {{\"serializedVersion\": \"2\", \"x\": 0, \"y\": 0, \"width\": 200, \"height\": 100}}}},\n  \"m_Slots\": [],\n  \"synonyms\": [],\n  \"m_Precision\": 0,\n  \"m_PreviewExpanded\": false,\n  \"m_DismissedVersion\": 0,\n  \"m_PreviewMode\": 0,\n  \"m_CustomColors\": {{\"m_SerializableColors\": []}}\n}}";
            var nodeRef = $"{{\"m_Id\": \"{newId}\"}}";
            content = InsertIntoArray(content, root, "m_Nodes", nodeRef);
            content = content.TrimEnd() + "\n\n" + nodeBlock + "\n";
            return content;
        }

        static string RemoveNode(string content, List<string> blocks, string root, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) throw new ArgumentException("nodeId required for remove");

            // 1. Remove the node block from file
            foreach (var b in blocks)
                if (JsonHelper.ExtractString(b, "m_ObjectId") == nodeId) { content = content.Replace(b, ""); break; }

            // 2. Remove node ref from m_Nodes + edges via line-based filtering
            // This is more reliable than SplitBlocks+Replace chains which break on whitespace drift
            var lines = content.Split('\n');
            var result = new List<string>();
            bool skipBlock = false;
            int skipDepth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                // Detect start of a block containing the nodeId (m_Nodes ref or edge)
                if (!skipBlock && trimmed.Contains(nodeId))
                {
                    // Find enclosing { on this or previous lines
                    int braceCount = 0;
                    foreach (char c in trimmed) { if (c == '{') braceCount++; if (c == '}') braceCount--; }

                    if (braceCount == 0)
                    {
                        // Self-contained on one line — skip it and any trailing comma on prev line
                        if (result.Count > 0 && result[result.Count - 1].TrimEnd().EndsWith(","))
                            result[result.Count - 1] = result[result.Count - 1].TrimEnd().TrimEnd(',');
                        continue;
                    }
                    else if (braceCount > 0)
                    {
                        // Multi-line block starting here — skip until balanced
                        skipBlock = true;
                        skipDepth = braceCount;
                        if (result.Count > 0 && result[result.Count - 1].TrimEnd().EndsWith(","))
                            result[result.Count - 1] = result[result.Count - 1].TrimEnd().TrimEnd(',');
                        continue;
                    }
                }

                if (skipBlock)
                {
                    foreach (char c in trimmed) { if (c == '{') skipDepth++; if (c == '}') skipDepth--; }
                    if (skipDepth <= 0) skipBlock = false;
                    continue;
                }

                result.Add(lines[i]);
            }

            content = string.Join("\n", result);
            while (content.Contains("\n\n\n")) content = content.Replace("\n\n\n", "\n\n");
            return content;
        }

        static string AddEdge(string content, string root, string outputNode, int outputSlot, string inputNode, int inputSlot)
        {
            var edgeJson = $"{{\"m_OutputSlot\": {{\"m_Node\": {{\"m_Id\": \"{outputNode}\"}}, \"m_SlotId\": {outputSlot}}}, \"m_InputSlot\": {{\"m_Node\": {{\"m_Id\": \"{inputNode}\"}}, \"m_SlotId\": {inputSlot}}}}}";
            return InsertIntoArray(content, root, "m_Edges", edgeJson);
        }

        static string RemoveEdge(string content, string root, string outputNode, int outputSlot, string inputNode, int inputSlot)
        {
            var edgesArr = JsonHelper.ExtractArray(root, "m_Edges");
            if (edgesArr == "[]") return content;
            var edgeBlocks = SplitBlocks(edgesArr);
            var filtered = new List<string>();
            foreach (var eb in edgeBlocks)
            {
                var outSlot = ExtractSlotRef(eb, eb.IndexOf("\"m_OutputSlot\""));
                var inSlot  = ExtractSlotRef(eb, eb.IndexOf("\"m_InputSlot\""));
                if (outSlot != null && inSlot != null
                    && outSlot.Item1 == outputNode && outSlot.Item2 == outputSlot.ToString()
                    && inSlot.Item1  == inputNode  && inSlot.Item2  == inputSlot.ToString()) continue;
                filtered.Add(eb);
            }
            var newArr = filtered.Count > 0 ? "[\n    " + string.Join(",\n    ", filtered) + "\n  ]" : "[]";
            return content.Replace(edgesArr, newArr);
        }

        static string InsertIntoArray(string content, string root, string arrayKey, string item)
        {
            var arr = JsonHelper.ExtractArray(root, arrayKey);
            var last = arr.LastIndexOf(']');
            var inner = arr.Substring(0, last).TrimEnd();
            var updated = (inner.EndsWith("[") ? inner : inner + ",\n    ") + item + "\n  ]";
            var newRoot = root.Replace(arr, updated);
            return content.Replace(root, newRoot);
        }

    }
}
