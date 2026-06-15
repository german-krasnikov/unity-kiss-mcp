// Pure arg-quoting for Mono direct-exec (not shell). Platform-split:
//   QuotePosix — macOS/Linux fork+exec (backslash/double-quote escaped)
//   QuoteWindows — Windows MSVC CRT CommandLineToArgvW rules
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ArgQuoting
    {
        internal static string Quote(string a)
        {
            if (a == null) a = "";
            if (a.Length > 0 && !NeedsQuote(a)) return a;
            return SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows
                ? QuoteWindows(a)
                : QuotePosix(a);
        }

        private static bool NeedsQuote(string a)
        {
            foreach (var c in a)
                if (c == ' ' || c == '\t' || c == '\n' || c == '"' || c == '\\' || c == '$' || c == '`')
                    return true;
            return false;
        }

        /// <summary>
        /// POSIX quoting for Mono fork+exec on macOS/Linux.
        /// Backslash and double-quote are escaped; $ and ` wrapped defensively.
        /// </summary>
        internal static string QuotePosix(string a)
        {
            if (a == null) a = "";
            if (a.Length > 0 && !NeedsQuote(a)) return a;
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

        /// <summary>
        /// Windows MSVC CRT quoting (CommandLineToArgvW rules):
        /// - Backslashes are literal UNLESS immediately preceding a double-quote
        /// - N backslashes + " → 2N backslashes + escaped "
        /// - N trailing backslashes before closing " → 2N backslashes
        /// </summary>
        internal static string QuoteWindows(string a)
        {
            if (a == null) a = "";
            var sb = new StringBuilder(a.Length + 4);
            sb.Append('"');
            for (int i = 0; i < a.Length; )
            {
                int nbs = 0;
                while (i < a.Length && a[i] == '\\') { nbs++; i++; }

                if (i == a.Length)
                {
                    // Trailing backslashes: must be doubled (they precede closing ")
                    sb.Append('\\', nbs * 2);
                    break;
                }
                if (a[i] == '"')
                {
                    // Backslashes before ": 2N+1 (double them + one to escape the ")
                    sb.Append('\\', nbs * 2 + 1);
                    sb.Append('"');
                    i++;
                }
                else
                {
                    // Backslashes before normal char: literal
                    sb.Append('\\', nbs);
                    sb.Append(a[i]);
                    i++;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
