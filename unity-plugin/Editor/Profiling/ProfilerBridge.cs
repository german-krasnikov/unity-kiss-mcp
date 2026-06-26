using System;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>
    /// Wraps ProfilerRecorder + FrameTimingManager into a single CollectFrame() call.
    /// Lazy: recorder is created only on first call, disposed when no longer needed.
    /// _frameProvider is replaced in tests to avoid real Unity API dependency.
    /// </summary>
    internal static class ProfilerBridge
    {
        private static ProfilerRecorder _cpuRecorder;
        private static int _prevGcCount;
        // Cached to avoid per-call heap allocation
        private static readonly FrameTiming[] _timings = new FrameTiming[1];

        // Injected in tests to replace real Unity API
        internal static Func<FrameSample> _frameProvider = CollectFrameReal;

        internal static FrameSample CollectFrame() => _frameProvider();

        private static void EnsureInitialized()
        {
            if (_cpuRecorder.Valid) return;
            _cpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            _prevGcCount = GC.CollectionCount(0);
        }

        internal static void Shutdown()
        {
            if (_cpuRecorder.Valid) _cpuRecorder.Dispose();
            _cpuRecorder = default;
        }

        private static FrameSample CollectFrameReal()
        {
            EnsureInitialized();
            float dt = Time.unscaledDeltaTime;
            float cpuMs = _cpuRecorder.Valid ? _cpuRecorder.LastValue / 1_000_000f : 0f;

            float gpuMs = -1f;
            FrameTimingManager.CaptureFrameTimings();
            uint timingCount = FrameTimingManager.GetLatestTimings(1, _timings);
            if (timingCount > 0 && _timings[0].gpuFrameTime > 0)
                gpuMs = (float)_timings[0].gpuFrameTime;

            int gcNow = GC.CollectionCount(0);
            int gcDelta = Math.Max(0, gcNow - _prevGcCount);
            _prevGcCount = gcNow;

            return new FrameSample
            {
                DeltaTime = dt,
                CpuMs = cpuMs,
                GpuMs = gpuMs,
                GcGen0Count = gcDelta,
                MonoUsedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),
                DrawCalls = UnityStats.drawCalls,
                Batches = UnityStats.batches,
                Triangles = UnityStats.triangles,
                SetPassCalls = UnityStats.setPassCalls,
            };
        }
    }
}
