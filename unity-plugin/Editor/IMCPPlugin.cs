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
    }
}
