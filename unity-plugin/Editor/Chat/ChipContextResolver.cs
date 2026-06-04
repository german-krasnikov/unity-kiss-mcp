// Resolves chip paths to context text at three depths.
// PathOnly: path as-is. Summary: SelectionSummary-style. Full: ComponentSerializer dump.
// Asset paths (not starting with '/') are always PathOnly.
// Full > FullBudget chars falls back to Summary.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal enum ChipDepth { PathOnly, Summary, Full }

    internal static class ChipContextResolver
    {
        internal const int FullBudget = 2000;

#if UNITY_INCLUDE_TESTS
        // Seam: inject in tests instead of calling ComponentSerializer.FindObject
        internal static Func<string, GameObject> FindObjectOverride;
#endif

        /// <summary>1 chip → Full depth; 2+ chips → Summary for each.</summary>
        internal static string ResolveAll(List<string> chipPaths)
        {
            if (chipPaths == null || chipPaths.Count == 0) return "";
            var depth = chipPaths.Count == 1 ? ChipDepth.Full : ChipDepth.Summary;
            var sb = new StringBuilder();
            foreach (var path in chipPaths)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(ResolveOne(path, depth));
            }
            return sb.ToString();
        }

        /// <summary>Resolve a single chip path at the given depth.</summary>
        internal static string ResolveOne(string chipPath, ChipDepth depth, int budgetOverride = -1)
        {
            // Asset paths never have scene context
            if (IsAssetPath(chipPath)) return chipPath;
            if (depth == ChipDepth.PathOnly)
            {
                var goForId = FindGo(chipPath);
                if (goForId != null && goForId)
                    return chipPath + " #" + goForId.GetInstanceID();
                return chipPath;
            }

            var go = FindGo(chipPath);
            if (go == null || !go) return chipPath; // destroyed or not found → PathOnly

            if (depth == ChipDepth.Summary) return SelectionSummary.Summarize(go, "Context");

            // Full
            var budget = budgetOverride >= 0 ? budgetOverride : FullBudget;
            var full = ComponentSerializer.SerializeAll(go.GetInstanceID());
            if (full != null && full.Length <= budget) return full;
            return SelectionSummary.Summarize(go, "Context"); // budget exceeded → Summary
        }

        // ── private helpers ───────────────────────────────────────────────────

        private static bool IsAssetPath(string path)
            => !string.IsNullOrEmpty(path) && !path.StartsWith("/");

        private static GameObject FindGo(string path)
        {
#if UNITY_INCLUDE_TESTS
            if (FindObjectOverride != null) return FindObjectOverride(path);
#endif
            try { return ComponentSerializer.FindObject(path); }
            catch { return null; }
        }
    }
}
