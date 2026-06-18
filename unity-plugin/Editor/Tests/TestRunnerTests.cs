// TDD: TestRunner core-path tests — EditMode only, no UNITY_INCLUDE_TESTS required.
// Covers: registration, stale-timeout detection, concurrent-run guard,
//         GetResults state machine, and result format contract.
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunnerTests
    {
        // ── Reflection helpers ────────────────────────────────────────────────

        private static FieldInfo IsRunningField() =>
            typeof(TestRunner).GetField("_isRunning",
                BindingFlags.Static | BindingFlags.NonPublic);

        private const string KeyPending   = "UnityMCP_tests_pending";
        private const string KeyResults   = "UnityMCP_test_results";
        private const string KeyStartTime = "UnityMCP_tests_start";

        [SetUp]
        public void SetUp()
        {
            // Reset static state before each test
            IsRunningField()?.SetValue(null, 0);
            SessionState.SetBool(KeyPending, false);
            SessionState.SetString(KeyResults, "");
            SessionState.SetFloat(KeyStartTime, 0f);
        }

        [TearDown]
        public void TearDown() => SetUp(); // symmetric cleanup

        // ── 1. Registration ───────────────────────────────────────────────────

        [Test]
        public void TestRunner_IsRegistered()
            => Assert.IsTrue(CommandRegistry.IsRegistered("run_tests"),
                "run_tests must be registered in CommandRegistry");

        // ── 2. Stale-timeout detection ────────────────────────────────────────

        [Test]
        public void TestRunner_StaleTimeout_DetectsExpiredRun()
        {
            // Inject fake uptime via seam — test always runs regardless of actual Editor uptime
            TestRunner.GetTimeSinceStartup = () => 1000.0;
            try
            {
                // Start time = 1000 - 700 = 300, so elapsed = 700 > StaleTimeoutSec(600)
                const float fakeStart = 300f;
                SessionState.SetBool(KeyPending, true);
                SessionState.SetFloat(KeyStartTime, fakeStart);

                var result = TestRunner.GetResults();

                Assert.AreEqual("none (stale pending cleared)", result,
                    "Expired pending run must be cleared and reported as stale");
                Assert.IsFalse(SessionState.GetBool(KeyPending, true),
                    "KeyPending must be reset after stale detection");
            }
            finally
            {
                TestRunner.GetTimeSinceStartup = () => EditorApplication.timeSinceStartup;
            }
        }

        [Test]
        public void TestRunner_ActiveRun_ReturnsPending()
        {
            // Run started just now — should NOT be considered stale
            SessionState.SetBool(KeyPending, true);
            SessionState.SetFloat(KeyStartTime, (float)EditorApplication.timeSinceStartup);

            Assert.AreEqual("pending", TestRunner.GetResults());
        }

        // ── 3. Filter format — field exists on Execute signature ──────────────

        [Test]
        public void TestRunner_FilterParam_ExistsOnExecuteMethod()
        {
            // Verify the public Execute method accepts a filter parameter.
            // The actual pipe-split is internal; this guards against signature regressions.
            var method = typeof(TestRunner).GetMethod("Execute",
                BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(method, "Execute method must be public static");

            var parms = method.GetParameters();
            var filterParam = System.Array.Find(parms, p => p.Name == "filter");
            Assert.IsNotNull(filterParam, "Execute must have a 'filter' parameter");
            Assert.AreEqual(typeof(string), filterParam.ParameterType);
        }

        // ── 4. Concurrent run guard — via _isRunning reflection ───────────────

        [Test]
        public void TestRunner_ConcurrentRun_IsRunningFieldExists_AndIsInt()
        {
            var field = IsRunningField();
            Assert.IsNotNull(field, "_isRunning field must exist");
            Assert.AreEqual(typeof(int), field.FieldType,
                "_isRunning must be int (used with Interlocked.CompareExchange)");
        }

        [Test]
        public void TestRunner_ConcurrentRun_WhenRunningIsOne_GetResultsReturnsPending()
        {
            // Simulate _isRunning=1 with a fresh start time → GetResults reports pending
            IsRunningField().SetValue(null, 1);
            SessionState.SetBool(KeyPending, true);
            SessionState.SetFloat(KeyStartTime, (float)EditorApplication.timeSinceStartup);

            Assert.AreEqual("pending", TestRunner.GetResults(),
                "While _isRunning=1 and pending not stale, GetResults must return 'pending'");
        }

        // ── 5. Result format contract ─────────────────────────────────────────

        [Test]
        public void TestRunner_ResultFormat_IncludesPassCount()
        {
            const string stored = "5 tests: 4 passed, 1 FAILED (12.3s)\nFAIL SomeTest (0.01s)";
            SessionState.SetString(KeyResults, stored);
            SessionState.SetBool(KeyPending, false);

            var result = TestRunner.GetResults();

            StringAssert.Contains("passed", result, "Result must contain 'passed'");
            StringAssert.Contains("FAILED", result, "Result must contain 'FAILED'");
        }

        [Test]
        public void TestRunner_GetResults_WhenNoRun_ReturnsNone()
        {
            // Fresh state — no results stored, not pending
            Assert.AreEqual("none", TestRunner.GetResults());
        }

        [Test]
        public void TestRunner_GetResults_AfterResults_ReturnsStoredString()
        {
            const string fakeResult = "10 tests: 10 passed (2.0s)";
            SessionState.SetString(KeyResults, fakeResult);
            SessionState.SetBool(KeyPending, false);

            Assert.AreEqual(fakeResult, TestRunner.GetResults());
        }

        // ── 6. get_test_count registration ───────────────────────────────────

        [Test]
        public void GetTestCount_IsRegistered()
            => Assert.IsTrue(CommandRegistry.IsRegistered("get_test_count"),
                "get_test_count must be registered in CommandRegistry");

        [Test]
        public void GetTestCount_IsAllowedDuringCompile()
            => Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("get_test_count"),
                "get_test_count must be allowed during compile");

    }
}
