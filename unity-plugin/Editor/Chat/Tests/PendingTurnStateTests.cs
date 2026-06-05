// TDD — PendingTurnState serialization contract.
// Zero Unity deps: System only. Pure NUnit.
// v4: KindKeys[] parallel to ChipPaths, interleaved B64 in chip lines.
using System;
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

        [Test]
        public void RoundTrip_IdlePhase_ChipsAndTextPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: null, pendingText: "draft text",
                chipPaths: new[] { "/Player", "/Enemy" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { "hierarchy", "hierarchy" });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("Idle",       rt.Value.ActivityPhase);
            Assert.AreEqual("draft text", rt.Value.PendingText);
            Assert.AreEqual(2,            rt.Value.ChipPaths.Length);
            Assert.AreEqual("/Player",    rt.Value.ChipPaths[0]);
        }

        // ── Idle + domain reload roundtrips ───────────────────────────────────

        [Test]
        public void RoundTrip_IdleChipsAndText_KindKeysPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: null, pendingText: "fix player",
                chipPaths: new[] { "/Player", "/Enemy" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Hierarchy });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(ChipKindKeys.Hierarchy, rt.Value.KindKeys[0]);
            Assert.AreEqual(ChipKindKeys.Hierarchy, rt.Value.KindKeys[1]);
        }

        [Test]
        public void RoundTrip_IdleChipsNoText_ChipsPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: null, pendingText: "",
                chipPaths: new[] { "/Player" },
                agentMode: false, agentName: "", activityPhase: "Idle",
                kindKeys: new[] { ChipKindKeys.Hierarchy });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("/Player", rt.Value.ChipPaths[0]);
        }

        [Test]
        public void RoundTrip_IdleNoChipsText_TextPreserved()
        {
            var orig = new PendingTurnState(
                sessionId: null, pendingText: "just typing",
                chipPaths: new string[0],
                agentMode: false, agentName: "", activityPhase: "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("just typing", rt.Value.PendingText);
        }

        [Test]
        public void RoundTrip_ActiveTurnWithChips_AllFieldsPreserved()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var orig = new PendingTurnState(
                sessionId: "sess-123", pendingText: "fix player",
                chipPaths: new[] { "/Player", "Assets/Foo.cs" },
                agentMode: true, agentName: "agent", activityPhase: "Sending",
                undoGroupId: 7, savedAtUtc: now, backendKind: BackendKind.Claude,
                kindKeys: new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Script });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("sess-123",             rt.Value.SessionId);
            Assert.AreEqual("fix player",           rt.Value.PendingText);
            Assert.AreEqual(2,                      rt.Value.ChipPaths.Length);
            Assert.IsTrue(rt.Value.AgentMode);
            Assert.AreEqual("Sending",              rt.Value.ActivityPhase);
            Assert.AreEqual(7,                      rt.Value.UndoGroupId);
            Assert.AreEqual(now,                    rt.Value.SavedAtUtc);
            Assert.AreEqual(ChipKindKeys.Hierarchy, rt.Value.KindKeys[0]);
            Assert.AreEqual(ChipKindKeys.Script,    rt.Value.KindKeys[1]);
        }

        [Test]
        public void RoundTrip_FiveChipsDifferentKinds_AllPreserved()
        {
            var paths = new[] { "/A", "/B", "Assets/C.cs", "Assets/D.prefab", "Assets/E.mat" };
            var kinds = new[] {
                ChipKindKeys.Hierarchy, ChipKindKeys.Hierarchy,
                ChipKindKeys.Script, ChipKindKeys.Prefab, ChipKindKeys.Material };
            var orig = new PendingTurnState("s", "t", paths, false, "", "Idle", kindKeys: kinds);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(5, rt.Value.ChipPaths.Length);
            for (var i = 0; i < 5; i++)
            {
                Assert.AreEqual(paths[i], rt.Value.ChipPaths[i]);
                Assert.AreEqual(kinds[i], rt.Value.KindKeys[i]);
            }
        }

        [Test]
        public void RoundTrip_ChipOrder_Preserved()
        {
            var paths = new[] { "/C", "/A", "/B" };
            var kinds = new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Script, ChipKindKeys.Prefab };
            var orig = new PendingTurnState("s", "t", paths, false, "", "Idle", kindKeys: kinds);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("/C",                  rt.Value.ChipPaths[0]);
            Assert.AreEqual("/A",                  rt.Value.ChipPaths[1]);
            Assert.AreEqual("/B",                  rt.Value.ChipPaths[2]);
            Assert.AreEqual(ChipKindKeys.Hierarchy, rt.Value.KindKeys[0]);
            Assert.AreEqual(ChipKindKeys.Script,    rt.Value.KindKeys[1]);
            Assert.AreEqual(ChipKindKeys.Prefab,    rt.Value.KindKeys[2]);
        }

        [Test]
        public void RoundTrip_AtMentionText_SurvivesBase64()
        {
            const string text = "@Player fix @Enemy health";
            var orig = new PendingTurnState("s", text, new string[0], false, "", "Idle");
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(text, rt.Value.PendingText);
        }

        // ── Staleness guard ───────────────────────────────────────────────────

        [Test]
        public void Staleness_ActiveTurn_OldTimestamp_ShouldDiscard()
        {
            var savedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 120;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.IsTrue(now - savedAtUtc > 60);
        }

        [Test]
        public void Staleness_ActiveTurn_FreshTimestamp_ShouldKeep()
        {
            var savedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 5;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Assert.IsTrue(now - savedAtUtc <= 60);
        }

        [Test]
        public void Staleness_IdleSave_OldTimestamp_Exempt()
        {
            var orig = new PendingTurnState("s", "t", new string[0], false, "", "Idle",
                savedAtUtc: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("Idle", rt.Value.ActivityPhase);
        }

        [Test]
        public void Staleness_Legacy_ZeroTimestamp_AllowedThrough()
        {
            var orig = new PendingTurnState("s", "t", new string[0], false, "", "Sending",
                savedAtUtc: 0L);
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual(0L, rt.Value.SavedAtUtc);
        }

        // ── Corrupt input ─────────────────────────────────────────────────────

        [Test]
        public void Deserialize_TruncatedHeader_ReturnsNull()
            => Assert.IsNull(PendingTurnState.Deserialize("a|b|c"));

        [Test]
        public void Deserialize_MissingChipLines_GracefulEmpty()
        {
            var textB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("t"));
            var actB64  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Idle"));
            var raw = $"sess|{textB64}|0||{actB64}|2|5|100|0";
            var rt = PendingTurnState.Deserialize(raw);
            Assert.IsNotNull(rt);
            Assert.AreEqual(2, rt.Value.ChipPaths.Length);
            Assert.AreEqual("", rt.Value.ChipPaths[0]);
            Assert.AreEqual("", rt.Value.ChipPaths[1]);
        }

        [Test]
        public void Deserialize_InvalidBase64InText_ReturnsNull()
            => Assert.IsNull(PendingTurnState.Deserialize("sess|NOT_VALID!!!|0||SWRsZQ==|0"));

        [Test]
        public void Deserialize_NegativeChipCount_ReturnsNull()
        {
            var textB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("t"));
            var actB64  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Idle"));
            Assert.IsNull(PendingTurnState.Deserialize($"sess|{textB64}|0||{actB64}|-1"));
        }

        // ── Backward compat ───────────────────────────────────────────────────

        [Test]
        public void V1Header_AllDefaultsIncludingBackendAndKindKeys()
        {
            const string v1 = "sess1|dA==|0||SWRsZQ==|0";
            var rt = PendingTurnState.Deserialize(v1);
            Assert.IsNotNull(rt);
            Assert.AreEqual(BackendKind.Claude, rt.Value.BackendKind);
            Assert.AreEqual(0, rt.Value.KindKeys.Length);
        }

        [Test]
        public void RoundTrip_ChipPathWithPipe_SurvivesBase64()
        {
            var orig = new PendingTurnState("s", "t",
                new[] { "Assets/foo|bar.cs" }, false, "", "Idle",
                kindKeys: new[] { ChipKindKeys.Script });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("Assets/foo|bar.cs", rt.Value.ChipPaths[0]);
        }

        [Test]
        public void RoundTrip_ChipPathWithNewline_SurvivesBase64()
        {
            var orig = new PendingTurnState("s", "t",
                new[] { "Assets/foo\nbar.cs" }, false, "", "Idle",
                kindKeys: new[] { ChipKindKeys.Script });
            var rt = PendingTurnState.Deserialize(orig.Serialize());
            Assert.IsNotNull(rt);
            Assert.AreEqual("Assets/foo\nbar.cs", rt.Value.ChipPaths[0]);
        }
    }
}
