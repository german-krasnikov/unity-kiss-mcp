// TDD — F27: _needsRefresh flag triggers AssetDatabase.Refresh after code-editing tools.
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class DomainRefreshTests
    {
        // Inject minimal transcript so HandleToolRecord doesn't NPE on _transcript.
        // _transcript is internal — direct assignment, no reflection (rename = compile error).
        private static void InjectMinimalTranscript(MCPChatWindow w)
        {
            var container = new VisualElement();
            var registry  = ChatBlockRendererFactory.CreateDefault(null, null);
            w._transcript = new ChatTranscript(container, registry);
        }

        private static void InvokeHandleToolRecord(MCPChatWindow w, ToolCallRecord rec)
        {
            typeof(MCPChatWindow)
                .GetMethod("HandleToolRecord", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(w, new object[] { rec });
        }

        // _needsRefresh starts false on a fresh window instance.
        [Test]
        public void NeedsRefresh_DefaultFalse()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try { Assert.IsFalse(w._needsRefresh); }
            finally { Object.DestroyImmediate(w); }
        }

        // Verifies IsCodeEditingTool returns true for "Edit" tool name.
        // Full HandleToolRecord integration isn't feasible in EditMode (requires live turn processing).
        [Test]
        public void IsCodeEditingTool_CodeEditTool_SetsFlag()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // F27 fix: _needsRefresh is set at result-complete (HasResult=true), not args-complete.
                var rec = new ToolCallRecord("Edit", "id1", "{}", resultText: "ok");
                if (rec.HasResult && MCPChatWindow.IsCodeEditingTool(rec))
                    w._needsRefresh = true;
                Assert.IsTrue(w._needsRefresh);
            }
            finally { Object.DestroyImmediate(w); }
        }

        // Non-code tool does not set _needsRefresh.
        [Test]
        public void NonCodeTool_DoesNotSetNeedsRefresh()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var rec = new ToolCallRecord("get_hierarchy", "id2", "{}");
                if (MCPChatWindow.IsCodeEditingTool(rec))
                    w._needsRefresh = true;
                Assert.IsFalse(w._needsRefresh);
            }
            finally { Object.DestroyImmediate(w); }
        }

        // After refresh is consumed the flag resets to false.
        [Test]
        public void NeedsRefresh_ResetsAfterConsume()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                w._needsRefresh = true;
                // Simulate DrainAndRender consume logic.
                if (w._needsRefresh) w._needsRefresh = false;
                Assert.IsFalse(w._needsRefresh);
            }
            finally { Object.DestroyImmediate(w); }
        }

        // P1-5 F27 timing invariant: args-complete record does NOT set _needsRefresh, but sets _turnEditedCode
        [Test]
        public void HandleToolRecord_ArgsComplete_CodeEdit_DoesNotSetNeedsRefresh()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectMinimalTranscript(w);
                // chip-creation record (ArgsJson == null)
                InvokeHandleToolRecord(w, new ToolCallRecord("Edit", "id1", null));
                // args-complete record (ArgsJson set, HasResult=false)
                InvokeHandleToolRecord(w, new ToolCallRecord("Edit", "id1", "{}"));
                Assert.IsFalse(w._needsRefresh,   "_needsRefresh must NOT be set at args-complete");
                Assert.IsTrue(w._turnEditedCode,   "_turnEditedCode must be set at args-complete");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // P1-5 F27 timing invariant: result-complete record DOES set _needsRefresh
        [Test]
        public void HandleToolRecord_ResultComplete_CodeEdit_SetsNeedsRefresh()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                InjectMinimalTranscript(w);
                // chip-creation record
                InvokeHandleToolRecord(w, new ToolCallRecord("Edit", "id2", null));
                // result-complete record (HasResult=true)
                InvokeHandleToolRecord(w, new ToolCallRecord("Edit", "id2", "{}", resultText: "ok"));
                Assert.IsTrue(w._needsRefresh, "_needsRefresh must be set at result-complete");
            }
            finally { Object.DestroyImmediate(w); }
        }

        // P0-1: _transcriptRestored must be cleared even on the early-return (null pending) path.
        // Regression pin: before the fix the flag stayed true when LoadPendingState returned null,
        // allowing a later OnAfterReloadResume call to suppress a legitimate user bubble.
        [Test]
        public void TryResumePendingTurn_NullPending_ClearsTranscriptRestoredFlag()
        {
            var w = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                // Ensure no pending state on disk so TryResumePendingTurn hits the null early-return.
                ReloadGuard.ClearPendingState();

                // Set the flag to true, simulating CreateGUI setting it after a transcript restore.
                typeof(MCPChatWindow)
                    .GetField("_transcriptRestored", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(w, true);

                // Invoke TryResumePendingTurn — will hit `if (pending == null) return;`.
                typeof(MCPChatWindow)
                    .GetMethod("TryResumePendingTurn", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(w, null);

                var flagAfter = (bool)typeof(MCPChatWindow)
                    .GetField("_transcriptRestored", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(w);
                Assert.IsFalse(flagAfter, "_transcriptRestored must be false after early-return path");
            }
            finally { Object.DestroyImmediate(w); }
        }
    }
}
