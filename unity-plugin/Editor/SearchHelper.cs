using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    public static class SearchHelper
    {
        public static string Search(string query, string root = null, int limit = 50)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("query is required");

            var q = ParseQuery(query);
            var results = new List<GameObject>();
            if (!WalkScene(query, root, q, results, limit, out var notFound))
                return notFound;

            if (results.Count == 0)
                return BuildEmptyHint(query);

            int overflow = 0;
            if (limit > 0 && results.Count >= limit)
            {
                var all = new List<GameObject>();
                WalkScene(query, root, q, all, 0, out _);
                overflow = all.Count - results.Count;
            }

            var sb = new StringBuilder();
            foreach (var go in results)
            {
                sb.Append(go.name).Append(" #").Append(go.GetInstanceID());
                var compNames = new List<string>();
                foreach (var c in go.GetComponents<Component>())
                    if (c != null && !(c is Transform)) compNames.Add(c.GetType().Name);
                if (compNames.Count > 0)
                    sb.Append(" [").Append(string.Join(",", compNames)).Append("]");
                if (!go.activeSelf) sb.Append(" !");
                sb.AppendLine();
            }
            if (overflow > 0)
                sb.AppendLine($"...+{overflow} more (limit={limit})");
            return sb.ToString().TrimEnd('\n');
        }

        // Returns false + sets notFound hint when root is not found; otherwise populates results.
        private static bool WalkScene(string query, string root, SearchQuery q,
            List<GameObject> results, int limit, out string notFound)
        {
            notFound = null;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                CollectMatches(stage.prefabContentsRoot.transform, q, results, limit);
            }
            else if (!string.IsNullOrEmpty(root) && root != "/")
            {
                var rootGO = ComponentSerializer.FindObject(root);
                if (rootGO == null) { notFound = BuildEmptyHint(query); return false; }
                CollectMatches(rootGO.transform, q, results, limit);
            }
            else
            {
                foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    CollectMatches(r.transform, q, results, limit);
                    if (limit > 0 && results.Count >= limit) break;
                }
            }
            return true;
        }

        // Struct for parsed query
        private struct SearchQuery
        {
            public string Name;       // plain text → substring match (case-insensitive)
            public string Component;  // t:XXX
            public string Tag;        // tag=XXX
            public int? Layer;        // layer=N
            public bool? Active;      // active=true/false
        }

        // Parse "t:Light tag=Player active=false Main Camera" into SearchQuery
        private static SearchQuery ParseQuery(string query)
        {
            var q = new SearchQuery();
            var nameParts = new List<string>();

            foreach (var token in query.Split(' '))
            {
                if (string.IsNullOrEmpty(token)) continue;

                if (token.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
                    q.Component = ComponentSerializer.StripNamespace(token.Substring(2));
                else if (token.StartsWith("tag=", StringComparison.OrdinalIgnoreCase))
                    q.Tag = token.Substring(4);
                else if (token.StartsWith("layer=", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(token.Substring(6), out var layer))
                        q.Layer = layer;
                }
                else if (token.StartsWith("active=", StringComparison.OrdinalIgnoreCase))
                    q.Active = token.Substring(7).ToLower() == "true";
                else
                    nameParts.Add(token);
            }

            if (nameParts.Count > 0)
                q.Name = string.Join(" ", nameParts);

            return q;
        }

        private static bool Matches(GameObject go, SearchQuery q)
        {
            // Name: case-insensitive substring
            if (!string.IsNullOrEmpty(q.Name) &&
                go.name.IndexOf(q.Name, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            // Component: check by type name
            if (!string.IsNullOrEmpty(q.Component))
            {
                bool found = false;
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c != null && c.GetType().Name.Equals(q.Component, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            // Tag
            if (!string.IsNullOrEmpty(q.Tag) && go.tag != q.Tag)
                return false;

            // Layer
            if (q.Layer.HasValue && go.layer != q.Layer.Value)
                return false;

            // Active
            if (q.Active.HasValue && go.activeSelf != q.Active.Value)
                return false;

            return true;
        }

        private static void CollectMatches(Transform t, SearchQuery q, List<GameObject> results, int limit = 0)
        {
            if (limit > 0 && results.Count >= limit) return;
            if (Matches(t.gameObject, q))
                results.Add(t.gameObject);

            foreach (Transform child in t)
            {
                if (limit > 0 && results.Count >= limit) return;
                CollectMatches(child, q, results, limit);
            }
        }

        private static string BuildEmptyHint(string query)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            string ctxName; int total; var tops = new List<string>();
            if (stage != null)
            {
                ctxName = stage.prefabContentsRoot.name;
                total = stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).Length;
                foreach (Transform c in stage.prefabContentsRoot.transform) tops.Add(c.name);
            }
            else
            {
                var sc = SceneManager.GetActiveScene();
                ctxName = sc.name;
                var roots = sc.GetRootGameObjects();
                total = 0; foreach (var r in roots) total += r.GetComponentsInChildren<Transform>(true).Length;
                foreach (var r in roots) tops.Add(r.name);
            }
            var topStr = tops.Count <= 8 ? string.Join(", ", tops)
                : string.Join(", ", tops.GetRange(0, 8)) + $", +{tops.Count - 8} more";
            var topLine = topStr.Length > 0 ? $"\ntop: {topStr}" : "";
            return $"no matches in '{ctxName}' ({total} objects){topLine}\nhint: t:Type | tag=X | layer=N | active=true | name-substring";
        }
    }
}
