// Integration tests for chip-text-chip SEQUENCE — send flow, bubble rendering, cleanup.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipSendSequenceTests
    {
        private InlineChipField _chipField;
        private ChatTranscript  _transcript;
        private VisualElement   _container;
        private ChipConfig      _cfg;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _chipField  = new InlineChipField();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container, ChatBlockRendererFactory.CreateDefault(null, null));
            _cfg        = new ChipConfig();
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
        private (string, string) SimulateSend() => ChipTestHelpers.SimulateSend(_chipField, _transcript, _cfg);

        // ── Send: sequence preserved in payload ───────────────────────────────

        [Test]
        public void Send_ChipTextChip_RawTextHasSequence()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            var (_, raw) = SimulateSend();
            Assert.Less(raw.IndexOf("@Player"), raw.IndexOf("@Enemy"));
        }

        [Test]
        public void Send_ChipTextChip_LlmTextHasChipPayload()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            var (tj, _) = SimulateSend();
            StringAssert.Contains("[hierarchy:/Player #1]", tj);
            StringAssert.Contains("[hierarchy:/Enemy #2]",  tj);
        }

        [Test]
        public void Send_ChipTextChip_BubbleTextPreservesOrder()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            SimulateSend();
            var text = ChatWindowAssertions.GetUserBubble(_container, 0).userData as string ?? "";
            Assert.Less(text.IndexOf("@Player"), text.IndexOf("@Enemy"));
        }

        [Test]
        public void Send_ChipTextChip_ChipStripHasTwoPills()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            SimulateSend();
            ChatWindowAssertions.AssertBubbleHasChipStrip(ChatWindowAssertions.GetUserBubble(_container, 0), 2);
        }

        [Test]
        public void Send_ChipTextChip_ChipStripOrderMatchesModel()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            SimulateSend();
            var strip = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "user-chip-strip");
            var pills = strip.Query(className: "inline-chip-pill").ToList();
            ChatWindowAssertions.AssertPillContent(pills[0], ChipKindKeys.Hierarchy, "Player");
            ChatWindowAssertions.AssertPillContent(pills[1], ChipKindKeys.Hierarchy, "Enemy");
        }

        // ── Send: chips-only ──────────────────────────────────────────────────

        [Test]
        public void Send_TwoChipsNoText_BothInPayload()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            InsertChip(H("/Enemy",  "Enemy",  2), "Enemy");
            var (tj, _) = SimulateSend();
            StringAssert.Contains("[hierarchy:/Player #1]", tj);
            StringAssert.Contains("[hierarchy:/Enemy #2]",  tj);
        }

        [Test]
        public void Send_TwoChipsNoText_TextHasBothAtNames()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            InsertChip(H("/Enemy",  "Enemy",  2), "Enemy");
            var (_, raw) = SimulateSend();
            StringAssert.Contains("@Player", raw);
            StringAssert.Contains("@Enemy",  raw);
        }

        [Test]
        public void Send_EmptyTextOnlyChips_Sends()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            InsertChip(H("/Enemy",  "Enemy",  2), "Enemy");
            _chipField.Text = "";
            var (tj, _) = SimulateSend();
            Assert.IsNotNull(tj);
        }

        // ── Send: three chips interleaved ─────────────────────────────────────

        [Test]
        public void Send_ThreeChipsInterleaved_FullSequence()
        {
            Type("a "); InsertChip(H("/Player", "Player", 1), "Player");
            Type(" b "); InsertChip(H("/Enemy",  "Enemy",  2), "Enemy");
            Type(" c "); InsertChip(H("/Boss",   "Boss",   3), "Boss");
            var (tj, raw) = SimulateSend();
            Assert.Less(raw.IndexOf("@Player"), raw.IndexOf("@Enemy"));
            Assert.Less(raw.IndexOf("@Enemy"),  raw.IndexOf("@Boss"));
            StringAssert.Contains("[hierarchy:/Player #1]", tj);
            StringAssert.Contains("[hierarchy:/Enemy #2]",  tj);
            StringAssert.Contains("[hierarchy:/Boss #3]",   tj);
            ChatWindowAssertions.AssertBubbleHasChipStrip(ChatWindowAssertions.GetUserBubble(_container, 0), 3);
        }

        // ── Post-send: cleanup ────────────────────────────────────────────────

        [Test]
        public void Send_AfterChipTextChip_AllCleared()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2), "Enemy");
            SimulateSend();
            ChatWindowAssertions.AssertInputText(_chipField, "");
            Assert.AreEqual(0, _chipField.Model.Count);
            ChatWindowAssertions.AssertPillRowHidden(_chipField);
        }
    }
}
