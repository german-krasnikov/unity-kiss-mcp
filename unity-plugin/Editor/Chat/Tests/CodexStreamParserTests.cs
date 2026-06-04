// TDD tests for CodexStreamParser.
// Fixture lines are inlined as constants (same pattern as ChatStreamParserTests).
// Scrubbed: /Users/german/... replaced with /Users/testuser/... per NDA/public-repo policy.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CodexStreamParserTests
    {
        // ── turn1_get_hierarchy fixture lines ─────────────────────────────────
        private const string Turn1_L1 = "{\"type\":\"thread.started\",\"thread_id\":\"019e9353-8c51-7143-89cb-e1fa68b4bb08\"}";
        private const string Turn1_L2 = "{\"type\":\"turn.started\"}";
        private const string Turn1_L3 = "{\"type\":\"item.completed\",\"item\":{\"id\":\"item_0\",\"type\":\"agent_message\",\"text\":\"I'll inspect the current Unity scene hierarchy with the MCP tool, then summarize the object structure briefly and stop.\"}}";
        private const string Turn1_L4 = "{\"type\":\"item.started\",\"item\":{\"id\":\"item_1\",\"type\":\"mcp_tool_call\",\"server\":\"unity\",\"tool\":\"get_hierarchy\",\"arguments\":{\"summary\":true,\"components\":false,\"compress\":true,\"incremental\":false},\"result\":null,\"error\":null,\"status\":\"in_progress\"}}";
        private const string Turn1_L5 = "{\"type\":\"item.completed\",\"item\":{\"id\":\"item_1\",\"type\":\"mcp_tool_call\",\"server\":\"unity\",\"tool\":\"get_hierarchy\",\"arguments\":{\"summary\":true,\"components\":false,\"compress\":true,\"incremental\":false},\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"GridTest (28 nodes)\\n  Main Camera\\n  GridFloor\\n  GridPlayer\\n  Collectible_1\\n  Collectible_2\\n  Collectible_3\\n  Grid (20 children)\\n  DebugLifeA\\n\"}]},\"error\":null,\"status\":\"completed\"}}";
        private const string Turn1_L7 = "{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":24167,\"cached_input_tokens\":16128,\"output_tokens\":176,\"reasoning_output_tokens\":40}}";

        // ── resume_get_console fixture lines (SCRUBBED: testuser/TestProject) ─
        private const string Resume_L4 = "{\"type\":\"item.started\",\"item\":{\"id\":\"item_1\",\"type\":\"mcp_tool_call\",\"server\":\"unity\",\"tool\":\"get_console\",\"arguments\":{\"count\":20,\"first\":0},\"result\":null,\"error\":null,\"status\":\"in_progress\"}}";
        private const string Resume_L5 = "{\"type\":\"item.completed\",\"item\":{\"id\":\"item_1\",\"type\":\"mcp_tool_call\",\"server\":\"unity\",\"tool\":\"get_console\",\"arguments\":{\"count\":20,\"first\":0},\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"[Log] 17:01:50.164 Saving results to: /Users/testuser/Library/Application Support/DefaultCompany/TestProject/TestResults.xml\\n[Log] 17:21:07.186 [MCP] Client disconnected (gen=1)\"}]},\"error\":null,\"status\":\"completed\"}}";

        // ── missing_mcp_config fixture line (failed tool call) ─────────────────
        private const string Missing_L7 = "{\"type\":\"item.completed\",\"item\":{\"id\":\"item_3\",\"type\":\"mcp_tool_call\",\"server\":\"unity\",\"tool\":\"list_mcp_resources\",\"arguments\":{\"server\":\"unity\"},\"result\":null,\"error\":{\"message\":\"resources/list failed: unknown MCP server 'unity'\"},\"status\":\"failed\"}}";

        // ── Helpers ───────────────────────────────────────────────────────────

        private static List<ChatEvent> Parse(string line)
        {
            var sink = new List<ChatEvent>();
            CodexStreamParser.ParseLine(line, sink);
            return sink;
        }

        // ── thread.started ────────────────────────────────────────────────────

        [Test]
        public void ThreadStarted_EmitsSessionInit()
        {
            var events = Parse(Turn1_L1);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.SessionInit, events[0].Kind);
            Assert.AreEqual("019e9353-8c51-7143-89cb-e1fa68b4bb08", events[0].SessionId);
        }

        // ── turn.started ──────────────────────────────────────────────────────

        [Test]
        public void TurnStarted_Ignored()
        {
            var events = Parse(Turn1_L2);
            Assert.AreEqual(0, events.Count);
        }

        // ── agent_message item.completed ──────────────────────────────────────

        [Test]
        public void ItemCompleted_AgentMessage_EmitsTextDelta()
        {
            var events = Parse(Turn1_L3);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, events[0].Kind);
            StringAssert.StartsWith("I'll inspect", events[0].Text);
        }

        // ── mcp_tool_call item.started ────────────────────────────────────────

        [Test]
        public void ItemStarted_McpToolCall_EmitsToolStartAndArgsComplete()
        {
            var events = Parse(Turn1_L4);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.ToolStart,        events[0].Kind);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, events[1].Kind);
            Assert.AreEqual("unity:get_hierarchy",          events[0].Text);
        }

        [Test]
        public void McpToolCall_Arguments_IsObject()
        {
            var events = Parse(Turn1_L4);
            Assert.IsTrue(events[0].ArgsJson.StartsWith("{"),
                "ArgsJson must be a JSON object, not a string");
        }

        [Test]
        public void McpToolCall_Arguments_ContainsSummaryKey()
        {
            var events = Parse(Turn1_L4);
            StringAssert.Contains("summary", events[0].ArgsJson);
        }

        // ── mcp_tool_call item.completed ──────────────────────────────────────

        [Test]
        public void ItemCompleted_McpToolCall_EmitsToolResult()
        {
            var events = Parse(Turn1_L5);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsTrue(events[0].IsOk);
        }

        [Test]
        public void McpToolCall_ResultText_ExtractsContentText()
        {
            var events = Parse(Turn1_L5);
            StringAssert.Contains("GridTest", events[0].Text);
        }

        // ── turn.completed ────────────────────────────────────────────────────

        [Test]
        public void TurnCompleted_EmitsTurnDone()
        {
            var events = Parse(Turn1_L7);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TurnDone, events[0].Kind);
            Assert.AreEqual(0f,     events[0].CostUsd);
            Assert.AreEqual(24167,  events[0].InputTokens);
            Assert.AreEqual(176,    events[0].OutputTokens);
        }

        // ── resume: get_console ───────────────────────────────────────────────

        [Test]
        public void Resume_McpToolCall_Started_EmitsToolStart()
        {
            var events = Parse(Resume_L4);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.ToolStart, events[0].Kind);
            Assert.AreEqual("unity:get_console",     events[0].Text);
        }

        [Test]
        public void Resume_McpToolCall_Completed_EmitsToolResult()
        {
            var events = Parse(Resume_L5);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsTrue(events[0].IsOk);
            StringAssert.Contains("TestResults.xml", events[0].Text);
        }

        // ── missing mcp config: failed tool call ──────────────────────────────

        [Test]
        public void MissingMcpConfig_FailedToolCall_EmitsToolResultNotOk()
        {
            var events = Parse(Missing_L7);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsFalse(events[0].IsOk);
            StringAssert.Contains("unknown MCP server", events[0].Text);
        }

        // ── command_execution ─────────────────────────────────────────────────

        private const string CmdExec_Started =
            "{\"type\":\"item.started\",\"item\":{\"id\":\"c1\",\"type\":\"command_execution\",\"command\":\"ls -la\",\"status\":\"in_progress\"}}";

        private const string CmdExec_Completed_Ok =
            "{\"type\":\"item.completed\",\"item\":{\"id\":\"c1\",\"type\":\"command_execution\",\"command\":\"ls -la\",\"aggregated_output\":\"total 8\\ndrwxr-xr-x\",\"status\":\"completed\"}}";

        private const string CmdExec_Completed_Declined =
            "{\"type\":\"item.completed\",\"item\":{\"id\":\"c1\",\"type\":\"command_execution\",\"command\":\"rm -rf /\",\"aggregated_output\":\"\",\"status\":\"declined\"}}";

        private const string CmdExec_Completed_Failed =
            "{\"type\":\"item.completed\",\"item\":{\"id\":\"c1\",\"type\":\"command_execution\",\"command\":\"ls -la\",\"aggregated_output\":\"\",\"status\":\"failed\"}}";

        [Test]
        public void CommandExecution_Started_EmitsToolStartAndArgsComplete()
        {
            var events = Parse(CmdExec_Started);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.ToolStart,        events[0].Kind);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, events[1].Kind);
            Assert.AreEqual("shell:ls -la",                 events[0].Text);
        }

        [Test]
        public void CommandExecution_Completed_AggregatedOutput_EmitsToolResultOk()
        {
            var events = Parse(CmdExec_Completed_Ok);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsTrue(events[0].IsOk);
            StringAssert.Contains("total 8", events[0].Text);
        }

        [Test]
        public void CommandExecution_Completed_Declined_EmitsToolResultNotOk()
        {
            var events = Parse(CmdExec_Completed_Declined);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsFalse(events[0].IsOk);
        }

        [Test]
        public void CommandExecution_Completed_Failed_EmitsToolResultNotOk()
        {
            var events = Parse(CmdExec_Completed_Failed);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsFalse(events[0].IsOk);
        }

        // ── file_change ───────────────────────────────────────────────────────

        private const string FileChange_Started =
            "{\"type\":\"item.started\",\"item\":{\"id\":\"f1\",\"type\":\"file_change\",\"changes\":[{\"path\":\"Assets/Foo.cs\",\"kind\":\"update\"}],\"status\":\"in_progress\"}}";

        private const string FileChange_Completed_Ok =
            "{\"type\":\"item.completed\",\"item\":{\"id\":\"f1\",\"type\":\"file_change\",\"changes\":[{\"path\":\"Assets/Foo.cs\",\"kind\":\"update\"}],\"summary\":\"1 file updated\",\"status\":\"completed\"}}";

        [Test]
        public void FileChange_Started_EmitsToolStartAndArgsComplete()
        {
            var events = Parse(FileChange_Started);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.ToolStart,        events[0].Kind);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, events[1].Kind);
            Assert.AreEqual("file_change",                  events[0].Text);
        }

        [Test]
        public void FileChange_Started_ArgsContainsPath()
        {
            var events = Parse(FileChange_Started);
            StringAssert.Contains("Assets/Foo.cs", events[0].ArgsJson);
        }

        [Test]
        public void FileChange_Completed_EmitsToolResultOk()
        {
            var events = Parse(FileChange_Completed_Ok);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsTrue(events[0].IsOk);
            StringAssert.Contains("1 file updated", events[0].Text);
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
        public void MalformedJson_EmitsError()
        {
            var events = Parse("{broken");
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.Error, events[0].Kind);
        }

        [Test]
        public void UnknownType_Ignored()
        {
            var events = Parse("{\"type\":\"future_thing\"}");
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void UnknownItemType_Ignored()
        {
            var events = Parse("{\"type\":\"item.completed\",\"item\":{\"type\":\"quantum_widget\"}}");
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void TurnFailed_EmitsError()
        {
            const string line = "{\"type\":\"turn.failed\",\"error\":{\"message\":\"context limit exceeded\"}}";
            var events = Parse(line);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.Error, events[0].Kind);
            StringAssert.Contains("context limit", events[0].Text);
        }

        [Test]
        public void TopLevelError_EmitsError()
        {
            const string line = "{\"type\":\"error\",\"message\":\"rate limit\"}";
            var events = Parse(line);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.Error, events[0].Kind);
            StringAssert.Contains("rate limit", events[0].Text);
        }
    }
}
