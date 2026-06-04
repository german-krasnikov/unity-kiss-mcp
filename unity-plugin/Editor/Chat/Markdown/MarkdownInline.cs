// Converts inline Markdown spans to Unity rich-text. Pure, NUnit-testable.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    public static class MarkdownInline
    {
        internal const string CodeColor = "#9aa5ce"; // single source; ChatLinkify matches this
        private const string LinkColor = "#566677";

        // U+FDD0/U+FDD1 are permanent Unicode non-characters — they can never appear in
        // real text, so they are collision-proof placeholders for extracted code spans.
        private const string SlotOpen  = "﷐";
        private const string SlotClose = "﷑";

        private static readonly Regex _code  = new Regex(@"`([^`]+)`",         RegexOptions.Compiled);
        private static readonly Regex _bold  = new Regex(@"\*\*(.+?)\*\*",     RegexOptions.Compiled);
        private static readonly Regex _under = new Regex(@"\b_(.+?)_\b",       RegexOptions.Compiled);
        private static readonly Regex _ital  = new Regex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex _link  = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);

        /// <summary>Neutralizes a literal '&lt;' with a noparse scope so UIToolkit rich-text
        /// won't treat it as a tag. UIToolkit decodes NO HTML entities, so emitting '&amp;lt;'
        /// renders verbatim (the bug). '&amp;' and '&gt;' are harmless and pass through.</summary>
        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("<", "<noparse><</noparse>");
        }

        /// <summary>
        /// Converts inline markdown to Unity rich-text. Neutralizes literal '&lt;' FIRST
        /// (via noparse) so raw HTML from input never becomes real tags. Code spans protect inner stars.
        /// </summary>
        public static string ToRichText(string span)
        {
            if (span == null) return null;
            if (span == "") return "";

            // Step 1: extract code spans before escaping so their content is preserved verbatim.
            var codeSlots = new List<string>();
            var withPlaceholders = _code.Replace(span, m =>
            {
                codeSlots.Add(m.Groups[1].Value); // raw inner text
                return SlotOpen + (codeSlots.Count - 1) + SlotClose;
            });

            // Step 2: escape HTML in the non-code text.
            withPlaceholders = Escape(withPlaceholders);

            // Step 2b: replace AI response bracket tags [kind:ref] with rich-text pills.
            // Must run after escape (so < inside refs is neutralized) and before bold/italic
            // (so the emitted <color>/<link> tags are not processed as markdown).
            withPlaceholders = ResponseTagInliner.Apply(withPlaceholders);

            // Step 3: apply bold, italic, links.
            withPlaceholders = _bold.Replace(withPlaceholders, "<b>$1</b>");
            withPlaceholders = _under.Replace(withPlaceholders, "<i>$1</i>");
            withPlaceholders = _ital.Replace(withPlaceholders, "<i>$1</i>");
            withPlaceholders = _link.Replace(withPlaceholders,
                m => m.Groups[1].Value + " <color=" + LinkColor + ">" + m.Groups[2].Value + "</color>");

            // Step 4: restore code spans as colored (not italic — code isn't emphasis).
            // Inner code text is escaped but stars left literal (no bold/italic processing).
            for (int i = 0; i < codeSlots.Count; i++)
            {
                var inner = Escape(codeSlots[i]); // escape HTML in code content
                var rendered = "<color=" + CodeColor + ">" + inner + "</color>";
                withPlaceholders = withPlaceholders.Replace(
                    SlotOpen + i + SlotClose, rendered);
            }

            return withPlaceholders;
        }
    }
}
