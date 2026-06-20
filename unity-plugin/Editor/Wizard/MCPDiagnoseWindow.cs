using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace UnityMCP.Editor
{
    /// <summary>Standalone window that hosts MCPDiagnosePanel.</summary>
    [MovedFrom(autoUpdateAPI: true, sourceNamespace: "UnityMCP.Editor", sourceAssembly: "UnityMCP.Editor")]
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
