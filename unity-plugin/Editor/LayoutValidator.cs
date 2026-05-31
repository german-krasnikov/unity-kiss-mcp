using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class LayoutValidator
    {
        public static string Validate(string root, float minDistance)
        {
            var rootGO = ComponentSerializer.FindObject(root);
            if (rootGO == null) return ErrorHelper.ObjectNotFound(root);

            var triggers = new List<(Transform t, string name)>();
            var solids = new List<(Transform t, string name)>();

            foreach (var col in rootGO.GetComponentsInChildren<Collider>(true))
            {
                var path = GetRelativePath(col.transform, rootGO.transform);
                if (col.isTrigger) triggers.Add((col.transform, path));
                else solids.Add((col.transform, path));
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Layout: {triggers.Count} triggers, {solids.Count} solids");

            int warnings = 0;
            for (int i = 0; i < triggers.Count; i++)
                for (int j = i + 1; j < triggers.Count; j++)
                {
                    var dist = Vector3.Distance(triggers[i].t.position, triggers[j].t.position);
                    if (dist < minDistance)
                    {
                        sb.AppendLine($"WARNING: {triggers[i].name} <-> {triggers[j].name} dist={dist:F1}m < {minDistance}m");
                        warnings++;
                    }
                }

            sb.Append(warnings == 0 ? "OK: no trigger overlaps" : $"{warnings} warning(s)");
            return sb.ToString();
        }

        public static string GetSpatialContext(string path, float radius)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) return ErrorHelper.ObjectNotFound(path);

            var pos = go.transform.position;
            var sb = new StringBuilder();
            sb.AppendLine($"Position: ({pos.x:F1},{pos.y:F1},{pos.z:F1})");

            foreach (var col in go.GetComponentsInChildren<Collider>())
            {
                var type = col.isTrigger ? "TRIGGER" : "SOLID";
                var bounds = col.bounds;
                sb.AppendLine($"  {col.gameObject.name} [{type}] center=({bounds.center.x:F1},{bounds.center.y:F1},{bounds.center.z:F1}) size=({bounds.size.x:F1},{bounds.size.y:F1},{bounds.size.z:F1})");
            }

            Physics.SyncTransforms();
            sb.AppendLine("Approach vectors:");
            var dirs = new (string name, Vector3 dir)[] {
                ("N", Vector3.forward), ("S", Vector3.back),
                ("E", Vector3.right), ("W", Vector3.left),
                ("NE", (Vector3.forward+Vector3.right).normalized),
                ("NW", (Vector3.forward+Vector3.left).normalized),
                ("SE", (Vector3.back+Vector3.right).normalized),
                ("SW", (Vector3.back+Vector3.left).normalized)
            };
            foreach (var (name, dir) in dirs)
            {
                var testPoint = pos + dir * radius;
                var blocked = Physics.Linecast(testPoint, pos, out _);
                sb.AppendLine($"  {name}: ({testPoint.x:F1},{testPoint.y:F1},{testPoint.z:F1}) {(blocked ? "BLOCKED" : "CLEAR")}");
            }

            var nearby = Physics.OverlapSphere(pos, radius);
            if (nearby.Length > 0)
            {
                sb.AppendLine($"Nearby ({radius}m radius):");
                foreach (var col in nearby)
                {
                    if (col.transform.root == go.transform.root) continue;
                    var dist = Vector3.Distance(col.transform.position, pos);
                    sb.AppendLine($"  {col.gameObject.name} dist={dist:F1}m {(col.isTrigger ? "TRIGGER" : "SOLID")}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string GetRelativePath(Transform child, Transform root)
        {
            if (child == root) return root.name;
            var parts = new List<string>();
            var current = child;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
