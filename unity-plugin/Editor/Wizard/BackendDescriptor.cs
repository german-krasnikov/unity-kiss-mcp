namespace UnityMCP.Editor.Wizard
{
    public enum InstallMechanism { PythonConfig, CliCommand, ChatAuto }

    public sealed class BackendDescriptor
    {
        public string Key;
        public string DisplayName;
        public string Icon;
        public string Description;
        public InstallMechanism Mechanism;
        public string BinaryName; // for PATH check via which/where
        public string ConfigDir;  // for dir existence check (~ expanded at runtime)

        public static readonly BackendDescriptor[] All = new[]
        {
            new BackendDescriptor
            {
                Key = "claude-code", DisplayName = "Claude Code", Icon = "◆",
                Description = "Anthropic's CLI — writes ~/.claude.json",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "claude", ConfigDir = "~/.claude"
            },
            new BackendDescriptor
            {
                Key = "claude-desktop", DisplayName = "Claude Desktop", Icon = "◆",
                Description = "Desktop app — writes mcpServers config",
                Mechanism = InstallMechanism.PythonConfig,
                ConfigDir = "~/Library/Application Support/Claude"
            },
            new BackendDescriptor
            {
                Key = "cursor", DisplayName = "Cursor", Icon = "▶",
                Description = "AI-first editor — writes ~/.cursor/mcp.json",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "cursor", ConfigDir = "~/.cursor"
            },
            new BackendDescriptor
            {
                Key = "windsurf", DisplayName = "Windsurf", Icon = "◈",
                Description = "Codeium's editor — writes mcp_config.json",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "windsurf", ConfigDir = "~/.codeium"
            },
            new BackendDescriptor
            {
                Key = "vscode", DisplayName = "VS Code", Icon = "◧",
                Description = "Visual Studio Code with Copilot MCP",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "code"
            },
            new BackendDescriptor
            {
                Key = "codex", DisplayName = "Codex", Icon = "◉",
                Description = "OpenAI Codex CLI — writes MCP config file",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "codex"
            },
            new BackendDescriptor
            {
                Key = "kimi", DisplayName = "Kimi K2", Icon = "◎",
                Description = "Moonshot AI — MCP auto-configured at chat start",
                Mechanism = InstallMechanism.ChatAuto
            },
            new BackendDescriptor
            {
                Key = "opencode", DisplayName = "OpenCode", Icon = "◌",
                Description = "Open-source CLI — writes MCP config file",
                Mechanism = InstallMechanism.PythonConfig,
                BinaryName = "opencode"
            },
            new BackendDescriptor
            {
                Key = "antigravity", DisplayName = "Antigravity", Icon = "◑",
                Description = "In-Unity chat backend — auto-configured",
                Mechanism = InstallMechanism.ChatAuto
            },
        };
    }
}
