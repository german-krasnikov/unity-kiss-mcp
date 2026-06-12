using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    public static class ObjectDiffHelper
    {
        public static string Diff(string pathA, string pathB)
        {
            var goA = ComponentSerializer.FindObject(pathA);
            var goB = ComponentSerializer.FindObject(pathB);

            if (goA == null) return $"Error: pathA not found: {pathA}";
            if (goB == null) return $"Error: pathB not found: {pathB}";

            var sb = new StringBuilder();
            sb.AppendLine($"--- {pathA}");
            sb.AppendLine($"+++ {pathB}");

            var compDiff = DiffComponents(goA, goB);
            var propDiff = DiffProperties(pathA, pathB, goA, goB);
            var childDiff = DiffChildren(goA, goB);

            if (compDiff.Length == 0 && propDiff.Length == 0 && childDiff.Length == 0)
                return "= (identical)";

            if (compDiff.Length > 0) { sb.AppendLine("Components:"); sb.Append(compDiff); }
            if (propDiff.Length > 0) { sb.AppendLine("Properties:"); sb.Append(propDiff); }
            if (childDiff.Length > 0) { sb.AppendLine("Children:"); sb.Append(childDiff); }

            return sb.ToString().TrimEnd();
        }

        private static string DiffComponents(GameObject goA, GameObject goB)
        {
            var setA = GetComponentNames(goA);
            var setB = GetComponentNames(goB);
            var sb = new StringBuilder();
            foreach (var c in setA) if (!setB.Contains(c)) sb.AppendLine($"  - {c}");
            foreach (var c in setB) if (!setA.Contains(c)) sb.AppendLine($"  + {c}");
            return sb.ToString();
        }

        private static string DiffProperties(string pathA, string pathB, GameObject goA, GameObject goB)
        {
            var sb = new StringBuilder();

            // Always compare Transform
            DiffComponentProps(sb, pathA, pathB, "Transform");

            var compNamesA = GetComponentNames(goA);
            var compNamesB = GetComponentNames(goB);
            foreach (var typeName in compNamesA)
            {
                if (!compNamesB.Contains(typeName)) continue;
                DiffComponentProps(sb, pathA, pathB, typeName);
            }
            return sb.ToString();
        }

        private static void DiffComponentProps(StringBuilder sb, string pathA, string pathB, string typeName)
        {
            var serialA = ComponentSerializer.Serialize(pathA, typeName);
            var serialB = ComponentSerializer.Serialize(pathB, typeName);
            if (serialA == null || serialB == null || serialA == serialB) return;

            var dictA = ParseKeyValues(serialA);
            var dictB = ParseKeyValues(serialB);
            bool headerPrinted = false;
            foreach (var kv in dictA)
            {
                if (!dictB.TryGetValue(kv.Key, out var valB) || valB == kv.Value) continue;
                if (!headerPrinted) { sb.AppendLine($"  [{typeName}]"); headerPrinted = true; }
                sb.AppendLine($"    {kv.Key}: {kv.Value} → {valB}");
            }
        }

        private static string DiffChildren(GameObject goA, GameObject goB)
        {
            var childrenA = GetChildNames(goA);
            var childrenB = GetChildNames(goB);
            var sb = new StringBuilder();
            foreach (var c in childrenA) if (!childrenB.Contains(c)) sb.AppendLine($"  - {c}");
            foreach (var c in childrenB) if (!childrenA.Contains(c)) sb.AppendLine($"  + {c}");
            return sb.ToString();
        }

        private static HashSet<string> GetComponentNames(GameObject go)
        {
            var set = new HashSet<string>();
            foreach (var c in go.GetComponents<Component>())
                if (c != null && !(c is Transform)) set.Add(c.GetType().Name);
            return set;
        }

        private static List<string> GetChildNames(GameObject go)
        {
            var list = new List<string>();
            for (int i = 0; i < go.transform.childCount; i++)
                list.Add(go.transform.GetChild(i).name);
            return list;
        }

        private static Dictionary<string, string> ParseKeyValues(string text)
        {
            var dict = new Dictionary<string, string>();
            foreach (var line in text.Split('\n'))
            {
                var idx = line.IndexOf(": ", StringComparison.Ordinal);
                if (idx < 0) continue;
                var key = line.Substring(0, idx).Trim();
                var val = line.Substring(idx + 2).Trim();
                if (key.Length > 0) dict[key] = val;
            }
            return dict;
        }
    }
}
