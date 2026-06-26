using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>Converts ProfileAnalyzer.Stats to compact text. ~55 tokens vs ~240 JSON.</summary>
    internal static class ProfileFormatter
    {
        internal static string FormatSummary(string sessionId, ProfileAnalyzer.Stats s)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"session:{sessionId} {s.DurationS:F1}s {s.FrameCount}frames");
            sb.AppendLine($"fps avg={s.FpsAvg:F1} min={s.FpsMin:F0} max={s.FpsMax:F0} p99={s.FpsP99:F0} stutter={s.StutterCount}");
            sb.AppendLine($"cpu avg={s.CpuAvg:F1}ms max={s.CpuMax:F1}ms p99={s.CpuP99:F1}ms");
            sb.AppendLine(s.GpuAvg < 0f
                ? "gpu=N/A"
                : $"gpu avg={s.GpuAvg:F1}ms max={s.GpuMax:F1}ms");
            sb.AppendLine($"mem start={MB(s.MemStart)}MB end={MB(s.MemEnd)}MB peak={MB(s.MemPeak)}MB");
            sb.AppendLine($"gc gen0=+{s.GcCollections} in {s.DurationS:F1}s");
            sb.Append($"draw avg={s.DrawCallsAvg} max={s.DrawCallsMax} tris={FormatTris(s.TrianglesAvg)}");
            return sb.ToString();
        }

        internal static string FormatCompare(string sessionA, string sessionB,
            ProfileAnalyzer.Stats a, ProfileAnalyzer.Stats b, ProfileAnalyzer.DeltaStats d)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"compare {sessionA}→{sessionB}: {d.Verdict}");
            sb.AppendLine($"fps {a.FpsAvg:F1}→{b.FpsAvg:F1} ({d.FpsDeltaPct:+0.0;-0.0}%)");
            sb.AppendLine($"mem delta={MB(d.MemDeltaBytes):+0;-0}MB gc delta={d.GcDelta:+0;-0}");
            sb.Append($"draw delta={d.DrawCallDelta:+0;-0}");
            return sb.ToString();
        }

        internal static string FormatList(IEnumerable<ProfileSession> sessions)
        {
            var sb = new StringBuilder();
            foreach (var s in sessions)
                sb.AppendLine($"{s.Id} {s.Timestamp:HH:mm:ss} {s.Mode} {s.Stats.FrameCount}f {s.Stats.DurationS:F1}s fps={s.Stats.FpsAvg:F0}");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : "no sessions";
        }

        internal static string FormatStatus(string sessionId, int frames, float elapsedS, float durationS)
        {
            return durationS > 0f
                ? $"recording session:{sessionId} {frames}frames {elapsedS:F1}s/{durationS:F1}s"
                : $"recording session:{sessionId} {frames}frames {elapsedS:F1}s";
        }

        private static long MB(long bytes) => bytes / 1_048_576;

        private static string FormatTris(long tris)
        {
            if (tris >= 1_000_000) return $"{tris / 1_000_000f:F1}M";
            if (tris >= 1_000) return $"{tris / 1_000f:F1}K";
            return tris.ToString();
        }
    }
}
