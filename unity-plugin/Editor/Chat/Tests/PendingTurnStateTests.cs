// TDD — PendingTurnState serialization contract.
// Zero Unity deps: System only. Pure NUnit.
// v4: KindKeys[] parallel to ChipPaths, interleaved B64 in chip lines.
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
            var orig = new PendingTurnState("s", "line1\nline2", new string[0], false, null, "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("line1\nline2", rt.Value.PendingText);
        }

        [Test]
        public void RoundTrip_AgentNameWithPipe_Preserved()
        {
            var orig = new PendingTurnState("sid", "text", new string[0], true, "foo|bar", "Sending");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt, "must not discard state when agentName contains a pipe");
            Assert.AreEqual("foo|bar", rt.Value.AgentName);
            Assert.AreEqual("Sending", rt.Value.ActivityPhase);
        }

        [Test]
        public void RoundTrip_ActivityPhaseWithPipe_Preserved()
        {
            var orig = new PendingTurnState("sid", "text", new string[0], false, "agent", "some|phase");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("some|phase", rt.Value.ActivityPhase);
        }

        // ── v2 field tests ────────────────────────────────────────────────────

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
            const string v1 = "sess1|dA==|0||SWRsZQ==|0";
            var rt = PendingTurnState.Deserialize(v1);
            Assert.IsNotNull(rt);
            Assert.AreEqual(-1, rt.Value.UndoGroupId);
            Assert.AreEqual(0L, rt.Value.SavedAtUtc);
        }

        [Test]
        public void Deserialize_V2Header_ParsesNewFields()
        {
            const string v2 = "sess1|dA==|0||SWRsZQ==|0|7|9999";
            var rt = PendingTurnState.Deserialize(v2);
            Assert.IsNotNull(rt);
            Assert.AreEqual(7,     rt.Value.UndoGroupId);
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

        // ── v3 BackendKind ────────────────────────────────────────────────────

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
            const string v2 = "sess1|dA==|0||SWRsZQ==|0|5|1717502400";
            var rt = PendingTurnState.Deserialize(v2);
            Assert.IsNotNull(rt);
            Assert.AreEqual(BackendKind.Claude, rt.Value.BackendKind);
            Assert.AreEqual(5,           rt.Value.UndoGroupId);
            Assert.AreEqual(1717502400L, rt.Value.SavedAtUtc);
        }

        [Test]
        public void Deserialize_V1Header_DefaultsBackendKindToClaude()
        {
            const string v1 = "sess1|dA==|0||SWRsZQ==|0";
            var rt = PendingTurnState.Deserialize(v1);
            Assert.IsNotNull(rt);
            Assert.AreEqual(BackendKind.Claude, rt.Value.BackendKind);
            Assert.AreEqual(-1, rt.Value.UndoGroupId);
            Assert.AreEqual(0L, rt.Value.SavedAtUtc);
        }

        // ── v4 KindKeys roundtrip ─────────────────────────────────────────────

        [Test]
        public void RoundTrip_V4_KindKeys_Preserved()
        {
            var orig = new PendingTurnState(
                sessionId: "s", pendingText: "t",
                chipPaths: new[] { "/Player", "Assets/Script.cs" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Script });

            var rt = PendingTurnState.Deserialize(orig.Serialize());

            Assert.IsNotNull(rt);
            Assert.AreEqual(2,                       rt.Value.KindKeys.Length);
            Assert.AreEqual(ChipKindKeys.Hierarchy,  rt.Value.KindKeys[0]);
            Assert.AreEqual(ChipKindKeys.Script,     rt.Value.KindKeys[1]);
        }

        [Test]
        public void RoundTrip_V4_ChipPaths_AlsoPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: "s", pendingText: "t",
                chipPaths: new[] { "/Enemy" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy });

            var rt = PendingTurnState.Deserialize(orig.Serialize());

            Assert.IsNotNull(rt);
            Assert.AreEqual("/Enemy", rt.Value.ChipPaths[0]);
        }

        [Test]
        public void V4_BackwardCompat_V3ChipLine_EmptyKindKey()
        {
            // Simulate a v3 chip line (just PathB64, no pipe separator)
            var pathB64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Assets/foo.fbx"));
            var textB64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("t"));
            var actB64  = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Idle"));
            var raw     = $"sess|{textB64}|0||{actB64}|1|5|100|0\n{pathB64}";

            var rt = PendingTurnState.Deserialize(raw);

            Assert.IsNotNull(rt);
            Assert.AreEqual("Assets/foo.fbx", rt.Value.ChipPaths[0]);
            Assert.AreEqual("",               rt.Value.KindKeys[0], "v3 chip line → empty KindKey (re-detect)");
        }

        [Test]
        public void V4_NoKindKeys_DefaultsToEmptyArray()
        {
            // PendingTurnState without kindKeys param → empty array
            var orig = new PendingTurnState("s", "t", new[] { "/P" }, false, "", "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            // v4 serialize always writes B64|B64; empty kindKey = "" encoded
            Assert.AreEqual(1, rt.Value.KindKeys.Length);
            Assert.AreEqual("", rt.Value.KindKeys[0]);
        }
    }
}
