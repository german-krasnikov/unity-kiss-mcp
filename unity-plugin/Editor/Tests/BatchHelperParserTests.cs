// NUnit tests for BatchHelper.ParseLine / ParseLines — CS2.test.4.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BatchHelperParserTests
    {
        // ── ParseLine ─────────────────────────────────────────────────────────

        [Test]
        public void ParseLine_CommandOnly_ReturnsEmptyArgs()
        {
            var (cmd, args) = BatchHelper.ParseLine("create_object");
            Assert.AreEqual("create_object", cmd);
            Assert.AreEqual("{}", args);
        }

        [Test]
        public void ParseLine_UnquotedValue_ExtractedCorrectly()
        {
            var (cmd, args) = BatchHelper.ParseLine("set_active path=/Player value=true");
            Assert.AreEqual("set_active", cmd);
            StringAssert.Contains("\"path\"", args);
            StringAssert.Contains("/Player", args);
            StringAssert.Contains("true", args);
        }

        [Test]
        public void ParseLine_QuotedValue_WithSpaces()
        {
            var (cmd, args) = BatchHelper.ParseLine("create_object name=\"my object\"");
            Assert.AreEqual("create_object", cmd);
            StringAssert.Contains("my object", args);
        }

        [Test]
        public void ParseLine_ParenVector_PreservedAsToken()
        {
            var (cmd, args) = BatchHelper.ParseLine("set_property path=/X value=(1,2,3)");
            Assert.AreEqual("set_property", cmd);
            StringAssert.Contains("(1,2,3)", args);
        }

        [Test]
        public void ParseLine_Empty_ReturnsNullCmd()
        {
            var (cmd, _) = BatchHelper.ParseLine("");
            Assert.IsNull(cmd);
        }

        [Test]
        public void ParseLine_MultipleArgs_AllPresent()
        {
            var (cmd, args) = BatchHelper.ParseLine("set_property path=/A component=Transform prop=m_LocalPosition value=(0,0,0)");
            Assert.AreEqual("set_property", cmd);
            StringAssert.Contains("path", args);
            StringAssert.Contains("component", args);
            StringAssert.Contains("prop", args);
            StringAssert.Contains("value", args);
        }

        // ── ParseLines ────────────────────────────────────────────────────────

        [Test]
        public void ParseLines_SkipsComments()
        {
            var lines = "# this is a comment\ncreate_object name=A";
            var result = BatchHelper.ParseLines(lines);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("create_object", result[0].cmd);
        }

        [Test]
        public void ParseLines_SkipsBlankLines()
        {
            var lines = "\n\ncreate_object name=A\n\n";
            var result = BatchHelper.ParseLines(lines);
            Assert.AreEqual(1, result.Count);
        }

        [Test]
        public void ParseLines_MultipleCommands_AllParsed()
        {
            var lines = "create_object name=A\nset_active path=/A value=false\ndelete_object path=/A";
            var result = BatchHelper.ParseLines(lines);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("create_object", result[0].cmd);
            Assert.AreEqual("set_active", result[1].cmd);
            Assert.AreEqual("delete_object", result[2].cmd);
        }

        [Test]
        public void ParseLines_NullInput_ReturnsEmpty()
        {
            var result = BatchHelper.ParseLines(null);
            Assert.AreEqual(0, result.Count);
        }
    }
}
