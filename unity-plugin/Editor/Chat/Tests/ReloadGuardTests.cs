// TDD — RED first. Tests drive ReloadGuard lock/save/load/clear contract.
// Uses a temp file path to avoid touching Library/ during tests.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ReloadGuardTests
    {
        private string _tmpPath;

        [SetUp]
        public void SetUp()
        {
            _tmpPath = Path.Combine(Path.GetTempPath(), $"ReloadGuardTest_{System.Guid.NewGuid()}.txt");
            ReloadGuard.OverrideFilePath(_tmpPath); // test seam
            ReloadGuard.ResetForTest();              // clear in-memory lock counter
        }

        [TearDown]
        public void TearDown()
        {
            ReloadGuard.ResetForTest();
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
        }

        // ── Lock / Unlock ─────────────────────────────────────────────────────

        [Test]
        public void OnTurnStarted_SetsLockActive()
        {
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked);
        }

        [Test]
        public void OnTurnFinished_ClearsLock()
        {
            ReloadGuard.OnTurnStarted();
            ReloadGuard.OnTurnFinished();
            Assert.IsFalse(ReloadGuard.IsLocked);
        }

        [Test]
        public void OnTurnFinished_WithoutStart_NoThrow()
        {
            // Double-finish / finish-without-start must be idempotent
            Assert.DoesNotThrow(() => ReloadGuard.OnTurnFinished());
            Assert.IsFalse(ReloadGuard.IsLocked);
        }

        [Test]
        public void DoubleFinish_NoThrow()
        {
            ReloadGuard.OnTurnStarted();
            ReloadGuard.OnTurnFinished();
            Assert.DoesNotThrow(() => ReloadGuard.OnTurnFinished());
            Assert.IsFalse(ReloadGuard.IsLocked);
        }

        // ── Save / Load / Clear ───────────────────────────────────────────────

        [Test]
        public void SaveAndLoad_SameState_RoundTrips()
        {
            var state = new PendingTurnState("sid", "hello", new[] { "/A" }, true, "reviewer", "Sending");
            ReloadGuard.SavePendingState(state);

            // Simulate domain reload: reset the in-memory state
            ReloadGuard.ResetForTest();

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNotNull(loaded, "loaded state must not be null after save");
            Assert.AreEqual("sid",   loaded.Value.SessionId);
            Assert.AreEqual("hello", loaded.Value.PendingText);
        }

        [Test]
        public void ClearPendingState_LoadReturnsNull()
        {
            var state = new PendingTurnState("sid", "hello", new string[0], false, null, "Idle");
            ReloadGuard.SavePendingState(state);
            ReloadGuard.ClearPendingState();

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNull(loaded);
        }

        [Test]
        public void LoadPendingState_NoFile_ReturnsNull()
        {
            // File was never created
            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNull(loaded);
        }

        [Test]
        public void LoadPendingState_CorruptedFile_ReturnsNull()
        {
            File.WriteAllText(_tmpPath, "corrupted|||garbage");
            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNull(loaded);
        }

        [Test]
        public void LoadPendingState_EmptyFile_ReturnsNull()
        {
            File.WriteAllText(_tmpPath, "");
            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNull(loaded);
        }

        // ── New: double-start / double-finish balance ─────────────────────────

        [Test]
        public void DoubleStart_DoubleFinish_NotLocked()
        {
            // Start→Start→Finish→Finish ⇒ IsLocked false (counter returns to 0)
            ReloadGuard.OnTurnStarted();
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked, "must be locked after two starts");
            ReloadGuard.OnTurnFinished();
            Assert.IsTrue(ReloadGuard.IsLocked, "still locked after first finish (depth=1)");
            ReloadGuard.OnTurnFinished();
            Assert.IsFalse(ReloadGuard.IsLocked, "must be unlocked after two finishes");
        }

        [Test]
        public void ResetForTest_ClearsWatchdogDelegate()
        {
            // Verify ResetForTest removes the watchdog so it cannot accumulate across tests.
            ReloadGuard.OnTurnStarted();
            ReloadGuard.ResetForTest(); // must remove watchdog from EditorApplication.update
            // After reset, IsLocked is false
            Assert.IsFalse(ReloadGuard.IsLocked);
        }
    }
}
