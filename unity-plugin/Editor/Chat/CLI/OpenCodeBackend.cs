// OpenCode CLI backend — spawn-per-turn, NDJSON type-based protocol.
// MCP injected via OPENCODE_CONFIG env var pointing to temp JSON.
using System.Collections.Generic;
using System.IO;

namespace UnityMCP.Editor.Chat
{
    internal sealed class OpenCodeBackend : CliBackendBase
    {
        private readonly string _model;
        private readonly bool   _skipPermissions;
        private readonly string _extraArgs;

        internal OpenCodeBackend(string model = null, bool skipPermissions = true,
            string extraArgs = null, string resumeSessionId = null)
        {
            _model           = model;
            _skipPermissions = skipPermissions;
            _extraArgs       = extraArgs;
            SessionId        = resumeSessionId;
        }

        protected override string BinaryName            => "opencode";
        protected override bool   IsPersistentProcess   => false;
        protected override bool   SendInitializeHandshake => false;

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(
            string binaryPath, string resumeId)
        {
            var prompt = ExtractPlainText(PendingPrompt ?? "");
            return OpenCodeArgBuilder.Build(
                prompt, _model, _skipPermissions, _extraArgs,
                Path.GetTempPath(), MCPServer.ServerChatPort, resumeId);
        }

        protected override void SpawnNewProcess(string binary, string[] args, string[] strip)
        {
            var port = MCPServer.ServerChatPort;
            OpenCodeArgBuilder.WriteConfig(Path.GetTempPath(), port);
            var env = OpenCodeArgBuilder.BuildEnv(Path.GetTempPath(), port);
            _proc = new ChatProcess();
            _proc.Spawn(binary, args, strip, env);
            CloseStdinOnProc();
        }

        protected override void ParseLine(string line, List<ChatEvent> sink)
            => OpenCodeParser.ParseLine(line, sink);
    }
}
