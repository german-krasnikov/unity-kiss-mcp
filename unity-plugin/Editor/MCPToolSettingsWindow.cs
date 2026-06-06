using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool Settings window — shows tool enable/disable toggles, presets, search.
    /// Replaces the old MCPSettings EditorWindow (MCPSettings is now a static data class).
    /// </summary>
    public class MCPToolSettingsWindow : EditorWindow
    {
        [MenuItem("MCP/Tool Settings", priority = 2)]
        [MenuItem("MCP/Settings", priority = 2)]
        [MenuItem("Tools/MCP/Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPToolSettingsWindow>("MCP Tool Settings");
            window.minSize = new Vector2(320, 480);
        }

        public void CreateGUI()
        {
            MCPSettingsUI.Build(rootVisualElement);
        }
    }
}
