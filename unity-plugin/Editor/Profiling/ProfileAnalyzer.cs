using System;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>Pure static stats computation from FrameSample[]. No Unity API dependencies.</summary>
    internal static class ProfileAnalyzer
    {
        internal struct Stats
        {
            public float FpsAvg, FpsMin, FpsMax, FpsP99;
            public int StutterCount;
            public float CpuAvg, CpuMax, CpuP99;
            public float GpuAvg, GpuMax;   // -1 if unavailable
            public long MemStart, MemEnd, MemPeak;
            public int GcCollections;
            public int DrawCallsAvg, DrawCallsMax;
            public long TrianglesAvg;
            public int FrameCount;
            public float DurationS;
        }

        internal struct DeltaStats
        {
            public float FpsDeltaPct;
            public long MemDeltaBytes;
            public int GcDelta;
            public int DrawCallDelta;
            public string Verdict;  // "REGRESSED" | "IMPROVED" | "STABLE"
        }

        internal static Stats Compute(FrameSample[] frames)
        {
            if (frames.Length == 0) return new Stats();

            var fpsList = new float[frames.Length];
            var cpuList = new float[frames.Length];
            float totalDt = 0f, gpuTotal = 0f, gpuMax = -1f;
            long memPeak = 0, drawTotal = 0, triTotal = 0;
            int gcTotal = 0, drawMax = 0, gpuCount = 0;

            for (int i = 0; i < frames.Length; i++)
            {
                var f = frames[i];
                fpsList[i] = f.DeltaTime > 0f ? 1f / f.DeltaTime : 0f;
                cpuList[i] = f.CpuMs;
                totalDt += f.DeltaTime;
                gcTotal += f.GcGen0Count;
                if (f.MonoUsedBytes > memPeak) memPeak = f.MonoUsedBytes;
                drawTotal += f.DrawCalls;
                if (f.DrawCalls > drawMax) drawMax = f.DrawCalls;
                triTotal += f.Triangles;
                if (f.GpuMs >= 0f) { gpuTotal += f.GpuMs; if (f.GpuMs > gpuMax) gpuMax = f.GpuMs; gpuCount++; }
            }

            float avgDt = totalDt / frames.Length;
            float threshold = avgDt * 1.5f;
            int stutterCount = 0;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i].DeltaTime > threshold) stutterCount++;

            Array.Sort(fpsList);
            Array.Sort(cpuList);

            float fpsSum = 0f;
            for (int i = 0; i < fpsList.Length; i++) fpsSum += fpsList[i];

            return new Stats
            {
                FrameCount = frames.Length,
                DurationS = totalDt,
                FpsAvg = fpsSum / frames.Length,
                FpsMin = fpsList[0],
                FpsMax = fpsList[frames.Length - 1],
                FpsP99 = Percentile(fpsList, 1),   // 1st percentile = 99th pct frame time
                StutterCount = stutterCount,
                CpuAvg = Average(cpuList),
                CpuMax = cpuList[frames.Length - 1],
                CpuP99 = Percentile(cpuList, 99),
                GpuAvg = gpuCount > 0 ? gpuTotal / gpuCount : -1f,
                GpuMax = gpuCount > 0 ? gpuMax : -1f,
                MemStart = frames[0].MonoUsedBytes,
                MemEnd = frames[frames.Length - 1].MonoUsedBytes,
                MemPeak = memPeak,
                GcCollections = gcTotal,
                DrawCallsAvg = (int)(drawTotal / frames.Length),
                DrawCallsMax = drawMax,
                TrianglesAvg = triTotal / frames.Length,
            };
        }

        internal static DeltaStats Compare(Stats baseline, Stats candidate)
        {
            float fpsDelta = baseline.FpsAvg > 0f
                ? (candidate.FpsAvg - baseline.FpsAvg) / baseline.FpsAvg * 100f
                : 0f;

            string verdict = fpsDelta < -5f ? "REGRESSED"
                : fpsDelta > 5f ? "IMPROVED"
                : "STABLE";

            return new DeltaStats
            {
                FpsDeltaPct = fpsDelta,
                MemDeltaBytes = candidate.MemEnd - baseline.MemEnd,
                GcDelta = candidate.GcCollections - baseline.GcCollections,
                DrawCallDelta = candidate.DrawCallsAvg - baseline.DrawCallsAvg,
                Verdict = verdict,
            };
        }

        private static float Percentile(float[] sorted, int pct)
        {
            if (sorted.Length == 0) return 0f;
            int idx = (int)(sorted.Length * pct / 100f);
            return sorted[Math.Clamp(idx, 0, sorted.Length - 1)];
        }

        private static float Average(float[] arr)
        {
            float sum = 0f;
            for (int i = 0; i < arr.Length; i++) sum += arr[i];
            return arr.Length > 0 ? sum / arr.Length : 0f;
        }
    }
}
