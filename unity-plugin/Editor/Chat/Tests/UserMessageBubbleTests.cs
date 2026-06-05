// TDD — Group D: ChatTranscript.AppendUserBubble(UserMessage) rendering tests.
// Verifies Bug 1 fix: interleaved segments, no strip-at-top double display.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class UserMessageBubbleTests
    {
        private ChatTranscript _transcript;
        private VisualElement  _container;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container,
                ChatBlockRendererFactory.CreateDefault(null, null));
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private static ChipData H(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        private static PositionedChip PC(ChipData chip, int offset)
            => new PositionedChip(chip, offset);

        private VisualElement Bubble(int i = 0) => ChatWindowAssertions.GetUserBubble(_container, i);

        // D1: text-only UserMessage → content container has label child
        [Test]
        public void D1_TextOnly_ContentContainerHasLabel()
        {
            var msg = ChipTextInterleaver.Build("hello world", new List<PositionedChip>());
            _transcript.AppendUserBubble(msg);
            var b     = Bubble();
            var wrap  = b.Q(className: "msg-user-content");
            Assert.IsNotNull(wrap, "Should have msg-user-content container");
            var labels = wrap.Query<Label>().ToList();
            Assert.AreEqual(1, labels.Count);
            Assert.AreEqual("hello world", labels[0].text);
            Assert.IsTrue(labels[0].ClassListContains("msg-text"), "Text label must have msg-text class for correct USS styling");
        }

        // D2: chip-only UserMessage → no text labels, one pill
        [Test]
        public void D2_ChipOnly_OnePillNoLabel()
        {
            var chip = H("/A", "A", 1);
            var msg  = ChipTextInterleaver.Build("", new List<PositionedChip> { PC(chip, 0) });
            _transcript.AppendUserBubble(msg);
            var wrap  = Bubble().Q(className: "msg-user-content");
            Assert.IsNotNull(wrap);
            var pills  = wrap.Query(className: "inline-chip-pill").ToList();
            int directLabels = 0;
            foreach (var child in wrap.Children())
                if (child is Label) directLabels++;
            Assert.AreEqual(1, pills.Count);
            Assert.AreEqual(0, directLabels);
        }

        // D3: text+chip+text → 3 children in order: Label, pill, Label
        [Test]
        public void D3_TextChipText_ThreeChildrenInOrder()
        {
            var chip = H("/A", "A", 1);
            var msg  = ChipTextInterleaver.Build("before after", new List<PositionedChip> { PC(chip, 6) });
            // "before" text, chip, " after" text
            _transcript.AppendUserBubble(msg);
            var wrap     = Bubble().Q(className: "msg-user-content");
            Assert.IsNotNull(wrap);
            Assert.GreaterOrEqual(wrap.childCount, 3);
            // First child should be a label
            Assert.IsInstanceOf<Label>(wrap[0], "First child should be a label");
            // Second child should contain a pill
            Assert.IsNotNull(wrap[1].Q(className: "inline-chip-pill"), "Second child should be a pill");
            // Third child should be a label
            Assert.IsInstanceOf<Label>(wrap[2], "Third child should be a label");
        }

        // D4: chip+text+chip → no strip-at-top; interleaved
        [Test]
        public void D4_ChipTextChip_Interleaved_NoStripAtTop()
        {
            var chipA = H("/A", "A", 1);
            var chipB = H("/B", "B", 2);
            var pos   = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 5) };
            var msg   = ChipTextInterleaver.Build("hello world", pos);
            _transcript.AppendUserBubble(msg);
            var b = Bubble();
            // No user-chip-strip (that's the old layout)
            Assert.IsNull(b.Q(className: "user-chip-strip"),
                "F13: no strip-at-top; chips are interleaved");
            // But msg-user-content exists with pills
            var wrap  = b.Q(className: "msg-user-content");
            Assert.IsNotNull(wrap);
            var pills = wrap.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(2, pills.Count);
        }

        // D5: empty text segments between chips are omitted (no blank Labels)
        [Test]
        public void D5_EmptyTextSegmentOmitted_NoBlankLabels()
        {
            // chip at offset 0 in empty text → only chip, empty text filtered out
            var chip = H("/A", "A", 1);
            var pos  = new List<PositionedChip> { PC(chip, 0) };
            var msg  = ChipTextInterleaver.Build("", pos);
            _transcript.AppendUserBubble(msg);
            var wrap = Bubble().Q(className: "msg-user-content");
            int directLabels = 0;
            foreach (var child in wrap.Children())
                if (child is Label) directLabels++;
            Assert.AreEqual(0, directLabels, "Empty text segments should not produce Labels");
        }

        // D6: userData on bubble = plain text only (no chip tokens)
        [Test]
        public void D6_BubbleUserData_IsPlainText()
        {
            var chip = H("/Player", "Player", 1);
            var pos  = new List<PositionedChip> { PC(chip, 5) };
            var msg  = ChipTextInterleaver.Build("fix health now", pos);
            _transcript.AppendUserBubble(msg);
            var userData = Bubble().userData as string ?? "";
            Assert.IsFalse(userData.Contains("@"), "userData should not contain @mention tokens");
            StringAssert.Contains("fix", userData);
            StringAssert.Contains("health now", userData);
        }

        // D7: bubble has msg-bubble and msg-bubble--user classes
        [Test]
        public void D7_BubbleHasCorrectClasses()
        {
            var msg = ChipTextInterleaver.Build("hi", new List<PositionedChip>());
            _transcript.AppendUserBubble(msg);
            var b = Bubble();
            Assert.IsTrue(b.ClassListContains("msg-bubble"));
            Assert.IsTrue(b.ClassListContains("msg-bubble--user"));
        }

        // D8: inline pill label matches chip display name
        [Test]
        public void D8_InlinePillLabel_MatchesDisplayName()
        {
            var chip = H("/Player", "Player", 1);
            var msg = ChipTextInterleaver.Build("fix", new List<PositionedChip> { PC(chip, 3) });
            _transcript.AppendUserBubble(msg);
            var wrap = Bubble().Q(className: "msg-user-content");
            var pill = wrap.Query(className: "inline-chip-pill").First();
            Assert.IsNotNull(pill);
            ChatWindowAssertions.AssertPillContent(pill, ChipKindKeys.Hierarchy, "Player");
        }

    }
}
