// Wave 4 — testable behaviors from MCPChatWindow.ChipInput.cs / OnSend strip path.
// (1) OnSend NBSP-strip: StripReservation removes FFFC+NBSP runs so clean payload is emitted.
// (2) Context-menu decision: SpanIndexAtCaret drives chip-specific vs. generic menu items.
// No VisualElement wiring tested here (not unit-testable per brief §KNOWN LIMITATIONS).
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class Wave4ChipInputTests
    {
        // ── (1) OnSend NBSP-strip ─────────────────────────────────────────────

        // When IsAvailable is true (NBSP path) the displayed text contains
        // FFFC+NBSP runs.  StripReservation must remove them to produce the
        // clean payload that is actually sent to the AI.
        [Test]
        public void StripReservation_OnSendPath_ProducesCleanPayload()
        {
            // Simulate typed text: "Fix " + chip reservation(4 NBSP) + " bug"
            string displayed = "Fix " + NbspReservation.BuildReservation(4) + " bug";
            string payload   = NbspReservation.StripReservation(displayed);
            Assert.AreEqual("Fix  bug", payload);
        }

        // Multiple chips in one message — all removed.
        [Test]
        public void StripReservation_MultipleChips_AllRemoved()
        {
            string displayed = NbspReservation.BuildReservation(3)
                             + "text"
                             + NbspReservation.BuildReservation(2);
            string payload = NbspReservation.StripReservation(displayed);
            Assert.AreEqual("text", payload);
        }

        // Plain text with no chips must pass through unchanged.
        [Test]
        public void StripReservation_NoChips_Unchanged()
        {
            const string plain = "hello world";
            Assert.AreEqual(plain, NbspReservation.StripReservation(plain));
        }

        // Strip is idempotent (safe to call even on IsAvailable==false path
        // where text only has bare FFFC markers — bare FFFC is also stripped).
        [Test]
        public void StripReservation_BareFFFC_Removed()
        {
            // A bare U+FFFC with zero NBSP (the IsAvailable==false path)
            string text = "a" + NbspReservation.FFFC + "b";
            Assert.AreEqual("ab", NbspReservation.StripReservation(text));
        }

        // ── (2) Context-menu decision ─────────────────────────────────────────

        // When caret is inside a chip span, SpanIndexAtCaret >= 0 → chip menu
        // items (Show LLM payload, Copy path, Remove) should be shown.
        [Test]
        public void SpanIndexAtCaret_InsideSpan_ReturnsNonNegative_ShowsChipMenu()
        {
            string text = "pre" + NbspReservation.BuildReservation(4) + "suf";
            var spans   = TokenSpan.ComputeTokenSpans(text);
            // caret at the FFFC position (start of chip span)
            int chipStart  = text.IndexOf(NbspReservation.FFfcChar);
            int chipIdx    = TokenSpan.SpanIndexAtCaret(spans, chipStart);
            Assert.GreaterOrEqual(chipIdx, 0, "Caret inside span must return chip index");
        }

        // When caret is outside all spans, SpanIndexAtCaret == -1 → generic
        // "Add Selection to Context" item only.
        [Test]
        public void SpanIndexAtCaret_OutsideSpan_ReturnsMinusOne_ShowsGenericMenu()
        {
            string text = "pre" + NbspReservation.BuildReservation(4) + "suf";
            var spans   = TokenSpan.ComputeTokenSpans(text);
            // caret at position 0 (before FFFC)
            int chipIdx = TokenSpan.SpanIndexAtCaret(spans, 0);
            Assert.AreEqual(-1, chipIdx, "Caret outside spans must return -1");
        }

        // Menu picks the CORRECT chip when multiple chips are present.
        [Test]
        public void SpanIndexAtCaret_ThreeChips_ReturnsCorrectChipIndex()
        {
            string text = NbspReservation.BuildReservation(2)
                        + "mid"
                        + NbspReservation.BuildReservation(3)
                        + "end"
                        + NbspReservation.BuildReservation(1);
            var spans = TokenSpan.ComputeTokenSpans(text);
            Assert.AreEqual(3, spans.Count);

            // caret at FFFC of the second chip
            int secondStart = spans[1].Start;
            Assert.AreEqual(1, TokenSpan.SpanIndexAtCaret(spans, secondStart));
        }
    }
}
