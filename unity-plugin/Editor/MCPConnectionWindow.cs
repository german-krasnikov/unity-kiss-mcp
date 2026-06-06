using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public class MCPConnectionWindow : EditorWindow
    {
        [MenuItem("MCP/Connection", priority = 4)]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPConnectionWindow>("MCP Connection");
            window.minSize = new Vector2(340, 300);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            var ss = MCPEditorUtils.LoadStyleSheet("MCPSettings.uss");
            if (ss != null) root.styleSheets.Add(ss);

            var header = new Label("MCP Connection");
            header.AddToClassList("plugin-section-header");
            root.Add(header);

            if (ChatSettingsHook.HasConnectionSubscribers)
            {
                ChatSettingsHook.InvokeConnection(root);
            }
            else
            {
                var msg = new Label("Agent Chat is disabled.\nEnable it in MCP/Tool Settings.");
                msg.style.whiteSpace = WhiteSpace.Normal;
                msg.style.marginBottom = 8;
                root.Add(msg);

                var btn = new Button(() => ChatSettingsHook.SetChatEnabled(true))
                    { text = "Enable Agent Chat" };
                root.Add(btn);
            }
        }
    }
}
