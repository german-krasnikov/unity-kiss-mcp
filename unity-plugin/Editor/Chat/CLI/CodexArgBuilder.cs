// Build codex exec argv. Pure static, no I/O.
// First-turn: exec --json -C <cwd> -s danger-full-access --skip-git-repo-check + -c flags + prompt
// Resume-turn: exec resume <id> --json --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check + -c flags + prompt
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public static class CodexArgBuilder
    {
        private static readonly string[] StripKeys = { "OPENAI_API_KEY" };

        /// <summary>
        /// Build codex exec argv.
        /// resumeSessionId non-null → exec resume subcommand.
        /// pythonCommand + pythonArgs from ChatMcpConfigWriter.ResolvePythonCommand.
        /// </summary>
        public static (string[] args, string[] stripEnvKeys) Build(
            string prompt,
            string resumeSessionId,
            string pythonCommand,
            string[] pythonArgs,
            int startupTimeoutSec = 30,
            string extraArgs = null)
        {
            var args = new List<string> { "exec" };

            if (resumeSessionId != null)
            {
                // Resume: exec resume <id> --json --dangerously-bypass-approvals-and-sandbox ...
                args.Add("resume");
                args.Add(resumeSessionId);
                args.Add("--json");
                args.Add("--dangerously-bypass-approvals-and-sandbox");
            }
            else
            {
                // First turn: exec --json -C <cwd> -s danger-full-access ...
                args.Add("--json");
                args.Add("-C");
                args.Add(ProjectRoot());
                args.Add("-s");
                args.Add("danger-full-access");
            }

            args.Add("--skip-git-repo-check");

            // MCP server wiring via TOML -c flags (3 required)
            args.Add("-c");
            args.Add($"mcp_servers.unity.command=\"{TomlEscapeString(pythonCommand ?? PythonFallback)}\"");
            args.Add("-c");
            args.Add($"mcp_servers.unity.args=[{BuildTomlStringArray(pythonArgs)}]");
            args.Add("-c");
            args.Add($"mcp_servers.unity.startup_timeout_sec={startupTimeoutSec}");

            // ArgTokenizer handles quoted spans: --flag "multi word" stays 2 tokens.
            if (!string.IsNullOrEmpty(extraArgs))
                foreach (var token in ArgTokenizer.Split(extraArgs))
                    args.Add(token);

            // Prompt is always the last positional argument
            if (!string.IsNullOrEmpty(prompt))
                args.Add(prompt);

            return (args.ToArray(), StripKeys);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Platform-aware Python executable name: "python" on Windows, "python3" elsewhere.</summary>
        internal static string PythonFallback
            => SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows ? "python" : "python3";

        private static string ProjectRoot()
        {
#if UNITY_EDITOR
            return Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? ".";
#else
            return Directory.GetCurrentDirectory();
#endif
        }

        /// <summary>Double-quote a string for TOML (escape backslash and double-quote).</summary>
        internal static string TomlEscapeString(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        /// <summary>Build a TOML inline array of strings: "-m","unity_mcp.server"</summary>
        internal static string BuildTomlStringArray(string[] items)
        {
            if (items == null || items.Length == 0) return "";
            var parts = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
                parts[i] = $"\"{TomlEscapeString(items[i])}\"";
            return string.Join(",", parts);
        }
    }
}
