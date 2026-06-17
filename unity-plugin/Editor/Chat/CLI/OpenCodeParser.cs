// Parses opencode --format json NDJSON lines into ChatEvents.
// Type-based dispatch. tool_use fires post-execution (input+output in same event).
// Pure logic: no UnityEngine deps, fully NUnit-testable.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class OpenCodeParser
    {
        public static void ParseLine(string line, List<ChatEvent> sink)
        {
            if (string.IsNullOrEmpty(line)) return;
            try { Dispatch(line, sink); }
            catch (Exception ex) { sink.Add(ChatEvent.Error("OpenCodeParser: " + ex.Message)); }
        }

        private static void Dispatch(string line, List<ChatEvent> sink)
        {
            var type = JsonHelper.ExtractString(line, "type");
            switch (type)
            {
                case "step_start":
                    var sid = JsonHelper.ExtractString(line, "sessionID");
                    if (!string.IsNullOrEmpty(sid)) sink.Add(ChatEvent.SessionInit(sid));
                    break;

                case "text":
                    var partText = JsonHelper.ExtractObject(line, "part");
                    var text     = JsonHelper.ExtractString(partText, "text") ?? "";
                    if (text.Length > 0) sink.Add(ChatEvent.TextDelta(text));
                    break;

                case "tool_use":
                    DispatchToolUse(line, sink);
                    break;

                case "step_finish":
                    DispatchStepFinish(line, sink);
                    break;

                case "error":
                    var errData = JsonHelper.ExtractObject(line, "error");
                    var errInner = JsonHelper.ExtractObject(errData, "data");
                    var msg = JsonHelper.ExtractString(errInner, "message") ?? "opencode error";
                    sink.Add(ChatEvent.Error(msg));
                    break;
            }
            // unknown types → ignored (forward-compat)
        }

        private static void DispatchToolUse(string line, List<ChatEvent> sink)
        {
            var part   = JsonHelper.ExtractObject(line, "part");
            var state  = JsonHelper.ExtractObject(part, "state");
            var status = JsonHelper.ExtractString(state, "status") ?? "";

            // C8: opencode emits pending→running→completed per tool; only emit events on terminal states
            if (status != "completed" && status != "error") return;

            var tool   = JsonHelper.ExtractString(part, "tool") ?? "";
            var callId = JsonHelper.ExtractString(part, "callID") ?? "";
            var input  = JsonHelper.ExtractObject(state, "input");
            var output = JsonHelper.ExtractString(state, "output") ?? "";

            var argsJson = (input == null || input == "{}") ? "{}" : input;

            sink.Add(ChatEvent.ToolStart(tool, argsJson, callId));
            sink.Add(ChatEvent.ToolArgsComplete());
            sink.Add(ChatEvent.ToolResult(callId, output, ok: status != "error"));
        }

        private static void DispatchStepFinish(string line, List<ChatEvent> sink)
        {
            var part   = JsonHelper.ExtractObject(line, "part");
            var reason = JsonHelper.ExtractString(part, "reason") ?? "";
            if (reason != "stop") return;

            var sid  = JsonHelper.ExtractString(line, "sessionID") ?? "";
            // C7: ExtractFloat handles both numeric literals and quoted strings
            var cost = JsonHelper.ExtractFloat(part, "cost");

            var tokens    = JsonHelper.ExtractObject(part, "tokens");
            var inputStr  = JsonHelper.ExtractString(tokens, "input")  ?? "0";
            var outputStr = JsonHelper.ExtractString(tokens, "output") ?? "0";
            int.TryParse(inputStr,  out var inputTok);
            int.TryParse(outputStr, out var outputTok);

            sink.Add(ChatEvent.TurnDone(sid, cost, inputTok, outputTok));
        }
    }
}
