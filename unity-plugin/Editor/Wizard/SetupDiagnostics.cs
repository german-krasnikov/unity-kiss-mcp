using System.IO;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>Pure static diagnostics — no Unity I/O, fully unit-testable.</summary>
    public static class SetupDiagnostics
    {
        // ── Python ────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether a usable Python executable exists for the given server dir.
        /// Resolution order: .venv/Scripts/python.exe (Windows) → .venv/bin/python → python3/python fallback.
        /// </summary>
        public static (bool ok, string detail) CheckPython(string serverDir)
        {
            if (string.IsNullOrEmpty(serverDir) || !Directory.Exists(serverDir))
                return (false, "Server directory not found");

            var cmd = ResolvePythonCmd(serverDir);
            var isFallback = cmd == "python3" || cmd == "python";
            if (isFallback)
                return (true, $"Python via {cmd} (system fallback)");

            if (File.Exists(cmd))
                return (true, $"Python at {cmd}");

            return (false, $"Python not found (tried {cmd})");
        }

        private static string ResolvePythonCmd(string serverDir)
        {
            var win = Path.Combine(serverDir, ".venv", "Scripts", "python.exe");
            if (File.Exists(win)) return win;

            var venv = Path.Combine(serverDir, ".venv", "bin", "python");
            if (File.Exists(venv)) return venv;

            return UnityEngine.SystemInfo.operatingSystemFamily
                == UnityEngine.OperatingSystemFamily.Windows ? "python" : "python3";
        }

        // ── Server ────────────────────────────────────────────────────────────

        /// <summary>Returns running state of the MCP TCP server.</summary>
        public static (bool ok, string detail) CheckServer()
        {
            if (!MCPServer.IsRunning)
                return (false, "MCP server not running");

            return (true, $"Server on :{MCPServer.ServerPort}");
        }

        // ── Snippet ───────────────────────────────────────────────────────────

        /// <summary>Builds the <c>claude mcp add</c> command for the given port.</summary>
        public static string BuildClaudeCodeSnippet(int port)
        {
            return $"claude mcp add unity -- env UNITY_MCP_PORT={port} python3 -m unity_mcp.server";
        }
    }
}
