// F13: Tests updated — no @mention injection; RemoveChipAt is index-only (no text mutation).
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
            field.AddChip(Chip("A"));
            field.ClearChips();
            Assert.AreEqual(0, field.LastCursorPos);
        }

        // F13: RemoveChipAt removes model entry; does NOT mutate TextField text.
        [Test]
        public void RemoveChip_RemovesFromModel_DoesNotMutateText()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Player"));
            field.TextField.value = "some text";
            field.RemoveChipAt(0);
            Assert.AreEqual(0, field.Model.Count);
            Assert.AreEqual("some text", field.Text, "Text must be unchanged — no @mention to remove");
        }

        // F13: removing one of two chips leaves text unchanged, removes correct model entry.
        [Test]
        public void RemoveChip_TwoChips_RemovesFirstFromModel()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            field.AddChip(Chip("B"));
            field.TextField.value = "plain text";
            field.RemoveChipAt(0);
            Assert.AreEqual(1, field.Model.Count);
            Assert.AreEqual("/B", field.Model.Chips[0].Path);
            Assert.AreEqual("plain text", field.Text, "Text must be unchanged");
        }

        // F13: no @mention text to remove means text stays exactly as-is.
        [Test]
        public void RemoveChip_NoMentionInText_TextUnchanged()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Player"));
            field.TextField.value = "something else";
            field.RemoveChipAt(0);
            Assert.AreEqual("something else", field.Text);
        }

        // F13: duplicate-name chips removed by index — model entry at 0 is removed.
        [Test]
        public void RemoveChip_DuplicateNames_RemovesModelEntry0()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            field.AddChip(Chip("A"));
            field.RemoveChipAt(0);
            // One chip should remain
            Assert.AreEqual(1, field.Model.Count);
        }

        [Test]
        public void TwoChipsWithoutRefocus_SecondUsesLastCursorPos()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            field.TextField.value = "text";
            field.TextField.cursorIndex = 3;
            field.TextField.selectIndex = 3;
            // Without FocusOut, LastCursorPos is still 0 (stale)
            Assert.AreEqual(0, field.LastCursorPos, "LastCursorPos stale without FocusOut");
        }
    }
}
