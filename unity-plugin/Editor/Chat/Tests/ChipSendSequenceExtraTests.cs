using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipSendSequenceExtraTests
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

        [Test]
        public void Send_MixedKinds_AllBracketsInPayload()
        {
            _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/Player", "Player", 1));
            _chipField.AddChip(new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo", 0));
            _chipField.AddChip(new ChipData(ChipKindKeys.Asset, "Assets/Tex.png", "Tex", 0));
            Type("check");
            var (tj, _) = SimulateSend();
            StringAssert.Contains("[hierarchy:/Player #1]", tj);
            StringAssert.Contains("[script:Assets/Foo.cs]", tj);
            StringAssert.Contains("[asset:Assets/Tex.png]", tj);
        }

        [Test]
        public void Send_FiveChipsAllKinds_PayloadNewlineSeparated()
        {
            _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            _chipField.AddChip(new ChipData(ChipKindKeys.Script,    "Assets/B.cs",    "B", 0));
            _chipField.AddChip(new ChipData(ChipKindKeys.Prefab,    "Assets/C.prefab", "C", 0));
            _chipField.AddChip(new ChipData(ChipKindKeys.Material,  "Assets/D.mat",   "D", 0));
            _chipField.AddChip(new ChipData(ChipKindKeys.Asset,     "Assets/E.fbx",   "E", 0));
            Type("go");
            var (tj, _) = SimulateSend();
            var payloadSection = tj.Substring(tj.IndexOf('\n') + 1);
            Assert.GreaterOrEqual(payloadSection.Split('\n').Length, 4);
        }

        [Test]
        public void Send_PostSend_PillRowChildCountZero()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            SimulateSend();
            Assert.AreEqual(0, _chipField[0].Query(className: "inline-chip-pill").ToList().Count);
        }

        [Test]
        public void Send_PostSend_TranscriptHasOneBubble()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            SimulateSend();
            ChatWindowAssertions.AssertUserBubbleCount(_container, 1);
        }
    }
}
