// TDD tests for #8: StderrRingBuffer (pure — no Unity deps).
#if UNITY_MCP_CHAT
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatProcessTests
    {
        // Ring keeps only the last N lines in insertion order.
        [Test]
        public void Ring_KeepsLastN_InOrder()
        {
            var ring = new StderrRingBuffer(3);
            ring.Add("a"); ring.Add("b"); ring.Add("c"); ring.Add("d");
            var lines = ring.Lines.ToList();
            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual("b", lines[0]);
            Assert.AreEqual("c", lines[1]);
            Assert.AreEqual("d", lines[2]);
        }

        // Ring with fewer lines than capacity returns all in order.
        [Test]
        public void Ring_FewerThanCapacity_ReturnsAll()
        {
            var ring = new StderrRingBuffer(5);
            ring.Add("x"); ring.Add("y");
            var lines = ring.Lines.ToList();
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("x", lines[0]);
            Assert.AreEqual("y", lines[1]);
        }

        // Empty ring yields no lines.
        [Test]
        public void Ring_Empty_YieldsNoLines()
        {
            var ring = new StderrRingBuffer(5);
            Assert.AreEqual(0, ring.Lines.ToList().Count);
        }

        // BuildExitErrorMessage includes exit code.
        [Test]
        public void BuildExitErrorMessage_IncludesExitCode()
        {
            var msg = StderrRingBuffer.BuildExitErrorMessage(127, new[] { "not found" });
            StringAssert.Contains("127", msg);
        }

        // BuildExitErrorMessage includes stderr tail.
        [Test]
        public void BuildExitErrorMessage_IncludesStderrTail()
        {
            var msg = StderrRingBuffer.BuildExitErrorMessage(1, new[] { "line1", "line2" });
            StringAssert.Contains("line1", msg);
            StringAssert.Contains("line2", msg);
        }

        // Empty stderr: message is still valid (just exit code).
        [Test]
        public void BuildExitErrorMessage_EmptyStderr_IsValid()
        {
            var msg = StderrRingBuffer.BuildExitErrorMessage(1, new List<string>());
            StringAssert.Contains("1", msg);
            Assert.IsNotNull(msg);
        }
    }
}
#endif
