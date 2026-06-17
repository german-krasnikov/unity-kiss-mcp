// Parses `kimi --output-format stream-json` NDJSON lines into ChatEvents.
// Role-based dispatch (no "type" field, unlike Gemini).
// Pure logic: no UnityEngine deps, fully NUnit-testable.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class KimiParser
    {
        public static void ParseLine(string line, List<ChatEvent> sink)
        {
            if (string.IsNullOrEmpty(line)) return;
            try { Dispatch(line, sink); }
            catch (Exception ex) { sink.Add(ChatEvent.Error("KimiParser: " + ex.Message)); }
        }

        private static void Dispatch(string line, List<ChatEvent> sink)
        {
            var role = JsonHelper.ExtractString(line, "role");
            switch (role)
            {
                case "assistant":
                    var toolCallsRaw = JsonHelper.ExtractArray(line, "tool_calls");
                    if (!string.IsNullOrEmpty(toolCallsRaw) && toolCallsRaw != "[]")
                        DispatchToolCalls(toolCallsRaw, sink);
                    else
                    {
                        var content = JsonHelper.ExtractString(line, "content") ?? "";
                        if (content.Length > 0) sink.Add(ChatEvent.TextDelta(content));
                    }
                    // Fallback: some kimi versions may emit finish_reason:"stop".
                    if (JsonHelper.ExtractString(line, "finish_reason") == "stop")
                        sink.Add(ChatEvent.TurnDone(null, 0f, 0, 0));
                    break;

                case "meta":
                    if (JsonHelper.ExtractString(line, "type") == "session.resume_hint")
                        sink.Add(ChatEvent.TurnDone(null, 0f, 0, 0));
                    break;

                case "tool":
                    var id      = JsonHelper.ExtractString(line, "tool_call_id") ?? "";
                    var result  = JsonHelper.ExtractString(line, "content") ?? "";
                    var isError = JsonHelper.ExtractString(line, "isError");
                    var ok      = !string.Equals(isError, "true", StringComparison.OrdinalIgnoreCase);
                    sink.Add(ChatEvent.ToolResult(id, result, ok: ok));
                    break;

                // "user" echo and unknown roles — ignore
            }
        }

        private static void DispatchToolCalls(string arrayJson, List<ChatEvent> sink)
        {
            // Walk each element of the tool_calls array.
            // Format: [{"id":"tc-1","type":"function","function":{"name":"...","arguments":"..."}}]
            int pos = 0;
            while (pos < arrayJson.Length)
            {
                var obj = JsonHelper.ExtractNextArrayObject(arrayJson, ref pos);
                if (obj == null) break;

                var funcObj  = JsonHelper.ExtractObject(obj, "function");
                if (string.IsNullOrEmpty(funcObj) || funcObj == "{}") continue;

                var name = JsonHelper.ExtractString(funcObj, "name") ?? "";
                if (!name.StartsWith("mcp_", StringComparison.Ordinal)) continue;
                if (name.EndsWith("ask_user", StringComparison.Ordinal)) continue;

                var toolId   = JsonHelper.ExtractString(obj, "id") ?? "";
                var argsJson = JsonHelper.ExtractString(funcObj, "arguments") ?? "";

                sink.Add(ChatEvent.ToolStart(name, argsJson, toolId));
                sink.Add(ChatEvent.ToolArgsComplete());
            }
        }
    }
}
