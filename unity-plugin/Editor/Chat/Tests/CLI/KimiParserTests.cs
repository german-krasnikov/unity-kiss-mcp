// TDD tests for KimiParser.
// Role-based NDJSON dispatch — role:assistant (text/tool_calls), role:tool, role:user (echo).
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class KimiParserTests
    {
        private static List<ChatEvent> Parse(string line)
        {
            var sink = new List<ChatEvent>();
            KimiParser.ParseLine(line, sink);
            return sink;
        }

        // ── role:assistant / text ─────────────────────────────────────────────

        [Test]
        public void Assistant_WithContent_EmitsTextDelta()
        {
            var events = Parse("{\"role\":\"assistant\",\"content\":\"Hello world\"}");
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, events[0].Kind);
            Assert.AreEqual("Hello world", events[0].Text);
        }

        [Test]
        public void Assistant_EmptyContent_EmitsNothing()
        {
            var events = Parse("{\"role\":\"assistant\",\"content\":\"\"}");
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Assistant_NoContentField_EmitsNothing()
        {
            var events = Parse("{\"role\":\"assistant\"}");
            Assert.AreEqual(0, events.Count);
        }

        // ── role:assistant / tool_calls ───────────────────────────────────────

        [Test]
        public void Assistant_McpToolCall_EmitsToolStartAndArgsComplete()
        {
            var line = "{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"tc-1\",\"type\":\"function\",\"function\":{\"name\":\"mcp_unity-mcp_get_hierarchy\",\"arguments\":\"{}\"}}]}";
            var events = Parse(line);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.ToolStart,        events[0].Kind);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, events[1].Kind);
        }

        [Test]
        public void Assistant_McpToolCall_MapsNameAndId()
        {
            var line = "{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"tc-99\",\"type\":\"function\",\"function\":{\"name\":\"mcp_unity-mcp_batch\",\"arguments\":\"{\\\"ops\\\":[]}\"}}]}";
            var events = Parse(line);
            Assert.AreEqual("mcp_unity-mcp_batch", events[0].Text);
            Assert.AreEqual("tc-99", events[0].ToolId);
        }

        [Test]
        public void Assistant_McpToolCall_ArgsJsonPresent()
        {
            var line = "{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"tc-1\",\"type\":\"function\",\"function\":{\"name\":\"mcp_unity-mcp_batch\",\"arguments\":\"{\\\"ops\\\":[1,2]}\"}}]}";
            var events = Parse(line);
            StringAssert.Contains("ops", events[0].ArgsJson);
        }

        [Test]
        public void Assistant_NonMcpToolCall_EmitsNothing()
        {
            // Internal tool (not mcp_ prefixed) must be filtered out
            var line = "{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"tc-1\",\"type\":\"function\",\"function\":{\"name\":\"web_search\",\"arguments\":\"{}\"}}]}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Assistant_AskUserTool_EmitsNothing()
        {
            // ask_user goes through MCP TCP path — suppress to avoid double AskUserCard
            var line = "{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"id\":\"tc-1\",\"type\":\"function\",\"function\":{\"name\":\"mcp_unity-mcp_ask_user\",\"arguments\":\"{}\"}}]}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Assistant_EmptyToolCallsArray_EmitsNothing()
        {
            var line = "{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[]}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count);
        }

        // ── role:tool ─────────────────────────────────────────────────────────

        [Test]
        public void Tool_EmitsToolResult()
        {
            var line = "{\"role\":\"tool\",\"tool_call_id\":\"tc-1\",\"content\":\"Main Camera found\"}";
            var events = Parse(line);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsTrue(events[0].IsOk);
        }

        [Test]
        public void Tool_MapsToolCallId()
        {
            var line = "{\"role\":\"tool\",\"tool_call_id\":\"tc-42\",\"content\":\"ok\"}";
            var events = Parse(line);
            Assert.AreEqual("tc-42", events[0].ToolId);
        }

        [Test]
        public void Tool_ContentInText()
        {
            var line = "{\"role\":\"tool\",\"tool_call_id\":\"tc-1\",\"content\":\"result data here\"}";
            var events = Parse(line);
            StringAssert.Contains("result data here", events[0].Text);
        }

        // ── role:user (prompt echo) ────────────────────────────────────────────

        [Test]
        public void User_Echo_EmitsNothing()
        {
            var events = Parse("{\"role\":\"user\",\"content\":\"my prompt\"}");
            Assert.AreEqual(0, events.Count);
        }

        // ── unknown role ──────────────────────────────────────────────────────

        [Test]
        public void UnknownRole_EmitsNothing()
        {
            var events = Parse("{\"role\":\"system\",\"content\":\"sys prompt\"}");
            Assert.AreEqual(0, events.Count);
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
        public void MalformedJson_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Parse("{not valid json{{{{"));
        }

        [Test]
        public void MalformedJson_EmitsNoEvents()
        {
            var events = Parse("{role:assistant content:broken");
            // Malformed JSON has no valid "role" key — must emit 0 events (not throw).
            Assert.AreEqual(0, events.Count);
        }

        // ── M1: TurnDone (finish_reason fallback + meta hint) ────────────────

        [Test]
        public void Assistant_FinishReasonStop_EmitsTurnDone()
        {
            var line = "{\"role\":\"assistant\",\"content\":\"\",\"finish_reason\":\"stop\"}";
            var events = Parse(line);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TurnDone, events[0].Kind);
        }

        [Test]
        public void Assistant_TextAndFinishReasonStop_EmitsTextDeltaThenTurnDone()
        {
            var line = "{\"role\":\"assistant\",\"content\":\"Final answer\",\"finish_reason\":\"stop\"}";
            var events = Parse(line);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, events[0].Kind);
            Assert.AreEqual(ChatEventKind.TurnDone,  events[1].Kind);
        }

        [Test]
        public void Assistant_NoFinishReason_DoesNotEmitTurnDone()
        {
            var line = "{\"role\":\"assistant\",\"content\":\"partial\"}";
            var events = Parse(line);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, events[0].Kind);
        }

        [Test]
        public void Assistant_FinishReasonNonStop_DoesNotEmitTurnDone()
        {
            var line = "{\"role\":\"assistant\",\"content\":\"\",\"finish_reason\":\"tool_calls\"}";
            var events = Parse(line);
            // tool_calls finish_reason with empty content + empty tool_calls → nothing
            Assert.IsFalse(events.Exists(e => e.Kind == ChatEventKind.TurnDone));
        }

        [Test]
        public void Meta_SessionResumeHint_EmitsTurnDone()
        {
            var line = "{\"role\":\"meta\",\"type\":\"session.resume_hint\",\"session_id\":\"session_xxx\",\"command\":\"kimi -r session_xxx\",\"content\":\"...\"}";
            var events = Parse(line);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TurnDone, events[0].Kind);
        }

        [Test]
        public void Meta_OtherType_EmitsNothing()
        {
            var line = "{\"role\":\"meta\",\"type\":\"something_else\"}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void Meta_NoTypeField_EmitsNothing()
        {
            var events = Parse("{\"role\":\"meta\"}");
            Assert.AreEqual(0, events.Count);
        }

        // ── M3: tool error state ──────────────────────────────────────────────

        [Test]
        public void Tool_IsErrorFalse_EmitsOkTrue()
        {
            var line = "{\"role\":\"tool\",\"tool_call_id\":\"tc-1\",\"content\":\"ok\",\"isError\":\"false\"}";
            var events = Parse(line);
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events[0].IsOk);
        }

        [Test]
        public void Tool_IsErrorTrue_EmitsOkFalse()
        {
            var line = "{\"role\":\"tool\",\"tool_call_id\":\"tc-1\",\"content\":\"failed\",\"isError\":\"true\"}";
            var events = Parse(line);
            Assert.AreEqual(1, events.Count);
            Assert.IsFalse(events[0].IsOk);
        }

        [Test]
        public void Tool_NoIsErrorField_EmitsOkTrue()
        {
            var line = "{\"role\":\"tool\",\"tool_call_id\":\"tc-1\",\"content\":\"result\"}";
            var events = Parse(line);
            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events[0].IsOk);
        }
    }
}
