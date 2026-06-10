// TDD: SyncHelper — epoch, trigger, events, ISyncOps seam.
// All public APIs tested via mock ISyncOps injection.
// Run order: 1→2→3→4→5→6→7→8→9→10
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SyncHelperTests
    {
        private MockSyncOps _mock;

        [SetUp]
        public void SetUp()
        {
            _mock = new MockSyncOps();
            SyncHelper.Ops = _mock;
            SyncHelper.ResetForTest();
        }

        [TearDown]
        public void TearDown() => SyncHelper.ResetForTest();

        // #1: epoch starts at 0 after reset and can be read back
        [Test]
        public void Epoch_Survives_SessionState_Roundtrip()
        {
            Assert.AreEqual(0, SyncHelper.CurrentEpoch);
        }

        // #2: TriggerSync increments epoch
        [Test]
        public void TriggerSync_Increments_Epoch()
        {
            SyncHelper.TriggerSync(resolve: false);
            Assert.AreEqual(1, SyncHelper.CurrentEpoch);

            SyncHelper.TriggerSync(resolve: false);
            Assert.AreEqual(2, SyncHelper.CurrentEpoch);
        }

        // #3: TriggerSync calls Ops.Refresh()
        [Test]
        public void TriggerSync_Calls_Refresh()
        {
            SyncHelper.TriggerSync(resolve: false);
            Assert.AreEqual(1, _mock.RefreshCount);
        }

        // #4: TriggerSync calls Resolve BEFORE Refresh when requested
        [Test]
        public void TriggerSync_Calls_Resolve_Before_Refresh_When_Requested()
        {
            SyncHelper.TriggerSync(resolve: true);
            Assert.AreEqual(1, _mock.ResolveCount);
            Assert.IsTrue(_mock.ResolveCalledBeforeRefresh,
                "Resolve must be called before Refresh");
        }

        // #5: returns will_compile=true when IsCompiling after Refresh
        [Test]
        public void TriggerSync_Returns_WillCompile_True_When_Compiling()
        {
            _mock.IsCompilingAfterRefresh = true;
            var result = SyncHelper.TriggerSync(resolve: false);
            StringAssert.Contains("will_compile=true", result);
            StringAssert.Contains("epoch=1", result);
            StringAssert.StartsWith("sync_ack", result);
        }

        // #6: returns will_compile=false when idle
        [Test]
        public void TriggerSync_Returns_WillCompile_False_When_Idle()
        {
            _mock.IsCompilingAfterRefresh = false;
            var result = SyncHelper.TriggerSync(resolve: false);
            StringAssert.Contains("will_compile=false", result);
        }

        // #7: GetSyncStatus returns epoch and state
        [Test]
        public void GetSyncStatus_Returns_Epoch_And_State()
        {
            var status = SyncHelper.GetSyncStatus();
            StringAssert.StartsWith("epoch=", status);
            StringAssert.Contains("|state=", status);
        }

        // #8: compile failed fires OnSyncFailed
        [Test]
        public void CompileFailed_Fires_OnSyncFailed()
        {
            string firedWith = null;
            SyncHelper.OnSyncFailed += err => firedWith = err;

            _mock.ScriptCompilationFailedOnFinish = true;
            SyncHelper.TriggerSync(resolve: false);
            SyncHelper.SimulateCompilationFinished(); // test seam

            Assert.IsNotNull(firedWith, "OnSyncFailed must fire on compile failure");
        }

        // #9: IsCompileClean is false during compile
        [Test]
        public void IsCompileClean_False_During_Compile()
        {
            SyncHelper.SimulateCompilationStarted(); // test seam
            Assert.IsFalse(SyncHelper.IsCompileClean);
        }

        // #10: IsCompileClean is true after reload
        [Test]
        public void IsCompileClean_True_After_Reload()
        {
            SyncHelper.SimulateCompilationStarted();
            Assert.IsFalse(SyncHelper.IsCompileClean);

            SyncHelper.SimulateAfterAssemblyReload(); // test seam
            Assert.IsTrue(SyncHelper.IsCompileClean);
        }

        // #11: self-heal — will_compile was a false positive (no compile ever started):
        // after the grace period GetSyncStatus reports ready instead of compiling forever.
        [Test]
        public void GetSyncStatus_SelfHeals_When_Compile_Never_Started()
        {
            double fakeTime = 100.0;
            SyncHelper.NowSeconds = () => fakeTime;

            _mock.IsCompilingAfterRefresh = true; // will_compile=true at trigger time
            var ack = SyncHelper.TriggerSync(resolve: false);
            StringAssert.Contains("will_compile=true", ack);

            _mock.IsCompilingAfterRefresh = false; // ...but no compile ever starts
            fakeTime += SyncHelper.SelfHealGraceSeconds + 1.0;

            var status = SyncHelper.GetSyncStatus();
            StringAssert.Contains("state=ready", status);
            Assert.IsTrue(SyncHelper.IsCompileClean, "self-heal must restore clean flag");
        }

        // #12: no self-heal inside the grace window (R-1: compile may still be starting)
        [Test]
        public void GetSyncStatus_Stays_Compiling_Inside_Grace_Window()
        {
            double fakeTime = 100.0;
            SyncHelper.NowSeconds = () => fakeTime;

            _mock.IsCompilingAfterRefresh = true;
            SyncHelper.TriggerSync(resolve: false);
            _mock.IsCompilingAfterRefresh = false;

            fakeTime += SyncHelper.SelfHealGraceSeconds - 5.0;
            StringAssert.Contains("state=compiling", SyncHelper.GetSyncStatus());
        }

        // #13: no self-heal when a compile actually started — R-4 still owns the
        // ready transition (only afterAssemblyReload may report ready).
        [Test]
        public void GetSyncStatus_No_SelfHeal_When_Compile_Started()
        {
            double fakeTime = 100.0;
            SyncHelper.NowSeconds = () => fakeTime;

            _mock.IsCompilingAfterRefresh = true;
            SyncHelper.TriggerSync(resolve: false);
            _mock.IsCompilingAfterRefresh = false;

            SyncHelper.SimulateCompilationStarted(); // compile DID start
            fakeTime += SyncHelper.SelfHealGraceSeconds + 60.0;

            StringAssert.Contains("state=compiling", SyncHelper.GetSyncStatus(),
                "started compile must wait for reload/failed, never self-heal");
        }
    }

    // ── MockSyncOps ──────────────────────────────────────────────────────────

    public sealed class MockSyncOps : ISyncOps
    {
        public int RefreshCount { get; private set; }
        public int ResolveCount { get; private set; }
        public bool ResolveCalledBeforeRefresh { get; private set; }
        public bool IsCompilingAfterRefresh { get; set; }
        public bool ScriptCompilationFailedOnFinish { get; set; }

        public void Refresh()
        {
            if (ResolveCount > 0 && RefreshCount == 0)
                ResolveCalledBeforeRefresh = true;
            RefreshCount++;
        }

        public void Resolve() => ResolveCount++;

        public bool IsCompiling => IsCompilingAfterRefresh;
        public bool IsUpdating  => false;
        public bool ScriptCompilationFailed => ScriptCompilationFailedOnFinish;
    }
}
