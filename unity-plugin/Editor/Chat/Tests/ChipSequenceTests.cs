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
        private void InsertChip(ChipData c) => ChipTestHelpers.InsertChip(_chipField, c);
        private void SetCursor(int p) => ChipTestHelpers.SetCursor(_chipField, p);
        private void Type(string t) => ChipTestHelpers.Type(_chipField, t);

        // ── Single chip insertion ─────────────────────────────────────────────

        [Test]
        public void InsertChip_AtEmptyField_ChipInModel()
        {
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("/Player", _chipField.Model.Chips[0].Path);
            Assert.AreEqual("", _chipField.Text);
        }

        [Test]
        public void InsertChip_AtEndOfText_ChipInModel()
        {
            _chipField.Text = "hello "; SetCursor(6);
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("hello ", _chipField.Text);
        }

        [Test]
        public void InsertChip_AtBeginning_ChipInModel()
        {
            _chipField.Text = "world"; SetCursor(0);
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("world", _chipField.Text);
        }

        [Test]
        public void InsertChip_InMiddleOfText_ChipInModel()
        {
            _chipField.Text = "hello world"; SetCursor(6);
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("hello world", _chipField.Text);
        }

        [Test]
        public void InsertChip_CursorAtZero_ChipAtOffsetZero()
        {
            _chipField.Text = "tail"; SetCursor(0);
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("tail", _chipField.Text);
        }

        [Test]
        public void InsertChip_ThenModifyText_ChipStillInModel()
        {
            InsertChip(H("/Player", "Player", 1));
            _chipField.Text = "completely different text";
            Assert.AreEqual(1, _chipField.Model.Count);
        }

        // ── chip → text → chip sequence ───────────────────────────────────────

        [Test]
        public void ChipTextChip_SequencePreserved()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            // F13: no @mention text; text field holds only typed text
            Assert.AreEqual(" text ", _chipField.Text);
            Assert.AreEqual(2, _chipField.Model.Count);
        }

        [Test]
        public void ChipTextChip_ModelOrderMatchesInsertionOrder()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            Assert.AreEqual("Player", _chipField.Model.Chips[0].DisplayName);
            Assert.AreEqual("Enemy",  _chipField.Model.Chips[1].DisplayName);
        }

        [Test]
        public void ChipTextChip_PillRowHasTwoPills()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            ChatWindowAssertions.AssertChipCount(_chipField, 2);
        }

        // ── text → chip → text → chip → text ─────────────────────────────────

        [Test]
        public void TextChipTextChipText_FullSequence()
        {
            Type("start ");
            InsertChip(H("/Player", "Player", 1));
            Type(" middle ");
            InsertChip(H("/Enemy", "Enemy", 2));
            Type(" end");
            // F13: text field holds only typed text; chips are tracked by position
            Assert.AreEqual("start  middle  end", _chipField.Text);
            Assert.AreEqual(2, _chipField.Model.Count);
        }
    }
}
