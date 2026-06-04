// Pure static: replaces [kind:ref] bracket tags in AI response text with rich-text pills.
// Only matches the exact format that Surface 1 (ChipContextResolver.FormatChipRef) emits.
// Conservative: unrecognized kind → left unchanged. Pure, NUnit-testable.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    internal static class ResponseTagInliner
    {
        // Matches ONLY the known kind prefixes — highly conservative to avoid false positives.
        // Requires non-empty ref ([kind:] with nothing after colon is NOT matched).
        private static readonly Regex _tag = new Regex(
            @"\[(?<kind>hierarchy|scene|script|prefab|material|texture|so|asset):(?<ref>[^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Kind-to-color map for visual differentiation.
        private static readonly Dictionary<string, string> _colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "hierarchy",  "#4a9eff" },
            { "scene",      "#c084fc" },
            { "script",     "#4ade80" },
            { "prefab",     "#60a5fa" },
            { "material",   "#f97316" },
            { "texture",    "#facc15" },
            { "so",         "#fb7185" },
            { "asset",      "#94a3b8" },
        };

        /// <summary>
        /// Replace [kind:ref] bracket tags with colored rich-text pills wrapped in link tags.
        /// Returns input unchanged if no recognized tags found.
        /// </summary>
        internal static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return _tag.Replace(text, m =>
            {
                var kind  = m.Groups["kind"].Value.ToLowerInvariant();
                var refer = m.Groups["ref"].Value;
                var color = _colors.TryGetValue(kind, out var c) ? c : "#94a3b8";
                // Link ID for ChatRefAction navigation: use obj: for hierarchy, asset: otherwise.
                var linkId = kind == "hierarchy" ? "obj:" + refer : "asset:" + refer;
                return $"<link=\"{linkId}\"><color={color}><b>[{kind}]</b></color> {refer}</link>";
            });
        }

        /// <summary>
        /// Extract typed (Kind, Ref) pairs from text without replacing. For testing.
        /// </summary>
        internal static List<(ChipKind Kind, string Ref)> ExtractTags(string text)
        {
            var result = new List<(ChipKind, string)>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (Match m in _tag.Matches(text))
            {
                var kindStr = m.Groups["kind"].Value.ToLowerInvariant();
                var refer   = m.Groups["ref"].Value;
                result.Add((ParseKind(kindStr), refer));
            }
            return result;
        }

        private static ChipKind ParseKind(string s)
        {
            switch (s)
            {
                case "hierarchy": return ChipKind.Hierarchy;
                case "scene":     return ChipKind.Scene;
                case "script":    return ChipKind.Script;
                case "prefab":    return ChipKind.Prefab;
                case "material":  return ChipKind.Material;
                case "texture":   return ChipKind.Texture;
                case "so":        return ChipKind.ScriptableObject;
                default:          return ChipKind.Asset;
            }
        }
    }
}
