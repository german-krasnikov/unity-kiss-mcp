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

        public MCPToolAttribute(string name, string description = "")
        {
            Name = name;
            Description = description;
        }
    }
}
