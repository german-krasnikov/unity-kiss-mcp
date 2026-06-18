// Holds the singleton preview context for the current domain.
// Disposed before domain reload to release EditorApplication.update hooks.
using System;
using System.Threading;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    // Default IPreviewContext implementation. Owned by PreviewLifetimeScope.
    internal sealed class PreviewContext : IPreviewContext, IDisposable
    {
        public IAssetPreviewService PreviewService { get; }
        public IChipExistenceService ExistenceService { get; }
        public CancellationToken CancellationToken { get; }

        readonly CancellationTokenSource _cts;

        public PreviewContext(IAssetPreviewService previewService, IChipExistenceService existenceService)
        {
            PreviewService   = previewService   ?? throw new ArgumentNullException(nameof(previewService));
            ExistenceService = existenceService ?? throw new ArgumentNullException(nameof(existenceService));
            _cts             = new CancellationTokenSource();
            CancellationToken = _cts.Token;
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            try { (PreviewService as IDisposable)?.Dispose(); } catch { }
            try { ExistenceService?.Dispose(); } catch { }
        }

        public void Cancel() => Dispose();
    }

    // v0.36.1 preview lifetime scope.
    [InitializeOnLoad]
    internal static class PreviewLifetimeScope
    {
        static PreviewContext _current;

        static PreviewLifetimeScope()
        {
            EditorApplication.delayCall += EnsureCurrent;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeCurrent;
        }

        public static IPreviewContext Current
        {
            get
            {
                EnsureCurrent();
                return _current;
            }
        }

        static void EnsureCurrent()
        {
            if (_current == null)
            {
                var previewService = new AssetPreviewService();
                var existenceService = new ChipExistenceService();
                _current = new PreviewContext(previewService, existenceService);
            }
        }

        static void DisposeCurrent()
        {
            _current?.Dispose();
            _current = null;
        }
    }
}
