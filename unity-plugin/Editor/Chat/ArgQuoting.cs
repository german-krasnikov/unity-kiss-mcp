// Pure arg-quoting for Mono direct-exec (not shell). Only whitespace/quote/backslash
// matter to the Mono tokenizer. $ and backtick are literal but we wrap them defensively.
using System.Text;

namespace UnityMCP.Editor.Chat
{
    internal static class ArgQuoting
    {
        internal static string Quote(string a)
        {
            if (a == null) a = "";

            var needsQuote = a.Length == 0;
            if (!needsQuote)
                foreach (var c in a)
                    if (c == ' ' || c == '\t' || c == '\n' || c == '"' || c == '\\' || c == '$' || c == '`')
                    { needsQuote = true; break; }

            if (!needsQuote) return a;

            // Escape backslash first, then double-quote — order is critical to avoid double-escaping.
            var sb = new StringBuilder(a.Length + 4);
            sb.Append('"');
            foreach (var c in a)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
