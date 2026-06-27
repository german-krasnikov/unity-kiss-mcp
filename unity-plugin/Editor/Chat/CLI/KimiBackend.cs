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
        private bool _pendingResumeNotice;

        internal KimiBackend(string model = null, string approvalMode = null,
            string extraArgs = null, string resumeSessionId = null)
        {
            _model        = model;
            _approvalMode = approvalMode;
            _extraArgs    = extraArgs;
            // Kimi doesn't support --resume. Show a notice on first response, clear stale key.
            if (!string.IsNullOrEmpty(resumeSessionId))
            {
                _pendingResumeNotice = true;
                UnityEditor.SessionState.EraseString("MCPChat_BackendSessionId");
            }
            SessionId = null; // clear: Kimi can't resume, so don't persist any id
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
        {
            if (_pendingResumeNotice)
            {
                sink.Add(ChatEvent.Error("[Session restarted — Kimi does not support resume after domain reload]"));
                _pendingResumeNotice = false;
            }
            KimiParser.ParseLine(line, sink);
        }
    }
}
