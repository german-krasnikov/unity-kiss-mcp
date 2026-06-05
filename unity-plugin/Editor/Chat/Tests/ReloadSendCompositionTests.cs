using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ReloadSendCompositionTests
    {
        private InlineChipField _chipField;
        private ChatTranscript  _transcript;
        private VisualElement   _container;
        private ChipConfig      _cfg;
        private string          _tmpPath;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _chipField  = new InlineChipField();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container, ChatBlockRendererFactory.CreateDefault(null, null));
            _cfg        = new ChipConfig();
            _tmpPath    = Path.Combine(Path.GetTempPath(), $"ReloadSendComp_{System.Guid.NewGuid()}.txt");
            ReloadGuard.OverrideFilePath(_tmpPath);
            ReloadGuard.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
            ReloadGuard.ResetForTest();
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
        }

        static ChipData H(string path, string name, int id = 0) => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

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
            _transcript.AppendUserBubble(rawText, snapshot);
            _chipField.ClearChips();
            _chipField.Text = "";
            return (UserTurnBuilder.Build(llmText), rawText);
        }

        // ── Domain reload + send ──────────────────────────────────────────────

        [Test]
        public void ReloadDuringComposition_RestoreThenSend()
        {
            InsertChip(H("/Player", "Player", 1), "Player"); Type("fix this");
            var (paths, kindKeys) = _chipField.Model.SerializeForReload();
            var savedText = _chipField.Text;

            _chipField.ClearChips(); _chipField.Text = "";
            _chipField.Model.RestoreFromReload(paths, kindKeys);
            _chipField.Text = savedText;
            Type(" more");

            var (tj, _) = SimulateSend();
            Assert.IsNotNull(tj);
            StringAssert.Contains("[hierarchy:/Player", tj);
        }

        [Test]
        public void ReloadDuringComposition_ChipsAndTextPreserved()
        {
            InsertChip(H("/Player", "Player", 1), "Player"); Type("check");
            var (paths, kindKeys) = _chipField.Model.SerializeForReload();
            var savedText = _chipField.Text;

            _chipField.ClearChips(); _chipField.Text = "";
            _chipField.Model.RestoreFromReload(paths, kindKeys);
            _chipField.Text = savedText;

            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("/Player", _chipField.Model.Chips[0].Path);
            StringAssert.Contains("check", _chipField.Text);
        }

        [Test]
        public void ReloadDuringComposition_ThenTwoSends_Independent()
        {
            InsertChip(H("/Player", "Player", 1), "Player");
            var (paths, kindKeys) = _chipField.Model.SerializeForReload();

            _chipField.ClearChips(); _chipField.Text = "";
            _chipField.Model.RestoreFromReload(paths, kindKeys);
            Type("first"); SimulateSend();

            InsertChip(H("/Enemy", "Enemy", 2), "Enemy"); Type("second"); SimulateSend();

            var pills0 = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "user-chip-strip")
                .Query(className: "inline-chip-pill").ToList();
            ChatWindowAssertions.AssertPillContent(pills0[0], ChipKindKeys.Hierarchy, "Player");

            var pills1 = ChatWindowAssertions.GetUserBubble(_container, 1).Q(className: "user-chip-strip")
                .Query(className: "inline-chip-pill").ToList();
            ChatWindowAssertions.AssertPillContent(pills1[0], ChipKindKeys.Hierarchy, "Enemy");
        }
    }
}
