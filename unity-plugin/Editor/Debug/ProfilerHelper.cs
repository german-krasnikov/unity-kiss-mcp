using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor
{
    internal static class ProfilerHelper
    {
        public static string GetFrameStats()
        {
            var s = ProfilerBridge.CollectFrame();
            ProfilerBridge.Shutdown(); // one-shot: dispose recorder immediately
            var sb = new StringBuilder();
            float fps = s.DeltaTime > 0f ? 1f / s.DeltaTime : 0f;
            sb.AppendLine($"frame dt={s.DeltaTime * 1000:F1}ms fps={fps:F1}");
            sb.AppendLine($"cpu={s.CpuMs:F1}ms gpu={(s.GpuMs < 0f ? "N/A" : s.GpuMs.ToString("F1") + "ms")}");
            sb.AppendLine($"draw={s.DrawCalls} batches={s.Batches} tris={RenderAnalyzer.FormatNum(s.Triangles)}");
            sb.Append($"mono={s.MonoUsedBytes / 1_048_576}MB gc_gen0={s.GcGen0Count}");
            return sb.ToString();
        }

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
