// TDD — RED first. Tests drive PendingTurnState serialization contract.
// Zero Unity deps: System only. Pure NUnit.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PendingTurnStateTests
    {
        [Test]
        public void RoundTrip_AllFields_Match()
        {
            var orig = new PendingTurnState(
                sessionId:    "sess-abc123",
                pendingText:  "hello world",
                chipPaths:    new[] { "/Player/Sword", "/Enemy" },
                agentMode:    true,
                agentName:    "code-reviewer",
                activityPhase: "Sending");

            var serialized = orig.Serialize();
            var rt = PendingTurnState.Deserialize(serialized);

            Assert.IsNotNull(rt);
            Assert.AreEqual("sess-abc123",    rt.Value.SessionId);
            Assert.AreEqual("hello world",    rt.Value.PendingText);
            Assert.AreEqual("/Player/Sword",  rt.Value.ChipPaths[0]);
            Assert.AreEqual("/Enemy",         rt.Value.ChipPaths[1]);
            Assert.IsTrue(rt.Value.AgentMode);
            Assert.AreEqual("code-reviewer",  rt.Value.AgentName);
            Assert.AreEqual("Sending",        rt.Value.ActivityPhase);
        }

        [Test]
        public void RoundTrip_EmptyChips_Match()
        {
            var orig = new PendingTurnState("s", "t", new string[0], false, null, "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(0, rt.Value.ChipPaths.Length);
        }

        [Test]
        public void RoundTrip_NullAgentName_IsEmpty()
        {
            var orig = new PendingTurnState("s", "t", new string[0], false, null, "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            // null agent preserved as empty string or null — both acceptable, just not throw
            Assert.IsTrue(rt.Value.AgentName == null || rt.Value.AgentName == "");
        }

        [Test]
        public void Deserialize_Null_ReturnsNull()
            => Assert.IsNull(PendingTurnState.Deserialize(null));

        [Test]
        public void Deserialize_EmptyString_ReturnsNull()
            => Assert.IsNull(PendingTurnState.Deserialize(""));

        [Test]
        public void Deserialize_CorruptedData_ReturnsNull()
            => Assert.IsNull(PendingTurnState.Deserialize("not|valid|corrupt"));

        [Test]
        public void Serialize_IsPlainText_NoBraces()
        {
            // Verify it's NOT JSON — no curly braces.
            var s = new PendingTurnState("s", "t", new string[0], false, null, "Idle").Serialize();
            Assert.IsFalse(s.Contains("{"), "must be plain text, not JSON");
        }

        [Test]
        public void Serialize_IsPipeDelimited()
        {
            var s = new PendingTurnState("s", "t", new string[0], false, null, "Idle").Serialize();
            Assert.IsTrue(s.Contains("|"), "must be pipe-delimited");
        }

        [Test]
        public void RoundTrip_TextWithNewlines_Preserved()
        {
            // Text may contain newlines from multiline input
            var orig = new PendingTurnState("s", "line1\nline2", new string[0], false, null, "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("line1\nline2", rt.Value.PendingText);
        }

        // ── New: pipe character in AgentName must not corrupt header ─────────

        [Test]
        public void RoundTrip_AgentNameWithPipe_Preserved()
        {
            // "foo|bar" in AgentName would break int.Parse(header[5]) if not B64-encoded.
            var orig = new PendingTurnState("sid", "text", new string[0], true, "foo|bar", "Sending");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt, "must not discard state when agentName contains a pipe");
            Assert.AreEqual("foo|bar", rt.Value.AgentName);
            Assert.AreEqual("Sending", rt.Value.ActivityPhase);
        }

        [Test]
        public void RoundTrip_ActivityPhaseWithPipe_Preserved()
        {
            // Defense-in-depth: ActivityPhase also B64-encoded.
            var orig = new PendingTurnState("sid", "text", new string[0], false, "agent", "some|phase");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt, "must not discard state when activityPhase contains a pipe");
            Assert.AreEqual("some|phase", rt.Value.ActivityPhase);
        }
    }
}
