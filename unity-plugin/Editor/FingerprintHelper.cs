using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class FingerprintHelper
    {
        public static string Fingerprint(string path, int depth)
        {
            var go = !string.IsNullOrEmpty(path) ? ComponentSerializer.FindObject(path) : null;
            GameObject[] roots;
            if (go != null)
                roots = new[] { go };
            else
            {
                roots = SceneManager.GetActiveScene().GetRootGameObjects();
            }

            var sb = new StringBuilder();
            foreach (var root in roots)
                AppendFingerprint(sb, root.transform, depth, 0);

            var hash = sb.ToString().GetHashCode();
            return $"fp:{hash:X8}";
        }

        private static void AppendFingerprint(StringBuilder sb, Transform t, int maxDepth, int depth)
        {
            sb.Append(t.gameObject.name).Append(':');
            foreach (var comp in t.gameObject.GetComponents<Component>())
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                    sb.Append(prop.name).Append('=')
                      .Append(ComponentSerializer.GetPropertyValueString(prop)).Append(';');
            }
            if (depth < maxDepth)
                for (int i = 0; i < t.childCount; i++)
                    AppendFingerprint(sb, t.GetChild(i), maxDepth, depth + 1);
        }
    }
}
