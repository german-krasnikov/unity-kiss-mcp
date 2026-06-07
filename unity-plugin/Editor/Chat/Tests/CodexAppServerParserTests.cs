// TDD tests for CodexAppServerParser.
// Fixtures use real JSON from spike5 (2026-06-07, codex 0.137.0).
// Scrubbed: user paths replaced per NDA/public-repo policy.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CodexAppServerParserTests
    {
        // ── Real spike fixtures (scrubbed) ────────────────────────────────────

        // thread/start response — id at result.thread.id
        private const string S_ThreadStartResponse =
            "{\"id\":2,\"result\":{\"thread\":{\"id\":\"019ea1d7-6cd3-7c53-bf6e-9713598e4d4d\",\"sessionId\":\"019ea1d7-6cd3-7c53-bf6e-9713598e4d4d\"},\"model\":\"gpt-5.4-mini\"}}";

        // thread/started notification — id at params.thread.id
        private const string S_ThreadStartedNotif =
            "{\"method\":\"thread/started\",\"params\":{\"thread\":{\"id\":\"019ea1d7-6cd3-7c53-bf6e-9713598e4d4d\",\"sessionId\":\"019ea1d7-6cd3-7c53-bf6e-9713598e4d4d\",\"preview\":\"\"}}}";

        // item/agentMessage/delta notification
        private const string S_Delta =
            "{\"method\":\"item/agentMessage/delta\",\"params\":{\"threadId\":\"019ea1d7\",\"turnId\":\"turn1\",\"itemId\":\"msg_1\",\"delta\":\"I'm querying\"}}";

        // item/started mcpToolCall (camelCase type!)
        private const string S_McpToolStarted =
            "{\"method\":\"item/started\",\"params\":{\"item\":{\"type\":\"mcpToolCall\",\"id\":\"call_abc123\",\"server\":\"unity\",\"tool\":\"get_hierarchy\",\"status\":\"inProgress\",\"arguments\":{\"components\":false,\"compress\":true,\"summary\":false}},\"threadId\":\"019ea1d7\",\"turnId\":\"turn1\"}}";

        // item/completed mcpToolCall with result
        private const string S_McpToolCompleted =
            "{\"method\":\"item/completed\",\"params\":{\"item\":{\"type\":\"mcpToolCall\",\"id\":\"call_abc123\",\"server\":\"unity\",\"tool\":\"get_hierarchy\",\"status\":\"completed\",\"arguments\":{},\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"Main Camera $a\\nGridFloor $b\"}]},\"error\":null,\"durationMs\":405},\"threadId\":\"019ea1d7\",\"turnId\":\"turn1\"}}";

        // item/completed mcpToolCall with error
        private const string S_McpToolFailed =
            "{\"method\":\"item/completed\",\"params\":{\"item\":{\"type\":\"mcpToolCall\",\"id\":\"call_xyz\",\"server\":\"unity\",\"tool\":\"get_hierarchy\",\"status\":\"failed\",\"result\":null,\"error\":{\"message\":\"tool not found\"}},\"threadId\":\"t1\",\"turnId\":\"tu1\"}}";

        // turn/completed — no usage in spike output
        private const string S_TurnCompleted =
            "{\"method\":\"turn/completed\",\"params\":{\"threadId\":\"019ea1d7\",\"turn\":{\"id\":\"turn1\",\"status\":\"completed\",\"error\":null,\"durationMs\":8121}}}";

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<ChatEvent> Parse(string line)
        {
            var sink = new List<ChatEvent>();
            CodexAppServerParser.ParseLine(line, sink);
            return sink;
        }

        // ── thread/start response (result.thread.id) ──────────────────────────

        [Test]
        public void ThreadStartResponse_EmitsSessionInit()
        {
            var events = Parse(S_ThreadStartResponse);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.SessionInit, events[0].Kind);
            Assert.AreEqual("019ea1d7-6cd3-7c53-bf6e-9713598e4d4d", events[0].SessionId);
        }

        // ── thread/started notification (params.thread.id) ────────────────────

        [Test]
        public void ThreadStartedNotification_EmitsSessionInit()
        {
            var events = Parse(S_ThreadStartedNotif);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.SessionInit, events[0].Kind);
            Assert.AreEqual("019ea1d7-6cd3-7c53-bf6e-9713598e4d4d", events[0].SessionId);
        }

        // ── item/agentMessage/delta ───────────────────────────────────────────

        [Test]
        public void AgentMessageDelta_EmitsTextDelta()
        {
            var events = Parse(S_Delta);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, events[0].Kind);
            Assert.AreEqual("I'm querying", events[0].Text);
        }

        [Test]
        public void AgentMessageDelta_Empty_EmitsNothing()
        {
            const string line = "{\"method\":\"item/agentMessage/delta\",\"params\":{\"delta\":\"\"}}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count);
        }

        // ── item/started mcpToolCall ──────────────────────────────────────────

        [Test]
        public void ItemStarted_McpToolCall_EmitsToolStartAndArgsComplete()
        {
            var events = Parse(S_McpToolStarted);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.ToolStart,        events[0].Kind);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, events[1].Kind);
            Assert.AreEqual("unity:get_hierarchy",          events[0].Text);
        }

        [Test]
        public void ItemStarted_McpToolCall_ArgsIsJsonObject()
        {
            var events = Parse(S_McpToolStarted);
            Assert.IsTrue(events[0].ArgsJson.StartsWith("{"), "ArgsJson must be object");
            StringAssert.Contains("compress", events[0].ArgsJson);
        }

        // ── item/completed mcpToolCall ────────────────────────────────────────

        [Test]
        public void ItemCompleted_McpToolCall_EmitsToolResult()
        {
            var events = Parse(S_McpToolCompleted);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsTrue(events[0].IsOk);
            StringAssert.Contains("Main Camera", events[0].Text);
        }

        [Test]
        public void ItemCompleted_McpToolCall_Failed_EmitsToolResultNotOk()
        {
            var events = Parse(S_McpToolFailed);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsFalse(events[0].IsOk);
            StringAssert.Contains("tool not found", events[0].Text);
        }

        // ── turn/completed ────────────────────────────────────────────────────

        [Test]
        public void TurnCompleted_EmitsTurnDone()
        {
            var events = Parse(S_TurnCompleted);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TurnDone, events[0].Kind);
            Assert.AreEqual(0f, events[0].CostUsd);
        }

        // ── edge cases ────────────────────────────────────────────────────────

        [Test]
        public void NullLine_EmitsNothing()
        {
            var events = Parse(null);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void EmptyLine_EmitsNothing()
        {
            var events = Parse("");
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void UnknownMethod_Ignored()
        {
            var events = Parse("{\"method\":\"remoteControl/status/changed\",\"params\":{\"status\":\"disabled\"}}");
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void MalformedJson_EmitsError()
        {
            var events = Parse("{broken");
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.Error, events[0].Kind);
        }

        [Test]
        public void InitializeResponse_Ignored()
        {
            // id:1 response has no thread — should produce nothing
            const string line = "{\"id\":1,\"result\":{\"userAgent\":\"spike/0.137.0\"}}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void UnknownItemType_Ignored()
        {
            const string line = "{\"method\":\"item/started\",\"params\":{\"item\":{\"type\":\"reasoning\",\"id\":\"rs_1\"}}}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count);
        }
    }
}
