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
            if (_evBuf.Count == 0 && _toolBuf.Count == 0) return;
            // Refresh the ref cache at most ~1/sec while streaming so objects Claude just
            // created become clickable without reopening the window. Idle frames early-return
            // above, so this never spins when nothing is streaming.
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
                    _waitingReply = false; _typingDots.style.display = DisplayStyle.None; break;
                case ChatEventKind.TurnDone:
                    if (_activity.Done()) OnActivityChanged();
                    _transcript.FinalizeAssistant();
                    if (ev.CostUsd > 0f)
                    {
                        _totalCostUsd += ev.CostUsd; _inputTokens += ev.InputTokens; _outputTokens += ev.OutputTokens;
                        _costBadge.text = $"${_totalCostUsd:F4}  {_inputTokens}↑{_outputTokens}↓";
                    }
                    _waitingReply = false; _typingDots.style.display = DisplayStyle.None; break;
                case ChatEventKind.Error:
                    if (_activity.Fail()) OnActivityChanged();
                    _transcript.AppendToolChip(ev.Text ?? "Error", ok: false);
                    _waitingReply = false; _typingDots.style.display = DisplayStyle.None; break;
            }
        }

        private void HandleToolRecord(ToolCallRecord rec)
        {
            if (rec.ArgsJson == null && !rec.HasResult)
            {
                // null ArgsJson = chip-creation record (ToolStart moment)
                if (_activity.FirstToken()) OnActivityChanged();
                _transcript.AppendToolChip(rec.Name, ok: true, toolId: rec.Id);
                _waitingReply = false; _typingDots.style.display = DisplayStyle.None;
            }
            else
            {
                // ArgsJson or HasResult = detail update
                _transcript.UpdateToolDetail(rec.Id, rec);
            }
        }

        private void TickDots()
        {
            if (!_waitingReply) return;
            _dotCount = (_dotCount + 1) % 4;
            _typingDots.text = new string('.', _dotCount + 1);
        }
    }
}
