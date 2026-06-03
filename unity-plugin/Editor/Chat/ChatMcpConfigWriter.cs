// Generates the --mcp-config JSON for the Claude CLI, deriving the server path
// from the UPM package location. Eliminates the hardcoded ~/.claude/mcp.json path.
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public static class ChatMcpConfigWriter
    {
        private const string PackageId  = "com.unity-mcp.editor";
        private const string ConfigFile = "unity-mcp-config.json";

        // ── Pure helpers ──────────────────────────────────────────────────────

        /// <summary>Returns <c>packageRoot/../server</c> normalized. No I/O.</summary>
        public static string DeriveServerPath(string packageRoot) =>
            Path.GetFullPath(Path.Combine(packageRoot.TrimEnd('/', '\\'), "..", "server"));

        /// <summary>Formats the MCP config JSON blob expected by --mcp-config.</summary>
        public static string BuildClaudeConfigJson(string command, string[] args)
        {
            var sb = new StringBuilder();
            sb.Append("{\"mcpServers\":{\"unity\":{\"command\":\"");
            sb.Append(JsonHelper.EscapeJson(command));
            sb.Append("\",\"args\":[");
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(JsonHelper.EscapeJson(args[i]));
                sb.Append('"');
            }
            sb.Append("]}}}");
            return sb.ToString();
        }

        // ── I/O helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the absolute server directory if it contains a pyproject.toml, else null.
        /// </summary>
        public static string ResolveServerDir(string packageRoot)
        {
            var dir = DeriveServerPath(packageRoot);
            return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "pyproject.toml"))
                ? dir
                : null;
        }

        /// <summary>
        /// Resolves the Python command and args for the server.
        /// Resolution order: venv python → uv (resolvedUvPath) → python3.
        /// <para>Pass a pre-resolved absolute uv path (or null) to keep this unit-testable
        /// without depending on the host having uv in PATH.</para>
        /// </summary>
        public static (string command, string[] args) ResolvePythonCommand(string serverDir, string resolvedUvPath)
        {
            var venvPython = Path.Combine(serverDir, ".venv", "bin", "python");
            if (File.Exists(venvPython))
                return (venvPython, new[] { "-m", "unity_mcp.server" });

            if (!string.IsNullOrEmpty(resolvedUvPath))
                return (resolvedUvPath, new[] { "run", "--directory", serverDir, "unity-mcp" });

            return ("python3", new[] { "-m", "unity_mcp.server" });
        }

        /// <summary>
        /// Resolves uv's absolute path via the login shell (handles macOS GUI truncated PATH).
        /// Returns null if uv is not found.
        /// </summary>
        private static string ResolveUvPath()
        {
            try
            {
                var psi = LoginShellCommand.Create("command -v \"$1\"", "uv");
                using var p = System.Diagnostics.Process.Start(psi);
                var result = p?.StandardOutput.ReadToEnd().Trim();
                if (p != null && !p.WaitForExit(3000)) { try { p.Kill(); } catch { } }
                return string.IsNullOrEmpty(result) ? null : result;
            }
            catch { return null; }
        }

        /// <summary>
        /// Resolves everything, writes the config to a stable temp file, and returns its path.
        /// Returns null (+ LogError) if the server directory cannot be found.
        /// </summary>
        public static string GetOrCreateConfigPath()
        {
            var packageRoot = Path.GetFullPath($"Packages/{PackageId}");
            var serverDir   = ResolveServerDir(packageRoot);
            if (serverDir == null)
            {
                Debug.LogError($"[MCP Chat] Server not found at expected path (package={packageRoot}). " +
                               "Check the UPM package is installed correctly.");
                return null;
            }

            var uvPath        = ResolveUvPath();
            var (cmd, args)   = ResolvePythonCommand(serverDir, uvPath);
            var json          = BuildClaudeConfigJson(cmd, args);
            var configPath    = Path.Combine(Path.GetTempPath(), ConfigFile);
            File.WriteAllText(configPath, json, Encoding.UTF8);
            return configPath;
        }
    }
}
