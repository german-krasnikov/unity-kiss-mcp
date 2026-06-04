// InlineChipKeyHandler: wires TextField.ValueChanged → tracker sync → overlay refresh.
// Static Attach keeps MCPChatWindow coupling minimal.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class InlineChipKeyHandler
    {
        /// <summary>
        /// Wire the ValueChanged callback on <paramref name="field"/> so that
        /// any deletion of U+FFFC markers updates <paramref name="tracker"/> and
        /// rebuilds <paramref name="overlay"/>.
        /// </summary>
        internal static void Attach(
            TextField          field,
            InlineChipTracker  tracker,
            InlineChipOverlay  overlay)
        {
            field.RegisterValueChangedCallback<string>(evt =>
            {
                var removed = tracker.SyncToText(evt.previousValue, evt.newValue);
                if (removed.Count > 0)
                    overlay.Refresh(); // full rebuild is cheap for small chip counts
            });
        }
    }
}
