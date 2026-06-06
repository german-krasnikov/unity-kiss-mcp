using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public class MCPToolSettingsWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<MCPToolSettingsWindow>("MCP Tool Settings");
            window.minSize = new Vector2(320, 480);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var ss = MCPEditorUtils.LoadStyleSheet("MCPSettings.uss");
            if (ss != null) root.styleSheets.Add(ss);
            var hubSs = MCPEditorUtils.LoadStyleSheet("MCPHub.uss");
            if (hubSs != null) root.styleSheets.Add(hubSs);

            root.Add(ToolsHeaderAnim.Build(root));

            var scroll = new ScrollView();
            scroll.AddToClassList("tool-scroll");
            root.Add(scroll);

            MCPSettingsUI.BuildToolsSection(scroll);
        }
    }
}
