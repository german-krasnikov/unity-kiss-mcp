// Shell-style tokenizer for ExtraArgs fields: splits on whitespace but respects
// "double" and 'single' quoted spans so multi-word values survive.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal static class ArgTokenizer
    {
        /// <summary>
        /// Splits <paramref name="raw"/> into argv tokens.
        /// Rules:
        ///  • Whitespace (space/tab) is a separator unless inside a quoted span.
        ///  • "double-quoted" or 'single-quoted' spans become one token (quotes stripped).
        ///  • Unbalanced opening quote → rest of string treated as one token.
        ///  • Empty tokens dropped.
        /// </summary>
        public static List<string> Split(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(raw)) return result;

            int  i        = 0;
            int  len      = raw.Length;

            while (i < len)
            {
                // Skip whitespace between tokens.
                while (i < len && (raw[i] == ' ' || raw[i] == '\t')) i++;
                if (i >= len) break;

                char   quoteChar = '\0';
                var    token     = new System.Text.StringBuilder();

                // Peek: does this token open with a quote?
                if (raw[i] == '"' || raw[i] == '\'')
                {
                    quoteChar = raw[i];
                    i++; // skip opening quote
                    // Consume until matching close quote or end of string.
                    while (i < len && raw[i] != quoteChar)
                        token.Append(raw[i++]);
                    if (i < len) i++; // skip closing quote
                    // A quoted token may be followed immediately by more chars — collect them.
                    while (i < len && raw[i] != ' ' && raw[i] != '\t')
                        token.Append(raw[i++]);
                }
                else
                {
                    // Unquoted run: stop at whitespace or a quote start.
                    while (i < len && raw[i] != ' ' && raw[i] != '\t')
                    {
                        if (raw[i] == '"' || raw[i] == '\'')
                        {
                            // Inline quote inside a token (e.g. foo"bar baz") — consume quoted span.
                            quoteChar = raw[i++];
                            while (i < len && raw[i] != quoteChar)
                                token.Append(raw[i++]);
                            if (i < len) i++; // skip closing quote
                        }
                        else
                        {
                            token.Append(raw[i++]);
                        }
                    }
                }

                if (token.Length > 0)
                    result.Add(token.ToString());
            }

            return result;
        }
    }
}
