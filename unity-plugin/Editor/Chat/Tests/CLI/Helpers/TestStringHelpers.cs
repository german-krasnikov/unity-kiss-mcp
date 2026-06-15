using System;

namespace UnityMCP.Editor.Chat.Tests
{
    public static class TestStringHelpers
    {
        public static int CountOccurrences(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return 0;
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) != -1)
            {
                count++;
                idx += token.Length;
            }
            return count;
        }
    }
}
