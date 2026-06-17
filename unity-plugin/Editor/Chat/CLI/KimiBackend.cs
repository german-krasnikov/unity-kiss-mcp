// Kimi CLI backend — spawn-per-turn (headless -p mode), stream-json protocol.
// No session resume: kimi -p mode does not emit a session_id in stream-json output.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal sealed class KimiBackend : CliBackendBase
    {
        private readonly string _model;
        private readonly string _approvalMode;
        private readonly string _extraArgs;

        internal KimiBackend(string model = null, string approvalMode = null,
            string extraArgs = null, string resumeSessionId = null)
        {
            _model        = model;
            _approvalMode = approvalMode;
            _extraArgs    = extraArgs;
            // Note: kimi -p mode doesn't emit session_id — resumeSessionId unused.
        }

        protected override string BinaryName           => "kimi";
        protected override bool   IsPersistentProcess  => false;

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(
            string binaryPath, string resumeId)
        {
            var prompt = ExtractPlainText(PendingPrompt ?? "");
            return KimiArgBuilder.Build(prompt, _model, _approvalMode, _extraArgs);
        }

        protected override void ParseLine(string line, List<ChatEvent> sink)
            => KimiParser.ParseLine(line, sink);
    }
}
