// Classifies tool names by risk level. Pure static — no UnityEngine deps, fully testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static class RiskClassifier
    {
        private static readonly Dictionary<string, RiskLevel> _exact =
            new Dictionary<string, RiskLevel>
            {
                { "Bash",        RiskLevel.High   },
                { "Write",       RiskLevel.High   },
                { "Edit",        RiskLevel.Medium },
                { "MultiEdit",   RiskLevel.Medium },
                { "NotebookEdit",RiskLevel.Medium },
                { "TodoWrite",   RiskLevel.Medium },
                { "Read",        RiskLevel.Low    },
                { "Glob",        RiskLevel.Low    },
                { "Grep",        RiskLevel.Low    },
                { "WebFetch",    RiskLevel.Low    },
                { "WebSearch",   RiskLevel.Low    },
                { "TodoRead",    RiskLevel.Low    },
            };

        public static RiskLevel Classify(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return RiskLevel.Medium;
            if (_exact.TryGetValue(toolName, out var level)) return level;
            if (toolName.StartsWith("mcp__")) return RiskLevel.Low;
            return RiskLevel.Medium;
        }
    }
}
