// Resolves absolute paths to CLI binaries (claude, codex, uv, …).
// Platform dispatch: where.exe (Windows), bash -lic (Linux), /bin/zsh -lc (macOS).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatBinaryResolver
    {
        internal const string PrefKey       = "UnityMCP_Chat_ClaudePath";
        internal const string CodexPrefKey  = "UnityMCP_Chat_Path_codex";
        internal const string GeminiPrefKey = "UnityMCP_Chat_Path_gemini";

        // Per-binary negative cache: key = binary name, value = resolved path (null = not found).
        // Populated on first probe per binary; cleared by ResetCacheForTests.
        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

#if UNITY_INCLUDE_TESTS
        // Seam: inject in tests instead of spawning a shell (mirrors FindObjectOverride pattern)
        internal static Func<string, string> WhichOverride;
        internal static void ResetCacheForTests() { _cache.Clear(); }
#endif

        /// <summary>
        /// Returns the absolute path to the claude binary, or null if not found.
        /// Checks EditorPrefs override first, then PATH-resolves via login shell.
        /// Negative results are cached — use forceRefresh:true to bust the cache.
        /// </summary>
        internal static string Resolve(bool forceRefresh = false)
        {
            var pref = EditorPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(pref)) return pref;

            if (!forceRefresh && _cache.ContainsKey("claude")) return _cache["claude"];

            var result = WhichViaSh("claude");
            _cache["claude"] = result;
            return result;
        }

        /// <summary>
        /// Resolve an arbitrary CLI binary by name via login shell.
        /// For "claude" delegates to <see cref="Resolve()"/> to honour EditorPrefs + cache.
        /// For other binaries, checks a per-binary EditorPrefs key first, then caches result.
        /// Negative results are cached per binary name to avoid repeated shell probes.
        /// </summary>
        internal static string Resolve(string binaryName)
        {
            if (binaryName == "claude") return Resolve();

            // Per-backend EditorPrefs escape-hatch (R1: codex, etc.)
            var overrideKey  = $"UnityMCP_Chat_Path_{binaryName}";
            var overridePref = EditorPrefs.GetString(overrideKey, "");
            if (!string.IsNullOrEmpty(overridePref)) return overridePref;

            // Per-binary negative cache: probe once, then reuse result.
            if (_cache.TryGetValue(binaryName, out var cached)) return cached;

            var result = WhichViaSh(binaryName);
            _cache[binaryName] = result;
            return result;
        }

        private static string WhichViaSh(string binary)
        {
#if UNITY_INCLUDE_TESTS
            if (WhichOverride != null) return WhichOverride(binary);
#endif
            try
            {
                ProcessStartInfo psi;
                switch (SystemInfo.operatingSystemFamily)
                {
                    case OperatingSystemFamily.Windows:
                        psi = new ProcessStartInfo("where.exe", binary)
                        {
                            UseShellExecute        = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow         = true,
                            StandardOutputEncoding = new UTF8Encoding(false),  // cp1251 safety: Cyrillic in %PATH%
                        };
                        // CWD-hijack mitigation (MITRE T1574.008)
                        psi.EnvironmentVariables["NoDefaultCurrentDirectoryInExePath"] = "1";
                        break;

                    case OperatingSystemFamily.Linux:
                        // -lic: login+interactive so ~/.bashrc (nvm/pyenv/mise) is sourced.
                        // bash -lc skips .bashrc due to non-interactive guard.
                        var shell     = File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
                        var shellName = Path.GetFileName(shell);
                        var bashArgs  = $"-lic 'command -v \"$1\"' {shellName} {LoginShellCommand.ShellQuoteSingle(binary)}";
                        psi = new ProcessStartInfo(shell, bashArgs)
                        {
                            UseShellExecute        = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true,  // suppress "no job control" warning
                            CreateNoWindow         = true,
                            StandardOutputEncoding = new UTF8Encoding(false),  // cp1251 safety
                            StandardErrorEncoding  = new UTF8Encoding(false),
                        };
                        break;

                    default: // macOS
                        psi = LoginShellCommand.Create("command -v \"$1\"", binary);
                        break;
                }

                using var p = Process.Start(psi);
                if (p == null) return null;

                string result;
                if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows)
                {
                    result = PickWindowsPath(p.StandardOutput.ReadToEnd());
                }
                else if (SystemInfo.operatingSystemFamily == OperatingSystemFamily.Linux)
                {
                    var allOutput = p.StandardOutput.ReadToEnd();
                    try { p.StandardError.ReadToEnd(); } catch { } // drain stderr (suppress "no job control")
                    result = PickLinuxPath(allOutput);
                }
                else
                {
                    // macOS: interior newline after Trim() == banner contamination → reject.
                    result = RejectIfMultiline(p.StandardOutput.ReadToEnd().Trim());
                }

                if (!p.WaitForExit(3000)) { try { p.Kill(); } catch { } }
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch
            {
                return null;
            }
        }

        // ── Output-parsing helpers (internal for unit tests) ─────────────────

        /// <summary>
        /// From multi-line where.exe output, return first .exe line (preferred),
        /// then first .cmd line, else null. Extensionless npm bash-shim lines are rejected.
        /// </summary>
        internal static string PickWindowsPath(string whereOutput)
        {
            string exeLine = null, cmdLine = null;
            foreach (var raw in whereOutput.Split('\n'))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (exeLine == null && line.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    exeLine = line;
                if (cmdLine == null && line.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
                    cmdLine = line;
            }
            return exeLine ?? cmdLine;
        }

        /// <summary>
        /// From bash -lic output, return the last line starting with '/' (the real path).
        /// Interactive .bashrc banners precede the path and don't start with '/'.
        /// </summary>
        internal static string PickLinuxPath(string stdout)
        {
            var lines = stdout.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.Length > 0 && line[0] == '/') return line;
            }
            return null;
        }

        /// <summary>
        /// Returns null if trimmedOutput contains an interior '\n' (banner contamination).
        /// Otherwise returns the value unchanged.
        /// Interior newline after Trim() means multiple output lines were received — reject
        /// to avoid treating a banner line as a valid path.
        /// </summary>
        internal static string RejectIfMultiline(string trimmedOutput)
            => (trimmedOutput != null && trimmedOutput.Contains("\n")) ? null : trimmedOutput;
    }
}
