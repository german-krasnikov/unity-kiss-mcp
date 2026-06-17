// IBackendProvider for kimi CLI (headless -p mode).
namespace UnityMCP.Editor.Chat
{
    internal sealed class KimiProvider : IBackendProvider
    {
        public string ProviderId  => "kimi";
        public string BinaryName  => "kimi";
        public string DisplayName => "Kimi";
        public int    SortOrder   => 30;

        public IChatBackend Create(BackendCreateArgs a)
        {
            var cfg = a.Store?.Kimi;
            return new KimiBackend(
                model:           cfg?.Model,
                approvalMode:    cfg?.ApprovalMode,
                extraArgs:       cfg?.ExtraArgs,
                resumeSessionId: a.ResumeSessionId);
        }
    }
}
