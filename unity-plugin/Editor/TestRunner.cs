using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#if UNITY_INCLUDE_TESTS
using UnityEditor.TestTools.TestRunner.Api;
#endif

namespace UnityMCP.Editor
{
    public static class TestRunner
    {
        private static int _isRunning;
        private static double _runStartedAt;
        private const string KeyPending = "UnityMCP_tests_pending";
        private const string KeyResults = "UnityMCP_test_results";
        private const string KeyStartTime = "UnityMCP_tests_start";
        private const double StaleTimeoutSec = 600.0; // 10 min max test run

        [InitializeOnLoadMethod]
        private static void ResetOnReload()
        {
            _isRunning = 0;
#if UNITY_INCLUDE_TESTS
            // Re-register callbacks if tests were running when domain reload occurred.
            // Unity Test Framework preserves execution state; only our callbacks are lost.
            if (!SessionState.GetBool(KeyPending, false)) return;
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultCollector(null, api, true));
#endif
        }

        /// <summary>Returns stored test results, "pending" if running, or "none" if no run.</summary>
        public static string GetResults()
        {
            if (SessionState.GetBool(KeyPending, false))
            {
                // Clear stale pending flag (crashed/cancelled test run)
                var start = SessionState.GetFloat(KeyStartTime, 0f);
                if (start > 0f && EditorApplication.timeSinceStartup - start > StaleTimeoutSec)
                {
                    SessionState.SetBool(KeyPending, false);
                    return "none (stale pending cleared)";
                }
                return "pending";
            }
            var r = SessionState.GetString(KeyResults, "");
            return string.IsNullOrEmpty(r) ? "none" : r;
        }

#if UNITY_INCLUDE_TESTS
        public static void Execute(string mode, Action<string> onComplete, string group = null, string filter = null)
        {
            if (_isRunning == 1 && EditorApplication.timeSinceStartup - _runStartedAt > 120.0)
                _isRunning = 0;

            if (System.Threading.Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                onComplete("Error: test run already in progress");
                return;
            }
            _runStartedAt = EditorApplication.timeSinceStartup;
            SessionState.SetBool(KeyPending, true);
            SessionState.SetFloat(KeyStartTime, (float)EditorApplication.timeSinceStartup);
            SessionState.SetString(KeyResults, "");

            try
            {
                // Unity Test Framework calls SaveCurrentModifiedScenesIfUserWantsTo
                // which shows a modal dialog on dirty untitled scenes. Pre-save to avoid.
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (scene.isDirty && string.IsNullOrEmpty(scene.path))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/TestsTemp"))
                        AssetDatabase.CreateFolder("Assets", "TestsTemp");
                    EditorSceneManager.SaveScene(scene, "Assets/TestsTemp/__mcp_test_temp.unity");
                }
                else if (scene.isDirty)
                    EditorSceneManager.SaveScene(scene);

                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                var collector = new ResultCollector(onComplete, api);
                api.RegisterCallbacks(collector);

                var f = new Filter { testMode = ParseMode(mode) };
                if (!string.IsNullOrEmpty(group)) f.groupNames = new[] { group };
                if (!string.IsNullOrEmpty(filter)) f.groupNames = filter.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                api.Execute(new ExecutionSettings(f));
            }
            catch (Exception e)
            {
                System.Threading.Interlocked.Exchange(ref _isRunning, 0);
                SessionState.SetBool(KeyPending, false);
                SessionState.SetFloat(KeyStartTime, 0f);
                onComplete($"Error: {e.Message}");
            }
        }

        private static TestMode ParseMode(string mode)
        {
            if (string.IsNullOrEmpty(mode) || mode == "EditMode")
                return TestMode.EditMode;
            if (mode == "PlayMode")
                return TestMode.PlayMode;
            Debug.LogWarning($"[MCP] Unknown test mode '{mode}', defaulting to EditMode");
            return TestMode.EditMode;
        }

        private class ResultCollector : ICallbacks
        {
            private readonly Action<string> _onComplete;
            private readonly TestRunnerApi _api;
            private readonly bool _destroyApi;
            private readonly List<TestCaseResult> _results = new List<TestCaseResult>();
            private DateTime _startTime;

            private struct TestCaseResult
            {
                public string Name;
                public string Status;
                public double Duration;
                public string Message;
            }

            public ResultCollector(Action<string> onComplete, TestRunnerApi api, bool destroyApi = false)
            {
                _onComplete = onComplete;
                _api = api;
                _destroyApi = destroyApi;
            }

            public void RunStarted(ITestAdaptor testsToRun) => _startTime = DateTime.Now;

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite)
                {
                    _results.Add(new TestCaseResult
                    {
                        Name = result.Test.FullName,
                        Status = result.TestStatus.ToString(),
                        Duration = result.Duration,
                        Message = result.Message
                    });
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _api.UnregisterCallbacks(this);
                if (_destroyApi) UnityEngine.Object.DestroyImmediate(_api);
                System.Threading.Interlocked.Exchange(ref _isRunning, 0);
                var formatted = FormatResults();
                SessionState.SetString(KeyResults, formatted);
                SessionState.SetBool(KeyPending, false);
                SessionState.SetFloat(KeyStartTime, 0f);
                try { _onComplete?.Invoke(formatted); }
                catch (Exception e) { Debug.LogException(e); }
            }

            private string FormatResults()
            {
                var elapsed = (DateTime.Now - _startTime).TotalSeconds;
                var passed = _results.Count(r => r.Status == "Passed");
                var failed = _results.Count(r => r.Status == "Failed");
                var skipped = _results.Count(r => r.Status == "Skipped");

                var sb = new StringBuilder();
                sb.AppendFormat("{0} tests: {1} passed", _results.Count, passed);
                if (failed > 0) sb.AppendFormat(", {0} FAILED", failed);
                if (skipped > 0) sb.AppendFormat(", {0} skipped", skipped);
                sb.AppendFormat(" ({0:F1}s)", elapsed);

                foreach (var r in _results.Where(r => r.Status == "Failed"))
                {
                    sb.AppendLine();
                    sb.AppendFormat("FAIL {0} ({1:F2}s)", r.Name, r.Duration);
                    if (!string.IsNullOrEmpty(r.Message))
                    {
                        sb.AppendLine();
                        sb.Append("  ").Append(r.Message);
                    }
                }

                return sb.ToString();
            }
        }
#else
        public static void Execute(string mode, Action<string> onComplete, string group = null, string filter = null)
        {
            onComplete("Error: com.unity.test-framework package not installed");
        }
#endif
    }
}
