using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public class MCPStatusWindow : EditorWindow
    {
        private double _lastRepaintTime;
        private const double RepaintInterval = 1.0;

        [MenuItem("Tools/Unity MCP/Status")]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPStatusWindow>("MCP Status");
            window.minSize = new Vector2(220, 120);
        }

        private void OnEnable()
        {
            EditorApplication.update += ThrottledRepaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= ThrottledRepaint;
        }

        private void ThrottledRepaint()
        {
            if (EditorApplication.timeSinceStartup - _lastRepaintTime > RepaintInterval)
            {
                _lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("MCP Server Status", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var running = MCPServer.IsRunning;
            DrawStatusLine("Server", running, running ? $"Running :{MCPServer.ServerPort}" : "Stopped");

            var connected = MCPServer.IsClientConnected;
            DrawStatusLine("Client", connected, connected ? "Connected" : "Disconnected");

            EditorGUILayout.Space();

            if (GUILayout.Button("Restart Server"))
            {
                MCPServer.Stop();
                MCPServer.StartAsync();
            }

            if (GUILayout.Button("Kill MCP Processes"))
            {
                try { System.Diagnostics.Process.Start("pkill", "-f unity_mcp.server"); }
                catch (System.Exception e) { Debug.LogWarning($"[MCP] pkill failed: {e.Message}"); }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Reimport Plugin"))
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

        private static void DrawStatusLine(string label, bool ok, string text)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"{label}:", GUILayout.Width(50));

            var prevColor = GUI.color;
            GUI.color = ok ? Color.green : Color.red;
            GUILayout.Label("\u25CF", GUILayout.Width(16));
            GUI.color = prevColor;

            GUILayout.Label(text);
            EditorGUILayout.EndHorizontal();
        }
    }
}
