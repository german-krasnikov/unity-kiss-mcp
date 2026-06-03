using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>Shared MCP action methods — used by status window and status bar widget.</summary>
    internal static class MCPActions
    {
        internal static void Restart()
        {
            MCPServer.Stop();
            MCPServer.StartAsync();
        }

        internal static void Kill()
        {
            try { System.Diagnostics.Process.Start("pkill", "-f unity_mcp.server"); }
            catch (System.Exception e) { Debug.LogWarning($"[MCP] pkill failed: {e.Message}"); }
        }

        internal static void Reimport()
        {
            var guids = AssetDatabase.FindAssets("t:asmdef", new[] { "Packages/com.unity-mcp.editor" });
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log("[MCP] Plugin reimported — recompiling...");
            }
            else
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Debug.Log("[MCP] AssetDatabase.Refresh forced");
            }
        }
    }
}
