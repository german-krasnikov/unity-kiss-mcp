// Pure: replaces [kind:ref] bracket tags in AI response text with rich-text pills.
// Dynamic regex rebuilt from ChipKindRegistry.AllKeys (longest-first, Regex.Escaped).
// Cached on ChipKindRegistry.Version — auto-refreshes when plugins register new kinds.
// H2: linkId format is "chip:KEY:REF".
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    public static class ResponseTagInliner
    {
        private static int   _cachedVersion = -1;
        private static Regex _cachedRegex;

        /// <summary>
        /// Replace [kind:ref] bracket tags with colored rich-text pills wrapped in link tags.
        /// Returns input unchanged if no recognized tags found.
        /// </summary>
        public static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var rx = GetOrRebuildRegex();
            return rx.Replace(text, m =>
            {
                var kind  = m.Groups["kind"].Value.ToLowerInvariant();
                var refer = m.Groups["ref"].Value;
                // P4: honor per-kind color overrides via the same resolver as ChipPillFactory.
                var color = ChipPillFactory.ColorResolver?.Invoke(kind)
                    ?? ChipKindRegistry.ForKey(kind)?.HexColor ?? "#94a3b8";
                var linkId = "chip:" + kind + ":" + refer; // H2
                return $"<link=\"{linkId}\"><color={color}><b>[{kind}]</b></color> {refer}</link>";
            });
        }

        /// <summary>Extract typed (KindKey, Ref) pairs from text without replacing.</summary>
        internal static List<(string KindKey, string Ref)> ExtractTags(string text)
        {
            var result = new List<(string, string)>();
            if (string.IsNullOrEmpty(text)) return result;
            var rx = GetOrRebuildRegex();
            foreach (Match m in rx.Matches(text))
            {
                var kindKey = m.Groups["kind"].Value.ToLowerInvariant();
                var refer   = m.Groups["ref"].Value;
                result.Add((kindKey, refer));
            }
            return result;
        }

        /// <summary>True if text contains at least one recognized [kind:ref] tag.</summary>
        internal static bool HasTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return GetOrRebuildRegex().IsMatch(text);
        }

        /// <summary>
        /// Split text into ordered segments: literal text runs and parsed tags.
        /// Adjacent tags produce no empty text segments between them.
        /// </summary>
        internal static IReadOnlyList<TagSegment> Split(string text)
        {
            var result = new List<TagSegment>();
            if (string.IsNullOrEmpty(text)) return result;
            var rx  = GetOrRebuildRegex();
            int pos = 0;
            foreach (Match m in rx.Matches(text))
            {
                if (m.Index > pos)
                    result.Add(new TagSegment(text.Substring(pos, m.Index - pos)));
                var kind  = m.Groups["kind"].Value.ToLowerInvariant();
                var refer = m.Groups["ref"].Value;
                result.Add(new TagSegment(kind, refer));
                pos = m.Index + m.Length;
            }
            if (pos < text.Length)
                result.Add(new TagSegment(text.Substring(pos)));
            return result;
        }

        // ── private ───────────────────────────────────────────────────────────

        private static Regex GetOrRebuildRegex()
        {
            if (_cachedRegex != null && _cachedVersion == ChipKindRegistry.Version)
                return _cachedRegex;

            // Longest-first so longer keys like "scriptableobject" beat "script" (if ever added).
            var keys = ChipKindRegistry.AllKeys
                .OrderByDescending(k => k.Length)
                .Select(Regex.Escape);
            var pattern = @"\[(?<kind>" + string.Join("|", keys) + @"):(?<ref>[^\]]+)\]";
            _cachedRegex    = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            _cachedVersion  = ChipKindRegistry.Version;
            return _cachedRegex;
        }
    }

    // ── TagSegment ────────────────────────────────────────────────────────────

    /// <summary>One segment of a Split result: either literal text or a parsed tag.</summary>
    internal readonly struct TagSegment
    {
        internal readonly bool   IsTag;
        internal readonly string Text;    // literal text when !IsTag; raw ref when IsTag
        internal readonly string KindKey; // non-null only when IsTag

        internal TagSegment(string text)                   { IsTag = false; Text = text;   KindKey = null; }
        internal TagSegment(string kindKey, string rawRef) { IsTag = true;  Text = rawRef; KindKey = kindKey; }
    }

}
