// AtMentionNormalizer: converts @Name mentions in LLM responses to [kind:ref] tags.
// Fixes Bug 2: LLM echoes @Name back; normalizer converts to bracket format
// so MixedParagraphRenderer can render them as pills.
// Uses manual longest-first scan (not regex) to correctly handle multi-word names.
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Replaces @Name mentions in LLM response text with [kind:path #id] bracket tags
    /// when a matching chip exists in the sent chips for the turn.
    /// Multi-word names handled via longest-first matching.
    /// </summary>
    internal static class AtMentionNormalizer
    {
        /// <summary>Normalize @Name mentions in text using sentChips as the lookup source.</summary>
        internal static string Normalize(string text, IReadOnlyList<ChipData> sentChips)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (sentChips == null || sentChips.Count == 0) return text;

            // Longest-first prevents @Main matching when @Main Camera exists.
            var sorted = sentChips
                .Where(c => !string.IsNullOrEmpty(c.DisplayName))
                .OrderByDescending(c => c.DisplayName.Length)
                .ToList();
            if (sorted.Count == 0) return text;

            var sb    = new StringBuilder(text.Length);
            int pos   = 0;

            while (pos < text.Length)
            {
                // Look for '@' character
                int at = text.IndexOf('@', pos);
                if (at < 0)
                {
                    sb.Append(text, pos, text.Length - pos);
                    break;
                }

                // Copy text before '@'
                sb.Append(text, pos, at - pos);

                // Try to match each chip name (longest first) after '@'
                int nameStart = at + 1;
                bool matched  = false;
                foreach (var chip in sorted)
                {
                    var name = chip.DisplayName;
                    if (nameStart + name.Length > text.Length) continue;
                    // Case-insensitive compare
                    if (string.Compare(text, nameStart, name, 0, name.Length,
                            System.StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        // Ensure the match doesn't continue into more word chars
                        int after = nameStart + name.Length;
                        bool boundary = after >= text.Length
                            || !char.IsLetterOrDigit(text[after]) && text[after] != '_';
                        if (boundary)
                        {
                            sb.Append(ChipContextResolver.FormatChipRef(
                                chip.KindKey, chip.Path, chip.InstanceID));
                            pos    = after;
                            matched = true;
                            break;
                        }
                    }
                }

                if (!matched)
                {
                    sb.Append('@');
                    pos = nameStart;
                }
            }
            return sb.ToString();
        }
    }
}
