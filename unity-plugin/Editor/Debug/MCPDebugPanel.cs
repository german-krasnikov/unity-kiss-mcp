using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal sealed class MCPDebugPanel : EditorWindow
    {
        [MenuItem("MCP/Debug Panel", priority = 3)]
        static void Open() => GetWindow<MCPDebugPanel>("MCP Debug");

        private MCPDebugUI _ui;

        void CreateGUI()
        {
            _ui = new MCPDebugUI();
            _ui.Build(rootVisualElement);
        }
    }
}
