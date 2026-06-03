using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatStreamParserTests
    {
        // ── TextDelta ──────────────────────────────────────────────────────────

        [Test]
        public void ParseLine_TextDelta_ReturnsTextDeltaEvent()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.TextDelta, result.Value.Kind);
            Assert.AreEqual("Hello", result.Value.Text);
        }

        [Test]
        public void ParseLine_TextDeltaEmpty_ReturnsEmptyText()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.TextDelta, result.Value.Kind);
            Assert.AreEqual("", result.Value.Text);
        }

        // ── ToolStart (content_block_start tool_use) ───────────────────────────

        [Test]
        public void ParseLine_ContentBlockStartToolUse_ReturnsToolStart()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_start\",\"content_block\":{\"type\":\"tool_use\",\"name\":\"mcp__unity-mcp__get_hierarchy\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.ToolStart, result.Value.Kind);
            Assert.AreEqual("mcp__unity-mcp__get_hierarchy", result.Value.Text);
            Assert.AreEqual("", result.Value.ArgsJson);
        }

        // ── input_json_delta (partial args) ───────────────────────────────────

        [Test]
        public void ParseLine_InputJsonDelta_ReturnsToolStartWithPartialArgs()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_delta\",\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"path\\\"\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.ToolStart, result.Value.Kind);
            Assert.IsTrue(result.Value.ArgsJson.Contains("path"));
        }

        // ── result success ────────────────────────────────────────────────────

        [Test]
        public void ParseLine_ResultSuccess_ReturnsTurnDone()
        {
            const string line = "{\"type\":\"result\",\"is_error\":false,\"session_id\":\"sess-abc\",\"total_cost_usd\":0.002,\"usage\":{\"input_tokens\":100,\"output_tokens\":50}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.TurnDone, result.Value.Kind);
            Assert.AreEqual("sess-abc", result.Value.SessionId);
            Assert.AreEqual(100, result.Value.InputTokens);
            Assert.AreEqual(50,  result.Value.OutputTokens);
            Assert.IsTrue(result.Value.CostUsd > 0f);
            Assert.IsTrue(result.Value.IsOk);
        }

        // ── result error ──────────────────────────────────────────────────────

        [Test]
        public void ParseLine_ResultError_ReturnsError()
        {
            const string line = "{\"type\":\"result\",\"is_error\":true,\"error\":\"rate limited\"}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.Error, result.Value.Kind);
            Assert.IsTrue(result.Value.Text.Contains("rate limited"));
        }

        // ── system/init ───────────────────────────────────────────────────────

        [Test]
        public void ParseLine_SystemInit_ReturnsTurnDoneWithSessionId()
        {
            const string line = "{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"sess-xyz\",\"model\":\"claude-opus-4\"}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.TurnDone, result.Value.Kind);
            Assert.AreEqual("sess-xyz", result.Value.SessionId);
        }

        // ── system/api_retry ──────────────────────────────────────────────────

        [Test]
        public void ParseLine_SystemApiRetry_ReturnsError()
        {
            const string line = "{\"type\":\"system\",\"subtype\":\"api_retry\",\"error\":\"overloaded\"}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.Error, result.Value.Kind);
        }

        // ── silently ignored ──────────────────────────────────────────────────

        [Test]
        public void ParseLine_AssistantType_ReturnsNull()
        {
            var result = ChatStreamParser.ParseLine("{\"type\":\"assistant\",\"message\":{}}");
            Assert.IsNull(result);
        }

        [Test]
        public void ParseLine_UserType_ReturnsNull()
        {
            var result = ChatStreamParser.ParseLine("{\"type\":\"user\",\"message\":{}}");
            Assert.IsNull(result);
        }

        [Test]
        public void ParseLine_ContentBlockStop_ReturnsNull()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_stop\"}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNull(result);
        }

        [Test]
        public void ParseLine_MessageStart_ReturnsNull()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"message_start\",\"message\":{}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNull(result);
        }

        [Test]
        public void ParseLine_UnknownTopLevel_ReturnsNull()
        {
            var result = ChatStreamParser.ParseLine("{\"type\":\"future_type\",\"data\":{}}");
            Assert.IsNull(result);
        }

        // ── malformed / empty ─────────────────────────────────────────────────

        [Test]
        public void ParseLine_EmptyString_ReturnsNull()
        {
            Assert.IsNull(ChatStreamParser.ParseLine(""));
            Assert.IsNull(ChatStreamParser.ParseLine("   "));
            Assert.IsNull(ChatStreamParser.ParseLine(null));
        }

        [Test]
        public void ParseLine_MalformedJson_ReturnsErrorEvent()
        {
            var result = ChatStreamParser.ParseLine("{broken json{{{");
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.Error, result.Value.Kind);
        }
    }
}
