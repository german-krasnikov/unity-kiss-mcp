using UnityEngine;

namespace UnityMCP.Editor.Profiling
{
    internal static class PerfThresholds
    {
        static readonly Color32 Good = new(0x3A, 0xD2, 0x9F, 0xFF); // #3ad29f
        static readonly Color32 Warn = new(0xE8, 0xA2, 0x3A, 0xFF); // #e8a23a
        static readonly Color32 Crit = new(0xE9, 0x45, 0x60, 0xFF); // #e94560

        internal static string FpsBand(float fps)      => fps > 50f ? "good" : fps > 30f ? "warn" : "crit";
        internal static string FrameTimeBand(float ms)  => ms < 16.6f ? "good" : ms < 33.3f ? "warn" : "crit";
        internal static string DrawCallBand(int dc)    => dc < 500 ? "good" : dc < 2000 ? "warn" : "crit";
        internal static string TriBand(long tris)      => tris < 1_000_000 ? "good" : tris < 5_000_000 ? "warn" : "crit";
        internal static string MemBand(long usedMB)    => usedMB < 512 ? "good" : usedMB < 1024 ? "warn" : "crit";

        internal static Color32 ColorForBand(string band) => band switch
        {
            "good" => Good,
            "warn" => Warn,
            _      => Crit
        };

        // Smooth lerp between thresholds — no hard color steps.
        internal static Color32 FpsColor(float fps)
        {
            if (fps >= 60f) return Good;
            if (fps >= 30f) return Color32.Lerp(Warn, Good, (fps - 30f) / 30f);
            return Color32.Lerp(Crit, Warn, Mathf.Clamp01(fps / 30f));
        }

        internal static Color32 DrawCallColor(int dc)
        {
            if (dc < 500) return Good;
            if (dc < 2000) return Color32.Lerp(Good, Warn, (dc - 500f) / 1500f);
            return Crit;
        }

        internal static Color32 TriColor(long tris)
        {
            if (tris < 1_000_000) return Good;
            if (tris < 5_000_000) return Color32.Lerp(Good, Warn, (tris - 1_000_000f) / 4_000_000f);
            return Crit;
        }
    }
}
