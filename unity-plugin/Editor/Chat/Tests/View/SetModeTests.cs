// TDD — Issue #6: SetMode must preserve SessionId when switching Ask ↔ Agent.
// SetMode captures _backend?.SessionId BEFORE Stop(), then calls CreateBackendWithSession(resumeId).
// The new ClaudeBackend stores resumeSessionId as its own SessionId, so we can assert equality.
#if UNITY_MCP_CHAT
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SetModeTests
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private static void InjectBackend(MCPChatWindow w, IChatBackend backend)
        {
            typeof(MCPChatWindow)
                .GetField("_backend", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(w, backend);
        }

        private static IChatBackend GetBackend(MCPChatWindow w) =>
            (IChatBackend)typeof(MCPChatWindow)
                .GetField("_backend", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(w);

        private static void CallSetMode(MCPChatWindow w, bool agentMode)
        {
            typeof(MCPChatWindow)
                .GetMethod("SetMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(w, new object[] { agentMode });
        }

        private static bool GetAgentMode(MCPChatWindow w) =>
            (bool)typeof(MCPChatWindow)
                .GetField("_agentMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(w);

        // Set SessionId on any CliBackendBase via reflection (protected set).
        private static void SetSessionId(CliBackendBase b, string id)
        {
            typeof(CliBackendBase)
                .GetProperty("SessionId", BindingFlags.Public | BindingFlags.Instance)
                .SetValue(b, id);
        }

        // ── Test 1: SetMode_SameMode_NoOp ────────────────────────────────────

        [Test]
        public void SetMode_SameMode_IsNoOp()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                bool before = GetAgentMode(w);
                var stopCount = 0;
                // No-op means _agentMode unchanged and no backend replacement.
                var backend = GetBackend(w);

                CallSetMode(w, before); // same mode → early return

                Assert.AreEqual(before,  GetAgentMode(w));
                Assert.AreSame(backend, GetBackend(w), "backend must not change on same-mode call");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Test 2: SetMode_PreservesSessionId ───────────────────────────────

        [Test]
        public void SetMode_PreservesSessionId_WhenSwitchingModes()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Arrange: inject a fake backend with a known SessionId
                var fakeBackend = new TestCliBackend();
                SetSessionId(fakeBackend, "sess-abc123");
                InjectBackend(w, fakeBackend);

                bool initialMode = GetAgentMode(w); // false (Ask)

                // Act: switch to opposite mode
                CallSetMode(w, !initialMode);

                // Assert: new backend's SessionId equals the preserved resumeId
                var newBackend = GetBackend(w);
                Assert.IsNotNull(newBackend, "new backend must be created");
                Assert.AreNotSame(fakeBackend, newBackend, "must replace backend");
                Assert.AreEqual("sess-abc123", newBackend.SessionId,
                    "SetMode must pass old SessionId to new backend (--resume)");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // ── Test 3: SetMode_NullSessionId_CreatesFreashBackend ───────────────

        [Test]
        public void SetMode_NullSessionId_DoesNotCrash()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Inject backend with null SessionId (no session yet)
                var fakeBackend = new TestCliBackend(); // SessionId defaults to null
                InjectBackend(w, fakeBackend);

                bool initialMode = GetAgentMode(w);
                Assert.DoesNotThrow(() => CallSetMode(w, !initialMode));

                // New backend is created with null resume → fresh session (correct)
                Assert.IsNotNull(GetBackend(w));
            }
            finally { Object.DestroyImmediate(w); }
        }
    }
}
#endif
