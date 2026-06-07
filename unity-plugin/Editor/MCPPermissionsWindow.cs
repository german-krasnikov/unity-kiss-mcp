using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Permissions window — deny-set configuration for Agent Chat tools.
    /// </summary>
    public class MCPPermissionsWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<MCPPermissionsWindow>("MCP Permissions");
            window.minSize = new Vector2(320, 460);
            window.maxSize = new Vector2(600, 900);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var styleSheet = MCPEditorUtils.LoadStyleSheet("MCPSettings.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            var hubSs = MCPEditorUtils.LoadStyleSheet("MCPHub.uss");
            if (hubSs != null) root.styleSheets.Add(hubSs);

            root.Add(PermissionsHeaderAnim.Build(root));

            var info = new Label("Controls which MCP tools the in-Unity Chat agent may call. " +
                                 "Tool toggles are in the Settings hub.");
            info.AddToClassList("info-label");
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.marginBottom = 8;
            root.Add(info);

            var config = new PermissionConfig();
            root.Add(MCPSettingsPermUI.BuildContent(config));
        }
    }
}
