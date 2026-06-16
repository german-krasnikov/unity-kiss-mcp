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
            // ChatProcess exit envelope: {"type":"result","is_error":true,"error":"..."}
            if (line.Contains("\"is_error\":true"))
            {
                var errMsg = JsonHelper.ExtractString(line, "error") ?? "Process error";
                sink.Add(ChatEvent.Error(errMsg));
                return;
            }

            var method = JsonHelper.ExtractString(line, "method");
            if (method != null) { DispatchNotification(method, line, sink); return; }

            // JSON-RPC error response: {"error":{"code":...,"message":"..."},"id":...}
            var rpcErr = JsonHelper.ExtractObject(line, "error");
            if (rpcErr != "{}")
            {
                var errMsg = JsonHelper.ExtractString(rpcErr, "message") ?? "JSON-RPC error";
                sink.Add(ChatEvent.Error(errMsg));
                return;
            }

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

                case "error":
                {
                    var errObj = JsonHelper.ExtractObject(p, "error");
                    var errMsg = JsonHelper.ExtractString(errObj, "message") ?? "Codex error";
                    sink.Add(ChatEvent.Error(errMsg));
                    break;
                }

                case "turn/completed":
                {
                    var turn       = JsonHelper.ExtractObject(p, "turn");
                    var turnStatus = JsonHelper.ExtractString(turn, "status");
                    if (turnStatus == "failed")
                    {
                        var turnErr    = JsonHelper.ExtractObject(turn, "error");
                        var turnErrMsg = JsonHelper.ExtractString(turnErr, "message") ?? "Turn failed";
                        sink.Add(ChatEvent.Error(turnErrMsg));
                    }
                    sink.Add(ChatEvent.TurnDone(null, 0f, 0, 0));
                    break;
                }

                case "thread/started":
                    var thread = JsonHelper.ExtractObject(p, "thread");
                    var tid = JsonHelper.ExtractString(thread, "id");
                    if (!string.IsNullOrEmpty(tid)) sink.Add(ChatEvent.SessionInit(tid));
                    break;

                case "tool/requestUserInput":
                case "item/tool/requestUserInput":
                {
                    // id is a JSON number — ExtractString handles unquoted scalars
                    var rpcId = JsonHelper.ExtractString(line, "id") ?? "0";
                    var questions = JsonHelper.ExtractArray(p, "questions");
                    if (!string.IsNullOrEmpty(questions) && questions != "[]")
                        sink.Add(ChatEvent.AskUser("codex:" + rpcId, "{\"questions\":" + questions + "}"));
                    break;
                }

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
                case "reasoning":
                    // Reasoning tokens are silent (not rendered) but prove the process is alive.
                    // Emit Heartbeat so the inactivity watchdog resets without affecting UI.
                    sink.Add(ChatEvent.Heartbeat());
                    break;
                // agentMessage, userMessage, etc. — ignore
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
            var status    = JsonHelper.ExtractString(item, "status");
            var resultObj = JsonHelper.ExtractObject(item, "result");
            // Codex sets status:"completed" even when the MCP tool returned an error.
            // The real indicator is result.isError:true (no space in Codex JSON).
            var ok        = status == "completed" && !resultObj.Contains("\"isError\":true");
            string resultText;
            if (status == "completed")
            {
                // Extract text from result.content[0].text regardless of isError flag.
                var content = JsonHelper.ExtractArray(resultObj, "content");
                var first   = JsonHelper.ExtractFirstArrayObject(content);
                resultText  = (first != null ? JsonHelper.ExtractString(first, "text") : null) ?? "";
                if (!ok && string.IsNullOrEmpty(resultText))
                    resultText = "[MCP tool error]";
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
