// IBackendProvider: one file per CLI backend, zero core changes.
// Discovered via TypeCache at runtime; registered manually in tests.
namespace UnityMCP.Editor.Chat
{
    /// <summary>Arguments passed to IBackendProvider.Create().</summary>
    internal readonly struct BackendCreateArgs
    {
        public readonly string McpConfigPath;
        public readonly bool   AgentMode;      // true = acceptEdits, false = plan
        public readonly string AgentName;      // null = default
        public readonly PermissionConfig PermConfig;
        public readonly string ResumeSessionId;
        public readonly BackendConfigStore Store;

        public BackendCreateArgs(string mcpConfigPath, bool agentMode,
            string agentName, PermissionConfig permConfig,
            string resumeSessionId, BackendConfigStore store)
        {
            McpConfigPath   = mcpConfigPath;
            AgentMode       = agentMode;
            AgentName       = agentName;
            PermConfig      = permConfig;
            ResumeSessionId = resumeSessionId;
            Store           = store;
        }
    }

    /// <summary>
    /// One implementation per CLI tool. TypeCache discovers all concrete types
    /// automatically — adding a new backend = 1 file, 0 core changes.
    /// </summary>
    internal interface IBackendProvider
    {
        /// <summary>Stable id used for persistence mapping ("claude", "codex").</summary>
        string ProviderId { get; }

        /// <summary>Binary name passed to ChatBinaryResolver.</summary>
        string BinaryName { get; }

        /// <summary>Label shown in the dropdown.</summary>
        string DisplayName { get; }

        /// <summary>Lower = earlier in dropdown.</summary>
        int SortOrder { get; }

        /// <summary>Create the backend for a new or resumed session.</summary>
        IChatBackend Create(BackendCreateArgs args);
    }
}
