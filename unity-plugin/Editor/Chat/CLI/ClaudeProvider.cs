// IBackendProvider for claude CLI.
using System.IO;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ClaudeProvider : IBackendProvider
    {
        public string ProviderId  => "claude";
        public string BinaryName  => "claude";
        public string DisplayName => "Claude";
        public int    SortOrder   => 0;

        public IChatBackend Create(BackendCreateArgs a)
        {
            var cfg = a.McpConfigPath
                ?? Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    ".claude", "mcp.json");
            var mode = a.AgentMode ? "acceptEdits" : "plan";
            return new ClaudeBackend(cfg, mode, a.AgentName, a.PermConfig,
                a.ResumeSessionId,
                a.Store?.Claude.Model, a.Store?.Claude.ExtraArgs);
        }
    }
}
