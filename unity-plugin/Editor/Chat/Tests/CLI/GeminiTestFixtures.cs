// Shared NDJSON fixture strings for Gemini parser and related tests.
namespace UnityMCP.Editor.Chat.Tests
{
    internal static class GeminiTestFixtures
    {
        internal const string Init =
            "{\"type\":\"init\",\"session_id\":\"gemini-sess-abc\",\"model\":\"gemini-2.5-flash\",\"timestamp\":\"2026-01-01T00:00:00Z\"}";

        internal const string InitNoSession =
            "{\"type\":\"init\",\"model\":\"gemini-2.5-flash\",\"timestamp\":\"2026-01-01T00:00:00Z\"}";

        internal const string MessageDelta =
            "{\"type\":\"message\",\"role\":\"assistant\",\"content\":\"Hello world\",\"delta\":true,\"timestamp\":\"2026-01-01T00:00:01Z\"}";

        internal const string MessageEmpty =
            "{\"type\":\"message\",\"role\":\"assistant\",\"content\":\"\",\"delta\":true,\"timestamp\":\"2026-01-01T00:00:01Z\"}";

        internal const string ToolUse =
            "{\"type\":\"tool_use\",\"tool_name\":\"mcp_unity-mcp_batch\",\"tool_id\":\"tool-123\",\"parameters\":{\"ops\":[{\"cmd\":\"get_hierarchy\"}]},\"timestamp\":\"2026-01-01T00:00:02Z\"}";

        internal const string ToolResult =
            "{\"type\":\"tool_result\",\"tool_id\":\"tool-123\",\"status\":\"success\",\"output\":\"Main Camera\\nDirectional Light\",\"timestamp\":\"2026-01-01T00:00:03Z\"}";

        internal const string ToolResultError =
            "{\"type\":\"tool_result\",\"tool_id\":\"tool-456\",\"status\":\"error\",\"output\":\"tool not found\",\"timestamp\":\"2026-01-01T00:00:03Z\"}";

        internal const string Result =
            "{\"type\":\"result\",\"status\":\"success\",\"session_id\":\"gemini-sess-abc\",\"stats\":{\"total_tokens\":100,\"duration_ms\":1200,\"tool_calls\":1},\"timestamp\":\"2026-01-01T00:00:04Z\"}";

        internal const string Error =
            "{\"type\":\"error\",\"message\":\"API quota exceeded\",\"timestamp\":\"2026-01-01T00:00:05Z\"}";

        // Prompt echo: Gemini echoes user prompt as a message event with role:user
        internal const string MessageUserEcho =
            "{\"type\":\"message\",\"role\":\"user\",\"content\":\"Что на сцене?\",\"delta\":false,\"timestamp\":\"2026-01-01T00:00:00Z\"}";

        // Internal Gemini tool (no mcp_ prefix) — must be filtered out
        internal const string ToolUseInternal =
            "{\"type\":\"tool_use\",\"tool_name\":\"update_topic\",\"tool_id\":\"tool-internal-1\",\"parameters\":{\"topic\":\"scene query\"},\"timestamp\":\"2026-01-01T00:00:02Z\"}";

        // MCP ask_user tool — Gemini prefixes with mcp_{server}_ (hyphens become underscores)
        internal const string ToolUseAskUser =
            "{\"type\":\"tool_use\",\"tool_name\":\"mcp_unity_mcp_ask_user\",\"tool_id\":\"tool-ask-1\",\"parameters\":{\"prompt\":\"Continue?\",\"options\":[\"Yes\",\"No\"]},\"timestamp\":\"2026-01-01T00:00:02Z\"}";
    }
}
