// TDD tests for Codex elicitation (mcpServer/elicitation/request) handling.
// Verified against codex 0.141.0 McpServerElicitationRequestResponse schema.
// Tests run RED first (before implementation), then GREEN after fix.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CodexElicitationTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        // mcpServer/elicitation/request with integer id (form mode)
        private const string S_ElicitationIntId =
            "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"mcpServer/elicitation/request\"," +
            "\"params\":{\"serverName\":\"unity_chat\",\"threadId\":\"t1\"," +
            "\"message\":\"Allow set_property?\",\"mode\":\"form\"," +
            "\"requestedSchema\":{\"type\":\"object\",\"properties\":{}}}}";

        // mcpServer/elicitation/request with string id
        private const string S_ElicitationStringId =
            "{\"jsonrpc\":\"2.0\",\"id\":\"abc-uuid\",\"method\":\"mcpServer/elicitation/request\"," +
            "\"params\":{\"serverName\":\"unity_chat\",\"threadId\":\"t1\"," +
            "\"message\":\"Allow?\",\"mode\":\"form\"," +
            "\"requestedSchema\":{\"type\":\"object\",\"properties\":{}}}}";

        // mcpServer/elicitation/request with url mode (second oneOf variant)
        private const string S_ElicitationUrlMode =
            "{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"mcpServer/elicitation/request\"," +
            "\"params\":{\"serverName\":\"unity_chat\",\"threadId\":\"t1\"," +
            "\"message\":\"Approve?\",\"mode\":\"url\",\"url\":\"https://example.com\"}}";

        // benign notification (no "id" field) — must be ignored silently
        private const string S_BenignNotification =
            "{\"method\":\"thread/status/changed\",\"params\":{\"status\":\"active\"}}";

        // shell approval — must NOT be auto-accepted (surface as Error)
        private const string S_ShellApproval =
            "{\"jsonrpc\":\"2.0\",\"id\":8,\"method\":\"item/commandExecution/requestApproval\"," +
            "\"params\":{\"command\":\"rm -rf /\",\"workingDirectory\":\"/tmp\"}}";

        // unknown server request with id (future-proofing)
        private const string S_UnknownRequest =
            "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"mcpServer/someNewRequest\"," +
            "\"params\":{\"serverName\":\"unity_chat\"}}";

        private static List<ChatEvent> Parse(string line)
        {
            var sink = new List<ChatEvent>();
            CodexAppServerParser.ParseLine(line, sink);
            return sink;
        }

        // ── Layer 2: parser auto-accepts elicitation ──────────────────────────

        [Test]
        public void ElicitationRequest_IntId_EmitsAutoReply()
        {
            var events = Parse(S_ElicitationIntId);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.AutoReply, events[0].Kind);
        }

        [Test]
        public void ElicitationRequest_IntId_ReplyContainsEchoedId()
        {
            var events = Parse(S_ElicitationIntId);
            Assert.AreEqual(1, events.Count);
            // id:5 must be unquoted integer in reply
            StringAssert.Contains("\"id\":5", events[0].Text);
        }

        [Test]
        public void ElicitationRequest_IntId_ReplyContainsAcceptAction()
        {
            var events = Parse(S_ElicitationIntId);
            StringAssert.Contains("\"action\":\"accept\"", events[0].Text);
        }

        [Test]
        public void ElicitationRequest_IntId_ReplyContainsContent()
        {
            var events = Parse(S_ElicitationIntId);
            StringAssert.Contains("\"content\":{}", events[0].Text);
        }

        [Test]
        public void ElicitationRequest_IntId_ReplyIsJsonRpc()
        {
            var events = Parse(S_ElicitationIntId);
            StringAssert.Contains("\"jsonrpc\":\"2.0\"", events[0].Text);
        }

        [Test]
        public void ElicitationRequest_StringId_EmitsAutoReply()
        {
            var events = Parse(S_ElicitationStringId);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.AutoReply, events[0].Kind);
        }

        [Test]
        public void ElicitationRequest_StringId_ReplyContainsQuotedId()
        {
            var events = Parse(S_ElicitationStringId);
            // "abc-uuid" must be quoted string in reply
            StringAssert.Contains("\"id\":\"abc-uuid\"", events[0].Text);
        }

        [Test]
        public void ElicitationRequest_UrlMode_EmitsAutoReply()
        {
            var events = Parse(S_ElicitationUrlMode);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.AutoReply, events[0].Kind);
        }

        // ── Layer 3: benign notifications silently ignored ────────────────────

        // Real codex 0.141.0 turn/started — params.turn.id is NESTED; no top-level id.
        // HasRpcId must use top-level-only check or this fires the unknown-request branch.
        private const string S_TurnStartedWithNestedId =
            "{\"method\":\"turn/started\",\"params\":{\"threadId\":\"019ea1d7-6cd3-7c53-bf6e-9713598e4d4d\"," +
            "\"turn\":{\"id\":\"turn-abc123\",\"items\":[],\"status\":\"inProgress\"}}}";

        // turn/completed also has params.turn.id nested
        private const string S_TurnCompletedWithNestedId =
            "{\"method\":\"turn/completed\",\"params\":{\"threadId\":\"019ea1d7\"," +
            "\"turn\":{\"id\":\"turn-abc123\",\"status\":\"completed\",\"error\":null,\"durationMs\":8121}}}";

        // item/started with nested item.id — notification, must be ignored if method not handled
        private const string S_ItemStartedUnknownTypeWithNestedId =
            "{\"method\":\"item/started\",\"params\":{\"item\":{\"type\":\"userMessage\"," +
            "\"id\":\"msg-001\"},\"threadId\":\"t1\",\"turnId\":\"turn1\"}}";

        [Test]
        public void TurnStarted_WithNestedId_EmitsNothing()
        {
            // REGRESSION: turn/started has params.turn.id nested — must NOT trigger unknown-request branch.
            var events = Parse(S_TurnStartedWithNestedId);
            Assert.AreEqual(0, events.Count, "turn/started with nested id must produce 0 events");
        }

        [Test]
        public void TurnStarted_WithNestedId_NoAutoReply()
        {
            var events = Parse(S_TurnStartedWithNestedId);
            foreach (var ev in events)
                Assert.AreNotEqual(ChatEventKind.AutoReply, ev.Kind, "turn/started must not produce AutoReply");
        }

        [Test]
        public void TurnStarted_WithNestedId_NoError()
        {
            var events = Parse(S_TurnStartedWithNestedId);
            foreach (var ev in events)
                Assert.AreNotEqual(ChatEventKind.Error, ev.Kind, "turn/started must not produce Error");
        }

        [Test]
        public void TurnCompleted_WithNestedId_StillEmitsTurnDone()
        {
            // turn/completed has explicit case — nested id must not break it (emits TurnDone, not Error+Decline).
            var events = Parse(S_TurnCompletedWithNestedId);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TurnDone, events[0].Kind);
        }

        [Test]
        public void ItemStarted_UnknownTypeWithNestedId_EmitsNothing()
        {
            // item/started with unknown item type (userMessage) and nested item.id — must be ignored.
            var events = Parse(S_ItemStartedUnknownTypeWithNestedId);
            Assert.AreEqual(0, events.Count, "userMessage item with nested id must produce 0 events");
        }

        [Test]
        public void UnknownRequest_WithNestedIdToo_DeclineUsesTopLevelId()
        {
            // True unknown request: top-level "id":99 AND nested params.turn.id.
            // Decline reply must echo top-level id=99, not the nested one.
            const string line =
                "{\"jsonrpc\":\"2.0\",\"id\":99,\"method\":\"foo/bar/request\"," +
                "\"params\":{\"turn\":{\"id\":\"turn-nested\"}}}";
            var events = Parse(line);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.Error,     events[0].Kind);
            Assert.AreEqual(ChatEventKind.AutoReply, events[1].Kind);
            StringAssert.Contains("\"id\":99",          events[1].Text, "must echo top-level id=99");
            StringAssert.DoesNotContain("turn-nested",  events[1].Text, "must NOT use nested params id");
        }

        [Test]
        public void BenignNotification_NoId_EmitsNothing()
        {
            var events = Parse(S_BenignNotification);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void KnownBenignMethods_Ignored()
        {
            var methods = new[]
            {
                "{\"method\":\"turn/started\",\"params\":{}}",
                "{\"method\":\"item/agentMessage/started\",\"params\":{}}",
                "{\"method\":\"mcpServer/status/updated\",\"params\":{}}",
                "{\"method\":\"serverRequest/resolved\",\"params\":{}}",
                "{\"method\":\"remoteControl/status/changed\",\"params\":{\"status\":\"disabled\"}}",
            };
            foreach (var line in methods)
            {
                var events = Parse(line);
                Assert.AreEqual(0, events.Count, $"Expected 0 events for: {line}");
            }
        }

        // ── Layer 3: shell approval surfaced, not auto-accepted ───────────────

        [Test]
        public void ShellApproval_Surfaced_NotAutoAccepted()
        {
            var events = Parse(S_ShellApproval);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.Error, events[0].Kind,
                "Shell approval must surface as Error, not be auto-accepted");
        }

        [Test]
        public void ShellApproval_NoAutoReplyEvent()
        {
            var events = Parse(S_ShellApproval);
            foreach (var ev in events)
                Assert.AreNotEqual(ChatEventKind.AutoReply, ev.Kind,
                    "Shell approval must NOT produce AutoReply");
        }

        // ── Layer 3: unknown server request surfaced and auto-declined ───────

        [Test]
        public void UnknownRequestWithId_SurfacesError_AndAutoDeclines()
        {
            // Unknown server requests (have "id") must surface an Error and auto-decline.
            // This unblocks codex without granting permission for an unknown request type.
            var events = Parse(S_UnknownRequest);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.Error,     events[0].Kind);
            Assert.AreEqual(ChatEventKind.AutoReply, events[1].Kind);
            StringAssert.Contains("\"action\":\"decline\"", events[1].Text);
        }

        // ── Regression: read-only tool path unaffected ────────────────────────

        [Test]
        public void McpToolCall_ReadOnly_NoAutoReply()
        {
            // get_hierarchy (read-only) must still produce ToolStart+ToolArgsComplete, no AutoReply
            const string line =
                "{\"method\":\"item/started\",\"params\":{\"item\":{\"type\":\"mcpToolCall\"," +
                "\"id\":\"call_abc123\",\"server\":\"unity\",\"tool\":\"get_hierarchy\",\"status\":\"inProgress\"," +
                "\"arguments\":{\"components\":false}},\"threadId\":\"t1\",\"turnId\":\"turn1\"}}";
            var events = Parse(line);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.ToolStart,        events[0].Kind);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, events[1].Kind);
            foreach (var ev in events)
                Assert.AreNotEqual(ChatEventKind.AutoReply, ev.Kind, "No AutoReply for normal tool calls");
        }
    }
}
