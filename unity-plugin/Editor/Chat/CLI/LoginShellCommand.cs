// Pure helper — no UnityEngine deps, fully NUnit-testable.
// Builds safe zsh -lic argv: binary/path passed as positional arg, never interpolated into script body.
using System.Diagnostics;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    public static class LoginShellCommand
    {
        /// <summary>
        /// Wraps s in single-quotes with POSIX-correct escaping of embedded single-quotes.
        /// Each ' becomes '\'' (close-quote, literal-quote, open-quote).
        /// </summary>
        public static string ShellQuoteSingle(string s) =>
            "'" + s.Replace("'", "'\\''") + "'";

        /// <summary>
        /// Produces the single-string Arguments value for ProcessStartInfo (Unity Mono compatible).
        /// Both script and arg are single-quoted so the OS re-parse cannot split or interpret them.
        /// </summary>
        public static string BuildArguments(string script, string arg) =>
            $"-lic {ShellQuoteSingle(script)} zsh {ShellQuoteSingle(arg)}";

        /// <summary>
        /// Factory: creates a ready-to-use ProcessStartInfo for /bin/zsh login-shell invocation.
        /// Always uses /bin/zsh regardless of $SHELL; bash users must set Override Path in settings.
        /// </summary>
        public static ProcessStartInfo Create(string script, string arg) =>
            new ProcessStartInfo("/bin/zsh", BuildArguments(script, arg))
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                CreateNoWindow         = true,
                StandardOutputEncoding = new UTF8Encoding(false),  // cp1251 safety
            };
    }
}
