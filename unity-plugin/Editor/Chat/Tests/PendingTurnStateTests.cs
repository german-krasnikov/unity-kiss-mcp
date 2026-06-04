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

        // ── v2 field tests (#12 / #26) ────────────────────────────────────────

        [Test]
        public void RoundTrip_V2Fields_UndoGroupIdAndTimestamp()
        {
            var orig = new PendingTurnState("s", "t", new string[0], false, null, "Idle",
                undoGroupId: 42, savedAtUtc: 1717502400L);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(42,          rt.Value.UndoGroupId);
            Assert.AreEqual(1717502400L, rt.Value.SavedAtUtc);
        }

        [Test]
        public void Deserialize_V1Header_DefaultsNewFields()
        {
            // Hand-crafted v1 header (6 fields): sess1|dA==|0||SWRsZQ==|0
            // B64("t")="dA==", B64("")="", B64("Idle")="SWRsZQ=="
            const string v1 = "sess1|dA==|0||SWRsZQ==|0";
            var rt = PendingTurnState.Deserialize(v1);
            Assert.IsNotNull(rt);
            Assert.AreEqual(-1, rt.Value.UndoGroupId, "legacy file must default UndoGroupId to -1");
            Assert.AreEqual(0L, rt.Value.SavedAtUtc,  "legacy file must default SavedAtUtc to 0");
        }

        [Test]
        public void Deserialize_V2Header_ParsesNewFields()
        {
            // Hand-crafted v2 header (8 fields): appends |7|9999
            const string v2 = "sess1|dA==|0||SWRsZQ==|0|7|9999";
            var rt = PendingTurnState.Deserialize(v2);
            Assert.IsNotNull(rt);
            Assert.AreEqual(7,    rt.Value.UndoGroupId);
            Assert.AreEqual(9999L, rt.Value.SavedAtUtc);
        }

        [Test]
        public void RoundTrip_NegativeUndoGroupId_Preserved()
        {
            var orig = new PendingTurnState("s", "t", new string[0], false, null, "Idle",
                undoGroupId: -1, savedAtUtc: 0L);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(-1, rt.Value.UndoGroupId);
        }

        // ── v3 BackendKind (reload-survival) ──────────────────────────────────

        [Test]
        public void RoundTrip_BackendKind_Codex_Persists()
        {
            var orig = new PendingTurnState("sess-codex", "do stuff", new string[0], true, "agent",
                "Sending", undoGroupId: 0, savedAtUtc: 1000L, backendKind: BackendKind.Codex);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(BackendKind.Codex, rt.Value.BackendKind);
            Assert.AreEqual("sess-codex",      rt.Value.SessionId);
            Assert.AreEqual("do stuff",        rt.Value.PendingText);
        }

        [Test]
        public void Deserialize_V2Header_DefaultsBackendKindToClaude()
        {
            // Hand-crafted v2 header (8 pipe-fields, NO BackendKind field).
            // B64("t")="dA==", B64("")="", B64("Idle")="SWRsZQ=="
            // Fields: SessionId|PendingTextB64|AgentMode|AgentNameB64|ActivityPhaseB64|ChipCount|UndoGroupId|SavedAtUtc
            const string v2 = "sess1|dA==|0||SWRsZQ==|0|5|1717502400";
            var rt = PendingTurnState.Deserialize(v2);
            Assert.IsNotNull(rt);
            Assert.AreEqual(BackendKind.Claude, rt.Value.BackendKind,
                "v2 state without BackendKind field must default to Claude");
            Assert.AreEqual(5,           rt.Value.UndoGroupId);
            Assert.AreEqual(1717502400L, rt.Value.SavedAtUtc);
        }

        [Test]
        public void Deserialize_V1Header_DefaultsBackendKindToClaude()
        {
            // v1: 6 fields — no UndoGroupId, SavedAtUtc, or BackendKind
            const string v1 = "sess1|dA==|0||SWRsZQ==|0";
            var rt = PendingTurnState.Deserialize(v1);
            Assert.IsNotNull(rt);
            Assert.AreEqual(BackendKind.Claude, rt.Value.BackendKind);
            Assert.AreEqual(-1, rt.Value.UndoGroupId);
            Assert.AreEqual(0L, rt.Value.SavedAtUtc);
        }
    }
}
