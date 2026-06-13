using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static partial class ShaderGraphHelper
    {
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
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            catch (Exception ex) { Debug.LogWarning($"ShaderGraph import failed for '{path}': {ex.Message}"); }
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
            try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            catch (Exception ex) { Debug.LogWarning($"ShaderGraph import failed for '{path}': {ex.Message}"); }
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
            // More reliable than SplitBlocks+Replace chains which break on whitespace drift
            var lines = content.Split('\n');
            var result = new List<string>();
            bool skipBlock = false;
            int skipDepth = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                if (!skipBlock && trimmed.Contains(nodeId))
                {
                    int braceCount = 0;
                    foreach (char c in trimmed) { if (c == '{') braceCount++; if (c == '}') braceCount--; }

                    if (braceCount == 0)
                    {
                        if (result.Count > 0 && result[result.Count - 1].TrimEnd().EndsWith(","))
                            result[result.Count - 1] = result[result.Count - 1].TrimEnd().TrimEnd(',');
                        continue;
                    }
                    else if (braceCount > 0)
                    {
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
