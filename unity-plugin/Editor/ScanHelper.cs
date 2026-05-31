using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ScanHelper
    {
        public static string Scan(string bands)
        {
            var allGOs = Object.FindObjectsOfType<GameObject>();
            int total = allGOs.Length;
            int colliders = 0, triggers = 0, audio = 0, lights = 0,
                rigidbodies = 0, canvas = 0, nav = 0;

            foreach (var go in allGOs)
            {
                foreach (var c in go.GetComponents<Collider>())
                {
                    colliders++;
                    if (c.isTrigger) triggers++;
                }
                colliders += go.GetComponents<Collider2D>().Length;
                audio += go.GetComponents<AudioSource>().Length;
                lights += go.GetComponents<Light>().Length;
                rigidbodies += go.GetComponents<Rigidbody>().Length;
                rigidbodies += go.GetComponents<Rigidbody2D>().Length;
                canvas += go.GetComponents<Canvas>().Length;
                foreach (var c in go.GetComponents<Component>())
                    if (c != null && c.GetType().Name.Contains("NavMesh")) nav++;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"SCAN: {total} objects");
            AppendBand(sb, "colliders", colliders, total);
            AppendBand(sb, "triggers", triggers, total);
            AppendBand(sb, "rigidbody", rigidbodies, total);
            AppendBand(sb, "audio", audio, total);
            AppendBand(sb, "lights", lights, total);
            AppendBand(sb, "canvas", canvas, total);
            AppendBand(sb, "navigation", nav, total);
            return sb.ToString().TrimEnd();
        }

        private static void AppendBand(StringBuilder sb, string name, int count, int total)
        {
            if (count > 0)
            {
                int pct = total > 0 ? count * 100 / total : 0;
                sb.AppendLine($"  {name}: {count} ({pct}%)");
            }
        }
    }
}
