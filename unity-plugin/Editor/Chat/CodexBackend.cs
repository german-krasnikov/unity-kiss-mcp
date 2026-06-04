// Codex strategy over CliBackendBase — spawn-per-turn, no stdin loop.
// Decision 1A: prepend EditorStateSnapshot on first turn only (resumeId == null).
using System.Collections.Generic;
using System.IO;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    internal sealed class CodexBackend : CliBackendBase
    {
        private readonly string   _pythonCommand;
        private readonly string[] _pythonArgs;
        private readonly int      _startupTimeoutSec;
        private readonly string   _extraArgs;

        internal CodexBackend(string resumeSessionId = null,
            int startupTimeoutSec = 30, string extraArgs = null)
        {
            SessionId          = resumeSessionId;
            _startupTimeoutSec = startupTimeoutSec;
            _extraArgs         = extraArgs;

            var packageRoot = Path.GetFullPath("Packages/com.unity-mcp.editor");
            var serverDir   = ChatMcpConfigWriter.ResolveServerDir(packageRoot);
            if (serverDir != null)
                (_pythonCommand, _pythonArgs) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, null);
            else
            {
                _pythonCommand = "python3";
                _pythonArgs    = new[] { "-m", "unity_mcp.server" };
            }
        }

        protected override string BinaryName          => "codex";
        protected override bool   IsPersistentProcess => false;

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(string binaryPath, string resumeId)
        {
            var rawPrompt = ExtractPromptText(PendingPrompt ?? "");

            // Decision 1A: inject snapshot on first turn only.
            if (resumeId == null && !string.IsNullOrEmpty(rawPrompt))
            {
                var snapshot = EditorStateSnapshot.Capture();
                if (!string.IsNullOrEmpty(snapshot))
                    rawPrompt = snapshot + "\n\n" + rawPrompt;
            }

            return CodexArgBuilder.Build(rawPrompt, resumeId, _pythonCommand, _pythonArgs,
                _startupTimeoutSec, _extraArgs);
        }

        protected override void ParseLine(string line, List<ChatEvent> sink)
            => CodexStreamParser.ParseLine(line, sink);

        // ── Helpers ───────────────────────────────────────────────────────────

        // Extract raw user text from the UserTurnBuilder JSON envelope:
        // {"message":{"role":"user","content":[{"type":"text","text":"..."}]}}
        private static string ExtractPromptText(string turnJson)
        {
            var msg     = JsonHelper.ExtractObject(turnJson, "message");
            var content = JsonHelper.ExtractArray(msg, "content");
            var first   = JsonHelper.ExtractFirstArrayObject(content);
            return (first != null ? JsonHelper.ExtractString(first, "text") : null) ?? "";
        }
    }
}
