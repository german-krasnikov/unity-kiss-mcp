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

            var portField = new IntegerField("Port") { value = MCPServer.ServerPort };
            portField.AddToClassList("hub-port-label");
            section.Add(portField);

            var chatPortField = new IntegerField("Chat Port") { value = MCPServer.ServerChatPort };
            chatPortField.AddToClassList("hub-port-label");
            section.Add(chatPortField);

            var restartWarning = new Label("Restart required to apply") { visible = false };
            restartWarning.AddToClassList("hub-port-restart-warning");
            section.Add(restartWarning);

            portField.RegisterValueChangedCallback(e =>
            {
                var v = e.newValue;
                if (v < 1024 || v > 65535) { portField.SetValueWithoutNotify(e.previousValue); return; }
                if (v == chatPortField.value) { portField.SetValueWithoutNotify(e.previousValue); return; }
                MCPServer.SavePorts(v, chatPortField.value);
                restartWarning.visible = (v != MCPServer.ServerPort || chatPortField.value != MCPServer.ServerChatPort);
            });

            chatPortField.RegisterValueChangedCallback(e =>
            {
                var v = e.newValue;
                if (v < 1024 || v > 65535) { chatPortField.SetValueWithoutNotify(e.previousValue); return; }
                if (v == portField.value) { chatPortField.SetValueWithoutNotify(e.previousValue); return; }
                MCPServer.SavePorts(portField.value, v);
                restartWarning.visible = (portField.value != MCPServer.ServerPort || v != MCPServer.ServerChatPort);
            });

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
