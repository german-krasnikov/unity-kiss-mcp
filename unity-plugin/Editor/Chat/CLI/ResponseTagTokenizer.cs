// Single-pass tokenizer for chip reference syntax.
// Emits Text | Tag | BarePath tokens for [kind:ref], ⟦kind:ref⟧ and bare file paths.
// Bare-path extensions are sourced from IChipKindProvider.BarePathExtensions (no hardcoded list).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    public enum TokenKind
    {
        Text,
        Tag,
        BarePath
    }

    public readonly struct TagToken
    {
        public TokenKind Kind { get; }
        public string Raw { get; }
        public string KindKey { get; }
        public string Ref { get; }

        public TagToken(TokenKind kind, string raw, string kindKey, string refValue)
        {
            Kind = kind;
            Raw = raw;
            KindKey = kindKey;
            Ref = refValue;
        }

        public static TagToken Text(string raw) => new(TokenKind.Text, raw, null, null);
        public static TagToken Tag(string kindKey, string rawRef) => new(TokenKind.Tag, null, kindKey, rawRef);
        public static TagToken BarePath(string raw, string kindKey) => new(TokenKind.BarePath, raw, kindKey, raw);
    }

    public static class ResponseTagTokenizer
    {
        private static int _cachedVersion = -1;
        private static Dictionary<string, string> _extensionToKind;
        private static Regex _cachedBacktickRx;
        private static Regex _cachedTokenRx;

        public static IReadOnlyList<TagToken> Tokenize(ReadOnlySpan<char> text)
        {
            if (text.IsEmpty) return Array.Empty<TagToken>();

            var extMap = GetExtensionMap();
            var result = new List<TagToken>();
            int i = 0;
            int start = 0;

            while (i < text.Length)
            {
                if (text[i] == '\u27E6') // ⟦
                {
                    if (TryParseFence(text, i, out var kindKey, out var refText, out var endIndex))
                    {
                        if (i > start)
                            SplitText(result, text.Slice(start, i - start), extMap);
                        result.Add(TagToken.Tag(kindKey, refText));
                        i = endIndex;
                        start = i;
                        continue;
                    }

                    // Unknown fence: skip to closing ⟧ unless it contains a nested opener.
                    int close = text.Slice(i + 1).IndexOf('\u27E7');
                    if (close >= 0 && !ContainsOpener(text.Slice(i + 1, close)))
                    {
                        i += close + 2;
                        continue;
                    }
                }
                else if (text[i] == '[')
                {
                    if (TryParseBracket(text, i, out var kindKey, out var refText, out var endIndex))
                    {
                        if (i > start)
                            SplitText(result, text.Slice(start, i - start), extMap);
                        result.Add(TagToken.Tag(kindKey, refText));
                        i = endIndex;
                        start = i;
                        continue;
                    }

                    // Unknown/non-kind bracket: skip to closing ] unless it contains a nested opener.
                    int close = text.Slice(i + 1).IndexOf(']');
                    if (close >= 0 && !ContainsOpener(text.Slice(i + 1, close)))
                    {
                        i += close + 2;
                        continue;
                    }
                }

                i++;
            }

            if (start < text.Length)
                SplitText(result, text.Slice(start), extMap);

            return result;
        }

        public static IReadOnlyList<TagToken> Tokenize(string text)
            => text == null ? Array.Empty<TagToken>() : Tokenize(text.AsSpan());

        // ── tag parsers ─────────────────────────────────────────────────────────

        private static bool TryParseFence(ReadOnlySpan<char> text, int start,
            out string kindKey, out string refText, out int endIndex)
        {
            kindKey = null;
            refText = null;
            endIndex = start;

            var afterOpen = text.Slice(start + 1);
            int colon = afterOpen.IndexOf(':');
            if (colon < 0) return false;

            int close = afterOpen.IndexOf('\u27E7'); // ⟧
            if (close < 0 || close < colon) return false;

            kindKey = new string(afterOpen.Slice(0, colon)).ToLowerInvariant();
            if (ChipKindRegistry.ForKey(kindKey) == null) return false;

            int refLen = close - colon - 1;
            refText = refLen > 0
                ? new string(afterOpen.Slice(colon + 1, refLen))
                : "";
            endIndex = start + 1 + close + 1;
            return true;
        }

        private static bool TryParseBracket(ReadOnlySpan<char> text, int start,
            out string kindKey, out string refText, out int endIndex)
        {
            kindKey = null;
            refText = null;
            endIndex = start;

            int j = start + 1;
            while (j < text.Length && text[j] != ':' && text[j] != ']') j++;
            if (j >= text.Length || text[j] != ':') return false;

            int kindLen = j - (start + 1);
            if (kindLen == 0) return false;

            kindKey = new string(text.Slice(start + 1, kindLen)).ToLowerInvariant();
            if (ChipKindRegistry.ForKey(kindKey) == null) return false;

            int refStart = j + 1;
            int k = refStart;
            int depth = 0;
            while (k < text.Length)
            {
                if (text[k] == '[')
                {
                    depth++;
                }
                else if (text[k] == ']')
                {
                    if (depth == 0) break;
                    depth--;
                }
                k++;
            }

            if (k >= text.Length || text[k] != ']' || k == refStart) return false;

            refText = new string(text.Slice(refStart, k - refStart));
            endIndex = k + 1;
            return true;
        }

        // ── bare-path tokenization ──────────────────────────────────────────────

        private static void SplitText(List<TagToken> result, ReadOnlySpan<char> span,
            Dictionary<string, string> extMap)
        {
            if (span.IsEmpty) return;
            if (extMap.Count == 0)
            {
                result.Add(TagToken.Text(new string(span)));
                return;
            }

            var text = new string(span);
            var backtickRx = GetBacktickRegex();
            var tokenRx = GetTokenRegex();

            int pos = 0;
            foreach (Match m in backtickRx.Matches(text))
            {
                if (m.Index > pos)
                    result.Add(TagToken.Text(text.Substring(pos, m.Index - pos)));

                var raw = m.Groups[1].Value.Trim('`');
                var ext = GetExtension(raw);
                if (extMap.TryGetValue(ext, out var kindKey))
                    result.Add(TagToken.BarePath(raw, kindKey));
                else
                    result.Add(TagToken.Text(m.Value));

                pos = m.Index + m.Length;
            }

            if (pos < text.Length)
            {
                string tail = text.Substring(pos);
                int tpos = 0;
                foreach (Match m in tokenRx.Matches(tail))
                {
                    if (m.Index > tpos)
                        result.Add(TagToken.Text(tail.Substring(tpos, m.Index - tpos)));

                    var raw = m.Groups[1].Value;
                    var ext = GetExtension(raw);
                    if (extMap.TryGetValue(ext, out var kindKey))
                        result.Add(TagToken.BarePath(raw, kindKey));
                    else
                        result.Add(TagToken.Text(m.Value));

                    tpos = m.Index + m.Length;
                }

                if (tpos < tail.Length)
                    result.Add(TagToken.Text(tail.Substring(tpos)));
            }
        }

        private static Regex GetBacktickRegex()
        {
            if (_cachedBacktickRx != null) return _cachedBacktickRx;
            var extPattern = string.Join("|", _extensionToKind.Keys.Select(e => e.TrimStart('.')));
            _cachedBacktickRx = new Regex($@"(?:^|(?<=\s))(`[^`]+\.(?:{extPattern})`)(?=\s|$)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return _cachedBacktickRx;
        }

        private static Regex GetTokenRegex()
        {
            if (_cachedTokenRx != null) return _cachedTokenRx;
            var extPattern = string.Join("|", _extensionToKind.Keys.Select(e => e.TrimStart('.')));
            _cachedTokenRx = new Regex($@"(?:^|(?<=\s))([^\s`\[\]\u27E6\u27E7]+\.(?:{extPattern}))(?=\s|$)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return _cachedTokenRx;
        }

        private static bool ContainsOpener(ReadOnlySpan<char> span)
        {
            for (int k = 0; k < span.Length; k++)
                if (span[k] == '[' || span[k] == '\u27E6')
                    return true;
            return false;
        }

        private static string GetExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(dot).ToLowerInvariant() : "";
        }

        private static Dictionary<string, string> GetExtensionMap()
        {
            int version = ChipKindRegistry.Version;
            if (_cachedVersion == version && _extensionToKind != null)
                return _extensionToKind;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in ChipKindRegistry.AllKeys)
            {
                var provider = ChipKindRegistry.ForKey(key);
                if (provider?.BarePathExtensions == null) continue;
                foreach (var ext in provider.BarePathExtensions)
                {
                    if (string.IsNullOrEmpty(ext)) continue;
                    var normalized = ext.StartsWith(".") ? ext : "." + ext;
                    if (!map.ContainsKey(normalized))
                        map[normalized] = key;
                }
            }

            _extensionToKind = map;
            _cachedVersion = version;
            _cachedBacktickRx = null;
            _cachedTokenRx = null;
            return map;
        }
    }
}
