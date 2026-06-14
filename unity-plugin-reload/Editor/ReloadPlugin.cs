// ReloadPlugin — [InitializeOnLoad] bootstrap for the reload mini-server.
// Starts ReloadMiniServer, persists port, registers cleanup on domain reload.
using System.Diagnostics;
using System.IO;
using UnityEditor;

namespace UnityMCP.Reload
{
    [InitializeOnLoad]
    public static class ReloadPlugin
    {
        private static int _pid;

        // Pure helper — no Unity API calls, fully unit-testable.
        public static bool ShouldStartReloadServer(bool isBatchMode, string[] commandLineArgs)
        {
            if (isBatchMode) return false;
            foreach (var arg in commandLineArgs)
                if (arg.IndexOf("AssetImportWorker", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            return true;
        }

        static ReloadPlugin()
        {
            _pid = Process.GetCurrentProcess().Id;

            if (!ShouldStartReloadServer(
                    UnityEngine.Application.isBatchMode,
                    System.Environment.GetCommandLineArgs()))
                return;

            var port = ReloadPortResolver.GetReloadPort();
            ReloadMiniServer.Start(port);

            var actualPort = ReloadMiniServer.ActualPort;
            if (actualPort > 0)
            {
                // F2: write port\nProjectDir\nProjectName for CWD-based disambiguation.
                // Application.dataPath is available on main thread here ([InitializeOnLoad]).
                var dataPath   = UnityEngine.Application.dataPath;
                var projectDir = Path.GetDirectoryName(dataPath) ?? "";
                var projectName = Path.GetFileName(projectDir);
                ReloadPortResolver.MergePersist(actualPort);
                ReloadPortResolver.WriteReloadPortFile(_pid, actualPort, projectDir, projectName);
            }

            ReloadCompileNotifier.RegisterEvents();

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.update += ProcessQueue;
        }

        private static void OnBeforeReload()
        {
            ReloadMiniServer.Stop();
            ReloadPortResolver.DeleteReloadPortFile(_pid);
        }

        private static void ProcessQueue()
        {
            ReloadCompileNotifier.UpdateCache();
            while (ReloadMiniServer.UpdateQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (System.Exception e) { UnityEngine.Debug.LogException(e); }
            }
        }
    }
}
