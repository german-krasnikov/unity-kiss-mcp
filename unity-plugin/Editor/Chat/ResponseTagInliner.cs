// Pure: replaces [kind:ref] bracket tags in AI response text with rich-text pills.
// Dynamic regex rebuilt from ChipKindRegistry.AllKeys (longest-first, Regex.Escaped).
// Cached on ChipKindRegistry.Version — auto-refreshes when plugins register new kinds.
// H2: linkId format is "chip:KEY:REF".
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    internal static class ResponseTagInliner
    {
        private static int   _cachedVersion = -1;
        private static Regex _cachedRegex;

        /// <summary>
        /// Replace [kind:ref] bracket tags with colored rich-text pills wrapped in link tags.
        /// Returns input unchanged if no recognized tags found.
        /// </summary>
        internal static string Apply(string text)
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
}
