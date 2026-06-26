using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityMCP.Editor
{
    internal static class MemoryHelper
    {
        private static readonly Dictionary<string, int> _prevCounts = new Dictionary<string, int>();

        private static readonly (string name, System.Type type)[] _types =
        {
            ("Texture2D",  typeof(Texture2D)),
            ("Material",   typeof(Material)),
            ("Mesh",       typeof(Mesh)),
            ("GameObject", typeof(GameObject)),
            ("AudioClip",  typeof(AudioClip)),
        };

        public static void ResetCounts() => _prevCounts.Clear();

        public static string GetSnapshot(string include = "all")
        {
            var sb = new StringBuilder();

            long monoUsed    = Profiler.GetMonoUsedSizeLong();
            long monoHeap    = Profiler.GetMonoHeapSizeLong();
            long totalAlloc  = Profiler.GetTotalAllocatedMemoryLong();
            sb.AppendLine($"mono={monoUsed / 1048576}MB/{monoHeap / 1048576}MB alloc={totalAlloc / 1048576}MB");
            sb.AppendLine($"gc: gen0={System.GC.CollectionCount(0)} gen1={System.GC.CollectionCount(1)} gen2={System.GC.CollectionCount(2)}");

            foreach (var (name, type) in _types)
            {
                if (include != "all" && !include.Contains(name.ToLower())) continue;
                int count = Resources.FindObjectsOfTypeAll(type).Length;
                _prevCounts.TryGetValue(name, out int prev);
                int delta = count - prev;
                _prevCounts[name] = count;
                string deltaStr = delta == 0 ? "" : delta > 0 ? $" +{delta}" : $" {delta}";
                sb.AppendLine($"{name}: {count}{deltaStr}");
            }

            return sb.ToString();
        }
    }
}
