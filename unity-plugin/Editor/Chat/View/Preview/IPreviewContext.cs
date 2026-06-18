// Lifecycle context passed to every preview builder.
// Carries shared services and the cancellation token for the owning VisualElement.
using System.Threading;

namespace UnityMCP.Editor.Chat
{
    public interface IPreviewContext
    {
        IAssetPreviewService PreviewService { get; }
        IChipExistenceService ExistenceService { get; }
        CancellationToken CancellationToken { get; }
    }
}
