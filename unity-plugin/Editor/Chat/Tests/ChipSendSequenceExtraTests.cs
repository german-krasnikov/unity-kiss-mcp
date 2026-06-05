using System.Collections.Generic;
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

        private static ChipData H(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        private void InsertChip(ChipData chip, string displayName)
        {
            var tf = _chipField.TextField;
            int cursor = tf.cursorIndex;
            _chipField.AddChip(chip);
            var mention = "@" + displayName + " ";
            tf.value = (tf.value ?? "").Insert(cursor, mention);
            tf.selectIndex = tf.cursorIndex = cursor + mention.Length;
        }

        private void SetCursor(int pos) { _chipField.TextField.cursorIndex = pos; _chipField.TextField.selectIndex = pos; }
        private void Type(string text) { _chipField.Text = (_chipField.Text ?? "") + text; SetCursor(_chipField.Text.Length); }

        private (string turnJson, string rawText) SimulateSend()
        {
            var rawText  = (_chipField.Text ?? "").Trim();
            var snapshot = _chipField.Model.Count > 0 ? new List<ChipData>(_chipField.Model.Chips) : null;
            var llmText  = rawText;
            if (_chipField.Model.Count > 0)
            {
                var ctx = _chipField.Model.SerializePayload(_cfg);
                if (!string.IsNullOrEmpty(ctx)) llmText += "\n" + ctx;
            }
            if (string.IsNullOrEmpty(llmText)) return (null, rawText);
            var turnJson = UserTurnBuilder.Build(llmText);
            _transcript.AppendUserBubble(rawText, snapshot);
            _chipField.ClearChips();
            _chipField.Text = "";
            return (turnJson, rawText);
        }

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
