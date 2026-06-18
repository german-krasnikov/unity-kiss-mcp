// Formatter over ResponseTagTokenizer: turns Tag/BarePath tokens into rich-text pills.
// Apply() replaces tokens with <link> rich text; Split() returns ordered TagSegments.
// NormalizeBarePaths is kept as a tokenizer-based wrapper for backward compatibility
// with MixedParagraphRenderer (to be refactored in Dev 2).
// H2: linkId format is "chip:KEY:REF".
using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    public static class ResponseTagInliner
    {
        /// <summary>
        /// Replace [kind:ref] bracket tags, ⟦kind:ref⟧ unicode fences, and bare paths
        /// with colored rich-text pills wrapped in link tags.
        /// Returns input unchanged if no recognized tokens are found.
        /// </summary>
        public static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var tokens = ResponseTagTokenizer.Tokenize(text);
            if (tokens.Count == 0) return text;

            var sb = new StringBuilder(text.Length);
            foreach (var token in tokens)
            {
                switch (token.Kind)
                {
                    case TokenKind.Text:
                        sb.Append(token.Raw);
                        break;
                    case TokenKind.Tag:
                    case TokenKind.BarePath:
                        sb.Append(FormatTag(token.KindKey, token.Ref));
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Extract typed (KindKey, Ref) pairs from text without replacing.</summary>
        internal static List<(string KindKey, string Ref)> ExtractTags(string text)
        {
            var result = new List<(string, string)>();
            if (string.IsNullOrEmpty(text)) return result;

            foreach (var token in ResponseTagTokenizer.Tokenize(text))
            {
                if (token.Kind == TokenKind.Tag || token.Kind == TokenKind.BarePath)
                    result.Add((token.KindKey, token.Ref));
            }
            return result;
        }

        /// <summary>True if text contains at least one recognized tag or bare path token.</summary>
        internal static bool HasTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var token in ResponseTagTokenizer.Tokenize(text))
                if (token.Kind == TokenKind.Tag || token.Kind == TokenKind.BarePath)
                    return true;
            return false;
        }

        /// <summary>
        /// Backward-compat wrapper: wraps bare image-path tokens (e.g. "img.png" or "`img.png`")
        /// as ⟦image:path⟧ tags. Implemented via the tokenizer so the parsing logic stays single-source.
        /// </summary>
        internal static string NormalizeBarePaths(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var tokens = ResponseTagTokenizer.Tokenize(text);
            if (tokens.Count == 0) return text;

            var sb = new StringBuilder(text.Length);
            foreach (var token in tokens)
            {
                if (token.Kind == TokenKind.BarePath)
                    sb.Append("⟦").Append(token.KindKey).Append(":").Append(token.Ref).Append("⟧");
                else
                    sb.Append(token.Raw);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Split text into ordered segments: literal text runs and parsed tags/bare paths.
        /// </summary>
        internal static IReadOnlyList<TagSegment> Split(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<TagSegment>();

            var tokens = ResponseTagTokenizer.Tokenize(text);
            var result = new List<TagSegment>(tokens.Count);
            foreach (var token in tokens)
            {
                if (token.Kind == TokenKind.Text)
                    result.Add(new TagSegment(token.Raw));
                else
                    result.Add(new TagSegment(token.KindKey, token.Ref));
            }
            return result;
        }

        // ── private ───────────────────────────────────────────────────────────

        private static string FormatTag(string kindKey, string rawRef)
        {
            var color = ChipPillFactory.ColorResolver?.Invoke(kindKey)
                ?? ChipKindRegistry.ForKey(kindKey)?.HexColor ?? "#94a3b8";
            var linkId = "chip:" + kindKey + ":" + rawRef; // H2
            return $"<link=\"{linkId}\"><color={color}><b>[{kindKey}]</b></color> {rawRef}</link>";
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
