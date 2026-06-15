// IBackendProvider for codex CLI (app-server mode).
namespace UnityMCP.Editor.Chat
{
    internal sealed class CodexProvider : IBackendProvider
    {
        public string ProviderId  => "codex";
        public string BinaryName  => "codex";
        public string DisplayName => "Codex";
        public int    SortOrder   => 10;

        public IChatBackend Create(BackendCreateArgs a)
            => new CodexAppServerBackend(a.ResumeSessionId,
                a.Store?.Codex.StartupTimeoutSec ?? 30,
                a.Store?.Codex.ExtraArgs);
    }
}
