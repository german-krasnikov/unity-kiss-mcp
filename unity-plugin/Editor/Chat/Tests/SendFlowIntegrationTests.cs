// Integration tests for OnSend data-flow pipeline.
// Tests data transformation directly — no window instantiation required.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SendFlowIntegrationTests
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
            var registry = ChatBlockRendererFactory.CreateDefault(null, null);
            _transcript = new ChatTranscript(_container, registry);
            _cfg        = new ChipConfig();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private static ChipData HierarchyChip(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        private static ChipData ScriptChip(string path, string name)
            => new ChipData(ChipKindKeys.Script, path, name, 0);

        // Replicates OnSend + AppendChipContext + DispatchTurn data flow.
        private (string turnJson, string rawText) SimulateSend()
        {
            var rawText      = (_chipField.Text ?? "").Trim();
            var chipSnapshot = _chipField.Model.Count > 0
                ? new List<ChipData>(_chipField.Model.Chips) : null;
            var llmText = rawText;

            if (_chipField.Model.Count > 0)
            {
                var context = _chipField.Model.SerializePayload(_cfg);
                if (!string.IsNullOrEmpty(context)) llmText += "\n" + context;
            }
            if (string.IsNullOrEmpty(llmText)) return (null, rawText);

            var turnJson = UserTurnBuilder.Build(llmText);
            _transcript.AppendUserBubble(rawText, chipSnapshot);
            _chipField.ClearChips();
            _chipField.Text = "";
            return (turnJson, rawText);
        }

        [Test]
        public void TextOnly_TurnJsonContainsText()
        {
            _chipField.Text = "hello world";
            var (turnJson, _) = SimulateSend();
            StringAssert.Contains("hello world", turnJson);
        }

        [Test]
        public void TextOnly_BubbleShowsText()
        {
            _chipField.Text = "hello world";
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleText(bubble, "hello world");
        }

        [Test]
        public void TextOnly_NoChipStrip()
        {
            _chipField.Text = "hello world";
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleHasNoChipStrip(bubble);
        }

        [Test]
        public void TextPlusOneChip_LlmTextHasChipContext()
        {
            _chipField.Text = "fix this";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 42));
            var (turnJson, _) = SimulateSend();
            // hierarchy chip with id=42 → [hierarchy:/Player #42]
            StringAssert.Contains("[hierarchy:/Player #42]", turnJson);
        }

        [Test]
        public void TextPlusOneChip_RawTextPreserved()
        {
            _chipField.Text = "fix this";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 42));
            var (_, rawText) = SimulateSend();
            // rawText must NOT contain bracket expansion
            Assert.IsFalse(rawText.Contains("[hierarchy:"), "rawText must not contain bracket tags");
            StringAssert.Contains("fix this", rawText);
        }

        [Test]
        public void TextPlusOneChip_BubbleHasChipStrip()
        {
            _chipField.Text = "fix this";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 42));
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleHasChipStrip(bubble, 1);
        }

        [Test]
        public void TextPlusThreeChips_AllChipRefsInPayload()
        {
            _chipField.Text = "analyze";
            _chipField.AddChip(HierarchyChip("/Player",  "Player",  1));
            _chipField.AddChip(HierarchyChip("/Enemy",   "Enemy",   2));
            _chipField.AddChip(ScriptChip("Assets/Health.cs", "Health"));
            var (turnJson, _) = SimulateSend();
            StringAssert.Contains("[hierarchy:/Player #1]",     turnJson);
            StringAssert.Contains("[hierarchy:/Enemy #2]",      turnJson);
            StringAssert.Contains("[script:Assets/Health.cs]",  turnJson);
        }

        [Test]
        public void TextPlusThreeChips_StripHasThreePills()
        {
            _chipField.Text = "analyze";
            _chipField.AddChip(HierarchyChip("/Player",  "Player",  1));
            _chipField.AddChip(HierarchyChip("/Enemy",   "Enemy",   2));
            _chipField.AddChip(ScriptChip("Assets/Health.cs", "Health"));
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleHasChipStrip(bubble, 3);
        }

        [Test]
        public void ChipsOnly_NoText_StillSends()
        {
            _chipField.Text = "";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            var (turnJson, _) = SimulateSend();
            Assert.IsNotNull(turnJson, "chips-only send must produce a turnJson");
        }

        [Test]
        public void ChipsOnly_BubbleStripPresent()
        {
            _chipField.Text = "";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleHasChipStrip(bubble, 1);
        }

        [Test]
        public void AfterSend_InputCleared()
        {
            _chipField.Text = "some text";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            SimulateSend();
            ChatWindowAssertions.AssertInputText(_chipField, "");
            Assert.AreEqual(0, _chipField.Model.Count, "Model must be empty after send");
        }

        [Test]
        public void AfterSend_PillRowHidden()
        {
            _chipField.Text = "some text";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            SimulateSend();
            ChatWindowAssertions.AssertPillRowHidden(_chipField);
        }

        [Test]
        public void SentBubble_UserDataMatchesRawText()
        {
            const string msg = "check the player";
            _chipField.Text = msg;
            _chipField.AddChip(HierarchyChip("/Player", "Player", 99));
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            Assert.AreEqual(msg, bubble.userData as string,
                "bubble.userData must equal rawText (not llmText with bracket expansion)");
        }
    }
}
