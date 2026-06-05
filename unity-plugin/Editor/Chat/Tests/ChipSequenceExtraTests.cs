// TDD — ChipSequence additional tests (Wave 1).
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipSequenceExtraTests
    {
        private InlineChipField _chipField;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _chipField = new InlineChipField();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private static ChipData H(string path, string name, int id = 0) => ChipTestHelpers.H(path, name, id);
        private void InsertChip(ChipData c, string n) => ChipTestHelpers.InsertChip(_chipField, c, n);
        private void SetCursor(int p) => ChipTestHelpers.SetCursor(_chipField, p);
        private void Type(string t) => ChipTestHelpers.Type(_chipField, t);

        // ── Cursor placement ──────────────────────────────────────────────────

        [Test]
        public void InsertChip_CursorBetweenWords_AtNameInMiddle()
        {
            _chipField.Text = "hello world"; SetCursor(6);
            InsertChip(H("/Player", "Player"), "Player");
            Assert.AreEqual("hello @Player world", _chipField.Text);
        }

        [Test]
        public void InsertChip_AfterAnotherChip_BothPresent()
        {
            InsertChip(H("/A", "A", 1), "A");
            InsertChip(H("/B", "B", 2), "B");
            StringAssert.Contains("@A", _chipField.Text);
            StringAssert.Contains("@B", _chipField.Text);
            Assert.AreEqual(2, _chipField.Model.Count);
        }

        [Test]
        public void InsertChip_CursorClampedToTextLength()
        {
            _chipField.Text = "hi"; SetCursor(999);
            Assert.DoesNotThrow(() => InsertChip(H("/X", "X"), "X"));
            StringAssert.Contains("@X", _chipField.Text);
        }

        // ── Multiple chips ────────────────────────────────────────────────────

        [Test]
        public void ChipChip_Consecutive_BothInModel()
        {
            InsertChip(H("/A", "A", 1), "A");
            InsertChip(H("/B", "B", 2), "B");
            Assert.AreEqual(2, _chipField.Model.Count);
            Assert.AreEqual("A", _chipField.Model.Chips[0].DisplayName);
            Assert.AreEqual("B", _chipField.Model.Chips[1].DisplayName);
        }

        [Test]
        public void FiveChipsInterleaved_ModelOrder()
        {
            for (int i = 1; i <= 5; i++)
            {
                Type("t" + i + " ");
                InsertChip(H("/N" + i, "N" + i, i), "N" + i);
            }
            Assert.AreEqual(5, _chipField.Model.Count);
            for (int i = 0; i < 5; i++) Assert.AreEqual("N" + (i + 1), _chipField.Model.Chips[i].DisplayName);
        }

        [Test]
        public void FiveChipsInterleaved_TextContainsAllAtNames()
        {
            for (int i = 1; i <= 5; i++)
            {
                Type("t" + i + " ");
                InsertChip(H("/N" + i, "N" + i, i), "N" + i);
            }
            for (int i = 1; i <= 5; i++) StringAssert.Contains("@N" + i, _chipField.Text);
        }

        // ── No chips ──────────────────────────────────────────────────────────

        [Test]
        public void TextOnly_NoChips_ModelEmpty()
        {
            Type("just text");
            Assert.AreEqual(0, _chipField.Model.Count);
        }

        // ── Clear then re-insert ──────────────────────────────────────────────

        [Test]
        public void ChipThenClear_ThenChipTextChip_FreshSequence()
        {
            InsertChip(H("/A", "A", 1), "A");
            _chipField.ClearChips();
            _chipField.Text = "";
            SetCursor(0);

            InsertChip(H("/B", "B", 2), "B");
            Type("mid ");
            InsertChip(H("/C", "C", 3), "C");

            Assert.AreEqual(2, _chipField.Model.Count);
            Assert.AreEqual("B", _chipField.Model.Chips[0].DisplayName);
            Assert.AreEqual("C", _chipField.Model.Chips[1].DisplayName);
            StringAssert.DoesNotContain("@A", _chipField.Text);
        }
    }
}
