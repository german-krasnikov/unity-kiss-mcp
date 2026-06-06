using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class MCPHubUI
    {
        public static void Build(VisualElement root)
        {
            var ss = MCPEditorUtils.LoadStyleSheet("MCPHub.uss");
            if (ss != null) root.styleSheets.Add(ss);
            root.AddToClassList("hub-root");

            root.Add(HubHeaderAnim.Build(root));
            root.Add(BuildGeneralSection());

            var divTop = MCPHubDivider.Build(root);
            root.Add(divTop);

            root.Add(HubCardButton.Build("⚙",  "Tools",         "Enable / disable MCP tools",
                MCPToolSettingsWindow.ShowWindow));
            root.Add(HubCardButton.Build("🔒", "Permissions",    "Agent tool deny-set",
                MCPPermissionsWindow.ShowWindow));
            root.Add(HubCardButton.Build("💬", "Chat Settings",  ChatCardSubtitle(),
                MCPChatSettingsWindow.ShowWindow));

            var divBot = MCPHubDivider.Build(root);
            root.Add(divBot);
        }

        private static VisualElement BuildGeneralSection()
        {
            var section = new VisualElement();
            section.AddToClassList("hub-section");

            var autoDiscard = new Toggle("Auto-discard scene on quit")
                { value = MCPSettings.AutoDiscardScene };
            autoDiscard.RegisterValueChangedCallback(e =>
                EditorPrefs.SetBool(MCPSettings.KeyAutoDiscard, e.newValue));
            section.Add(autoDiscard);

            var chatEnable = new Toggle("Enable Agent Chat")
                { value = ChatSettingsHook.IsChatEnabled() };
            chatEnable.tooltip = "Adds the UNITY_MCP_CHAT define — Unity recompiles on change.";
            chatEnable.RegisterValueChangedCallback(e => ChatSettingsHook.SetChatEnabled(e.newValue));
            section.Add(chatEnable);

            var port = new Label($"localhost:{MCPServer.ServerPort}");
            port.AddToClassList("hub-port-label");
            section.Add(port);

            return section;
        }

        // Sync read — no shell spawn: uses cached binary path + EditorPrefs auth key.
        private static string ChatCardSubtitle()
        {
            // TODO(F24d): refresh subtitle dynamically when auth probe completes
            if (!ChatSettingsHook.IsChatBinaryAvailable()) return "CLI not configured";
            var auth = EditorPrefs.GetString("UnityMCP_Chat_AuthStatus", "");
            return auth == "ok"   ? "Claude CLI · logged in"
                 : auth == "fail" ? "Claude CLI · not logged in"
                 : "Claude CLI · checking...";
        }
    }
}
