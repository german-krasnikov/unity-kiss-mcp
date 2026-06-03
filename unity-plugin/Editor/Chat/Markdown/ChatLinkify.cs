// Post-processes rich text: wraps resolved <color=#9aa5ce> spans in <link> tags.
// PURE — zero UnityEngine/UnityEditor dependencies. NUnit-testable.
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatLinkify
    {
        internal delegate string Resolver(string name);

        // Matches <color=CODECOLOR>CONTENT</color> — non-greedy, handles nested tags.
        // Pattern is built from MarkdownInline.CodeColor (single source) so a colour change
        // there can't silently break linkification.
        private static readonly Regex _span = new Regex(
            @"<color=" + Regex.Escape(MarkdownInline.CodeColor) + @">(.*?)</color>",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // Detects if a code span is already inside a <link ...> </link> pair.
        // We look for <link= that is NOT closed before our match position.
        private static readonly Regex _linkOpen  = new Regex(@"<link=",   RegexOptions.Compiled);
        private static readonly Regex _linkClose = new Regex(@"</link>",  RegexOptions.Compiled);

        /// <summary>
        /// Scans richText for inline-code color spans and wraps resolved ones in
        /// &lt;link&gt; tags with underline. Unresolved spans pass through unchanged.
        /// Priority: object > script > asset.
        /// Asset resolver should be path-based (span must start with "Assets/") to avoid
        /// false-positive links on bare names.
        /// </summary>
        internal static string Apply(string richText, Resolver resolveObject, Resolver resolveScript, Resolver resolveAsset)
        {
            if (richText == null) return null;
            if (richText == "") return "";

            return _span.Replace(richText, m =>
            {
                // Skip spans already inside a <link> tag by counting opens/closes before this match.
                var before     = richText.Substring(0, m.Index);
                var openCount  = _linkOpen.Matches(before).Count;
                var closeCount = _linkClose.Matches(before).Count;
                if (openCount > closeCount) return m.Value; // inside existing link

                var inner = m.Groups[1].Value;          // rich text inside the color span
                var name  = StripNoparse(inner);         // clean name for resolver lookup

                var payload = resolveObject?.Invoke(name);
                if (payload != null)
                    return WrapLink("obj:" + payload, m.Value);

                payload = resolveScript?.Invoke(name);
                if (payload != null)
                    return WrapLink("script:" + payload, m.Value);

                payload = resolveAsset?.Invoke(name);
                if (payload != null)
                    return WrapLink("asset:" + payload, m.Value);

                return m.Value; // unresolved — pass through unchanged
            });
        }

        // Wraps colorSpan with <link> + <u> to signal clickability visually.
        private static string WrapLink(string linkId, string colorSpan) =>
            "<link=\"" + linkId + "\"><u>" + colorSpan + "</u></link>";

        /// <summary>Strips &lt;noparse&gt;X&lt;/noparse&gt; pairs, returning the inner X.
        /// Used to reconstruct the plain identifier name for resolver lookup.</summary>
        internal static string StripNoparse(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            // <noparse><</noparse> -> <  (MarkdownInline.Escape pattern)
            return Regex.Replace(s, @"<noparse>(.*?)</noparse>", "$1");
        }
    }
}
