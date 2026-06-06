using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Standalone window for Agent Tool Permissions — deny-set configuration.
    /// Distinct from MCPToolSettingsWindow which controls what tools exist.
    /// </summary>
    public class MCPPermissionsWindow : EditorWindow
    {
        [MenuItem("MCP/Permissions", priority = 3)]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPPermissionsWindow>("MCP Permissions");
            window.minSize = new Vector2(320, 460);
            window.maxSize = new Vector2(600, 900);
        }

        public void CreateGUI()
        {
            var styleSheet = MCPEditorUtils.LoadStyleSheet("MCPSettings.uss");
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            var header = new Label("Agent Tool Permissions");
            header.AddToClassList("plugin-section-header");
            rootVisualElement.Add(header);

            var info = new Label("Controls which tools Agent Chat may call. " +
                                 "Tool Settings controls what tools exist.");
            info.AddToClassList("info-label");
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.marginBottom = 8;
            rootVisualElement.Add(info);

            var config = new PermissionConfig();
            var content = MCPSettingsPermUI.BuildContent(config);
            rootVisualElement.Add(content);
        }
    }
}
