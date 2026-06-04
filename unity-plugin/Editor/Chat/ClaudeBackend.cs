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
        private readonly string           _model;          // empty = use backend default
        private readonly string           _extraArgs;      // whitespace-split appended to argv

        // Snapshot captured once at construction (fresh session only — SessionId == null).
        private readonly string _snapshot;

        internal ClaudeBackend(string mcpConfigPath, string permissionMode,
            string agentName = null, PermissionConfig permConfig = null,
            string resumeSessionId = null,
            string model = null, string extraArgs = null)
        {
            _mcpConfigPath  = mcpConfigPath;
            _permissionMode = permissionMode;
            _agentName      = agentName;
            _permConfig     = permConfig;
            _model          = model;
            _extraArgs      = extraArgs;
            _snapshot       = resumeSessionId == null ? EditorStateSnapshot.Capture() : null;
            SessionId       = resumeSessionId;
        }

        protected override string BinaryName         => "claude";
        protected override bool   IsPersistentProcess => true;

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(string binaryPath, string resumeId)
            => ClaudeArgBuilder.Build(binaryPath, _mcpConfigPath, _permissionMode,
                   resumeId, _agentName, _permConfig?.GetAllowedToolIds(), _snapshot,
                   _model, _extraArgs);

        protected override void ParseLine(string line, List<ChatEvent> sink)
            => ChatStreamParser.ParseInto(line, sink);
    }
}
