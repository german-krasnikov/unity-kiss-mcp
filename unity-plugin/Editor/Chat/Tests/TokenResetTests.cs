// TDD — Token counter reset on backend/mode switch (F1).
// Uses reflection to drive MCPChatWindow without a live GUI panel.
#if UNITY_MCP_CHAT
using System.Reflection;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TokenResetTests
    {
        private static readonly FieldInfo s_inputTokens  = typeof(MCPChatWindow)
            .GetField("_inputTokens",  BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_outputTokens = typeof(MCPChatWindow)
            .GetField("_outputTokens", BindingFlags.NonPublic | BindingFlags.Instance);

        private MCPChatWindow CreateWindow()
        {
            // CreateInstance avoids full EditorWindow lifecycle (no OnEnable/CreateGUI).
            return UnityEngine.ScriptableObject.CreateInstance<MCPChatWindow>();
        }

        [Test]
        public void ResetTokenCounters_ZeroesBothFields()
        {
            var w = CreateWindow();
            s_inputTokens .SetValue(w, 500);
            s_outputTokens.SetValue(w, 300);

            w.ResetTokenCounters();

            Assert.AreEqual(0, (int)s_inputTokens .GetValue(w));
            Assert.AreEqual(0, (int)s_outputTokens.GetValue(w));
            UnityEngine.Object.DestroyImmediate(w);
        }

        [Test]
        public void ResetTokenCounters_ClearsLabel()
        {
            var w = CreateWindow();
            // _tokenReadout is null (no CreateGUI) — null guard must not throw.
            Assert.DoesNotThrow(() => w.ResetTokenCounters());
            UnityEngine.Object.DestroyImmediate(w);
        }

        [Test]
        public void SetMode_CallsResetTokenCounters()
        {
            var w = CreateWindow();
            s_inputTokens .SetValue(w, 100);
            s_outputTokens.SetValue(w, 200);

            // SetMode is private — invoke via reflection.
            var setMode = typeof(MCPChatWindow)
                .GetMethod("SetMode", BindingFlags.NonPublic | BindingFlags.Instance);
            // SetMode(false) — default _agentMode is false, so switch to true to trigger the body.
            setMode.Invoke(w, new object[] { true });

            Assert.AreEqual(0, (int)s_inputTokens .GetValue(w), "inputTokens zeroed");
            Assert.AreEqual(0, (int)s_outputTokens.GetValue(w), "outputTokens zeroed");
            UnityEngine.Object.DestroyImmediate(w);
        }

        [Test]
        public void SetMode_SameMode_NoReset()
        {
            var w      = CreateWindow();
            var setMode = typeof(MCPChatWindow)
                .GetMethod("SetMode", BindingFlags.NonPublic | BindingFlags.Instance);

            // Accumulate tokens, then call SetMode with the current mode (false = default).
            s_inputTokens .SetValue(w, 77);
            s_outputTokens.SetValue(w, 88);
            setMode.Invoke(w, new object[] { false }); // same as default → guard fires

            Assert.AreEqual(77, (int)s_inputTokens .GetValue(w), "no reset when mode unchanged");
            Assert.AreEqual(88, (int)s_outputTokens.GetValue(w), "no reset when mode unchanged");
            UnityEngine.Object.DestroyImmediate(w);
        }

        // ── AddRefToContext edge cases ────────────────────────────────────────

        [Test]
        public void AddRefToContext_EmptyPath_DoesNotThrow()
        {
            var w          = CreateWindow();
            var addRef     = typeof(MCPChatWindow)
                .GetMethod("AddRefToContext", BindingFlags.NonPublic | BindingFlags.Instance);

            // Empty string — guard must return early without throw.
            Assert.DoesNotThrow(() => addRef.Invoke(w, new object[] { "" }));
            UnityEngine.Object.DestroyImmediate(w);
        }

        [Test]
        public void AddRefToContext_NullPath_DoesNotThrow()
        {
            var w      = CreateWindow();
            var addRef = typeof(MCPChatWindow)
                .GetMethod("AddRefToContext", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.DoesNotThrow(() => addRef.Invoke(w, new object[] { null }));
            UnityEngine.Object.DestroyImmediate(w);
        }
    }
}
#endif
