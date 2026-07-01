using System;

namespace UnityMCP.Editor
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MCPToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public bool Mutating { get; set; }
        public bool Runtime { get; set; }
        // CSV of param names, e.g. "path,type". AttributeScanner always forwards
        // Required/Optional as "" when left unset (never raw null) — an [MCPTool] method
        // can never be free-form. This closes the old bypass where an attribute-registered
        // command with no schema entry skipped validation entirely.
        public string Required { get; set; }
        public string Optional { get; set; }

        public MCPToolAttribute(string name, string description = "")
        {
            Name = name;
            Description = description;
        }
    }
}
