using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CursorAndRemoveTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static ChipData Chip(string name)
            => new ChipData(ChipKindKeys.Hierarchy, "/" + name, name, 0);

        [Test]
        public void LastCursorPos_DefaultsToZero()
        {
            var field = new InlineChipField();
            Assert.AreEqual(0, field.LastCursorPos);
        }

        [Test]
        public void ClearChips_ResetsLastCursorPos()
        {
            var field = new InlineChipField();
            field.TextField.value = "hello";
            // Simulate saved cursor
            field.AddChip(Chip("A"));
            field.ClearChips();
            Assert.AreEqual(0, field.LastCursorPos);
        }

        [Test]
        public void RemoveChip_RemovesMentionFromText()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Player"));
            field.TextField.value = "@Player ";
            field.RemoveChipAt(0);
            Assert.AreEqual("", field.Text);
        }

        [Test]
        public void RemoveChip_RemovesMentionMidText()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            field.AddChip(Chip("B"));
            field.TextField.value = "@A text @B rest";
            field.RemoveChipAt(0);
            Assert.AreEqual("text @B rest", field.Text);
        }

        [Test]
        public void RemoveChip_NoMentionInText_NoChange()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Player"));
            field.TextField.value = "something else";
            field.RemoveChipAt(0);
            Assert.AreEqual("something else", field.Text);
        }

        [Test]
        public void RemoveChip_DuplicateNames_RemovesFirstOnly()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            field.AddChip(Chip("A"));
            field.TextField.value = "@A @A";
            field.RemoveChipAt(0);
            Assert.AreEqual("@A", field.Text);
        }

        [Test]
        public void TwoChipsWithoutRefocus_SecondUsesLastCursorPos()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            field.TextField.value = "@A ";
            field.TextField.cursorIndex = 3;
            field.TextField.selectIndex = 3;
            // Without FocusOut, LastCursorPos is still 0 (stale)
            Assert.AreEqual(0, field.LastCursorPos, "LastCursorPos stale without FocusOut");
        }
    }
}
