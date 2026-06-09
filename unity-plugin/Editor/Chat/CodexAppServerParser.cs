// Parse JSON-RPC 2.0 lines from `codex app-server` into ChatEvents.
// Notifications have "method", responses have "result". Never throws.
using System;
using System.Collections.Generic;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public static class CodexAppServerParser
    {
        public static void ParseLine(string line, List<ChatEvent> sink)
        {
            if (string.IsNullOrEmpty(line)) return;
            try { Dispatch(line, sink); }
            catch (Exception ex) { sink.Add(ChatEvent.Error("CodexAppServerParser: " + ex.Message)); }
        }

        // ── Top-level dispatch ────────────────────────────────────────────────

        private static void Dispatch(string line, List<ChatEvent> sink)
        {
            var method = JsonHelper.ExtractString(line, "method");
            if (method != null) { DispatchNotification(method, line, sink); return; }

            // JSON-RPC response: check for result.thread.id (thread/start response)
            // Codex app-server never sends empty-object results; ExtractObject returns "{}" when key absent
            var result = JsonHelper.ExtractObject(line, "result");
            if (result != "{}")
            {
                var thread   = JsonHelper.ExtractObject(result, "thread");
                var threadId = JsonHelper.ExtractString(thread, "id");
                if (!string.IsNullOrEmpty(threadId))
                    sink.Add(ChatEvent.SessionInit(threadId));
                return; // recognized response (even if no threadId — e.g., turn/start ack)
            }

            // Has neither method nor result — malformed or completely unrecognized line.
            // Every valid JSON-RPC message has one of those, so flag it.
            sink.Add(ChatEvent.Error($"CodexAppServerParser: unrecognized line: {line.Substring(0, System.Math.Min(60, line.Length))}"));
        }

        // ── Notification dispatch ─────────────────────────────────────────────

        private static void DispatchNotification(string method, string line, List<ChatEvent> sink)
        {
            var p = JsonHelper.ExtractObject(line, "params");
            switch (method)
            {
                case "item/agentMessage/delta":
                    var delta = JsonHelper.ExtractString(p, "delta") ?? "";
                    if (delta.Length > 0) sink.Add(ChatEvent.TextDelta(delta));
                    break;

                case "item/started":
                    DispatchItem("item/started", JsonHelper.ExtractObject(p, "item"), sink);
                    break;

                case "item/completed":
                    DispatchItem("item/completed", JsonHelper.ExtractObject(p, "item"), sink);
                    break;

                case "turn/completed":
                    sink.Add(ChatEvent.TurnDone(null, 0f, 0, 0));
                    break;

                case "thread/started":
                    var thread = JsonHelper.ExtractObject(p, "thread");
                    var tid = JsonHelper.ExtractString(thread, "id");
                    if (!string.IsNullOrEmpty(tid)) sink.Add(ChatEvent.SessionInit(tid));
                    break;

                // mcpServer/*, thread/status/changed, turn/started, item/agentMessage/*, etc. — ignore
            }
        }

        // ── Item dispatch ────────────────────────────────────────────────────

        private static void DispatchItem(string eventType, string item, List<ChatEvent> sink)
        {
            var itemType = JsonHelper.ExtractString(item, "type");
            switch (itemType)
            {
                case "mcpToolCall":   // camelCase in app-server (NOT mcp_tool_call)
                    DispatchMcpToolCall(eventType, item, sink);
                    break;
                // reasoning, agentMessage, userMessage, etc. — ignore
            }
        }

        private static void DispatchMcpToolCall(string eventType, string item, List<ChatEvent> sink)
        {
            var id     = JsonHelper.ExtractString(item, "id");
            var server = JsonHelper.ExtractString(item, "server") ?? "unity";
            var tool   = JsonHelper.ExtractString(item, "tool")   ?? "unknown";
            var name   = $"{server}:{tool}";

            if (eventType == "item/started")
            {
                var argsJson = JsonHelper.ExtractObject(item, "arguments");
                sink.Add(ChatEvent.ToolStart(name, argsJson, id));
                sink.Add(ChatEvent.ToolArgsComplete());
                return;
            }

            // item/completed
            var status = JsonHelper.ExtractString(item, "status");
            var ok     = status == "completed";
            string resultText;
            if (ok)
            {
                var resultObj = JsonHelper.ExtractObject(item, "result");
                var content   = JsonHelper.ExtractArray(resultObj, "content");
                var first     = JsonHelper.ExtractFirstArrayObject(content);
                resultText    = (first != null ? JsonHelper.ExtractString(first, "text") : null) ?? "";
            }
            else
            {
                var errObj = JsonHelper.ExtractObject(item, "error");
                resultText = JsonHelper.ExtractString(errObj, "message") ?? "";
            }
            sink.Add(ChatEvent.ToolResult(id, resultText, ok));
        }
    }
}
