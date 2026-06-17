// TDD tests for OpenCodeParser.
// Type-based NDJSON dispatch — pure, no UnityEngine deps.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class OpenCodeParserTests
    {
        // ── NDJSON fixtures ───────────────────────────────────────────────────
        private const string StepStart  = "{\"type\":\"step_start\",\"sessionID\":\"oc-1\",\"timestamp\":1000,\"part\":{\"type\":\"step-start\"}}";
        private const string Text       = "{\"type\":\"text\",\"sessionID\":\"oc-1\",\"timestamp\":2000,\"part\":{\"text\":\"Hello world\"}}";
        private const string ToolOk     = "{\"type\":\"tool_use\",\"sessionID\":\"oc-1\",\"timestamp\":3000,\"part\":{\"tool\":\"bash\",\"callID\":\"c1\",\"state\":{\"status\":\"completed\",\"input\":{\"cmd\":\"echo hi\"},\"output\":\"hi\\n\"}}}";
        private const string ToolErr    = "{\"type\":\"tool_use\",\"sessionID\":\"oc-1\",\"timestamp\":3100,\"part\":{\"tool\":\"bash\",\"callID\":\"c2\",\"state\":{\"status\":\"error\",\"input\":{\"cmd\":\"bad\"},\"output\":\"not found\"}}}";
        private const string StepFinish = "{\"type\":\"step_finish\",\"sessionID\":\"oc-1\",\"timestamp\":4000,\"part\":{\"reason\":\"stop\",\"cost\":0.001,\"tokens\":{\"input\":100,\"output\":20}}}";
        private const string Err        = "{\"type\":\"error\",\"sessionID\":\"oc-1\",\"timestamp\":5000,\"error\":{\"data\":{\"message\":\"rate limit\"}}}";

        private static List<ChatEvent> Parse(string line)
        {
            var sink = new List<ChatEvent>();
            OpenCodeParser.ParseLine(line, sink);
            return sink;
        }

        // ── step_start ────────────────────────────────────────────────────────

        [Test]
        public void StepStart_EmitsSessionInit()
        {
            var events = Parse(StepStart);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.SessionInit, events[0].Kind);
        }

        [Test]
        public void StepStart_PropagatesSessionId()
        {
            var events = Parse(StepStart);
            Assert.AreEqual("oc-1", events[0].SessionId);
        }

        [Test]
        public void StepStart_SecondCall_EmitsAnotherSessionInit()
        {
            // Parser is pure/stateless per call — always emits from step_start
            var events1 = Parse(StepStart);
            var events2 = Parse(StepStart);
            Assert.AreEqual(1, events1.Count);
            Assert.AreEqual(1, events2.Count);
            Assert.AreEqual(ChatEventKind.SessionInit, events2[0].Kind);
        }

        // ── text ──────────────────────────────────────────────────────────────

        [Test]
        public void Text_EmitsTextDelta()
        {
            var events = Parse(Text);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, events[0].Kind);
        }

        [Test]
        public void Text_FullContentIsOneEvent()
        {
            var events = Parse(Text);
            Assert.AreEqual("Hello world", events[0].Text);
        }

        // ── tool_use (completed) ──────────────────────────────────────────────

        [Test]
        public void ToolUse_Completed_EmitsThreeEvents()
        {
            var events = Parse(ToolOk);
            Assert.AreEqual(3, events.Count);
        }

        [Test]
        public void ToolUse_Maps_PartTool_ToText()
        {
            var events = Parse(ToolOk);
            Assert.AreEqual(ChatEventKind.ToolStart, events[0].Kind);
            Assert.AreEqual("bash", events[0].Text);
        }

        [Test]
        public void ToolUse_Maps_CallId_ToToolId()
        {
            var events = Parse(ToolOk);
            Assert.AreEqual("c1", events[0].ToolId);
        }

        [Test]
        public void ToolUse_Maps_StateInput_ToArgsJson()
        {
            var events = Parse(ToolOk);
            Assert.AreEqual(ChatEventKind.ToolArgsComplete, events[1].Kind);
            // ArgsJson lives on ToolStart (events[0])
            StringAssert.Contains("cmd", events[0].ArgsJson);
        }

        [Test]
        public void ToolUse_Completed_ResultOk_True()
        {
            var events = Parse(ToolOk);
            Assert.AreEqual(ChatEventKind.ToolResult, events[2].Kind);
            Assert.IsTrue(events[2].IsOk);
        }

        // ── tool_use (error) ──────────────────────────────────────────────────

        [Test]
        public void ToolUse_Error_ResultOk_False()
        {
            var events = Parse(ToolErr);
            Assert.AreEqual(ChatEventKind.ToolResult, events[2].Kind);
            Assert.IsFalse(events[2].IsOk);
        }

        [Test]
        public void ToolUse_Error_OutputInResultText()
        {
            var events = Parse(ToolErr);
            Assert.AreEqual("not found", events[2].Text);
        }

        // ── step_finish ───────────────────────────────────────────────────────

        [Test]
        public void StepFinish_Stop_EmitsTurnDone()
        {
            var events = Parse(StepFinish);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TurnDone, events[0].Kind);
        }

        [Test]
        public void StepFinish_PropagatesCost()
        {
            var events = Parse(StepFinish);
            Assert.Greater(events[0].CostUsd, 0f);
        }

        [Test]
        public void StepFinish_PropagatesInputTokens()
        {
            var events = Parse(StepFinish);
            Assert.AreEqual(100, events[0].InputTokens);
        }

        [Test]
        public void StepFinish_PropagatesOutputTokens()
        {
            var events = Parse(StepFinish);
            Assert.AreEqual(20, events[0].OutputTokens);
        }

        [Test]
        public void StepFinish_PropagatesSessionId()
        {
            var events = Parse(StepFinish);
            Assert.AreEqual("oc-1", events[0].SessionId);
        }

        [Test]
        public void StepFinish_NonStop_EmitsNothing()
        {
            var line = "{\"type\":\"step_finish\",\"sessionID\":\"oc-1\",\"part\":{\"reason\":\"interrupted\"}}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count);
        }

        // ── error ─────────────────────────────────────────────────────────────

        [Test]
        public void Error_EmitsError()
        {
            var events = Parse(Err);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.Error, events[0].Kind);
        }

        [Test]
        public void Error_PropagatesMessage()
        {
            var events = Parse(Err);
            Assert.AreEqual("rate limit", events[0].Text);
        }

        // ── edge cases ────────────────────────────────────────────────────────

        [Test]
        public void NullLine_NoEvents()
        {
            var events = Parse(null);
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void EmptyLine_NoEvents()
        {
            var events = Parse("");
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void UnknownType_Ignored()
        {
            var events = Parse("{\"type\":\"unknown_event\",\"sessionID\":\"oc-1\"}");
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void MalformedJson_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Parse("{not valid json{{{{"));
        }

        // ── C7: cost is numeric literal ───────────────────────────────────────

        [Test]
        public void StepFinish_NumericCost_ParsedCorrectly()
        {
            // cost is a JSON number (not a string) — must not be 0
            var events = Parse(StepFinish);
            Assert.Greater(events[0].CostUsd, 0f,
                "cost=0.001 numeric literal must parse to >0");
        }

        [Test]
        public void StepFinish_ZeroCost_ParsesAsZero()
        {
            var line = "{\"type\":\"step_finish\",\"sessionID\":\"oc-1\",\"part\":{\"reason\":\"stop\",\"cost\":0,\"tokens\":{\"input\":1,\"output\":1}}}";
            var events = Parse(line);
            Assert.AreEqual(0f, events[0].CostUsd);
        }

        // ── C8: tool_use pending/running must not emit events ─────────────────

        [Test]
        public void ToolUse_Pending_EmitsNothing()
        {
            var line = "{\"type\":\"tool_use\",\"sessionID\":\"oc-1\",\"part\":{\"tool\":\"bash\",\"callID\":\"c1\",\"state\":{\"status\":\"pending\",\"input\":{},\"output\":\"\"}}}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count, "pending status must produce no events");
        }

        [Test]
        public void ToolUse_Running_EmitsNothing()
        {
            var line = "{\"type\":\"tool_use\",\"sessionID\":\"oc-1\",\"part\":{\"tool\":\"bash\",\"callID\":\"c1\",\"state\":{\"status\":\"running\",\"input\":{\"cmd\":\"ls\"},\"output\":\"\"}}}";
            var events = Parse(line);
            Assert.AreEqual(0, events.Count, "running status must produce no events");
        }

        [Test]
        public void ToolUse_Completed_StillEmitsThreeEvents()
        {
            var events = Parse(ToolOk);
            Assert.AreEqual(3, events.Count, "completed status must still emit 3 events");
        }

        [Test]
        public void ToolUse_Error_StillEmitsThreeEvents()
        {
            var events = Parse(ToolErr);
            Assert.AreEqual(3, events.Count, "error status must still emit 3 events");
        }
    }
}
