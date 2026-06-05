// TDD tests for P2: User bubble renders [kind:ref] tags as pills.
// Calls MixedParagraphRenderer.InlineElement directly — same codepath as ChatTranscript.
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
    }
}
