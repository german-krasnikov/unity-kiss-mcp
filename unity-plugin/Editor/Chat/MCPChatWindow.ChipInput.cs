// Partial MCPChatWindow — chip field wiring (Wave 0 replacement).
// Gutted from overlay/dirty-tick wiring to InlineChipField context menu wiring.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        /// <summary>
        /// Wire the InlineChipField context menu for "Add Selection to Context".
        /// Called from BuildInputArea. All dirty-tick / overlay wiring removed — InlineChipField
        /// is a composed VisualElement; no positioning required.
        /// </summary>
        internal void WireChipInput()
        {
            if (_chipField == null) return;
            _chipField.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(
                    "Add Selection to Context",
                    _ => InsertInlineChip(Selection.activeGameObject),
                    _ => Selection.activeGameObject != null
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);
            }));
        }
    }
}
