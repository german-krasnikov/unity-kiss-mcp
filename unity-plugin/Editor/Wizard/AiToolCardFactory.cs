using System;
using System.IO;

namespace UnityMCP.Editor.Wizard
{
    internal enum CardAction { CopyText, WriteConfig, CopyPort }

    internal readonly struct BackendCard
    {
        internal readonly string Name;
        internal readonly string Body;
        internal readonly string BtnLabel;
        internal readonly CardAction Action;
        internal readonly string Payload;

        internal BackendCard(string name, string body, string btnLabel, CardAction action, string payload)
        {
            Name = name; Body = body; BtnLabel = btnLabel; Action = action; Payload = payload;
        }
    }

    internal static class AiToolCardFactory
    {
        internal static BackendCard[] Build(int port)
        {
            var snippet = $"claude mcp add unity -- env UNITY_MCP_PORT={port} uvx unity-mcp";
            return new[]
            {
                // ── Group A: External MCP Hosts ───────────────────────────────
                new BackendCard("Claude Code",
                    snippet,
                    "Copy",
                    CardAction.CopyText,
                    snippet),

                new BackendCard("Claude Desktop",
                    "Write mcpServers entry to claude_desktop_config.json",
                    "Write Config",
                    CardAction.WriteConfig,
                    ClaudeDesktopPath()),

                new BackendCard("Cursor",
                    "Write mcpServers entry to ~/.cursor/mcp.json",
                    "Write Config",
                    CardAction.WriteConfig,
                    CursorPath()),

                new BackendCard("Windsurf",
                    "Write mcpServers entry to mcp_config.json",
                    "Write Config",
                    CardAction.WriteConfig,
                    WindsurfPath()),

                // ── Group B: In-Unity Chat Backends ───────────────────────────
                new BackendCard("Gemini",
                    $"MCP auto-configured at chat start. Port: {port}",
                    "Copy Port",
                    CardAction.CopyPort,
                    port.ToString()),

                new BackendCard("Kimi K2",
                    $"MCP auto-configured at chat start. Port: {port}",
                    "Copy Port",
                    CardAction.CopyPort,
                    port.ToString()),

                new BackendCard("Codex",
                    $"MCP wired via CLI flags at spawn. Port: {port}",
                    "Copy Port",
                    CardAction.CopyPort,
                    port.ToString()),

                new BackendCard("OpenCode",
                    $"MCP config injected via env var. Port: {port}",
                    "Copy Port",
                    CardAction.CopyPort,
                    port.ToString()),
            };
        }

        // ── Platform-aware config paths (mirrors Python clients.py) ──────────

        internal static string ClaudeDesktopPath()
        {
#if UNITY_EDITOR_WIN
            var appdata = Environment.GetEnvironmentVariable("APPDATA")
                ?? Path.Combine(Home(), "AppData", "Roaming");
            return Path.Combine(appdata, "Claude", "claude_desktop_config.json");
#elif UNITY_EDITOR_OSX
            return Path.Combine(Home(), "Library", "Application Support", "Claude", "claude_desktop_config.json");
#else
            return Path.Combine(Home(), ".config", "Claude", "claude_desktop_config.json");
#endif
        }

        internal static string CursorPath()
            => Path.Combine(Home(), ".cursor", "mcp.json");

        internal static string WindsurfPath()
        {
#if UNITY_EDITOR_WIN
            var appdata = Environment.GetEnvironmentVariable("APPDATA")
                ?? Path.Combine(Home(), "AppData", "Roaming");
            return Path.Combine(appdata, "Codeium", "windsurf", "mcp_config.json");
#else
            return Path.Combine(Home(), ".codeium", "windsurf", "mcp_config.json");
#endif
        }

        private static string Home()
            => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
