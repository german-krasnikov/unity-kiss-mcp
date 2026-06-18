// Lazy-build toggle panel for inline chip previews.
// Uses PreviewBuilderRegistry + IPreviewContext. Cancels pending async work on detach.
using System;
using System.Threading;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ChipInlinePreviewPanel : VisualElement
    {
        readonly string _kindKey;
        readonly string _path;
        readonly Action _navigateFallback;
        readonly Action _pingAction;
        readonly IPreviewContext _context;

        VisualElement _preview;
        bool _built;
        CancellationTokenSource _cts;

        internal ChipInlinePreviewPanel(string kindKey, string path, Action navigateFallback,
            Action pingAction = null, IPreviewContext context = null)
        {
            _kindKey          = kindKey;
            _path             = path;
            _navigateFallback = navigateFallback;
            _pingAction       = pingAction;
            _context          = context ?? PreviewLifetimeScope.Current;
            style.display     = DisplayStyle.None;
            AddToClassList("chip-inline-preview");

            RegisterCallback<DetachFromPanelEvent>(_ => CancelAndDispose());
        }

        /// <summary>
        /// First call: builds preview lazily via PreviewBuilderRegistry. If null, calls navigateFallback.
        /// Subsequent calls: toggle display Flex/None without rebuilding.
        /// pingAction is invoked only on first show.
        /// </summary>
        internal void Toggle()
        {
            if (!_built)
            {
                _built = true;
                _preview = BuildPreview();

                if (_preview == null)
                {
                    _navigateFallback?.Invoke();
                    return;
                }

                Add(_preview);
                _pingAction?.Invoke();
            }

            if (_preview == null) return;

            style.display = style.display == DisplayStyle.None
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        /// <summary>True when the panel is currently visible (display == Flex).</summary>
        internal bool IsVisible => style.display == DisplayStyle.Flex;

        VisualElement BuildPreview()
        {
            CancelAndDispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(_context.CancellationToken);
            var localCtx = new ChildPreviewContext(_context, _cts.Token);
            var request = new PreviewRequest(_kindKey, _path);
            return PreviewBuilderRegistry.Build(request, localCtx);
        }

        void CancelAndDispose()
        {
            if (_cts == null) return;
            try { _cts.Cancel(); } catch { }
            try { _cts.Dispose(); } catch { }
            _cts = null;
        }

        /// <summary>Wraps the parent context with a panel-local cancellation token.</summary>
        sealed class ChildPreviewContext : IPreviewContext
        {
            public IAssetPreviewService PreviewService => _parent.PreviewService;
            public IChipExistenceService ExistenceService => _parent.ExistenceService;
            public CancellationToken CancellationToken { get; }

            readonly IPreviewContext _parent;

            public ChildPreviewContext(IPreviewContext parent, CancellationToken token)
            {
                _parent = parent;
                CancellationToken = token;
            }
        }
    }
}
