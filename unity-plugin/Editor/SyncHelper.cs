// SyncHelper — epoch, trigger, events, ISyncOps seam, IsCompileClean.
// public everywhere: Tests.dll must access all of this (CS0122 trap).
using System;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public static class SyncHelper
    {
        private const string EpochKey = "MCP_SyncEpoch";
        private const string CleanKey = "MCP_SyncClean";
        private const string StateKey = "MCP_SyncState";  // idle|compiling|ready|failed
        private const string ErrKey   = "MCP_SyncError";
        private const string TriggerTimeKey    = "MCP_SyncTriggerTime";
        private const string CompileStartedKey = "MCP_SyncCompileStarted";

        // Self-heal: will_compile can be a false positive (IsUpdating during import,
        // but no compile ever starts) — without a grace timeout the state would stay
        // "compiling" forever. 10s covers the Refresh→compilationStarted window.
        public const double SelfHealGraceSeconds = 10.0;

        // Injectable clock (timeSinceStartup survives domain reload, dies with the
        // editor — same lifetime as SessionState, so the pair stays consistent).
        public static Func<double> NowSeconds = () => EditorApplication.timeSinceStartup;

        // --- State ---
        public static int  CurrentEpoch   => SessionState.GetInt(EpochKey, 0);
        public static bool IsCompileClean => SessionState.GetBool(CleanKey, true);

        // --- Events ---
        public static event Action         OnSyncComplete;
        public static event Action<string> OnSyncFailed;

        // --- Injectable seam ---
        public static ISyncOps Ops { get; set; } = new UnitySyncOps();

        static SyncHelper()
        {
            CompilationPipeline.compilationStarted  += _ => OnCompileStarted();
            CompilationPipeline.compilationFinished += _ => OnCompileFinished();
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
        }

        // --- Called from CommandRouter ---
        public static string TriggerSync(bool resolve)
        {
            var epoch = CurrentEpoch + 1;
            SessionState.SetInt(EpochKey, epoch);
            SessionState.SetString(StateKey, "compiling");
            SessionState.SetBool(CleanKey, false);
            SessionState.SetFloat(TriggerTimeKey, (float)NowSeconds());
            SessionState.SetBool(CompileStartedKey, false);

            if (resolve) Ops.Resolve();
            Ops.Refresh();

            var willCompile = Ops.IsCompiling || Ops.IsUpdating;
            if (!willCompile)
            {
                // Refresh was a no-op — no compile will happen, no reload will happen.
                // Reset state to ready so sync_status doesn't stay stuck on "compiling".
                SessionState.SetString(StateKey, "ready");
                SessionState.SetBool(CleanKey, true);
            }
            return $"sync_ack|epoch={epoch}|will_compile={willCompile.ToString().ToLower()}";
        }

        public static string GetSyncStatus()
        {
            var epoch = CurrentEpoch;
            var state = SessionState.GetString(StateKey, "idle");
            if (state == "failed")
            {
                var err = SessionState.GetString(ErrKey, "");
                return $"epoch={epoch}|state=failed|err={err}";
            }
            // §3.3: include dur= when compiling (contract requirement)
            if (state == "compiling")
            {
                // Self-heal: trigger happened, grace expired, yet no compile ever
                // started and nothing is busy — the will_compile ack was a false
                // positive. Report ready instead of "compiling" forever (R-1 safe:
                // inside the grace window we still report compiling).
                var started = SessionState.GetBool(CompileStartedKey, false);
                var elapsed = NowSeconds() - SessionState.GetFloat(TriggerTimeKey, 0f);
                if (!started && !Ops.IsCompiling && !Ops.IsUpdating
                    && elapsed > SelfHealGraceSeconds)
                {
                    SessionState.SetString(StateKey, "ready");
                    SessionState.SetBool(CleanKey, true);
                    return $"epoch={epoch}|state=ready";
                }
                var dur = CompileNotifier.ElapsedSeconds;
                return $"epoch={epoch}|state=compiling|dur={dur.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}";
            }
            return $"epoch={epoch}|state={state}";
        }

        // --- Test seams (called from tests; in production, called via Unity events) ---
        public static void SimulateCompilationStarted() => OnCompileStarted();
        public static void SimulateCompilationFinished() => OnCompileFinished();
        public static void SimulateAfterAssemblyReload() => OnAfterReload();

        public static void ResetForTest()
        {
            SessionState.EraseInt(EpochKey);
            SessionState.EraseBool(CleanKey);
            SessionState.EraseString(StateKey);
            SessionState.EraseString(ErrKey);
            SessionState.EraseFloat(TriggerTimeKey);
            SessionState.EraseBool(CompileStartedKey);
            NowSeconds = () => EditorApplication.timeSinceStartup;
            OnSyncComplete = null;
            OnSyncFailed   = null;
        }

        // --- Private handlers ---
        private static void OnCompileStarted()
        {
            SessionState.SetBool(CleanKey, false);
            SessionState.SetString(StateKey, "compiling");
            SessionState.SetBool(CompileStartedKey, true);
        }

        private static void OnCompileFinished()
        {
            if (Ops.ScriptCompilationFailed)
            {
                SessionState.SetString(StateKey, "failed");
                var err = "script compilation failed";
                SessionState.SetString(ErrKey, err);
                OnSyncFailed?.Invoke(err);
            }
            // Success: don't write "ready" here — R-4 fix.
            // "ready" only written from afterAssemblyReload (new domain).
        }

        private static void OnAfterReload()
        {
            SessionState.SetBool(CleanKey, true);
            SessionState.SetString(StateKey, "ready");
            OnSyncComplete?.Invoke();
        }
    }

    // ── Interface (public for CS0122) ─────────────────────────────────────────

    public interface ISyncOps
    {
        void Refresh();
        void Resolve();
        bool IsCompiling { get; }
        bool IsUpdating  { get; }
        bool ScriptCompilationFailed { get; }
    }

    // ── Production impl (public for CS0122) ──────────────────────────────────

    public sealed class UnitySyncOps : ISyncOps
    {
        public void Refresh() => AssetDatabase.Refresh();
        public void Resolve() => UnityEditor.PackageManager.Client.Resolve();
        public bool IsCompiling          => EditorApplication.isCompiling;
        public bool IsUpdating           => EditorApplication.isUpdating;
        public bool ScriptCompilationFailed => EditorUtility.scriptCompilationFailed;
    }
}
