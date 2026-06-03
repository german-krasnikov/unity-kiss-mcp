// Resolves the absolute path to the claude CLI binary.
// Handles Unity-from-Finder truncated PATH via /bin/zsh -lc 'command -v claude'.
using System.Diagnostics;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatBinaryResolver
    {
        private const string PrefKey = "UnityMCP_Chat_ClaudePath";
        private static string _cached;

        /// <summary>
        /// Returns the absolute path to the claude binary, or null if not found.
        /// Checks EditorPrefs override first, then PATH-resolves via login shell.
        /// </summary>
        internal static string Resolve(bool forceRefresh = false)
        {
            // EditorPrefs override always wins
            var pref = EditorPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(pref)) return pref;

            if (!forceRefresh && _cached != null) return _cached;

            _cached = WhichViaSh("claude");
            return _cached;
        }

        private static string WhichViaSh(string binary)
        {
            try
            {
                var psi = LoginShellCommand.Create("command -v \"$1\"", binary);
                using var p = Process.Start(psi);
                var result = p?.StandardOutput.ReadToEnd().Trim();
                if (p != null && !p.WaitForExit(3000)) { try { p.Kill(); } catch { } }
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch
            {
                return null;
            }
        }
    }
}
