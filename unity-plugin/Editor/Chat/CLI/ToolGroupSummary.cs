// Pure static helper — zero UnityEngine deps. Tested by ToolGroupSummaryTests.
namespace UnityMCP.Editor.Chat
{
    public static class ToolGroupSummary
    {
        public static string Format(int count, bool anyError, bool running)
        {
            var noun = count == 1 ? "tool" : "tools";
            var s = $"⚙ {count} {noun}";
            if (anyError) s += " ✕";
            if (running)  s += "...";
            return s;
        }
    }
}
