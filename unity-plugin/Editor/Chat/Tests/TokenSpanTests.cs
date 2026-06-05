// Wave 3 — pure unit tests for TokenSpan (no Unity runtime needed).
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TokenSpanTests
    {
        // Helpers: build text with 0/1/3 chips using NbspReservation
        private static string NoChips()   => "hello world";
        private static string OneChip()   => "before" + NbspReservation.BuildReservation(2) + "after";
        private static string ThreeChips() =>
            "a" + NbspReservation.BuildReservation(2) +
            "b" + NbspReservation.BuildReservation(3) +
            "c" + NbspReservation.BuildReservation(1) + "d";

        // (1) ComputeTokenSpans with 0 chips -> empty list
        [Test]
        public void ComputeTokenSpans_NoChips_ReturnsEmpty()
        {
            var spans = TokenSpan.ComputeTokenSpans(NoChips());
            Assert.AreEqual(0, spans.Count);
        }

        // (2) ComputeTokenSpans with 1 chip -> one span covering FFFC+2xNBSP
        [Test]
        public void ComputeTokenSpans_OneChip_ReturnsOneSpan()
        {
            string text  = OneChip();
            var spans    = TokenSpan.ComputeTokenSpans(text);
            Assert.AreEqual(1, spans.Count);
            // span covers index of FFFC through last NBSP (inclusive end = start+3-1 for 1+2 chars)
            int fffcIdx = text.IndexOf(NbspReservation.FFfcChar);
            Assert.AreEqual(fffcIdx,     spans[0].Start);
            Assert.AreEqual(fffcIdx + 2, spans[0].End);   // FFFC + 2 NBSP = 3 chars, end = start+2
        }

        // (3) ComputeTokenSpans with 3 chips -> 3 spans
        [Test]
        public void ComputeTokenSpans_ThreeChips_ReturnsThreeSpans()
        {
            var spans = TokenSpan.ComputeTokenSpans(ThreeChips());
            Assert.AreEqual(3, spans.Count);
        }

        // (4) CountTrailingNbsp: FFFC + 3 NBSP -> 3
        [Test]
        public void CountTrailingNbsp_ThreeNbsp_Returns3()
        {
            string text  = NbspReservation.BuildReservation(3);
            int count    = TokenSpan.CountTrailingNbsp(text, 0); // fffcIndex=0
            Assert.AreEqual(3, count);
        }

        // (5a) IsInsideSpan: caret at midpoint of span -> true
        [Test]
        public void IsInsideSpan_MidpointCaret_ReturnsTrue()
        {
            string text  = OneChip();
            var spans    = TokenSpan.ComputeTokenSpans(text);
            int fffcIdx  = text.IndexOf(NbspReservation.FFfcChar);
            int midpoint = fffcIdx + 1;  // inside the NBSP run
            Assert.IsTrue(TokenSpan.IsInsideSpan(spans, midpoint));
        }

        // (5b) IsInsideSpan: caret outside any span -> false
        [Test]
        public void IsInsideSpan_OutsideCaret_ReturnsFalse()
        {
            string text = OneChip();
            var spans   = TokenSpan.ComputeTokenSpans(text);
            Assert.IsFalse(TokenSpan.IsInsideSpan(spans, 0)); // before FFFC
        }

        // (6) SpanIndexAtCaret: inside second of 3 spans -> returns 1; outside -> -1
        [Test]
        public void SpanIndexAtCaret_InsideSecondSpan_ReturnsCorrectIndex()
        {
            string text = ThreeChips();
            var spans   = TokenSpan.ComputeTokenSpans(text);
            Assert.AreEqual(3, spans.Count);

            int midSecond = spans[1].Start + 1;
            Assert.AreEqual(1, TokenSpan.SpanIndexAtCaret(spans, midSecond));
        }

        [Test]
        public void SpanIndexAtCaret_OutsideAllSpans_ReturnsMinusOne()
        {
            string text = ThreeChips();
            var spans   = TokenSpan.ComputeTokenSpans(text);
            Assert.AreEqual(-1, TokenSpan.SpanIndexAtCaret(spans, 0));
        }
    }
}
