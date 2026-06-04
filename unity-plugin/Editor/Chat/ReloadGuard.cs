// Domain-reload-safe lock + pending-state persistence.
// Prevents assembly reload from killing a live turn; resumes it after reload.
// Unity-API calls (Lock/Unlock) are guarded by _locked counter so this class
// is also usable from unit tests via OverrideFilePath + ResetForTest.
using System;
using System.IO;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal static class ReloadGuard
    {
        // Default path — Library/ is local/gitignored in every Unity project.
        private static string _filePath = Path.Combine("Library", "MCP_ChatPendingTurn.txt");

        // Counter (not bool) so OnTurnFinished is always safe even if called extra times.
        private static int _lockDepth;

        // Watchdog: auto-unlock after ~120s to prevent a hung turn blocking all reloads.
        private static double _lockStartTime;
        private const double  WatchdogSeconds = 120.0;

        internal static bool IsLocked => _lockDepth > 0;

        // ── Lock / Unlock ─────────────────────────────────────────────────────

        internal static void OnTurnStarted()
        {
            if (_lockDepth == 0)
            {
                EditorApplication.LockReloadAssemblies();
                _lockStartTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += WatchdogTick;
            }
            _lockDepth++;
        }

        internal static void OnTurnFinished()
        {
            if (_lockDepth <= 0) return;
            _lockDepth--;
            if (_lockDepth == 0)
                ForceUnlock();
        }

        private static void ForceUnlock()
        {
            _lockDepth = 0;
            EditorApplication.update -= WatchdogTick;
            EditorApplication.UnlockReloadAssemblies();
        }

        private static void WatchdogTick()
        {
            if (_lockDepth <= 0)
            {
                EditorApplication.update -= WatchdogTick;
                return;
            }
            if (EditorApplication.timeSinceStartup - _lockStartTime > WatchdogSeconds)
                ForceUnlock();
        }

        // ── Pending state ─────────────────────────────────────────────────────

        internal static void SavePendingState(PendingTurnState state)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(_filePath, state.Serialize());
            }
            catch { /* never crash on reload path */ }
        }

        internal static PendingTurnState? LoadPendingState()
        {
            try
            {
                if (!File.Exists(_filePath)) return null;
                var raw = File.ReadAllText(_filePath);
                return PendingTurnState.Deserialize(raw);
            }
            catch
            {
                return null;
            }
        }

        internal static void ClearPendingState()
        {
            try { File.Delete(_filePath); } catch { }
        }

        // ── Test seams (no-op in production — only called from tests) ─────────

        internal static void OverrideFilePath(string path) => _filePath = path;

        internal static void ResetForTest()
        {
            // Reset in-memory counter without touching Unity API
            // (tests run without domain reload machinery).
            _lockDepth = 0;
            EditorApplication.update -= WatchdogTick; // prevent stale delegate accumulation across tests
        }
    }
}
