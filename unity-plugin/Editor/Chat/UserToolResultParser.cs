// Parses "user" NDJSON lines containing tool_result entries.
// Pure: zero UnityEngine deps.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal static class UserToolResultParser
    {
        // Single-event path — returns first tool_result or null.
        internal static ChatEvent? ParseFirst(string line)
        {
            var contentArray = GetContentArray(line);
            if (contentArray == "[]") return null;
            var firstObj = JsonHelper.ExtractFirstArrayObject(contentArray);
            if (firstObj == null) return null;
            if (JsonHelper.ExtractString(firstObj, "type") != "tool_result") return null;
            return ExtractEvent(firstObj);
        }

        // Multi-event path — emits one event per tool_result entry (FIX 1).
        internal static void ParseAll(string line, List<ChatEvent> sink)
        {
            var contentArray = GetContentArray(line);
            if (contentArray == "[]") return;
            int pos = 0;
            string obj;
            while ((obj = JsonArrayScan.ExtractNextObject(contentArray, ref pos)) != null)
            {
                if (JsonHelper.ExtractString(obj, "type") != "tool_result") continue;
                var ev = ExtractEvent(obj);
                if (ev.HasValue) sink.Add(ev.Value);
            }
        }

        private static string GetContentArray(string line)
        {
            var msg = JsonHelper.ExtractObject(line, "message");
            return JsonHelper.ExtractArray(msg, "content");
        }

        private static ChatEvent? ExtractEvent(string obj)
        {
            var toolUseId  = JsonHelper.ExtractString(obj, "tool_use_id") ?? "";
            var isErr      = JsonHelper.ExtractString(obj, "is_error") == "true";
            var resultText = JsonHelper.ExtractString(obj, "content");
            if (resultText == null)
            {
                var inner    = JsonHelper.ExtractArray(obj, "content");
                var innerObj = JsonHelper.ExtractFirstArrayObject(inner);
                if (innerObj != null)
                    resultText = JsonHelper.ExtractString(innerObj, "text") ?? "";
            }
            return ChatEvent.ToolResult(toolUseId, resultText ?? "", !isErr);
        }
    }
}
