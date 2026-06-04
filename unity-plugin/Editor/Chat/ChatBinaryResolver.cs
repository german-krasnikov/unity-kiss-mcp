// Resolves the absolute path to the claude CLI binary.
// Handles Unity-from-Finder truncated PATH via /bin/zsh -lc 'command -v claude'.
using System;
using System.Diagnostics;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatBinaryResolver
    {
        internal const string PrefKey = "UnityMCP_Chat_ClaudePath";
        private static string _cached;
        private static bool   _probed;

#if UNITY_INCLUDE_TESTS
        // Seam: inject in tests instead of spawning /bin/zsh (mirrors FindObjectOverride pattern)
        internal static Func<string, string> WhichOverride;
        internal static void ResetCacheForTests() { _cached = null; _probed = false; }
#endif

        /// <summary>
        /// Returns the absolute path to the claude binary, or null if not found.
        /// Checks EditorPrefs override first, then PATH-resolves via login shell.
        /// Negative results are cached — use forceRefresh:true to bust the cache.
        /// </summary>
        internal static string Resolve(bool forceRefresh = false)
        {
            // EditorPrefs override always wins (bypasses cache entirely)
            var pref = EditorPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(pref)) return pref;

            if (!forceRefresh && _probed) return _cached;

            _cached = WhichViaSh("claude");
            _probed = true;
            return _cached;
        }

        private static string WhichViaSh(string binary)
        {
#if UNITY_INCLUDE_TESTS
            if (WhichOverride != null) return WhichOverride(binary);
#endif
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
