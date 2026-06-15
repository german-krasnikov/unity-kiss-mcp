// Parses `gemini --output-format stream-json` NDJSON lines into ChatEvents.
// Pure logic: no UnityEngine deps, fully NUnit-testable.
// Field mapping vs Claude: tool_name→name, tool_id→id, parameters→input.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class GeminiParser
    {
        public static void ParseLine(string line, List<ChatEvent> sink)
        {
            if (string.IsNullOrEmpty(line)) return;
            try { Dispatch(line, sink); }
            catch (Exception ex) { sink.Add(ChatEvent.Error("GeminiParser: " + ex.Message)); }
        }

        private static void Dispatch(string line, List<ChatEvent> sink)
        {
            var type = JsonHelper.ExtractString(line, "type");
            switch (type)
            {
                case "init":
                    // Emit SessionInit so the session id is tracked.
                    var sid = JsonHelper.ExtractString(line, "session_id");
                    if (!string.IsNullOrEmpty(sid)) sink.Add(ChatEvent.SessionInit(sid));
                    break;

                case "message":
                    // Skip prompt echo: Gemini echoes user input as role:user message.
                    var role = JsonHelper.ExtractString(line, "role") ?? "";
                    if (role == "user") break;
                    var content = JsonHelper.ExtractString(line, "content") ?? "";
                    if (content.Length > 0) sink.Add(ChatEvent.TextDelta(content));
                    break;

                case "tool_use":
                    // Skip internal Gemini tools (update_topic, google_search, etc.) — show only MCP tools.
                    var toolName = JsonHelper.ExtractString(line, "tool_name") ?? "";
                    if (!toolName.StartsWith("mcp_", StringComparison.Ordinal)) break;
                    var toolId   = JsonHelper.ExtractString(line, "tool_id")   ?? "";
                    var argsJson = JsonHelper.ExtractObject(line, "parameters");
                    if (argsJson == "{}") argsJson = "";
                    // ask_user goes through MCP TCP path (CommandRouter.OnAskUser) —
                    // suppress tool_use display to avoid double AskUserCard.
                    if (toolName.EndsWith("ask_user", StringComparison.Ordinal)) break;
                    sink.Add(ChatEvent.ToolStart(toolName, argsJson, toolId));
                    sink.Add(ChatEvent.ToolArgsComplete());
                    break;

                case "tool_result":
                    var resId     = JsonHelper.ExtractString(line, "tool_id") ?? "";
                    var status    = JsonHelper.ExtractString(line, "status")  ?? "";
                    var output    = JsonHelper.ExtractString(line, "output")  ?? "";
                    var ok        = status != "error";
                    sink.Add(ChatEvent.ToolResult(resId, output, ok));
                    break;

                case "result":
                    var resSid     = JsonHelper.ExtractString(line, "session_id");
                    var stats      = JsonHelper.ExtractObject(line, "stats");
                    int.TryParse(JsonHelper.ExtractString(stats, "total_tokens"), out var totalTokens);
                    sink.Add(ChatEvent.TurnDone(resSid, 0f, 0, totalTokens));
                    break;

                case "error":
                    var errMsg = JsonHelper.ExtractString(line, "message") ?? "Gemini error";
                    sink.Add(ChatEvent.Error(errMsg));
                    break;

                // "init" without session_id, unknown types — ignore (forward-compat).
            }
        }
    }
}
