using System.Collections.Generic;

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
    }
}
