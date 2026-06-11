using System.IO;
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
            // Read PID from lockfile written by the Python server.
            // Lockfile format: ~/.unity-mcp/server-<port>.lock — PID at bytes 0-31 (UTF-8 decimal).
            // Source-of-truth: server/src/unity_mcp/lockfile.py
            var lockFile = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".unity-mcp", "server-9500.lock");

            if (!File.Exists(lockFile))
            {
                Debug.LogWarning("[MCP] Kill: lockfile not found — MCP server is not running");
                return;
            }

            var text = File.ReadAllText(lockFile).Trim().Split(new char[]{'\n','\r',' ','\0'}, 2)[0];
            if (!int.TryParse(text, out var pid))
            {
                Debug.LogWarning($"[MCP] Kill: cannot parse PID from lockfile ({text})");
                return;
            }

            try
            {
                System.Diagnostics.Process.GetProcessById(pid).Kill();
                Debug.Log($"[MCP] Kill: terminated PID {pid}");
            }
            catch (System.ArgumentException)
            {
                Debug.LogWarning($"[MCP] Kill: process {pid} not running (already stopped)");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MCP] Kill: failed to kill PID {pid} — {ex.Message}");
            }
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
