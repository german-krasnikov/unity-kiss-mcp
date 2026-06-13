using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ColliderChecker
    {
        public static string Check(string path = null)
        {
            if (path != null)
                return CheckPath(path);

            var issues = new List<string>();
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                CollectIssues(go, issues);

            return issues.Count == 0 ? "OK: no collider issues" : string.Join("\n", issues);
        }

        // Exposed separately for targeted path checks in tests
        public static string CheckPath(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) return ErrorHelper.ObjectNotFound(path);

            var issues = new List<string>();
            CollectIssues(go, issues);
            return issues.Count == 0 ? "OK: no collider issues" : string.Join("\n", issues);
        }

        private static void CollectIssues(GameObject go, List<string> issues)
        {
            var cols = go.GetComponents<Collider>();
            foreach (var col in cols)
            {
                if (col.isTrigger)
                {
                    var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInParent<Rigidbody>();
                    if (rb == null)
                        issues.Add($"TRIGGER_NO_RB: {ComponentSerializer.GetPath(go)}/{col.GetType().Name} — trigger without Rigidbody");
                }

                if (col is BoxCollider bc && (bc.size.x < 0.01f || bc.size.y < 0.01f || bc.size.z < 0.01f))
                    issues.Add($"MICRO_COL: {ComponentSerializer.GetPath(go)}/BoxCollider size too small");

                if (col is SphereCollider sc && sc.radius < 0.01f)
                    issues.Add($"MICRO_COL: {ComponentSerializer.GetPath(go)}/SphereCollider radius too small");
            }

            var scale = go.transform.lossyScale;
            if ((scale.x < 0 || scale.y < 0 || scale.z < 0) && cols.Length > 0)
                issues.Add($"NEG_SCALE: {ComponentSerializer.GetPath(go)} scale=({scale.x},{scale.y},{scale.z})");
        }
    }
}
