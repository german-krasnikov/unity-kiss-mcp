using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal partial class MCPDebugUI
    {
        private VisualElement BuildConsolePreview()
        {
            var container = new VisualElement();
            container.AddToClassList("console-preview");

            var header = new Label("Console (last 5)");
            header.AddToClassList("section-header");
            container.Add(header);

            _consoleLabel = new Label("(no logs)");
            _consoleLabel.AddToClassList("console-log");
            container.Add(_consoleLabel);

            return container;
        }

        private void RefreshConsolePreview()
        {
            if (_consoleLabel == null) return;
            var logs = ConsoleCapture.GetLogs(count: 5);
            _consoleLabel.text = string.IsNullOrEmpty(logs) ? "(no logs)" : logs;
        }
    }
}
