// Instance-based async asset preview service with cancellation support.
using System;
using System.Threading;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public interface IAssetPreviewService
    {
        /// <summary>Request a thumbnail for an asset. Callback runs on the main thread.</summary>
        void RequestPreview(string assetPath, Action<Texture2D> onDone, CancellationToken ct = default);

        /// <summary>Invalidate a single cached path.</summary>
        void Invalidate(string assetPath);

        /// <summary>Clear the entire cache and queue.</summary>
        void Clear();
    }
}
