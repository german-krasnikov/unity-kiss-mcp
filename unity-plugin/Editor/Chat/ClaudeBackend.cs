// IChatBackend implementation: composes resolver + arg-builder + process.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ClaudeBackend : IChatBackend, IDisposable
    {
        private readonly string           _mcpConfigPath;
        private readonly string           _permissionMode; // "plan" | "acceptEdits"
        private readonly string           _agentName;      // null = default Claude; non-null → --agent <name>
        private readonly PermissionConfig _permConfig;     // null → blanket allow-all

        private ChatProcess _proc;
        private readonly List<string>           _drainBuf    = new List<string>(32);
        private readonly ToolCallAccumulator    _accumulator = new ToolCallAccumulator();

        public bool   IsRunning => _proc?.IsRunning ?? false;
        public string SessionId { get; private set; }

        internal ClaudeBackend(string mcpConfigPath, string permissionMode,
            string agentName = null, PermissionConfig permConfig = null,
            string resumeSessionId = null)
        {
            _mcpConfigPath  = mcpConfigPath;
            _permissionMode = permissionMode;
            _agentName      = agentName;
            _permConfig     = permConfig;
            SessionId       = resumeSessionId; // pre-seed for --resume on first Start()
        }

        public void Start()
        {
            if (IsRunning) return;
            var binary = ChatBinaryResolver.Resolve();
            if (binary == null)
            {
                Debug.LogError("[MCP Chat] claude binary not found — check Settings > Agent Chat > Binary Path.");
                return;
            }
            var allowed = _permConfig?.GetAllowedToolIds();
            // Inject editor state on fresh sessions only (SessionId==null means no --resume).
            var snapshot = SessionId == null ? EditorStateSnapshot.Capture() : null;
            var (args, strip) = ClaudeArgBuilder.Build(binary, _mcpConfigPath, _permissionMode,
                SessionId, _agentName, allowed, snapshot);
            _proc = new ChatProcess();
            _proc.Spawn(binary, args, strip);
        }

        public void SendTurn(string turnJson)
        {
            if (!IsRunning) Start();
            _proc?.WriteLine(turnJson);
        }

        private readonly List<ChatEvent> _parseBuf = new List<ChatEvent>(4);

        public void DrainEvents(List<ChatEvent> output, List<ToolCallRecord> toolOutput = null)
        {
            if (_proc == null) return;
            _drainBuf.Clear();
            _proc.DrainLines(_drainBuf);

            foreach (var line in _drainBuf)
            {
                _parseBuf.Clear();
                ChatStreamParser.ParseInto(line, _parseBuf);

                foreach (var ev in _parseBuf)
                {
                    if ((ev.Kind == ChatEventKind.TurnDone || ev.Kind == ChatEventKind.SessionInit)
                        && !string.IsNullOrEmpty(ev.SessionId))
                        SessionId = ev.SessionId;

                    var rec = _accumulator.Feed(ev);
                    if (rec.HasValue && toolOutput != null)
                        toolOutput.Add(rec.Value);

                    // Only forward non-tool events to the UI event buffer
                    switch (ev.Kind)
                    {
                        case ChatEventKind.TextDelta:
                        case ChatEventKind.TurnDone:
                        case ChatEventKind.SessionInit:
                        case ChatEventKind.Error:
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

        public void Dispose() => Stop();
    }
}
