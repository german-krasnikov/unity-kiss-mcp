// Pure height-calculation logic for the auto-growing input panel.
// Zero UnityEngine deps, fully NUnit-testable.
namespace UnityMCP.Editor.Chat
{
    internal sealed class InputHeightCalc
    {
        internal const float CompactH   = 117f; // 4 lines: 4*18 + PadH(14) + ActionBarH(31)
        internal const float LineH      = 18f;
        internal const float PadH       = 14f;
        internal const float ActionBarH = 31f;
        internal const float AbsMaxH    = 300f;
        internal const float WindowFrac = 0.4f;

        internal bool  ManualOverride { get; private set; }
        internal float ManualHeight   { get; private set; }

        internal void SetManual(float h)
        {
            ManualOverride = true;
            ManualHeight   = Clamp(h, CompactH, AbsMaxH);
        }

        internal void Reset()
        {
            ManualOverride = false;
            ManualHeight   = 0f;
        }

        internal float Compute(int lineCount, float windowH, bool hasChips)
        {
            if (ManualOverride) return ManualHeight;
            if (windowH <= 0f) windowH = 600f;
            float chipH  = hasChips ? 24f : 0f;
            float textH  = System.Math.Max(lineCount, 1) * LineH + PadH;
            float areaH  = textH + ActionBarH + chipH;
            float maxH   = (float)System.Math.Min(windowH * WindowFrac, AbsMaxH);
            float minH   = (float)System.Math.Min(CompactH, maxH); // tiny window: respect cap
            return Clamp(areaH, minH, maxH);
        }

        internal float ComputeMax(float windowH)
        {
            if (windowH <= 0f) windowH = 600f;
            return (float)System.Math.Min(windowH * WindowFrac, AbsMaxH);
        }

        internal static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1;
            int n = 1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') n++;
            return n;
        }

        private static float Clamp(float v, float min, float max)
            => v < min ? min : v > max ? max : v;
    }
}
