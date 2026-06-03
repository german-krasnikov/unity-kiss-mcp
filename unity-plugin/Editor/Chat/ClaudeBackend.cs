// IChatBackend implementation: composes resolver + arg-builder + process.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ClaudeBackend : IChatBackend, IDisposable
    {
        private readonly string _mcpConfigPath;
        private readonly string _permissionMode; // "plan" | "acceptEdits"

        private ChatProcess _proc;
        private readonly List<string> _drainBuf = new List<string>(32);

        public bool   IsRunning => _proc?.IsRunning ?? false;
        public string SessionId { get; private set; }

        internal ClaudeBackend(string mcpConfigPath, string permissionMode)
        {
            _mcpConfigPath  = mcpConfigPath;
            _permissionMode = permissionMode;
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

            var (args, strip) = ClaudeArgBuilder.Build(binary, _mcpConfigPath, _permissionMode, SessionId);
            _proc = new ChatProcess();
            _proc.Spawn(binary, args, strip);
        }

        public void SendTurn(string turnJson)
        {
            if (!IsRunning) Start();
            _proc?.WriteLine(turnJson);
        }

        public void DrainEvents(List<ChatEvent> output)
        {
            if (_proc == null) return;
            _drainBuf.Clear();
            _proc.DrainLines(_drainBuf);

            foreach (var line in _drainBuf)
            {
                var ev = ChatStreamParser.ParseLine(line);
                if (ev == null) continue;

                // Capture session ID from init or turn-done
                if (ev.Value.Kind == ChatEventKind.TurnDone && !string.IsNullOrEmpty(ev.Value.SessionId))
                    SessionId = ev.Value.SessionId;

                output.Add(ev.Value);
            }
        }

        public void Stop()
        {
            _proc?.Dispose();
            _proc = null;
        }

        public void Dispose() => Stop();
    }
}
