// Claude strategy over CliBackendBase — persistent stdin loop, stream-json protocol.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ClaudeBackend : CliBackendBase
    {
        private readonly string           _mcpConfigPath;
        private readonly string           _permissionMode; // "plan" | "acceptEdits"
        private readonly string           _agentName;      // null = default Claude
        private readonly PermissionConfig _permConfig;     // null → blanket allow-all

        // Snapshot captured once at construction (fresh session only — SessionId == null).
        private readonly string _snapshot;

        internal ClaudeBackend(string mcpConfigPath, string permissionMode,
            string agentName = null, PermissionConfig permConfig = null,
            string resumeSessionId = null)
        {
            _mcpConfigPath  = mcpConfigPath;
            _permissionMode = permissionMode;
            _agentName      = agentName;
            _permConfig     = permConfig;
            _snapshot       = resumeSessionId == null ? EditorStateSnapshot.Capture() : null;
            SessionId       = resumeSessionId;
        }

        protected override string BinaryName         => "claude";
        protected override bool   IsPersistentProcess => true;

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(string binaryPath, string resumeId)
            => ClaudeArgBuilder.Build(binaryPath, _mcpConfigPath, _permissionMode,
                   resumeId, _agentName, _permConfig?.GetAllowedToolIds(), _snapshot);

        protected override void ParseLine(string line, List<ChatEvent> sink)
            => ChatStreamParser.ParseInto(line, sink);
    }
}
