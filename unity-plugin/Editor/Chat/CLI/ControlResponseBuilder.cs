// Builds JSON control_response messages sent to claude CLI stdin.
// Pure static — no UnityEngine deps, fully NUnit-testable.
namespace UnityMCP.Editor.Chat
{
    internal static class ControlResponseBuilder
    {
        public static string Allow(string requestId) =>
            BuildResponse(requestId, "{\"behavior\":\"allow\"}");

        public static string Deny(string requestId, string message = null)
        {
            var reasonJson = string.IsNullOrEmpty(message)
                ? ""
                : $",\"reason\":\"{UnityMCP.Editor.JsonHelper.EscapeJson(message)}\"";
            return BuildResponse(requestId, $"{{\"behavior\":\"deny\"{reasonJson}}}");
        }

        public static string InitializeRequest(string requestId = null)
        {
            var id = requestId ?? System.Guid.NewGuid().ToString("N").Substring(0, 8);
            var safeId = UnityMCP.Editor.JsonHelper.EscapeJson(id);
            return $"{{\"type\":\"control_request\",\"request_id\":\"{safeId}\",\"request\":{{\"subtype\":\"initialize\"}}}}";
        }

        // SAFETY: resultsJson must be pre-constructed valid JSON (caller escapes individual values).
        public static string Elicitation(string requestId, string resultsJson)
        {
            var safeId = UnityMCP.Editor.JsonHelper.EscapeJson(requestId ?? "");
            return BuildResponse(requestId, $"{{\"elicitation_id\":\"{safeId}\",\"results\":{resultsJson}}}");
        }

        public static string ElicitationHook(string requestId, string questionsJson, string answersJson = null, string freeResponse = null)
        {
            string updatedInput;
            if (!string.IsNullOrEmpty(freeResponse))
            {
                var safeResp = UnityMCP.Editor.JsonHelper.EscapeJson(freeResponse);
                updatedInput = $"{{\"questions\":{questionsJson},\"response\":\"{safeResp}\"}}";
            }
            else
            {
                updatedInput = $"{{\"questions\":{questionsJson},\"answers\":{answersJson ?? "{}"}}}";
            }
            return BuildResponse(requestId, $"{{\"behavior\":\"allow\",\"updatedInput\":{updatedInput}}}");
        }

        public static string Interrupt() =>
            "{\"type\":\"control_request\",\"request\":{\"subtype\":\"interrupt\"}}";

        // JSON-RPC 2.0 response for Codex tool/requestUserInput.
        // rpcId is a raw number — embedded unquoted in JSON.
        public static string CodexUserInputResponse(string codexRequestId, string answersArrayJson)
        {
            var raw = codexRequestId?.StartsWith("codex:") == true ? codexRequestId.Substring(6) : "0";
            var idJson = int.TryParse(raw, out _) ? raw : $"\"{UnityMCP.Editor.JsonHelper.EscapeJson(raw)}\"";
            return $"{{\"jsonrpc\":\"2.0\",\"id\":{idJson},\"result\":{{\"answers\":{answersArrayJson}}}}}";
        }

        private static string BuildResponse(string requestId, string responseJson)
        {
            var safeId = UnityMCP.Editor.JsonHelper.EscapeJson(requestId ?? "");
            return $"{{\"type\":\"control_response\",\"response\":{{\"subtype\":\"success\",\"request_id\":\"{safeId}\",\"response\":{responseJson}}}}}";
        }
    }
}
