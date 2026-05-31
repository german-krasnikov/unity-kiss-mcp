using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    public static class HierarchySerializer
    {
        public const int MAX_NODES = 3000;

        public static string Serialize(int depth = 99, string root = null, string filter = null, bool components = false)
        {
            var sb = new StringBuilder();
            var roots = GetRootObjects(root);
            int nodeCount = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                var isLast = (i == roots.Length - 1);
                SerializeObject(sb, roots[i], depth, 0, new List<bool>(), isLast, filter, ref nodeCount, components);
                if (nodeCount >= MAX_NODES) break;
            }

            if (nodeCount >= MAX_NODES)
            {
                sb.AppendLine($"... truncated at {MAX_NODES} nodes. Use filter/root/depth to narrow.");
            }

            return sb.ToString();
        }

        // ── Incremental cache ──────────────────────────────────────────────────────
    private static string _lastHierarchy;

    public static void ResetIncrementalCache() => _lastHierarchy = null;

    public static string SerializeIncremental(int depth, string root, string filter, bool components)
    {
        var current = Serialize(depth, root, filter, components);
        if (_lastHierarchy != null && current == _lastHierarchy)
            return "NO_CHANGE";
        _lastHierarchy = current;
        return current;
    }

    public static string SerializeSubtree(GameObject root, int depth = 1)
        {
            if (root == null) return "";
            var sb = new StringBuilder();
            int nodeCount = 0;
            SerializeObject(sb, root, depth, 0, new List<bool>(), true, null, ref nodeCount);
            return sb.ToString().TrimEnd();
        }

        internal static void SerializeObject(
            StringBuilder sb,
            GameObject go,
            int maxDepth,
            int currentDepth,
            List<bool> parentIsLast,
            bool isLast,
            string filter,
            ref int nodeCount,
            bool components = false)
        {
            if (nodeCount >= MAX_NODES) return;

            // Filter check
            if (!string.IsNullOrEmpty(filter) && !MatchesFilter(go, filter, maxDepth, currentDepth))
                return;

            nodeCount++;

            // Append indent
            AppendIndent(sb, parentIsLast, isLast);

            // Name
            sb.Append(go.name);

            // Component list (skip Transform, skip nulls)
            if (components)
            {
                var comps = go.GetComponents<Component>();
                sb.Append(" [");
                bool first = true;
                foreach (var c in comps)
                {
                    if (c == null || c is Transform) continue;
                    if (!first) sb.Append(',');
                    sb.Append(c.GetType().Name);
                    first = false;
                }
                sb.Append(']');
            }

            // Short ref (replaces instance ID in output)
            sb.Append(' ').Append(RefManager.Assign(go));

            // Inactive marker
            if (!go.activeSelf)
                sb.Append(" !");

            // Children count suffix when depth-truncated
            if (currentDepth >= maxDepth && go.transform.childCount > 0)
            {
                sb.Append(" +").Append(CountDescendants(go.transform));
            }

            sb.AppendLine();

            // Children
            if (currentDepth >= maxDepth)
                return;

            var transform = go.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (nodeCount >= MAX_NODES)
                {
                    // Show how many siblings we're skipping
                    var remaining = transform.childCount - i;
                    AppendIndent(sb, parentIsLast, true);
                    sb.AppendLine($"... +{remaining} siblings");
                    break;
                }

                var child = transform.GetChild(i).gameObject;
                var childIsLast = (i == transform.childCount - 1);

                parentIsLast.Add(isLast);
                SerializeObject(sb, child, maxDepth, currentDepth + 1, parentIsLast, childIsLast, filter, ref nodeCount, components);
                parentIsLast.RemoveAt(parentIsLast.Count - 1);
            }
        }

        private static void AppendIndent(StringBuilder sb, List<bool> parentIsLast, bool isLast)
        {
            // Draw parent continuation lines
            for (int i = 0; i < parentIsLast.Count; i++)
            {
                sb.Append(parentIsLast[i] ? "   " : "│  ");
            }

            // Draw current level connector
            if (parentIsLast.Count > 0)
            {
                sb.Append(isLast ? "└─ " : "├─ ");
            }
        }

        private static bool MatchesFilter(GameObject go, string filter, int maxDepth, int currentDepth)
        {
            // Check name
            if (go.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // Check children (if within depth limit)
            if (currentDepth < maxDepth)
            {
                var transform = go.transform;
                for (int i = 0; i < transform.childCount; i++)
                {
                    if (MatchesFilter(transform.GetChild(i).gameObject, filter, maxDepth, currentDepth + 1))
                        return true;
                }
            }

            return false;
        }

        public static string SerializeSummary(string root = null)
        {
            var roots = GetRootObjects(root);
            var sb = new StringBuilder();

            if (string.IsNullOrEmpty(root))
            {
                // Full scene summary: header + per-root lines
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                int total = 0;
                foreach (var r in roots) total += 1 + CountDescendants(r.transform);
                sb.AppendLine($"{scene.name} ({total} nodes)");
                foreach (var r in roots)
                {
                    int desc = CountDescendants(r.transform);
                    var inactive = r.activeSelf ? "" : " !";
                    if (desc > 0)
                        sb.AppendLine($"  {r.name} ({desc} children){inactive}");
                    else
                        sb.AppendLine($"  {r.name}{inactive}");
                }
            }
            else if (roots.Length == 0)
            {
                sb.AppendLine($"Not found: {root}");
            }
            else if (roots.Length == 1)
            {
                var r = roots[0];
                int desc = CountDescendants(r.transform);
                var inactive = r.activeSelf ? "" : " !";
                if (desc > 0)
                    sb.AppendLine($"{r.name} ({desc} children){inactive}");
                else
                    sb.AppendLine($"{r.name}{inactive}");
            }

            return sb.ToString();
        }

        private static int CountDescendants(Transform t, int budget = MAX_NODES)
        {
            int count = t.childCount;
            for (int i = 0; i < t.childCount && count < budget; i++)
                count += CountDescendants(t.GetChild(i), budget - count);
            return count;
        }

        private static GameObject[] GetRootObjects(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                // Check prefab stage first
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                    return new[] { stage.prefabContentsRoot };

                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                return scene.GetRootGameObjects();
            }

            var root = ComponentSerializer.FindObject(rootPath);
            return root != null ? new[] { root } : new GameObject[0];
        }
    }
}
