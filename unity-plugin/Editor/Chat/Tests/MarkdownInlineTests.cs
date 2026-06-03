// Tests for MarkdownInline. Pure, NUnit-testable.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MarkdownInlineTests
    {
        [Test]
        public void Null_DoesNotThrow()
        {
            string result = null;
            Assert.DoesNotThrow(() => result = MarkdownInline.ToRichText(null));
            // null or empty is acceptable
            Assert.IsTrue(result == null || result == "");
        }

        [Test]
        public void Empty_ReturnsEmpty()
        {
            Assert.AreEqual("", MarkdownInline.ToRichText(""));
        }

        [Test]
        public void Bold_DoubleStar_WrapsB()
        {
            var result = MarkdownInline.ToRichText("**hello**");
            Assert.IsTrue(result.Contains("<b>hello</b>"), $"Got: {result}");
        }

        [Test]
        public void Italic_SingleStar_WrapsI()
        {
            var result = MarkdownInline.ToRichText("*world*");
            Assert.IsTrue(result.Contains("<i>world</i>"), $"Got: {result}");
        }

        [Test]
        public void InlineCode_Backticks_Colored()
        {
            var result = MarkdownInline.ToRichText("`foo`");
            Assert.IsTrue(result.Contains("<color=#9aa5ce>") && result.Contains("foo"), $"Got: {result}");
        }

        [Test]
        public void EscapesAngleBrackets_BeforeTags()
        {
            // <b>hi</b> must NOT become bold — angle brackets must be escaped
            var result = MarkdownInline.ToRichText("<b>hi</b>");
            Assert.IsTrue(result.Contains("&lt;b&gt;"), $"Got: {result}");
            // must NOT contain a raw <b> tag from the input
            Assert.IsFalse(result.Contains("<b>hi</b>"), $"Raw tag leaked: {result}");
        }

        [Test]
        public void CodeSpanProtectsInnerStars()
        {
            // Stars inside a code span must NOT be bold/italic
            var result = MarkdownInline.ToRichText("`*not bold*`");
            Assert.IsFalse(result.Contains("<b>"), $"Should not have bold: {result}");
            Assert.IsFalse(result.Contains("<i>"), $"Should not have italic: {result}");
            Assert.IsTrue(result.Contains("*not bold*"), $"Stars should survive: {result}");
        }

        [Test]
        public void Link_RendersColoredText()
        {
            var result = MarkdownInline.ToRichText("[click](http://example.com)");
            Assert.IsTrue(result.Contains("click"), $"Got: {result}");
            Assert.IsTrue(result.Contains("http://example.com"), $"URL missing: {result}");
            Assert.IsTrue(result.Contains("<color="), $"URL not colored: {result}");
        }

        [Test]
        public void UnpairedStar_LeftLiteral()
        {
            var result = MarkdownInline.ToRichText("hello * world");
            Assert.IsTrue(result.Contains("*"), $"Star should survive: {result}");
            Assert.IsFalse(result.Contains("<i>"), $"Should not italicize: {result}");
        }

        [Test]
        public void BoldItalicNested()
        {
            var result = MarkdownInline.ToRichText("**_nested_**");
            Assert.IsTrue(result.Contains("<b>"), $"Got: {result}");
        }
    }
}
