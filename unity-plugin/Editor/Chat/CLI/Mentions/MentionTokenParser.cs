using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Scans backward from cursor to detect an unresolved @mention token.
    /// Pure static — no allocations beyond the returned query string.
    /// </summary>
    internal static class MentionTokenParser
    {
        internal static bool TryExtract(
            string text, int cursorPos,
            IReadOnlyList<PositionedChip> chips,
            out int atIndex, out string query)
        {
            atIndex = -1;
            query   = null;

            if (string.IsNullOrEmpty(text) || cursorPos <= 0) return false;

            // Walk backward from cursor to find '@'
            int i = cursorPos - 1;
            while (i >= 0)
            {
                char c = text[i];
                // Stop if we hit whitespace before finding '@'
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') return false;
                if (c == '@') break;
                i--;
            }

            if (i < 0) return false; // no '@' found

            // '@' must be at start OR preceded by whitespace/newline
            if (i > 0)
            {
                char prev = text[i - 1];
                if (prev != ' ' && prev != '\t' && prev != '\n' && prev != '\r') return false;
            }

            // The char immediately after '@' (if any, within cursor range) must be letter/digit/_/-
            int afterAt = i + 1;
            if (afterAt < cursorPos)
            {
                char first = text[afterAt];
                if (!IsValidTokenChar(first)) return false;
            }

            // Reject if a resolved chip already occupies this '@' offset
            if (chips != null)
            {
                foreach (var pc in chips)
                    if (pc.TextOffset == i) return false;
            }

            atIndex = i;
            query   = text.Substring(afterAt, cursorPos - afterAt);
            return true;
        }

        private static bool IsValidTokenChar(char c)
            => char.IsLetterOrDigit(c) || c == '_' || c == '-';
    }
}
