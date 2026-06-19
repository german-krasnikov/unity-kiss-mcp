using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>Contract for each page in the setup wizard.</summary>
    public interface IWizardScreen
    {
        string Title { get; }
        VisualElement Build();
        void OnEnter();  // called when screen becomes visible
        void OnExit();   // called when screen leaves
    }
}
