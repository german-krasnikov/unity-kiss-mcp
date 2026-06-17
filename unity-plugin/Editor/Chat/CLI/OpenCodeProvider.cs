// IBackendProvider for opencode CLI (spawn-per-turn, NDJSON format).
namespace UnityMCP.Editor.Chat
{
    internal sealed class OpenCodeProvider : IBackendProvider
    {
        public string ProviderId  => "opencode";
        public string BinaryName  => "opencode";
        public string DisplayName => "OpenCode";
        public int    SortOrder   => 40;

        public IChatBackend Create(BackendCreateArgs a)
        {
            var cfg = a.Store?.OpenCode;
            return new OpenCodeBackend(
                model:           cfg?.Model,
                skipPermissions: cfg?.SkipPermissions ?? true,
                extraArgs:       cfg?.ExtraArgs,
                resumeSessionId: a.ResumeSessionId);
        }
    }
}
