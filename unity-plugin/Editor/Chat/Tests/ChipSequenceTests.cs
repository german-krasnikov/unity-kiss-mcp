// Integration tests for chip-text-chip SEQUENCE — insertion & model state.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipSequenceTests
    {
        private InlineChipField _chipField;
        private ChipConfig      _cfg;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _chipField = new InlineChipField();
            _cfg       = new ChipConfig();
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

        // ── Single chip insertion ─────────────────────────────────────────────

        [Test]
        public void InsertChip_AtEmptyField_TextHasAtName()
        {
            InsertChip(H("/Player", "Player"), "Player");
            Assert.AreEqual("@Player ", _chipField.Text);
        }

        [Test]
        public void InsertChip_AtEndOfText_AtNameAppended()
        {
            _chipField.Text = "hello "; SetCursor(6);
            InsertChip(H("/Player", "Player"), "Player");
            Assert.AreEqual("hello @Player ", _chipField.Text);
        }

        [Test]
        public void InsertChip_AtBeginning_AtNamePrepended()
        {
            _chipField.Text = "world"; SetCursor(0);
            InsertChip(H("/Player", "Player"), "Player");
            Assert.AreEqual("@Player world", _chipField.Text);
        }

        [Test]
        public void InsertChip_InMiddleOfText_AtNameInserted()
        {
            _chipField.Text = "hello world"; SetCursor(6);
            InsertChip(H("/Player", "Player"), "Player");
            Assert.AreEqual("hello @Player world", _chipField.Text);
        }

        [Test]
        public void InsertChip_CursorAtZero_AtNameFirst()
        {
            _chipField.Text = "tail"; SetCursor(0);
            InsertChip(H("/Player", "Player"), "Player");
            StringAssert.StartsWith("@Player ", _chipField.Text);
        }

        [Test]
        public void InsertChip_ThenModifyText_ChipStillInModel()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            _chipField.Text = "completely different text";
            Assert.AreEqual(1, _chipField.Model.Count);
        }

        // ── chip → text → chip sequence ───────────────────────────────────────

        [Test]
        public void ChipTextChip_SequencePreserved()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            Assert.AreEqual("@Player  text @Enemy ", _chipField.Text);
        }

        [Test]
        public void ChipTextChip_ModelOrderMatchesInsertionOrder()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            Assert.AreEqual("Player", _chipField.Model.Chips[0].DisplayName);
            Assert.AreEqual("Enemy",  _chipField.Model.Chips[1].DisplayName);
        }

        [Test]
        public void ChipTextChip_PillRowHasTwoPills()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            ChatWindowAssertions.AssertChipCount(_chipField, 2);
        }

        // ── text → chip → text → chip → text ─────────────────────────────────

        [Test]
        public void TextChipTextChipText_FullSequence()
        {
            Type("start ");
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" middle ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            Type(" end");
            Assert.AreEqual("start @Player  middle @Enemy  end", _chipField.Text);
        }
    }
}
