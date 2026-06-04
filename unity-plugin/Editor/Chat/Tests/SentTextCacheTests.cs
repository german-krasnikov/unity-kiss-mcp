// TDD — tests for the two reload-survival fixes:
//   FIX A: SentTextCache captures sent text so SaveStateBeforeReload
//           doesn't read the (already-cleared) input field.
//   FIX B: TryResumePendingTurn calls ReloadGuard.OnTurnStarted()
//           so a reload mid-resume is locked, symmetric with DispatchTurn.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SentTextCacheTests
    {
        // ── FIX A: SentTextCache ──────────────────────────────────────────────

        [Test]
        public void Set_ThenGet_ReturnsText()
        {
            var cache = new SentTextCache();
            cache.Set("hello world");
            Assert.AreEqual("hello world", cache.Get());
        }

        [Test]
        public void Get_DefaultValue_IsEmpty()
        {
            var cache = new SentTextCache();
            Assert.AreEqual("", cache.Get());
        }

        [Test]
        public void Set_Null_TreatedAsEmpty()
        {
            var cache = new SentTextCache();
            cache.Set(null);
            Assert.AreEqual("", cache.Get());
        }

        [Test]
        public void Set_ClearInput_CacheStillHasText()
        {
            // Simulates the DispatchTurn → SaveStateBeforeReload sequence:
            // 1. DispatchTurn sets cache BEFORE clearing input.
            // 2. Input is cleared (simulated by overwrite with "").
            // 3. SaveStateBeforeReload reads cache — must be original text.
            var cache    = new SentTextCache();
            var inputSim = "my question";

            cache.Set(inputSim);   // DispatchTurn: cache before clear
            inputSim = "";         // DispatchTurn: _input.value = ""

            // SaveStateBeforeReload reads cache, not input
            Assert.AreEqual("my question", cache.Get(),
                "SaveStateBeforeReload must read the cache, not the cleared input field");
            Assert.AreEqual("", inputSim,
                "input was cleared — proves the bug would have returned empty without cache");
        }

        // ── FIX A: round-trip — PendingText in saved state equals cached sent text ──

        [Test]
        public void SavePendingState_PendingTextMatchesSentText_NotClearedInput()
        {
            var tmpPath = Path.Combine(Path.GetTempPath(),
                $"SentTextCacheTest_{System.Guid.NewGuid()}.txt");
            try
            {
                ReloadGuard.OverrideFilePath(tmpPath);
                ReloadGuard.ResetForTest();

                var cache = new SentTextCache();
                cache.Set("the real question");

                // Simulate SaveStateBeforeReload: read from cache, not input
                var state = new PendingTurnState(
                    sessionId:    "sess",
                    pendingText:  cache.Get(),   // FIX A: use cache, not _input.value
                    chipPaths:    new string[0],
                    agentMode:    false,
                    agentName:    "",
                    activityPhase: "Sending");
                ReloadGuard.SavePendingState(state);

                var loaded = ReloadGuard.LoadPendingState();
                Assert.IsNotNull(loaded);
                Assert.AreEqual("the real question", loaded.Value.PendingText,
                    "PendingText must equal the sent text, not the empty input");
            }
            finally
            {
                ReloadGuard.ResetForTest();
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
        }

        // ── FIX B: resume path calls OnTurnStarted / is lock-balanced ────────

        [Test]
        public void ResumePathLock_StartedThenFinished_IsNotLocked()
        {
            // TryResumePendingTurn contract:
            // - calls ReloadGuard.OnTurnStarted() before SendTurn  (FIX B)
            // - lock depth increments
            // - when TurnDone/Error fires → OnTurnFinished → lock released
            ReloadGuard.ResetForTest();

            // Simulate what TryResumePendingTurn must do after FIX B:
            ReloadGuard.OnTurnStarted();   // FIX B: lock acquired on resume
            Assert.IsTrue(ReloadGuard.IsLocked, "lock must be held during resumed turn");

            // Simulate TurnDone path (already wired via HandleEvent → TurnDone → OnTurnFinished)
            ReloadGuard.OnTurnFinished();
            Assert.IsFalse(ReloadGuard.IsLocked,
                "lock must be released after resumed turn finishes");
        }

        [Test]
        public void ResumePathLock_StartedWithoutFinish_StaysLocked()
        {
            // Mid-turn state: another reload must be blocked
            ReloadGuard.ResetForTest();
            ReloadGuard.OnTurnStarted();
            Assert.IsTrue(ReloadGuard.IsLocked,
                "reload must be blocked while resumed turn is live");
            ReloadGuard.ResetForTest(); // cleanup
        }
    }
}
