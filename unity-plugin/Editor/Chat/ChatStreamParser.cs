// Parses claude --output-format stream-json NDJSON lines into ChatEvents.
// Pure logic: no UnityEngine deps, fully NUnit-testable.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class ChatStreamParser
    {
        /// <summary>
        /// Parse one NDJSON line. Returns null for silently-ignored line types.
        /// Never throws — malformed input yields an Error event.
        /// </summary>
        public static ChatEvent? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            try   { return ParseInternal(line); }
            catch (Exception ex) { return ChatEvent.Error("Parse error: " + ex.Message); }
        }

        /// <summary>
        /// Parses a line and appends 0..N events to <paramref name="sink"/>.
        /// For "user" lines with multiple tool_result entries, emits one event per entry.
        /// For all other lines, delegates to ParseLine (0 or 1 event).
        /// </summary>
        public static void ParseInto(string line, List<ChatEvent> sink)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            try
            {
                if (JsonHelper.ExtractString(line, "type") == "user")
                {
                    UserToolResultParser.ParseAll(line, sink);
                    return;
                }
            }
            catch { /* fall through */ }

            var ev = ParseLine(line);
            if (ev.HasValue) sink.Add(ev.Value);
        }

        private static ChatEvent? ParseInternal(string line)
        {
            var type = JsonHelper.ExtractString(line, "type");
            switch (type)
            {
                case "system":       return ParseSystem(line);
                case "result":       return ParseResult(line);
                case "stream_event": return ParseStreamEvent(line);
                case "user":         return UserToolResultParser.ParseFirst(line);
                case "assistant":    return null;    // silently ignored
                case null:
                    return LooksMalformed(line) ? ChatEvent.Error("Malformed stream line") : (ChatEvent?)null;
                default:
                    return null; // forward-compat: unknown top-level types = no-op
            }
        }

        // Balanced-bracket scan: a line that opens as JSON but never closes is malformed.
        private static bool LooksMalformed(string line)
        {
            var t = line.TrimStart();
            if (t.Length == 0 || (t[0] != '{' && t[0] != '[')) return false;
            int depth = 0; bool inStr = false, esc = false;
            foreach (var c in t)
            {
                if (esc) { esc = false; continue; }
                if (inStr) { if (c == '\\') esc = true; else if (c == '"') inStr = false; continue; }
                if (c == '"') inStr = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') { if (--depth < 0) return true; }
            }
            return depth != 0 || inStr;
        }

        // ── system ────────────────────────────────────────────────────────────

        private static ChatEvent? ParseSystem(string line)
        {
            var subtype = JsonHelper.ExtractString(line, "subtype");
            switch (subtype)
            {
                case "init":
                    var sid = JsonHelper.ExtractString(line, "session_id");
                    return ChatEvent.TurnDone(sid, 0f, 0, 0);
                case "api_retry":
                    var err = JsonHelper.ExtractString(line, "error") ?? "API retry";
                    return ChatEvent.Error(err);
                default:
                    return null;
            }
        }

        // ── result ────────────────────────────────────────────────────────────

        private static ChatEvent? ParseResult(string line)
        {
            var isError = JsonHelper.ExtractString(line, "is_error");
            if (isError == "true")
            {
                var errMsg = JsonHelper.ExtractString(line, "error") ?? "Unknown error";
                return ChatEvent.Error(errMsg);
            }
            var sid     = JsonHelper.ExtractString(line, "session_id");
            var costStr = JsonHelper.ExtractString(line, "total_cost_usd");
            float.TryParse(costStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var costUsd);
            var usage = JsonHelper.ExtractObject(line, "usage");
            int.TryParse(JsonHelper.ExtractString(usage, "input_tokens"),  out var inp);
            int.TryParse(JsonHelper.ExtractString(usage, "output_tokens"), out var out_);
            return ChatEvent.TurnDone(sid, costUsd, inp, out_);
        }

        // ── stream_event ──────────────────────────────────────────────────────

        private static ChatEvent? ParseStreamEvent(string line)
        {
            var ev     = JsonHelper.ExtractObject(line, "event");
            var evType = JsonHelper.ExtractString(ev, "type");
            switch (evType)
            {
                case "content_block_delta": return ParseContentBlockDelta(ev);
                case "content_block_start": return ParseContentBlockStart(ev);
                case "content_block_stop":  return ChatEvent.ToolArgsComplete();
                case "message_start":
                case "message_delta":
                case "message_stop":        return null;
                default:                    return null;
            }
        }

        private static ChatEvent? ParseContentBlockDelta(string ev)
        {
            var delta     = JsonHelper.ExtractObject(ev, "delta");
            var deltaType = JsonHelper.ExtractString(delta, "type");
            switch (deltaType)
            {
                case "text_delta":
                    var text = JsonHelper.ExtractString(delta, "text") ?? "";
                    return ChatEvent.TextDelta(text);
                case "input_json_delta":
                    var partial = JsonHelper.ExtractString(delta, "partial_json") ?? "";
                    return ChatEvent.ToolStart(null, partial); // partial args, name unknown
                default:
                    return null;
            }
        }

        private static ChatEvent? ParseContentBlockStart(string ev)
        {
            var block = JsonHelper.ExtractObject(ev, "content_block");
            var bType = JsonHelper.ExtractString(block, "type");
            if (bType == "tool_use")
            {
                var name = JsonHelper.ExtractString(block, "name") ?? "";
                var id   = JsonHelper.ExtractString(block, "id")   ?? "";
                return ChatEvent.ToolStart(name, "", id);
            }
            return null;
        }
    }
}
