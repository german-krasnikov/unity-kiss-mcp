// Subscribes to ChatSettingsHook.OnBuildConnection and fills the MCPConnectionWindow
// with connection content (binary, auth, backends, chips) — no outer Foldout wrapper.
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class ChatConnectionSection
    {
        static ChatConnectionSection()
        {
            ChatSettingsHook.OnBuildConnection += Build;
        }

        private static void Build(VisualElement root) => ChatSettingsSection.BuildContent(root);
    }
}
