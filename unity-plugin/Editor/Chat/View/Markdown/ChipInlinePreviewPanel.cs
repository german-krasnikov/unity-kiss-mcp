// Lazy-build toggle panel for inline chip previews.
// Sits as a sibling after the pill. First Toggle() builds + shows;
// subsequent calls toggle visibility without rebuilding.
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ChipInlinePreviewPanel : VisualElement
    {
        readonly string _kindKey;
        readonly string _path;
        readonly Action _navigateFallback;

        VisualElement _preview;
        bool _built;

        internal ChipInlinePreviewPanel(string kindKey, string path, Action navigateFallback)
        {
            _kindKey          = kindKey;
            _path             = path;
            _navigateFallback = navigateFallback;
            style.display     = DisplayStyle.None;
            AddToClassList("chip-inline-preview");
        }

        /// <summary>
        /// First call: builds preview lazily. If Build returns null, calls navigateFallback instead.
        /// Subsequent calls: toggle display Flex/None without rebuilding.
        /// </summary>
        internal void Toggle()
        {
            if (!_built)
            {
                _preview = InlinePreviewBuilder.Build(_kindKey, _path);
                _built   = true;

                if (_preview == null)
                {
                    _navigateFallback?.Invoke();
                    return;
                }

                Add(_preview);
            }

            if (_preview == null) return; // fallback was already invoked

            style.display = style.display == DisplayStyle.None
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        /// <summary>True when the panel is currently visible (display == Flex).</summary>
        internal bool IsVisible => style.display == DisplayStyle.Flex;
    }
}
