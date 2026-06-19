// Antigravity (agy) CLI backend — spawn-per-turn, plain text output.
// No NDJSON: each stdout line is a text delta; TurnDone emitted on process exit.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AntigravityBackend : CliBackendBase
    {
        private readonly string _model;
        private readonly string _approvalMode;
        private readonly bool   _sandbox;
        private readonly string _extraArgs;

        // Sentinel line injected into drain buffer when the process finishes cleanly.
        // ParseLine recognises it and emits TurnDone without emitting it as text.
        internal const string EofSentinel = "\x00AGY_EOF";

        internal AntigravityBackend(string model = null, string approvalMode = null,
            bool sandbox = false, string extraArgs = null, string resumeSessionId = null)
        {
            _model        = model;
            _approvalMode = approvalMode;
            _sandbox      = sandbox;
            _extraArgs    = extraArgs;
            SessionId     = resumeSessionId;
            // agy --conversation resume not yet wired (no session_id in stdout)
        }

        protected override string BinaryName           => "agy";
        protected override bool   IsPersistentProcess  => false;
        protected override bool   SendInitializeHandshake => false;

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(
            string binaryPath, string resumeId)
        {
            var prompt = ExtractPlainText(PendingPrompt ?? "");
            return AgyArgBuilder.Build(prompt, _model, _approvalMode, _sandbox, _extraArgs);
        }

        protected override void ParseLine(string line, List<ChatEvent> sink)
            => AgyParser.ParseLine(line, sink);

        /// <summary>
        /// After draining stdout lines, inject EOF sentinel if the process just finished.
        /// The base DrainEvents loop calls ParseLine on every line including the sentinel.
        /// </summary>
        protected override void DrainRawLines(List<string> buf)
        {
            var wasRunning = _proc?.IsRunning ?? false;
            base.DrainRawLines(buf);
            var nowDone = wasRunning && (_proc == null || !_proc.IsRunning);
            if (nowDone) buf.Add(EofSentinel);
        }
    }
}
