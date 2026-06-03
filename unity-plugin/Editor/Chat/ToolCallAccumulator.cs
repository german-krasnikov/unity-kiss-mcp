// Stateful assembler: turns raw ChatEvents into ToolCallRecords.
// Chip creation on ToolStart; args assembled on ToolArgsComplete; result on ToolResult.
// Pure: zero UnityEngine deps, fully NUnit-testable.
using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ToolCallAccumulator
    {
        private string        _currentId;
        private string        _currentName;
        private readonly StringBuilder _argsBuilder = new StringBuilder();

        // Completed (args assembled) calls awaiting their result event.
        private readonly Dictionary<string, ToolCallRecord> _pending =
            new Dictionary<string, ToolCallRecord>();

        /// <summary>
        /// Feed one event. Returns a ToolCallRecord when the event is a chip-creation,
        /// args-complete, or result signal. Returns null for all other events.
        /// </summary>
        internal ToolCallRecord? Feed(ChatEvent ev)
        {
            switch (ev.Kind)
            {
                case ChatEventKind.ToolStart when ev.Text != null:
                    // content_block_start — new tool call begins
                    _currentId   = ev.ToolId;
                    _currentName = ev.Text;
                    _argsBuilder.Clear();
                    // null ArgsJson = chip-creation marker (discriminates from "" = assembled-empty)
                    return new ToolCallRecord(_currentName, _currentId, null);

                case ChatEventKind.ToolStart:
                    // input_json_delta — accumulate arg fragment (name == null)
                    _argsBuilder.Append(ev.ArgsJson);
                    return null;

                case ChatEventKind.ToolArgsComplete:
                    if (_currentId == null) return null;   // stop for a text/non-tool block
                    var assembled = new ToolCallRecord(_currentName, _currentId,
                        _argsBuilder.ToString());
                    _pending[_currentId] = assembled;
                    _currentId = null; _currentName = null; _argsBuilder.Clear();
                    return assembled;

                case ChatEventKind.ToolResult:
                    if (_pending.TryGetValue(ev.ToolId, out var pending))
                    {
                        _pending.Remove(ev.ToolId);
                        return pending.WithResult(ev.Text, ev.IsOk);
                    }
                    // Orphan: no matching start (edge case or tool result before args complete)
                    return new ToolCallRecord("?", ev.ToolId, "", ev.Text, ev.IsOk);

                default:
                    return null;
            }
        }

        internal void Reset()
        {
            _currentId = null; _currentName = null;
            _argsBuilder.Clear(); _pending.Clear();
        }
    }
}
