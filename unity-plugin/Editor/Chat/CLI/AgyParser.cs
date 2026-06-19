// Parses `agy -p` plain text output into ChatEvents.
// agy outputs plain text (not NDJSON) — each non-empty line is a text delta.
// TurnDone is emitted when AntigravityBackend injects EofSentinel on process exit.
// Pure logic: no UnityEngine deps, fully NUnit-testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class AgyParser
    {
        public static void ParseLine(string line, List<ChatEvent> sink)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (line == AntigravityBackend.EofSentinel)
            {
                sink.Add(ChatEvent.TurnDone(null, 0f, 0, 0));
                return;
            }
            sink.Add(ChatEvent.TextDelta(line));
        }
    }
}
