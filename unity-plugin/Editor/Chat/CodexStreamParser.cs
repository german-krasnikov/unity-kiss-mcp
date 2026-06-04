// Parse one Codex NDJSON line into 0..N ChatEvents.
// Pure static, no UnityEngine deps. Never throws.
using System;
using System.Collections.Generic;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public static class CodexStreamParser
    {
        /// <summary>
        /// Parse one Codex NDJSON line. Appends 0..N events to sink.
        /// Malformed lines yield ChatEvent.Error; unknown types are silently ignored.
        /// </summary>
        public static void ParseLine(string line, List<ChatEvent> sink)
        {
            if (string.IsNullOrEmpty(line)) return;
            try
            {
                DispatchTopLevel(line, sink);
            }
            catch (Exception ex)
            {
                sink.Add(ChatEvent.Error($"CodexStreamParser: {ex.Message}"));
            }
        }

        // ── Top-level dispatch ────────────────────────────────────────────────

        private static void DispatchTopLevel(string line, List<ChatEvent> sink)
        {
            var type = JsonHelper.ExtractString(line, "type");

            // All valid Codex NDJSON lines carry a top-level "type" field.
            // null type = malformed JSON (e.g. truncated, non-object, missing key).
            if (type == null)
            {
                sink.Add(ChatEvent.Error($"Malformed Codex line (no type): {line.Substring(0, System.Math.Min(60, line.Length))}"));
                return;
            }

            switch (type)
            {
                case "thread.started":
                    var threadId = JsonHelper.ExtractString(line, "thread_id");
                    if (!string.IsNullOrEmpty(threadId))
                        sink.Add(ChatEvent.SessionInit(threadId));
                    break;

                case "turn.completed":
                    var usage = JsonHelper.ExtractObject(line, "usage");
                    var inp   = ParseInt(JsonHelper.ExtractString(usage, "input_tokens"));
                    var outp  = ParseInt(JsonHelper.ExtractString(usage, "output_tokens"));
                    sink.Add(ChatEvent.TurnDone(null, 0f, inp, outp));
                    break;

                case "turn.failed":
                    var errObj = JsonHelper.ExtractObject(line, "error");
                    var errMsg = JsonHelper.ExtractString(errObj, "message") ?? "turn.failed";
                    sink.Add(ChatEvent.Error(errMsg));
                    break;

                case "error":
                    var msg = JsonHelper.ExtractString(line, "message") ?? "error";
                    sink.Add(ChatEvent.Error(msg));
                    break;

                case "item.started":
                case "item.completed":
                    var item     = JsonHelper.ExtractObject(line, "item");
                    var itemType = JsonHelper.ExtractString(item, "type");
                    DispatchItem(type, item, itemType, sink);
                    break;

                // turn.started, item.updated, unknown — ignore
                default:
                    break;
            }
        }

        // ── Item dispatch ─────────────────────────────────────────────────────

        private static void DispatchItem(string eventType, string item, string itemType, List<ChatEvent> sink)
        {
            switch (itemType)
            {
                case "agent_message":
                    if (eventType == "item.completed")
                    {
                        var text = JsonHelper.ExtractString(item, "text") ?? "";
                        sink.Add(ChatEvent.TextDelta(text));
                    }
                    break;

                case "mcp_tool_call":
                    DispatchMcpToolCall(eventType, item, sink);
                    break;

                case "command_execution":
                    DispatchCommandExecution(eventType, item, sink);
                    break;

                case "file_change":
                    DispatchFileChange(eventType, item, sink);
                    break;

                case "error":
                    if (eventType == "item.completed")
                        sink.Add(ChatEvent.Error(JsonHelper.ExtractString(item, "message") ?? "item error"));
                    break;

                // reasoning, web_search, todo_list, collab_tool_call — ignore v1
                default:
                    break;
            }
        }

        private static void DispatchMcpToolCall(string eventType, string item, List<ChatEvent> sink)
        {
            var id     = JsonHelper.ExtractString(item, "id");
            var server = JsonHelper.ExtractString(item, "server") ?? "unity";
            var tool   = JsonHelper.ExtractString(item, "tool")   ?? "unknown";
            var name   = $"{server}:{tool}";

            if (eventType == "item.started")
            {
                // Arguments arrive complete at started — emit ToolStart + ToolArgsComplete together.
                var argsJson = JsonHelper.ExtractObject(item, "arguments");
                sink.Add(ChatEvent.ToolStart(name, argsJson, id));
                sink.Add(ChatEvent.ToolArgsComplete());
                return;
            }

            // item.completed
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

        private static void DispatchCommandExecution(string eventType, string item, List<ChatEvent> sink)
        {
            var id  = JsonHelper.ExtractString(item, "id");
            if (eventType == "item.started")
            {
                var cmd = JsonHelper.ExtractString(item, "command") ?? "";
                sink.Add(ChatEvent.ToolStart("shell:" + cmd, "", id));
                sink.Add(ChatEvent.ToolArgsComplete());
                return;
            }
            var status = JsonHelper.ExtractString(item, "status");
            var ok     = status == "completed";
            var output = JsonHelper.ExtractString(item, "aggregated_output")
                      ?? JsonHelper.ExtractString(item, "output")
                      ?? "";
            sink.Add(ChatEvent.ToolResult(id, output, ok));
        }

        private static void DispatchFileChange(string eventType, string item, List<ChatEvent> sink)
        {
            var id = JsonHelper.ExtractString(item, "id");
            if (eventType == "item.started")
            {
                var argsJson = JsonHelper.ExtractArray(item, "changes") ?? "";
                sink.Add(ChatEvent.ToolStart("file_change", argsJson, id));
                sink.Add(ChatEvent.ToolArgsComplete());
                return;
            }
            var status  = JsonHelper.ExtractString(item, "status");
            var ok      = status == "completed";
            var summary = JsonHelper.ExtractString(item, "summary") ?? "";
            sink.Add(ChatEvent.ToolResult(id, summary, ok));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int ParseInt(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return int.TryParse(s, out var v) ? v : 0;
        }
    }
}
