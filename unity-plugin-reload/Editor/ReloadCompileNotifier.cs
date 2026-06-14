// ReloadCompileNotifier — tracks compilation state via CompilationPipeline events.
// Ported from unity-plugin/Editor/CompileNotifier.cs (lines 1-75).
// No [InitializeOnLoad] — event registration is done by ReloadPlugin (increment 3).
// All types public: CS0122 trap avoided (memory: feedback_compile_verify_test_assembly).
using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Reload
{
    public static class ReloadCompileNotifier
    {
        // SessionState keys — hardcoded strings matching main CompileNotifier.cs
        // (no reference to main constants — zero dep on UnityMCP.Editor).
        public const string StartKey    = "MCP_CompileStart";
        public const string DurationKey = "MCP_LastDuration";
        public const string FailedKey   = "MCP_CompileFailed";

        // Public SessionState key constants — for testability and DiagnoseCommand cache reads.
        public const string CompileErrorsSessionKey  = "MCP_CompileErrors";
        public const string SyncStateSessionKey      = "MCP_SyncState";
        public const string SyncEpochSessionKey      = "MCP_SyncEpoch";
        public const string SyncCompileStartedKey_SS = "MCP_SyncCompileStarted";
        public const string StampAtTriggerKey_SS     = "MCP_StampAtTrigger";
        public const string DomainStampKey_SS        = "MCP_DomainStamp";

        // Volatile cache — written from main thread, read from any thread.
        private static volatile bool   _cachedIsCompiling;
        private static volatile bool   _cachedStarted;
        private static volatile bool   _cachedCnActive;
        private static volatile string _cachedCompileErrors  = "";
        private static volatile string _cachedSyncState      = "unknown";
        private static volatile int    _cachedSyncEpoch;
        private static volatile string _cachedStampAtTrigger = "";
        private static volatile string _cachedDomainStamp    = "";
        private static volatile string _cachedStatus         = "idle-never|0";

        // F1/F7: project root cached from main thread — safe to read from ThreadPool workers.
        // Application.dataPath can only be called on main thread; diagnose is dispatched inline.
        private static volatile string _cachedProjectRoot    = "";

        // Thread-safe accessors (no Unity API calls).
        public static bool   CachedIsCompiling    => _cachedIsCompiling;
        public static bool   CachedStarted        => _cachedStarted;
        public static bool   CachedCnActive       => _cachedCnActive;
        public static string CachedCompileErrors  => _cachedCompileErrors;
        public static string CachedSyncState      => _cachedSyncState;
        public static int    CachedSyncEpoch      => _cachedSyncEpoch;
        public static string CachedStampAtTrigger => _cachedStampAtTrigger;
        public static string CachedDomainStamp    => _cachedDomainStamp;
        public static string CachedStatus         => _cachedStatus;

        /// <summary>Project root directory (parent of Assets/). Thread-safe.</summary>
        public static string CachedProjectRoot    => _cachedProjectRoot;

        /// <summary>
        /// Snapshots main-thread-only Unity APIs into volatile fields.
        /// Must be called from EditorApplication.update (main thread).
        /// </summary>
        public static void UpdateCache()
        {
            _cachedIsCompiling    = EditorApplication.isCompiling;
            _cachedCnActive       = SessionState.GetFloat(StartKey, 0f) > 0f;
            _cachedStarted        = SessionState.GetBool(SyncCompileStartedKey_SS, false);
            _cachedCompileErrors  = SessionState.GetString(CompileErrorsSessionKey, "");
            _cachedSyncState      = SessionState.GetString(SyncStateSessionKey, "unknown");
            _cachedSyncEpoch      = SessionState.GetInt(SyncEpochSessionKey, 0);
            _cachedStampAtTrigger = SessionState.GetString(StampAtTriggerKey_SS, "");
            _cachedDomainStamp    = SessionState.GetString(DomainStampKey_SS, "");
            _cachedStatus         = GetStatus();
            // F1/F7: cache dataPath from main thread (ThreadPool can't call Application.dataPath)
            var dataPath = UnityEngine.Application.dataPath;
            _cachedProjectRoot = !string.IsNullOrEmpty(dataPath)
                ? System.IO.Path.GetDirectoryName(dataPath) ?? "" : "";
        }

        // G14: latched-isCompiling ceiling (5 min). After this threshold GetStatus() returns
        // "idle-stale" to surface the wedge.
        public const float StaleCeilingSeconds = 300f;

        // Injectable clock seam for unit tests.
        public static Func<float> NowSecondsFloat = () => (float)EditorApplication.timeSinceStartup;

        // Called by ReloadPlugin [InitializeOnLoad] in increment 3.
        public static void RegisterEvents()
        {
            CompilationPipeline.compilationStarted += _ =>
            {
                SessionState.SetFloat(StartKey, NowSecondsFloat());
                SessionState.SetBool(FailedKey, false);
            };

            CompilationPipeline.compilationFinished += _ =>
            {
                var start = SessionState.GetFloat(StartKey, 0f);
                if (start > 0f)
                    SessionState.SetFloat(DurationKey, NowSecondsFloat() - start);
                SessionState.SetFloat(StartKey, 0f);
                if (EditorUtility.scriptCompilationFailed)
                    SessionState.SetBool(FailedKey, true);
            };
        }

        public static bool IsCompiling => SessionState.GetFloat(StartKey, 0f) > 0f;

        public static float ElapsedSeconds => IsCompiling
            ? NowSecondsFloat() - SessionState.GetFloat(StartKey, 0f)
            : 0f;

        public static float LastDurationSeconds => SessionState.GetFloat(DurationKey, 0f);

        public static string GetStatus()
        {
            if (IsCompiling)
            {
                var elapsed = ElapsedSeconds;
                if (elapsed > StaleCeilingSeconds)
                    return "idle-stale|" + elapsed.ToString("F1", CultureInfo.InvariantCulture);
                return "compiling|" + elapsed.ToString("F1", CultureInfo.InvariantCulture);
            }
            var last = LastDurationSeconds;
            var durStr = last > 0f ? last.ToString("F1", CultureInfo.InvariantCulture) : "0";
            if (SessionState.GetBool(FailedKey, false))
                return "idle-failed|" + durStr;
            if (last <= 0f && SessionState.GetFloat(StartKey, 0f) <= 0f)
                return "idle-never|0";
            return "idle|" + durStr;
        }
    }
}
