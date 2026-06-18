// Routes click gestures on a chip pill: single click = navigate, right-click = context menu.
// Preview panel accessible via right-click "Show Preview" in context menu.
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class ChipClickRouter
    {
        /// <summary>
        /// Registers click handlers on the pill: single click invokes navigateAction.
        /// Preview is accessible via right-click context menu (Show Preview).
        /// </summary>
        internal static void Register(VisualElement pill, ChipInlinePreviewPanel previewPanel,
            Action navigateAction)
        {
            if (pill == null) return;

            pill.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 1)
                    navigateAction?.Invoke();
            });
        }
    }
}
