using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class UpdateDispatcher
    {
        internal static void DoUpdate(System.Action<bool> onComplete = null)
        {
            var ver = UpdateChecker.AvailableVersion;

            void OnDone(bool ok)
            {
                if (ok) UpdateChecker.ClearCache();
                onComplete?.Invoke(ok);
            }

            if (InstallSourceDetector.Detect() == InstallSourceDetector.Source.Local)
            {
                var root = InstallSourceDetector.LocalRepoRoot();
                if (root == null)
                {
                    Debug.LogWarning("[MCP Update] Local install but repo root not found. Pull manually.");
                    OnDone(false);
                    return;
                }
                LocalPluginUpdater.UpdateAsync(root, onProgress: m => Debug.Log("[MCP Update] " + m), onComplete: OnDone);
            }
            else
            {
                UpmPluginUpdater.Update(ver, OnDone);
            }
        }
    }
}
