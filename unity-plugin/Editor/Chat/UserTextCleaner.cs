using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    internal static class UserTextCleaner
    {
        private static readonly Regex AtMention = new Regex(
            @"(?<=^|\s)@[\w.]+ ?", RegexOptions.Compiled);

        internal static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var cleaned = AtMention.Replace(text, "");
            cleaned = Regex.Replace(cleaned, @"  +", " ").Trim();
            return cleaned;
        }
    }
}
