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
        // rpcId has "codex:" prefix (e.g. "codex:42") — strips prefix then formats id.
        public static string CodexUserInputResponse(string codexRequestId, string answersArrayJson)
        {
            var raw = codexRequestId?.StartsWith("codex:") == true ? codexRequestId.Substring(6) : "0";
            return $"{{\"jsonrpc\":\"2.0\",\"id\":{FormatRpcId(raw)},\"result\":{{\"answers\":{answersArrayJson}}}}}";
        }

        // JSON-RPC 2.0 response accepting a mcpServer/elicitation/request.
        // rpcId is the raw "id" from the server request line (int or string, no "codex:" prefix).
        // Shape verified against codex 0.141.0 McpServerElicitationRequestResponse schema:
        //   {"action":"accept"|"decline"|"cancel","content":{...}}
        public static string CodexElicitationAccept(string rpcId) =>
            $"{{\"jsonrpc\":\"2.0\",\"id\":{FormatRpcId(rpcId)},\"result\":{{\"action\":\"accept\",\"content\":{{}}}}}}";

        // JSON-RPC 2.0 decline for unknown/unsupported server requests — unblocks codex without granting permission.
        // rpcId has "codex:" prefix (e.g. "codex:7") — stripped before formatting, same as CodexUserInputResponse.
        public static string CodexElicitationDecline(string rpcId)
        {
            var raw = rpcId?.StartsWith("codex:") == true ? rpcId.Substring(6) : rpcId ?? "0";
            return $"{{\"jsonrpc\":\"2.0\",\"id\":{FormatRpcId(raw)},\"result\":{{\"action\":\"decline\"}}}}";
        }

        // Shared: format a JSON-RPC id — int-looking values go unquoted, others quoted+escaped.
        private static string FormatRpcId(string raw)
        {
            var s = raw ?? "0";
            return int.TryParse(s, out _) ? s : $"\"{UnityMCP.Editor.JsonHelper.EscapeJson(s)}\"";
        }

        private static string BuildResponse(string requestId, string responseJson)
        {
            var safeId = UnityMCP.Editor.JsonHelper.EscapeJson(requestId ?? "");
            return $"{{\"type\":\"control_response\",\"response\":{{\"subtype\":\"success\",\"request_id\":\"{safeId}\",\"response\":{responseJson}}}}}";
        }
    }
}
