// Public interface for extensible settings panel contributions.
// Implement and call SettingsProviderRegistry.Register() from [InitializeOnLoad].
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Contributes a foldout section to the Chat Settings page.
    /// Implement and call SettingsProviderRegistry.Register() from [InitializeOnLoad].
    /// </summary>
    public interface ISettingsProvider
    {
        /// <summary>Unique lowercase key. Must match ^[a-z0-9_]+$.</summary>
        string Key { get; }

        /// <summary>Foldout label shown in the settings panel.</summary>
        string DisplayName { get; }

        /// <summary>Lower = rendered first. Built-ins use 1000+. Third-party: pick > 1000.</summary>
        int Order { get; }

        /// <summary>Build and add controls to parent. Called once per settings panel open.</summary>
        void BuildUI(VisualElement parent);
    }
}
