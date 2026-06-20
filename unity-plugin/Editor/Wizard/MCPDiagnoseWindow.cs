using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>Standalone window that hosts MCPDiagnosePanel.</summary>
    public class MCPDiagnoseWindow : EditorWindow
    {
        private void CreateGUI()
        {
            var ss = MCPEditorUtils.LoadStyleSheet("Wizard/SetupWizard.uss");
            if (ss != null) rootVisualElement.styleSheets.Add(ss);

            rootVisualElement.Add(MCPDiagnosePanel.Build());

            var banner = UpdateBanner.Build();
            if (banner != null)
                rootVisualElement.Add(banner);
        }
    }
}
