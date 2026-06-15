// TDD — P6: New Session / Clear.
// Tests 1-5: CliBackendBase.Stop() + fresh-construct idiom (replaces deleted NewSession seam)
// Tests 6-8: ChatTranscript.Clear()
// Tests 9-10: orchestration via direct field drive
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class NewSessionTests
    {
        private TestCliBackend _b;
        private string _tmpPath;

        [SetUp]
        public void SetUp()
        {
            _b = new TestCliBackend();
            ChatBinaryResolver.WhichOverride = _ => "/fake/test";
            ChatBinaryResolver.ResetCacheForTests();

            _tmpPath = Path.Combine(Path.GetTempPath(), $"NewSessionTest_{System.Guid.NewGuid()}.txt");
            ReloadGuard.OverrideFilePath(_tmpPath);
            ReloadGuard.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            ChatBinaryResolver.WhichOverride = null;
            ChatBinaryResolver.ResetCacheForTests();
            ReloadGuard.ResetForTest();
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
        }

        // Helper: set SessionId via TurnDone drain
        private void SetSessionId(string id)
        {
            _b.ParseLineFunc = (_, s) => { s.Add(ChatEvent.TurnDone(id, 0f, 0, 0)); return true; };
            _b.LinesToDrain.Enqueue("x");
            _b._proc = new ChatProcess();
            _b.DrainEvents(new List<ChatEvent>());
        }

        // ── Test 1: Stop clears _proc ─────────────────────────────────────────

        [Test]
        public void CliBackendBase_Stop_ClearsProc()
        {
            _b._proc = new ChatProcess();
            Assert.IsNotNull(_b._proc, "pre-condition: proc exists");

            _b.Stop();

            Assert.IsNull(_b._proc, "Stop must clear _proc (process killed)");
        }

        // ── Test 2: Stop leaves IsRunning false ──────────────────────────────

        [Test]
        public void CliBackendBase_Stop_IsRunningFalse()
        {
            _b._proc = new ChatProcess();
            _b.Stop();
            Assert.IsFalse(_b.IsRunning);
        }

        // ── Test 3: Fresh construction with null resumeId → Start sends null resumeId ─

        [Test]
        public void CliBackendBase_FreshConstruct_StartPassesNullResumeId()
        {
            // A brand-new TestCliBackend has SessionId=null (no resume).
            _b.Start();
            Assert.IsNull(_b.LastResumeId, "BuildArgs must receive null resumeId on fresh instance");
        }

        // ── Test 4: Stop is idempotent ────────────────────────────────────────

        [Test]
        public void CliBackendBase_Stop_Idempotent()
        {
            Assert.DoesNotThrow(() =>
            {
                _b.Stop();
                _b.Stop();
            });
        }

        // ── Test 5: After Stop, new Start passes null resumeId (session not restored) ─

        [Test]
        public void CliBackendBase_AfterStop_NextStartBuildsFreshArgs()
        {
            SetSessionId("old");
            // The production idiom for fresh session is CreateBackend() = construct new instance.
            // Stop alone does NOT clear SessionId — that is intentional (Stop ≠ new session).
            // This test verifies the *construction* path: new TestCliBackend has null SessionId.
            var fresh = new TestCliBackend();
            fresh.Start();
            Assert.IsNull(fresh.LastResumeId, "BuildArgs must receive null resumeId on fresh instance");
        }

        // ── Test 6: ChatTranscript.Clear removes all children ────────────────

        [Test]
        public void ChatTranscript_Clear_RemovesAllChildren()
        {
            var container = new VisualElement();
            var registry  = ChatBlockRendererFactory.CreateDefault(null, null);
            var t = new ChatTranscript(container, registry);

            t.AppendUserBubble("one");
            t.AppendUserBubble("two");
            t.AppendUserBubble("three");

            t.Clear();

            Assert.AreEqual(0, container.childCount);
        }

        // ── Test 7: ChatTranscript.Clear resets _msgCount ────────────────────

        [Test]
        public void ChatTranscript_Clear_ResetsMsgCount()
        {
            var container = new VisualElement();
            var registry  = ChatBlockRendererFactory.CreateDefault(null, null);
            var t = new ChatTranscript(container, registry);

            // Add 5 items, clear, then add 201 — eviction must start from 0
            for (int i = 0; i < 5; i++) t.AppendUserBubble($"msg{i}");
            t.Clear();

            // Now add 201 items — MaxMessages is 200, so exactly 1 eviction
            for (int i = 0; i < 201; i++) t.AppendUserBubble($"new{i}");
            // container.childCount should be 200 (201 added, 1 evicted)
            Assert.AreEqual(200, container.childCount);
        }

        // ── Test 8: ChatTranscript.Clear safe while streaming ────────────────

        [Test]
        public void ChatTranscript_Clear_SafeWhileStreaming()
        {
            var container = new VisualElement();
            var registry  = ChatBlockRendererFactory.CreateDefault(null, null);
            var t = new ChatTranscript(container, registry);

            t.AppendOrExtendAssistant("partial text");

            Assert.DoesNotThrow(() => t.Clear());
            Assert.AreEqual(0, container.childCount);

            // Streaming state fully reset — new stream must work
            Assert.DoesNotThrow(() =>
            {
                t.AppendOrExtendAssistant("new stream");
                t.FlushStreaming();
            });
        }

        // ── Test 9: NewSession clears ReloadGuard pending state ──────────────

        [Test]
        public void NewSession_ClearsPendingState()
        {
            var state = new PendingTurnState("sid", "hello", new[] { "/A" }, true, "reviewer", "Sending");
            ReloadGuard.SavePendingState(state);
            Assert.IsNotNull(ReloadGuard.LoadPendingState(), "pre-condition: state is saved");

            // Execute the NewSession reset sequence (the reloadGuard + backend parts)
            ReloadGuard.OnTurnFinished();
            _b.Stop();  // production: _backend?.Stop() then CreateBackend() — Stop is the kill step
            ReloadGuard.ClearPendingState();

            Assert.IsNull(ReloadGuard.LoadPendingState());
        }

        // ── Test 10: NewSession while active → activity becomes Idle ─────────

        [Test]
        public void NewSession_WhileActive_ActivityBecomesIdle()
        {
            var activity = new ChatActivityState();
            activity.Send(); // → Sending
            Assert.AreEqual(ActivityPhase.Sending, activity.Phase);

            // Execute the activity reset portion of NewSession
            ReloadGuard.OnTurnFinished();
            _b.Stop();
            if (activity.Phase != ActivityPhase.Idle)
                activity.Done();

            Assert.AreEqual(ActivityPhase.Idle, activity.Phase);
        }
    }
}
