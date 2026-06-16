namespace UnityMCP.Editor.Chat
{
    public static class TokenFormat
    {
        public static string Abbr(int n) =>
            n >= 1000 ? $"{n / 1000f:0.0}k" : n.ToString();

        public static string FormatReadout(int inp, int out_, float costUsd)
        {
            if (inp == 0 && out_ == 0 && costUsd == 0f) return "";
            var cost = costUsd > 0f ? $"  ${costUsd:0.0000}" : "";
            return $"↑ {Abbr(inp)}  ↓ {Abbr(out_)}{cost}";
        }
    }
}
