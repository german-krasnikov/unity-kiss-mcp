// Pure arg-construction logic — no process spawning, fully NUnit-testable.
using System.Collections.Generic;

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
        public static (string[] args, string[] stripEnvKeys) Build(
            string binaryPath,
            string mcpConfigPath,
            string permissionMode,
            string resumeSessionId)
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
                // Pre-approve the Unity MCP server. Headless `claude -p` has no interactive
                // permission prompt, so any un-allowlisted tool call silently blocks (built-in
                // tools like ToolSearch are auto-allowed, MCP ones are not). ASK stays read-only
                // because plan mode still blocks mutations on top of this allowlist.
                // Must match the server key in mcp.json ("unity").
                "--allowedTools", "mcp__unity",
                // built-in AskUserQuestion auto-fails in headless stream-json (~500ms, no stdin wait) -> force prose questions
                "--disallowedTools", "AskUserQuestion",
            };

            if (!string.IsNullOrEmpty(resumeSessionId))
            {
                args.Add("--resume");
                args.Add(resumeSessionId);
            }

            return (args.ToArray(), new[] { "ANTHROPIC_API_KEY" });
        }
    }
}
