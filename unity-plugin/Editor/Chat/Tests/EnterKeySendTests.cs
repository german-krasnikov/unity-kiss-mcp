// Tests for EnterKeyLogic pure helpers. NUnit-testable.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class EnterKeySendTests
    {
        [Test]
        public void Classify_Return_NoAlt_Send()
        {
            Assert.AreEqual(EnterAction.Send, EnterKeyLogic.Classify(true, false));
        }

        [Test]
        public void Classify_Return_Alt_Newline()
        {
            Assert.AreEqual(EnterAction.Newline, EnterKeyLogic.Classify(true, true));
        }

        [Test]
        public void Classify_KeypadEnter_NoAlt_Send()
        {
            // isReturnOrKeypadEnter=true covers both Return and KeypadEnter
            Assert.AreEqual(EnterAction.Send, EnterKeyLogic.Classify(true, false));
        }

        [Test]
        public void Classify_NotReturn_Ignore()
        {
            Assert.AreEqual(EnterAction.Ignore, EnterKeyLogic.Classify(false, false));
            Assert.AreEqual(EnterAction.Ignore, EnterKeyLogic.Classify(false, true));
        }

        [Test]
        public void InsertNewline_AtEnd_AppendsAndAdvancesCaret()
        {
            var (text, caret) = EnterKeyLogic.InsertNewline("hello", 5);
            Assert.AreEqual("hello\n", text);
            Assert.AreEqual(6, caret);
        }

        [Test]
        public void InsertNewline_MidString_InsertsAtCaret()
        {
            var (text, caret) = EnterKeyLogic.InsertNewline("ab", 1);
            Assert.AreEqual("a\nb", text);
            Assert.AreEqual(2, caret);
        }

        [Test]
        public void InsertNewline_EmptyString_CaretOne()
        {
            var (text, caret) = EnterKeyLogic.InsertNewline("", 0);
            Assert.AreEqual("\n", text);
            Assert.AreEqual(1, caret);
        }
    }
}
