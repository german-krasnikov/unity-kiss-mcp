using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    public static class ErrorHelper
    {
        public static string ObjectNotFound(string path)
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var sb = new StringBuilder();
            sb.Append("'").Append(path).Append("' not found. Root objects: ");

            var limit = Mathf.Min(roots.Length, 10);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(roots[i].name);
            }

            if (roots.Length > 10)
                sb.Append(", ...");

            // "Did you mean" suggestion based on closest root name
            var lastName = path.TrimStart('/').Split('/').Last();
            var closest = ClosestName(lastName, roots.Select(r => r.name).ToArray());
            if (closest != null)
                sb.Append($". Did you mean '{closest}'?");

            sb.Append(". Use get_hierarchy to see children.");
            return sb.ToString();
        }

        private static string ClosestName(string query, string[] candidates)
        {
            if (candidates.Length == 0) return null;
            string best = null;
            int bestDist = int.MaxValue;
            int threshold = System.Math.Max(3, query.Length * 2 / 5);
            string queryLower = query.ToLower();
            foreach (var c in candidates)
            {
                int d = StringDistance.Levenshtein(queryLower, c.ToLower());
                if (d < bestDist && d <= threshold)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        public static string ComponentNotFound(string type, GameObject go)
        {
            var components = go.GetComponents<Component>();
            var sb = new StringBuilder();
            sb.Append("'").Append(type).Append("' not on '").Append(go.name).Append("'. Available: ");

            var allTypes = components
                .Where(c => c != null)
                .Select(c => c.GetType().Name)
                .ToList();

            sb.Append(string.Join(", ", allTypes.Take(10)));

            if (allTypes.Count > 10)
                sb.Append(", ...");

            sb.Append(". Use manage_component(action=add) to add it.");
            return sb.ToString();
        }

        public static string PropertyNotFound(string prop, string component, string path) =>
            $"Property '{prop}' not found on {component}. Use get_component(path=\"{path}\", type=\"{component}\") to see available properties.";

        public static string InvalidAction(string action, string[] valid) =>
            $"Unknown action '{action}'. Valid: {string.Join("|", valid)}.";
    }
}
