// Drain loop + event handlers extracted to keep MCPChatWindow.cs under 200 lines.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private double _lastRefresh;

        private void DrainAndRender()
        {
            _evBuf.Clear(); _toolBuf.Clear();
            _backend?.DrainEvents(_evBuf, _toolBuf);

            if (_evBuf.Count == 0 && _toolBuf.Count == 0)
            {
                // Dead-process guard: buffer is empty, so no result line is coming.
                // Only fires if the process truly died (crash/kill) without emitting a result.
                // Phase-first: cheap field read before the nullable backend deref.
                if (_activity.Phase != ActivityPhase.Idle && _backend != null && !_backend.IsRunning)
                {
                    if (_activity.Fail()) OnActivityChanged();
                }
                return;
            }
            // Refresh the ref cache at most ~1/sec while streaming so objects Claude just
            // created become clickable without reopening the window.
            if (EditorApplication.timeSinceStartup - _lastRefresh > 1.0)
            {
                _resolver?.Refresh();
                _lastRefresh = EditorApplication.timeSinceStartup;
            }
            foreach (var ev in _evBuf) HandleEvent(ev);
            foreach (var rec in _toolBuf) HandleToolRecord(rec);
            _transcript.FlushStreaming();
            _scroll.scrollOffset = new Vector2(0, float.MaxValue);
        }

        private void HandleEvent(ChatEvent ev)
        {
            switch (ev.Kind)
            {
                case ChatEventKind.TextDelta:
                    if (_activity.FirstToken()) OnActivityChanged();
                    _transcript.AppendOrExtendAssistant(ev.Text);
                    break;
                case ChatEventKind.TurnDone:
                    if (_activity.Done()) OnActivityChanged();
                    _transcript.FinalizeAssistant();
                    if (ev.InputTokens > 0 || ev.OutputTokens > 0)
                    {
                        _inputTokens  += ev.InputTokens;
                        _outputTokens += ev.OutputTokens;
                        _tokenReadout.text =
                            $"↑ {TokenFormat.Abbr(_inputTokens)}  ↓ {TokenFormat.Abbr(_outputTokens)}";
                    }
                    break;
                case ChatEventKind.SessionInit:
                    break; // non-terminal: session established, keep animation running
                case ChatEventKind.Error:
                    if (_activity.Fail()) OnActivityChanged();
                    _transcript.AppendToolChip(ev.Text ?? "Error", ok: false);
                    break;
            }
        }

        private void HandleToolRecord(ToolCallRecord rec)
        {
            if (rec.ArgsJson == null && !rec.HasResult)
            {
                // null ArgsJson = chip-creation record (ToolStart moment)
                if (_activity.FirstToken()) OnActivityChanged();
                _transcript.AppendToolChip(rec.Name, ok: true, toolId: rec.Id);
            }
            else
            {
                // ArgsJson or HasResult = detail update
                _transcript.UpdateToolDetail(rec.Id, rec);
            }
        }
    }
}
