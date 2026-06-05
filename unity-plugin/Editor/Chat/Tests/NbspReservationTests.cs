// Wave 3 — pure unit tests for NbspReservation (no Unity runtime needed).
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class NbspReservationTests
    {
        // (1) ComputeN: ceil(100/8) = 13
        [Test]
        public void ComputeN_NormalAdvance_ReturnsCeil()
        {
            Assert.AreEqual(13, NbspReservation.ComputeN(100f, 8f));
        }

        // (2) MF4: advance <= 0 -> floor to 1
        [Test]
        public void ComputeN_ZeroAdvance_ReturnsOne()
        {
            Assert.AreEqual(1, NbspReservation.ComputeN(50f, 0f));
        }

        // (3) BuildReservation(3) == U+FFFC + 3 x U+00A0
        [Test]
        public void BuildReservation_N3_ReturnsMarkerPlusThreeNbsp()
        {
            string expected = NbspReservation.FFFC
                + NbspReservation.NBSP
                + NbspReservation.NBSP
                + NbspReservation.NBSP;
            Assert.AreEqual(expected, NbspReservation.BuildReservation(3));
        }

        // (4) StripReservation roundtrip: removes all FFFC+NBSP runs, leaves other text
        [Test]
        public void StripReservation_Roundtrip_RemovesAllRunsLeavesOtherText()
        {
            string input  = "hello" + NbspReservation.BuildReservation(3)
                          + "world" + NbspReservation.BuildReservation(2);
            string result = NbspReservation.StripReservation(input);
            Assert.AreEqual("helloworld", result);
        }

        // (5) FindCorruptedChips: FFFC followed by 1 NBSP but expected 3 -> returns index 0
        [Test]
        public void FindCorruptedChips_WrongNbspCount_ReturnsCorruptedIndex()
        {
            // chip at index 0 expected to have 3 NBSP, but text only has 1
            string text    = NbspReservation.FFFC + NbspReservation.NBSP;
            var expected   = new List<int> { 3 };
            var corrupted  = NbspReservation.FindCorruptedChips(text, expected);
            Assert.AreEqual(1, corrupted.Count);
            Assert.AreEqual(0, corrupted[0]);
        }

        // (6) FindCorruptedChips: correct counts -> empty list
        [Test]
        public void FindCorruptedChips_CorrectCounts_ReturnsEmpty()
        {
            string text   = NbspReservation.BuildReservation(3);
            var expected  = new List<int> { 3 };
            var corrupted = NbspReservation.FindCorruptedChips(text, expected);
            Assert.AreEqual(0, corrupted.Count);
        }
    }
}
