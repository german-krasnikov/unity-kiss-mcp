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
            if (depth == ChipDepth.PathOnly) return chipPath;

            var go = FindGo(chipPath);
            if (go == null || !go) return chipPath; // destroyed or not found → PathOnly

            if (depth == ChipDepth.Summary) return BuildSummary(go);

            // Full
            var budget = budgetOverride >= 0 ? budgetOverride : FullBudget;
            var full = ComponentSerializer.SerializeAll(go.GetInstanceID());
            if (full != null && full.Length <= budget) return full;
            return BuildSummary(go); // budget exceeded → Summary
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

        private static string BuildSummary(GameObject go)
        {
            // Matches SelectionSummary structure: [Context: /Path (Comp1, Comp2)]
            const int MaxComponents = 3;
            var path  = ComponentSerializer.GetPath(go);
            var comps = go.GetComponents<Component>();
            var sb    = new StringBuilder("[Context: ");
            sb.Append(path);

            var names = new List<string>(comps.Length);
            foreach (var c in comps)
                if (c != null && !(c is Transform))
                    names.Add(c.GetType().Name);

            if (names.Count > 0)
            {
                sb.Append(" (");
                var shown = names.Count <= MaxComponents ? names.Count : MaxComponents;
                for (var i = 0; i < shown; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(names[i]);
                }
                if (names.Count > MaxComponents) sb.Append(", ...");
                sb.Append(")");
            }

            sb.Append("]");
            return sb.ToString();
        }
    }
}
