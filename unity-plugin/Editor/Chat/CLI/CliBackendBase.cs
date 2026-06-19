// Shared lifecycle for all CLI chat backends (Claude, Codex, etc).
// Each CLI subclass overrides only the 5 variation axes.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public abstract class CliBackendBase : IChatBackend, IDisposable
    {
        // ── 5 AXES ───────────────────────────────────────────────────────────

        // (a) Build argv + env-keys-to-strip. resumeId is null on first turn.
        protected abstract (string[] args, string[] stripEnvKeys) BuildArgs(string binaryPath, string resumeId);

        // (b) Parse one NDJSON line into 0..N ChatEvents.
        protected abstract void ParseLine(string line, List<ChatEvent> sink);

        // (c) CLI binary name for ChatBinaryResolver (e.g. "claude", "codex").
        protected abstract string BinaryName { get; }

        // (d) true = persistent stdin loop (Claude), false = spawn-per-turn (Codex).
        protected abstract bool IsPersistentProcess { get; }

        // (e) true = send Agent SDK initialize handshake after spawn.
        protected virtual bool SendInitializeHandshake => false;

        // ── SHARED STATE ─────────────────────────────────────────────────────

        // Injectable logger — prod default is Debug.LogError; tests swap to capture.
        internal Action<string> LogError = UnityEngine.Debug.LogError;

        internal  ChatProcess _proc;
        private   readonly List<string>         _drainBuf    = new List<string>(32);
        private   readonly List<ChatEvent>       _parseBuf    = new List<ChatEvent>(4);
        internal  readonly ToolCallAccumulator   _accumulator = new ToolCallAccumulator();

        /// <summary>Prompt stored for spawn-per-turn backends to read in BuildArgs.</summary>
        protected string PendingPrompt { get; private set; }

        public virtual bool IsRunning => _proc?.IsRunning ?? false;
        public string SessionId { get; protected set; }

        // ── LIFECYCLE (shared, not overridden) ────────────────────────────────

        public void Start()
        {
            if (IsRunning) return;
            var binary = ChatBinaryResolver.Resolve(BinaryName);
            if (binary == null)
            {
                LogError($"[MCP Chat] {BinaryName} binary not found — check Settings > Agent Chat > Binary Path.");
                return;
            }
            var (args, strip) = BuildArgs(binary, SessionId);
            SpawnNewProcess(binary, args, strip);
            if (IsPersistentProcess && SendInitializeHandshake)
                WriteLineToProc(ControlResponseBuilder.InitializeRequest());
        }

        public void SendTurn(string turnJson)
        {
            PendingPrompt = turnJson;
            if (IsPersistentProcess)
            {
                if (!IsRunning) Start();
                WriteLineToProc(turnJson);
            }
            else
            {
                // Spawn-per-turn: dispose old, start fresh with prompt baked into argv.
                if (_proc != null) { _proc.Dispose(); _proc = null; }
                _accumulator.Reset();
                Start();
                CloseStdinOnProc();  // Codex hangs without this (spike fact #4)
            }
        }

        public void DrainEvents(List<ChatEvent> output, List<ToolCallRecord> toolOutput = null)
        {
            if (_proc == null) return;
            _drainBuf.Clear();
            DrainRawLines(_drainBuf);

            foreach (var line in _drainBuf)
            {
                _parseBuf.Clear();
                ParseLine(line, _parseBuf);

                foreach (var ev in _parseBuf)
                {
                    if ((ev.Kind == ChatEventKind.TurnDone || ev.Kind == ChatEventKind.SessionInit)
                        && !string.IsNullOrEmpty(ev.SessionId))
                        SessionId = ev.SessionId;

                    var rec = _accumulator.Feed(ev);
                    if (rec.HasValue && toolOutput != null)
                        toolOutput.Add(rec.Value);

                    switch (ev.Kind)
                    {
                        case ChatEventKind.TextDelta:
                        case ChatEventKind.TurnDone:
                        case ChatEventKind.SessionInit:
                        case ChatEventKind.Error:
                        case ChatEventKind.PermissionPrompt:
                        case ChatEventKind.AskUser:
                        case ChatEventKind.ToolProgress:
                        case ChatEventKind.RateLimit:
                        case ChatEventKind.SessionState:
                            output.Add(ev);
                            break;
                    }
                }
            }
        }

        public void Stop()
        {
            _proc?.Dispose();
            _proc = null;
            _accumulator.Reset();
        }

        /// <summary>Send a control_response JSON line to the CLI process stdin.</summary>
        public void SendControlResponse(string json) => WriteLineToProc(json);

        public void Dispose() => Stop();

        // ── SHARED HELPERS ────────────────────────────────────────────────────

        /// <summary>
        /// Unwrap Claude SDK user turn JSON {"type":"user","message":{"role":"user","content":[{"type":"text","text":"..."}]}}
        /// into plain text. Falls back to raw input if not that shape.
        /// Used by spawn-per-turn backends (Gemini, Kimi) to pass plain text via -p.
        /// </summary>
        internal static string ExtractPlainText(string turnJson)
        {
            if (string.IsNullOrEmpty(turnJson)) return turnJson;
            var msg = JsonHelper.ExtractObject(turnJson, "message");
            if (msg == null || msg == "{}") return turnJson;
            var content = JsonHelper.ExtractArray(msg, "content");
            if (string.IsNullOrEmpty(content) || content == "[]") return turnJson;
            var first = JsonHelper.ExtractFirstArrayObject(content);
            if (first == null) return turnJson;
            var text = JsonHelper.ExtractString(first, "text");
            return text ?? turnJson;
        }

        // ── PROTECTED VIRTUAL SEAMS (overridable in tests) ────────────────────

        protected virtual void DrainRawLines(List<string> buf) => _proc?.DrainLines(buf);

        protected virtual void SpawnNewProcess(string binary, string[] args, string[] strip)
        {
            _proc = new ChatProcess();
            // UNITY_MCP_PORT and UNITY_MCP_CHAT are NOT set here — if set on the CLI process,
            // ALL MCP servers inherit them (both "unity" from --mcp-config AND "unity-mcp" from
            // ~/.mcp.json), causing duplicate TCP connections to the chat port.
            // Both vars are already in --mcp-config env, scoped to "unity" server only.
            var envVars = new Dictionary<string, string>
            {
                { "UNITY_MCP_SESSION_TIMEOUT", "300" },
            };
            _proc.Spawn(binary, args, strip, envVars);
        }

        protected virtual void WriteLineToProc(string line) => _proc?.WriteLine(line);

        protected virtual void CloseStdinOnProc() => _proc?.CloseStdin();
    }
}
