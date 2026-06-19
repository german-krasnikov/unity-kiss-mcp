// TDD tests for AgyParser.
// agy outputs plain text — each line becomes TextDelta; EOF sentinel becomes TurnDone.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgyParserTests
    {
        private static List<ChatEvent> Parse(string line)
        {
            var sink = new List<ChatEvent>();
            AgyParser.ParseLine(line, sink);
            return sink;
        }

        // ── Plain text lines ──────────────────────────────────────────────────

        [Test]
        public void PlainTextLine_EmitsTextDelta()
        {
            var events = Parse("Hello from agy");
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, events[0].Kind);
            Assert.AreEqual("Hello from agy", events[0].Text);
        }

        [Test]
        public void MultipleLines_EachBecomesTextDelta()
        {
            var sink = new List<ChatEvent>();
            AgyParser.ParseLine("Line 1", sink);
            AgyParser.ParseLine("Line 2", sink);
            Assert.AreEqual(2, sink.Count);
            Assert.AreEqual("Line 1", sink[0].Text);
            Assert.AreEqual("Line 2", sink[1].Text);
        }

        // ── Empty / null lines ────────────────────────────────────────────────

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

        // ── EOF sentinel → TurnDone ───────────────────────────────────────────

        [Test]
        public void EofSentinel_EmitsTurnDone()
        {
            var events = Parse(AntigravityBackend.EofSentinel);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TurnDone, events[0].Kind);
        }

        [Test]
        public void EofSentinel_TurnDone_NullSessionId()
        {
            // agy doesn't emit session_id in stdout
            var events = Parse(AntigravityBackend.EofSentinel);
            Assert.IsNull(events[0].SessionId);
        }

        [Test]
        public void EofSentinel_TurnDone_ZeroCost()
        {
            var events = Parse(AntigravityBackend.EofSentinel);
            Assert.AreEqual(0f, events[0].CostUsd);
        }

        [Test]
        public void EofSentinel_NotEmittedAsText()
        {
            // The sentinel must become TurnDone, never TextDelta
            var events = Parse(AntigravityBackend.EofSentinel);
            Assert.AreNotEqual(ChatEventKind.TextDelta, events[0].Kind);
        }

        // ── JSON-looking lines are treated as plain text ───────────────────────

        [Test]
        public void JsonLookingLine_EmitsTextDeltaNotParsed()
        {
            // agy may output JSON-ish text — should be treated as plain text
            var events = Parse("{\"some\":\"json\"}");
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(ChatEventKind.TextDelta, events[0].Kind);
        }
    }
}
