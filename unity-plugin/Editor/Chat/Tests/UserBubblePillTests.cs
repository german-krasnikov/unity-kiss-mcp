// TDD tests for P2: User bubble renders [kind:ref] tags as pills.
// Calls MixedParagraphRenderer.InlineElement directly — same codepath as ChatTranscript.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class UserBubblePillTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }

        [Test]
        public void Test_UserBubble_PlainText_RendersLabel()
        {
            var ve = MixedParagraphRenderer.InlineElement("hello world", "msg-text");
            Assert.IsTrue(ve.ClassListContains("msg-text"), "must have msg-text class");
            // No tag → no pill
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNull(pill, "plain text must not contain a pill");
        }

        [Test]
        public void Test_UserBubble_WithTag_RendersPill()
        {
            var ve = MixedParagraphRenderer.InlineElement("[hierarchy:/Player #1]", "msg-text");
            Assert.IsTrue(ve.ClassListContains("msg-text"), "must have msg-text class");
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "tag text must produce a pill with class inline-chip-pill");
        }

        [Test]
        public void Test_UserBubble_MixedTagAndText_ThreeChildren()
        {
            // "a [script:Foo.cs] b" → container with 3+ children (text + pill + text)
            var ve = MixedParagraphRenderer.InlineElement("a [script:Foo.cs] b", "msg-text");
            Assert.GreaterOrEqual(ve.childCount, 3,
                $"mixed content must produce 3+ children, got {ve.childCount}");
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "mixed content must contain a pill");
        }

        [Test]
        public void Test_UserBubble_EmptyText_NoCrash()
        {
            Assert.DoesNotThrow(() =>
            {
                var ve = MixedParagraphRenderer.InlineElement("", "msg-text");
                Assert.IsNotNull(ve);
            });
        }

        // ── ChatTranscript.AppendUserBubble chip strip ────────────────────────

        private static ChatTranscript MakeTranscript(out VisualElement container)
        {
            container = new VisualElement();
            var registry = ChatBlockRendererFactory.CreateDefault(null, null);
            return new ChatTranscript(container, registry);
        }

        [Test]
        public void AppendUserBubble_WithChips_ShowsChipStrip()
        {
            var t = MakeTranscript(out var container);
            var chips = new List<ChipData>
            {
                new ChipData(ChipKindKeys.Script, "Assets/Player.cs", "Player", 0),
                new ChipData(ChipKindKeys.Hierarchy, "/Player", "Enemy", 0),
            };

            t.AppendUserBubble("analyze @Player", chips);

            var strip = container.Q(className: "user-chip-strip");
            Assert.IsNotNull(strip, "bubble must contain a .user-chip-strip element");
            Assert.AreEqual(2, strip.childCount, "strip must contain one pill per chip");
        }

        [Test]
        public void AppendUserBubble_NoChips_NoChipStrip()
        {
            var t = MakeTranscript(out var container);

            t.AppendUserBubble("plain text");

            var strip = container.Q(className: "user-chip-strip");
            Assert.IsNull(strip, "no chips → no chip strip element");
        }

        [Test]
        public void AppendUserBubble_EmptyChipList_NoChipStrip()
        {
            var t = MakeTranscript(out var container);

            t.AppendUserBubble("plain text", new List<ChipData>());

            var strip = container.Q(className: "user-chip-strip");
            Assert.IsNull(strip, "empty chip list → no chip strip element");
        }
    }
}
