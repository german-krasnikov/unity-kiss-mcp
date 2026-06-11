// Codex persistent session via `codex app-server` (direct stdio, JSON-RPC 2.0).
// One process per session; MCP server stays connected across all turns.
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    internal sealed class CodexAppServerBackend : CliBackendBase
    {
        private readonly string   _pythonCommand;
        private readonly string[] _pythonArgs;
        private readonly int      _startupTimeoutSec;
        private readonly string   _extraArgs;

        // ── State machine ─────────────────────────────────────────────────────
        private int    _nextId;           // atomic JSON-RPC id counter
        private string _pendingTurnText;  // queued until threadId arrives from thread/start response

        protected override string BinaryName          => "codex";
        protected override bool   IsPersistentProcess => true;

        internal CodexAppServerBackend(string resumeSessionId = null,
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
                _pythonCommand = CodexArgBuilder.PythonFallback;
                _pythonArgs = new[] { "-m", "unity_mcp.server" };
            }
        }

        // ── Axis overrides ────────────────────────────────────────────────────

        protected override (string[] args, string[] stripEnvKeys) BuildArgs(string binaryPath, string resumeId)
        {
            // resumeId is irrelevant here — thread resume goes via JSON-RPC, not argv
            var args = new List<string> { "app-server" };
            args.Add("-c");
            args.Add($"mcp_servers.unity.command=\"{CodexArgBuilder.TomlEscapeString(_pythonCommand ?? CodexArgBuilder.PythonFallback)}\"");
            args.Add("-c");
            args.Add($"mcp_servers.unity.args=[{BuildTomlArray(_pythonArgs)}]");
            args.Add("-c");
            args.Add($"mcp_servers.unity.startup_timeout_sec={_startupTimeoutSec}");

            if (!string.IsNullOrEmpty(_extraArgs))
                foreach (var token in ArgTokenizer.Split(_extraArgs))
                    args.Add(token);

            return (args.ToArray(), new[] { "OPENAI_API_KEY" });
        }

        protected override void SpawnNewProcess(string binary, string[] args, string[] strip)
        {
            base.SpawnNewProcess(binary, args, strip);
            // Send initialize immediately after spawn (fire-and-forget; response is ignored)
            var id = ++_nextId;
            base.WriteLineToProc(
                $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"initialize\"," +
                $"\"params\":{{\"clientInfo\":{{\"name\":\"unity-mcp\",\"version\":\"1\"}}}}}}");
        }

        protected override void WriteLineToProc(string turnJson)
        {
            var text = ExtractPromptText(turnJson);

            if (SessionId == null)
            {
                // First turn: send thread/start and queue the actual turn text until
                // sessionId (threadId) arrives in DrainEvents via ParseLine.
                _pendingTurnText = text;
                var id       = ++_nextId;
                var snapshot = EditorStateSnapshot.Capture();
                var instr    = string.IsNullOrEmpty(snapshot)
                    ? "null"
                    : $"\"{JsonHelper.EscapeJson(snapshot)}\"";
                var cwd      = JsonHelper.EscapeJson(ProjectRoot());
                base.WriteLineToProc(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"thread/start\"," +
                    $"\"params\":{{\"cwd\":\"{cwd}\",\"baseInstructions\":{instr}}}}}");
            }
            else
            {
                SendTurnStart(text, SessionId);
            }
        }

        // DrainRawLines is called at the start of every DrainEvents tick (every ~33ms).
        // SessionId is set by the base loop on the tick that processes SessionInit.
        // On the following tick, this check fires and sends the queued turn/start.
        protected override void DrainRawLines(List<string> buf)
        {
            base.DrainRawLines(buf);
            if (_pendingTurnText != null && SessionId != null)
            {
                var text = _pendingTurnText;
                _pendingTurnText = null;
                SendTurnStart(text, SessionId);
            }
        }

        protected override void ParseLine(string line, List<ChatEvent> sink)
            => CodexAppServerParser.ParseLine(line, sink);

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SendTurnStart(string text, string threadId)
        {
            var id = ++_nextId;
            base.WriteLineToProc(
                $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"turn/start\"," +
                $"\"params\":{{\"threadId\":\"{threadId}\"," +
                $"\"input\":[{{\"type\":\"text\",\"text\":\"{JsonHelper.EscapeJson(text)}\"}}]}}}}");
        }

        // Extract raw text from UserTurnBuilder envelope: {"message":{"content":[{"text":"..."}]}}
        private static string ExtractPromptText(string turnJson)
        {
            var msg     = JsonHelper.ExtractObject(turnJson, "message");
            var content = JsonHelper.ExtractArray(msg, "content");
            var first   = JsonHelper.ExtractFirstArrayObject(content);
            return (first != null ? JsonHelper.ExtractString(first, "text") : null) ?? "";
        }

        private static string ProjectRoot()
        {
#if UNITY_EDITOR
            return System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? ".";
#else
            return System.IO.Directory.GetCurrentDirectory();
#endif
        }

        private static string BuildTomlArray(string[] items)
        {
            if (items == null || items.Length == 0) return "";
            var parts = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
                parts[i] = $"\"{CodexArgBuilder.TomlEscapeString(items[i])}\"";
            return string.Join(",", parts);
        }
    }
}
