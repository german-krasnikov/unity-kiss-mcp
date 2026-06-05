// TDD — InlineChipField tests (Wave 0).
// Element-tree construction asserts. No resolvedStyle/layout/live-panel dependence.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlineChipFieldTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static ChipData Chip(string path)
            => new ChipData(ChipKindKeys.Script, path, path, 0);

        // ── Constructor ───────────────────────────────────────────────────────

        [Test]
        public void Constructor_HasTextField()
        {
            var field = new InlineChipField();
            var tf = field.Q<TextField>();
            Assert.IsNotNull(tf, "InlineChipField must contain a TextField");
        }

        // ── AddChip ───────────────────────────────────────────────────────────

        [Test]
        public void AddChip_AddsPillBeforeTextField()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));

            // index 0 = pill VE, index 1 = TextField
            Assert.AreEqual(2, field.childCount);
            Assert.IsNotInstanceOf<TextField>(field[0], "first child must be pill, not TextField");
            Assert.IsInstanceOf<TextField>(field[1], "second child must be TextField");
        }

        [Test]
        public void AddTwoChips_PillsBeforeTextField()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            field.AddChip(Chip("Assets/B.cs"));

            Assert.AreEqual(3, field.childCount);
            Assert.IsNotInstanceOf<TextField>(field[0]);
            Assert.IsNotInstanceOf<TextField>(field[1]);
            Assert.IsInstanceOf<TextField>(field[2]);
        }

        // ── RemoveChipAt ──────────────────────────────────────────────────────

        [Test]
        public void RemoveChip_RemovesPillFromTree()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            field.AddChip(Chip("Assets/B.cs"));

            field.RemoveChipAt(0);

            // 1 pill + 1 TextField = 2 children
            Assert.AreEqual(2, field.childCount);
            Assert.IsInstanceOf<TextField>(field[1]);
        }

        // ── Clear ─────────────────────────────────────────────────────────────

        [Test]
        public void Clear_RemovesAllPills()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            field.AddChip(Chip("Assets/B.cs"));
            field.AddChip(Chip("Assets/C.cs"));

            field.ClearChips();

            // Only the TextField must remain
            Assert.AreEqual(1, field.childCount);
            Assert.IsInstanceOf<TextField>(field[0]);
        }

        // ── Text ──────────────────────────────────────────────────────────────

        [Test]
        public void Text_ReturnsTextFieldValue()
        {
            var field = new InlineChipField();
            // Access internal TextField via Q
            var tf = field.Q<TextField>();
            tf.value = "hello world";

            Assert.AreEqual("hello world", field.Text);
        }

        // ── Model sync ────────────────────────────────────────────────────────

        [Test]
        public void Model_SyncsWithField()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            field.AddChip(Chip("Assets/B.cs"));

            Assert.AreEqual(2, field.Model.Count);

            field.RemoveChipAt(0);
            Assert.AreEqual(1, field.Model.Count);

            field.ClearChips();
            Assert.AreEqual(0, field.Model.Count);
        }
    }
}
