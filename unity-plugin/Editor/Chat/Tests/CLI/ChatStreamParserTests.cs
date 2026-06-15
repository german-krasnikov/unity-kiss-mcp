using System.Collections.Generic;
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
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_start\",\"content_block\":{\"type\":\"tool_use\",\"name\":\"mcp__unity__get_hierarchy\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.ToolStart, result.Value.Kind);
            Assert.AreEqual("mcp__unity__get_hierarchy", result.Value.Text);
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
        public void ParseLine_SystemInit_ReturnsSessionInitNotTurnDone()
        {
            // system/init must NOT produce TurnDone — that would stop the activity animation
            // before any real work has happened. It maps to SessionInit instead.
            const string line = "{\"type\":\"system\",\"subtype\":\"init\",\"session_id\":\"abc-123\",\"model\":\"claude\",\"tools\":[],\"cwd\":\"/x\"}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.SessionInit, result.Value.Kind);
            Assert.AreEqual("abc-123", result.Value.SessionId);
            Assert.IsTrue(result.Value.IsOk);
        }

        [Test]
        public void ParseLine_ResultSuccess_StillReturnsTurnDone_WithCostAndTokens()
        {
            // The genuine terminal event — must remain TurnDone.
            const string line = "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"session_id\":\"abc-123\",\"total_cost_usd\":0.012,\"usage\":{\"input_tokens\":100,\"output_tokens\":50}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.TurnDone, result.Value.Kind);
            Assert.AreEqual("abc-123", result.Value.SessionId);
            Assert.AreEqual(100, result.Value.InputTokens);
            Assert.AreEqual(50,  result.Value.OutputTokens);
            Assert.IsTrue(result.Value.CostUsd > 0f);
            Assert.IsTrue(result.Value.IsOk);
        }

        [Test]
        public void ChatEvent_SessionInit_Factory_SetsKindAndSessionId()
        {
            var ev = ChatEvent.SessionInit("abc-123");
            Assert.AreEqual(ChatEventKind.SessionInit, ev.Kind);
            Assert.AreEqual("abc-123", ev.SessionId);
            Assert.IsTrue(ev.IsOk);
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
        public void ParseLine_UserType_EmptyContent_ReturnsNull()
        {
            var result = ChatStreamParser.ParseLine("{\"type\":\"user\",\"message\":{\"content\":[]}}");
            Assert.IsNull(result);
        }

        [Test]
        public void ParseLine_ContentBlockStop_ReturnsToolArgsComplete()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_stop\"}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, result.Value.Kind);
        }

        // ── ToolId extraction ─────────────────────────────────────────────────

        [Test]
        public void ParseLine_ContentBlockStartToolUse_ExtractsId()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_start\",\"content_block\":{\"type\":\"tool_use\",\"id\":\"toolu_abc\",\"name\":\"get_hierarchy\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual("toolu_abc", result.Value.ToolId);
        }

        // ── tool_result (user message) ────────────────────────────────────────

        [Test]
        public void ParseLine_UserToolResult_ReturnsToolResult()
        {
            const string line = "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"toolu_xyz\",\"content\":\"Root/Cube\"}]}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.ToolResult, result.Value.Kind);
            Assert.AreEqual("toolu_xyz", result.Value.ToolId);
            Assert.AreEqual("Root/Cube", result.Value.Text);
            Assert.IsTrue(result.Value.IsOk);
        }

        [Test]
        public void ParseLine_UserToolResult_Error_SetsIsOkFalse()
        {
            const string line = "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"toolu_err\",\"is_error\":true,\"content\":\"boom\"}]}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.ToolResult, result.Value.Kind);
            Assert.IsFalse(result.Value.IsOk);
        }

        [Test]
        public void ParseLine_UserToolResult_NestedContentArray_ExtractsText()
        {
            const string line = "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"toolu_n\",\"content\":[{\"type\":\"text\",\"text\":\"hello nested\"}]}]}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual("hello nested", result.Value.Text);
        }

        [Test]
        public void ParseLine_UserToolResult_StringContent_ExtractsDirectly()
        {
            const string line = "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"toolu_s\",\"content\":\"Main Camera [Camera]\"}]}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual("Main Camera [Camera]", result.Value.Text);
        }

        [Test]
        public void ParseLine_InputJsonDelta_CarriesNullName()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_delta\",\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"path\\\"\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.ToolStart, result.Value.Kind);
            Assert.IsNull(result.Value.Text);  // name is null for delta events
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

        // ── ParseInto: multi tool_result (FIX 1) ──────────────────────────────

        [Test]
        public void ParseInto_UserWithTwoToolResults_EmitsTwoEvents()
        {
            // A "user" NDJSON line with two tool_result entries in the content array.
            const string line =
                "{\"type\":\"user\",\"message\":{\"content\":[" +
                "{\"type\":\"tool_result\",\"tool_use_id\":\"id_a\",\"content\":\"result_a\"}," +
                "{\"type\":\"tool_result\",\"tool_use_id\":\"id_b\",\"is_error\":true,\"content\":\"result_b\"}" +
                "]}}";

            var sink = new List<ChatEvent>();
            ChatStreamParser.ParseInto(line, sink);

            Assert.AreEqual(2, sink.Count, "Must emit one event per tool_result entry");

            Assert.AreEqual(ChatEventKind.ToolResult, sink[0].Kind);
            Assert.AreEqual("id_a",     sink[0].ToolId);
            Assert.AreEqual("result_a", sink[0].Text);
            Assert.IsTrue(sink[0].IsOk);

            Assert.AreEqual(ChatEventKind.ToolResult, sink[1].Kind);
            Assert.AreEqual("id_b",     sink[1].ToolId);
            Assert.AreEqual("result_b", sink[1].Text);
            Assert.IsFalse(sink[1].IsOk);
        }

        [Test]
        public void ParseInto_NonUserLine_EmitsSingleEventLikeParseLine()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_stop\"}}";
            var sink = new List<ChatEvent>();
            ChatStreamParser.ParseInto(line, sink);
            Assert.AreEqual(1, sink.Count);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, sink[0].Kind);
        }

        [Test]
        public void ParseInto_UserSingleToolResult_EmitsOneEvent()
        {
            const string line = "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"toolu_xyz\",\"content\":\"Root/Cube\"}]}}";
            var sink = new List<ChatEvent>();
            ChatStreamParser.ParseInto(line, sink);
            Assert.AreEqual(1, sink.Count);
            Assert.AreEqual("toolu_xyz", sink[0].ToolId);
        }

        // ── CH2.test.2: ParseContentBlockStart returns null for non-tool_use blocks ──

        [Test]
        public void ParseLine_ContentBlockStart_TextType_ReturnsNull()
        {
            // type='text' content_block_start must return null (not a ToolStart)
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_start\",\"content_block\":{\"type\":\"text\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNull(result, "content_block_start with type='text' must return null");
        }

        [Test]
        public void ParseLine_ContentBlockStart_UnknownType_ReturnsNull()
        {
            const string line = "{\"type\":\"stream_event\",\"event\":{\"type\":\"content_block_start\",\"content_block\":{\"type\":\"thinking\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNull(result, "content_block_start with unknown type must return null");
        }

        [Test]
        public void ParseLine_SystemInitNoSubtype_ReturnsNull()
        {
            // system event with unknown subtype must return null (not SessionInit)
            const string line = "{\"type\":\"system\",\"subtype\":\"unknown_event\",\"data\":{}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNull(result, "system event with unknown subtype must return null");
        }

        // ── CH2.test.3: ParseAll skips non-tool_result items in mixed array ──────

        [Test]
        public void ParseAll_MixedContentArray_SkipsNonToolResult()
        {
            // Content array has text item first, then a tool_result — only the latter must emit
            const string line =
                "{\"type\":\"user\",\"message\":{\"content\":[" +
                "{\"type\":\"text\",\"text\":\"some context\"}," +
                "{\"type\":\"tool_result\",\"tool_use_id\":\"id_x\",\"content\":\"output\"}" +
                "]}}";

            var sink = new List<ChatEvent>();
            ChatStreamParser.ParseInto(line, sink);

            Assert.AreEqual(1, sink.Count, "must emit only the tool_result item, skipping text");
            Assert.AreEqual(ChatEventKind.ToolResult, sink[0].Kind);
            Assert.AreEqual("id_x", sink[0].ToolId);
        }

        [Test]
        public void ParseAll_ArrayWithNoToolResult_EmitsNothing()
        {
            const string line =
                "{\"type\":\"user\",\"message\":{\"content\":[" +
                "{\"type\":\"text\",\"text\":\"just text\"}," +
                "{\"type\":\"image\",\"source\":{}}" +
                "]}}";

            var sink = new List<ChatEvent>();
            ChatStreamParser.ParseInto(line, sink);

            Assert.AreEqual(0, sink.Count, "no tool_result → must emit nothing");
        }

        // ── string content starting with '[' must NOT be parsed as array ─────────

        [Test]
        public void ParseLine_UserToolResult_StringContentStartingWithBracket_ExtractsString()
        {
            // content is a quoted string that starts with '[' — must return the full string,
            // not fall through to ExtractArray which would silently return empty.
            const string line = "{\"type\":\"user\",\"message\":{\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"toolu_br\",\"content\":\"[Player, Enemy]\"}]}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.ToolResult, result.Value.Kind);
            Assert.AreEqual("[Player, Enemy]", result.Value.Text,
                "String content starting with '[' must be returned as-is, not parsed as array");
        }

        // ── sdk_control_request ────────────────────────────────────────────────

        [Test]
        public void ParseLine_SdkControlRequest_Permission_ReturnsPermissionPrompt()
        {
            const string line = "{\"type\":\"sdk_control_request\",\"request\":{\"subtype\":\"permission\",\"request_id\":\"req-1\",\"tool_name\":\"bash\",\"tool_input\":{\"cmd\":\"rm -rf /\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.PermissionPrompt, result.Value.Kind);
            Assert.AreEqual("req-1", result.Value.RequestId);
            Assert.AreEqual("bash",  result.Value.Text);
        }

        [Test]
        public void ParseLine_SdkControlRequest_Elicitation_ReturnsAskUser()
        {
            const string line = "{\"type\":\"sdk_control_request\",\"request\":{\"subtype\":\"elicitation\",\"request_id\":\"req-2\",\"elicitation\":{\"questions\":[]}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.AskUser, result.Value.Kind);
            Assert.AreEqual("req-2", result.Value.RequestId);
        }

        [Test]
        public void ParseLine_SdkControlRequest_McpMessage_ReturnsNull()
        {
            const string line = "{\"type\":\"sdk_control_request\",\"request\":{\"subtype\":\"mcp_message\"}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNull(result);
        }

        // ── control_request (new protocol v2.1.177) ───────────────────────────

        [Test]
        public void ParseLine_ControlRequest_HookCallback_ReturnsPermissionPrompt()
        {
            const string line = "{\"type\":\"control_request\",\"request_id\":\"e3b315d2\",\"request\":{\"subtype\":\"hook_callback\",\"callback_id\":\"hook_0\",\"input\":{\"hook_event_name\":\"PreToolUse\",\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"echo test\"}},\"tool_use_id\":\"toolu_01\"}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.PermissionPrompt, result.Value.Kind);
            Assert.AreEqual("e3b315d2", result.Value.RequestId);
            Assert.AreEqual("Bash", result.Value.Text);
        }

        [Test]
        public void ParseLine_ControlRequest_HookCallback_AskUserQuestion_ReturnsAskUser()
        {
            const string line = "{\"type\":\"control_request\",\"request_id\":\"ask-1\",\"request\":{\"subtype\":\"hook_callback\",\"callback_id\":\"hook_0\",\"input\":{\"hook_event_name\":\"PreToolUse\",\"tool_name\":\"AskUserQuestion\",\"tool_input\":{\"questions\":[{\"question\":\"What?\",\"options\":[{\"label\":\"A\"},{\"label\":\"B\"}]}]}}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.AskUser, result.Value.Kind);
            Assert.AreEqual("ask-1", result.Value.RequestId);
        }

        [Test]
        public void ParseLine_ControlResponse_ReturnsNull()
        {
            const string line = "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\"}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNull(result);
        }

        [Test]
        public void ParseLine_ToolProgress_ReturnsToolProgressEvent()
        {
            const string line = "{\"type\":\"tool_progress\",\"percentage\":50,\"message\":\"halfway\"}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.ToolProgress, result.Value.Kind);
            Assert.AreEqual(50f, result.Value.Percentage, 0.01f);
            Assert.AreEqual("halfway", result.Value.Text);
        }

        [Test]
        public void ParseLine_RateLimitEvent_ReturnsRateLimitEvent()
        {
            const string line = "{\"type\":\"rate_limit_event\",\"message\":\"slow down\"}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.RateLimit, result.Value.Kind);
            Assert.AreEqual("slow down", result.Value.Text);
        }

        [Test]
        public void ParseLine_SessionStateChanged_ReturnsSessionStateEvent()
        {
            const string line = "{\"type\":\"session_state_changed\",\"state\":\"paused\"}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.SessionState, result.Value.Kind);
            Assert.AreEqual("paused", result.Value.State);
        }

        // ── can_use_tool (Agent SDK protocol) ────────────────────────────────────

        [Test]
        public void ParseLine_ControlRequest_CanUseTool_ReturnsPermissionPrompt()
        {
            const string line = "{\"type\":\"control_request\",\"request\":{\"subtype\":\"can_use_tool\",\"request_id\":\"req-sdk\",\"tool_name\":\"Bash\",\"input\":{\"command\":\"echo test\"}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.PermissionPrompt, result.Value.Kind);
            Assert.AreEqual("req-sdk", result.Value.RequestId);
            Assert.AreEqual("Bash", result.Value.Text);
        }

        [Test]
        public void ParseLine_ControlRequest_CanUseTool_AskUserQuestion_ReturnsAskUser()
        {
            const string line = "{\"type\":\"control_request\",\"request\":{\"subtype\":\"can_use_tool\",\"request_id\":\"ask-sdk\",\"tool_name\":\"AskUserQuestion\",\"input\":{\"questions\":[{\"question\":\"Pick one\",\"options\":[{\"label\":\"A\"},{\"label\":\"B\"}]}]}}}";
            var result = ChatStreamParser.ParseLine(line);
            Assert.IsNotNull(result);
            Assert.AreEqual(ChatEventKind.AskUser, result.Value.Kind);
            Assert.AreEqual("ask-sdk", result.Value.RequestId);
        }
    }
}
