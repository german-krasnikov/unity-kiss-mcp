// IBackendProvider for Antigravity CLI (agy, headless -p mode, plain text output).
namespace UnityMCP.Editor.Chat
{
    internal sealed class AntigravityProvider : IBackendProvider
    {
        public string ProviderId  => "antigravity";
        public string BinaryName  => "agy";
        public string DisplayName => "Antigravity";
        public int    SortOrder   => 20;

        public IChatBackend Create(BackendCreateArgs a)
        {
            var cfg = a.Store?.Antigravity;
            return new AntigravityBackend(
                model:           cfg?.Model,
                approvalMode:    cfg?.ApprovalMode,
                sandbox:         cfg?.Sandbox ?? false,
                extraArgs:       cfg?.ExtraArgs,
                resumeSessionId: a.ResumeSessionId);
        }
    }
}
