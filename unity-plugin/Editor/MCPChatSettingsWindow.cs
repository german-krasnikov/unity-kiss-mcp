using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public class MCPChatSettingsWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var w = GetWindow<MCPChatSettingsWindow>("MCP Chat Settings");
            w.minSize = new Vector2(340, 300);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var ss = MCPEditorUtils.LoadStyleSheet("MCPSettings.uss");
            if (ss != null) root.styleSheets.Add(ss);
            var hubSs = MCPEditorUtils.LoadStyleSheet("MCPHub.uss");
            if (hubSs != null) root.styleSheets.Add(hubSs);

            root.Add(ChatHeaderAnim.Build(root));

            if (ChatSettingsHook.HasConnectionSubscribers)
            {
                ChatSettingsHook.InvokeConnection(root);
            }
            else
            {
                var msg = new Label("Agent Chat is disabled.\nEnable it in MCP/Settings.");
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
