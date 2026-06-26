using System;

namespace UnityMCP.Editor.Profiling
{
    internal struct FrameSample
    {
        public float DeltaTime;      // seconds, from Time.unscaledDeltaTime
        public float CpuMs;          // main thread marker total ms
        public float GpuMs;          // from FrameTimingManager, -1 if unavailable
        public int DrawCalls;
        public int Batches;
        public long Triangles;
        public long MonoUsedBytes;
        public int GcGen0Count;      // GC.CollectionCount(0) delta vs previous frame
        public int SetPassCalls;
    }

    internal sealed class ProfileSession
    {
        public string Id;
        public RecordMode Mode;
        public DateTime Timestamp;
        public ProfileAnalyzer.Stats Stats;
    }

    internal enum RecordMode { Burst, Manual, Triggered }
    internal enum RecordState { Idle, Recording }
}
