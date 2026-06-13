using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestParserEdgeCaseTests
    {
        // ── CS4.arch.5: MOVE TO missing position ────────────────────────────

        [Test]
        public void Parse_MoveWithoutToKeyword_ThrowsWithMoveSyntaxMessage()
        {
            // "MOVE /Player" has no TO keyword: toIdx == -1
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("MOVE /Player"));
            StringAssert.Contains("MOVE syntax", ex.Message);
        }

        [Test]
        public void Parse_Move_ToAtEnd_ThrowsArgumentException()
        {
            // "MOVE TO" — TO is last token, no position follows
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("MOVE TO"));
            StringAssert.Contains("MOVE syntax", ex.Message);
        }

        [Test]
        public void Parse_Move_WithPathAndNoPosition_ThrowsArgumentException()
        {
            // "MOVE /Player TO" — position token missing
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("MOVE /Player TO"));
            StringAssert.Contains("MOVE syntax", ex.Message);
        }

        [Test]
        public void Parse_Move_ValidLine_Succeeds()
        {
            var steps = PlaytestParser.Parse("MOVE TO 1,2,3");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Move, steps[0].Type);
            Assert.That(steps[0].Position.x, Is.EqualTo(1f).Within(0.001f));
        }

        // ── CS4.test.5: Compare error paths ─────────────────────────────────

        [Test]
        public void Compare_RelationalOp_NonNumericOperands_ThrowsRequiresNumericValues()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Compare("idle", "??", "running"));
            StringAssert.Contains("requires numeric values", ex.Message);
        }

        [Test]
        public void Compare_GreaterThan_StringValues_Throws()
        {
            // ">" requires numeric values — strings don't parse as float
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Compare("idle", ">", "running"));
            StringAssert.Contains("requires numeric values", ex.Message);
        }

        [Test]
        public void Compare_LessThan_StringValues_Throws()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Compare("abc", "<", "def"));
            StringAssert.Contains("requires numeric values", ex.Message);
        }

        // ── CS4.test.7: ASSERT_BATCH missing END ────────────────────────────

        [Test]
        public void Parse_AssertBatchBlockWithoutEnd_ThrowsWithEndMessage()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("ASSERT_BATCH\nASSERT /X|C|f == 1"));
            StringAssert.Contains("END", ex.Message);
        }

        [Test]
        public void Parse_AssertBatch_WithEnd_Succeeds()
        {
            var steps = PlaytestParser.Parse("ASSERT_BATCH\nASSERT /X|C|f == 1\nEND");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.AssertBatch, steps[0].Type);
            Assert.AreEqual(1, steps[0].Queries.Length);
        }
    }
}
