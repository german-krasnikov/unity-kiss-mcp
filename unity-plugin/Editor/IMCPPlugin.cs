using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public interface IMCPPlugin
    {
        string Name { get; }
        string CommandPrefix { get; }
        void RegisterCommands();
        void OnDomainReload();
        IReadOnlyList<string> AdditionalCommands => System.Array.Empty<string>();
        /// <summary>
        /// Returns the subcategory label for a given command, or null/empty
        /// to fall back to the plugin name (current layout — no migration needed).
        /// </summary>
        string GetToolSubcategory(string command) => null;
        /// <summary>Optional per-plugin settings VisualElement. Return null to skip.</summary>
        VisualElement BuildSettingsUI() => null;
        /// <summary>True if this plugin provides a settings UI. Override alongside BuildSettingsUI.</summary>
        bool HasSettingsUI => false;
        /// <summary>Short description shown on the plugin card in the Plugins settings page.</summary>
        string Description => "";
    }
}
