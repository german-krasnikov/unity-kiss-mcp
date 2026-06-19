namespace UnityMCP.Editor.Chat
{
    internal static class SessionHandoff
    {
        internal static string GetResumeCommand(BackendKind kind, string sessionId, string projectDir = null)
        {
            if (string.IsNullOrEmpty(sessionId)) return null;
            var prefix = !string.IsNullOrEmpty(projectDir)
                ? $"cd {projectDir} && "
                : "";
            return kind switch
            {
                BackendKind.Claude   => $"{prefix}claude --resume {sessionId}",
                BackendKind.Codex    => $"codex resume {sessionId}",
                BackendKind.OpenCode => $"opencode -s {sessionId}",
                BackendKind.Kimi     => $"kimi -S {sessionId}",
                BackendKind.Antigravity => $"agy --conversation {sessionId}",
                _                    => null,
            };
        }

        internal static string GetBinaryName(BackendKind kind) => kind switch
        {
            BackendKind.Claude   => "claude",
            BackendKind.Codex    => "codex",
            BackendKind.OpenCode => "opencode",
            BackendKind.Kimi     => "kimi",
            BackendKind.Antigravity => "agy",
            _                    => null,
        };
    }
}
