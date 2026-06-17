// Gemini CLI backend — spawn-per-turn (headless -p mode), stream-json protocol.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal sealed class GeminiBackend : CliBackendBase
    {
        private readonly string _model;
        private readonly string _approvalMode;
        private readonly bool   _sandbox;
        private readonly string _extraArgs;

        internal GeminiBackend(string model = null, string approvalMode = null,
            bool sandbox = false, string extraArgs = null, string resumeSessionId = null)
        {
            _model        = model;
            _approvalMode = approvalMode;
            _sandbox      = sandbox;
            _extraArgs    = extraArgs;
            SessionId     = resumeSessionId;
        }

        protected override string BinaryName           => "gemini";
        protected override bool   IsPersistentProcess  => false;
        protected override bool   SendInitializeHandshake => false;

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(
            string binaryPath, string resumeId)
        {
            // PendingPrompt is the Claude SDK JSON envelope {"type":"user","message":...}.
            // Gemini CLI expects plain text via -p, so extract the text content.
            var prompt = ExtractPlainText(PendingPrompt ?? "");
            return GeminiArgBuilder.Build(prompt, _model, _approvalMode,
                _sandbox, _extraArgs);
        }

        protected override void ParseLine(string line, List<ChatEvent> sink)
            => GeminiParser.ParseLine(line, sink);
    }
}
