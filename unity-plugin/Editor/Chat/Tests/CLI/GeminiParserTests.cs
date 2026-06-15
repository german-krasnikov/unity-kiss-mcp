// TDD tests for GeminiParser.
// Covers all stream-json event types: init, message, tool_use, tool_result, result, error.
// Also covers field mapping: tool_name→Text, tool_id→ToolId, parameters→ArgsJson.
// Fixture NDJSON strings live in GeminiTestFixtures.cs (shared with other Gemini tests).
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class GeminiParserTests
    {
        // ── Helper ────────────────────────────────────────────────────────────

        private static List<ChatEvent> Parse(string line)
        {
            var sink = new List<ChatEvent>();
            GeminiParser.ParseLine(line, sink);
            return sink;
        }

        // ── init ──────────────────────────────────────────────────────────────

        [Test]
        public void Init_WithSessionId_EmitsSessionInit()
        {
            var events = Parse(GeminiTestFixtures.Init);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.SessionInit, events[0].Kind);
            Assert.AreEqual("gemini-sess-abc", events[0].SessionId);
        }

        [Test]
        public void Init_WithoutSessionId_EmitsNothing()
        {
            var events = Parse(GeminiTestFixtures.InitNoSession);
            Assert.AreEqual(0, events.Count);
        }

        // ── message ───────────────────────────────────────────────────────────

        [Test]
        public void Message_WithContent_EmitsTextDelta()
        {
            var events = Parse(GeminiTestFixtures.MessageDelta);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, events[0].Kind);
            Assert.AreEqual("Hello world", events[0].Text);
        }

        [Test]
        public void Message_EmptyContent_EmitsNothing()
        {
            var events = Parse(GeminiTestFixtures.MessageEmpty);
            Assert.AreEqual(0, events.Count);
        }

        // ── tool_use ──────────────────────────────────────────────────────────

        [Test]
        public void ToolUse_EmitsToolStartAndArgsComplete()
        {
            var events = Parse(GeminiTestFixtures.ToolUse);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.ToolStart,        events[0].Kind);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, events[1].Kind);
        }

        [Test]
        public void ToolUse_MapsToolNameToText()
        {
            // Gemini uses tool_name field; ChatEvent.Text holds the tool name.
            var events = Parse(GeminiTestFixtures.ToolUse);
            Assert.AreEqual("mcp_unity-mcp_batch", events[0].Text);
        }

        [Test]
        public void ToolUse_MapsToolIdToToolId()
        {
            var events = Parse(GeminiTestFixtures.ToolUse);
            Assert.AreEqual("tool-123", events[0].ToolId);
        }

        [Test]
        public void ToolUse_MapsParametersToArgsJson()
        {
            var events = Parse(GeminiTestFixtures.ToolUse);
            StringAssert.Contains("ops", events[0].ArgsJson);
        }

        // ── tool_result ───────────────────────────────────────────────────────

        [Test]
        public void ToolResult_Success_EmitsToolResultOk()
        {
            var events = Parse(GeminiTestFixtures.ToolResult);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsTrue(events[0].IsOk);
            Assert.AreEqual("tool-123", events[0].ToolId);
            StringAssert.Contains("Main Camera", events[0].Text);
        }

        [Test]
        public void ToolResult_Error_EmitsToolResultNotOk()
        {
            var events = Parse(GeminiTestFixtures.ToolResultError);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
            Assert.IsFalse(events[0].IsOk);
            StringAssert.Contains("tool not found", events[0].Text);
        }

        // ── result ────────────────────────────────────────────────────────────

        [Test]
        public void Result_EmitsTurnDone()
        {
            var events = Parse(GeminiTestFixtures.Result);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TurnDone, events[0].Kind);
        }

        [Test]
        public void Result_SessionId_PropagatedToTurnDone()
        {
            var events = Parse(GeminiTestFixtures.Result);
            Assert.AreEqual("gemini-sess-abc", events[0].SessionId);
        }

        [Test]
        public void Result_TotalTokens_PropagatedToOutputTokens()
        {
            var events = Parse(GeminiTestFixtures.Result);
            Assert.AreEqual(100, events[0].OutputTokens);
        }

        [Test]
        public void Result_CostUsd_IsZero()
        {
            // Gemini CLI doesn't report cost in stream-json.
            var events = Parse(GeminiTestFixtures.Result);
            Assert.AreEqual(0f, events[0].CostUsd);
        }

        // ── error ─────────────────────────────────────────────────────────────

        [Test]
        public void Error_EmitsError()
        {
            var events = Parse(GeminiTestFixtures.Error);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.Error, events[0].Kind);
            StringAssert.Contains("quota exceeded", events[0].Text);
        }

        // ── prompt echo filter ────────────────────────────────────────────────

        [Test]
        public void Message_RoleUser_EmitsNothing()
        {
            // Gemini echoes the prompt back as a message with role:user — must be skipped.
            var events = Parse(GeminiTestFixtures.MessageUserEcho);
            Assert.AreEqual(0, events.Count);
        }

        // ── ask_user suppression (TCP path handles it via CommandRouter) ─────

        [Test]
        public void ToolUse_AskUser_EmitsNothing()
        {
            // ask_user MCP tool goes through TCP path (CommandRouter.OnAskUser → AskUserCard).
            // Parser must suppress tool_use to avoid double AskUserCard.
            var events = Parse(GeminiTestFixtures.ToolUseAskUser);
            Assert.AreEqual(0, events.Count);
        }

        // ── internal tool filter ──────────────────────────────────────────────

        [Test]
        public void ToolUse_InternalTool_EmitsNothing()
        {
            // Non-mcp_ tool names (update_topic, google_search, etc.) must be skipped.
            var events = Parse(GeminiTestFixtures.ToolUseInternal);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void ToolUse_McpTool_EmitsTwoEvents()
        {
            // mcp_-prefixed tools must still emit ToolStart + ToolArgsComplete.
            var events = Parse(GeminiTestFixtures.ToolUse);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ChatEventKind.ToolStart,        events[0].Kind);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, events[1].Kind);
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
        public void UnknownType_Ignored()
        {
            var events = Parse("{\"type\":\"debug\",\"payload\":\"something\"}");
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void MalformedJson_DoesNotThrow()
        {
            // Nested object as tool_id — parser must not throw, ToolResult emitted regardless.
            var events = Parse("{\"type\":\"tool_result\",\"tool_id\":{\"nested\":\"object\"},\"status\":\"success\"}");
            Assert.AreEqual(1, events.Count, "Malformed tool_id → ToolResult still emitted");
            Assert.AreEqual(ChatEventKind.ToolResult, events[0].Kind);
        }
    }
}
