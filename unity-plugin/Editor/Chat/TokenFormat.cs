namespace UnityMCP.Editor.Chat
{
    public static class TokenFormat
    {
        public static string Abbr(int n) =>
            n >= 1000 ? $"{n / 1000f:0.0}k" : n.ToString();
    }
}
