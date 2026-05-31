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
        public static string Search(string query)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("query is required");

            var q = ParseQuery(query);
            var results = new List<GameObject>();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                CollectMatches(stage.prefabContentsRoot.transform, q, results);
            }
            else
            {
                foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    CollectMatches(root.transform, q, results);
            }

            if (results.Count == 0)
                return BuildEmptyHint(query);

            var sb = new StringBuilder();
            foreach (var go in results)
            {
                sb.Append(go.name).Append(" #").Append(go.GetInstanceID());
                // Append non-Transform components
                var comps = go.GetComponents<Component>();
                var compNames = new List<string>();
                foreach (var c in comps)
                {
                    if (c != null && !(c is Transform))
                        compNames.Add(c.GetType().Name);
                }
                if (compNames.Count > 0)
                    sb.Append(" [").Append(string.Join(",", compNames)).Append("]");
                if (!go.activeSelf)
                    sb.Append(" !");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd('\n');
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

        private static void CollectMatches(Transform t, SearchQuery q, List<GameObject> results)
        {
            if (Matches(t.gameObject, q))
                results.Add(t.gameObject);

            foreach (Transform child in t)
                CollectMatches(child, q, results);
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
