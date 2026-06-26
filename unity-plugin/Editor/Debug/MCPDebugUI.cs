using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    // Instance builder for MCPDebugPanel. Partial — watch rows, eval bar,
    // console preview, and add-watch form each live in their own file.
    internal partial class MCPDebugUI
    {
        // Refs to containers that RefreshAll() updates.
        private VisualElement _watchRowsContainer;
        private Label _consoleLabel;
        private Label _evalResultLabel;

        public void Build(VisualElement root)
        {
            var ss = MCPEditorUtils.LoadStyleSheet("Debug/MCPDebug.uss");
            if (ss != null) root.styleSheets.Add(ss);
            root.AddToClassList("mcp-debug-panel");

            root.Add(BuildAddWatch());

            _watchRowsContainer = new VisualElement();
            _watchRowsContainer.AddToClassList("watch-rows");
            root.Add(_watchRowsContainer);

            root.Add(BuildEvalBar());
            root.Add(BuildConsolePreview());

            RefreshAll();
            root.schedule.Execute(RefreshAll).Every(200);
        }

        internal void RefreshAll()
        {
            RefreshWatchRows();
            RefreshConsolePreview();
        }
    }
}
