using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class UpdateDispatcher
    {
        internal static void DoUpdate()
        {
            var ver = UpdateChecker.AvailableVersion;
            if (InstallSourceDetector.Detect() == InstallSourceDetector.Source.Local)
            {
                var root = InstallSourceDetector.LocalRepoRoot();
                if (root == null)
                {
                    Debug.LogWarning("[MCP Update] Local install but repo root not found. Pull manually.");
                    return;
                }
                LocalPluginUpdater.UpdateAsync(root, onProgress: m => Debug.Log("[MCP Update] " + m));
            }
            else
            {
                UpmPluginUpdater.Update(ver);
            }
        }
    }
}
