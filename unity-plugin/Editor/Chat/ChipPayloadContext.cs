// Context passed to IChipKindProvider.FormatPayload.
// Core resolves the summary string before calling the provider.
namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Pre-resolved context for chip payload formatting.
    /// Depth is "path"/"summary"/"full"/"none". ResolvedSummary is empty when depth=="path".
    /// </summary>
    public readonly struct ChipPayloadContext
    {
        public readonly string Depth;
        public readonly string ResolvedSummary;

        public ChipPayloadContext(string depth, string resolvedSummary)
        {
            Depth           = depth           ?? "path";
            ResolvedSummary = resolvedSummary ?? "";
        }
    }
}
