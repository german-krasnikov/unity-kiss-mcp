// IBackendProvider for gemini CLI (headless -p mode).
namespace UnityMCP.Editor.Chat
{
    internal sealed class GeminiProvider : IBackendProvider
    {
        public string ProviderId  => "gemini";
        public string BinaryName  => "gemini";
        public string DisplayName => "Gemini";
        public int    SortOrder   => 20;

        public IChatBackend Create(BackendCreateArgs a)
        {
            var cfg = a.Store?.Gemini;
            return new GeminiBackend(
                model:        cfg?.Model,
                approvalMode: cfg?.ApprovalMode,
                sandbox:      cfg?.Sandbox ?? false,
                extraArgs:    cfg?.ExtraArgs,
                resumeSessionId: a.ResumeSessionId);
        }
    }
}
