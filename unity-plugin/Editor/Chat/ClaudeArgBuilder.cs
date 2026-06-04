// Pure arg-construction logic — no process spawning, fully NUnit-testable.
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.Chat
{
    public static class ClaudeArgBuilder
    {
        /// <summary>
        /// Build CLI args and the list of env keys to strip from the child process.
        /// </summary>
        /// <param name="binaryPath">Absolute path to the claude binary.</param>
        /// <param name="mcpConfigPath">Path to mcp.json passed via --mcp-config.</param>
        /// <param name="permissionMode">"plan" (Ask/read-only) or "acceptEdits" (Agent).</param>
        /// <param name="resumeSessionId">Non-null → append --resume &lt;id&gt;.</param>
        /// <param name="agentName">Non-null → append --agent &lt;name&gt;.</param>
        /// <param name="allowedMcpTools">
        ///   null  → blanket "mcp__unity" prefix (all tools allowed).
        ///   array → enumerate only these tool names prefixed with MCP_TOOL_PREFIX; empty = deny all.
        /// </param>
        public static (string[] args, string[] stripEnvKeys) Build(
            string binaryPath,
            string mcpConfigPath,
            string permissionMode,
            string resumeSessionId,
            string agentName = null,
            string[] allowedMcpTools = null,
            string appendSystemPrompt = null)
        {
            var args = new List<string>
            {
                "-p",
                "--output-format", "stream-json",
                "--verbose",
                "--include-partial-messages",
                "--input-format",  "stream-json",
                "--mcp-config",    mcpConfigPath,
                "--permission-mode", permissionMode,
            };

            // Tool allowlist: null → blanket prefix; non-null → enumerate explicitly.
            if (allowedMcpTools == null)
            {
                // Pre-approve all Unity MCP tools via server-key prefix (must match mcp.json key).
                args.Add("--allowedTools");
                args.Add(PermissionConfig.MCP_BLANKET);
            }
            else if (allowedMcpTools.Length > 0)
            {
                args.Add("--allowedTools");
                args.Add(string.Join(",",
                    allowedMcpTools.Select(t => PermissionConfig.MCP_TOOL_PREFIX + t)));
            }
            // empty array → omit --allowedTools entirely (all MCP tools denied)

            // AskUserQuestion auto-fails in headless stream-json (~500ms, no stdin wait) → force prose questions.
            args.Add("--disallowedTools");
            args.Add("AskUserQuestion");

            if (!string.IsNullOrEmpty(resumeSessionId))
            {
                args.Add("--resume");
                args.Add(resumeSessionId);
            }

            if (!string.IsNullOrEmpty(agentName))
            {
                args.Add("--agent");
                args.Add(agentName);
            }

            if (!string.IsNullOrEmpty(appendSystemPrompt))
            {
                args.Add("--append-system-prompt");
                args.Add(appendSystemPrompt);
            }

            return (args.ToArray(), new[] { "ANTHROPIC_API_KEY" });
        }
    }
}
