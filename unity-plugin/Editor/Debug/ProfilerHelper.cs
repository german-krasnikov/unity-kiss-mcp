using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityMCP.Editor
{
    internal static class ProfilerHelper
    {
        public static string GetSnapshot()
        {
            var sb = new StringBuilder();

            float dt = Time.unscaledDeltaTime;
            float fps = dt > 0f ? 1f / dt : 0f;
            sb.AppendLine($"fps={fps:F0} dt={dt * 1000:F1}ms");

            long monoUsed = Profiler.GetMonoUsedSizeLong();
            long monoHeap = Profiler.GetMonoHeapSizeLong();
            long totalAlloc = Profiler.GetTotalAllocatedMemoryLong();
            long totalReserved = Profiler.GetTotalReservedMemoryLong();
            sb.AppendLine($"mono={monoUsed / 1048576}MB/{monoHeap / 1048576}MB total={totalAlloc / 1048576}MB/{totalReserved / 1048576}MB");

            sb.AppendLine($"gc_gen0={System.GC.CollectionCount(0)} gen1={System.GC.CollectionCount(1)} gen2={System.GC.CollectionCount(2)}");

            return sb.ToString();
        }
    }
}
